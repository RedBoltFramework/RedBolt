cmake_minimum_required(VERSION 3.28)

project(RedBoltFramework VERSION 0.1.0 LANGUAGES C CXX)

option(REDBOLT_WEB_BUILD "Build the framework-free WebAssembly target only" OFF)
if(EMSCRIPTEN)
    add_compile_options(-pthread -mbulk-memory)
    set_property(GLOBAL PROPERTY RULE_LAUNCH_LINK "${_redbolt_emscripten_environment}")
endif()

set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)
set(CMAKE_POSITION_INDEPENDENT_CODE ON)
set(CMAKE_CXX_SCAN_FOR_MODULES OFF)

set(REDBOLT_THIRDPARTY_ROOT "${CMAKE_CURRENT_SOURCE_DIR}/extern/thirdparty" CACHE PATH "Path to RedBolt's copied native dependency sources" FORCE)
set(REDBOLT_SHARPCPP_ROOT "${CMAKE_CURRENT_SOURCE_DIR}/SharpCpp" CACHE PATH "Path to the SharpCpp source tree used by RedBolt generators")

set(REDBOLT_BIN_DIR "${CMAKE_BINARY_DIR}/bin/$<CONFIG>")
set(REDBOLT_GENERATED_ROOT "${CMAKE_BINARY_DIR}/generated")
set(REDBOLT_NATIVE_GENERATED_DIR "${REDBOLT_GENERATED_ROOT}/redbolt-native")
set(REDBOLT_SDL_GENERATED_DIR "${REDBOLT_NATIVE_GENERATED_DIR}/sdl")
set(REDBOLT_DILIGENT_GENERATED_DIR "${REDBOLT_NATIVE_GENERATED_DIR}/diligent")
set(REDBOLT_IMGUI_GENERATED_DIR "${REDBOLT_NATIVE_GENERATED_DIR}/dearimgui")
set(REDBOLT_STB_GENERATED_DIR "${REDBOLT_NATIVE_GENERATED_DIR}/stb")
set(REDBOLT_SDL_BINDINGS_STEM "RedBolt.Bindings.Sdl")
set(REDBOLT_DILIGENT_BINDINGS_STEM "RedBolt.Bindings.Diligent")
set(REDBOLT_IMGUI_BINDINGS_STEM "RedBolt.Bindings.DearImGui")
set(REDBOLT_SDL_BINDINGS_CS "${REDBOLT_SDL_GENERATED_DIR}/${REDBOLT_SDL_BINDINGS_STEM}.Bindings.g.cs")
set(REDBOLT_SDL_BINDINGS_CPP "${REDBOLT_SDL_GENERATED_DIR}/${REDBOLT_SDL_BINDINGS_STEM}.Bindings.g.cpp")
set(REDBOLT_SDL_BINDINGS_JSON "${REDBOLT_SDL_GENERATED_DIR}/RedBolt.Bindings.Sdl.json")
set(REDBOLT_SDL_PARSE_SOURCE "${REDBOLT_SDL_GENERATED_DIR}/SDLAll.cpp")
set(REDBOLT_SDL_BUILD_INCLUDE_DIR "${CMAKE_BINARY_DIR}/deps/SDL/include")
set(REDBOLT_SDL_CONFIG_INCLUDE_DIR "${CMAKE_BINARY_DIR}/deps/SDL/include-config-$<LOWER_CASE:$<CONFIG>>")
set(REDBOLT_DILIGENT_BINDINGS_CS "${REDBOLT_DILIGENT_GENERATED_DIR}/${REDBOLT_DILIGENT_BINDINGS_STEM}.Bindings.g.cs")
set(REDBOLT_DILIGENT_BINDINGS_CPP "${REDBOLT_DILIGENT_GENERATED_DIR}/${REDBOLT_DILIGENT_BINDINGS_STEM}.Bindings.g.cpp")
set(REDBOLT_DILIGENT_BINDINGS_JSON "${REDBOLT_DILIGENT_GENERATED_DIR}/RedBolt.Bindings.Diligent.json")
set(REDBOLT_IMGUI_BINDINGS_CS "${REDBOLT_IMGUI_GENERATED_DIR}/${REDBOLT_IMGUI_BINDINGS_STEM}.Bindings.g.cs")
set(REDBOLT_IMGUI_BINDINGS_CPP "${REDBOLT_IMGUI_GENERATED_DIR}/${REDBOLT_IMGUI_BINDINGS_STEM}.Bindings.g.cpp")
set(REDBOLT_IMGUI_BINDINGS_JSON "${REDBOLT_IMGUI_GENERATED_DIR}/RedBolt.Bindings.DearImGui.json")
set(REDBOLT_STB_BINDINGS_STEM "RedBolt.Bindings.Stb")
set(REDBOLT_STB_BINDINGS_CS "${REDBOLT_STB_GENERATED_DIR}/${REDBOLT_STB_BINDINGS_STEM}.Bindings.g.cs")
set(REDBOLT_STB_BINDINGS_CPP "${REDBOLT_STB_GENERATED_DIR}/${REDBOLT_STB_BINDINGS_STEM}.Bindings.g.cpp")
set(REDBOLT_STB_BINDINGS_JSON "${REDBOLT_STB_GENERATED_DIR}/${REDBOLT_STB_BINDINGS_STEM}.json")
set(REDBOLT_LZ4_GENERATED_DIR "${REDBOLT_NATIVE_GENERATED_DIR}/lz4")
set(REDBOLT_WEBP_GENERATED_DIR "${REDBOLT_NATIVE_GENERATED_DIR}/webp")
set(REDBOLT_LZ4_BINDINGS_STEM "RedBolt.Bindings.Lz4")
set(REDBOLT_LZ4_BINDINGS_CS "${REDBOLT_LZ4_GENERATED_DIR}/${REDBOLT_LZ4_BINDINGS_STEM}.Bindings.g.cs")
set(REDBOLT_LZ4_BINDINGS_CPP "${REDBOLT_LZ4_GENERATED_DIR}/${REDBOLT_LZ4_BINDINGS_STEM}.Bindings.g.cpp")
set(REDBOLT_LZ4_BINDINGS_JSON "${REDBOLT_LZ4_GENERATED_DIR}/${REDBOLT_LZ4_BINDINGS_STEM}.json")
set(REDBOLT_WEBP_BINDINGS_STEM "RedBolt.Bindings.WebP")
set(REDBOLT_WEBP_BINDINGS_CS "${REDBOLT_WEBP_GENERATED_DIR}/${REDBOLT_WEBP_BINDINGS_STEM}.Bindings.g.cs")
set(REDBOLT_WEBP_BINDINGS_CPP "${REDBOLT_WEBP_GENERATED_DIR}/${REDBOLT_WEBP_BINDINGS_STEM}.Bindings.g.cpp")
set(REDBOLT_WEBP_BINDINGS_JSON "${REDBOLT_WEBP_GENERATED_DIR}/${REDBOLT_WEBP_BINDINGS_STEM}.json")
set(REDBOLT_GLSLANG_GENERATED_DIR "${REDBOLT_NATIVE_GENERATED_DIR}/glslang")
set(REDBOLT_SPIRV_CROSS_GENERATED_DIR "${REDBOLT_NATIVE_GENERATED_DIR}/spirv-cross")
set(REDBOLT_SPIRV_TOOLS_GENERATED_DIR "${REDBOLT_NATIVE_GENERATED_DIR}/spirv-tools")
set(REDBOLT_SPIRV_OPTIMIZER_GENERATED_DIR "${REDBOLT_NATIVE_GENERATED_DIR}/spirv-optimizer")
set(REDBOLT_SHADER_COMPILER_GENERATED_DIR "${REDBOLT_NATIVE_GENERATED_DIR}/shader-compiler")
set(REDBOLT_SPIRV_OPTIMIZER_PARSE_HEADER "${REDBOLT_SPIRV_OPTIMIZER_GENERATED_DIR}/SpirVOptimizerAll.hpp")
set(REDBOLT_SHADER_COMPILER_PARSE_HEADER "${REDBOLT_SHADER_COMPILER_GENERATED_DIR}/ShaderCompilerAll.hpp")
set(REDBOLT_GLSLANG_BINDINGS_STEM "RedBolt.Bindings.Glslang")
set(REDBOLT_SPIRV_CROSS_BINDINGS_STEM "RedBolt.Bindings.SpirVCross")
set(REDBOLT_SPIRV_TOOLS_BINDINGS_STEM "RedBolt.Bindings.SpirVTools")
set(REDBOLT_SPIRV_OPTIMIZER_BINDINGS_STEM "RedBolt.Bindings.SpirVOptimizer")
set(REDBOLT_SHADER_COMPILER_BINDINGS_STEM "RedBolt.Bindings.ShaderCompiler")
foreach(shader_surface GLSLANG SPIRV_CROSS SPIRV_TOOLS)
    set(REDBOLT_${shader_surface}_BINDINGS_CS "${REDBOLT_${shader_surface}_GENERATED_DIR}/${REDBOLT_${shader_surface}_BINDINGS_STEM}.Bindings.g.cs")
    set(REDBOLT_${shader_surface}_BINDINGS_CPP "${REDBOLT_${shader_surface}_GENERATED_DIR}/${REDBOLT_${shader_surface}_BINDINGS_STEM}.Bindings.g.cpp")
    set(REDBOLT_${shader_surface}_BINDINGS_JSON "${REDBOLT_${shader_surface}_GENERATED_DIR}/${REDBOLT_${shader_surface}_BINDINGS_STEM}.json")
