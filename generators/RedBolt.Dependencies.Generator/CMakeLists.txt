redbolt_require_path("${REDBOLT_SHARPCPP_ROOT}" "REDBOLT_SHARPCPP_ROOT")

if(NOT REDBOLT_DEPENDENCIES_GENERATOR_DLL)
    set(REDBOLT_DEPENDENCIES_GENERATOR_DLL
        "${PROJECT_SOURCE_DIR}/build/bin/tools/${REDBOLT_PLATFORM}/$<CONFIG>/RedBolt.Dependencies.Generator.dll"
        CACHE FILEPATH "RedBolt dependency generator assembly")
endif()

file(GLOB_RECURSE REDBOLT_SHARPCPP_GENERATOR_INPUTS CONFIGURE_DEPENDS
    "${REDBOLT_SHARPCPP_ROOT}/src/SharpCpp.Model/*.cs"
    "${REDBOLT_SHARPCPP_ROOT}/src/SharpCpp.Metadata/*.cs"
    "${REDBOLT_SHARPCPP_ROOT}/src/SharpCpp.Parsing/*.cs"
    "${REDBOLT_SHARPCPP_ROOT}/src/SharpCpp.Pipeline/*.cs"
    "${REDBOLT_SHARPCPP_ROOT}/src/SharpCpp.Rendering/*.cs"
    "${REDBOLT_SHARPCPP_ROOT}/src/SharpCpp.Support/*.cs"
    "${REDBOLT_SHARPCPP_ROOT}/include/SharpCpp/*.h"
    "${REDBOLT_SHARPCPP_ROOT}/include/SharpCpp/*.hpp")
file(GLOB REDBOLT_DEPENDENCIES_GENERATOR_INPUTS CONFIGURE_DEPENDS
    "${CMAKE_CURRENT_SOURCE_DIR}/*.cs")

set(REDBOLT_SHARPCPP_GENERATOR_INPUTS "${REDBOLT_SHARPCPP_GENERATOR_INPUTS}" CACHE INTERNAL "SharpCpp generator inputs")
set(REDBOLT_DEPENDENCIES_GENERATOR_INPUTS "${REDBOLT_DEPENDENCIES_GENERATOR_INPUTS}" CACHE INTERNAL "RedBolt generator inputs")

set(REDBOLT_SDL_GENERATED_DIR "${REDBOLT_NATIVE_GENERATED_DIR}/sdl" CACHE INTERNAL "SDL binding output")
set(REDBOLT_DILIGENT_GENERATED_DIR "${REDBOLT_NATIVE_GENERATED_DIR}/diligent" CACHE INTERNAL "Diligent binding output")
set(REDBOLT_IMGUI_GENERATED_DIR "${REDBOLT_NATIVE_GENERATED_DIR}/dearimgui" CACHE INTERNAL "Dear ImGui binding output")
set(REDBOLT_STB_GENERATED_DIR "${REDBOLT_NATIVE_GENERATED_DIR}/stb" CACHE INTERNAL "STB binding output")
set(REDBOLT_LZ4_GENERATED_DIR "${REDBOLT_NATIVE_GENERATED_DIR}/lz4" CACHE INTERNAL "LZ4 binding output")
set(REDBOLT_WEBP_GENERATED_DIR "${REDBOLT_NATIVE_GENERATED_DIR}/webp" CACHE INTERNAL "WebP binding output")
set(REDBOLT_GLSLANG_GENERATED_DIR "${REDBOLT_NATIVE_GENERATED_DIR}/glslang" CACHE INTERNAL "glslang binding output")
set(REDBOLT_SPIRV_CROSS_GENERATED_DIR "${REDBOLT_NATIVE_GENERATED_DIR}/spirv-cross" CACHE INTERNAL "SPIRV-Cross binding output")
set(REDBOLT_SPIRV_TOOLS_GENERATED_DIR "${REDBOLT_NATIVE_GENERATED_DIR}/spirv-tools" CACHE INTERNAL "SPIRV-Tools binding output")
set(REDBOLT_SPIRV_OPTIMIZER_GENERATED_DIR "${REDBOLT_NATIVE_GENERATED_DIR}/spirv-optimizer" CACHE INTERNAL "SPIR-V optimizer binding output")
set(REDBOLT_SHADER_COMPILER_GENERATED_DIR "${REDBOLT_NATIVE_GENERATED_DIR}/shader-compiler" CACHE INTERNAL "Shader compiler binding output")

