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
set(REDBOLT_IMGUI_SOURCE_ROOT "${REDBOLT_THIRDPARTY_ROOT}/imgui" CACHE INTERNAL "ImGui source root")

redbolt_require_path("${REDBOLT_SDL_SOURCE_ROOT}/CMakeLists.txt" "SDL source")
redbolt_require_path("${REDBOLT_DILIGENT_SOURCE_ROOT}/Graphics/GraphicsEngine/interface/GraphicsTypes.h" "Diligent headers")
redbolt_require_path("${REDBOLT_IMGUI_SOURCE_ROOT}/imgui.h" "ImGui headers")
redbolt_require_path("${REDBOLT_SHARPCPP_ROOT}/include/SharpCpp/Export.hpp" "SharpCpp native support headers")

if(NOT COMMAND install_third_party_libs)
    function(install_third_party_libs)
    endfunction()
endif()

if(NOT COMMAND group_sources)
    function(group_sources)
    endfunction()
endif()

macro(redbolt_add_external_subdirectory source_dir binary_dir)
    set(_redbolt_saved_c_flags "${CMAKE_C_FLAGS}")
    set(_redbolt_saved_cxx_flags "${CMAKE_CXX_FLAGS}")

    if(MSVC)
        set(CMAKE_C_FLAGS "${CMAKE_C_FLAGS} /W0")
        set(CMAKE_CXX_FLAGS "${CMAKE_CXX_FLAGS} /W0")
    else()
        set(CMAKE_C_FLAGS "${CMAKE_C_FLAGS} -w")
        set(CMAKE_CXX_FLAGS "${CMAKE_CXX_FLAGS} -w")
    endif()

    add_subdirectory("${source_dir}" "${binary_dir}" EXCLUDE_FROM_ALL)

    set(CMAKE_C_FLAGS "${_redbolt_saved_c_flags}")
    set(CMAKE_CXX_FLAGS "${_redbolt_saved_cxx_flags}")
endmacro()

set(SDL_SHARED ON CACHE BOOL "" FORCE)
set(SDL_STATIC OFF CACHE BOOL "" FORCE)
set(SDL2_DISABLE_SDL2MAIN ON CACHE BOOL "" FORCE)
set(SDL2_DISABLE_INSTALL ON CACHE BOOL "" FORCE)
set(SDL_TEST OFF CACHE BOOL "" FORCE)
set(SDL_TESTS OFF CACHE BOOL "" FORCE)
set(SDL_AUDIO ON CACHE BOOL "" FORCE)
set(SDL_ALSA ON CACHE BOOL "" FORCE)
set(SDL_ALSA_SHARED ON CACHE BOOL "" FORCE)
set(SDL_DISKAUDIO ON CACHE BOOL "" FORCE)
set(SDL_DUMMYAUDIO ON CACHE BOOL "" FORCE)
set(SDL_PIPEWIRE ON CACHE BOOL "" FORCE)
set(SDL_PIPEWIRE_SHARED ON CACHE BOOL "" FORCE)
set(SDL_PULSEAUDIO ON CACHE BOOL "" FORCE)
set(SDL_PULSEAUDIO_SHARED ON CACHE BOOL "" FORCE)
set(SDL_RENDER OFF CACHE BOOL "" FORCE)
set(SDL_JOYSTICK ON CACHE BOOL "" FORCE)
set(SDL_HAPTIC OFF CACHE BOOL "" FORCE)
set(SDL_SENSOR OFF CACHE BOOL "" FORCE)

redbolt_add_external_subdirectory("${REDBOLT_SDL_SOURCE_ROOT}" "${CMAKE_BINARY_DIR}/deps/SDL")

find_package(PkgConfig REQUIRED)
pkg_check_modules(REDBOLT_SDL2_IMAGE REQUIRED SDL2_image)

set(REDBOLT_SDL2_IMAGE_LIBS ${REDBOLT_SDL2_IMAGE_LIBRARIES})
list(REMOVE_ITEM REDBOLT_SDL2_IMAGE_LIBS SDL2)
if(NOT REDBOLT_SDL2_IMAGE_LIBS)
    message(FATAL_ERROR "SDL2_image package was found, but usable link targets were missing")
endif()

add_library(redbolt-sdl2-image INTERFACE)
target_include_directories(redbolt-sdl2-image SYSTEM INTERFACE
    ${REDBOLT_SDL2_IMAGE_INCLUDE_DIRS})
target_link_libraries(redbolt-sdl2-image INTERFACE
    ${REDBOLT_SDL2_IMAGE_LIBS})

set(REDBOLT_SDL_IMAGE_INCLUDE_DIRS
    ${REDBOLT_SDL2_IMAGE_INCLUDE_DIRS}
    CACHE INTERNAL "SDL_image include directories used by RedBolt")
