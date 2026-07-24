cmake_minimum_required(VERSION 3.28)

project(RedBoltFramework VERSION 0.1.0 LANGUAGES C CXX)

add_custom_target(redbolt-msbuild-build-guard ALL
    COMMAND "${CMAKE_COMMAND}" -P "${CMAKE_CURRENT_SOURCE_DIR}/cmake/CheckMsBuildBuild.cmake"
    VERBATIM)

option(REDBOLT_WEB_BUILD "Build the framework-free WebAssembly target only" OFF)

if(REDBOLT_WEB_BUILD OR EMSCRIPTEN)
    set(BUILD_SHARED_LIBS OFF CACHE BOOL "Build shared libraries" FORCE)
    set(REDBOLT_EXTERNAL_LIBRARY_TYPE STATIC)
else()
    set(BUILD_SHARED_LIBS ON CACHE BOOL "Build shared libraries" FORCE)
    set(REDBOLT_EXTERNAL_LIBRARY_TYPE SHARED)
endif()

if(MSVC)
    add_compile_options("$<$<COMPILE_LANGUAGE:CXX>:/std:c++20>")
else()
    add_compile_options("$<$<COMPILE_LANGUAGE:CXX>:-std=c++20>")
endif()
set(CMAKE_CXX_EXTENSIONS OFF)
set(CMAKE_POSITION_INDEPENDENT_CODE ON)
set(CMAKE_CXX_SCAN_FOR_MODULES OFF)

if(EMSCRIPTEN)
    add_compile_options(-pthread -mbulk-memory)
    set_property(GLOBAL PROPERTY RULE_LAUNCH_LINK "${_redbolt_emscripten_environment}")
endif()

set(REDBOLT_EXTERN_ROOT "${CMAKE_CURRENT_SOURCE_DIR}/extern" CACHE PATH "Path to RedBolt native dependency sources" FORCE)
set(REDBOLT_SHARPCPP_ROOT "${REDBOLT_EXTERN_ROOT}/SharpCpp" CACHE PATH "Path to the SharpCpp source tree" FORCE)
set(REDBOLT_PLATFORM "linux-gcc-x64-dll" CACHE STRING "RedBolt target platform")
set(REDBOLT_RUNTIME_OUTPUT_DIR "${CMAKE_CURRENT_SOURCE_DIR}/build/bin/${REDBOLT_PLATFORM}" CACHE PATH "RedBolt runtime output root")
set(REDBOLT_GENERATED_ROOT "${CMAKE_BINARY_DIR}/generated" CACHE INTERNAL "RedBolt generated file root")
set(REDBOLT_NATIVE_GENERATED_DIR "${REDBOLT_GENERATED_ROOT}/redbolt-native" CACHE INTERNAL "RedBolt generated native binding root")

if(EMSCRIPTEN)
    set(REDBOLT_NATIVE_LIBRARY_NAME "RedBolt.Native" CACHE INTERNAL "RedBolt generated binding native library name")
else()
    set(CMAKE_SHARED_LIBRARY_PREFIX "lib${REDBOLT_PLATFORM}-")
    set(REDBOLT_NATIVE_LIBRARY_NAME "${REDBOLT_PLATFORM}-RedBolt.Native" CACHE INTERNAL "RedBolt generated binding native library name")
endif()

set(CMAKE_RUNTIME_OUTPUT_DIRECTORY "${REDBOLT_RUNTIME_OUTPUT_DIR}")
set(CMAKE_LIBRARY_OUTPUT_DIRECTORY "${REDBOLT_RUNTIME_OUTPUT_DIR}")
set(CMAKE_ARCHIVE_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/lib/$<CONFIG>")
set(CMAKE_BUILD_RPATH "$ORIGIN")
set(CMAKE_INSTALL_RPATH "$ORIGIN")
foreach(_redbolt_configuration Debug Release RelWithDebInfo MinSizeRel)
    string(TOUPPER "${_redbolt_configuration}" _redbolt_configuration_upper)
    set(CMAKE_RUNTIME_OUTPUT_DIRECTORY_${_redbolt_configuration_upper} "${REDBOLT_RUNTIME_OUTPUT_DIR}/${_redbolt_configuration}")
    set(CMAKE_LIBRARY_OUTPUT_DIRECTORY_${_redbolt_configuration_upper} "${REDBOLT_RUNTIME_OUTPUT_DIR}/${_redbolt_configuration}")
endforeach()

add_subdirectory(cmake)
add_subdirectory(extern)
add_subdirectory(generators)
add_subdirectory(src)
add_subdirectory(native)

if(EMSCRIPTEN)
    add_subdirectory(samples)
endif()
