# Copyright (c) 2026 RedBolt and contributors.
# This work is licensed under the terms of the MIT license.
# For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

redbolt_require_path("${REDBOLT_SHARPCPP_ROOT}" "REDBOLT_SHARPCPP_ROOT")

if(NOT REDBOLT_DEPENDENCIES_GENERATOR_DLL)
    set(REDBOLT_DEPENDENCIES_GENERATOR_DLL
        "${PROJECT_SOURCE_DIR}/build/bin/$<CONFIG>/host/tools/RedBolt.Generator.Dependencies.dll"
        CACHE FILEPATH "RedBolt dependency generator assembly")
endif()

file(GLOB_RECURSE REDBOLT_SHARPCPP_GENERATOR_INPUTS CONFIGURE_DEPENDS
    "${REDBOLT_SHARPCPP_ROOT}/src/SharpCpp.Model/*.cs"
    "${REDBOLT_SHARPCPP_ROOT}/src/SharpCpp.Metadata/*.cs"
    "${REDBOLT_SHARPCPP_ROOT}/src/SharpCpp.Parsing/*.cs"
    "${REDBOLT_SHARPCPP_ROOT}/src/SharpCpp.Pipeline/*.cs"
    "${REDBOLT_SHARPCPP_ROOT}/src/SharpCpp.Rendering/*.cs"
    "${REDBOLT_SHARPCPP_ROOT}/src/SharpCpp.Support/*.cs"
    "${REDBOLT_SHARPCPP_ROOT}/src/SharpCpp.Model/*.csproj"
    "${REDBOLT_SHARPCPP_ROOT}/src/SharpCpp.Metadata/*.csproj"
    "${REDBOLT_SHARPCPP_ROOT}/src/SharpCpp.Parsing/*.csproj"
    "${REDBOLT_SHARPCPP_ROOT}/src/SharpCpp.Pipeline/*.csproj"
    "${REDBOLT_SHARPCPP_ROOT}/src/SharpCpp.Rendering/*.csproj"
    "${REDBOLT_SHARPCPP_ROOT}/src/SharpCpp.Support/*.csproj"
    "${REDBOLT_SHARPCPP_ROOT}/include/SharpCpp/*.h"
    "${REDBOLT_SHARPCPP_ROOT}/include/SharpCpp/*.hpp")
file(GLOB_RECURSE REDBOLT_DEPENDENCIES_GENERATOR_INPUTS CONFIGURE_DEPENDS
    "${CMAKE_CURRENT_SOURCE_DIR}/*.cs"
    "${CMAKE_CURRENT_SOURCE_DIR}/*.csproj")
list(APPEND REDBOLT_DEPENDENCIES_GENERATOR_INPUTS
    "${CMAKE_SOURCE_DIR}/src/RedBolt/Native/DearImGui/ImGuiValueMarshallers.hpp")

set(REDBOLT_SHARPCPP_GENERATOR_INPUTS "${REDBOLT_SHARPCPP_GENERATOR_INPUTS}" CACHE INTERNAL "SharpCpp generator inputs")
set(REDBOLT_DEPENDENCIES_GENERATOR_INPUTS "${REDBOLT_DEPENDENCIES_GENERATOR_INPUTS}" CACHE INTERNAL "RedBolt generator inputs")