set(REDBOLT_SDL_BINDINGS_STEM "RedBolt.Bindings.Sdl" CACHE INTERNAL "SDL binding stem")
set(REDBOLT_DILIGENT_BINDINGS_STEM "RedBolt.Bindings.Diligent" CACHE INTERNAL "Diligent binding stem")
set(REDBOLT_IMGUI_BINDINGS_STEM "RedBolt.Bindings.DearImGui" CACHE INTERNAL "Dear ImGui binding stem")
set(REDBOLT_STB_BINDINGS_STEM "RedBolt.Bindings.Stb" CACHE INTERNAL "STB binding stem")
set(REDBOLT_LZ4_BINDINGS_STEM "RedBolt.Bindings.Lz4" CACHE INTERNAL "LZ4 binding stem")
set(REDBOLT_WEBP_BINDINGS_STEM "RedBolt.Bindings.WebP" CACHE INTERNAL "WebP binding stem")
set(REDBOLT_GLSLANG_BINDINGS_STEM "RedBolt.Bindings.Glslang" CACHE INTERNAL "glslang binding stem")
set(REDBOLT_SPIRV_CROSS_BINDINGS_STEM "RedBolt.Bindings.SpirVCross" CACHE INTERNAL "SPIRV-Cross binding stem")
set(REDBOLT_SPIRV_TOOLS_BINDINGS_STEM "RedBolt.Bindings.SpirVTools" CACHE INTERNAL "SPIRV-Tools binding stem")
set(REDBOLT_SPIRV_OPTIMIZER_BINDINGS_STEM "RedBolt.Bindings.SpirVOptimizer" CACHE INTERNAL "SPIR-V optimizer binding stem")
set(REDBOLT_SHADER_COMPILER_BINDINGS_STEM "RedBolt.Bindings.ShaderCompiler" CACHE INTERNAL "Shader compiler binding stem")

foreach(binding SDL DILIGENT IMGUI STB LZ4 WEBP GLSLANG SPIRV_CROSS SPIRV_TOOLS SPIRV_OPTIMIZER SHADER_COMPILER)
    set(REDBOLT_${binding}_BINDINGS_CS
        "${REDBOLT_${binding}_GENERATED_DIR}/${REDBOLT_${binding}_BINDINGS_STEM}.Bindings.g.cs"
        CACHE INTERNAL "${binding} generated C# binding")
    set(REDBOLT_${binding}_BINDINGS_CPP
        "${REDBOLT_${binding}_GENERATED_DIR}/${REDBOLT_${binding}_BINDINGS_STEM}.Bindings.g.cpp"
        CACHE INTERNAL "${binding} generated C++ binding")
    set(REDBOLT_${binding}_BINDINGS_JSON
        "${REDBOLT_${binding}_GENERATED_DIR}/${REDBOLT_${binding}_BINDINGS_STEM}.json"
        CACHE INTERNAL "${binding} binding metadata")
endforeach()

set(REDBOLT_SDL_BUILD_INCLUDE_DIR "${CMAKE_BINARY_DIR}/deps/SDL/include" CACHE INTERNAL "SDL generated include directory")
set(REDBOLT_SDL_CONFIG_INCLUDE_DIR "${CMAKE_BINARY_DIR}/deps/SDL/include-config-$<LOWER_CASE:$<CONFIG>>" CACHE INTERNAL "SDL configuration include directory")
set(REDBOLT_SDL_PARSE_SOURCE "${REDBOLT_SDL_GENERATED_DIR}/SDLAll.cpp" CACHE INTERNAL "SDL parse source")
set(REDBOLT_DILIGENT_PARSE_HEADER "${REDBOLT_DILIGENT_GENERATED_DIR}/DiligentAll.hpp" CACHE INTERNAL "Diligent parse header")
set(REDBOLT_IMGUI_PARSE_SOURCE "${REDBOLT_IMGUI_GENERATED_DIR}/ImGuiAll.cpp" CACHE INTERNAL "Dear ImGui parse source")
set(REDBOLT_STB_PARSE_HEADER "${REDBOLT_STB_GENERATED_DIR}/StbVorbisAll.hpp" CACHE INTERNAL "STB parse header")
set(REDBOLT_LZ4_PARSE_HEADER "${REDBOLT_LZ4_GENERATED_DIR}/Lz4All.hpp" CACHE INTERNAL "LZ4 parse header")
set(REDBOLT_WEBP_PARSE_HEADER "${REDBOLT_WEBP_GENERATED_DIR}/WebPAll.hpp" CACHE INTERNAL "WebP parse header")
set(REDBOLT_SPIRV_OPTIMIZER_PARSE_HEADER "${REDBOLT_SPIRV_OPTIMIZER_GENERATED_DIR}/SpirVOptimizerAll.hpp" CACHE INTERNAL "SPIR-V optimizer parse header")
set(REDBOLT_SHADER_COMPILER_PARSE_HEADER "${REDBOLT_SHADER_COMPILER_GENERATED_DIR}/ShaderCompilerAll.hpp" CACHE INTERNAL "Shader compiler parse header")

