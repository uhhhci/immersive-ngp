using UnityEngine;
using UnityEngine.XR;
using AOT;
using System.IO;
using System.Runtime.InteropServices;
using System.Collections;
using Microsoft.MixedReality.Toolkit.UI;
using Microsoft.MixedReality.Toolkit.UI.BoundsControl;
using Microsoft.MixedReality.Toolkit.UI.BoundsControlTypes;
using System;

public class ExoStereoNeRFRenderer : MonoBehaviour
{
    [Header("Basic NERF Settings")]
    [Tooltip("Path to the image folder")]
    public string nerf_path = "Z:/Code/vrnerf/instant-ngp/data/nerf/fox";
    [Tooltip("Name of the model to load, should be with .msgpack extension")]
    public string model_name = "base.msgpack";
    public Material leftMaterial, rightMaterial;
    public GameObject leftImagePlane, rightImagePlane;
    public Camera MainCamera;
    public Camera NeRFLeftCam, NeRFRightCam;
    [Tooltip("If the extension should create depth map along with the images")]
    public bool useDepth = false;
    //public Camera UnityLeftRenderCam, UnityRightRenderCam;

    [Header("NGP Performance Settings")]
    [Tooltip("Using dlss is a must for reasonable VR performance")]
    public bool enableDlss = true;
    [Tooltip("Base Resolution")]
    public int width, height;


    [Header("VR NERF Settings")]
    //[Tooltip("Interpupil distance of your eyes")]
    //public float IPD = 0.063f; // here we use the average human IPD.
    [Tooltip("Automatically adjust the render FoV to the VR camera, if false, the FOV value falls back to user' settings. Note: for merging with the CAD model, we have to choose autoFoV if we need to have accurate alignment.")]
    public bool autoFoV = true;
    [Range(10, 120f)]
    [Tooltip("Field of view of the rendered NeRF scenes")]
    public float custom_fov = 50;


    [Header("Exocentric Manipulation Settings")]
    public GameObject transformBox;
    public Transform initCutoutBoxTransform, empty;
    public NeRFAABBCropBox aabbCropping;
    public GameObject originalBox;
    private BoundsControl bc;

    [Header("Other Feature Settings")]
    [Tooltip("Remove everythng on start: suitable for the ondemand painting mode")]
    public bool removeAllOnStart= false;
    [Tooltip("Set some initial transform if it is an object viewer")]
    public bool isObjectViewer = false;

    private float curr_scale_ratio = 1;
    private Vector3 initBoxPos = Vector3.zero;
    private Vector3 initBoxRot = Vector3.zero;
    private Vector3 init_aabb_min = Vector3.zero;
    private Vector3 init_aabb_max = Vector3.zero;

    private Vector3 curr_aabb_min = Vector3.zero;
    private Vector3 curr_aabb_max = Vector3.zero;
    private Vector3 curr_box_pos  = Vector3.zero;
    private Vector3 curr_box_rot  = Vector3.zero;
    

    private Vector3 render_center_offset = Vector3.zero;
    private Vector3 aabb_pos_change   = Vector3.zero;
    private float aabb_scale_change   = 1;

    // material & texture management
    public bool already_initalized = false;
    Texture2D leftTexture, rightTexture;
    Texture2D leftDepthTex, rightDeptTex;
    RenderTexture leftRenderTex, rightRenderTex;

    static System.IntPtr leftHandle  = System.IntPtr.Zero;
    static System.IntPtr rightHandle = System.IntPtr.Zero;
    static System.IntPtr leftHandleDepth  = System.IntPtr.Zero;
    static System.IntPtr rightHandleDepth = System.IntPtr.Zero;

    static bool texture_created = false;
    static bool graphics_initialized = false;

    private const int INIT_EVENT = 0x0001;
    private const int DRAW_EVENT = 0x0002;
    private const int DEINIT_EVENT = 0x0003;
    private const int CREATE_TEX = 0x0004;
    private const int DEINIT_VULKAN = 0x0005;