set(REDBOLT_SDL_GENERATED_DIR "${REDBOLT_NATIVE_GENERATED_DIR}/sdl" CACHE INTERNAL "SDL binding output")
set(REDBOLT_DILIGENT_GENERATED_DIR "${REDBOLT_NATIVE_GENERATED_DIR}/diligent" CACHE INTERNAL "Diligent binding output")
set(REDBOLT_IMGUI_GENERATED_DIR "${REDBOLT_NATIVE_GENERATED_DIR}/dearimgui" CACHE INTERNAL "Dear ImGui binding output")
set(REDBOLT_STB_GENERATED_DIR "${REDBOLT_NATIVE_GENERATED_DIR}/stb" CACHE INTERNAL "STB binding output")
set(REDBOLT_TINYGLTF_GENERATED_DIR "${REDBOLT_NATIVE_GENERATED_DIR}/tinygltf" CACHE INTERNAL "TinyGLTF binding output")
set(REDBOLT_LZ4_GENERATED_DIR "${REDBOLT_NATIVE_GENERATED_DIR}/lz4" CACHE INTERNAL "LZ4 binding output")
set(REDBOLT_HARFBUZZ_GENERATED_DIR "${REDBOLT_NATIVE_GENERATED_DIR}/harfbuzz" CACHE INTERNAL "HarfBuzz binding output")
set(REDBOLT_WEBP_GENERATED_DIR "${REDBOLT_NATIVE_GENERATED_DIR}/webp" CACHE INTERNAL "WebP binding output")
set(REDBOLT_GLSLANG_GENERATED_DIR "${REDBOLT_NATIVE_GENERATED_DIR}/glslang" CACHE INTERNAL "glslang binding output")
set(REDBOLT_SPIRV_CROSS_GENERATED_DIR "${REDBOLT_NATIVE_GENERATED_DIR}/spirv-cross" CACHE INTERNAL "SPIRV-Cross binding output")
set(REDBOLT_SPIRV_TOOLS_GENERATED_DIR "${REDBOLT_NATIVE_GENERATED_DIR}/spirv-tools" CACHE INTERNAL "SPIRV-Tools binding output")
set(REDBOLT_SHADER_COMPILER_GENERATED_DIR "${REDBOLT_NATIVE_GENERATED_DIR}/shader-compiler" CACHE INTERNAL "Shader compiler binding output")

set(REDBOLT_SDL_BINDINGS_STEM "RedBolt.Bindings.Sdl" CACHE INTERNAL "SDL binding stem")
set(REDBOLT_DILIGENT_BINDINGS_STEM "RedBolt.Bindings.Diligent" CACHE INTERNAL "Diligent binding stem")
set(REDBOLT_IMGUI_BINDINGS_STEM "RedBolt.Bindings.DearImGui" CACHE INTERNAL "Dear ImGui binding stem")
set(REDBOLT_STB_BINDINGS_STEM "RedBolt.Bindings.Stb" CACHE INTERNAL "STB binding stem")
set(REDBOLT_TINYGLTF_BINDINGS_STEM "RedBolt.Bindings.TinyGltf" CACHE INTERNAL "TinyGLTF binding stem")
set(REDBOLT_LZ4_BINDINGS_STEM "RedBolt.Bindings.Lz4" CACHE INTERNAL "LZ4 binding stem")
set(REDBOLT_HARFBUZZ_BINDINGS_STEM "RedBolt.Bindings.HarfBuzz" CACHE INTERNAL "HarfBuzz binding stem")
set(REDBOLT_WEBP_BINDINGS_STEM "RedBolt.Bindings.WebP" CACHE INTERNAL "WebP binding stem")
set(REDBOLT_GLSLANG_BINDINGS_STEM "RedBolt.Bindings.Glslang" CACHE INTERNAL "glslang binding stem")
set(REDBOLT_SPIRV_CROSS_BINDINGS_STEM "RedBolt.Bindings.SpirVCross" CACHE INTERNAL "SPIRV-Cross binding stem")
set(REDBOLT_SPIRV_TOOLS_BINDINGS_STEM "RedBolt.Bindings.SpirVTools" CACHE INTERNAL "SPIRV-Tools binding stem")
set(REDBOLT_SHADER_COMPILER_BINDINGS_STEM "RedBolt.Bindings.ShaderCompiler" CACHE INTERNAL "Shader compiler binding stem")

foreach(binding SDL DILIGENT IMGUI STB TINYGLTF LZ4 HARFBUZZ WEBP GLSLANG SPIRV_CROSS SPIRV_TOOLS SHADER_COMPILER)
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

