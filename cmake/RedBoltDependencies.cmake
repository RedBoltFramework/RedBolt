include_guard(GLOBAL)

function(redbolt_require_path path description)
    if(NOT EXISTS "${path}")
        message(FATAL_ERROR "${description} does not exist: ${path}")
    endif()
endfunction()

redbolt_require_path("${REDBOLT_THIRDPARTY_ROOT}" "REDBOLT_THIRDPARTY_ROOT")
redbolt_require_path("${REDBOLT_SHARPCPP_ROOT}" "REDBOLT_SHARPCPP_ROOT")

set(REDBOLT_SDL_SOURCE_ROOT "${REDBOLT_THIRDPARTY_ROOT}/SDL" CACHE INTERNAL "SDL source root")
set(REDBOLT_DILIGENT_SOURCE_ROOT "${REDBOLT_THIRDPARTY_ROOT}/Diligent" CACHE INTERNAL "Diligent source root")

redbolt_require_path("${REDBOLT_SDL_SOURCE_ROOT}/CMakeLists.txt" "SDL source")
redbolt_require_path("${REDBOLT_DILIGENT_SOURCE_ROOT}/Graphics/GraphicsEngine/interface/GraphicsTypes.h" "Diligent headers")
redbolt_require_path("${REDBOLT_SHARPCPP_ROOT}/include/SharpCpp/Export.hpp" "SharpCpp native support headers")

if(NOT COMMAND install_third_party_libs)
    function(install_third_party_libs)
    endfunction()
endif()

if(NOT COMMAND group_sources)
    function(group_sources)
    endfunction()
endif()

set(SDL_SHARED OFF CACHE BOOL "" FORCE)
set(SDL_STATIC ON CACHE BOOL "" FORCE)
set(SDL2_DISABLE_SDL2MAIN ON CACHE BOOL "" FORCE)
set(SDL2_DISABLE_INSTALL ON CACHE BOOL "" FORCE)
set(SDL_TEST OFF CACHE BOOL "" FORCE)
set(SDL_TESTS OFF CACHE BOOL "" FORCE)
set(SDL_AUDIO OFF CACHE BOOL "" FORCE)
set(SDL_RENDER OFF CACHE BOOL "" FORCE)
set(SDL_JOYSTICK OFF CACHE BOOL "" FORCE)
set(SDL_HAPTIC OFF CACHE BOOL "" FORCE)
set(SDL_SENSOR OFF CACHE BOOL "" FORCE)

add_subdirectory("${REDBOLT_SDL_SOURCE_ROOT}" "${CMAKE_BINARY_DIR}/deps/SDL" EXCLUDE_FROM_ALL)

set(DEBUG_CONFIGURATIONS DEBUG CACHE INTERNAL "Debug configurations")
set(RELEASE_CONFIGURATIONS RELEASE RELWITHDEBINFO MINSIZEREL CACHE INTERNAL "Release configurations")
set(DILIGENT_CORE_SOURCE_DIR "${REDBOLT_DILIGENT_SOURCE_ROOT}" CACHE INTERNAL "DiligentCore module source directory")
set(DILIGENT_CORE_DIR "extern/thirdparty/Diligent" CACHE INTERNAL "DiligentCore relative source directory")
set(DILIGENT_INSTALL_CORE OFF CACHE BOOL "" FORCE)
set(DILIGENT_INSTALL_PDB OFF CACHE BOOL "" FORCE)
set(DILIGENT_NO_FORMAT_VALIDATION ON CACHE BOOL "" FORCE)

