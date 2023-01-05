using UnityEngine;
using System.Runtime.InteropServices;
using AOT;
using System.IO;

public class NerfRendererPlugin
{
    [DllImport("ngp_shared", EntryPoint = "unity_nerf_set_initialize_values", CharSet = CharSet.Ansi)]
    public static extern void set_initialize_values(string scene, string checkpoint, bool use_dlss, int width, int height);

    [DllImport("ngp_shared", EntryPoint = "unity_nerf_deinitialize")]
    public static extern void deinitialize();

    [DllImport("ngp_shared", EntryPoint = "unity_nerf_deinit_ngx_vulkan")]
    public static extern void deinit_ngx_vulkan();

    [DllImport("ngp_shared", EntryPoint = "unity_nerf_destroy_texture")]
    public static extern void destroy_texture(System.IntPtr handle);

    //[DllImport("ngp_shared", EntryPoint = "unity_nerf_reset_camera")]
    //public static extern void reset_nerf_camera();

    [DllImport("ngp_shared", EntryPoint = "unity_nerf_get_left_handle")]
    public static extern System.IntPtr get_left_handle();

    [DllImport("ngp_shared", EntryPoint = "unity_nerf_get_right_handle")]
    public static extern System.IntPtr get_right_handle();

    [DllImport("ngp_shared", EntryPoint = "unity_nerf_get_graphics_init_state")]
    public static extern bool get_graphics_init_state();

    [DllImport("ngp_shared", EntryPoint = "unity_nerf_update_stereo_view_matrix")]
    public static extern unsafe void update_stereo_view_matrix(float* left, float* right);

    public static void update_stereo_view_matrix(float[] left, float[] right)
    {
        unsafe
        {
            fixed (float* left_ptr = left)
            {
                fixed (float* right_ptr = right)
                {
                    update_stereo_view_matrix(left_ptr, right_ptr);

                }
            }
        }

    }

    [DllImport("ngp_shared", EntryPoint = "GetRenderEventFunc")]
    public static extern System.IntPtr GetRenderEventFunc();
}