    private void Awake()
    {
        texture_created = false;
        already_initalized = false;
        float mainAspect = MainCamera.aspect;
        Debug.Log("width: " + (int)mainAspect * height);

        if (Directory.Exists(nerf_path) && File.Exists(Path.Combine(nerf_path, model_name)))
        {
            // set values for multi-threading rendering 
            NerfRendererPlugin.set_initialize_values(nerf_path, Path.Combine(nerf_path, model_name), enableDlss, useDepth, Mathf.RoundToInt(mainAspect * height), height);
            GL.IssuePluginEvent(NerfRendererPlugin.GetRenderEventFunc(), INIT_EVENT);

        }
        else
        {
            Debug.LogError("nerf model : " + Path.Combine(nerf_path, model_name) + " not found");

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
         Application.Quit();
#endif
        }

        // need to scale and update the handle of the NeRF Box
        bc = transformBox.GetComponent<BoundsControl>();

    }
    private void OnApplicationQuit()
    {
        if (already_initalized)
        {
            NeRFRendererCleanup();
        }
    }
    public void NeRFRendererCleanup()
    {
        GL.IssuePluginEvent(NerfRendererPlugin.GetRenderEventFunc(), DEINIT_EVENT);
        leftHandle = System.IntPtr.Zero;
        rightHandle = System.IntPtr.Zero;
        already_initalized = false;
        graphics_initialized = false;
        texture_created = false;
    }
    static void CleanupOnOnEditorQuit()
    {
        GL.IssuePluginEvent(NerfRendererPlugin.GetRenderEventFunc(), DEINIT_VULKAN);
    }

    public void SaveNeRFSnapshot()
    {
        try
        {
            // save the snapshot under the nerf path
            //string folder = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            //folder = string.Concat(folder.Split(Path.GetInvalidFileNameChars()));
            string path = Path.Combine(nerf_path, "unity.msgpack");
            //Directory.CreateDirectory(path);
            NerfRendererPlugin.save_snapshot(path);
        }
        catch
        {
            Debug.LogError("saving snapshot failed");
        }

    }

    #region Egocentric Manipulation
    public float getCurrScaleRatio()
    {
        return curr_scale_ratio;
    }

    public Vector3 getInitBoxPos()
    {
        return initBoxPos;
    }

    public Vector3 getInitBoxRot()
    {
        return initBoxRot;
    }
    public Vector3 getInitAABBMin()
    {
        return init_aabb_min;
    }

    public Vector3 getInitAABBMax()
    {
        return init_aabb_max;
    }

    public Vector3 getCurrAABBMin()
    {
        return curr_aabb_min;
    }

    public Vector3 getCurrAABBMax()
    {
        return curr_aabb_max;
    }

    private void initializeCutoutBox()
    {
        if (transformBox != null)
        {

            init_aabb_min = NerfRendererPlugin.getRenderAABBMin();
            init_aabb_max = NerfRendererPlugin.getRenderAABBMax();

            curr_aabb_min = init_aabb_min;
            curr_aabb_max = init_aabb_max;


            float[] arr = NerfRendererPlugin.getCropBoxTransform();

            // currently this way of calculation is okay. once rotation fo the bounding box set in , it needs to be adopted again..
            //Vector3 nerf_init_pos = 0.5f * (init_aabb_max + init_aabb_min);
            Vector3 nerf_init_pos   = new Vector3(arr[9], arr[10], arr[11]);
            Vector3 nerf_init_right = new Vector3(arr[0], arr[1], arr[2]);
            Vector3 nerf_init_up    = new Vector3(arr[3], arr[4], arr[5]);
            Vector3 nerf_init_for   = new Vector3(arr[6], arr[7], arr[8]);

            Vector3 nerf_init_scale = init_aabb_max - init_aabb_min;

            if (initCutoutBoxTransform != null)
            {
                Vector3 initpos = initCutoutBoxTransform.position;
                Vector3 initrot = initCutoutBoxTransform.rotation.eulerAngles;
                Vector3 initScale = initCutoutBoxTransform.localScale;
                transformBox.transform.position = initpos;
                transformBox.transform.rotation = Quaternion.Euler(initrot);
                transformBox.transform.localScale = initScale;

                empty.position = nerf_init_pos;
                empty.right = nerf_init_right;
                empty.up = nerf_init_up;
                empty.forward = nerf_init_for;
                empty.localScale = nerf_init_scale;
                Vector3 nerf_rot_euler = empty.rotation.eulerAngles;

                initBoxPos = nerf_init_pos;
                initBoxRot = nerf_rot_euler;

            }
            else if (isObjectViewer)
            {
                Vector3 initpos = new Vector3(0,0, 1.5f);
                Vector3 initrot = new Vector3(0, 180, 0);
                transformBox.transform.position = initpos;
                transformBox.transform.rotation = Quaternion.Euler(initrot);

                empty.position = nerf_init_pos;
                empty.right = nerf_init_right;
                empty.up = nerf_init_up;
                empty.forward = nerf_init_for;
                empty.localScale = nerf_init_scale;
                Vector3 nerf_rot_euler = empty.rotation.eulerAngles;

                initBoxPos = nerf_init_pos;
                initBoxRot = nerf_rot_euler;
                transformBox.transform.localScale = nerf_init_scale*0.6f;

            }
            else
            {

                transformBox.transform.position = nerf_init_pos;
                transformBox.transform.right = nerf_init_right;
                transformBox.transform.up = nerf_init_up;
                transformBox.transform.forward = nerf_init_for;
                transformBox.transform.localScale = nerf_init_scale;

                initBoxPos = transformBox.transform.position;
                initBoxRot = transformBox.transform.rotation.eulerAngles;
            }

            curr_box_pos = initBoxPos;
            curr_box_rot = initBoxRot;

            originalBox.transform.position   = initBoxPos;
            originalBox.transform.rotation   = Quaternion.Euler(initBoxRot);
            originalBox.transform.localScale = nerf_init_scale;
        }
        else
        {
            Debug.LogError("No Cutout box given to the StereoNerfRenderer, fail to begin exocentric manipulation!");
        }

    }
    public void enableExocentricManipulation()
    {
        if (transformBox != null)
        {
            transformBox.SetActive(true);
            aabbCropping.disableCropManipulation();
        }
        else
        {
            Debug.LogError("No Cutout box given to the StereoNerfRenderer, fail to begin exocentric manipulation!");
        }
    }
    public void disableExocentricManipulation()
    {
        transformBox.SetActive(false);
        //aabbCropping.disableCropManipulation();
        aabbCropping.NeRFAABBBox.SetActive(false);
    }

