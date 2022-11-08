using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

public class NERFLocomotion : MonoBehaviour
{
    public GameObject MRPlaySpace;
    public GameObject LeftCamera;

    public CharacterController characterController;
    public bool enabledDiscreetLocomotion = false;
    public bool enabledContinuousLocomotion = true;
    public float movementStep = 0.05f;
    public float rotationStep = 15; // degrees in eulers' angles.

    private InputDevice XRLeftController;
    private InputDevice XRRightController;
    private bool lastLeftJoystickState = false;
    private bool lastRightJoystickState = false;


    [SerializeField]
    private float speed = 1f;

    void OnEnable()
    {
        GetLeftController();
        GetRightController();
        MRPlaySpace = GameObject.Find("MixedRealityPlayspace");

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

    void RepositionNERFView()
    {
        if (MRPlaySpace != null)
        {
            // set transform to be in front of the nerf rendering result
            MRPlaySpace.transform.position = new Vector3(0, 0, 1.5f);
            MRPlaySpace.transform.rotation = Quaternion.Euler(0, -180f, 0);
        }
    }
    void Start()
    {
        GetLeftController();
        GetRightController();
        MRPlaySpace = GameObject.Find("MixedRealityPlayspace");
        RepositionNERFView();
    }

    public void ToggleLocomotion()
    {
        enabledDiscreetLocomotion = !enabledDiscreetLocomotion;
    }
    // integrate continuous locomotion

    void FixedUpdate()
    {
        // left controller controls character horizontal movement
        if (XRLeftController != null && enabledContinuousLocomotion && LeftCamera!=null)
        {
            Quaternion headYaw = Quaternion.Euler(0, LeftCamera.transform.eulerAngles.y, 0);

            Vector2 joystickMovement;
            XRLeftController.TryGetFeatureValue(CommonUsages.primary2DAxis, out joystickMovement);

            Vector3 direction = headYaw * new Vector3(joystickMovement.x, 0, joystickMovement.y);
            characterController.Move(direction * speed * Time.deltaTime);

        }


        // right controller controls character up and down movement
        if (XRRightController != null && enabledContinuousLocomotion)
        {
            Quaternion headYaw = Quaternion.Euler(0, LeftCamera.transform.eulerAngles.y, 0);

            Vector2 rightJoyStickMovement;
            XRRightController.TryGetFeatureValue(CommonUsages.primary2DAxis, out rightJoyStickMovement);

            Vector3 direction = headYaw* new Vector3(rightJoyStickMovement.x, rightJoyStickMovement.y ,0);
            characterController.Move(direction * speed * Time.deltaTime);

        }
    }
    // Update is called once per frame
    void Update()
    {
       
        // left controller controls
        if(XRLeftController != null && enabledDiscreetLocomotion)
        {
            Vector2 joystickMovement;
            bool tempState = false;
            XRLeftController.TryGetFeatureValue(CommonUsages.primary2DAxis, out joystickMovement) ;
            // left hand joy stick y axis moving the position up and down
  
            if(joystickMovement.magnitude != 0)
            {
                tempState = true;
            }

            if (tempState != lastLeftJoystickState)
            {
                if (joystickMovement.y > 0 && Mathf.Abs(joystickMovement.y) > Mathf.Abs(joystickMovement.x))
                {
                    Vector3 currentPos = MRPlaySpace.transform.position;
                    MRPlaySpace.transform.position = new Vector3(currentPos.x, currentPos.y + movementStep, currentPos.z);
                }
                if (joystickMovement.y < 0 && Mathf.Abs(joystickMovement.y) > Mathf.Abs(joystickMovement.x))
                {
                    Vector3 currentPos = MRPlaySpace.transform.position;
                    MRPlaySpace.transform.position = new Vector3(currentPos.x, currentPos.y - movementStep, currentPos.z);
                }

                // left hand joy stick x axis moving the position left and right
                if (joystickMovement.x > 0 && Mathf.Abs(joystickMovement.y) < Mathf.Abs(joystickMovement.x))
                {
                    Vector3 currentPos = MRPlaySpace.transform.position;
                    MRPlaySpace.transform.position = new Vector3(currentPos.x + movementStep, currentPos.y, currentPos.z);
                }
                if (joystickMovement.x < 0 && Mathf.Abs(joystickMovement.y) < Mathf.Abs(joystickMovement.x))
                {
                    Vector3 currentPos = MRPlaySpace.transform.position;
                    MRPlaySpace.transform.position = new Vector3(currentPos.x - movementStep, currentPos.y, currentPos.z);
                }

                lastLeftJoystickState = tempState;
            }

        }



        // right controller controls
        if (XRRightController != null && enabledDiscreetLocomotion)
        {
            Vector2 joystickMovement;
            bool tempStateRight = false;
            XRRightController.TryGetFeatureValue(CommonUsages.primary2DAxis, out joystickMovement);
            // left hand joy stick y axis moving the position up and down

            if (joystickMovement.magnitude != 0)
            {
                tempStateRight = true;
            }

            if (tempStateRight != lastRightJoystickState)
            {
                // on the y axis, it moves camera forward and backwards
                if (joystickMovement.y > 0 && Mathf.Abs(joystickMovement.y) > Mathf.Abs(joystickMovement.x))
                {
                    Vector3 currentPos = MRPlaySpace.transform.position;
                    MRPlaySpace.transform.position = new Vector3(currentPos.x, currentPos.y, currentPos.z + movementStep);
                }
                if (joystickMovement.y < 0 && Mathf.Abs(joystickMovement.y) > Mathf.Abs(joystickMovement.x))
                {
                    Vector3 currentPos = MRPlaySpace.transform.position;
                    MRPlaySpace.transform.position = new Vector3(currentPos.x, currentPos.y , currentPos.z - movementStep);
                }

                // right hand x axis applies slight rotation 
                if (joystickMovement.x > 0 && Mathf.Abs(joystickMovement.y) < Mathf.Abs(joystickMovement.x))
                {
                    Vector3 currentRot = MRPlaySpace.transform.rotation.eulerAngles;
                    MRPlaySpace.transform.eulerAngles = new Vector3(currentRot.x, currentRot.y + rotationStep, currentRot.z) ;
                }
                if (joystickMovement.x < 0 && Mathf.Abs(joystickMovement.y) < Mathf.Abs(joystickMovement.x))
                {
                    Vector3 currentRot = MRPlaySpace.transform.rotation.eulerAngles;
                    MRPlaySpace.transform.eulerAngles = new Vector3(currentRot.x, currentRot.y - rotationStep, currentRot.z);
                }

                lastRightJoystickState = tempStateRight;
            }

        }

    }
}
