using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Microsoft.MixedReality.Toolkit.UI;
using Microsoft.MixedReality.Toolkit.UI.BoundsControl;
using Microsoft.MixedReality.Toolkit.UI.BoundsControlTypes;
using Microsoft.MixedReality.Toolkit.UI.HandCoach;
using System.Diagnostics.Eventing.Reader;

public class NeRFAABBCropBox : MonoBehaviour
{
    [Header("AABB Bounding Box")]
    public bool enableCropping = false;
    public bool enableRotate = false;
    public ExoStereoNeRFRenderer neRFRenderer;
    public GameObject NeRFAABBBox;
    // public GameObject MinSphere, MaxSphere;
    [Header("Restricted boundary setting")]
    public bool hasRestrictedBoundary = false;
    [Tooltip("setting of render boundary from instant ngp make sure not to render clouds outside of the boudary area")]
    public Vector3 renderBoundaryMin = new Vector3(-0.5f, -0.5f, -0.5f);
    [Tooltip("setting of render boundary from instant ngp make sure not to render clouds outside of the boudary area")]
    public Vector3 renderBoundaryMax = new Vector3(1.5f, 1.5f, 1.5f);

    // last updated transforms from NeRFTransformBox
    Quaternion tbox_r;
    Vector3 tbox_s;
    Vector3 tbox_p;
    Vector3 newMin_ngp;
    Vector3 newMax_ngp;
    Vector3 newMin_unity;
    Vector3 newMax_unity;
    private BoundsControl bc;


    public void enableCropManipulation()
    {
        enableCropping = true;
        NeRFAABBBox.SetActive(true);
        neRFRenderer.transformBox.SetActive(false);
        enableRotate = true;
    }

    public void disableCropManipulation()
    {
        enableCropping = false;
        NeRFAABBBox.SetActive(false);
        neRFRenderer.transformBox.SetActive(true);
        //neRFRenderer.SyncWithAABBCropBox();
    }

    public void resetCropping()
    {
        NeRFAABBBox.transform.position = tbox_p;
        NeRFAABBBox.transform.rotation = tbox_r;
        NeRFAABBBox.transform.localScale = tbox_s;
    }

    public Vector3 getCurrAABBMinNGP()
    {
        return newMin_ngp;
    }

    public Vector3 getCurrAABBMaxNGP()
    {
        return newMax_ngp;
    }

    public Vector3 getCurrAABBMinUnity()
    {
        return newMin_unity;
    }

    public Vector3 getCurrAABBMaxUnity()
    {
        return newMax_unity;
    }

    public Vector3 getLastSyncBoxPos()
    {
        return tbox_p;
    }

    public Vector3 getLastSyncBoxScale()
    {
        return tbox_s;
    }
    public bool isCropping()
    {
        return enableCropping;
    }

    public Vector3 getBoxCornerPos(GameObject cube, int corner)
    {
        // gladly provided by chat-gpt, 0 is min, 1 is max

        Vector3 centerPosition = cube.transform.position;
        Vector3 cubeSize = cube.transform.localScale;
        Vector3 cornerLocalPosition = new Vector3(-cubeSize.x / 2, -cubeSize.y / 2, -cubeSize.z / 2);
        if(corner == 1)
        {
            cornerLocalPosition = new Vector3(cubeSize.x / 2, cubeSize.y / 2, cubeSize.z / 2);
        }
        Vector3 cornerDirection = cube.transform.TransformDirection(cornerLocalPosition);
        float cornerDistance = cornerDirection.magnitude;
        Vector3 cornerDirectionNormalized = cornerDirection / cornerDistance;
        Vector3 cornerGlobalPosition = centerPosition + cornerDirectionNormalized * cornerDistance;

        return cornerGlobalPosition;
    }