list(GET REDBOLT_SDL2_IMAGE_INCLUDE_DIRS 0 REDBOLT_SDL_IMAGE_PRIMARY_INCLUDE_DIR)
set(REDBOLT_SDL_IMAGE_HEADER
    "${REDBOLT_SDL_IMAGE_PRIMARY_INCLUDE_DIR}/SDL_image.h"
    CACHE INTERNAL "Primary SDL_image header used by RedBolt")
redbolt_require_path("${REDBOLT_SDL_IMAGE_HEADER}" "SDL_image header")

set(DEBUG_CONFIGURATIONS DEBUG CACHE INTERNAL "Debug configurations")
set(RELEASE_CONFIGURATIONS RELEASE RELWITHDEBINFO MINSIZEREL CACHE INTERNAL "Release configurations")
set(DILIGENT_CORE_SOURCE_DIR "${REDBOLT_DILIGENT_SOURCE_ROOT}" CACHE INTERNAL "DiligentCore module source directory")
set(DILIGENT_CORE_DIR "extern/thirdparty/Diligent" CACHE INTERNAL "DiligentCore relative source directory")
set(DILIGENT_INSTALL_CORE OFF CACHE BOOL "" FORCE)
set(DILIGENT_INSTALL_PDB OFF CACHE BOOL "" FORCE)
set(DILIGENT_NO_FORMAT_VALIDATION ON CACHE BOOL "" FORCE)
set(DILIGENT_NO_HLSL ON CACHE BOOL "" FORCE)
set(DILIGENT_NO_ARCHIVER ON CACHE BOOL "" FORCE)
set(DILIGENT_NO_GLSLANG OFF CACHE BOOL "" FORCE)

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
set(GL_SUPPORTED TRUE CACHE INTERNAL "OpenGL is supported")
set(GLES_SUPPORTED FALSE CACHE INTERNAL "OpenGLES is not supported")
set(VULKAN_SUPPORTED TRUE CACHE INTERNAL "Vulkan is supported on Linux platform")
set(METAL_SUPPORTED FALSE CACHE INTERNAL "Metal is not supported")
set(WEBGPU_SUPPORTED FALSE CACHE INTERNAL "WebGPU is not supported")
set(ARCHIVER_SUPPORTED FALSE CACHE INTERNAL "Archiver is not supported")

if(CMAKE_SYSTEM_NAME STREQUAL "Linux")
    set(PLATFORM_LINUX TRUE CACHE INTERNAL "Target platform: Linux")
else()
    message(FATAL_ERROR "RedBolt has not mapped Diligent platform selection for ${CMAKE_SYSTEM_NAME}")
endif()

find_package(GLEW QUIET)
if(TARGET GLEW::glew)
    # Diligent already has the target name it expects.
elseif(TARGET GLEW::GLEW)
    add_library(GLEW::glew ALIAS GLEW::GLEW)
else()
    pkg_check_modules(REDBOLT_GLEW REQUIRED IMPORTED_TARGET glew)
    add_library(redbolt-system-glew INTERFACE)
    target_link_libraries(redbolt-system-glew INTERFACE PkgConfig::REDBOLT_GLEW)
    add_library(GLEW::glew ALIAS redbolt-system-glew)
endif()

