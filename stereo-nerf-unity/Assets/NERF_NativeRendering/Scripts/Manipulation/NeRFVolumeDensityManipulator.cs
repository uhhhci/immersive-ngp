using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.XR;
using Microsoft.MixedReality.Toolkit.UI;

public class NeRFVolumeDensityManipulator : MonoBehaviour
{
    [Header("Slider Control")]
    public bool SliderControl = false;
    public float MinSize = 0.2f;
    public float MaxSize = 2.5f;
    public GameObject RevealSphereCopy;
    public GameObject EraseSphereCopy;

    [Header("Basic Components")]
    public ExoStereoNeRFRenderer neRFRenderer;
    public GameObject XRLeftControllerGameobj;
    public GameObject XRRightControllerGameobj;
    public GameObject EraserSphere;
    public GameObject RevealSphere;
    public GameObject Setting;
    public float armDistance = 0.5f;

    private float reveal_radius = 1.0f;
    private float erase_radius  = 1.0f;

    private Vector3 init_reveal_sphere_size = Vector3.one * 0.07f;
    private Vector3 init_erase_sphere_size  = Vector3.one * 0.07f;

    private InputDevice XRLeftController;
    private InputDevice XRRightController;
    void GetLeftController()
    {
        var leftHandedControllers = new List<InputDevice>();
        var desiredCharacteristics = InputDeviceCharacteristics.HeldInHand | InputDeviceCharacteristics.Left | InputDeviceCharacteristics.Controller;
        InputDevices.GetDevicesWithCharacteristics(desiredCharacteristics, leftHandedControllers);
        foreach (var device in leftHandedControllers)
        {
            XRLeftController = device;
        }
    }

    void GetRightController()
    {
        var rightHandedControllers = new List<InputDevice>();
        var desiredCharacteristics = InputDeviceCharacteristics.HeldInHand | InputDeviceCharacteristics.Right | InputDeviceCharacteristics.Controller;
        InputDevices.GetDevicesWithCharacteristics(desiredCharacteristics, rightHandedControllers);


        foreach (var device in rightHandedControllers)
        {
            // Debug.Log(string.Format("Device name '{0}' has characteristics '{1}'", device.name, device.characteristics.ToString()));
            XRRightController = device;
        }

    }

    public Vector3 getEraserPosNGP(Vector3 eraserPosUnity)
    {
        Vector3 initBoxRot = neRFRenderer.getInitBoxRot();
        Quaternion tbox_r  = Quaternion.Euler(neRFRenderer.transformBox.transform.rotation.eulerAngles - initBoxRot);
        float scale_r      = neRFRenderer.getCurrScaleRatio();
        
        Vector3 render_cen_unity = neRFRenderer.transformBox.transform.position;
        
        Vector3 relative_diff_ngp = (Quaternion.Inverse(tbox_r)) * eraserPosUnity - (Quaternion.Inverse(tbox_r)) * render_cen_unity;
        return neRFRenderer.getInitBoxPos() + relative_diff_ngp * scale_r;
    }

    public void eraseSphereAtPoint(Vector3 pointPosUnity, float radius)
    {
        Vector3 pos_ngp = getEraserPosNGP(pointPosUnity);
        NerfRendererPlugin.mark_density_grid_empty(new float[3] { pos_ngp.x, pos_ngp.y, pos_ngp.z }, radius);
    }

    public void revealSphereAtPoint(Vector3 pointPosUnity, float radius)
    {
        Vector3 pos_ngp = getEraserPosNGP(pointPosUnity);
        NerfRendererPlugin.reveal_density_grid_area(new float[3] { pos_ngp.x, pos_ngp.y, pos_ngp.z }, radius);
        //NerfRendererPlugin.reveal_density_grid_in_box(new float[3] { pos_ngp.x, pos_ngp.y, pos_ngp.z }, radius, radius, radius);
    }
    #region MRTKInteraction
    public void setRevealerSize(SliderEventData sliderEventData)
    {
        reveal_radius = Mathf.Lerp(MinSize, MaxSize, sliderEventData.NewValue);
        
        // visual feedback
        float _ratio = reveal_radius / 0.5f;
        RevealSphereCopy.transform.localScale = (init_reveal_sphere_size * _ratio) / neRFRenderer.getCurrScaleRatio();

    }

    public void revealAllMaskedGrid()
    {
        NerfRendererPlugin.reveal_all_masked_density();
    }

    public void OnRevealSliderInteractionStart()
    {
        float _ratio = reveal_radius / 0.5f;
        RevealSphere.transform.localScale = (init_reveal_sphere_size * _ratio) / neRFRenderer.getCurrScaleRatio();

        RevealSphereCopy.SetActive(true);
    }

    public void OnRevealSliderInteractionEnd()
    {
        RevealSphereCopy.SetActive(false);
    }