    private float[] getRTSMatrixNeRFStyle(Vector3 translate, Quaternion rotate , Vector3 scale , Vector3 t_offset)
    {
       // Matrix4x4 m = Matrix4x4.TRS(translate, rotate, scale).transpose;
        
        Matrix4x4 t  = Matrix4x4.Translate(translate);
       
        Matrix4x4 t1 = Matrix4x4.Translate(-t_offset);

        Matrix4x4 t2 = Matrix4x4.Translate(t_offset);

        Matrix4x4 r = Matrix4x4.Rotate(rotate);

        Matrix4x4 s = Matrix4x4.Scale(scale);

        Matrix4x4 m = (t * t1 * r  * t2 * s).transpose;
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

    private void updateOriginalBoxTransform()
    {
        Quaternion aabb_box_rot = NeRFAABBBox.transform.rotation;
        Vector3    initBoxPos   = neRFRenderer.getInitBoxPos();
        Vector3    initBoxRot   = neRFRenderer.getInitBoxRot();

        float _scale = neRFRenderer.getCurrScaleRatio();

        Vector3 rot_change = aabb_box_rot.eulerAngles - tbox_r.eulerAngles + initBoxRot;
        Vector3 pos_change = Quaternion.Inverse(tbox_r) * NeRFAABBBox.transform.position - Quaternion.Inverse(tbox_r) * tbox_p;

        neRFRenderer.originalBox.transform.position = initBoxPos + pos_change * _scale;
        neRFRenderer.originalBox.transform.rotation = Quaternion.Euler(rot_change);
        neRFRenderer.originalBox.transform.localScale = NeRFAABBBox.transform.localScale * _scale;

    }
    void Start()
    {
        // nerf aabb crop box is configured so that we can only operate transform through scaling. 
        // therefore, hide all other manipulation
        bc = NeRFAABBBox.GetComponent<BoundsControl>();

        bc.ScaleHandlesConfig.ScaleBehavior = HandleScaleMode.NonUniform;
        bc.ScaleHandlesConfig.HandleSize    = 0.03f;
        if (!enableRotate)
        {
            bc.RotationHandlesConfig.ShowHandleForX = false;
            bc.RotationHandlesConfig.ShowHandleForY = false;
            bc.RotationHandlesConfig.ShowHandleForZ = false;
        }
        NeRFAABBBox.SetActive(false);
       
    }
    // Update is called once per frame
    void Update()
    {

        if(neRFRenderer.already_initalized && enableCropping)
        {

            Quaternion aabb_box_rot = NeRFAABBBox.transform.rotation;
            Vector3 initBoxPos = neRFRenderer.getInitBoxPos();
            Vector3 initBoxRot = neRFRenderer.getInitBoxRot();

            float _scale = neRFRenderer.getCurrScaleRatio();

            Vector3 rot_change = aabb_box_rot.eulerAngles - tbox_r.eulerAngles + initBoxRot;
            Vector3 pos_change = Quaternion.Inverse(tbox_r ) * NeRFAABBBox.transform.position - Quaternion.Inverse(tbox_r) * tbox_p;

            neRFRenderer.originalBox.transform.position   = initBoxPos + pos_change * _scale;
            neRFRenderer.originalBox.transform.rotation   = Quaternion.Euler( rot_change);
            neRFRenderer.originalBox.transform.localScale = NeRFAABBBox.transform.localScale * _scale;

            newMin_unity = getBoxCornerPos(neRFRenderer.originalBox, 0);
            newMax_unity = getBoxCornerPos(neRFRenderer.originalBox, 1);

            Vector3 min_now_ngp = Quaternion.Inverse(Quaternion.Euler(rot_change)) * newMin_unity;
            Vector3 max_now_ngp = Quaternion.Inverse(Quaternion.Euler(rot_change)) * newMax_unity;


            Vector3 new_cen = (min_now_ngp + max_now_ngp) * 0.5f;
            Vector3 curr_s  = (max_now_ngp - min_now_ngp) * 0.5f;

            NerfRendererPlugin.set_crop_box_transform(getRTSMatrixNeRFStyle(new_cen, Quaternion.Euler(rot_change), curr_s, new_cen));

            //MinSphere.transform.position = newMin_unity;
            //MaxSphere.transform.position = newMax_unity;

        }
        else
        {
            tbox_s    = neRFRenderer.transformBox.transform.localScale;
            tbox_p    = neRFRenderer.transformBox.transform.position;
            tbox_r    = neRFRenderer.transformBox.transform.rotation;

            // we can't directly use the aabb bounding box from Unity, here is why: 
            // https://stackoverflow.com/questions/57711849/meshrenderer-has-wrong-bounds-when-rotated
           // MinSphere.transform.position = t_cube_renderer.bounds.min;
           // MaxSphere.transform.position = t_cube_renderer.bounds.max;

           // tbox_min = getBoxCornerPos(NeRFTransformBox, 0);
           // tbox_max = getBoxCornerPos(NeRFTransformBox, 1);
           // MinSphere.transform.position = tbox_min;
           // MaxSphere.transform.position = tbox_max;

            NeRFAABBBox.transform.position   = tbox_p;
            NeRFAABBBox.transform.rotation   = tbox_r;
            NeRFAABBBox.transform.localScale = tbox_s;
        }
    }
}
