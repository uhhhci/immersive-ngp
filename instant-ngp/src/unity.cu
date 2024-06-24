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
using namespace std::literals::chrono_literals;

struct TextureData {
    TextureData(const Texture& tex, const Texture& depth_tex, const RenderBuffer& buf, int width, int heigth)
    : surface_texture(tex), depth_texture(depth_tex), render_buffer(buf), width(width), height(height) {
    }
    ~TextureData(){
        surface_texture.reset();
        depth_texture.reset();
        render_buffer.reset();
    };
    Texture surface_texture;
    Texture depth_texture;
    RenderBuffer render_buffer;
    int width;
    int height;
};
static std::shared_ptr<ngp::Testbed> testbed = nullptr;
static std::unordered_map<GLuint, std::shared_ptr<TextureData>> textures;

// flags
bool graphics_initialized = false;
bool use_dlss = false;
bool use_depth = false;
static int _width;
static int _height;
static int _prev_width;
static int _prev_height;
static GLuint leftHandle;
static GLuint rightHandle;
static GLuint leftHandleDepth  = 0;
static GLuint rightHandleDepth = 0;
GLuint* left_handles;
GLuint* right_handles;
float zero_matrix[12] = {0,0,0,0,0,0,0,0,0,0,0,0};
float* view_matrix_left;
float* view_matrix_right;

extern "C" void UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API unity_nerf_set_initialize_values(const char* scene, const char* snapshot, bool dlss, bool _use_depth, int width, int height){

    use_dlss = dlss;
    _width = width;
    _height = height;
    _prev_width = width;
    _prev_height = height;
    use_depth = _use_depth;
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
    
    // performance setting recommended by the original instant-ngp
    testbed->m_background_color = {0.0f, 0.0f, 0.0f, 0.0f};
    // performance setting recommended by the original instant-ngp
    testbed->m_nerf.render_min_transmittance = 0.2f;

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
    glBindTexture(GL_TEXTURE_2D, 0);
    glDeleteTextures(1, &leftHandleDepth);
    glDeleteTextures(1, &rightHandleDepth);

    testbed.reset();
    glfwTerminate();
    leftHandle = 0;
    rightHandle = 0;
    leftHandleDepth  =  0;
    rightHandleDepth =  0;
    view_matrix_left = NULL;
    view_matrix_right = NULL;
    graphics_initialized = false;
    use_dlss = false;
    tlog::info() << "instant ngp testbed deinitialized" ;

}

static GLuint* UNITY_INTERFACE_API unity_nerf_create_texture(int width, int height) {
    
    if (!testbed){
        tlog::info() << "testbed not found!!" ;
        return 0;
    }
    GLuint* textureHandles = new GLuint[2];
    auto texture   = std::make_shared<ngp::GLTexture>();
    auto depth_tex = std::make_shared<ngp::GLTexture>();
    auto buffer    = std::make_shared<ngp::CudaRenderBuffer>(texture, depth_tex);
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

        //buffer->set_dlss_sharpening(1.0);
    }
    else{ buffer->disable_dlss();}
#endif

    buffer->resize(render_res);

    GLuint handle = texture->texture();
    GLuint handle_depth = depth_tex->texture();

    textures[handle] = std::make_shared<TextureData>(
        texture,
        depth_tex,
        buffer,
        width,
        height
    );

    textureHandles[0] = handle;
    textureHandles[1] = handle_depth;
   // tlog::info() << "GLTexture handle" << handle ;
    return textureHandles;
}