    public void resetNeRFCamera()
    {
        if (initCutoutBoxTransform != null)
        {
            Vector3 initpos = initCutoutBoxTransform.position;
            Vector3 initrot = initCutoutBoxTransform.rotation.eulerAngles;
            Vector3 initScale = initCutoutBoxTransform.localScale;
            transformBox.transform.position = initpos;
            transformBox.transform.rotation = Quaternion.Euler(initrot);
            transformBox.transform.localScale = initScale;
        }
        else
        {
            transformBox.transform.rotation = Quaternion.Euler(initBoxRot);
            transformBox.transform.position = initBoxPos;
        }

        transformBox.transform.localScale = init_aabb_max - init_aabb_min;
        curr_aabb_min = init_aabb_min;
        curr_aabb_max = init_aabb_max;
        aabb_pos_change = Vector3.zero;
        aabb_scale_change = 1;
        transformBox.SetActive(true);
        aabbCropping.disableCropManipulation();
        aabbCropping.resetCropping();

    }

    #endregion

    #region transformation management

    private void scaleBoxHandle()
    {
        float oldSize = bc.RotationHandlesConfig.HandleSize;
        bc.RotationHandlesConfig.HandleSize = oldSize / curr_scale_ratio;
        bc.ScaleHandlesConfig.HandleSize    = oldSize / curr_scale_ratio;
    }
    public Matrix4x4 translateCamera(Vector3 t, Matrix4x4 cam)
    {
        Matrix4x4 translate = Matrix4x4.Translate(t).transpose;
        return cam * translate;
    }

    public Matrix4x4 scaleCameraUniform(float scaleFactor, Matrix4x4 cam, Vector3 origin_offset)
    {
        Vector3 scale = new Vector3(1, 1, 1) * scaleFactor;
        Matrix4x4 scaleMatrix = Matrix4x4.Scale(scale).transpose;
        Matrix4x4 t1 = Matrix4x4.Translate(-origin_offset).transpose;
        Matrix4x4 t2 = Matrix4x4.Translate(origin_offset).transpose;

        return cam * t2 * scaleMatrix * t1;
    }
    // this is currently not useful as we don't want the object to look werid
    public Matrix4x4 scaleCameraNonUniform(Vector3 scale, Matrix4x4 cam, Vector3 origin_offset)
    {
        Matrix4x4 scaleMatrix = Matrix4x4.Scale(scale).transpose;
        Matrix4x4 t1 = Matrix4x4.Translate(-origin_offset).transpose;
        Matrix4x4 t2 = Matrix4x4.Translate(origin_offset).transpose;

        return cam * t2 * scaleMatrix * t1;
    }