set(REDBOLT_SDL_BUILD_INCLUDE_DIR "${SDL2_BINARY_DIR}/include" CACHE INTERNAL "SDL generated include directory" FORCE)
set(REDBOLT_SDL_CONFIG_INCLUDE_DIR "${SDL2_BINARY_DIR}/include-config-$<LOWER_CASE:$<CONFIG>>" CACHE INTERNAL "SDL configuration include directory" FORCE)
set(REDBOLT_SDL_PARSE_SOURCE "${REDBOLT_SDL_GENERATED_DIR}/SDLAll.cpp" CACHE INTERNAL "SDL parse source")
set(REDBOLT_DILIGENT_PARSE_HEADER "${REDBOLT_DILIGENT_GENERATED_DIR}/DiligentAll.hpp" CACHE INTERNAL "Diligent parse header")
set(REDBOLT_IMGUI_PARSE_SOURCE "${REDBOLT_IMGUI_GENERATED_DIR}/ImGuiAll.cpp" CACHE INTERNAL "Dear ImGui parse source")
set(REDBOLT_STB_PARSE_HEADER "${REDBOLT_STB_GENERATED_DIR}/StbVorbisAll.hpp" CACHE INTERNAL "STB parse header")
set(REDBOLT_TINYGLTF_PARSE_HEADER "${REDBOLT_TINYGLTF_GENERATED_DIR}/TinyGltfAll.hpp" CACHE INTERNAL "TinyGLTF parse header")
set(REDBOLT_LZ4_PARSE_HEADER "${REDBOLT_LZ4_GENERATED_DIR}/Lz4All.hpp" CACHE INTERNAL "LZ4 parse header")
set(REDBOLT_HARFBUZZ_PARSE_HEADER "${REDBOLT_HARFBUZZ_GENERATED_DIR}/HarfBuzzAll.h" CACHE INTERNAL "HarfBuzz parse header")
set(REDBOLT_WEBP_PARSE_HEADER "${REDBOLT_WEBP_GENERATED_DIR}/WebPAll.hpp" CACHE INTERNAL "WebP parse header")
set(REDBOLT_SHADER_COMPILER_PARSE_HEADER "${REDBOLT_SHADER_COMPILER_GENERATED_DIR}/ShaderCompilerAll.hpp" CACHE INTERNAL "Shader compiler parse header")
set(REDBOLT_DILIGENT_SUPPORT_INCLUDE_DIR "${CMAKE_SOURCE_DIR}/src/RedBolt/Native/Diligent" CACHE INTERNAL "RedBolt Diligent support include directory" FORCE)
set(REDBOLT_DILIGENT_SUPPORT_HEADER "${REDBOLT_DILIGENT_SUPPORT_INCLUDE_DIR}/DiligentSupport.hpp" CACHE INTERNAL "RedBolt Diligent support header")
set(REDBOLT_HARFBUZZ_SUPPORT_INCLUDE_DIR "${CMAKE_SOURCE_DIR}/src/RedBolt/Native/HarfBuzz" CACHE INTERNAL "RedBolt HarfBuzz support include directory" FORCE)
set(REDBOLT_IMGUI_SUPPORT_INCLUDE_DIR "${CMAKE_SOURCE_DIR}/src/RedBolt/Native/DearImGui" CACHE INTERNAL "RedBolt Dear ImGui support include directory" FORCE)
set(REDBOLT_SHADER_COMPILER_SUPPORT_INCLUDE_DIR "${CMAKE_SOURCE_DIR}/src/RedBolt/Native/ShaderCompiler" CACHE INTERNAL "RedBolt shader compiler support include directory" FORCE)
set(REDBOLT_IMGUI_INCLUDE_DIR "${REDBOLT_IMGUI_SOURCE_ROOT}" CACHE INTERNAL "Dear ImGui include directory")
set(REDBOLT_IMGUI_BACKEND_HEADER "${REDBOLT_IMGUI_SUPPORT_INCLUDE_DIR}/ImGuiBackend.hpp" CACHE INTERNAL "Dear ImGui backend header")

