#include "neural-graphics-primitives/unity.h"
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

#include <neural-graphics-primitives/adam_optimizer.h>
#include <neural-graphics-primitives/camera_path.h>
#include <neural-graphics-primitives/common.h>
#include <neural-graphics-primitives/discrete_distribution.h>
#include <neural-graphics-primitives/nerf.h>
#include <neural-graphics-primitives/nerf_loader.h>
#include <neural-graphics-primitives/render_buffer.h>
#include <neural-graphics-primitives/sdf.h>
#include <neural-graphics-primitives/shared_queue.h>
#include <neural-graphics-primitives/trainable_buffer.cuh>
#include <neural-graphics-primitives/testbed.h>
#include <neural-graphics-primitives/common_device.cuh>
#include <neural-graphics-primitives/common.h>
#include <neural-graphics-primitives/render_buffer.h>
#include <neural-graphics-primitives/tinyexr_wrapper.h>

#include <tiny-cuda-nn/gpu_memory.h>
#include <filesystem/path.h>
#include <cuda_gl_interop.h>

#include <tiny-cuda-nn/multi_stream.h>
#include <tiny-cuda-nn/random.h>

#include <json/json.hpp>
#include <filesystem/path.h>
#include <thread>
#include "gl/GL.h"
#include "gl/GLU.h"
#include <memory>


using Texture = std::shared_ptr<ngp::GLTexture>;
using RenderBuffer = std::shared_ptr<ngp::CudaRenderBuffer>;

struct TextureData {
    TextureData(const Texture& tex, const RenderBuffer& buf, int width, int heigth)
    : surface_texture(tex), render_buffer(buf), width(width), height(height) {
    }
    ~TextureData(){
        surface_texture.reset();
        render_buffer.reset();
    };
    Texture surface_texture;
    RenderBuffer render_buffer;
    int width;
    int height;
};
static std::shared_ptr<ngp::Testbed> testbed = nullptr;
static std::unordered_map<GLuint, std::shared_ptr<TextureData>> textures;

// flags
bool graphics_initialized = false;
bool use_dlss = false;
static int _width;
static int _height;
static GLuint leftHandle;
static GLuint rightHandle;
float zero_matrix[12] = {0,0,0,0,0,0,0,0,0,0,0,0};
float* view_matrix_left;
float* view_matrix_right;

extern "C" void UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API unity_nerf_set_initialize_values(const char* scene, const char* snapshot, bool dlss, int width, int height){

    use_dlss = dlss;
    _width = width;
    _height = height;

    view_matrix_left = zero_matrix;
    view_matrix_right = zero_matrix;

    testbed = std::make_shared<ngp::Testbed>(
        ngp::ETestbedMode::Nerf,
		scene
    );

    if (snapshot) {
        testbed->load_snapshot(
			snapshot
        );
    }
    tlog::info() << "instant ngp testbed created" ;

};

extern "C" bool UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API unity_nerf_get_graphics_init_state(){
    return graphics_initialized;
}
// this needs to happen in the render thread
extern "C" void UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API unity_nerf_initialize_graphics() { 
    if(graphics_initialized){
        tlog::info() << "graphics already initialized" ;
        return;
    }

	if (!glfwInit()) {
		std::cout << "Could not initialize glfw" << std::endl;
	}
    if (!gl3wInit()) {
        std::cout << "Could not initialize gl3w" << std::endl;
	}

#ifdef NGP_VULKAN
    if (use_dlss) { 
        try {
            ngp::vulkan_and_ngx_init();
        }
        catch (std::runtime_error exception) {
            use_dlss= false;
            std::cout << "Could not initialize vulkan" << std::endl;
        }
    }
#else
    use_dlss = false;
#endif
    
    graphics_initialized = true;
    tlog::info() << "graphics initialized" ;

}

extern "C" void UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API unity_nerf_deinit_ngx_vulkan(){
#ifdef NGP_VULKAN    
    if (use_dlss) { 
        ngp::vulkan_and_ngx_destroy();
        use_dlss = false;
    }
#endif
}
extern "C" void UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API unity_nerf_deinitialize() {
    textures.clear();
    
// #ifdef NGP_VULKAN    
//     if (use_dlss) { 
//         ngp::vulkan_and_ngx_destroy();
//         use_dlss = false;
//     }
// #endif
    testbed.reset();
    glfwTerminate();
    leftHandle = 0;
    rightHandle = 0;
    view_matrix_left = NULL;
    view_matrix_right = NULL;
    graphics_initialized = false;
    use_dlss = false;
    tlog::info() << "instant ngp testbed deinitialized" ;

}

