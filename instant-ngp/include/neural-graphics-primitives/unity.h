#pragma once

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

typedef unsigned int UnityTextureID;

#ifdef __cplusplus
extern "C" {
#endif

EXPORT_API void INTERFACE_API unity_nerf_initialize(const char* scene, const char* checkpoint, bool use_dlss);
EXPORT_API void INTERFACE_API unity_nerf_deinitialize();

EXPORT_API UnityTextureID INTERFACE_API unity_nerf_create_texture(int width, int height);
EXPORT_API void INTERFACE_API unity_nerf_update_texture(float* camera_matrix, UnityTextureID handle);
EXPORT_API void INTERFACE_API unity_nerf_update_aabb_crop(float* min_vec, float* max_vec);
EXPORT_API void INTERFACE_API unity_nerf_destroy_texture(UnityTextureID handle);
EXPORT_API void INTERFACE_API unity_nerf_reset_camera();

#ifdef __cplusplus
}
#endif
