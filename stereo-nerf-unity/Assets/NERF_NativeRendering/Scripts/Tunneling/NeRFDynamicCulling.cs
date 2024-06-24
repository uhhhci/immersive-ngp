using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Microsoft.MixedReality.Toolkit.UI;
//using Tobii.XR;
//using Tobii.XR.GazeModifier;

// Note: setting up eye tracking with vive pro eye should follow this page for legacy VR: https://developer.tobii.com/xr/develop/unity/getting-started/vive-pro-eye/
public class NeRFDynamicCulling : MonoBehaviour
{
    // Start is called before the first frame update
    // main camera to track
    public Camera mainCamera;
    public ExoStereoNeRFRenderer neRFRenderer;

    [Header("Dynamic Tunneling")]
    public bool tunneling = false;
    public float nearClippingPlane = 0.4f;
    public float boxLength  = 3.0f;
    [Range(10, 60f)]
    [Tooltip("field of view of the rendering area")]
    public float FoV = 30;

    [Header("User Study")]
    [SerializeField]
    private float defaultMidFoveatedRadius = 0.26f;
    [SerializeField]
    private float defaultCentralFoveatedRadius = 0.18f;

    public Vector2 getFrustrumSize(float distance, Camera cam)
    {
        // calcuate the size of the camera frustrum based on distance to the camera
        //https://docs.unity3d.com/2019.1/Documentation/Manual/FrustumSizeAtDistance.html
        //float mergeCoff=leftEye.GetFloat("_CentralRadius") * 2;
        var frustumHeight = 2.0f * distance * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
       // Debug.Log("frustum Height: " + frustumHeight + "  frustum width: " + frustumHeight * cam.aspect);
        return new Vector2(frustumHeight * cam.aspect, frustumHeight);
    }

    public void setNeRFNearClippingPlane(SliderEventData sliderEventData)
    {
        nearClippingPlane = sliderEventData.NewValue / 2 ;
    }

    public void setNeRFFarClippingPlane(SliderEventData sliderEventData)
    {
        // the slider value always range from 0,1; therefore, has to scale it
        boxLength = sliderEventData.NewValue * 6;
    }

    public void setTunnelingFoV(SliderEventData sliderEventData)
    {
        neRFRenderer.custom_fov = sliderEventData.NewValue * 100;
    }

    public void enableDynamicTunneling()
    {
        tunneling = true;
        neRFRenderer.aabbCropping.enableCropManipulation();
        neRFRenderer.aabbCropping.enableRotate = false;

        neRFRenderer.leftMaterial.SetFloat("_MidFoveateRadius", defaultMidFoveatedRadius * 10);
        neRFRenderer.rightMaterial.SetFloat("_MidFoveateRadius", defaultMidFoveatedRadius * 10);

        neRFRenderer.leftMaterial. SetFloat("_CentralRadius", defaultCentralFoveatedRadius / 2);
        neRFRenderer.rightMaterial.SetFloat("_CentralRadius", defaultCentralFoveatedRadius / 2);
        neRFRenderer.disableExocentricManipulation();
    }

    public void opagueDynamicTunneling()
    {
        neRFRenderer.leftMaterial.SetFloat("_MidFoveateRadius", 9);
        neRFRenderer.rightMaterial.SetFloat("_MidFoveateRadius", 9);

        neRFRenderer.leftMaterial.SetFloat("_CentralRadius", 0.385f);
        neRFRenderer.rightMaterial.SetFloat("_CentralRadius", 0.385f);
    }
    public void disableDynamicTunneling()
    {
        tunneling = false;
        neRFRenderer.aabbCropping.disableCropManipulation();
    }

    public void FillCamera(Camera cam, GameObject plane)
    {
        float pos = (cam.nearClipPlane + 0.01f);

        //  plane.transform.position = cam.transform.position + cam.transform.forward * pos;

        float h = Mathf.Tan(cam.fieldOfView * Mathf.Deg2Rad * 0.5f) * pos * 2f;

        plane.transform.localScale = new Vector3(h * cam.aspect, h, 1);
    }
    private void setBoxLocalScaleFromFoV()
    {
        // calculate the scale of the box => need to revise the algorithm a bit here so that when scaling FoV the length does not change. 
        Vector2 size = getFrustrumSize(nearClippingPlane + boxLength, neRFRenderer.NeRFLeftCam);
        //Vector3 old_scale = neRFRenderer.aabbCropping.neRFRenderer.transform.localScale;
        neRFRenderer.aabbCropping.NeRFAABBBox.transform.localScale = new Vector3(size.x, size.y, boxLength);
    }
    void Start()
    {
         disableDynamicTunneling();

    }

    // Update is called once per frame
    void Update()
    {
        if (tunneling)
        {

            float box_center_z = nearClippingPlane + boxLength * 0.5f;

            neRFRenderer.aabbCropping.NeRFAABBBox.transform.position = mainCamera.transform.TransformPoint(new Vector3(0, 0, box_center_z));

            neRFRenderer.aabbCropping.NeRFAABBBox.transform.LookAt(mainCamera.transform);

            setBoxLocalScaleFromFoV();

        }
    }
}
