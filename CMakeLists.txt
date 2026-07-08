cmake_minimum_required(VERSION 3.28)

project(RedBoltFramework VERSION 0.1.0 LANGUAGES C CXX)

set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)
set(CMAKE_POSITION_INDEPENDENT_CODE ON)

set(REDBOLT_BIN_DIR "${CMAKE_BINARY_DIR}/bin/$<CONFIG>")
set(REDBOLT_GENERATED_ROOT "${CMAKE_BINARY_DIR}/generated")
set(REDBOLT_NATIVE_GENERATED_DIR "${REDBOLT_GENERATED_ROOT}/redbolt-native")
set(REDBOLT_SDL_GENERATED_DIR "${REDBOLT_NATIVE_GENERATED_DIR}/sdl")
set(REDBOLT_DILIGENT_GENERATED_DIR "${REDBOLT_NATIVE_GENERATED_DIR}/diligent")
set(REDBOLT_IMGUI_GENERATED_DIR "${REDBOLT_NATIVE_GENERATED_DIR}/dearimgui")
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
set(REDBOLT_IMGUI_PARSE_SOURCE "${REDBOLT_IMGUI_GENERATED_DIR}/ImGuiAll.cpp")
set(REDBOLT_DILIGENT_PARSE_HEADER "${REDBOLT_DILIGENT_GENERATED_DIR}/DiligentAll.hpp")
set(REDBOLT_DILIGENT_SUPPORT_INCLUDE_DIR "${CMAKE_CURRENT_SOURCE_DIR}/native/RedBolt.Native/include")
set(REDBOLT_DILIGENT_SUPPORT_HEADER "${REDBOLT_DILIGENT_SUPPORT_INCLUDE_DIR}/RedBolt/DiligentSupport.hpp")
set(REDBOLT_DOTNET_SOLUTION "${CMAKE_CURRENT_SOURCE_DIR}/RedBolt.slnx")

set(REDBOLT_THIRDPARTY_ROOT "${CMAKE_CURRENT_SOURCE_DIR}/extern/thirdparty" CACHE PATH "Path to RedBolt's copied native dependency sources" FORCE)
set(REDBOLT_SHARPCPP_ROOT "${CMAKE_CURRENT_SOURCE_DIR}/SharpCpp" CACHE PATH "Path to the SharpCpp source tree used by RedBolt generators")

set(CMAKE_RUNTIME_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/bin")
set(CMAKE_LIBRARY_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/bin")
set(CMAKE_ARCHIVE_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/lib")

include(cmake/RedBoltDependencies.cmake)
include(cmake/RedBoltDotNet.cmake)
include(cmake/RedBoltSharpCpp.cmake)

find_program(DOTNET_EXECUTABLE dotnet REQUIRED)

redbolt_initialize_dotnet_consumer(
    CONSUMER_ROOT "${CMAKE_CURRENT_SOURCE_DIR}"
    OUTPUT_ROOT "${CMAKE_BINARY_DIR}/bin"
    INTERMEDIATE_ROOT "${CMAKE_BINARY_DIR}/obj/managed"
    DOTNET_STATE_DIR "${CMAKE_BINARY_DIR}/cmake/dotnet"
    PROPS_FILE "${CMAKE_BINARY_DIR}/cmake/dotnet/RedBolt.CMake.props"
    COMPAT_PROPS_FILE "${CMAKE_CURRENT_SOURCE_DIR}/.CMakeLast.props"
    GENERATED_BINDINGS_DIR "${REDBOLT_NATIVE_GENERATED_DIR}"
    SHARPCPP_ROOT "${REDBOLT_SHARPCPP_ROOT}")

file(MAKE_DIRECTORY "${REDBOLT_SDL_GENERATED_DIR}" "${REDBOLT_DILIGENT_GENERATED_DIR}" "${REDBOLT_IMGUI_GENERATED_DIR}")
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
#include <RedBolt/DiligentSupport.hpp>
]=])

file(WRITE "${REDBOLT_IMGUI_PARSE_SOURCE}" [=[
#include <SDL.h>
#include <imgui.h>
#define IMGUI_IMPL_API IMGUI_API
#include <imgui_impl_sdl2.h>
#include <RedBolt/ImGuiBackend.hpp>
]=])

set(REDBOLT_SDL_OWNED_HEADERS
    "${REDBOLT_SDL_BUILD_INCLUDE_DIR}"
    "${REDBOLT_SDL_IMAGE_HEADER}")

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

add_custom_target(redbolt-generate-native-bindings
    DEPENDS
        redbolt-generate-sdl-bindings
        redbolt-generate-diligent-bindings
        redbolt-generate-dearimgui-bindings)

add_subdirectory(native/RedBolt.Native)

add_custom_target(restore
    COMMAND "${DOTNET_EXECUTABLE}" restore "${REDBOLT_DOTNET_SOLUTION}"
        --nologo
        "-p:RedBoltCMakePropsPath=${REDBOLT_DOTNET_CMAKE_PROPS_FILE}"
        "-p:SharpCppCMakePropsPath=${REDBOLT_DOTNET_CMAKE_PROPS_FILE}"
    WORKING_DIRECTORY "${CMAKE_CURRENT_SOURCE_DIR}"
    COMMENT "Restoring RedBolt .NET solution"
    VERBATIM)

function(redbolt_add_dotnet_project_target csproj_path)
    get_filename_component(project_file_name "${csproj_path}" NAME)
    string(REGEX REPLACE "\\.csproj$" "" project_name "${project_file_name}")
    set(project_target_name "${project_name}")

    if(TARGET "${project_target_name}")
        set(project_target_name "redbolt-dotnet-${project_name}")
    endif()
    if(TARGET "${project_target_name}")
        message(FATAL_ERROR "Dotnet target name collision for ${csproj_path} -> ${project_target_name}")
    endif()

    if(CMAKE_GENERATOR MATCHES "Visual Studio")
        include_external_msproject("${project_target_name}" "${csproj_path}" restore)
        add_dependencies("${project_target_name}" redbolt-generate-native-bindings RedBolt.Native)
    else()
        add_custom_target("${project_target_name}"
            COMMAND "${DOTNET_EXECUTABLE}" build "${csproj_path}"
                --configuration $<CONFIG>
                --no-restore
                --nologo
                -m:1
                "-p:RedBoltCMakePropsPath=${REDBOLT_DOTNET_CMAKE_PROPS_FILE}"
                "-p:SharpCppCMakePropsPath=${REDBOLT_DOTNET_CMAKE_PROPS_FILE}"
            DEPENDS restore redbolt-generate-native-bindings RedBolt.Native
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
set(REDBOLT_MANAGED_DOTNET_TARGETS)
foreach(csproj_file IN LISTS REDBOLT_DOTNET_PROJECTS)
    redbolt_add_dotnet_project_target("${csproj_file}")
endforeach()

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
