set(REDBOLT_DILIGENT_SOURCE_ROOT "${REDBOLT_THIRDPARTY_ROOT}/Diligent" CACHE INTERNAL "Diligent source root")
redbolt_require_path("${REDBOLT_DILIGENT_SOURCE_ROOT}/Graphics/GraphicsEngine/interface/GraphicsTypes.h" "Diligent headers")

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

if(CMAKE_SYSTEM_NAME STREQUAL "Emscripten")
    set(PLATFORM_EMSCRIPTEN TRUE CACHE INTERNAL "Target platform: Emscripten")
    set(GL_SUPPORTED FALSE CACHE INTERNAL "Desktop OpenGL is not supported on Emscripten" FORCE)
    set(GLES_SUPPORTED TRUE CACHE INTERNAL "OpenGLES is supported on Emscripten" FORCE)
    set(VULKAN_SUPPORTED FALSE CACHE INTERNAL "Vulkan is not supported on Emscripten" FORCE)
    set(WEBGPU_SUPPORTED FALSE CACHE INTERNAL "WebGPU is not enabled by the RedBolt web build" FORCE)
elseif(CMAKE_SYSTEM_NAME STREQUAL "Linux")
    set(PLATFORM_LINUX TRUE CACHE INTERNAL "Target platform: Linux")
else()
    message(FATAL_ERROR "RedBolt has not mapped Diligent platform selection for ${CMAKE_SYSTEM_NAME}")
endif()

if(NOT EMSCRIPTEN)
    find_package(GLEW QUIET)
    if(TARGET GLEW::glew)
    elseif(TARGET GLEW::GLEW)
        add_library(GLEW::glew ALIAS GLEW::GLEW)
    else()
        find_package(PkgConfig REQUIRED)
        pkg_check_modules(REDBOLT_GLEW REQUIRED IMPORTED_TARGET glew)
        add_library(redbolt-system-glew INTERFACE)
        target_link_libraries(redbolt-system-glew INTERFACE PkgConfig::REDBOLT_GLEW)
        add_library(GLEW::glew ALIAS redbolt-system-glew)
    endif()
endif()

add_library(Diligent-PublicBuildSettings INTERFACE)
target_compile_definitions(Diligent-PublicBuildSettings INTERFACE
    "$<$<BOOL:${PLATFORM_LINUX}>:PLATFORM_LINUX=1>"
    "$<$<BOOL:${PLATFORM_EMSCRIPTEN}>:PLATFORM_EMSCRIPTEN=1>"
    D3D11_SUPPORTED=0
    D3D12_SUPPORTED=0
    "GL_SUPPORTED=$<BOOL:${GL_SUPPORTED}>"
    "GLES_SUPPORTED=$<BOOL:${GLES_SUPPORTED}>"
    "VULKAN_SUPPORTED=$<BOOL:${VULKAN_SUPPORTED}>"
    METAL_SUPPORTED=0
    "WEBGPU_SUPPORTED=$<BOOL:${WEBGPU_SUPPORTED}>")

add_library(Diligent-BuildSettings INTERFACE)
target_link_libraries(Diligent-BuildSettings INTERFACE Diligent-PublicBuildSettings)
target_compile_definitions(Diligent-BuildSettings INTERFACE
    __forceinline=inline
    "$<$<CONFIG:DEBUG>:_DEBUG;DEBUG>"
    "$<$<NOT:$<CONFIG:DEBUG>>:NDEBUG>")

include("${REDBOLT_DILIGENT_SOURCE_ROOT}/BuildTools/CMake/BuildUtils.cmake")
set(SPIRV_CROSS_ENABLE_C_API ON CACHE BOOL "Enable SPIRV-Cross C API" FORCE)
set(SPIRV_CROSS_ENABLE_HLSL ON CACHE BOOL "Enable SPIRV-Cross HLSL target support" FORCE)
set(SPIRV-Headers_SOURCE_DIR
    "${REDBOLT_DILIGENT_SOURCE_ROOT}/ThirdParty/SPIRV-Headers"
    CACHE PATH "SPIR-V headers source directory" FORCE)
set(SPIRV-Headers_SOURCE_DIR
    "${REDBOLT_DILIGENT_SOURCE_ROOT}/ThirdParty/SPIRV-Headers")
set(ARCHIVER_SUPPORTED TRUE CACHE INTERNAL "Enable Diligent SPIR-V toolchain dependencies" FORCE)
set(_redbolt_saved_build_shared_libs "${BUILD_SHARED_LIBS}")
set(CMAKE_POLICY_DEFAULT_CMP0077 NEW)
set(BUILD_SHARED_LIBS OFF)
set(BUILD_SHARED_LIBS OFF CACHE BOOL "" FORCE)
redbolt_add_external_subdirectory("${REDBOLT_DILIGENT_SOURCE_ROOT}/ThirdParty" "${CMAKE_BINARY_DIR}/deps/Diligent/ThirdParty")
set(BUILD_SHARED_LIBS "${_redbolt_saved_build_shared_libs}")
set(BUILD_SHARED_LIBS "${_redbolt_saved_build_shared_libs}" CACHE BOOL "" FORCE)
if(NOT TARGET glslang-default-resource-limits)
    add_library(glslang-default-resource-limits STATIC
        "${REDBOLT_DILIGENT_SOURCE_ROOT}/ThirdParty/glslang/StandAlone/ResourceLimits.cpp"
        "${REDBOLT_DILIGENT_SOURCE_ROOT}/ThirdParty/glslang/StandAlone/resource_limits_c.cpp")
    set_target_properties(glslang-default-resource-limits PROPERTIES
        POSITION_INDEPENDENT_CODE ON)
    target_include_directories(glslang-default-resource-limits PUBLIC
        "${REDBOLT_DILIGENT_SOURCE_ROOT}/ThirdParty/glslang/StandAlone"
        "${REDBOLT_DILIGENT_SOURCE_ROOT}/ThirdParty/glslang")
endif()
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
if(NOT EMSCRIPTEN)
    redbolt_add_external_subdirectory("${REDBOLT_DILIGENT_SOURCE_ROOT}/Graphics/GraphicsEngineVulkan" "${CMAKE_BINARY_DIR}/deps/Diligent/Graphics/GraphicsEngineVulkan")
endif()

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
    set(_redbolt_platform_defines "PLATFORM_EMSCRIPTEN=1" "PLATFORM_LINUX=0")
    set(_redbolt_graphics_defines "GL_SUPPORTED=0" "GLES_SUPPORTED=1" "VULKAN_SUPPORTED=0" "WEBGPU_SUPPORTED=0")
else()
    set(_redbolt_platform_defines "PLATFORM_EMSCRIPTEN=0" "PLATFORM_LINUX=1")
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
