using UnityEngine;
using System.Runtime.InteropServices;


public class NerfRendererPlugin
{
    [DllImport("ngp_shared", EntryPoint = "unity_nerf_initialize", CharSet=CharSet.Ansi)]
    public static extern void initialize(string scene, string checkpoint, bool use_dlss);

    [DllImport("ngp_shared", EntryPoint="unity_nerf_deinitialize")]
    public static extern void deinitialize();

    [DllImport("ngp_shared", EntryPoint="unity_nerf_create_texture")]
    public static extern System.IntPtr create_texture(int width, int height);

    [DllImport("ngp_shared", EntryPoint="unity_nerf_update_texture")]
    public static extern unsafe void update_texture(float* camera_matrix, System.IntPtr handle);

    public static void update_texture(float[] camera_matrix, System.IntPtr handle)
    {
        unsafe {
            fixed (float* cam_ptr = camera_matrix)
            {
                update_texture(cam_ptr, handle);
            }
        }
    }

    [DllImport("ngp_shared", EntryPoint = "unity_nerf_update_aabb_crop")]
    public static extern unsafe void update_aabb_crop(float* aabb_min, float* aabb_max);

    public static void update_aabb_crop(float[] aabb_min, float[] aabb_max)
    {
        unsafe
        {
            fixed (float* aabb_min_ptr = aabb_min)
            {
                fixed (float* aabb_max_ptr = aabb_max)
                {
                    update_aabb_crop(aabb_min_ptr, aabb_max_ptr);
                }
            }
        }
    }

    [DllImport("ngp_shared", EntryPoint="unity_nerf_destroy_texture")]
    public static extern void destroy_texture(System.IntPtr handle);

    [DllImport("ngp_shared", EntryPoint = "unity_nerf_reset_camera")]
    public static extern void reset_nerf_camera();


}
