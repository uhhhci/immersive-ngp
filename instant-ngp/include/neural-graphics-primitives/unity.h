#pragma once
#include "Unity/IUnityInterface.h"
#include "Unity/IUnityGraphics.h"

#ifdef _WIN32
#  include <GL/gl3w.h>
#else
#  include <GL/glew.h>
#endif
#include <GLFW/glfw3.h>
#include "gl/GL.h"
#include "gl/GLU.h"

#ifdef _MSC_VER
    #define INTERFACE_API __stdcall
    #define EXPORT_API __declspec(dllexport)
#else
    #define EXPORT_API
    #error "Unsported compiler have fun"
#endif

// Certain Unity APIs (GL.IssuePluginEvent, CommandBuffer.IssuePluginEvent) can callback into native plugins.
// Provide them with an address to a function of this signature.
typedef void (INTERFACE_API* UnityRenderingEvent)(int eventId);
typedef void (INTERFACE_API* UnityRenderingEventAndData)(int eventId, void* data);

// manipulation utilities
extern "C" void UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API   unity_nerf_update_aabb_crop(float* min_vec, float* max_vec);
extern "C" void UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API   unity_nerf_update_aabb(float* min_vec, float* max_vec);
extern "C" void UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API   unity_nerf_reset_camera();
extern "C" UNITY_INTERFACE_EXPORT float* UNITY_INTERFACE_API unity_nerf_get_render_aabb_min();
extern "C" UNITY_INTERFACE_EXPORT float* UNITY_INTERFACE_API unity_nerf_get_render_aabb_max();
extern "C" UNITY_INTERFACE_EXPORT float* UNITY_INTERFACE_API unity_nerf_get_aabb_min();
extern "C" UNITY_INTERFACE_EXPORT float* UNITY_INTERFACE_API unity_nerf_get_aabb_max();

extern "C" void UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API   unity_nerf_get_render_aabb_to_local(float arr[]);
extern "C" void UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API   unity_nerf_get_crop_box_transform(float arr[]);
extern "C" void UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API   unity_nerf_set_crop_box_transform(float* cropbox);

// density grid and bit field manipulation [choose where to render or not]
extern "C" void UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API unity_nerf_mark_density_grid_empty(float* pos, float scale);
extern "C" void UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API unity_nerf_reveal_density_grid_area(float* pos, float scale);
extern "C" void UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API unity_nerf_set_all_density_grid_empty();
extern "C" void UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API unity_nerf_reveal_all_masked_density();
extern "C" void UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API unity_nerf_empty_density_grid_in_box(float* pos, float box_width, float box_height, float box_length, float* R);
extern "C" void UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API unity_nerf_reveal_density_grid_in_box(float* pos, float box_width, float box_height, float box_length, float* R);

// graphics events
extern "C" void UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API unity_nerf_destroy_texture(GLuint handle);
extern "C" void UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API unity_nerf_deinitialize();
extern "C" void UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API unity_nerf_deinit_ngx_vulkan();
extern "C" UnityRenderingEvent UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API GetRenderEventFunc();

// transformation settings
extern "C" void UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API unity_nerf_set_render_fov(float val);
extern "C" void UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API unity_nerf_set_scale(float val);
extern "C" void UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API unity_nerf_set_view_dir(float val[]);
extern "C" void UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API unity_nerf_set_look_at(float val[]);
extern "C" void UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API unity_nerf_get_look_at(float val[]);
extern "C" void UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API unity_nerf_get_view_dir(float val[]);

// initialization set/ get for multithread rendering
extern "C" void UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API   unity_nerf_set_initialize_values(const char* scene, const char* checkpoint, bool use_dlss, bool _use_depth, int width, int height);
extern "C" GLuint UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API unity_nerf_get_left_handle();
extern "C" GLuint UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API unity_nerf_get_right_handle();
extern "C" GLuint UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API unity_nerf_get_left_depth_handle();
extern "C" GLuint UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API unity_nerf_get_right_depth_handle();
extern "C" float UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API  unity_nerf_get_render_ms();
extern "C" bool UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API   unity_nerf_get_graphics_init_state();
extern "C" void UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API   unity_nerf_update_stereo_view_matrix(float* left, float* right);
extern "C" void UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API   unity_nerf_update_resolution(int width, int height);
extern "C" void UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API   unity_nerf_save_snapshot(const char* filename);
