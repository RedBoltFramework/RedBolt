# Copyright (c) 2026 RedBolt and contributors.
# This work is licensed under the terms of the MIT license.
# For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

redbolt_require_path("${REDBOLT_SHARPCPP_ROOT}" "REDBOLT_SHARPCPP_ROOT")

set(REDBOLT_DEPENDENCIES_GENERATOR_DLL
    "${PROJECT_SOURCE_DIR}/build/bin/$<CONFIG>/host/tools/RedBolt.Generator.Dependencies.dll"
    CACHE FILEPATH "RedBolt dependency generator assembly")

function(_redbolt_define_binding binding directory stem)
    set(generated_dir "${REDBOLT_NATIVE_GENERATED_DIR}/${directory}")
    set(REDBOLT_${binding}_GENERATED_DIR "${generated_dir}" PARENT_SCOPE)
    set(REDBOLT_${binding}_BINDINGS_STEM "${stem}" PARENT_SCOPE)
    set(REDBOLT_${binding}_BINDINGS_CS "${generated_dir}/${stem}.Bindings.g.cs" PARENT_SCOPE)
    set(REDBOLT_${binding}_BINDINGS_CPP "${generated_dir}/${stem}.Bindings.g.cpp" PARENT_SCOPE)
    set(REDBOLT_${binding}_BINDINGS_JSON "${generated_dir}/${stem}.json" PARENT_SCOPE)
endfunction()

_redbolt_define_binding(SDL sdl RedBolt.Bindings.Sdl)
_redbolt_define_binding(DILIGENT diligent RedBolt.Bindings.Diligent)
_redbolt_define_binding(IMGUI dearimgui RedBolt.Bindings.DearImGui)
_redbolt_define_binding(STB stb RedBolt.Bindings.Stb)
_redbolt_define_binding(TINYGLTF tinygltf RedBolt.Bindings.TinyGltf)
_redbolt_define_binding(LZ4 lz4 RedBolt.Bindings.Lz4)
_redbolt_define_binding(HARFBUZZ harfbuzz RedBolt.Bindings.HarfBuzz)
_redbolt_define_binding(WEBP webp RedBolt.Bindings.WebP)
_redbolt_define_binding(GLSLANG glslang RedBolt.Bindings.Glslang)
_redbolt_define_binding(SPIRV_CROSS spirv-cross RedBolt.Bindings.SpirVCross)
_redbolt_define_binding(SPIRV_TOOLS spirv-tools RedBolt.Bindings.SpirVTools)
_redbolt_define_binding(SHADER_COMPILER shader-compiler RedBolt.Bindings.ShaderCompiler)

set(REDBOLT_SDL_BUILD_INCLUDE_DIR "${SDL2_BINARY_DIR}/include")
set(REDBOLT_SDL_CONFIG_INCLUDE_DIR "${SDL2_BINARY_DIR}/include-config-$<LOWER_CASE:$<CONFIG>>")
set(REDBOLT_SDL_PARSE_SOURCE "${REDBOLT_SDL_GENERATED_DIR}/SDLAll.cpp")
set(REDBOLT_DILIGENT_PARSE_HEADER "${REDBOLT_DILIGENT_GENERATED_DIR}/DiligentAll.hpp")
set(REDBOLT_IMGUI_PARSE_SOURCE "${REDBOLT_IMGUI_GENERATED_DIR}/ImGuiAll.cpp")
set(REDBOLT_STB_PARSE_HEADER "${REDBOLT_STB_GENERATED_DIR}/StbVorbisAll.hpp")
set(REDBOLT_TINYGLTF_PARSE_HEADER "${REDBOLT_TINYGLTF_GENERATED_DIR}/TinyGltfAll.hpp")
set(REDBOLT_LZ4_PARSE_HEADER "${REDBOLT_LZ4_GENERATED_DIR}/Lz4All.hpp")
set(REDBOLT_HARFBUZZ_PARSE_HEADER "${REDBOLT_HARFBUZZ_GENERATED_DIR}/HarfBuzzAll.h")
set(REDBOLT_WEBP_PARSE_HEADER "${REDBOLT_WEBP_GENERATED_DIR}/WebPAll.hpp")
set(REDBOLT_SHADER_COMPILER_PARSE_HEADER "${REDBOLT_SHADER_COMPILER_GENERATED_DIR}/ShaderCompilerAll.hpp")
set(REDBOLT_DILIGENT_SUPPORT_INCLUDE_DIR "${CMAKE_SOURCE_DIR}/src/RedBolt/Native/Diligent")
set(REDBOLT_DILIGENT_SUPPORT_HEADER "${REDBOLT_DILIGENT_SUPPORT_INCLUDE_DIR}/DiligentSupport.hpp")
set(REDBOLT_HARFBUZZ_SUPPORT_INCLUDE_DIR "${CMAKE_SOURCE_DIR}/src/RedBolt/Native/HarfBuzz")
set(REDBOLT_IMGUI_SUPPORT_INCLUDE_DIR "${CMAKE_SOURCE_DIR}/src/RedBolt/Native/DearImGui")
set(REDBOLT_SHADER_COMPILER_SUPPORT_INCLUDE_DIR "${CMAKE_SOURCE_DIR}/src/RedBolt/Native/ShaderCompiler")
set(REDBOLT_IMGUI_INCLUDE_DIR "${REDBOLT_IMGUI_SOURCE_ROOT}")
set(REDBOLT_IMGUI_BACKEND_HEADER "${REDBOLT_IMGUI_SUPPORT_INCLUDE_DIR}/ImGuiBackend.hpp")

