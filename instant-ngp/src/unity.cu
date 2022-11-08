#include "neural-graphics-primitives/unity.h"

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

    Texture surface_texture;
    RenderBuffer render_buffer;
    int width;
    int height;
};

static bool already_initalized = false;
static bool use_dlss = false;
static UnityTextureID nullHandle;
static std::shared_ptr<ngp::Testbed> testbed = nullptr;
static std::unordered_map<GLuint, std::shared_ptr<TextureData>> textures;

extern "C" void unity_nerf_initialize(const char* scene, const char* snapshot, bool dlss) { 
    if (already_initalized) {
        std::cout << "Already initalized nerf" << std::endl;
        return;
    }

    use_dlss = dlss;
    already_initalized = true;

    testbed = std::make_shared<ngp::Testbed>(
        ngp::ETestbedMode::Nerf,
		scene
    );

    if (snapshot) {
        testbed->load_snapshot(
			snapshot
        );
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
            std::cout << "Could not initialize vulkan" << std::endl;
        }
    }
#endif
}

extern "C" void unity_nerf_deinitialize() {
    
#ifdef NGP_VULKAN    
    if (use_dlss) { 
        ngp::vulkan_and_ngx_destroy();
    }
#endif
    already_initalized = false;
    testbed.reset();
    glfwTerminate();
}

extern "C" UnityTextureID unity_nerf_create_texture(int width, int height) {
    if (!testbed)
        return 0;

    // gladly ngp already implements gl textures for us
    // so we just need to call GLTexture to create a new one.
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
#endif

    buffer->resize(render_res);

    GLuint handle = texture->texture();
    // int* handle_ptr = new int;
    // *handle_ptr = static_cast<int>(handle);

    textures[texture->texture()] = std::make_shared<TextureData>(
        texture,
        buffer,
        width,
        height
    );

    // return the opengl texture handle
    // But unity fails to find the functions otherwise :/
    return handle;
}

extern "C" void unity_nerf_update_texture(float* camera_matrix, UnityTextureID handle) {
    if (!testbed)
        return;

    // GLuint handle = static_cast<GLuint>(*handle_ptr);
    auto found = textures.find(handle);
    if (found == std::end(textures)) {
        return;
    }

    Eigen::Matrix<float, 3, 4> camera { camera_matrix };

    RenderBuffer render_buffer = found->second->render_buffer;
    render_buffer->reset_accumulation();
    testbed->render_frame(camera,//testbed->m_camera,
                          camera,//testbed->m_camera,
                          Eigen::Vector4f::Zero(),
                          *render_buffer.get(),
                          true);
}

extern "C" void unity_nerf_update_aabb_crop(float* min_vec, float* max_vec){
    if (!testbed)
    return;

    Eigen::Vector3f min_aabb {min_vec};
    Eigen::Vector3f max_aabb {max_vec};
    
    testbed->m_render_aabb = ngp::BoundingBox(min_aabb, max_aabb);
    
}

extern "C" void unity_nerf_destroy_texture(UnityTextureID handle) {
    if (!testbed)
        return;

    // @TODO add warnings and stuff
    // GLuint handle = static_cast<GLuint>(*handle_ptr);
    auto found = textures.find(handle); 
    if (found == std::end(textures)) {
        return;
    } 

    found->second->surface_texture.reset();
    found->second->render_buffer.reset();

    found->second.reset();
    // delete handle_ptr;
}


// utility functions

extern "C" void unity_nerf_reset_camera(){
    if (!testbed)
    return;
    testbed->reset_camera();
}
