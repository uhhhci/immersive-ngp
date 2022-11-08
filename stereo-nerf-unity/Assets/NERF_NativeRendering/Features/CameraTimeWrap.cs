using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraTimeWrap : MonoBehaviour
{
    // Asynchronous video time warp algorithm to compensate the motion sickness effects

    public Transform XRHead;
    public Transform LeftRenderPlane;
    public Transform RightRenderPlane;
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
    private void Start()
    {
        _lagPositions = new List<Vector3>();
        _lagRotations = new List<Quaternion>();
        _TimeStamps = new List<float>();
        elapsedTime = 0;
    }
    private void Update()
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
                    transform.position= Vector3.Lerp(lastpos, _lagPositions[0], diff);
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
            transform.position = XRHead.position;
            transform.rotation = XRHead.rotation;
        }
    }

    private void LateUpdate()
    {



    }
}
