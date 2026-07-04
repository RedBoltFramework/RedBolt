cmake_minimum_required(VERSION 3.28)

project(RedBolt VERSION 0.1.0 LANGUAGES C CXX)

set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)
set(CMAKE_POSITION_INDEPENDENT_CODE ON)

set(REDBOLT_BIN_DIR "${CMAKE_BINARY_DIR}/bin/$<CONFIG>")
set(REDBOLT_GENERATED_ROOT "${CMAKE_BINARY_DIR}/generated")
set(REDBOLT_NATIVE_GENERATED_DIR "${REDBOLT_GENERATED_ROOT}/redbolt-native")
set(REDBOLT_SDL_GENERATED_DIR "${REDBOLT_NATIVE_GENERATED_DIR}/sdl")
set(REDBOLT_DILIGENT_GENERATED_DIR "${REDBOLT_NATIVE_GENERATED_DIR}/diligent")
set(REDBOLT_SDL_BINDINGS_STEM "RedBolt.Bindings.Sdl")
set(REDBOLT_DILIGENT_BINDINGS_STEM "RedBolt.Bindings.Diligent")
set(REDBOLT_SDL_BINDINGS_CS "${REDBOLT_SDL_GENERATED_DIR}/${REDBOLT_SDL_BINDINGS_STEM}.Bindings.g.cs")
set(REDBOLT_SDL_BINDINGS_CPP "${REDBOLT_SDL_GENERATED_DIR}/${REDBOLT_SDL_BINDINGS_STEM}.Bindings.g.cpp")
set(REDBOLT_SDL_BINDINGS_JSON "${REDBOLT_SDL_GENERATED_DIR}/RedBolt.Bindings.Sdl.json")
set(REDBOLT_SDL_PARSE_SOURCE "${REDBOLT_SDL_GENERATED_DIR}/SDLAll.cpp")
set(REDBOLT_SDL_BUILD_INCLUDE_DIR "${CMAKE_BINARY_DIR}/deps/SDL/include")
set(REDBOLT_SDL_CONFIG_INCLUDE_DIR "${CMAKE_BINARY_DIR}/deps/SDL/include-config-$<LOWER_CASE:$<CONFIG>>")
set(REDBOLT_DILIGENT_BINDINGS_CS "${REDBOLT_DILIGENT_GENERATED_DIR}/${REDBOLT_DILIGENT_BINDINGS_STEM}.Bindings.g.cs")
set(REDBOLT_DILIGENT_BINDINGS_CPP "${REDBOLT_DILIGENT_GENERATED_DIR}/${REDBOLT_DILIGENT_BINDINGS_STEM}.Bindings.g.cpp")
set(REDBOLT_DILIGENT_BINDINGS_JSON "${REDBOLT_DILIGENT_GENERATED_DIR}/RedBolt.Bindings.Diligent.json")
set(REDBOLT_DILIGENT_PARSE_HEADER "${REDBOLT_DILIGENT_GENERATED_DIR}/DiligentAll.hpp")
set(REDBOLT_DOTNET_SOLUTION "${CMAKE_CURRENT_SOURCE_DIR}/RedBolt.slnx")

set(REDBOLT_THIRDPARTY_ROOT "${CMAKE_CURRENT_SOURCE_DIR}/extern/thirdparty" CACHE PATH "Path to RedBolt's copied native dependency sources" FORCE)
set(REDBOLT_SHARPCPP_ROOT "${CMAKE_CURRENT_SOURCE_DIR}/../SharpCpp-v3" CACHE PATH "Path to the SharpCpp source tree used by RedBolt generators")

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

file(MAKE_DIRECTORY "${REDBOLT_SDL_GENERATED_DIR}" "${REDBOLT_DILIGENT_GENERATED_DIR}")
file(WRITE "${REDBOLT_SDL_PARSE_SOURCE}" [=[
#include <SDL_config.h>
#include <SDL.h>

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
]=])

set(REDBOLT_SDL_OWNED_HEADERS
    "${REDBOLT_SDL_BUILD_INCLUDE_DIR}")

set(REDBOLT_DILIGENT_OWNED_HEADERS
    ${REDBOLT_DILIGENT_INTERFACE_DIRS})

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
    INCLUDE_DIRS "${REDBOLT_SDL_CONFIG_INCLUDE_DIR}" "${REDBOLT_SDL_BUILD_INCLUDE_DIR}"
    DEFINES "USING_GENERATED_CONFIG_H=1"
    OWNED_HEADERS ${REDBOLT_SDL_OWNED_HEADERS})

add_dependencies(redbolt-generate-sdl-bindings SDL2-static)

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
    INCLUDE_DIRS ${REDBOLT_DILIGENT_INCLUDE_DIRS}
    OWNED_HEADERS ${REDBOLT_DILIGENT_OWNED_HEADERS})

add_dependencies(redbolt-generate-diligent-bindings redbolt-generate-sdl-bindings)
add_dependencies(redbolt-generate-diligent-bindings redbolt-diligent-build)

add_custom_target(redbolt-generate-native-bindings
    DEPENDS
        redbolt-generate-sdl-bindings
        redbolt-generate-diligent-bindings)

add_subdirectory(native/RedBolt.Native)

add_custom_target(redbolt-csharp
    COMMAND "${DOTNET_EXECUTABLE}" build "${REDBOLT_DOTNET_SOLUTION}"
        --configuration $<CONFIG>
        --nologo
        "-p:RedBoltCMakePropsPath=${REDBOLT_DOTNET_CMAKE_PROPS_FILE}"
        "-p:SharpCppCMakePropsPath=${REDBOLT_DOTNET_CMAKE_PROPS_FILE}"
    DEPENDS redbolt-generate-native-bindings RedBolt.Native
    WORKING_DIRECTORY "${CMAKE_CURRENT_SOURCE_DIR}"
    COMMENT "Building RedBolt C# solution with generated native bindings"
    VERBATIM)

add_custom_target(redbolt-managed
    DEPENDS redbolt-csharp)

add_custom_target(redbolt-build-binding-smoke
    DEPENDS redbolt-csharp)

add_custom_target(redbolt-binding-smoke
    COMMAND "${DOTNET_EXECUTABLE}" "${REDBOLT_BIN_DIR}/RedBolt.BindingSmoke.dll"
    DEPENDS redbolt-build-binding-smoke RedBolt.Native
    WORKING_DIRECTORY "${REDBOLT_BIN_DIR}"
    COMMENT "Running managed binding smoke through RedBolt.Native"
    VERBATIM)

add_custom_target(redbolt-diligent-gl-smoke
    COMMAND "${CMAKE_COMMAND}" -E env
        REDBOLT_RUN_DILIGENT_GL_SMOKE=1
        SDL_VIDEODRIVER=x11
        "${DOTNET_EXECUTABLE}" "${REDBOLT_BIN_DIR}/RedBolt.BindingSmoke.dll"
    DEPENDS redbolt-build-binding-smoke RedBolt.Native
    WORKING_DIRECTORY "${REDBOLT_BIN_DIR}"
    COMMENT "Running managed Diligent OpenGL smoke through RedBolt.Native"
    VERBATIM)

add_custom_target(redbolt-first-cube
    COMMAND "${CMAKE_COMMAND}" -E env
        SDL_VIDEODRIVER=x11
        "${DOTNET_EXECUTABLE}" "${REDBOLT_BIN_DIR}/RedBolt.Sample.FirstCube.dll"
    DEPENDS redbolt-csharp RedBolt.Native
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
        redbolt-native-smoke
        redbolt-generate-native-bindings
        redbolt-binding-smoke)