set(REDBOLT_GLSLANG_SOURCE_ROOT "${REDBOLT_DILIGENT_SOURCE_ROOT}/ThirdParty/glslang")
set(REDBOLT_SPIRV_CROSS_SOURCE_ROOT "${REDBOLT_DILIGENT_SOURCE_ROOT}/ThirdParty/SPIRV-Cross")
set(REDBOLT_SPIRV_TOOLS_SOURCE_ROOT "${REDBOLT_DILIGENT_SOURCE_ROOT}/ThirdParty/SPIRV-Tools")
set(REDBOLT_GLSLANG_C_HEADER "${REDBOLT_GLSLANG_SOURCE_ROOT}/glslang/Public/resource_limits_c.h")
set(REDBOLT_SPIRV_CROSS_C_HEADER "${REDBOLT_SPIRV_CROSS_SOURCE_ROOT}/spirv_cross_c.h")
set(REDBOLT_SPIRV_TOOLS_C_HEADER "${REDBOLT_SPIRV_TOOLS_SOURCE_ROOT}/include/spirv-tools/libspirv.h")

redbolt_require_path("${REDBOLT_STB_VORBIS_HEADER}" "stb_vorbis header")
redbolt_require_path("${REDBOLT_STB_IMAGE_HEADER}" "stb_image header")
redbolt_require_path("${REDBOLT_STB_IMAGE_WRITE_HEADER}" "stb_image_write header")

file(MAKE_DIRECTORY
    "${REDBOLT_SDL_GENERATED_DIR}"
    "${REDBOLT_DILIGENT_GENERATED_DIR}"
    "${REDBOLT_IMGUI_GENERATED_DIR}"
    "${REDBOLT_STB_GENERATED_DIR}"
    "${REDBOLT_TINYGLTF_GENERATED_DIR}"
    "${REDBOLT_LZ4_GENERATED_DIR}"
    "${REDBOLT_HARFBUZZ_GENERATED_DIR}"
    "${REDBOLT_WEBP_GENERATED_DIR}"
    "${REDBOLT_GLSLANG_GENERATED_DIR}"
    "${REDBOLT_SPIRV_CROSS_GENERATED_DIR}"
    "${REDBOLT_SPIRV_TOOLS_GENERATED_DIR}"
    "${REDBOLT_SHADER_COMPILER_GENERATED_DIR}")

file(CONFIGURE OUTPUT "${REDBOLT_STB_PARSE_HEADER}" CONTENT [=[
#pragma once
#define STB_VORBIS_HEADER_ONLY
#include <stb_vorbis.c>
#include <stb_image.h>
#include <stb_image_write.h>
STBIWDEF unsigned char* stbi_write_png_to_mem(
    const unsigned char* pixels, int stride_bytes, int x, int y, int n, int* out_len);
struct stb_vorbis { int opaque; };
]=] @ONLY)
file(CONFIGURE OUTPUT "${REDBOLT_TINYGLTF_PARSE_HEADER}" CONTENT [=[
#pragma once
#include <tiny_gltf.h>
]=] @ONLY)
file(CONFIGURE OUTPUT "${REDBOLT_LZ4_PARSE_HEADER}" CONTENT [=[
#pragma once
#include <lz4.h>
#include <lz4hc.h>
]=] @ONLY)
file(CONFIGURE OUTPUT "${REDBOLT_HARFBUZZ_PARSE_HEADER}" CONTENT [=[
#pragma once
#include <hb.h>
#include <hb-ot.h>
#include <hb-raster.h>
#include <hb-gpu.h>
]=] @ONLY)
file(CONFIGURE OUTPUT "${REDBOLT_WEBP_PARSE_HEADER}" CONTENT [=[
#pragma once
#include <webp/decode.h>
#include <webp/encode.h>
]=] @ONLY)
file(CONFIGURE OUTPUT "${REDBOLT_SHADER_COMPILER_PARSE_HEADER}" CONTENT [=[
#pragma once
#include <ShaderCompilerSupport.hpp>
]=] @ONLY)
file(CONFIGURE OUTPUT "${REDBOLT_SDL_PARSE_SOURCE}" CONTENT [=[
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
]=] @ONLY)
set(REDBOLT_DILIGENT_OPENXR_INCLUDE)
if(REDBOLT_ENABLE_OPENXR AND NOT EMSCRIPTEN)
    set(REDBOLT_DILIGENT_OPENXR_INCLUDE "#include <OpenXRUtilities.h>")