set(REDBOLT_STB_SOURCE_ROOT "${REDBOLT_THIRDPARTY_ROOT}/STB" CACHE INTERNAL "STB source root")
set(REDBOLT_STB_INCLUDE_DIR "${REDBOLT_STB_SOURCE_ROOT}/include" CACHE INTERNAL "STB include directory")
set(REDBOLT_STB_VORBIS_HEADER "${REDBOLT_STB_INCLUDE_DIR}/STB/stb_vorbis.h" CACHE INTERNAL "stb_vorbis header")
set(REDBOLT_STB_IMAGE_WRITE_HEADER "${REDBOLT_STB_INCLUDE_DIR}/STB/stb_image_write.h" CACHE INTERNAL "stb_image_write header")
set(REDBOLT_DILIGENT_SUPPORT_INCLUDE_DIR "${CMAKE_SOURCE_DIR}/native/RedBolt.Native/include" CACHE INTERNAL "RedBolt native support include directory" FORCE)
set(REDBOLT_DILIGENT_SUPPORT_HEADER "${REDBOLT_DILIGENT_SUPPORT_INCLUDE_DIR}/RedBolt/DiligentSupport.hpp" CACHE INTERNAL "RedBolt Diligent support header")
set(REDBOLT_IMGUI_INCLUDE_DIR "${REDBOLT_IMGUI_SOURCE_ROOT}" CACHE INTERNAL "Dear ImGui include directory")
set(REDBOLT_IMGUI_BACKEND_HEADER "${REDBOLT_DILIGENT_SUPPORT_INCLUDE_DIR}/RedBolt/ImGuiBackend.hpp" CACHE INTERNAL "Dear ImGui backend header")

set(REDBOLT_GLSLANG_SOURCE_ROOT "${REDBOLT_DILIGENT_SOURCE_ROOT}/ThirdParty/glslang" CACHE INTERNAL "glslang source root")
set(REDBOLT_SPIRV_CROSS_SOURCE_ROOT "${REDBOLT_DILIGENT_SOURCE_ROOT}/ThirdParty/SPIRV-Cross" CACHE INTERNAL "SPIRV-Cross source root")
set(REDBOLT_SPIRV_TOOLS_SOURCE_ROOT "${REDBOLT_DILIGENT_SOURCE_ROOT}/ThirdParty/SPIRV-Tools" CACHE INTERNAL "SPIRV-Tools source root")
set(REDBOLT_GLSLANG_C_HEADER "${REDBOLT_GLSLANG_SOURCE_ROOT}/glslang/Public/resource_limits_c.h" CACHE INTERNAL "glslang C header")
set(REDBOLT_SPIRV_CROSS_C_HEADER "${REDBOLT_SPIRV_CROSS_SOURCE_ROOT}/spirv_cross_c.h" CACHE INTERNAL "SPIRV-Cross C header")
set(REDBOLT_SPIRV_TOOLS_C_HEADER "${REDBOLT_SPIRV_TOOLS_SOURCE_ROOT}/include/spirv-tools/libspirv.h" CACHE INTERNAL "SPIRV-Tools C header")

redbolt_require_path("${REDBOLT_STB_VORBIS_HEADER}" "stb_vorbis header")
redbolt_require_path("${REDBOLT_STB_IMAGE_WRITE_HEADER}" "stb_image_write header")

file(MAKE_DIRECTORY
    "${REDBOLT_SDL_GENERATED_DIR}"
    "${REDBOLT_DILIGENT_GENERATED_DIR}"
    "${REDBOLT_IMGUI_GENERATED_DIR}"
    "${REDBOLT_STB_GENERATED_DIR}"
    "${REDBOLT_LZ4_GENERATED_DIR}"
    "${REDBOLT_WEBP_GENERATED_DIR}"
    "${REDBOLT_GLSLANG_GENERATED_DIR}"
    "${REDBOLT_SPIRV_CROSS_GENERATED_DIR}"
    "${REDBOLT_SPIRV_TOOLS_GENERATED_DIR}"
    "${REDBOLT_SPIRV_OPTIMIZER_GENERATED_DIR}"
    "${REDBOLT_SHADER_COMPILER_GENERATED_DIR}")

