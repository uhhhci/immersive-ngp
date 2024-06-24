using UnityEngine;
using System.Runtime.InteropServices;
using AOT;
using System.IO;

public class NerfRendererPlugin
{
    [DllImport("ngp_shared", EntryPoint = "unity_nerf_set_initialize_values", CharSet = CharSet.Ansi)]
    public static extern void set_initialize_values(string scene, string checkpoint, bool use_dlss, bool use_depth, int width, int height);

    [DllImport("ngp_shared", EntryPoint = "unity_nerf_save_snapshot", CharSet = CharSet.Ansi)]
    public static extern void save_snapshot(string filename);

    [DllImport("ngp_shared", EntryPoint = "unity_nerf_deinitialize")]
    public static extern void deinitialize();

    [DllImport("ngp_shared", EntryPoint = "unity_nerf_deinit_ngx_vulkan")]
    public static extern void deinit_ngx_vulkan();

    [DllImport("ngp_shared", EntryPoint = "unity_nerf_destroy_texture")]
    public static extern void destroy_texture(System.IntPtr handle);

    [DllImport("ngp_shared", EntryPoint = "unity_nerf_get_left_handle")]
    public static extern System.IntPtr get_left_handle();

    [DllImport("ngp_shared", EntryPoint = "unity_nerf_get_right_handle")]
    public static extern System.IntPtr get_right_handle();

    [DllImport("ngp_shared", EntryPoint = "unity_nerf_get_left_depth_handle")]
    public static extern System.IntPtr get_left_depth_handle();

    [DllImport("ngp_shared", EntryPoint = "unity_nerf_get_right_depth_handle")]
    public static extern System.IntPtr get_right_depth_handle();

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

    [DllImport("ngp_shared", EntryPoint = "unity_nerf_set_crop_box_transform")]
    public static extern unsafe void set_crop_box_transform(float* m);

    public static void set_crop_box_transform(float[] _m)
    {
        unsafe
        {
            fixed (float* m = _m)
            {
                 set_crop_box_transform(m);
            }
        }

    }

    [DllImport("ngp_shared", EntryPoint = "GetRenderEventFunc")]
    public static extern System.IntPtr GetRenderEventFunc();

    // utiliy functions

    [DllImport("ngp_shared", EntryPoint = "unity_nerf_update_aabb_crop")]
    public static extern unsafe void update_aabb_crop(float* min, float* max);
    public static void update_aabb_crop(float[] min, float[] max)
    {
        unsafe
        {
            fixed (float* min_ptr = min)
            {
                fixed (float* max_ptr = max)
                {
                    update_aabb_crop(min_ptr, max_ptr);
                }
            }
        }

    }


    [DllImport("ngp_shared", EntryPoint = "unity_nerf_mark_density_grid_empty")]
    public static extern unsafe void mark_density_grid_empty(float* pos, float scale);
    public static void mark_density_grid_empty(float[] pos, float scale)
    {
        unsafe
        {
            fixed (float* pos_ptr = pos)
            {
                mark_density_grid_empty(pos_ptr, scale);
            }
        }

    }


    [DllImport("ngp_shared", EntryPoint = "unity_nerf_reveal_density_grid_area")]
    public static extern unsafe void reveal_density_grid_area(float* pos, float scale);
    public static void reveal_density_grid_area(float[] pos, float scale)
    {
        unsafe
        {
            fixed (float* pos_ptr = pos)
            {
                reveal_density_grid_area(pos_ptr, scale);
            }
        }

    }

    [DllImport("ngp_shared", EntryPoint = "unity_nerf_reveal_density_grid_in_box")]
    public static extern unsafe void reveal_density_grid_in_box(float* pos, float box_width, float box_height, float box_length, float* R);
    public static void reveal_density_grid_in_box(float[] pos, float box_width, float box_height, float box_length, float[] R)
    {
        unsafe
        {
            fixed (float* pos_ptr = pos)
            {
                fixed(float* r_ptr = R)
                {
                    reveal_density_grid_in_box(pos_ptr, box_width, box_height, box_length, r_ptr);

                }
            }
        }

    }

