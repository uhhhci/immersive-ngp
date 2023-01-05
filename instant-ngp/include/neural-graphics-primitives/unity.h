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
extern "C" void UNITY_INTERFACE_API unity_nerf_update_aabb_crop(float* min_vec, float* max_vec);

// graphics events
extern "C" void UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API unity_nerf_destroy_texture(GLuint handle);
//extern "C" void UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API unity_nerf_initialize(const char* scene, const char* checkpoint, bool use_dlss, int width, int height);
extern "C" void UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API unity_nerf_deinitialize();
extern "C" void UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API unity_nerf_deinit_ngx_vulkan();
extern "C" UnityRenderingEvent UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API GetRenderEventFunc();
// setters/ getters

extern "C" void UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API unity_nerf_set_initialize_values(const char* scene, const char* checkpoint, bool use_dlss, int width, int height);
extern "C" GLuint UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API unity_nerf_get_left_handle();
extern "C" GLuint UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API  unity_nerf_get_right_handle();
extern "C" bool UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API unity_nerf_get_graphics_init_state();
extern "C" void UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API  unity_nerf_update_stereo_view_matrix(float* left, float* right);