static GLuint UNITY_INTERFACE_API unity_nerf_create_texture(int width, int height) {
    
    if (!testbed){
        tlog::info() << "testbed not found!!" ;
        return 0;
    }
    
    auto texture = std::make_shared<ngp::GLTexture>();
    auto buffer = std::make_shared<ngp::CudaRenderBuffer>(texture);
    Eigen::Vector2i render_res { width, height }; 

#if defined(NGP_VULKAN)
    if (use_dlss) {
        buffer->enable_dlss({ width, height });
        // buffer->resize({ width, height });

        Eigen::Vector2i texture_res { width, height };
        render_res = buffer->in_resolution();
        if (render_res.isZero()) {
            render_res = texture_res / 16;
        } else {
            render_res = render_res.cwiseMin(texture_res);
        }

        if (buffer->dlss()) {
            render_res = buffer->dlss()->clamp_resolution(render_res);
        }
    }
    else{ buffer->disable_dlss();}
#endif

    buffer->resize(render_res);

    GLuint handle = texture->texture();
    textures[handle] = std::make_shared<TextureData>(
        texture,
        buffer,
        width,
        height
    );
    tlog::info() << "GLTexture handle" << handle ;
    return handle;
}

void UNITY_INTERFACE_API unity_nerf_update_texture() {
    if (!testbed){
        tlog::error() << "testbed not found" ;
        return;
    }

    auto left = textures.find(leftHandle);
    auto right = textures.find(rightHandle);

    if (left == std::end(textures)) {
        tlog::error() << "left texture handle not found" ;
        return;
    }
    if (right == std::end(textures)) {
        tlog::error() << "left texture handle not found" ;
        return;
    }

    Eigen::Matrix<float, 3, 4> camera_left {view_matrix_left};
    Eigen::Matrix<float, 3, 4> camera_right {view_matrix_right};

    RenderBuffer render_buffer_left = left->second->render_buffer;
    RenderBuffer render_buffer_right = right->second->render_buffer;

    render_buffer_left->reset_accumulation();
    render_buffer_right->reset_accumulation();

    testbed->render_frame(camera_left,//testbed->m_camera,
                          camera_left,//testbed->m_camera,
                          Eigen::Vector4f::Zero(),
                          *render_buffer_left.get(),
                          true);

    testbed->render_frame(camera_right,//testbed->m_camera,
                        camera_right,//testbed->m_camera,
                        Eigen::Vector4f::Zero(),
                        *render_buffer_right.get(),
                        true);
}

extern "C" void UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API unity_nerf_destroy_texture(GLuint handle) {

    if (!testbed)
        return;
    auto found = textures.find(handle); 
    if (found == std::end(textures)) {
        return;
    } 

    found->second->render_buffer->reset_accumulation();
    found->second->render_buffer.reset();
    found->second->surface_texture.reset();
    found->second.reset();
    tlog::info() << "GLTexture and render buffer destroyed" ;

}

// utility functions

static void UNITY_INTERFACE_API unity_nerf_reset_camera(){
    if (!testbed)
    return;
    testbed->reset_camera();
}

extern "C" void UNITY_INTERFACE_API unity_nerf_update_aabb_crop(float* min_vec, float* max_vec){
    if (!testbed)
    return;

    Eigen::Vector3f min_aabb {min_vec};
    Eigen::Vector3f max_aabb {max_vec};
    
    testbed->m_render_aabb = ngp::BoundingBox(min_aabb, max_aabb);
    
}


const int INIT_EVENT = 0x0001;
const int DRAW_EVENT = 0x0002;
const int DEINIT_EVENT = 0x0003;
const int CREATE_TEX = 0x0004;
const int DEINIT_VULKAN = 0x0005;


static void UNITY_INTERFACE_API unity_nerf_run_on_render_thread(int eventID)
{

    switch (eventID)
    {

        case INIT_EVENT:
            
            unity_nerf_initialize_graphics();

            break;

        case CREATE_TEX:

            leftHandle  = unity_nerf_create_texture(_width, _height);
            rightHandle = unity_nerf_create_texture(_width, _height);

            break;

        case DRAW_EVENT:
            
            unity_nerf_update_texture();

            break;

        case DEINIT_EVENT:

            unity_nerf_destroy_texture(leftHandle);
            unity_nerf_destroy_texture(rightHandle);
            unity_nerf_deinitialize();
            break;

        case DEINIT_VULKAN:

            unity_nerf_deinit_ngx_vulkan();
            break;

    }
}

// --------------------------------------------------------------------------
// GetRenderEventFunc, an example function we export which is used to get a rendering event callback function.

extern "C" UnityRenderingEvent UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API GetRenderEventFunc(){

	return unity_nerf_run_on_render_thread;
}

extern "C" GLuint UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API unity_nerf_get_left_handle(){
    
    return leftHandle;
}

extern "C" GLuint UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API unity_nerf_get_right_handle(){

    return rightHandle;
}

extern "C" void UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API unity_nerf_update_stereo_view_matrix(float* left, float* right){
    view_matrix_left = left;
    view_matrix_right = right;
}