file(WRITE "${REDBOLT_STB_PARSE_HEADER}" [=[
#pragma once
#define STB_VORBIS_HEADER_ONLY
#include <STB/stb_vorbis.h>
#include <STB/stb_image_write.h>
STBIWDEF unsigned char* stbi_write_png_to_mem(
    const unsigned char* pixels, int stride_bytes, int x, int y, int n, int* out_len);
struct stb_vorbis { int opaque; };
]=])
file(WRITE "${REDBOLT_LZ4_PARSE_HEADER}" [=[
#pragma once
#include <lz4.h>
#include <lz4hc.h>
]=])
file(WRITE "${REDBOLT_WEBP_PARSE_HEADER}" [=[
#pragma once
#include <stddef.h>
#include <stdint.h>
#ifdef __cplusplus
extern "C" {
#endif
size_t WebPEncodeLosslessRGB(const void* rgb, int width, int height, int stride, void** output);
size_t WebPEncodeLosslessRGBA(const void* rgba, int width, int height, int stride, void** output);
void WebPFree(void* ptr);
#ifdef __cplusplus
}
#endif
]=])
file(WRITE "${REDBOLT_SPIRV_OPTIMIZER_PARSE_HEADER}" [=[
#pragma once
#include <RedBolt/SpirVOptimizerSupport.hpp>
]=])
file(WRITE "${REDBOLT_SHADER_COMPILER_PARSE_HEADER}" [=[
#pragma once
#include <RedBolt/ShaderCompilerSupport.hpp>
]=])
file(WRITE "${REDBOLT_SDL_PARSE_SOURCE}" [=[
#include <SDL_config.h>
#include <SDL.h>
#include <SDL_image.h>

#ifndef SDL_VIDEO_DISABLED
#include <SDL_syswm.h>
#endif

#if defined(SDL_VIDEO_VULKAN)
#include <SDL_vulkan.h>
#endif

#if defined(REDBOLT_GENERATE_SDL_TEST_BINDINGS)
#include <SDL_test.h>
#endif
]=])
file(WRITE "${REDBOLT_DILIGENT_PARSE_HEADER}" [=[
#pragma once
#include <EngineFactoryOpenGL.h>
#include <GraphicsAccessories.hpp>
#include <RedBolt/DiligentSupport.hpp>
]=])
file(WRITE "${REDBOLT_IMGUI_PARSE_SOURCE}" [=[
#include <SDL.h>
#include <imgui.h>
#define IMGUI_IMPL_API IMGUI_API
#include <imgui_impl_sdl2.h>
#include <RedBolt/ImGuiBackend.hpp>
]=])

set(REDBOLT_SDL_OWNED_HEADERS "${REDBOLT_SDL_BUILD_INCLUDE_DIR};${REDBOLT_SDL_IMAGE_HEADER}" CACHE INTERNAL "SDL owned headers")
set(REDBOLT_DILIGENT_OWNED_HEADERS "${REDBOLT_DILIGENT_INCLUDE_DIRS};${REDBOLT_DILIGENT_SUPPORT_INCLUDE_DIR};${REDBOLT_DILIGENT_SUPPORT_HEADER}" CACHE INTERNAL "Diligent owned headers" FORCE)
set(REDBOLT_IMGUI_OWNED_HEADERS "${REDBOLT_IMGUI_INCLUDE_DIR};${REDBOLT_IMGUI_SOURCE_ROOT}/backends;${REDBOLT_IMGUI_BACKEND_HEADER}" CACHE INTERNAL "Dear ImGui owned headers" FORCE)
set(REDBOLT_BINDING_INCLUDE_DIRS
    "${REDBOLT_SHARPCPP_ROOT}/include"
    ${REDBOLT_DILIGENT_INCLUDE_DIRS}
    CACHE INTERNAL "Include directories used by RedBolt SharpCpp dependency generation")
set(REDBOLT_BINDING_DEFINES
    ${REDBOLT_DILIGENT_BINDING_DEFINES}
    CACHE INTERNAL "Compile definitions used by RedBolt SharpCpp dependency generation")
