using AOT;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;
using System.Collections;

public class StereoNerfRenderer : MonoBehaviour
{
    [Header("Basic NERF Settings")]
    [Tooltip("Basic settings for NERF")]
    public int width;
    public int height;
    public bool enableDlss;
    public Material leftMaterial, rightMaterial;
    public string nerf_path = "Z:/Code/vrnerf/instant-ngp/data/nerf/fox";
    public Camera XRELeftEyeCamera;

    [Header("VR NERF Settings")]
    [Tooltip("Interpupil distance of your eyes")]
    public float IPD = 0.063f; // here we use the average human IPD.   

    // material & texture management
    Texture2D leftTexture, rightTexture;
    Transform XRRightEyeTransform;
    static System.IntPtr leftHandle = System.IntPtr.Zero;
    static System.IntPtr rightHandle = System.IntPtr.Zero;
    static bool already_initalized = false;
    static bool texture_created = false;
    static bool graphics_initialized = false;

    // camera view and transform management
    static Vector3 left_forwardPos;
    static Vector3 left_upPos;
    static Vector3 left_rightPos;
    static Vector3 left_positionPos;

    static Vector3 right_forwardPos;
    static Vector3 right_upPos;
    static Vector3 right_rightPos;
    static Vector3 right_positionPos;


    private const int INIT_EVENT = 0x0001;
    private const int DRAW_EVENT = 0x0002;
    private const int DEINIT_EVENT = 0x0003;
    private const int CREATE_TEX = 0x0004;
    private const int DEINIT_VULKAN = 0x0005;


    Transform defaultNerfCamTransform;

    private void Awake()
    {
        texture_created = false;
        already_initalized = false;

        if (Directory.Exists(nerf_path) && File.Exists(Path.Combine(nerf_path, "base.msgpack")))
        {
            // set values for multi-threading rendering 
            NerfRendererPlugin.set_initialize_values(nerf_path, Path.Combine(nerf_path, "base.msgpack"), enableDlss, false, width, height); ;
            GL.IssuePluginEvent(NerfRendererPlugin.GetRenderEventFunc(), INIT_EVENT);

        }
        else
        {
            Debug.LogError("nerf model : " + Path.Combine(nerf_path, "base.msgpack") + " not found");

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
         Application.Quit();
#endif
        }
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
        //NerfRendererPlugin.deinit_ngx_vulkan();
        // alternatively, can also do sequential cleanup
       // NerfRendererPlugin.destroy_texture(leftHandle);
       // NerfRendererPlugin.destroy_texture(rightHandle);
       // NerfRendererPlugin.deinitialize();
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
    public void updateNeRFCameraPoseKeyboard()
    {

    }
    public void updateNeRFCameraPoseHMD()
    {

        left_positionPos = XRELeftEyeCamera.transform.position;
        left_forwardPos = XRELeftEyeCamera.transform.forward;
        left_upPos = XRELeftEyeCamera.transform.up;
        left_rightPos = XRELeftEyeCamera.transform.right;

        XRRightEyeTransform = XRELeftEyeCamera.transform;
        XRRightEyeTransform.position = new Vector3(XRELeftEyeCamera.transform.position.x - IPD, XRELeftEyeCamera.transform.position.y, XRELeftEyeCamera.transform.position.z);
        right_positionPos = XRRightEyeTransform.transform.position;
        right_forwardPos = XRRightEyeTransform.transform.forward;
        right_upPos = XRRightEyeTransform.transform.up;
        right_rightPos = XRRightEyeTransform.transform.right;
       
    }


    void Update()
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

        if (rightHandle.ToInt32() == 0 || leftHandle.ToInt32() == 0)
        {
            rightHandle = NerfRendererPlugin.get_right_handle();
            leftHandle = NerfRendererPlugin.get_left_handle();
        }

        if (rightHandle.ToInt32() != 0 && leftHandle.ToInt32() != 0 && !already_initalized)
        {

            rightTexture = Texture2D.CreateExternalTexture(width, height, TextureFormat.RGBAFloat, false, true, rightHandle);
            rightMaterial.mainTexture = rightTexture;

            leftTexture = Texture2D.CreateExternalTexture(width, height, TextureFormat.RGBAFloat, false, true, leftHandle);
            leftMaterial.mainTexture = leftTexture;

            already_initalized = true;
        }


        if (rightHandle.ToInt32() != 0 && leftHandle.ToInt32() != 0 && already_initalized)
        {

            updateNeRFCameraPoseHMD();

            float[] camera_matrix_LEFT = new float[3 * 4] {
                    left_rightPos.x,    left_rightPos.y,    left_rightPos.z,
                    left_upPos.x,       left_upPos.y,       left_upPos.z,
                    left_forwardPos.x,  left_forwardPos.y,  left_forwardPos.z,
                    left_positionPos.x, left_positionPos.y, left_positionPos.z};

            float[] camera_matrix_RIGHT = new float[3 * 4] {
                    right_rightPos.x,    right_rightPos.y,    right_rightPos.z,
                    right_upPos.x,       right_upPos.y,       right_upPos.z,
                    right_forwardPos.x,  right_forwardPos.y,  right_forwardPos.z,
                    right_positionPos.x, right_positionPos.y, right_positionPos.z
                };

            NerfRendererPlugin.update_stereo_view_matrix(camera_matrix_LEFT, camera_matrix_RIGHT);

            GL.IssuePluginEvent(NerfRendererPlugin.GetRenderEventFunc(), DRAW_EVENT);
            GL.InvalidateState();

        }

    }



}