    public Matrix4x4 rotateCamera(Vector3 rot, Matrix4x4 cam, Vector3 origin_offset, Vector3 axis_change)
    {

        Matrix4x4 rot_mat = Matrix4x4.Rotate(Quaternion.Euler(rot)).transpose;
        Matrix4x4 t1 = Matrix4x4.Translate(-origin_offset).transpose;
        Matrix4x4 t2 = Matrix4x4.Translate(origin_offset).transpose;
        // R1.T.-1 = R1
        Matrix4x4 r1 = Matrix4x4.Rotate(Quaternion.Euler(-axis_change));
        Matrix4x4 r2 = Matrix4x4.Rotate(Quaternion.Euler(axis_change));

        // so far cam*t2*r2* => rotate the camera back to the aabb bounding box axies, but actually, there is no such axies in instant-ngp
        // it is going to keep rotating around the global coordinate basis : / 
        // bUT Chat GPT gave us a trick , let's see if it works.

        //float[] arr = NerfRendererPlugin.getCropBoxTransform();
        //Vector3 init_right = new Vector3(arr[0], arr[1], arr[2]);
        //Vector3 init_up = new Vector3(arr[3], arr[4], arr[5]);
        //Vector3 init_forward = new Vector3(arr[6], arr[7], arr[8]);

        //Matrix4x4 _r = (xRotationMatrix * yRotationMatrix * zRotationMatrix).transpose;
        // https://stackoverflow.com/questions/14607640/rotating-a-vector-in-3d-space

        return cam * t2 * r2 * rot_mat * r1 * t1;
    }

    public Matrix4x4 trsCamera(Vector3 r, Vector3 t, float s, Matrix4x4 cam)
    {
        Vector3 scale = new Vector3(1, 1, 1) * s;
        Matrix4x4 sm = Matrix4x4.Scale(scale).transpose;
        Matrix4x4 tm = Matrix4x4.Translate(t).transpose;
        Matrix4x4 rm = Matrix4x4.Rotate(Quaternion.Euler(r)).transpose;
        return cam * tm * rm * sm;
    }

    private Vector3 getViewPos(Matrix4x4 cam)
    {
        return new Vector3(cam.m30, cam.m31, cam.m32);
    }
    private Vector3 getViewDir(Matrix4x4 cam)
    {
        return new Vector3(cam.m20, cam.m21, cam.m22);
    }

    private Vector3 getLookAt(Matrix4x4 cam)
    {
        return getViewPos(cam) + getViewDir(cam);
    }

    private Matrix4x4 getCameraMatrixNeRFStyle(Camera cam)
    {

        Vector3 up = cam.transform.up;
        Vector3 right = cam.transform.right;
        Vector3 forward = cam.transform.forward;
        Vector3 pos = cam.transform.position;

        Matrix4x4 _m_cam = new Matrix4x4();
        _m_cam.SetRow(0, new Vector4(right.x, right.y, right.z, 0));
        _m_cam.SetRow(1, new Vector4(up.x, up.y, up.z, 0));
        _m_cam.SetRow(2, new Vector4(forward.x, forward.y, forward.z, 0));
        _m_cam.SetRow(3, new Vector4(pos.x, pos.y, pos.z, 1));

        return _m_cam;
    }

    private float[] Matrix4fToArray(Matrix4x4 m)
    {
        // since eigen is column major, this data sequence can be directly loaded to create an eigen matrix
        float[] arr = new float[3 * 4]
        {
                m.m00, m.m01, m.m02,
                m.m10, m.m11, m.m12,
                m.m20, m.m21, m.m22,
                m.m30, m.m31, m.m32
        };

        return arr;
    }

    public void FillCamera(Camera cam, GameObject plane)
    {
        float pos = (cam.nearClipPlane + 0.01f);

        //  plane.transform.position = cam.transform.position + cam.transform.forward * pos;

        float h = Mathf.Tan(cam.fieldOfView * Mathf.Deg2Rad * 0.5f) * pos * 2f;

        plane.transform.localScale = new Vector3(h * cam.aspect, h, 1);
    }

    public void FitImagePlane(Camera cam, GameObject plane)
    {
        // similar to FillCamera(), but reduce the FoV
        float aspect = width / height;

        float pos = (cam.nearClipPlane + 0.01f);

        float h = Mathf.Tan(custom_fov * Mathf.Deg2Rad * 0.5f) * pos * 2f;

        plane.transform.localScale = new Vector3(h * aspect, h, 1);

        cam.transform.localRotation = Quaternion.identity;
    }