endforeach()
set(REDBOLT_SPIRV_OPTIMIZER_BINDINGS_CS "${REDBOLT_SPIRV_OPTIMIZER_GENERATED_DIR}/${REDBOLT_SPIRV_OPTIMIZER_BINDINGS_STEM}.Bindings.g.cs")
set(REDBOLT_SPIRV_OPTIMIZER_BINDINGS_CPP "${REDBOLT_SPIRV_OPTIMIZER_GENERATED_DIR}/${REDBOLT_SPIRV_OPTIMIZER_BINDINGS_STEM}.Bindings.g.cpp")
set(REDBOLT_SPIRV_OPTIMIZER_BINDINGS_JSON "${REDBOLT_SPIRV_OPTIMIZER_GENERATED_DIR}/${REDBOLT_SPIRV_OPTIMIZER_BINDINGS_STEM}.json")
set(REDBOLT_SHADER_COMPILER_BINDINGS_CS "${REDBOLT_SHADER_COMPILER_GENERATED_DIR}/${REDBOLT_SHADER_COMPILER_BINDINGS_STEM}.Bindings.g.cs")
set(REDBOLT_SHADER_COMPILER_BINDINGS_CPP "${REDBOLT_SHADER_COMPILER_GENERATED_DIR}/${REDBOLT_SHADER_COMPILER_BINDINGS_STEM}.Bindings.g.cpp")
set(REDBOLT_SHADER_COMPILER_BINDINGS_JSON "${REDBOLT_SHADER_COMPILER_GENERATED_DIR}/${REDBOLT_SHADER_COMPILER_BINDINGS_STEM}.json")
set(REDBOLT_LZ4_PARSE_HEADER "${REDBOLT_LZ4_GENERATED_DIR}/Lz4All.hpp")
set(REDBOLT_WEBP_PARSE_HEADER "${REDBOLT_WEBP_GENERATED_DIR}/WebPAll.hpp")
set(REDBOLT_STB_SOURCE_ROOT "${REDBOLT_THIRDPARTY_ROOT}/STB" CACHE PATH "Path to RedBolt's copied STB sources" FORCE)
set(REDBOLT_STB_INCLUDE_DIR "${REDBOLT_STB_SOURCE_ROOT}/include")
set(REDBOLT_STB_VORBIS_HEADER "${REDBOLT_STB_INCLUDE_DIR}/STB/stb_vorbis.h")
set(REDBOLT_STB_IMAGE_WRITE_HEADER "${REDBOLT_STB_INCLUDE_DIR}/STB/stb_image_write.h")
set(REDBOLT_STB_PARSE_HEADER "${REDBOLT_STB_GENERATED_DIR}/StbVorbisAll.hpp")
set(REDBOLT_IMGUI_PARSE_SOURCE "${REDBOLT_IMGUI_GENERATED_DIR}/ImGuiAll.cpp")
set(REDBOLT_DILIGENT_PARSE_HEADER "${REDBOLT_DILIGENT_GENERATED_DIR}/DiligentAll.hpp")
set(REDBOLT_DILIGENT_SUPPORT_INCLUDE_DIR "${CMAKE_CURRENT_SOURCE_DIR}/native/RedBolt.Native/include")
set(REDBOLT_DILIGENT_SUPPORT_HEADER "${REDBOLT_DILIGENT_SUPPORT_INCLUDE_DIR}/RedBolt/DiligentSupport.hpp")
set(REDBOLT_SHARPCPP_E2E_GENERATED_ROOT "${REDBOLT_GENERATED_ROOT}/sharpcpp-e2e")
set(REDBOLT_SHARPCPP_E2E_FILE_STEM "SharpCppTestsE2E")
set(REDBOLT_SHARPCPP_E2E_GENERATED_STAMP "${REDBOLT_SHARPCPP_E2E_GENERATED_ROOT}/generate.stamp")
set(REDBOLT_SHARPCPP_E2E_GENERATOR_LOG_FILE "${REDBOLT_SHARPCPP_E2E_GENERATED_ROOT}/generator.log")
set(REDBOLT_DOTNET_SOLUTION "${CMAKE_CURRENT_SOURCE_DIR}/RedBolt.slnx")
set(REDBOLT_SHARPCPP_E2E_COMPAT_PROPS "${REDBOLT_SHARPCPP_ROOT}/.CMakeLast.props")
set(REDBOLT_SHARPCPP_E2E_NATIVE_NAME "${CMAKE_SHARED_LIBRARY_PREFIX}SharpCppTestsE2E_Native${CMAKE_SHARED_LIBRARY_SUFFIX}")

