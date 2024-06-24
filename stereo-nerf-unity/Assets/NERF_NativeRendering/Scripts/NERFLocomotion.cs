using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

public class NERFLocomotion : MonoBehaviour
{
    [Header("Basic Settings")]
    public GameObject HeadObj;
    public CharacterController characterController;

    [Header("VR Locomotion")]
    [Tooltip("Locomotion settings for VR uses")]
    public bool enabledContinuousLocomotion = true;
    public bool flipDirection = false;
    [SerializeField]
    private float speed = 1f;

    private InputDevice XRLeftController;
    private InputDevice XRRightController;

    void OnEnable()
    {
        if (enabledContinuousLocomotion)
        {
            GetLeftController();
            GetRightController();

        }

    }

    void GetLeftController()
    {
        var leftHandedControllers = new List<InputDevice>();
        var desiredCharacteristics = InputDeviceCharacteristics.HeldInHand | InputDeviceCharacteristics.Left | InputDeviceCharacteristics.Controller;
        InputDevices.GetDevicesWithCharacteristics(desiredCharacteristics, leftHandedControllers);


        foreach (var device in leftHandedControllers)
        {
            Debug.Log(string.Format("Device name '{0}' has characteristics '{1}'", device.name, device.characteristics.ToString()));
            XRLeftController = device;
        }
        Debug.Log(XRLeftController.name);
    }

    void GetRightController()
    {
        var rightHandedControllers = new List<InputDevice>();
        var desiredCharacteristics = InputDeviceCharacteristics.HeldInHand | InputDeviceCharacteristics.Right | InputDeviceCharacteristics.Controller;
        InputDevices.GetDevicesWithCharacteristics(desiredCharacteristics, rightHandedControllers);


        foreach (var device in rightHandedControllers)
        {
            Debug.Log(string.Format("Device name '{0}' has characteristics '{1}'", device.name, device.characteristics.ToString()));
            XRRightController = device;
        }

    }
    void Start()
    {
        if ( enabledContinuousLocomotion)
        {
            GetLeftController();
            GetRightController();
        }
    }


    // integrate continuous locomotion

    void FixedUpdate()
    {
        if(XRLeftController == null)
        {
            GetLeftController();
        }
        if (XRRightController == null)
        {
            GetRightController();
        }

        // left controller controls character horizontal movement
        if (XRLeftController != null && enabledContinuousLocomotion && HeadObj != null)
        {
            Quaternion headYaw = Quaternion.Euler(0, HeadObj.transform.eulerAngles.y, 0);

            Vector2 joystickMovement;
            XRLeftController.TryGetFeatureValue(CommonUsages.primary2DAxis, out joystickMovement);
            // only move the character if it is a touch event.
            if (XRLeftController.name == "OpenVR Controller(VIVE Controller Pro MV) - Left"){
                bool isLeftClickEvent;

                XRLeftController.TryGetFeatureValue(CommonUsages.primary2DAxisClick, out isLeftClickEvent);
                if (isLeftClickEvent)
                {
                    Vector3 direction = headYaw * new Vector3(joystickMovement.x, 0, joystickMovement.y);
                    characterController.Move(direction * speed * Time.deltaTime);
                }

            }
            else
            {
                bool isLeftTouchEvent;

                XRLeftController.TryGetFeatureValue(CommonUsages.primary2DAxisTouch, out isLeftTouchEvent);
                if (isLeftTouchEvent)
                {
                    Vector3 direction = headYaw * new Vector3(joystickMovement.x, 0, joystickMovement.y);
                    characterController.Move(direction * speed * Time.deltaTime);
                }
            }

        }


        // right controller controls character up and down movement
        if (XRRightController != null && enabledContinuousLocomotion)
        {
            Quaternion headYaw = Quaternion.Euler(0, HeadObj.transform.eulerAngles.y, 0);

            Vector2 joystickMovement;
            XRRightController.TryGetFeatureValue(CommonUsages.primary2DAxis, out joystickMovement);

            if (XRRightController.name == "OpenVR Controller(VIVE Controller Pro MV) - Right")
            {
                bool isRightClickEvent;

                XRRightController.TryGetFeatureValue(CommonUsages.primary2DAxisClick, out isRightClickEvent);
                if (isRightClickEvent)
                {
                    Vector3 direction = headYaw * new Vector3(joystickMovement.x, joystickMovement.y, 0);
                    characterController.Move(direction * speed * Time.deltaTime);
                }

            }
            else
            {
                bool isRightTouchEvent;

                XRRightController.TryGetFeatureValue(CommonUsages.primary2DAxisTouch, out isRightTouchEvent);
                if (isRightTouchEvent)
                {
                    Vector3 direction = headYaw * new Vector3(joystickMovement.x, joystickMovement.y, 0);
                    characterController.Move(direction * speed * Time.deltaTime);
                }
            }

            //Vector3 direction = headYaw * new Vector3(rightJoyStickMovement.x, rightJoyStickMovement.y, 0);
            //characterController.Move(direction * speed * Time.deltaTime);

        }
    }

}