    public void SyncWithAABBCropBox()
    {
        // sychronize settings from the NeRFAABBCropBox manipulation
        curr_aabb_min = NerfRendererPlugin.getAABBMin();
        curr_aabb_max = NerfRendererPlugin.getAABBMax();
        curr_box_pos  = (curr_aabb_min + curr_aabb_max) * 0.5f;

        Vector3 last_sync_pos         = aabbCropping.getLastSyncBoxPos();
        Vector3 last_sync_scale       = aabbCropping.getLastSyncBoxScale();
        Vector3 curr_unity_aabb_pos   = aabbCropping.NeRFAABBBox.transform.position;
        Vector3 curr_unity_aabb_scale = aabbCropping.NeRFAABBBox.transform.localScale;

        aabb_pos_change   = curr_unity_aabb_pos - last_sync_pos;
        aabb_scale_change = curr_unity_aabb_scale.x / last_sync_scale.x;

        //transformBox.transform.localScale = curr_unity_aabb_scale;
        //transformBox.transform.position   = transformBox.transform.position - aabb_pos_change;
    }
    
    public float getIPDFromXRPlugin()
    {
        Vector3 leftEyePos  = InputDevices.GetDeviceAtXRNode(XRNode.LeftEye).TryGetFeatureValue(CommonUsages.leftEyePosition, out Vector3 leftEyePosValue) ? leftEyePosValue : Vector3.zero;
        Vector3 rightEyePos = InputDevices.GetDeviceAtXRNode(XRNode.RightEye).TryGetFeatureValue(CommonUsages.rightEyePosition, out Vector3 rightEyePosValue) ? rightEyePosValue : Vector3.zero;
        Quaternion leftRot  = InputDevices.GetDeviceAtXRNode(XRNode.LeftEye).TryGetFeatureValue(CommonUsages.leftEyeRotation, out Quaternion leftEyeRotValue) ? leftEyeRotValue : Quaternion.identity;
        Quaternion rightRot = InputDevices.GetDeviceAtXRNode(XRNode.RightEye).TryGetFeatureValue(CommonUsages.rightEyeRotation, out Quaternion rightEyeRotValue) ? rightEyeRotValue : Quaternion.identity;

        return (Quaternion.Inverse(rightRot) * rightEyePos).x - (Quaternion.Inverse(leftRot) * leftEyePos).x;
    }
    public void SetCustomFoV()
    {
    }
    #endregion

    #region depth test related
    private void createDepthRenderTexture(int __width, int __height)
    {
        // create render texture 
        RenderTextureFormat _format = RenderTextureFormat.Depth;
        

        leftRenderTex = new RenderTexture(__width, __height, 24, _format);
        rightRenderTex = new RenderTexture(__width, __height, 24, _format);

        leftRenderTex.Create();
        rightRenderTex.Create();
        // Unity camera settings
        NeRFLeftCam.depthTextureMode  = DepthTextureMode.Depth ;
        NeRFRightCam.depthTextureMode = DepthTextureMode.Depth;
       
        NeRFLeftCam.targetTexture  = leftRenderTex;
        NeRFRightCam.targetTexture = rightRenderTex;

        Shader.SetGlobalTexture("_LeftCameraDepthTexture", leftRenderTex);
        Shader.SetGlobalTexture("_RightCameraDepthTexture", rightRenderTex);

    }

    //private void createUnityCamRenderTexture(int __width, int __height)
    //{
    //    // create render texture 
    //    RenderTextureFormat _format = RenderTextureFormat.Default;


    //    leftRenderTex = new RenderTexture(__width, __height, 24, _format);
    //    rightRenderTex = new RenderTexture(__width, __height, 24, _format);

    //    leftRenderTex.Create();
    //    rightRenderTex.Create();
    //    // Unity camera settings

    //    UnityLeftRenderCam. targetTexture  = leftRenderTex;
    //    UnityRightRenderCam.targetTexture  = rightRenderTex;

    //    Shader.SetGlobalTexture("_RightUnitCamTex", rightRenderTex);
    //    Shader.SetGlobalTexture("_LeftUnitCamTex", leftRenderTex);

    //    UnityLeftRenderCam.transform.localPosition = Vector3.zero;
    //    UnityLeftRenderCam.transform.localRotation = Quaternion.identity;
    //    UnityRightRenderCam.transform.localPosition = Vector3.zero;
    //    UnityRightRenderCam.transform.localRotation = Quaternion.identity;

