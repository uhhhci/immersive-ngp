using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class CameraTimeWrap : MonoBehaviour
{
    // Asychronous time warp would be important here for interactions! 
    // When disable the camera time warp, this script can be used as a simple camera follower to transfer the tracking data of the main camera to the other camera
    public Transform XRHead;
    //public Transform LeftRenderPlane;
    //public Transform RightRenderPlane;
    public bool enableTimeWrap = true;

    public int DelayFrame = 3;
    public int FPS = 30;
    public float totalDelaySec;

    List<Vector3> _lagPositions;
    List<Quaternion> _lagRotations;
    List<float> _TimeStamps;

    private float currentTime;
    private float updateTime;

    public float elapsedTime;

    private Vector3 lastpos;
    private Quaternion lastrot;

    private Vector3 prevRot;
    private Vector3 prevPos;
    private void Start()
    {
        _lagPositions = new List<Vector3>();
        _lagRotations = new List<Quaternion>();
        _TimeStamps = new List<float>();
        elapsedTime = 0;

        prevRot = XRHead.rotation.eulerAngles;
        prevPos = XRHead.position;

    }

    private Vector3 getTargetPos()
    {
        Vector3 currpos = XRHead.position;
        Vector3 targetPos = new Vector3(Mathf.Abs(currpos.x - prevPos.x) < 0.002f ? prevPos.x : currpos.x,
                                        Mathf.Abs(currpos.y - prevPos.y) < 0.002f ? prevPos.y : currpos.y,
                                        Mathf.Abs(currpos.z - prevPos.z) < 0.002f ? prevPos.z : currpos.z);
        prevPos = targetPos;
        return targetPos;
    }

    private Vector3 getTargetRot()
    {
        Vector3 currrot = XRHead.rotation.eulerAngles;
        Vector3 targetRot = new Vector3(Mathf.Abs(currrot.x - prevRot.x) < 0.3f ? prevRot.x : currrot.x,
                                        Mathf.Abs(currrot.y - prevRot.y) < 0.3f ? prevRot.y : currrot.y,
                                        Mathf.Abs(currrot.z - prevRot.z) < 0.3f ? prevRot.z : currrot.z);
        prevRot = targetRot;
        return targetRot;
    }
    private void LateUpdate()
    {
        if (enableTimeWrap)
        {
            elapsedTime += Time.deltaTime;
            _lagRotations.Add(XRHead.rotation);
            _lagPositions.Add(XRHead.position);
            currentTime = System.DateTime.Now.AddMilliseconds(totalDelaySec * 1000).Millisecond;
            _TimeStamps.Add(currentTime);

            if (elapsedTime > totalDelaySec )
            {
                float diff = Mathf.Abs(totalDelaySec * 1000 - Mathf.Abs(System.DateTime.Now.Millisecond - _TimeStamps[0]));
                
                if(diff> 1)
                {
                    // wrong results, interpolate
                   // Debug.Log("difference: " + diff + "interpolate position");
                    transform.position = Vector3.Lerp   (lastpos, _lagPositions[0], diff);
                    transform.rotation = Quaternion.Lerp(lastrot, _lagRotations[0], diff);
                }
                else
                {
                    transform.position = _lagPositions[0];
                    transform.rotation = _lagRotations[0];
                }
 

                lastpos = _lagPositions[0];
                lastrot = _lagRotations[0];

                _lagPositions.RemoveAt(0);
                _lagRotations.RemoveAt(0);
                _TimeStamps.RemoveAt(0);
            }
        }
        else
        {

            transform.position = XRHead.position;//getTargetPos();
            transform.rotation = XRHead.rotation; //Quaternion.Euler(getTargetRot());
        }
    }

}