set(REDBOLT_GLSLANG_SOURCE_ROOT "${REDBOLT_DILIGENT_SOURCE_ROOT}/ThirdParty/glslang" CACHE INTERNAL "glslang source root")
set(REDBOLT_SPIRV_CROSS_SOURCE_ROOT "${REDBOLT_DILIGENT_SOURCE_ROOT}/ThirdParty/SPIRV-Cross" CACHE INTERNAL "SPIRV-Cross source root")
set(REDBOLT_SPIRV_TOOLS_SOURCE_ROOT "${REDBOLT_DILIGENT_SOURCE_ROOT}/ThirdParty/SPIRV-Tools" CACHE INTERNAL "SPIRV-Tools source root")
set(REDBOLT_GLSLANG_C_HEADER "${REDBOLT_GLSLANG_SOURCE_ROOT}/glslang/Public/resource_limits_c.h" CACHE INTERNAL "glslang C header")
set(REDBOLT_SPIRV_CROSS_C_HEADER "${REDBOLT_SPIRV_CROSS_SOURCE_ROOT}/spirv_cross_c.h" CACHE INTERNAL "SPIRV-Cross C header")
set(REDBOLT_SPIRV_TOOLS_C_HEADER "${REDBOLT_SPIRV_TOOLS_SOURCE_ROOT}/include/spirv-tools/libspirv.h" CACHE INTERNAL "SPIRV-Tools C header")

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

set(REDBOLT_SDL_OWNED_HEADERS "${REDBOLT_SDL_BUILD_INCLUDE_DIR};${REDBOLT_SDL_IMAGE_HEADER}" CACHE INTERNAL "SDL owned headers")
set(REDBOLT_DILIGENT_OWNED_HEADERS "${REDBOLT_DILIGENT_INCLUDE_DIRS};${REDBOLT_DILIGENT_SUPPORT_INCLUDE_DIR};${REDBOLT_DILIGENT_SUPPORT_HEADER}" CACHE INTERNAL "Diligent owned headers" FORCE)
set(REDBOLT_IMGUI_OWNED_HEADERS "${REDBOLT_IMGUI_INCLUDE_DIR};${REDBOLT_IMGUI_SOURCE_ROOT}/backends;${REDBOLT_IMGUIZMO_SOURCE_ROOT};${REDBOLT_IMGUI_BACKEND_HEADER}" CACHE INTERNAL "Dear ImGui owned headers" FORCE)
set(REDBOLT_BINDING_INCLUDE_DIRS
    "${REDBOLT_SHARPCPP_ROOT}/include"
    ${REDBOLT_DILIGENT_INCLUDE_DIRS}
    CACHE INTERNAL "Include directories used by RedBolt SharpCpp dependency generation")
if(MINGW)
    list(PREPEND REDBOLT_BINDING_INCLUDE_DIRS
        "${CMAKE_SOURCE_DIR}/cmake/Toolchains/MinGW/include")
endif()
set(REDBOLT_BINDING_INCLUDE_DIRS
    "${REDBOLT_BINDING_INCLUDE_DIRS}"
    CACHE INTERNAL "Include directories used by RedBolt SharpCpp dependency generation" FORCE)
set(_redbolt_binding_defines ${REDBOLT_DILIGENT_BINDING_DEFINES})
if(CMAKE_CONFIGURATION_TYPES)
    list(APPEND _redbolt_binding_defines "$<$<NOT:$<CONFIG:Debug>>:NDEBUG>")
elseif(CMAKE_BUILD_TYPE MATCHES "^(Release|Distribution|ReleaseASAN|ReleaseUBSAN|ReleaseTSAN|ReleaseCoverage)$")
    list(APPEND _redbolt_binding_defines NDEBUG)
endif()
set(REDBOLT_BINDING_DEFINES
    ${_redbolt_binding_defines}
    CACHE INTERNAL "Compile definitions used by RedBolt SharpCpp dependency generation" FORCE)