endif()
file(CONFIGURE OUTPUT "${REDBOLT_DILIGENT_PARSE_HEADER}" CONTENT [=[
#pragma once
#include <cstdint>
#if !PLATFORM_WEB
#undef UINTPTR_MAX
#define UINTPTR_MAX UINT64_MAX
#endif
#include <EngineFactoryOpenGL.h>
#include <GraphicsAccessories.hpp>
#include <DiligentSupport.hpp>
@REDBOLT_DILIGENT_OPENXR_INCLUDE@
]=] @ONLY)
file(CONFIGURE OUTPUT "${REDBOLT_IMGUI_PARSE_SOURCE}" CONTENT [=[
#include <SDL.h>
#include <imgui.h>
#include <imgui_internal.h>
#include <ImGuizmo.h>
#define IMGUI_IMPL_API IMGUI_API
#include <imgui_impl_sdl2.h>
#include <ImGuiBackend.hpp>

IMGUI_IMPL_API ImGuiKey ImGui_ImplSDL2_KeyEventToImGuiKey(SDL_Keycode keycode, SDL_Scancode scancode);

namespace ImGui
{
#define REDBOLT_IMGUI_TEXT_ARGS(text) "%.*s", ((text) ? (text).length() : 0), ((text) ? (text).Begin : "")
inline void Text(ImStrv text) { Text(REDBOLT_IMGUI_TEXT_ARGS(text)); }
inline void TextColored(const ImVec4& color, ImStrv text) { TextColored(color, REDBOLT_IMGUI_TEXT_ARGS(text)); }
inline void TextDisabled(ImStrv text) { TextDisabled(REDBOLT_IMGUI_TEXT_ARGS(text)); }
inline void TextWrapped(ImStrv text) { TextWrapped(REDBOLT_IMGUI_TEXT_ARGS(text)); }
inline void LabelText(ImStrv label, ImStrv text) { LabelText(label, REDBOLT_IMGUI_TEXT_ARGS(text)); }
inline void BulletText(ImStrv text) { BulletText(REDBOLT_IMGUI_TEXT_ARGS(text)); }
inline bool TreeNode(ImStrv id, ImStrv text) { return TreeNode(id, REDBOLT_IMGUI_TEXT_ARGS(text)); }
inline bool TreeNode(const void* id, ImStrv text) { return TreeNode(id, REDBOLT_IMGUI_TEXT_ARGS(text)); }
inline bool TreeNodeEx(ImStrv id, ImGuiTreeNodeFlags flags, ImStrv text) { return TreeNodeEx(id, flags, REDBOLT_IMGUI_TEXT_ARGS(text)); }
inline bool TreeNodeEx(const void* id, ImGuiTreeNodeFlags flags, ImStrv text) { return TreeNodeEx(id, flags, REDBOLT_IMGUI_TEXT_ARGS(text)); }
inline void SetTooltip(ImStrv text) { SetTooltip(REDBOLT_IMGUI_TEXT_ARGS(text)); }
inline void SetItemTooltip(ImStrv text) { SetItemTooltip(REDBOLT_IMGUI_TEXT_ARGS(text)); }
inline void LogText(ImStrv text) { LogText(REDBOLT_IMGUI_TEXT_ARGS(text)); }
#ifndef IMGUI_DISABLE_DEBUG_TOOLS
inline void DebugLog(ImStrv text) { DebugLog(REDBOLT_IMGUI_TEXT_ARGS(text)); }
#endif
#undef REDBOLT_IMGUI_TEXT_ARGS
}
]=] @ONLY)

set(REDBOLT_SDL_OWNED_HEADERS "${REDBOLT_SDL_BUILD_INCLUDE_DIR};${REDBOLT_SDL_IMAGE_HEADER}")
set(REDBOLT_DILIGENT_OWNED_HEADERS "${REDBOLT_DILIGENT_INCLUDE_DIRS};${REDBOLT_DILIGENT_SUPPORT_INCLUDE_DIR};${REDBOLT_DILIGENT_SUPPORT_HEADER}")
set(REDBOLT_IMGUI_OWNED_HEADERS "${REDBOLT_IMGUI_INCLUDE_DIR};${REDBOLT_IMGUI_SOURCE_ROOT}/backends;${REDBOLT_IMGUIZMO_SOURCE_ROOT};${REDBOLT_IMGUI_BACKEND_HEADER}")
set(REDBOLT_BINDING_INCLUDE_DIRS
    "${REDBOLT_SHARPCPP_ROOT}/include"
    ${REDBOLT_DILIGENT_INCLUDE_DIRS})
if(MINGW)
    list(PREPEND REDBOLT_BINDING_INCLUDE_DIRS
        "${CMAKE_SOURCE_DIR}/cmake/Toolchains/MinGW/include")
endif()
set(REDBOLT_BINDING_DEFINES ${REDBOLT_DILIGENT_BINDING_DEFINES})
if(CMAKE_CONFIGURATION_TYPES)
    list(APPEND REDBOLT_BINDING_DEFINES "$<$<NOT:$<CONFIG:Debug>>:NDEBUG>")
elseif(CMAKE_BUILD_TYPE MATCHES "^(Release|Distribution|ReleaseASAN|ReleaseUBSAN|ReleaseTSAN|ReleaseCoverage)$")
    list(APPEND REDBOLT_BINDING_DEFINES NDEBUG)
endif()
