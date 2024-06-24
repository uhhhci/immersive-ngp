using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraFollower : MonoBehaviour
{
    public Transform XRCameraToTrack;
    public float forwardOffset = 1f;
    public float rightOffset =0f;
    public float upOffset = 0;

    public float smoothfactor = 0.1f;
    private Vector3 targetPos;
    void Start()
    {

    }

    // Update is called once per frame
    void LateUpdate()
    {

        Vector3 currentPos = XRCameraToTrack.TransformPoint(new Vector3(rightOffset, upOffset, forwardOffset));
        targetPos = Vector3.Lerp(targetPos, currentPos, smoothfactor);
        transform.position = targetPos;
        transform.LookAt(XRCameraToTrack);

    }

}