set(PLATFORM_WIN32 FALSE CACHE INTERNAL "")
set(PLATFORM_UNIVERSAL_WINDOWS FALSE CACHE INTERNAL "")
set(PLATFORM_ANDROID FALSE CACHE INTERNAL "")
set(PLATFORM_LINUX FALSE CACHE INTERNAL "")
set(PLATFORM_APPLE FALSE CACHE INTERNAL "")
set(PLATFORM_MACOS FALSE CACHE INTERNAL "")
set(PLATFORM_IOS FALSE CACHE INTERNAL "")
set(PLATFORM_TVOS FALSE CACHE INTERNAL "")
set(PLATFORM_EMSCRIPTEN FALSE CACHE INTERNAL "")
set(D3D11_SUPPORTED FALSE CACHE INTERNAL "D3D11 is not supported")
set(D3D12_SUPPORTED FALSE CACHE INTERNAL "D3D12 is not supported")
set(GL_SUPPORTED FALSE CACHE INTERNAL "OpenGL is not supported")
set(GLES_SUPPORTED FALSE CACHE INTERNAL "OpenGLES is not supported")
set(VULKAN_SUPPORTED TRUE CACHE INTERNAL "Vulkan API is exposed by Diligent core")
set(METAL_SUPPORTED FALSE CACHE INTERNAL "Metal is not supported")
set(WEBGPU_SUPPORTED FALSE CACHE INTERNAL "WebGPU is not supported")
set(ARCHIVER_SUPPORTED FALSE CACHE INTERNAL "Archiver is not supported")

if(CMAKE_SYSTEM_NAME STREQUAL "Linux")
    set(PLATFORM_LINUX TRUE CACHE INTERNAL "Target platform: Linux")
else()
    message(FATAL_ERROR "RedBolt has not mapped Diligent platform selection for ${CMAKE_SYSTEM_NAME}")
endif()

add_library(Diligent-PublicBuildSettings INTERFACE)
target_compile_definitions(Diligent-PublicBuildSettings INTERFACE
    PLATFORM_LINUX=1
    D3D11_SUPPORTED=0
    D3D12_SUPPORTED=0
    GL_SUPPORTED=0
    GLES_SUPPORTED=0
    VULKAN_SUPPORTED=1
    METAL_SUPPORTED=0
    WEBGPU_SUPPORTED=0)

add_library(Diligent-BuildSettings INTERFACE)
target_link_libraries(Diligent-BuildSettings INTERFACE Diligent-PublicBuildSettings)
target_compile_definitions(Diligent-BuildSettings INTERFACE
    __forceinline=inline
    "$<$<CONFIG:DEBUG>:_DEBUG;DEBUG>"
    "$<$<NOT:$<CONFIG:DEBUG>>:NDEBUG>")

include("${REDBOLT_DILIGENT_SOURCE_ROOT}/BuildTools/CMake/BuildUtils.cmake")
add_subdirectory("${REDBOLT_DILIGENT_SOURCE_ROOT}/Primitives" "${CMAKE_BINARY_DIR}/deps/Diligent/Primitives" EXCLUDE_FROM_ALL)
add_subdirectory("${REDBOLT_DILIGENT_SOURCE_ROOT}/Platforms" "${CMAKE_BINARY_DIR}/deps/Diligent/Platforms" EXCLUDE_FROM_ALL)
add_subdirectory("${REDBOLT_DILIGENT_SOURCE_ROOT}/Common" "${CMAKE_BINARY_DIR}/deps/Diligent/Common" EXCLUDE_FROM_ALL)
add_subdirectory("${REDBOLT_DILIGENT_SOURCE_ROOT}/Graphics/GraphicsAccessories" "${CMAKE_BINARY_DIR}/deps/Diligent/Graphics/GraphicsAccessories" EXCLUDE_FROM_ALL)

add_library(Diligent-GraphicsEngineInterface INTERFACE)
target_include_directories(Diligent-GraphicsEngineInterface INTERFACE
    "${REDBOLT_DILIGENT_SOURCE_ROOT}/Graphics/GraphicsEngine/interface")
target_link_libraries(Diligent-GraphicsEngineInterface INTERFACE
    Diligent-Primitives
    Diligent-PublicBuildSettings)
