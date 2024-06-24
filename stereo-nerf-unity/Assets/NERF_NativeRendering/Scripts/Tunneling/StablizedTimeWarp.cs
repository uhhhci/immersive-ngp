using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StablizedTimeWarp : MonoBehaviour
{
    /// <summary>
    /// 4 degrees seems to be the sweet spot for stabilization range
    /// Anything higher seems laggy and anything lower makes motion feel quantized.
    /// </summary>
    public float k_AngleStabilization = 4.0f;
    public bool reprojection = true;

#pragma warning disable 649
    [SerializeField]
    [Tooltip("The transform to match position and orientation - ie. a tracked controller")]
    Transform m_FollowSource;

    [SerializeField]
    [Tooltip("The transform that contains the point to stabilize against - like the end of a broom for example")]
    Transform m_StabilizationPoint;

    [SerializeField]
    [Tooltip("When enabled, the object's previous orientation will be considered for stabilization")]
    bool m_UsePreviousOrientation = true;

    [SerializeField]
    [Tooltip("When enabled, the object's endpoint will be considered for stabilization")]
    bool m_UseEndPoint = true;
#pragma warning restore 649
    public bool filterTrackingData = false;

    // tracking stablization and filter
    private Vector3 prevRot;
    private Vector3 prevPos;
    public float ftf_latency = 0.0f; // keep track of the framerate in fps
    private float currFPS = 0;
    private float k_FPS = 1.0f / 60;

    // latency compensation with timewarp reprojection

    private List<Pose> poses;
    private List<float> timeStamps;
   // public float nerfRenderLatency;

    // TODO: add camera time warp: https://blog.unity.com/technology/detecting-performance-bottlenecks-with-unity-frame-timing-manager
    private void Start()
    {
        prevRot = m_FollowSource.rotation.eulerAngles;
        prevPos = m_FollowSource.position;

        poses = new List<Pose>();
        timeStamps = new List<float>();
        poses.Add(new Pose(m_FollowSource.position, m_FollowSource.rotation));
        timeStamps.Add(System.DateTime.Now.Millisecond);
    }
    #region rotation stablization
    private Vector3 getTargetPos()
    {
        Vector3 currpos = m_FollowSource.position;
        Vector3 targetPos = new Vector3(Mathf.Abs(currpos.x - prevPos.x) < 0.0018f ? prevPos.x : currpos.x,
                                        Mathf.Abs(currpos.y - prevPos.y) < 0.0018f ? prevPos.y : currpos.y,
                                        Mathf.Abs(currpos.z - prevPos.z) < 0.0018f ? prevPos.z : currpos.z);
        prevPos = targetPos;
        return targetPos;
    }

    private Vector3 getTargetRot()
    {
        Vector3 currrot = m_FollowSource.rotation.eulerAngles;
        Vector3 targetRot = new Vector3(Mathf.Abs(currrot.x - prevRot.x) < 0.3f ? prevRot.x : currrot.x,
                                        Mathf.Abs(currrot.y - prevRot.y) < 0.3f ? prevRot.y : currrot.y,
                                        Mathf.Abs(currrot.z - prevRot.z) < 0.3f ? prevRot.z : currrot.z);
        prevRot = targetRot;
        return targetRot;
    }

    private Pose getStablizedEndpointRotation()
    {
        var targetPosition = m_FollowSource.position;
        var targetRotation = m_FollowSource.rotation;

        if (filterTrackingData)
        {
            targetPosition = getTargetPos();
            //targetRotation = Quaternion.Euler(getTargetRot());
        }

        // Determine the angular difference between the current rotation and new 'follow' rotation
        // This is for maintaining a steady orientation for an object while moving the controller around
        var thisTransform = transform;
        var oldRotation = thisTransform.rotation;
        var steadyAngleDif = 180.0f;

        if (m_UsePreviousOrientation)
        {
            steadyAngleDif = Quaternion.Angle(oldRotation, targetRotation);
        }

        // Determine the optimal orientation this object would have if it was keeping the endpoint stable
        // Then get the angular difference between that rotation and the new 'follow' rotation
        var toEndPoint = (m_StabilizationPoint.position - targetPosition).normalized;
        var endPointRotation = Quaternion.LookRotation(toEndPoint, thisTransform.up);
        var endPointAngleDif = 180.0f;
        if (m_UseEndPoint)
        {
            endPointAngleDif = Quaternion.Angle(endPointRotation, targetRotation);
        }

        // Whichever angular difference is less is the one we stabilize against
        if (endPointAngleDif < steadyAngleDif)
        {
            var lerpFactor = CalculateStabilizedLerp(endPointAngleDif, Time.deltaTime);
            targetRotation = Quaternion.Slerp(endPointRotation, targetRotation, lerpFactor);
        }
        else
        {
            var lerpFactor = CalculateStabilizedLerp(steadyAngleDif, Time.deltaTime);
            targetRotation = Quaternion.Slerp(oldRotation, targetRotation, lerpFactor);
        }

        return new Pose(targetPosition, targetRotation);
    }


    private float CalculateStabilizedLerp(float distance, float timeSlice)
    {
        // The original angle stabilization code just calculated distance/maxAngle
        // This feels great in VR but is frame-dependent on experiences running at 90fps
        //return Mathf.Clamp01(distance / k_AngleStabilization);

        // We can estimate a time-independent analog
        var originalLerp = distance / k_AngleStabilization;
        if (originalLerp >= 1.0f)
        {
            return 1.0f;
        }

        if (originalLerp <= 0.0f)
        {
            return 0.0f;
        }

        // For fps higher than 90 fps, we scale this value
        // For fps lower than 90fps, we take advantage of the fact that each time this algorithm
        // runs with the same values, the remaining lerp distance squares itself
        // We estimate this up to 3 time slices.  At that point the numbers just get too small to be useful
        // (and any VR experience running at 30 fps is going to be pretty rough, even with re-projection)
        var doubleFrameLerp = originalLerp - originalLerp * originalLerp;
        var tripleFrameLerp = doubleFrameLerp * doubleFrameLerp;

        var firstSlice = Mathf.Clamp01(timeSlice / k_FPS);
        var secondSlice = Mathf.Clamp01((timeSlice - k_FPS) / k_FPS);
        var thirdSlice = Mathf.Clamp01((timeSlice - 2.0f * k_FPS) / k_FPS);

        return originalLerp * firstSlice + doubleFrameLerp * secondSlice + tripleFrameLerp * thirdSlice;
    }
    #endregion

    #region timewarp
    private void updatePerformanceStat()
    {
        ftf_latency += (Time.deltaTime - ftf_latency) * 0.1f; // frame to frame latency in seconds
        currFPS = 1.0f / ftf_latency;
       // Debug.Log("ftf latency: " + ftf_latency); // real time frame rate
        k_FPS = 1 / currFPS;
        // evaluated: the render latency of nerf is about the same as the ftf latency. 
        //nerfRenderLatency = NerfRendererPlugin.get_render_ms(); 
    }

    private Pose getReprojectedPose(float lookupTime)
    {

        //float displayTime = Time.time + ftf_latency;
        //Vector3 predictedPosition = transform.position + (transform.forward * latency * leftCamera.velocity.magnitude);
        //Quaternion predictedRotation = transform.rotation * Quaternion.Euler(leftCamera.angularVelocity.eulerAngles * latency);

        int left = 0;
        int right = 0;

        bool found = false;
        int ind = timeStamps.Count;


        while (!found)
        {
            ind -= 1;

            if (ind < 0)
            {
                break;
            }

            if (timeStamps[ind] < lookupTime)
            {
                left = ind;
                right = ind + 1;
                found = true;
            }
        }

        Pose p = new Pose();

        if (found)
        {
            Pose p_left  = poses[left];
            float t_left = timeStamps[left];

            Pose p_right;
            float t_right;

            if (right >= timeStamps.Count)
            {
                p_right = getStablizedEndpointRotation();
                t_right = System.DateTime.Now.Millisecond;
            }
            else
            {
                p_right = poses[right];
                t_right = timeStamps[right];
            }


            Vector3 targetPos    = Vector3.Lerp   (p_left.position, p_right.position, (lookupTime - t_left) / (t_right - t_left));
            Quaternion targetRot = Quaternion.Lerp(p_left.rotation, p_right.rotation, (lookupTime - t_left) / (t_right - t_left));

            p.position = targetPos;
            p.rotation = targetRot;
        }
        else
        {
            p = getStablizedEndpointRotation();
        }

        // delete all the irrelevant data now 
        timeStamps.RemoveRange(0, left);
        poses.RemoveRange(0, left);

        return p;
    }
    #endregion
    void LateUpdate()
    {

        updatePerformanceStat();

        if (reprojection)
        {
            // add new poses from real-time tracking
            poses.Add(getStablizedEndpointRotation());
            //poses.Add(new Pose(m_FollowSource.position, m_FollowSource.rotation));
            timeStamps.Add(System.DateTime.Now.AddMilliseconds(ftf_latency*1000).Millisecond);

            // calculate reprojection position
            // camera time warp algorithm predict future movement instead of delaying it
            float lookupTime = System.DateTime.Now.Millisecond ;

            // update the transform with reprojected poses
            Pose p = getReprojectedPose(lookupTime);
            transform.rotation = p.rotation;
            transform.position = p.position;
           // Debug.Log("nerf render latency: " + NerfRendererPlugin.get_render_ms());
        }
        else
        {
            // update the transform with reprojected poses
            Pose p = getStablizedEndpointRotation();
            transform.rotation = p.rotation;
            transform.position = p.position;
        }


    }

}