set(REDBOLT_SHARPCPP_E2E_COMPAT_PROPS_CONTENT "<Project>\n")
string(APPEND REDBOLT_SHARPCPP_E2E_COMPAT_PROPS_CONTENT
    "  <PropertyGroup Condition=\"'$(Configuration)' == ''\">\n"
    "    <SharpCppCMakeBuildDir>${CMAKE_BINARY_DIR}</SharpCppCMakeBuildDir>\n"
    "    <SharpCppOutputDir>${CMAKE_BINARY_DIR}/bin/Debug</SharpCppOutputDir>\n"
    "    <SharpCppIntermediateRoot>${CMAKE_BINARY_DIR}/obj/managed/Debug</SharpCppIntermediateRoot>\n"
    "    <SharpCppE2ENativeLibraryPath>${CMAKE_BINARY_DIR}/bin/Debug/${REDBOLT_SHARPCPP_E2E_NATIVE_NAME}</SharpCppE2ENativeLibraryPath>\n"
    "  </PropertyGroup>\n"
    "  <PropertyGroup Condition=\"'$(Configuration)' == 'Debug'\">\n"
    "    <SharpCppCMakeBuildDir>${CMAKE_BINARY_DIR}</SharpCppCMakeBuildDir>\n"
    "    <SharpCppOutputDir>${CMAKE_BINARY_DIR}/bin/Debug</SharpCppOutputDir>\n"
    "    <SharpCppIntermediateRoot>${CMAKE_BINARY_DIR}/obj/managed/Debug</SharpCppIntermediateRoot>\n"
    "    <SharpCppE2ENativeLibraryPath>${CMAKE_BINARY_DIR}/bin/Debug/${REDBOLT_SHARPCPP_E2E_NATIVE_NAME}</SharpCppE2ENativeLibraryPath>\n"
    "  </PropertyGroup>\n"
    "  <PropertyGroup Condition=\"'$(Configuration)' == 'Release'\">\n"
    "    <SharpCppCMakeBuildDir>${CMAKE_BINARY_DIR}</SharpCppCMakeBuildDir>\n"
    "    <SharpCppOutputDir>${CMAKE_BINARY_DIR}/bin/Release</SharpCppOutputDir>\n"
    "    <SharpCppIntermediateRoot>${CMAKE_BINARY_DIR}/obj/managed/Release</SharpCppIntermediateRoot>\n"
    "    <SharpCppE2ENativeLibraryPath>${CMAKE_BINARY_DIR}/bin/Release/${REDBOLT_SHARPCPP_E2E_NATIVE_NAME}</SharpCppE2ENativeLibraryPath>\n"
    "  </PropertyGroup>\n"
    "  <PropertyGroup Condition=\"'$(Configuration)' == 'RelWithDebInfo'\">\n"
    "    <SharpCppCMakeBuildDir>${CMAKE_BINARY_DIR}</SharpCppCMakeBuildDir>\n"
    "    <SharpCppOutputDir>${CMAKE_BINARY_DIR}/bin/RelWithDebInfo</SharpCppOutputDir>\n"
    "    <SharpCppIntermediateRoot>${CMAKE_BINARY_DIR}/obj/managed/RelWithDebInfo</SharpCppIntermediateRoot>\n"
    "    <SharpCppE2ENativeLibraryPath>${CMAKE_BINARY_DIR}/bin/RelWithDebInfo/${REDBOLT_SHARPCPP_E2E_NATIVE_NAME}</SharpCppE2ENativeLibraryPath>\n"
    "  </PropertyGroup>\n"
    "  <PropertyGroup Condition=\"'$(MSBuildProjectName)' == 'SharpCpp.Tests.E2E'\">\n"
    "    <SharpCppGeneratedDir>${REDBOLT_SHARPCPP_E2E_GENERATED_ROOT}</SharpCppGeneratedDir>\n"
    "    <SharpCppGeneratedStamp>${REDBOLT_SHARPCPP_E2E_GENERATED_STAMP}</SharpCppGeneratedStamp>\n"
    "  </PropertyGroup>\n"
    "</Project>\n")
file(WRITE "${REDBOLT_SHARPCPP_E2E_COMPAT_PROPS}" "${REDBOLT_SHARPCPP_E2E_COMPAT_PROPS_CONTENT}")

set(REDBOLT_SHARPCPP_E2E_TOOL "${REDBOLT_SHARPCPP_ROOT}/src/SharpCpp.Tool/SharpCpp.Tool.csproj")
set(REDBOLT_SHARPCPP_E2E_CSPROJ "${REDBOLT_SHARPCPP_ROOT}/tests/SharpCpp.Tests.E2E/SharpCpp.Tests.E2E.csproj")