void UNITY_INTERFACE_API unity_nerf_update_texture() {


    if (!testbed){
        tlog::error() << "testbed not found" ;
        return;
    }

    auto left  = textures.find(leftHandle);
    auto right = textures.find(rightHandle);

    if (left == std::end(textures)) {
        tlog::error() << "left texture handle not found" ;
        return;
    }
    if (right == std::end(textures)) {
        tlog::error() << "right texture handle not found" ;
        return;
    }

    Eigen::Matrix<float, 3, 4> camera_left {view_matrix_left};
    Eigen::Matrix<float, 3, 4> camera_right {view_matrix_right};

    RenderBuffer render_buffer_left  = left->second->render_buffer;
    RenderBuffer render_buffer_right = right->second->render_buffer;
    Eigen::Vector2i texture_res { _width, _height };
    Eigen::Vector2i dlss_res { _width, _height };

    {
        // update render latency
        {
        // frame time => we don't need this in instant ngp, since this needs to be calculated in Unity
            auto now = std::chrono::steady_clock::now();
            auto elapsed = now - testbed->m_last_frame_time_point;
            testbed->m_last_frame_time_point = now;
            testbed->m_render_ms.update(std::chrono::duration<float, std::milli>(elapsed).count());
        // auto start = std::chrono::steady_clock::now();
        // tcnn::ScopeGuard timing_guard{[&]() {
        //     testbed->m_render_ms.update(std::chrono::duration<float, std::milli>(std::chrono::steady_clock::now()-start).count());
        // }};
        }
        // implement dynamic resolution here
        if(_prev_width != _width || _prev_height!= _height){
            
            dlss_res = render_buffer_left->in_resolution();
            if (dlss_res.isZero()) {
                dlss_res = texture_res / 16;
            } else {
                dlss_res = dlss_res.cwiseMin(texture_res);
            }
            Eigen::Vector2i dlss_res2 {dlss_res.x(), dlss_res.y() };

            if (render_buffer_left->dlss()) {

			    dlss_res = render_buffer_left ->dlss()->clamp_resolution(dlss_res );
                dlss_res = render_buffer_right->dlss()->clamp_resolution(dlss_res2);
		    }
            render_buffer_left ->resize(dlss_res);
            render_buffer_right->resize(dlss_res);
        }


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

        if(use_depth){

            render_buffer_left -> render_depth(ngp::EColorSpace::SRGB, testbed->m_stream.get());
            render_buffer_right-> render_depth(ngp::EColorSpace::SRGB, testbed->m_stream.get());
        }

        render_buffer_left ->reset_accumulation();
        render_buffer_right->reset_accumulation();
    
    }
    _prev_width  = _width;
    _prev_height = _height;

    left->second->width  = _width;
    left->second->height = _height;
    right->second->width = _width;
    right->second->height= _height;

    testbed->reset_accumulation(true);

}
extern "C" void UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API unity_nerf_save_snapshot(const char* filename){
    if (!testbed){
        tlog::error() << "testbed not found" ;
        return;
    }
    tlog::info() << "testbed not found" ;

    testbed->save_snapshot(filename, false);
};


extern "C" void UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API unity_nerf_update_resolution(int width, int height){
    
    // this function must be called in Unity before initiate the "unity_nerf_update_texture" event.
    
    if (!testbed){
        tlog::error() << "testbed not found" ;
        return;
    }

    _width = width;
    _height = height;

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
    found->second->depth_texture.reset();
    found->second.reset();
    tlog::info() << "GLTexture and render buffer destroyed" ;

}

// remove certain render volume
extern "C" void UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API unity_nerf_set_all_density_grid_empty(){
    if (!testbed)
    return;
    testbed->mark_all_density_grid_empty(testbed->m_stream.get());
}

// remove certain render volume
extern "C" void UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API unity_nerf_reveal_all_masked_density(){
    if (!testbed)
    return;
    testbed->reveal_all_masked_density(testbed->m_stream.get());
}

// remove certain render volume
extern "C" void UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API unity_nerf_mark_density_grid_empty(float* pos, float scale){
    if (!testbed)
    return;
    Eigen::Vector3<float> remove_pos {pos};
    testbed->mark_density_grid_in_sphere_empty(remove_pos, scale*0.05f, testbed->m_stream.get());
}
// erase density grid defined by a box
extern "C" void UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API unity_nerf_empty_density_grid_in_box(float* pos, float box_width, float box_height, float box_length, float* R){
    if (!testbed)
    return;
    Eigen::Vector3<float> remove_pos {pos};
    Eigen::Matrix<float, 3, 3> _R {R};
    testbed->erase_volume_density_in_box(remove_pos, box_width, box_height, box_length, _R, testbed->m_stream.get());
}
// reveal certain render area
extern "C" void UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API unity_nerf_reveal_density_grid_area(float* pos, float scale){
    if (!testbed)
    return;
    Eigen::Vector3<float> remove_pos {pos};
    testbed->reveal_volume_density_in_sphere(remove_pos, scale*0.05f, testbed->m_stream.get());
}
// reveal density grid defined by a box
extern "C" void UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API unity_nerf_reveal_density_grid_in_box(float* pos, float box_width, float box_height, float box_length, float* R){
    if (!testbed)
    return;
    Eigen::Vector3<float> remove_pos {pos};
    Eigen::Matrix<float, 3, 3> _R {R};
    testbed->reveal_volume_density_in_box(remove_pos, box_width, box_height, box_length, _R, testbed->m_stream.get());
}
// utility functions

extern "C" void UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API unity_nerf_reset_camera(){
    if (!testbed)
    return;
    testbed->reset_camera();
}

extern "C" void UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API unity_nerf_set_render_fov(float val){
    if (!testbed)
        return;
    testbed->set_fov(val);

};

extern "C" void UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API unity_nerf_set_scale(float val){
    if (!testbed)
        return;

    testbed->set_scale(val);
};