    //}

    private void renderDepthToTexture()
    {
        NeRFLeftCam.Render();
        NeRFRightCam.Render();
       // UnityLeftRenderCam.Render();
       // UnityRightRenderCam.Render();
    }
    #endregion
    void LateUpdate()
    {

        if (NerfRendererPlugin.get_graphics_init_state())
        {
            graphics_initialized = true;
        }

        if (graphics_initialized && !texture_created)
        {
            GL.IssuePluginEvent(NerfRendererPlugin.GetRenderEventFunc(), CREATE_TEX);
            texture_created = true;
        }

        if (rightHandle.ToInt32() == 0 || leftHandle.ToInt32() == 0 || leftHandleDepth.ToInt32() == 0 || rightHandleDepth.ToInt32() == 0)
        {

            rightHandle = NerfRendererPlugin.get_right_handle();
            leftHandle  = NerfRendererPlugin.get_left_handle();
            leftHandleDepth  = NerfRendererPlugin.get_left_depth_handle();
            rightHandleDepth = NerfRendererPlugin.get_right_depth_handle();
        }


        if (rightHandle.ToInt32() != 0 && leftHandle.ToInt32() != 0 && leftHandleDepth.ToInt32() != 0 && rightHandleDepth.ToInt32() != 0  && !already_initalized)
        {

            float IPD = getIPDFromXRPlugin();

            NeRFLeftCam.transform.localPosition     = new Vector3(-IPD / 2, 0, 0);
            NeRFRightCam.transform.localPosition    = new Vector3( IPD / 2, 0, 0);
           // leftImagePlane.transform.localPosition  = new Vector3(-IPD / 2, 0, 0.11f);
           // rightImagePlane.transform.localPosition = new Vector3( IPD / 2, 0, 0.11f);
            
            int _width;

            if (autoFoV)
            {
                float mainAspect = MainCamera.aspect;
                _width = (int)Mathf.Round(height * mainAspect);
                rightTexture = Texture2D.CreateExternalTexture(_width, height, TextureFormat.RGBAFloat, false, true, rightHandle);                
                leftTexture  = Texture2D.CreateExternalTexture(_width, height, TextureFormat.RGBAFloat, false, true, leftHandle);

                leftMaterial.mainTexture = leftTexture;
                rightMaterial.mainTexture = rightTexture;

                NeRFLeftCam.fieldOfView = MainCamera.fieldOfView;
                NeRFRightCam.fieldOfView = MainCamera.fieldOfView;
                NeRFLeftCam.aspect = mainAspect;
                NeRFRightCam.aspect = mainAspect;

                float init_fov = NeRFLeftCam.fieldOfView;
                NerfRendererPlugin.set_render_fov(init_fov);

                FillCamera(NeRFLeftCam, leftImagePlane);
                FillCamera(NeRFRightCam, rightImagePlane);
                custom_fov = init_fov;

            }
            else
            {
                float aspect = width / height;
                _width = width;
                rightTexture = Texture2D.CreateExternalTexture(_width, height, TextureFormat.RGBAFloat, false, true, rightHandle);
                leftTexture  = Texture2D.CreateExternalTexture(_width, height, TextureFormat.RGBAFloat, false, true, leftHandle);

                leftMaterial.mainTexture = leftTexture;
                rightMaterial.mainTexture = rightTexture;

                Shader.SetGlobalTexture("_RightNeRFMainTex", rightTexture);
                Shader.SetGlobalTexture("_LeftNeRFMainTex", leftTexture);

                NeRFLeftCam.fieldOfView = custom_fov;
                NeRFRightCam.fieldOfView = custom_fov;
                NeRFLeftCam.aspect = aspect;
                NeRFRightCam.aspect = aspect;

                NerfRendererPlugin.set_render_fov(custom_fov);

                FitImagePlane(NeRFLeftCam, leftImagePlane);
                FitImagePlane(NeRFRightCam, rightImagePlane);

            }

            if (useDepth)
            {

                // create depth texture for NeRF camera
                rightDeptTex = Texture2D.CreateExternalTexture(_width, height, TextureFormat.RFloat, false, true, rightHandleDepth);
                leftDepthTex = Texture2D.CreateExternalTexture(_width, height, TextureFormat.RFloat, false, true, leftHandleDepth);

                Shader.SetGlobalTexture("_RightNeRFDepth", rightDeptTex);
                Shader.SetGlobalTexture("_LeftNeRFDepth", leftDepthTex);


                //Shader.SetGlobalFloat("_FoVNeRFCam", custom_fov);
                //Shader.SetGlobalFloat("_FoVMainCam", MainCamera.fieldOfView);
                //Shader.SetGlobalFloat("_NeRFCamWidth", width);
                //Shader.SetGlobalFloat("_NeRFCamHeight", height);

                createDepthRenderTexture(_width, height);
                //createUnityCamRenderTexture(_width, height);
            }

            if (removeAllOnStart)
            {
                NerfRendererPlugin.set_all_density_grid_empty();
            }
            initializeCutoutBox();
            already_initalized = true;
           
        }

        if (!autoFoV)
        {
            // update image plane from FoV settings
            float aspect = width / height;

            NeRFLeftCam.fieldOfView  = custom_fov;
            NeRFRightCam.fieldOfView = custom_fov;
            NeRFLeftCam.aspect = aspect;
            NeRFRightCam.aspect = aspect;

            NerfRendererPlugin.set_render_fov(custom_fov);

            FitImagePlane(NeRFLeftCam, leftImagePlane);
            FitImagePlane(NeRFRightCam, rightImagePlane);
        }

        if (rightHandle.ToInt32() != 0 && leftHandle.ToInt32() != 0 && already_initalized)
        {


            render_center_offset = initBoxPos; //(init_aabb_min + init_aabb_max) * 0.5f;

            Matrix4x4 _m_cam_left  = getCameraMatrixNeRFStyle(NeRFLeftCam);
            Matrix4x4 _m_cam_right = getCameraMatrixNeRFStyle(NeRFRightCam);
            // link the nerf space transform to the box

            Shader.SetGlobalMatrix("_ProjectionMatrixLeft", NeRFLeftCam.projectionMatrix);
            Shader.SetGlobalMatrix("_WorldToCameraMatrixLeft", NeRFLeftCam.worldToCameraMatrix);
            Shader.SetGlobalMatrix("_ProjectionMatrixRight", NeRFRightCam.projectionMatrix);
            Shader.SetGlobalMatrix("_WorldToCameraMatrixRight", NeRFRightCam.worldToCameraMatrix);

            // translate
            Vector3 box_pos_unity = transformBox.transform.position ;
            Vector3 nerf_pos = initBoxPos - box_pos_unity ;

            _m_cam_left  = translateCamera(nerf_pos, _m_cam_left);
            _m_cam_right = translateCamera(nerf_pos, _m_cam_right);

            // rotate
            Vector3 box_rot_unity = transformBox.transform.rotation.eulerAngles;
            //Vector3 nerf_rot2 = -box_rot_unity;
            Vector3 nerf_rot  = initBoxRot - box_rot_unity;
            _m_cam_left  = rotateCamera(nerf_rot, _m_cam_left,  -render_center_offset, nerf_rot);
            _m_cam_right = rotateCamera(nerf_rot, _m_cam_right, -render_center_offset, nerf_rot);

            // uniform scale
            float box_scale_unity = (transformBox.transform.localScale.x); // /aabb_scale_change
            float init_box_scale  = (init_aabb_max - init_aabb_min).x;

            curr_scale_ratio = Mathf.Abs(init_box_scale / box_scale_unity);
            _m_cam_left  = scaleCameraUniform(curr_scale_ratio, _m_cam_left,  -render_center_offset);
            _m_cam_right = scaleCameraUniform(curr_scale_ratio, _m_cam_right, -render_center_offset);

            float[] camera_matrix_LEFT  = Matrix4fToArray(_m_cam_left);
            float[] camera_matrix_RIGHT = Matrix4fToArray(_m_cam_right);


            NerfRendererPlugin.update_stereo_view_matrix(camera_matrix_LEFT, camera_matrix_RIGHT);

            GL.IssuePluginEvent(NerfRendererPlugin.GetRenderEventFunc(), DRAW_EVENT);
            renderDepthToTexture();
            GL.InvalidateState();

            // make UI more comfortable to use
           //   scaleBoxHandle();
        }

    }

    private void OnDestroy()
    {
        if (leftRenderTex != null)
        {
            leftRenderTex.Release();
        }

        if(rightRenderTex != null)
        {
            rightRenderTex.Release();
        }
    }

}