    [DllImport("ngp_shared", EntryPoint = "unity_nerf_empty_density_grid_in_box")]
    public static extern unsafe void empty_density_grid_in_box(float* pos, float box_width, float box_height, float box_length, float* R);
    public static void empty_density_grid_in_box(float[] pos, float box_width, float box_height, float box_length, float[] R)
    {
        unsafe
        {
            fixed (float* pos_ptr = pos)
            {
                fixed(float* r_ptr = R)
                {
                    empty_density_grid_in_box(pos_ptr, box_width, box_height, box_length, r_ptr);

                }
            }
        }

    }

    [DllImport("ngp_shared", EntryPoint = "unity_nerf_set_all_density_grid_empty")]
    public static extern void set_all_density_grid_empty();

    [DllImport("ngp_shared", EntryPoint = "unity_nerf_reveal_all_masked_density")]
    public static extern void reveal_all_masked_density();

    [DllImport("ngp_shared", EntryPoint = "unity_nerf_get_render_aabb_min")]
    public static extern unsafe float* get_render_aabb_min();

    [DllImport("ngp_shared", EntryPoint = "unity_nerf_get_render_aabb_max")]
    public static extern unsafe float* get_render_aabb_max();

    [DllImport("ngp_shared", EntryPoint = "unity_nerf_get_aabb_max")]
    public static extern unsafe float* get_aabb_max();

    [DllImport("ngp_shared", EntryPoint = "unity_nerf_get_aabb_min")]
    public static extern unsafe float* get_aabb_min();

    [DllImport("ngp_shared", EntryPoint = "unity_nerf_get_crop_box_transform")]
    public static extern void get_crop_box_transform(float[] arr);

    [DllImport("ngp_shared", EntryPoint = "unity_nerf_update_resolution")]
    public static extern void update_resolution(int width, int height);

    [DllImport("ngp_shared", EntryPoint = "unity_nerf_get_render_aabb_to_local")]
    public static extern void get_render_aabb_to_local(float[] arr);

    [DllImport("ngp_shared", EntryPoint = "unity_nerf_set_render_fov")]
    public static extern void set_render_fov(float val);

    [DllImport("ngp_shared", EntryPoint = "unity_nerf_set_scale")]
    public static extern void set_scale(float val);

    [DllImport("ngp_shared", EntryPoint = "unity_nerf_set_view_dir")]
    public static extern void set_view_dir(float[] val);

    [DllImport("ngp_shared", EntryPoint = "unity_nerf_set_look_at")]
    public static extern void set_look_at(float[] val);

    [DllImport("ngp_shared", EntryPoint = "unity_nerf_get_look_at")]
    public static extern void get_look_at(float[] val);

    [DllImport("ngp_shared", EntryPoint = "unity_nerf_get_view_dir")]
    public static extern void get_view_dir(float[] val);

    [DllImport("ngp_shared", EntryPoint = "unity_nerf_reset_camera")]
    public static extern void reset_camera();

    [DllImport("ngp_shared", EntryPoint = "unity_nerf_get_render_ms")]
    public static extern float get_render_ms();

    public static Vector3 getRenderAABBMin()
    {
        unsafe
        {
            float* ptr_aabb_min = get_render_aabb_min();
            return new Vector3(ptr_aabb_min[0], ptr_aabb_min[1], ptr_aabb_min[2]);
        }
    }

    public static Vector3 getRenderAABBMax()
    {
        unsafe
        {
            float* ptr_aabb_max = get_render_aabb_max();

            return new Vector3(ptr_aabb_max[0], ptr_aabb_max[1], ptr_aabb_max[2]);
        }

    }

    public static Vector3 getAABBMin()
    {
        unsafe
        {
            float* ptr_aabb_min = get_aabb_min();
            return new Vector3(ptr_aabb_min[0], ptr_aabb_min[1], ptr_aabb_min[2]);
        }
    }

    public static Vector3 getAABBMax()
    {
        unsafe
        {
            float* ptr_aabb_max = get_aabb_max();

            return new Vector3(ptr_aabb_max[0], ptr_aabb_max[1], ptr_aabb_max[2]);
        }

    }

    public static float[] getCropBoxTransform()
    {
        float[] arr = new float[12];
        get_crop_box_transform(arr);

        return arr;
    }

    public static float[] getRenderAABBToLocal()
    {
        float[] arr = new float[9];
        get_render_aabb_to_local(arr);
        return arr;
    }

    public static void ResetCropboxNeRF()
    {
        
    }
}