add_subdirectory("${REDBOLT_DILIGENT_SOURCE_ROOT}/Graphics/GraphicsEngine" "${CMAKE_BINARY_DIR}/deps/Diligent/Graphics/GraphicsEngine" EXCLUDE_FROM_ALL)

set(REDBOLT_DILIGENT_INCLUDE_DIRS
    "${REDBOLT_DILIGENT_SOURCE_ROOT}/Primitives/interface"
    "${REDBOLT_DILIGENT_SOURCE_ROOT}/Platforms/interface"
    "${REDBOLT_DILIGENT_SOURCE_ROOT}/Platforms/Basic/interface"
    "${REDBOLT_DILIGENT_SOURCE_ROOT}/Platforms/Linux/interface"
    "${REDBOLT_DILIGENT_SOURCE_ROOT}/Common/interface"
    "${REDBOLT_DILIGENT_SOURCE_ROOT}/Graphics/GraphicsEngine/interface"
    CACHE INTERNAL "Diligent include directories used by RedBolt")

set(REDBOLT_DILIGENT_BINDING_DEFINES
    "DILIGENT_SHARP_GEN=1"
    "PLATFORM_LINUX=1"
    "D3D11_SUPPORTED=0"
    "D3D12_SUPPORTED=0"
    "GL_SUPPORTED=0"
    "GLES_SUPPORTED=0"
    "VULKAN_SUPPORTED=1"
    "METAL_SUPPORTED=0"
    "WEBGPU_SUPPORTED=0"
    CACHE INTERNAL "Diligent compile definitions used by RedBolt")

add_library(redbolt-diligent-headers INTERFACE)
target_include_directories(redbolt-diligent-headers SYSTEM INTERFACE
    ${REDBOLT_DILIGENT_INCLUDE_DIRS})

if(CMAKE_SYSTEM_NAME STREQUAL "Linux")
    target_compile_definitions(redbolt-diligent-headers INTERFACE ${REDBOLT_DILIGENT_BINDING_DEFINES})
elseif(WIN32)
    target_compile_definitions(redbolt-diligent-headers INTERFACE PLATFORM_WIN32=1)
elseif(APPLE)
    target_compile_definitions(redbolt-diligent-headers INTERFACE PLATFORM_MACOS=1 PLATFORM_APPLE=1)
else()
    message(FATAL_ERROR "RedBolt has not mapped Diligent platform macros for ${CMAKE_SYSTEM_NAME}")
endif()
target_link_libraries(redbolt-diligent-headers INTERFACE
    Diligent-GraphicsEngineInterface
    Diligent-TargetPlatform)

add_library(redbolt-native-support INTERFACE)
target_include_directories(redbolt-native-support INTERFACE
    "${REDBOLT_SHARPCPP_ROOT}/include")
target_link_libraries(redbolt-native-support INTERFACE redbolt-diligent-headers)

set(REDBOLT_BINDING_INCLUDE_DIRS
    "${REDBOLT_SHARPCPP_ROOT}/include"
    ${REDBOLT_DILIGENT_INCLUDE_DIRS}
    CACHE INTERNAL "Include directories used by RedBolt SharpCpp dependency generation")

set(REDBOLT_BINDING_DEFINES
    ${REDBOLT_DILIGENT_BINDING_DEFINES}
    CACHE INTERNAL "Compile definitions used by RedBolt SharpCpp dependency generation")

add_custom_target(redbolt-diligent-build
    DEPENDS
        Diligent-GraphicsEngine
    COMMENT "Building RedBolt Diligent dependency targets")

add_library(redbolt-diligent-header-smoke STATIC
    "${CMAKE_CURRENT_SOURCE_DIR}/native/RedBolt.Native/src/DiligentHeaderSmoke.cpp")
target_link_libraries(redbolt-diligent-header-smoke PRIVATE redbolt-diligent-headers)

add_custom_target(redbolt-native-dependency-batch
    DEPENDS SDL2-static redbolt-diligent-build redbolt-diligent-header-smoke
    COMMENT "Building RedBolt initial native dependency batch")