add_library(Diligent-PublicBuildSettings INTERFACE)
target_compile_definitions(Diligent-PublicBuildSettings INTERFACE
    PLATFORM_LINUX=1
    D3D11_SUPPORTED=0
    D3D12_SUPPORTED=0
    GL_SUPPORTED=1
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
set(SPIRV_CROSS_ENABLE_C_API ON CACHE BOOL "Enable SPIRV-Cross C API" FORCE)
set(SPIRV_CROSS_ENABLE_HLSL ON CACHE BOOL "Enable SPIRV-Cross HLSL target support" FORCE)
set(ARCHIVER_SUPPORTED TRUE CACHE INTERNAL "Enable Diligent SPIR-V toolchain dependencies" FORCE)
redbolt_add_external_subdirectory("${REDBOLT_DILIGENT_SOURCE_ROOT}/ThirdParty" "${CMAKE_BINARY_DIR}/deps/Diligent/ThirdParty")
set(ARCHIVER_SUPPORTED FALSE CACHE INTERNAL "Archiver is not supported" FORCE)
if(TARGET SPIRV-Tools-opt)
    add_dependencies(SPIRV-Tools-opt
        spirv-tools-build-version
        spirv-tools-header-DebugInfo
        spirv-tools-header-NonSemanticShaderDebugInfo100
        spirv-tools-header-OpenCLDebugInfo100)
endif()
redbolt_add_external_subdirectory("${REDBOLT_DILIGENT_SOURCE_ROOT}/Primitives" "${CMAKE_BINARY_DIR}/deps/Diligent/Primitives")
redbolt_add_external_subdirectory("${REDBOLT_DILIGENT_SOURCE_ROOT}/Platforms" "${CMAKE_BINARY_DIR}/deps/Diligent/Platforms")
redbolt_add_external_subdirectory("${REDBOLT_DILIGENT_SOURCE_ROOT}/Common" "${CMAKE_BINARY_DIR}/deps/Diligent/Common")
redbolt_add_external_subdirectory("${REDBOLT_DILIGENT_SOURCE_ROOT}/Graphics/GraphicsAccessories" "${CMAKE_BINARY_DIR}/deps/Diligent/Graphics/GraphicsAccessories")

add_library(Diligent-GraphicsEngineInterface INTERFACE)
target_include_directories(Diligent-GraphicsEngineInterface INTERFACE
    "${REDBOLT_DILIGENT_SOURCE_ROOT}/Graphics/GraphicsEngine/interface")
target_link_libraries(Diligent-GraphicsEngineInterface INTERFACE
    Diligent-Primitives
    Diligent-PublicBuildSettings)
redbolt_add_external_subdirectory("${REDBOLT_DILIGENT_SOURCE_ROOT}/Graphics/GraphicsEngine" "${CMAKE_BINARY_DIR}/deps/Diligent/Graphics/GraphicsEngine")
redbolt_add_external_subdirectory("${REDBOLT_DILIGENT_SOURCE_ROOT}/Graphics/ShaderTools" "${CMAKE_BINARY_DIR}/deps/Diligent/Graphics/ShaderTools")
redbolt_add_external_subdirectory("${REDBOLT_DILIGENT_SOURCE_ROOT}/Graphics/GraphicsEngineNextGenBase" "${CMAKE_BINARY_DIR}/deps/Diligent/Graphics/GraphicsEngineNextGenBase")
redbolt_add_external_subdirectory("${REDBOLT_DILIGENT_SOURCE_ROOT}/Graphics/GraphicsEngineOpenGL" "${CMAKE_BINARY_DIR}/deps/Diligent/Graphics/GraphicsEngineOpenGL")
redbolt_add_external_subdirectory("${REDBOLT_DILIGENT_SOURCE_ROOT}/Graphics/GraphicsEngineVulkan" "${CMAKE_BINARY_DIR}/deps/Diligent/Graphics/GraphicsEngineVulkan")

set(REDBOLT_DILIGENT_INCLUDE_DIRS
    "${REDBOLT_DILIGENT_SOURCE_ROOT}/Primitives/interface"
    "${REDBOLT_DILIGENT_SOURCE_ROOT}/Platforms/interface"
    "${REDBOLT_DILIGENT_SOURCE_ROOT}/Platforms/Basic/interface"
    "${REDBOLT_DILIGENT_SOURCE_ROOT}/Platforms/Linux/interface"
    "${REDBOLT_DILIGENT_SOURCE_ROOT}/Common/interface"
    "${REDBOLT_DILIGENT_SOURCE_ROOT}/Graphics/GraphicsEngine/interface"
    "${REDBOLT_DILIGENT_SOURCE_ROOT}/Graphics/GraphicsAccessories/interface"
    "${REDBOLT_DILIGENT_SOURCE_ROOT}/Graphics/GraphicsEngineOpenGL/interface"
    "${REDBOLT_DILIGENT_SOURCE_ROOT}/Graphics/GraphicsEngineVulkan/interface"
    "${REDBOLT_DILIGENT_SOURCE_ROOT}/Graphics/HLSL2GLSLConverterLib/interface"
    "${REDBOLT_DILIGENT_SOURCE_ROOT}/ThirdParty/Vulkan-Headers/include"
    "${REDBOLT_DILIGENT_SOURCE_ROOT}/ThirdParty/volk"
    CACHE INTERNAL "Diligent include directories used by RedBolt")

set(REDBOLT_DILIGENT_BINDING_DEFINES
    "PLATFORM_LINUX=1"
    "D3D11_SUPPORTED=0"
    "D3D12_SUPPORTED=0"
    "GL_SUPPORTED=1"
    "GLES_SUPPORTED=0"
    "VULKAN_SUPPORTED=1"
    "METAL_SUPPORTED=0"
    "WEBGPU_SUPPORTED=0"
    "DILIGENT_NO_HLSL=1"
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
    Diligent-GraphicsEngineOpenGLInterface
    Diligent-GraphicsEngineVkInterface
    Diligent-TargetPlatform)

add_library(redbolt-native-support INTERFACE)
target_include_directories(redbolt-native-support INTERFACE
    "${REDBOLT_SHARPCPP_ROOT}/include"
    "${CMAKE_CURRENT_SOURCE_DIR}/native/RedBolt.Native/include")
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
        Diligent-GraphicsEngineOpenGL-static
        Diligent-GraphicsEngineVk-static
    COMMENT "Building RedBolt Diligent dependency targets")

redbolt_add_external_subdirectory("${REDBOLT_IMGUI_SOURCE_ROOT}" "${CMAKE_BINARY_DIR}/deps/ImGui")

add_custom_target(redbolt-native-dependency-batch
    DEPENDS SDL2 redbolt-diligent-build ImGui
    COMMENT "Building RedBolt initial native dependency batch")