extern "C" void UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API unity_nerf_set_look_at(float val[]){
    if (!testbed)
        return;
    testbed->set_look_at({val[0],val[1],val[2]});
};

extern "C" void UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API unity_nerf_get_look_at(float val[]){
    if (!testbed)
        return;
    float* v = testbed->look_at().data();
    for(int i=0; i<3; i++){
        val[i] = v[i];
    }
}

extern "C" void UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API unity_nerf_get_view_dir(float val[]){
    if (!testbed)
        return;
    float* v = testbed->view_dir().data();
    for(int i=0; i<3; i++){
        val[i] = v[i];
    }
};

extern "C" void UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API unity_nerf_set_view_dir(float val[]){
    if (!testbed)
        return;
    testbed->set_view_dir({val[0], val[1], val[2]});
};


extern "C" void UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API unity_nerf_update_aabb_crop(float* min_vec, float* max_vec){

    if (!testbed)
        return;

    Eigen::Vector3f min_aabb {min_vec};
    Eigen::Vector3f max_aabb {max_vec};
    
    testbed->m_render_aabb = ngp::BoundingBox(min_aabb, max_aabb);

}

extern "C" void UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API unity_nerf_update_aabb(float* min_vec, float* max_vec){
    // this update the generate scene where the user can see.
    if (!testbed)
        return;

    Eigen::Vector3f min_aabb {min_vec};
    Eigen::Vector3f max_aabb {max_vec};
    
    testbed->m_aabb = ngp::BoundingBox(min_aabb, max_aabb);
}

// how to pass array ptr to unity:  https://bravenewmethod.com/2017/10/30/unity-c-native-plugin-examples/
// how to visualize the unit cube in unity and manipulate it: 
// look at line 1239-1264 in testbed.cu for the instant-ngp implementation
extern "C" UNITY_INTERFACE_EXPORT float* UNITY_INTERFACE_API unity_nerf_get_render_aabb_min(){

    if (!testbed)
        return NULL;
    float* min = testbed->m_render_aabb.min.data();
    return min;

} 

extern "C" UNITY_INTERFACE_EXPORT float* UNITY_INTERFACE_API unity_nerf_get_render_aabb_max(){
    
    if (!testbed)
        return NULL;
    float* max = testbed->m_render_aabb.max.data();
    return max;

};

extern "C" UNITY_INTERFACE_EXPORT float* UNITY_INTERFACE_API unity_nerf_get_aabb_min(){
    if(!testbed)
        return NULL;
    float* min = testbed->m_aabb.min.data();
    return min;
};

extern "C" UNITY_INTERFACE_EXPORT float* UNITY_INTERFACE_API unity_nerf_get_aabb_max(){
    if(!testbed)
        return NULL;

    float* max = testbed->m_aabb.max.data();
    return max;
};


extern "C" void UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API unity_nerf_get_render_aabb_to_local(float arr[]){

    if (!testbed)
        return;
    float* aabb_to_local = testbed->m_render_aabb_to_local.data();
    for(int i =0; i< 9; i++){
        arr[i] = aabb_to_local[i];
    }
} 

extern "C" void UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API unity_nerf_get_crop_box_transform(float arr[]){
    if (!testbed)
        return;
    // eigen is column major 
    float* ptr2 = testbed ->crop_box(false).data();
    for (int i = 0; i < testbed ->crop_box(false).size(); i++) {
        arr[i] = ptr2[i];
    }
}

extern "C" void UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API unity_nerf_set_crop_box_transform(float* cropbox){
    if (!testbed)
        return;
    Eigen::Matrix<float, 3, 4> m { cropbox };
    testbed->set_crop_box(m, false);
};

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

            left_handles  = unity_nerf_create_texture(_width, _height);
            right_handles = unity_nerf_create_texture(_width, _height);
            
            leftHandle  = left_handles[0];
            rightHandle = right_handles[0];
            leftHandleDepth  = left_handles[1];
            rightHandleDepth = right_handles[1];

            delete[] left_handles;
            delete[] right_handles;

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

extern "C" GLuint UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API unity_nerf_get_left_depth_handle(){
    
    return leftHandleDepth;
}

extern "C" GLuint UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API unity_nerf_get_right_depth_handle(){

    return rightHandleDepth;
}

extern "C" void UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API unity_nerf_update_stereo_view_matrix(float* left, float* right){
    view_matrix_left = left;
    view_matrix_right = right;
}

extern "C" float UNITY_INTERFACE_EXPORT UNITY_INTERFACE_API unity_nerf_get_render_ms(){
    if (!testbed){
        tlog::error() << "testbed not found" ;
        return 0;
    }
    return testbed->m_render_ms.ema_val();
}