    public void setEraserSize(SliderEventData sliderEventData)
    {
        erase_radius = Mathf.Lerp(MinSize, MaxSize, sliderEventData.NewValue);

        // visual feedback
        float _ratio = erase_radius / 0.5f;
        EraseSphereCopy.transform.localScale = (init_reveal_sphere_size * _ratio) / neRFRenderer.getCurrScaleRatio();
    }
    public void OnEraseSliderIntractionStart()
    {
        float _ratio = erase_radius / 0.5f;
        EraserSphere.transform.localScale = (init_reveal_sphere_size * _ratio) / neRFRenderer.getCurrScaleRatio();

        EraseSphereCopy.SetActive(true);
    }
    public void OnEraseSliderInteractionEnd()
    {
        EraseSphereCopy.SetActive(false);
    }
    public void hideAllDensityGrid()
    {
        NerfRendererPlugin.set_all_density_grid_empty();
    }

    #endregion

    void Start()
    {
        GetLeftController();       
        GetRightController();
    }

    // Update is called once per frame
    void Update()
    {
        if (XRRightControllerGameobj == null)
        {
            XRRightControllerGameobj = GameObject.Find("/MixedRealityPlayspace/Right_Right OpenVR Controller");
        }
        if( XRLeftControllerGameobj == null)
        {
            XRLeftControllerGameobj = GameObject.Find("/MixedRealityPlayspace/Left_Left OpenVR Controller");
        }

        if (XRLeftController != null && XRLeftControllerGameobj!= null)
        {

            // use left hand to do reveal
            Vector3 left_controller_pos     = XRLeftControllerGameobj.transform.position;
            Vector3 left_controller_forward = XRLeftControllerGameobj.transform.forward;
            Vector3 eraser_pos_unity = EraserSphere.transform.position = left_controller_pos + left_controller_forward * armDistance;

            bool secondaryButtonState, triggerButtonState, grabButtonState;
            XRLeftController.TryGetFeatureValue(CommonUsages.secondaryButton, out secondaryButtonState);
            XRLeftController.TryGetFeatureValue(CommonUsages.triggerButton, out triggerButtonState);
            XRLeftController.TryGetFeatureValue(CommonUsages.gripButton, out grabButtonState);

            if (secondaryButtonState)
            {
                //if (Setting != null)
                //{
                //    Setting.SetActive(false);
                //}

                EraserSphere.SetActive(true);

                if (!SliderControl)
                {
                    if (triggerButtonState)
                    {
                        erase_radius += 0.04f;

                    }

                    if (grabButtonState)
                    {

                        erase_radius -= 0.04f;
                    }
                }

                erase_radius = erase_radius > MaxSize ? MaxSize : erase_radius;
                erase_radius = erase_radius < MinSize ? MinSize : erase_radius;

                float _ratio = erase_radius / 0.5f;
                EraserSphere.transform.localScale = (init_reveal_sphere_size * _ratio) / neRFRenderer.getCurrScaleRatio();
                eraseSphereAtPoint(eraser_pos_unity, erase_radius);

            }
            else
            {
                //if (Setting != null)
                //{
                //    Setting.SetActive(true);
                //}
                EraserSphere.SetActive(false);
            }
        }
        else
        {
            GetLeftController();
        }


        if (XRRightController != null && XRRightControllerGameobj != null)
        {

            // use right hand to do coarse erase with larger radius
            // This might have some bugs to be changed.. 
            Vector3 right_controller_pos     = XRRightControllerGameobj.transform.position;
            Vector3 right_controller_forward = XRRightControllerGameobj.transform.forward;
            Vector3 reveal_pos_unity = RevealSphere.transform.position = right_controller_pos + right_controller_forward * armDistance;


            bool secondaryButtonState, triggerButtonState, grabButtonState;
            XRRightController.TryGetFeatureValue(CommonUsages.secondaryButton, out secondaryButtonState);
            XRRightController.TryGetFeatureValue(CommonUsages.triggerButton, out triggerButtonState);
            XRRightController.TryGetFeatureValue(CommonUsages.gripButton, out grabButtonState);

            if (secondaryButtonState)
            {
                //if (Setting != null)
                //{
                //    Setting.SetActive(false);
                //}
                RevealSphere.SetActive(true);

                if (!SliderControl)
                {
                    if (triggerButtonState)
                    {
                        reveal_radius += 0.04f;
                    }

                    if (grabButtonState)
                    {

                        reveal_radius -= 0.04f;
                    }
                }
                reveal_radius = reveal_radius > MaxSize ? MaxSize : reveal_radius;
                reveal_radius = reveal_radius < MinSize ? MinSize : reveal_radius;

                float _ratio = reveal_radius / 0.5f;
                RevealSphere.transform.localScale = (init_reveal_sphere_size * _ratio) / neRFRenderer.getCurrScaleRatio();
                revealSphereAtPoint(reveal_pos_unity, reveal_radius);
            }
            else
            {
                //if (Setting != null)
                //{
                //    Setting.SetActive(true);
                //}
                RevealSphere.SetActive(false);
            }
        }
        else
        {
            GetRightController();
        }

        

    }
}