set(CMAKE_RUNTIME_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/bin")
set(CMAKE_LIBRARY_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/bin")
set(CMAKE_ARCHIVE_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/lib")

file(MAKE_DIRECTORY "${CMAKE_BINARY_DIR}/bin")
file(GLOB REDBOLT_RESOURCE_DIRECTORIES LIST_DIRECTORIES true CONFIGURE_DEPENDS
    "${CMAKE_CURRENT_SOURCE_DIR}/bin/*")
foreach(redbolt_resource_directory IN LISTS REDBOLT_RESOURCE_DIRECTORIES)
    get_filename_component(redbolt_resource_name "${redbolt_resource_directory}" NAME)
    set(redbolt_resource_link "${CMAKE_BINARY_DIR}/bin/${redbolt_resource_name}")
    if(NOT EXISTS "${redbolt_resource_link}")
        file(CREATE_LINK "${redbolt_resource_directory}" "${redbolt_resource_link}" SYMBOLIC)
    endif()
endforeach()

include(cmake/RedBoltDependencies.cmake)
include(cmake/RedBoltDotNet.cmake)
include(cmake/RedBoltSharpCpp.cmake)

set(REDBOLT_GLSLANG_SOURCE_ROOT "${REDBOLT_DILIGENT_SOURCE_ROOT}/ThirdParty/glslang")
set(REDBOLT_SPIRV_CROSS_SOURCE_ROOT "${REDBOLT_DILIGENT_SOURCE_ROOT}/ThirdParty/SPIRV-Cross")
set(REDBOLT_SPIRV_TOOLS_SOURCE_ROOT "${REDBOLT_DILIGENT_SOURCE_ROOT}/ThirdParty/SPIRV-Tools")
set(REDBOLT_GLSLANG_C_HEADER "${REDBOLT_GLSLANG_SOURCE_ROOT}/glslang/Public/resource_limits_c.h")
set(REDBOLT_SPIRV_CROSS_C_HEADER "${REDBOLT_SPIRV_CROSS_SOURCE_ROOT}/spirv_cross_c.h")
set(REDBOLT_SPIRV_TOOLS_C_HEADER "${REDBOLT_SPIRV_TOOLS_SOURCE_ROOT}/include/spirv-tools/libspirv.h")

find_program(DOTNET_EXECUTABLE dotnet REQUIRED)
redbolt_add_external_subdirectory("${REDBOLT_THIRDPARTY_ROOT}/LZ4" "${CMAKE_BINARY_DIR}/deps/LZ4")
redbolt_add_external_subdirectory("${REDBOLT_THIRDPARTY_ROOT}/WebP" "${CMAKE_BINARY_DIR}/deps/WebP")
set(REDBOLT_LZ4_INCLUDE_DIR "${REDBOLT_THIRDPARTY_ROOT}/LZ4/include/LZ4")
set(REDBOLT_LZ4_LIBRARY LZ4)
set(REDBOLT_WEBP_INCLUDE_DIR "${REDBOLT_THIRDPARTY_ROOT}/WebP/src")
set(REDBOLT_WEBP_LIBRARY WebP)

redbolt_initialize_dotnet_consumer(
    CONSUMER_ROOT "${CMAKE_CURRENT_SOURCE_DIR}"
    OUTPUT_ROOT "${CMAKE_BINARY_DIR}/bin"
    INTERMEDIATE_ROOT "${CMAKE_BINARY_DIR}/obj/managed"
    DOTNET_STATE_DIR "${CMAKE_BINARY_DIR}/cmake/dotnet"
    PROPS_FILE "${CMAKE_BINARY_DIR}/cmake/dotnet/RedBolt.CMake.props"
    COMPAT_PROPS_FILE "${CMAKE_CURRENT_SOURCE_DIR}/.CMakeLast.props"
    GENERATED_BINDINGS_DIR "${REDBOLT_NATIVE_GENERATED_DIR}"
    SHARPCPP_ROOT "${REDBOLT_SHARPCPP_ROOT}")

if(NOT EXISTS "${REDBOLT_STB_VORBIS_HEADER}")
    message(FATAL_ERROR "rbfx stb_vorbis.h was not found at ${REDBOLT_STB_VORBIS_HEADER}")
endif()
if(NOT EXISTS "${REDBOLT_STB_IMAGE_WRITE_HEADER}")
    message(FATAL_ERROR "rbfx stb_image_write.h was not found at ${REDBOLT_STB_IMAGE_WRITE_HEADER}")
endif()

file(MAKE_DIRECTORY "${REDBOLT_SDL_GENERATED_DIR}" "${REDBOLT_DILIGENT_GENERATED_DIR}" "${REDBOLT_IMGUI_GENERATED_DIR}" "${REDBOLT_STB_GENERATED_DIR}" "${REDBOLT_LZ4_GENERATED_DIR}" "${REDBOLT_WEBP_GENERATED_DIR}" "${REDBOLT_GLSLANG_GENERATED_DIR}" "${REDBOLT_SPIRV_CROSS_GENERATED_DIR}" "${REDBOLT_SPIRV_TOOLS_GENERATED_DIR}" "${REDBOLT_SPIRV_OPTIMIZER_GENERATED_DIR}" "${REDBOLT_SHADER_COMPILER_GENERATED_DIR}")
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

set(REDBOLT_DILIGENT_INTERFACE_DIRS
    "${REDBOLT_DILIGENT_SOURCE_ROOT}/Primitives/interface"
    "${REDBOLT_DILIGENT_SOURCE_ROOT}/Platforms/interface"
    "${REDBOLT_DILIGENT_SOURCE_ROOT}/Platforms/Basic/interface"
    "${REDBOLT_DILIGENT_SOURCE_ROOT}/Platforms/Linux/interface"
    "${REDBOLT_DILIGENT_SOURCE_ROOT}/Common/interface"
    "${REDBOLT_DILIGENT_SOURCE_ROOT}/Graphics/GraphicsEngine/interface"
    "${REDBOLT_DILIGENT_SOURCE_ROOT}/Graphics/GraphicsEngineOpenGL/interface"
    "${REDBOLT_DILIGENT_SOURCE_ROOT}/Graphics/HLSL2GLSLConverterLib/interface")

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

set(REDBOLT_SDL_OWNED_HEADERS "${REDBOLT_SDL_BUILD_INCLUDE_DIR}")
if(REDBOLT_SDL_IMAGE_HEADER)
    list(APPEND REDBOLT_SDL_OWNED_HEADERS "${REDBOLT_SDL_IMAGE_HEADER}")
endif()

set(REDBOLT_DILIGENT_OWNED_HEADERS
    ${REDBOLT_DILIGENT_INCLUDE_DIRS}
    "${REDBOLT_DILIGENT_SUPPORT_INCLUDE_DIR}"
    "${REDBOLT_DILIGENT_SUPPORT_HEADER}")

set(REDBOLT_IMGUI_INCLUDE_DIR "${REDBOLT_IMGUI_SOURCE_ROOT}")
set(REDBOLT_IMGUI_BACKEND_HEADER "${CMAKE_CURRENT_SOURCE_DIR}/native/RedBolt.Native/include/RedBolt/ImGuiBackend.hpp")
set(REDBOLT_IMGUI_OWNED_HEADERS "${REDBOLT_IMGUI_INCLUDE_DIR}" "${REDBOLT_IMGUI_SOURCE_ROOT}/backends")

redbolt_add_native_binding_generation(
    TARGET redbolt-generate-sdl-bindings
    SURFACE sdl
    HEADER "${REDBOLT_SDL_PARSE_SOURCE}"
    HEADER_INCLUDE_PATH "${REDBOLT_SDL_PARSE_SOURCE}"
    FILE_STEM "${REDBOLT_SDL_BINDINGS_STEM}"
    OUTPUT_DIR "${REDBOLT_SDL_GENERATED_DIR}"
    OUTPUT_METADATA "${REDBOLT_SDL_BINDINGS_JSON}"
    OUTPUT_CS "${REDBOLT_SDL_BINDINGS_CS}"
    OUTPUT_CPP "${REDBOLT_SDL_BINDINGS_CPP}"
    INCLUDE_DIRS "${REDBOLT_SDL_CONFIG_INCLUDE_DIR}" "${REDBOLT_SDL_BUILD_INCLUDE_DIR}" ${REDBOLT_SDL_IMAGE_INCLUDE_DIRS}
    DEFINES "USING_GENERATED_CONFIG_H=1"
    OWNED_HEADERS ${REDBOLT_SDL_OWNED_HEADERS})

add_dependencies(redbolt-generate-sdl-bindings SDL2)

redbolt_add_native_binding_generation(
    TARGET redbolt-generate-diligent-bindings
    SURFACE diligent
    HEADER "${REDBOLT_DILIGENT_PARSE_HEADER}"
    HEADER_INCLUDE_PATH "${REDBOLT_DILIGENT_PARSE_HEADER}"
    FILE_STEM "${REDBOLT_DILIGENT_BINDINGS_STEM}"
    OUTPUT_DIR "${REDBOLT_DILIGENT_GENERATED_DIR}"
    OUTPUT_METADATA "${REDBOLT_DILIGENT_BINDINGS_JSON}"
    OUTPUT_CS "${REDBOLT_DILIGENT_BINDINGS_CS}"
    OUTPUT_CPP "${REDBOLT_DILIGENT_BINDINGS_CPP}"
    INCLUDE_DIRS ${REDBOLT_DILIGENT_INCLUDE_DIRS} "${REDBOLT_DILIGENT_SUPPORT_INCLUDE_DIR}"
    OWNED_HEADERS ${REDBOLT_DILIGENT_OWNED_HEADERS})

add_dependencies(redbolt-generate-diligent-bindings redbolt-generate-sdl-bindings)
add_dependencies(redbolt-generate-diligent-bindings redbolt-diligent-build)

redbolt_add_native_binding_generation(
    TARGET redbolt-generate-dearimgui-bindings
    SURFACE dearimgui
    HEADER "${REDBOLT_IMGUI_PARSE_SOURCE}"
    HEADER_INCLUDE_PATH "${REDBOLT_IMGUI_PARSE_SOURCE}"
    FILE_STEM "${REDBOLT_IMGUI_BINDINGS_STEM}"
    OUTPUT_DIR "${REDBOLT_IMGUI_GENERATED_DIR}"
    OUTPUT_METADATA "${REDBOLT_IMGUI_BINDINGS_JSON}"
    OUTPUT_CS "${REDBOLT_IMGUI_BINDINGS_CS}"
    OUTPUT_CPP "${REDBOLT_IMGUI_BINDINGS_CPP}"
    INCLUDE_DIRS "${REDBOLT_IMGUI_INCLUDE_DIR}" "${REDBOLT_IMGUI_SOURCE_ROOT}/backends" "${CMAKE_CURRENT_SOURCE_DIR}/native/RedBolt.Native/include" "${REDBOLT_SDL_CONFIG_INCLUDE_DIR}" "${REDBOLT_SDL_BUILD_INCLUDE_DIR}" ${REDBOLT_SDL_IMAGE_INCLUDE_DIRS}
    DEFINES "IMGUI_DEFINE_MATH_OPERATORS=1" "IMGUI_DISABLE_SSE=1"
    OWNED_HEADERS
        ${REDBOLT_IMGUI_OWNED_HEADERS}
        "${REDBOLT_IMGUI_BACKEND_HEADER}")

add_dependencies(redbolt-generate-dearimgui-bindings ImGui ImGuiSdl2 redbolt-generate-sdl-bindings)

redbolt_add_native_binding_generation(
    TARGET redbolt-generate-stb-bindings
    SURFACE stb
    HEADER "${REDBOLT_STB_PARSE_HEADER}"
    HEADER_INCLUDE_PATH "StbVorbisAll.hpp"
    FILE_STEM "${REDBOLT_STB_BINDINGS_STEM}"
    OUTPUT_DIR "${REDBOLT_STB_GENERATED_DIR}"
    OUTPUT_METADATA "${REDBOLT_STB_BINDINGS_JSON}"
    OUTPUT_CS "${REDBOLT_STB_BINDINGS_CS}"
    OUTPUT_CPP "${REDBOLT_STB_BINDINGS_CPP}"
    INCLUDE_DIRS "${REDBOLT_STB_INCLUDE_DIR}"
    OWNED_HEADERS "${REDBOLT_STB_PARSE_HEADER}" "${REDBOLT_STB_VORBIS_HEADER}" "${REDBOLT_STB_IMAGE_WRITE_HEADER}")

redbolt_add_native_binding_generation(
    TARGET redbolt-generate-lz4-bindings
    SURFACE lz4
    HEADER "${REDBOLT_LZ4_PARSE_HEADER}"
    HEADER_INCLUDE_PATH "Lz4All.hpp"
    FILE_STEM "${REDBOLT_LZ4_BINDINGS_STEM}"
    OUTPUT_DIR "${REDBOLT_LZ4_GENERATED_DIR}"
    OUTPUT_METADATA "${REDBOLT_LZ4_BINDINGS_JSON}"
    OUTPUT_CS "${REDBOLT_LZ4_BINDINGS_CS}"
    OUTPUT_CPP "${REDBOLT_LZ4_BINDINGS_CPP}"
    INCLUDE_DIRS "${REDBOLT_LZ4_INCLUDE_DIR}"
    OWNED_HEADERS "${REDBOLT_LZ4_PARSE_HEADER}" "${REDBOLT_LZ4_INCLUDE_DIR}/lz4.h" "${REDBOLT_LZ4_INCLUDE_DIR}/lz4hc.h")

redbolt_add_native_binding_generation(
    TARGET redbolt-generate-webp-bindings
    SURFACE webp
    HEADER "${REDBOLT_WEBP_PARSE_HEADER}"
    HEADER_INCLUDE_PATH "WebPAll.hpp"
    FILE_STEM "${REDBOLT_WEBP_BINDINGS_STEM}"
    OUTPUT_DIR "${REDBOLT_WEBP_GENERATED_DIR}"
    OUTPUT_METADATA "${REDBOLT_WEBP_BINDINGS_JSON}"
    OUTPUT_CS "${REDBOLT_WEBP_BINDINGS_CS}"
    OUTPUT_CPP "${REDBOLT_WEBP_BINDINGS_CPP}"
    INCLUDE_DIRS "${REDBOLT_WEBP_INCLUDE_DIR}"
        OWNED_HEADERS "${REDBOLT_WEBP_PARSE_HEADER}" "${REDBOLT_WEBP_INCLUDE_DIR}/webp/encode.h" "${REDBOLT_WEBP_INCLUDE_DIR}/webp/types.h")

redbolt_add_native_binding_generation(
    TARGET redbolt-generate-glslang-bindings
    SURFACE glslang
    HEADER "${REDBOLT_GLSLANG_C_HEADER}"
    HEADER_INCLUDE_PATH "glslang/Public/resource_limits_c.h"
    FILE_STEM "${REDBOLT_GLSLANG_BINDINGS_STEM}"
    OUTPUT_DIR "${REDBOLT_GLSLANG_GENERATED_DIR}"
    OUTPUT_METADATA "${REDBOLT_GLSLANG_BINDINGS_JSON}"
    OUTPUT_CS "${REDBOLT_GLSLANG_BINDINGS_CS}"
    OUTPUT_CPP "${REDBOLT_GLSLANG_BINDINGS_CPP}"
    INCLUDE_DIRS "${REDBOLT_GLSLANG_SOURCE_ROOT}"
    OWNED_HEADERS "${REDBOLT_GLSLANG_SOURCE_ROOT}/glslang/Include" "${REDBOLT_GLSLANG_SOURCE_ROOT}/glslang/Public")

redbolt_add_native_binding_generation(
    TARGET redbolt-generate-spirv-cross-bindings
    SURFACE spirvcross
    HEADER "${REDBOLT_SPIRV_CROSS_C_HEADER}"
    HEADER_INCLUDE_PATH "spirv_cross_c.h"
    FILE_STEM "${REDBOLT_SPIRV_CROSS_BINDINGS_STEM}"
    OUTPUT_DIR "${REDBOLT_SPIRV_CROSS_GENERATED_DIR}"
    OUTPUT_METADATA "${REDBOLT_SPIRV_CROSS_BINDINGS_JSON}"
    OUTPUT_CS "${REDBOLT_SPIRV_CROSS_BINDINGS_CS}"
    OUTPUT_CPP "${REDBOLT_SPIRV_CROSS_BINDINGS_CPP}"
    INCLUDE_DIRS "${REDBOLT_SPIRV_CROSS_SOURCE_ROOT}"
    OWNED_HEADERS "${REDBOLT_SPIRV_CROSS_SOURCE_ROOT}")

redbolt_add_native_binding_generation(
    TARGET redbolt-generate-spirv-tools-bindings
    SURFACE spirvtools
    HEADER "${REDBOLT_SPIRV_TOOLS_C_HEADER}"
    HEADER_INCLUDE_PATH "spirv-tools/libspirv.h"
    FILE_STEM "${REDBOLT_SPIRV_TOOLS_BINDINGS_STEM}"
    OUTPUT_DIR "${REDBOLT_SPIRV_TOOLS_GENERATED_DIR}"
    OUTPUT_METADATA "${REDBOLT_SPIRV_TOOLS_BINDINGS_JSON}"
    OUTPUT_CS "${REDBOLT_SPIRV_TOOLS_BINDINGS_CS}"
    OUTPUT_CPP "${REDBOLT_SPIRV_TOOLS_BINDINGS_CPP}"
    INCLUDE_DIRS "${REDBOLT_SPIRV_TOOLS_SOURCE_ROOT}/include"
    OWNED_HEADERS "${REDBOLT_SPIRV_TOOLS_SOURCE_ROOT}/include")

redbolt_add_native_binding_generation(
    TARGET redbolt-generate-spirv-optimizer-bindings
    SURFACE spirvoptimizer
    HEADER "${REDBOLT_SPIRV_OPTIMIZER_PARSE_HEADER}"
    HEADER_INCLUDE_PATH "SpirVOptimizerAll.hpp"
    FILE_STEM "${REDBOLT_SPIRV_OPTIMIZER_BINDINGS_STEM}"
    OUTPUT_DIR "${REDBOLT_SPIRV_OPTIMIZER_GENERATED_DIR}"
    OUTPUT_METADATA "${REDBOLT_SPIRV_OPTIMIZER_BINDINGS_JSON}"
    OUTPUT_CS "${REDBOLT_SPIRV_OPTIMIZER_BINDINGS_CS}"
    OUTPUT_CPP "${REDBOLT_SPIRV_OPTIMIZER_BINDINGS_CPP}"
    INCLUDE_DIRS "${REDBOLT_SPIRV_TOOLS_SOURCE_ROOT}/include" "${REDBOLT_DILIGENT_SUPPORT_INCLUDE_DIR}"
    OWNED_HEADERS "${REDBOLT_SPIRV_OPTIMIZER_PARSE_HEADER}"
        "${REDBOLT_DILIGENT_SUPPORT_INCLUDE_DIR}/RedBolt/SpirVOptimizerSupport.hpp")

redbolt_add_native_binding_generation(
    TARGET redbolt-generate-shader-compiler-bindings
    SURFACE shadercompiler
    HEADER "${REDBOLT_SHADER_COMPILER_PARSE_HEADER}"
    HEADER_INCLUDE_PATH "ShaderCompilerAll.hpp"
    FILE_STEM "${REDBOLT_SHADER_COMPILER_BINDINGS_STEM}"
    OUTPUT_DIR "${REDBOLT_SHADER_COMPILER_GENERATED_DIR}"
    OUTPUT_METADATA "${REDBOLT_SHADER_COMPILER_BINDINGS_JSON}"
    OUTPUT_CS "${REDBOLT_SHADER_COMPILER_BINDINGS_CS}"
    OUTPUT_CPP "${REDBOLT_SHADER_COMPILER_BINDINGS_CPP}"
    INCLUDE_DIRS "${REDBOLT_DILIGENT_SUPPORT_INCLUDE_DIR}"
    OWNED_HEADERS "${REDBOLT_SHADER_COMPILER_PARSE_HEADER}"
        "${REDBOLT_DILIGENT_SUPPORT_INCLUDE_DIR}/RedBolt/ShaderCompilerSupport.hpp")

add_custom_target(redbolt-generate-native-bindings
    DEPENDS
        redbolt-generate-sdl-bindings
        redbolt-generate-diligent-bindings
        redbolt-generate-dearimgui-bindings
        redbolt-generate-stb-bindings
        redbolt-generate-lz4-bindings
        redbolt-generate-webp-bindings
        redbolt-generate-glslang-bindings
        redbolt-generate-spirv-cross-bindings
        redbolt-generate-spirv-tools-bindings
        redbolt-generate-spirv-optimizer-bindings
        redbolt-generate-shader-compiler-bindings)

add_subdirectory(native/RedBolt.Native)

if(NOT REDBOLT_WEB_BUILD)
add_custom_target(restore
    COMMAND "${DOTNET_EXECUTABLE}" restore "${REDBOLT_DOTNET_SOLUTION}"
        --nologo
        "-p:RedBoltCMakePropsPath=${REDBOLT_DOTNET_CMAKE_PROPS_FILE}"
        "-p:SharpCppCMakePropsPath=${REDBOLT_DOTNET_CMAKE_PROPS_FILE}"
        "-p:UseWorkloadResolver=false"
    WORKING_DIRECTORY "${CMAKE_CURRENT_SOURCE_DIR}"
    COMMENT "Restoring RedBolt .NET solution"
    VERBATIM)

function(redbolt_add_dotnet_project_target csproj_path)
    set(options)
    set(oneValueArgs OUT_VARIABLE)
    set(multiValueArgs DEPENDS PROPERTIES)
    cmake_parse_arguments(ARG "${options}" "${oneValueArgs}" "${multiValueArgs}" ${ARGN})

    get_filename_component(project_file_name "${csproj_path}" NAME)
    string(REGEX REPLACE "\\.csproj$" "" project_name "${project_file_name}")
    set(project_target_name "${project_name}")

    if(TARGET "${project_target_name}")
        set(project_target_name "redbolt-dotnet-${project_name}")
    endif()
    if(TARGET "${project_target_name}")
        message(FATAL_ERROR "Dotnet target name collision for ${csproj_path} -> ${project_target_name}")
    endif()

    if(NOT "${ARG_OUT_VARIABLE}" STREQUAL "")
        set("${ARG_OUT_VARIABLE}" "${project_target_name}" PARENT_SCOPE)
    endif()

    if(CMAKE_GENERATOR MATCHES "Visual Studio")
        include_external_msproject("${project_target_name}" "${csproj_path}" restore)
        add_dependencies("${project_target_name}" redbolt-generate-native-bindings RedBolt.Native ${ARG_DEPENDS})
    else()
        add_custom_target("${project_target_name}"
            COMMAND "${DOTNET_EXECUTABLE}" build "${csproj_path}"
                --configuration $<CONFIG>
                --no-restore
                --nologo
                -m:1
                "-p:RedBoltCMakePropsPath=${REDBOLT_DOTNET_CMAKE_PROPS_FILE}"
                "-p:SharpCppCMakePropsPath=${REDBOLT_DOTNET_CMAKE_PROPS_FILE}"
                ${ARG_PROPERTIES}
            DEPENDS restore redbolt-generate-native-bindings RedBolt.Native ${ARG_DEPENDS}
            WORKING_DIRECTORY "${CMAKE_CURRENT_SOURCE_DIR}"
            COMMENT "Building ${project_target_name} with generated native bindings"
            VERBATIM)
    endif()

    list(APPEND REDBOLT_MANAGED_DOTNET_TARGETS "${project_target_name}")
    set(REDBOLT_MANAGED_DOTNET_TARGETS "${REDBOLT_MANAGED_DOTNET_TARGETS}" PARENT_SCOPE)
endfunction()

file(READ "${REDBOLT_DOTNET_SOLUTION}" REDBOLT_DOTNET_SOLUTION_CONTENT)
string(REGEX MATCHALL "Path=\"([^\"]+\\.csproj)\"" REDBOLT_DOTNET_PROJECT_ENTRIES "${REDBOLT_DOTNET_SOLUTION_CONTENT}")
set(REDBOLT_DOTNET_PROJECTS)
foreach(project_entry IN LISTS REDBOLT_DOTNET_PROJECT_ENTRIES)
    string(REGEX REPLACE "Path=\"([^\"]+\\.csproj)\".*" "\\1" project_relative_path "${project_entry}")
    list(APPEND REDBOLT_DOTNET_PROJECTS "${CMAKE_CURRENT_SOURCE_DIR}/${project_relative_path}")
endforeach()
list(REMOVE_DUPLICATES REDBOLT_DOTNET_PROJECTS)

file(GLOB_RECURSE REDBOLT_SHARPCPP_E2E_INPUT_HEADERS CONFIGURE_DEPENDS
    "${REDBOLT_SHARPCPP_ROOT}/tests/SharpCpp.Tests.E2E/*/*.hpp")
file(MAKE_DIRECTORY "${REDBOLT_SHARPCPP_E2E_GENERATED_ROOT}")
add_custom_command(
    OUTPUT "${REDBOLT_SHARPCPP_E2E_GENERATED_STAMP}"
    COMMAND "${CMAKE_COMMAND}"
        "-DGENERATOR_DLL=${REDBOLT_BIN_DIR}/SharpCpp.Tool.dll"
        "-DGENERATED_STAMP=${REDBOLT_SHARPCPP_E2E_GENERATED_STAMP}"
        "-DGENERATED_DIR=${REDBOLT_SHARPCPP_E2E_GENERATED_ROOT}"
        -P "${REDBOLT_SHARPCPP_ROOT}/tests/SharpCpp.Tests.E2E/CheckGeneratedStaleness.cmake"
    COMMAND "${CMAKE_COMMAND}" -E make_directory "${REDBOLT_SHARPCPP_E2E_GENERATED_ROOT}"
    COMMAND "${CMAKE_COMMAND}" -E env
        "LD_LIBRARY_PATH=$<SHELL_PATH:${REDBOLT_BIN_DIR}>:$ENV{LD_LIBRARY_PATH}"
        "${DOTNET_EXECUTABLE}" run --project "${REDBOLT_SHARPCPP_E2E_TOOL}"
        --configuration $<IF:$<CONFIG:Debug>,Debug,Release>
        --no-launch-profile
        --no-restore
        -- generate-sample
        --generated-root "${REDBOLT_SHARPCPP_E2E_GENERATED_ROOT}"
        --project-root "${REDBOLT_SHARPCPP_ROOT}"
        --file-stem "${REDBOLT_SHARPCPP_E2E_FILE_STEM}"
        --log-file "${REDBOLT_SHARPCPP_E2E_GENERATOR_LOG_FILE}"
    COMMAND "${CMAKE_COMMAND}" -E touch "${REDBOLT_SHARPCPP_E2E_GENERATED_STAMP}"
    DEPENDS SharpCpp.Tool ${REDBOLT_SHARPCPP_E2E_INPUT_HEADERS} ${REDBOLT_SHARPCPP_GENERATOR_INPUTS}
        "${REDBOLT_DOTNET_CMAKE_PROPS_FILE}"
        "${CMAKE_CURRENT_SOURCE_DIR}/Directory.Build.props"
        "${REDBOLT_SHARPCPP_E2E_TOOL}"
    WORKING_DIRECTORY "${REDBOLT_SHARPCPP_ROOT}"
    VERBATIM
    USES_TERMINAL)

add_custom_target(redbolt-sharpcpp-e2e-generated
    DEPENDS "${REDBOLT_SHARPCPP_E2E_GENERATED_STAMP}")

file(GLOB REDBOLT_SHARPCPP_E2E_GENERATED_CPP_FILES CONFIGURE_DEPENDS "${REDBOLT_SHARPCPP_E2E_GENERATED_ROOT}/*.g.cpp")
if(NOT REDBOLT_SHARPCPP_E2E_GENERATED_CPP_FILES)
    file(WRITE "${REDBOLT_SHARPCPP_E2E_GENERATED_ROOT}/.stub.cpp" "// stub - replaced by generator\n")
    set(REDBOLT_SHARPCPP_E2E_GENERATED_CPP_FILES "${REDBOLT_SHARPCPP_E2E_GENERATED_ROOT}/.stub.cpp")
endif()

add_library(SharpCppTestsE2E_Native SHARED ${REDBOLT_SHARPCPP_E2E_GENERATED_CPP_FILES})
add_dependencies(SharpCppTestsE2E_Native redbolt-sharpcpp-e2e-generated)
target_compile_features(SharpCppTestsE2E_Native PRIVATE cxx_std_20)
set_target_properties(
    SharpCppTestsE2E_Native
    PROPERTIES
        CXX_SCAN_FOR_MODULES OFF
        UNITY_BUILD ON
        UNITY_BUILD_BATCH_SIZE 100
        RUNTIME_OUTPUT_DIRECTORY "${REDBOLT_BIN_DIR}"
        LIBRARY_OUTPUT_DIRECTORY "${REDBOLT_BIN_DIR}"
        ARCHIVE_OUTPUT_DIRECTORY "${REDBOLT_BIN_DIR}")
target_include_directories(
    SharpCppTestsE2E_Native
    PRIVATE
        "${REDBOLT_SHARPCPP_ROOT}/include"
        "${REDBOLT_SHARPCPP_E2E_GENERATED_ROOT}"
        "${REDBOLT_SHARPCPP_ROOT}/tests/SharpCpp.Tests.E2E")
target_compile_options(SharpCppTestsE2E_Native PRIVATE $<$<COMPILE_LANG_AND_ID:CXX,GNU,Clang,AppleClang>:-Wno-attributes>)

set(REDBOLT_MANAGED_DOTNET_TARGETS)
foreach(csproj_file IN LISTS REDBOLT_DOTNET_PROJECTS)
    if(csproj_file STREQUAL "${REDBOLT_SHARPCPP_E2E_CSPROJ}")
        redbolt_add_dotnet_project_target(
            "${csproj_file}"
            OUT_VARIABLE REDBOLT_SHARPCPP_E2E_TARGET
            DEPENDS redbolt-sharpcpp-e2e-generated redbolt-sharpcpp-e2e-native
            PROPERTIES
                "-p:SharpCppGeneratedDir=${REDBOLT_SHARPCPP_E2E_GENERATED_ROOT}")
    else()
        redbolt_add_dotnet_project_target("${csproj_file}")
    endif()
endforeach()

add_custom_target(redbolt-sharpcpp-e2e-native DEPENDS SharpCppTestsE2E_Native)

add_custom_target(redbolt-sharpcpp-e2e-tests
    COMMAND "${CMAKE_COMMAND}" -E env
        "SharpCppGeneratedDir=${REDBOLT_SHARPCPP_E2E_GENERATED_ROOT}"
        "LD_LIBRARY_PATH=$<SHELL_PATH:${REDBOLT_BIN_DIR}>:$ENV{LD_LIBRARY_PATH}"
        "${DOTNET_EXECUTABLE}" test "${REDBOLT_SHARPCPP_E2E_CSPROJ}"
            --configuration $<CONFIG>
            --no-build
            --no-restore
            --logger "console;verbosity=minimal"
    DEPENDS "${REDBOLT_SHARPCPP_E2E_TARGET}" redbolt-sharpcpp-e2e-native
    WORKING_DIRECTORY "${CMAKE_CURRENT_SOURCE_DIR}"
    COMMENT "Running SharpCpp generic E2E tests"
    VERBATIM)

add_custom_target(redbolt-managed ALL
    DEPENDS ${REDBOLT_MANAGED_DOTNET_TARGETS})

add_custom_target(redbolt-first-cube
    COMMAND "${CMAKE_COMMAND}" -E env
        SDL_VIDEODRIVER=x11
        "${DOTNET_EXECUTABLE}" "${REDBOLT_BIN_DIR}/RedBolt.Sample.FirstCube.dll"
    DEPENDS RedBolt.Sample.FirstCube RedBolt.Native
    WORKING_DIRECTORY "${REDBOLT_BIN_DIR}"
    COMMENT "Running visible first cube through generated SDL/Diligent bindings"
    VERBATIM)
else()
    function(redbolt_collect_static_targets root_target output_variable)
        set(queue "${root_target}")
        set(visited)
        set(static_targets)
        while(queue)
            list(POP_FRONT queue target)
            if(target IN_LIST visited OR NOT TARGET "${target}")
                continue()
            endif()
            list(APPEND visited "${target}")
            get_target_property(aliased_target "${target}" ALIASED_TARGET)
            if(aliased_target)
                list(APPEND queue "${aliased_target}")
                continue()
            endif()
            get_target_property(target_type "${target}" TYPE)
            if(target_type STREQUAL "STATIC_LIBRARY")
                list(APPEND static_targets "${target}")
            endif()
            get_target_property(link_libraries "${target}" LINK_LIBRARIES)
            get_target_property(interface_link_libraries "${target}" INTERFACE_LINK_LIBRARIES)
            foreach(link_item IN LISTS link_libraries interface_link_libraries)
                if(TARGET "${link_item}")
                    list(APPEND queue "${link_item}")
                endif()
            endforeach()
        endwhile()
        list(REMOVE_DUPLICATES static_targets)
        set("${output_variable}" "${static_targets}" PARENT_SCOPE)
    endfunction()

    redbolt_collect_static_targets(RedBolt.Native redbolt_wasm_static_targets)
    set(REDBOLT_WASM_ENGINE_ARCHIVE "${CMAKE_BINARY_DIR}/lib/Release/wasm/RedBolt.Native.a")
    set(REDBOLT_WASM_MRI_FILE "${CMAKE_BINARY_DIR}/redbolt-wasm-archives.mri")
    set(redbolt_wasm_mri "create ${REDBOLT_WASM_ENGINE_ARCHIVE}\n")
    foreach(target IN LISTS redbolt_wasm_static_targets)
        string(APPEND redbolt_wasm_mri "addlib $<TARGET_FILE:${target}>\n")
    endforeach()
    string(APPEND redbolt_wasm_mri "save\nend\n")
    file(GENERATE OUTPUT "${REDBOLT_WASM_MRI_FILE}" CONTENT "${redbolt_wasm_mri}")

    file(GLOB_RECURSE redbolt_wasm_resource_files CONFIGURE_DEPENDS
        RELATIVE "${CMAKE_CURRENT_SOURCE_DIR}/bin"
        "${CMAKE_CURRENT_SOURCE_DIR}/bin/CoreData/*"
        "${CMAKE_CURRENT_SOURCE_DIR}/bin/Data/*")
    set(REDBOLT_WASM_RESOURCE_MANIFEST "${CMAKE_BINARY_DIR}/generated/web/resources.manifest.json")
    set(redbolt_wasm_resource_manifest "[\n")
    set(redbolt_wasm_resource_separator "")
    foreach(resource_file IN LISTS redbolt_wasm_resource_files)
        string(APPEND redbolt_wasm_resource_manifest
            "${redbolt_wasm_resource_separator}  {\"name\":\"../resources/${resource_file}\",\"virtualPath\":\"${resource_file}\"}")
        set(redbolt_wasm_resource_separator ",\n")
    endforeach()
    string(APPEND redbolt_wasm_resource_manifest "\n]\n")
    file(MAKE_DIRECTORY "${CMAKE_BINARY_DIR}/generated/web")
    file(WRITE "${REDBOLT_WASM_RESOURCE_MANIFEST}" "${redbolt_wasm_resource_manifest}")

    add_custom_command(
        OUTPUT "${REDBOLT_WASM_ENGINE_ARCHIVE}"
        COMMAND "${CMAKE_COMMAND}"
            "-DREDBOLT_EMSCRIPTEN_ENV=${_redbolt_emscripten_environment}"
            "-DREDBOLT_ARCHIVER=${CMAKE_AR}"
            "-DREDBOLT_MRI_FILE=${REDBOLT_WASM_MRI_FILE}"
            "-DREDBOLT_OUTPUT_ARCHIVE=${REDBOLT_WASM_ENGINE_ARCHIVE}"
            -P "${CMAKE_CURRENT_SOURCE_DIR}/cmake/MergeStaticArchives.cmake"
        DEPENDS ${redbolt_wasm_static_targets}
        COMMENT "Merging RedBolt WebAssembly engine archive"
        VERBATIM)
    add_custom_target(redbolt-wasm-native DEPENDS "${REDBOLT_WASM_ENGINE_ARCHIVE}")
    add_custom_target(redbolt-wasm
        COMMAND "${DOTNET_EXECUTABLE}" publish
            "${CMAKE_CURRENT_SOURCE_DIR}/samples/RedBolt.Sample.Web/RedBolt.Sample.Web.csproj"
            --configuration Release
            "-p:RedBoltCMakePropsPath=${REDBOLT_DOTNET_CMAKE_PROPS_FILE}"
            "-p:RedBoltWebEngineArchive=${REDBOLT_WASM_ENGINE_ARCHIVE}"
            "-p:RedBoltWebResourcesManifest=${REDBOLT_WASM_RESOURCE_MANIFEST}"
        DEPENDS redbolt-wasm-native
        WORKING_DIRECTORY "${CMAKE_CURRENT_SOURCE_DIR}"
        COMMENT "Publishing framework-free RedBolt WebAssembly host"
        VERBATIM)
    find_package(Python3 COMPONENTS Interpreter REQUIRED)
    add_custom_target(redbolt-wasm-serve
        COMMAND "${Python3_EXECUTABLE}"
            "${CMAKE_CURRENT_SOURCE_DIR}/cmake/ServeWebAssembly.py"
            --directory "${CMAKE_BINARY_DIR}/bin/Release/publish/wwwroot"
            --port 8000
        DEPENDS redbolt-wasm
        USES_TERMINAL
        VERBATIM)
endif()

add_custom_target(redbolt-verify-config
    COMMAND "${CMAKE_COMMAND}"
        "-DREDBOLT_THIRDPARTY_ROOT=${REDBOLT_THIRDPARTY_ROOT}"
        "-DREDBOLT_SHARPCPP_ROOT=${REDBOLT_SHARPCPP_ROOT}"
        -P "${CMAKE_CURRENT_SOURCE_DIR}/cmake/VerifyRedBoltConfig.cmake"
    COMMENT "Verifying RedBolt dependency configuration"
    VERBATIM)

add_custom_target(redbolt-stage0
    DEPENDS
        redbolt-verify-config
        redbolt-native-dependency-batch
        redbolt-generate-native-bindings)
