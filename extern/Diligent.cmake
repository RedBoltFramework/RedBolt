set(REDBOLT_DILIGENT_SOURCE_ROOT "${REDBOLT_EXTERN_ROOT}/Diligent" CACHE INTERNAL "Diligent source root")
redbolt_require_path("${REDBOLT_DILIGENT_SOURCE_ROOT}/Graphics/GraphicsEngine/interface/GraphicsTypes.h" "Diligent headers")

set(DILIGENT_INSTALL_CORE OFF CACHE BOOL "" FORCE)
set(DILIGENT_INSTALL_PDB OFF CACHE BOOL "" FORCE)
set(DILIGENT_BUILD_TESTS OFF CACHE BOOL "" FORCE)
set(DILIGENT_NO_FORMAT_VALIDATION ON CACHE BOOL "" FORCE)
set(DILIGENT_NO_HLSL ON CACHE BOOL "" FORCE)
set(DILIGENT_NO_ARCHIVER OFF CACHE BOOL "" FORCE)
set(DILIGENT_NO_SUPER_RESOLUTION ON CACHE BOOL "" FORCE)
set(DILIGENT_NO_DIRECT3D11 ON CACHE BOOL "" FORCE)
set(DILIGENT_NO_DIRECT3D12 ON CACHE BOOL "" FORCE)
set(DILIGENT_NO_OPENGL OFF CACHE BOOL "" FORCE)
set(DILIGENT_NO_METAL ON CACHE BOOL "" FORCE)
set(DILIGENT_NO_WEBGPU ON CACHE BOOL "" FORCE)
set(DILIGENT_NO_GLSLANG OFF CACHE BOOL "" FORCE)
set(SPIRV_CROSS_ENABLE_C_API ON CACHE BOOL "Enable SPIRV-Cross C API" FORCE)
set(SPIRV_CROSS_ENABLE_HLSL ON CACHE BOOL "Enable SPIRV-Cross HLSL target support" FORCE)
if(EMSCRIPTEN)
    set(DILIGENT_NO_VULKAN ON CACHE BOOL "" FORCE)
else()
    set(DILIGENT_NO_VULKAN OFF CACHE BOOL "" FORCE)
endif()

set(_redbolt_saved_build_shared_libs "${BUILD_SHARED_LIBS}")
set(BUILD_SHARED_LIBS OFF)
redbolt_add_external_subdirectory(
    "${REDBOLT_DILIGENT_SOURCE_ROOT}"
    "${CMAKE_BINARY_DIR}/deps/Diligent")
set(BUILD_SHARED_LIBS "${_redbolt_saved_build_shared_libs}")

set(REDBOLT_DILIGENT_INCLUDE_DIRS
    "${REDBOLT_DILIGENT_SOURCE_ROOT}/Primitives/interface"
    "${REDBOLT_DILIGENT_SOURCE_ROOT}/Platforms/interface"
    "${REDBOLT_DILIGENT_SOURCE_ROOT}/Platforms/Basic/interface"
    "${REDBOLT_DILIGENT_SOURCE_ROOT}/Common/interface"
    "${REDBOLT_DILIGENT_SOURCE_ROOT}/Graphics/GraphicsEngine/interface"
    "${REDBOLT_DILIGENT_SOURCE_ROOT}/Graphics/GraphicsAccessories/interface"
    "${REDBOLT_DILIGENT_SOURCE_ROOT}/Graphics/GraphicsEngineOpenGL/interface"
    "${REDBOLT_DILIGENT_SOURCE_ROOT}/Graphics/HLSL2GLSLConverterLib/interface")

if(EMSCRIPTEN)
    list(APPEND REDBOLT_DILIGENT_INCLUDE_DIRS
        "${REDBOLT_DILIGENT_SOURCE_ROOT}/Platforms/Emscripten/interface")
else()
    list(APPEND REDBOLT_DILIGENT_INCLUDE_DIRS
        "${REDBOLT_DILIGENT_SOURCE_ROOT}/Platforms/Linux/interface"
        "${REDBOLT_DILIGENT_SOURCE_ROOT}/Graphics/GraphicsEngineVulkan/interface"
        "${REDBOLT_DILIGENT_SOURCE_ROOT}/ThirdParty/Vulkan-Headers/include"
        "${REDBOLT_DILIGENT_SOURCE_ROOT}/ThirdParty/volk")
endif()
set(REDBOLT_DILIGENT_INCLUDE_DIRS ${REDBOLT_DILIGENT_INCLUDE_DIRS}
    CACHE INTERNAL "Diligent include directories used by RedBolt")

if(EMSCRIPTEN)
    set(_redbolt_platform_defines "PLATFORM_WEB=1" "PLATFORM_EMSCRIPTEN=1" "PLATFORM_LINUX=0")
    set(_redbolt_graphics_defines "GL_SUPPORTED=0" "GLES_SUPPORTED=1" "VULKAN_SUPPORTED=0" "WEBGPU_SUPPORTED=0")
else()
    set(_redbolt_platform_defines "PLATFORM_WEB=0" "PLATFORM_EMSCRIPTEN=0" "PLATFORM_LINUX=1")
    set(_redbolt_graphics_defines "GL_SUPPORTED=1" "GLES_SUPPORTED=0" "VULKAN_SUPPORTED=1" "WEBGPU_SUPPORTED=0")
endif()
set(REDBOLT_DILIGENT_BINDING_DEFINES
    ${_redbolt_platform_defines}
    "D3D11_SUPPORTED=0"
    "D3D12_SUPPORTED=0"
    ${_redbolt_graphics_defines}
    "METAL_SUPPORTED=0"
    "DILIGENT_NO_HLSL=1"
    CACHE INTERNAL "Diligent compile definitions used by RedBolt SharpCpp generation")

add_library(redbolt-diligent-headers INTERFACE)
target_include_directories(redbolt-diligent-headers SYSTEM INTERFACE
    ${REDBOLT_DILIGENT_INCLUDE_DIRS})

if(CMAKE_SYSTEM_NAME STREQUAL "Linux" OR CMAKE_SYSTEM_NAME STREQUAL "Emscripten")
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
    Diligent-TargetPlatform)
if(NOT EMSCRIPTEN)
    target_link_libraries(redbolt-diligent-headers INTERFACE Diligent-GraphicsEngineVkInterface)
endif()

set(REDBOLT_DILIGENT_ENGINE_TARGETS
    Diligent-GraphicsEngine
    Diligent-GraphicsEngineOpenGL-static)
if(NOT EMSCRIPTEN)
    list(APPEND REDBOLT_DILIGENT_ENGINE_TARGETS Diligent-GraphicsEngineVk-static)
endif()
add_custom_target(redbolt-diligent-build
    DEPENDS ${REDBOLT_DILIGENT_ENGINE_TARGETS}
    COMMENT "Building RedBolt Diligent dependency targets")
