set(DILIGENT_INSTALL_CORE OFF CACHE BOOL "" FORCE)
set(DILIGENT_INSTALL_PDB OFF CACHE BOOL "" FORCE)
set(DILIGENT_BUILD_TESTS OFF CACHE BOOL "" FORCE)
set(DILIGENT_NO_FORMAT_VALIDATION ON CACHE BOOL "" FORCE)
set(DILIGENT_NO_HLSL ON CACHE BOOL "" FORCE)
set(DILIGENT_NO_ARCHIVER OFF CACHE BOOL "" FORCE)
set(DILIGENT_NO_SUPER_RESOLUTION ON CACHE BOOL "" FORCE)
set(_redbolt_diligent_compile_shaders_with_wine OFF)
if(WIN32)
    set(DILIGENT_NO_DIRECT3D11 OFF CACHE BOOL "" FORCE)
    if(MINGW AND CMAKE_HOST_SYSTEM_NAME STREQUAL "Linux")
        find_program(REDBOLT_WINE_EXECUTABLE NAMES wine)
        if(REDBOLT_WINE_EXECUTABLE AND EXISTS "${REDBOLT_WINE_EXECUTABLE}")
            set(_redbolt_diligent_compile_shaders_with_wine ON)
            set(DILIGENT_NO_DIRECT3D12 OFF CACHE BOOL "" FORCE)
        else()
            message(STATUS "Wine was not found; disabling Diligent Direct3D 12")
            set(DILIGENT_NO_DIRECT3D12 ON CACHE BOOL "" FORCE)
        endif()
    else()
        set(DILIGENT_NO_DIRECT3D12 OFF CACHE BOOL "" FORCE)
    endif()
else()
    set(DILIGENT_NO_DIRECT3D11 ON CACHE BOOL "" FORCE)
    set(DILIGENT_NO_DIRECT3D12 ON CACHE BOOL "" FORCE)
endif()
set(DILIGENT_NO_OPENGL OFF CACHE BOOL "" FORCE)
set(DILIGENT_NO_METAL ON CACHE BOOL "" FORCE)
set(DILIGENT_NO_WEBGPU ON CACHE BOOL "" FORCE)
set(DILIGENT_NO_GLSLANG OFF CACHE BOOL "" FORCE)
set(SPIRV_CROSS_ENABLE_C_API ON CACHE BOOL "Enable SPIRV-Cross C API" FORCE)
set(SPIRV_CROSS_ENABLE_HLSL ON CACHE BOOL "Enable SPIRV-Cross HLSL target support" FORCE)
set(SPIRV_CROSS_SHARED ${BUILD_SHARED_LIBS} CACHE BOOL "Build SPIRV-Cross C shared library" FORCE)
set(SPIRV_TOOLS_BUILD_STATIC ON CACHE BOOL "" FORCE)
if(EMSCRIPTEN)
    set(DILIGENT_NO_VULKAN ON CACHE BOOL "" FORCE)
else()
    set(DILIGENT_NO_VULKAN OFF CACHE BOOL "" FORCE)
endif()

if(_redbolt_diligent_compile_shaders_with_wine)
    set(_redbolt_diligent_wine_prefix "${CMAKE_BINARY_DIR}/wine")
    set(_redbolt_diligent_wine_stamp "${_redbolt_diligent_wine_prefix}/.redbolt-initialized")
    add_custom_command(
        OUTPUT "${_redbolt_diligent_wine_stamp}"
        COMMAND "${CMAKE_COMMAND}" -E make_directory "${_redbolt_diligent_wine_prefix}"
        COMMAND "${CMAKE_COMMAND}" -E env
            "DISPLAY="
            "WINEARCH=win64"
            "WINEPREFIX=${_redbolt_diligent_wine_prefix}"
            "WINEDLLOVERRIDES=mscoree,mshtml="
            "WINEDEBUG=-all"
            "${REDBOLT_WINE_EXECUTABLE}" wineboot --init
        COMMAND "${CMAKE_COMMAND}" -E touch "${_redbolt_diligent_wine_stamp}"
        VERBATIM)
    add_custom_target(redbolt-diligent-wine-prefix DEPENDS "${_redbolt_diligent_wine_stamp}")
    add_executable(redbolt-diligent-shader-compiler
        "${CMAKE_SOURCE_DIR}/src/RedBolt/Native/src/DiligentShaderCompiler.c")
    target_link_libraries(redbolt-diligent-shader-compiler PRIVATE d3dcompiler)
    set(DILIGENT_D3D12_SHADER_COMPILER_RUNNER "${REDBOLT_WINE_EXECUTABLE}")
    set(DILIGENT_D3D12_SHADER_COMPILER_WINE_PREFIX "${_redbolt_diligent_wine_prefix}")
    set(DILIGENT_D3D12_SHADER_COMPILER "$<TARGET_FILE:redbolt-diligent-shader-compiler>")
    set(DILIGENT_D3D12_SHADER_COMPILER_TARGET
        redbolt-diligent-wine-prefix redbolt-diligent-shader-compiler)
endif()

set(_redbolt_saved_build_shared_libs "${BUILD_SHARED_LIBS}")
set(BUILD_SHARED_LIBS OFF)
if(EMSCRIPTEN OR MINGW)
    set(CMAKE_CXX_LINK_LIBRARY_USING_WHOLE_ARCHIVE
        "LINKER:--whole-archive"
        "<LINK_ITEM>"
        "LINKER:--no-whole-archive")
    set(CMAKE_CXX_LINK_LIBRARY_USING_WHOLE_ARCHIVE_SUPPORTED TRUE)
endif()

CPMAddPackage(
    NAME Diligent
    GIT_REPOSITORY https://github.com/DiligentGraphics/DiligentCore.git
    GIT_TAG d4fe9c85a2011b4fd030a3cd107f5c46c94fbe8b
    CUSTOM_CACHE_KEY "."
    PATCHES "${CMAKE_CURRENT_LIST_DIR}/Diligent.patch"
    EXCLUDE_FROM_ALL YES)
set(BUILD_SHARED_LIBS "${_redbolt_saved_build_shared_libs}")
set(REDBOLT_DILIGENT_SOURCE_ROOT "${Diligent_SOURCE_DIR}" CACHE INTERNAL "Diligent source root")
redbolt_require_path("${REDBOLT_DILIGENT_SOURCE_ROOT}/Graphics/GraphicsEngine/interface/GraphicsTypes.h" "Diligent headers")

if(MINGW AND (D3D11_SUPPORTED OR D3D12_SUPPORTED))
    target_compile_definitions(Diligent-BuildSettings INTERFACE _Maybenull_=)
    target_include_directories(Diligent-ShaderTools PUBLIC
        "${REDBOLT_DILIGENT_SOURCE_ROOT}/ThirdParty/DirectXShaderCompiler")
endif()

if(NOT EMSCRIPTEN)
    set_target_properties(glew-static PROPERTIES
        C_VISIBILITY_PRESET default
        CXX_VISIBILITY_PRESET default
        VISIBILITY_INLINES_HIDDEN OFF)
    set(_redbolt_diligent_shared_targets
        Diligent-GraphicsEngineOpenGL-shared
        Diligent-GraphicsEngineVk-shared)
    if(WIN32)
        if(D3D11_SUPPORTED)
            list(APPEND _redbolt_diligent_shared_targets Diligent-GraphicsEngineD3D11-shared)
        endif()
        if(D3D12_SUPPORTED)
            list(APPEND _redbolt_diligent_shared_targets Diligent-GraphicsEngineD3D12-shared)
        endif()
    endif()
    if(WIN32)
        set_target_properties(${_redbolt_diligent_shared_targets} PROPERTIES PREFIX "")
    else()
        set_target_properties(${_redbolt_diligent_shared_targets}
            PROPERTIES PREFIX "${CMAKE_SHARED_LIBRARY_PREFIX}")
    endif()
    set_property(TARGET spirv-cross-c-shared PROPERTY VERSION)
    set_property(TARGET spirv-cross-c-shared PROPERTY SOVERSION)
endif()

if(MINGW)
    function(redbolt_replace_diligent_link_library target old_name new_name)
        foreach(_redbolt_link_property IN ITEMS LINK_LIBRARIES INTERFACE_LINK_LIBRARIES)
            get_target_property(_redbolt_link_libraries ${target} ${_redbolt_link_property})
            if(_redbolt_link_libraries)
                string(REPLACE "${old_name}" "${new_name}"
                    _redbolt_link_libraries "${_redbolt_link_libraries}")
                set_property(TARGET ${target} PROPERTY
                    ${_redbolt_link_property} "${_redbolt_link_libraries}")
            endif()
        endforeach()
    endfunction()

    foreach(_redbolt_diligent_target IN ITEMS
            Diligent-GraphicsEngineOpenGL-shared
            Diligent-GraphicsEngineVk-shared
            Diligent-Archiver-shared)
        get_target_property(_redbolt_link_options ${_redbolt_diligent_target} LINK_OPTIONS)
        list(FILTER _redbolt_link_options EXCLUDE REGEX "--version-script")
        set_property(TARGET ${_redbolt_diligent_target} PROPERTY LINK_OPTIONS ${_redbolt_link_options})
    endforeach()
    target_sources(Diligent-GraphicsEngineOpenGL-shared PRIVATE
        "${REDBOLT_DILIGENT_SOURCE_ROOT}/Graphics/GraphicsEngineOpenGL/src/GraphicsEngineOpenGL.def")
    target_sources(Diligent-GraphicsEngineVk-shared PRIVATE
        "${REDBOLT_DILIGENT_SOURCE_ROOT}/Graphics/GraphicsEngineVulkan/src/GraphicsEngineVk.def")
    target_sources(Diligent-Archiver-shared PRIVATE
        "${REDBOLT_DILIGENT_SOURCE_ROOT}/Graphics/Archiver/src/Archiver.def")

    foreach(_redbolt_diligent_target IN ITEMS
            Diligent-GraphicsEngineOpenGL-static
            Diligent-Archiver-static)
        redbolt_replace_diligent_link_library(
            ${_redbolt_diligent_target} "opengl32.lib" "opengl32")
    endforeach()

    set(_redbolt_diligent_direct3d_targets)
    if(D3D11_SUPPORTED)
        list(APPEND _redbolt_diligent_direct3d_targets Diligent-GraphicsEngineD3D11-static)
    endif()
    if(D3D12_SUPPORTED)
        list(APPEND _redbolt_diligent_direct3d_targets Diligent-GraphicsEngineD3D12-static)
    endif()
    foreach(_redbolt_diligent_target IN LISTS _redbolt_diligent_direct3d_targets)
        redbolt_replace_diligent_link_library(
            ${_redbolt_diligent_target} "dxgi.lib" "dxgi")
        redbolt_replace_diligent_link_library(
            ${_redbolt_diligent_target} "d3dcompiler.lib" "d3dcompiler")
        target_link_libraries(${_redbolt_diligent_target} PUBLIC dxguid)
    endforeach()
    if(D3D11_SUPPORTED)
        redbolt_replace_diligent_link_library(
            Diligent-GraphicsEngineD3D11-static "d3d11.lib" "d3d11")
    endif()

    redbolt_replace_diligent_link_library(
        Diligent-Win32Platform "Shlwapi.lib" "shlwapi")
endif()

set(REDBOLT_DILIGENT_INCLUDE_DIRS
    "${REDBOLT_DILIGENT_SOURCE_ROOT}/Primitives/interface"
    "${REDBOLT_DILIGENT_SOURCE_ROOT}/Platforms/interface"
    "${REDBOLT_DILIGENT_SOURCE_ROOT}/Platforms/Basic/interface"
    "${REDBOLT_DILIGENT_SOURCE_ROOT}/Common/interface"
    "${REDBOLT_DILIGENT_SOURCE_ROOT}/Graphics/GraphicsEngine/interface"
    "${REDBOLT_DILIGENT_SOURCE_ROOT}/Graphics/GraphicsAccessories/interface"
    "${REDBOLT_DILIGENT_SOURCE_ROOT}/Graphics/GraphicsEngineOpenGL/interface"
    "${REDBOLT_DILIGENT_SOURCE_ROOT}/Graphics/HLSL2GLSLConverterLib/interface"
    "${REDBOLT_DILIGENT_SOURCE_ROOT}/ThirdParty/glew/include")

if(EMSCRIPTEN)
    list(APPEND REDBOLT_DILIGENT_INCLUDE_DIRS
        "${REDBOLT_DILIGENT_SOURCE_ROOT}/Platforms/Emscripten/interface")
elseif(WIN32)
    list(APPEND REDBOLT_DILIGENT_INCLUDE_DIRS
        "${REDBOLT_DILIGENT_SOURCE_ROOT}/Platforms/Win32/interface"
        "${REDBOLT_DILIGENT_SOURCE_ROOT}/Graphics/GraphicsEngineD3DBase/interface"
        "${REDBOLT_DILIGENT_SOURCE_ROOT}/Graphics/GraphicsEngineVulkan/interface"
        "${REDBOLT_DILIGENT_SOURCE_ROOT}/ThirdParty/Vulkan-Headers/include"
        "${REDBOLT_DILIGENT_SOURCE_ROOT}/ThirdParty/volk")
    if(D3D11_SUPPORTED)
        list(APPEND REDBOLT_DILIGENT_INCLUDE_DIRS
            "${REDBOLT_DILIGENT_SOURCE_ROOT}/Graphics/GraphicsEngineD3D11/interface")
    endif()
    if(D3D12_SUPPORTED)
        list(APPEND REDBOLT_DILIGENT_INCLUDE_DIRS
            "${REDBOLT_DILIGENT_SOURCE_ROOT}/Graphics/GraphicsEngineD3D12/interface")
    endif()
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
elseif(WIN32)
    set(_redbolt_platform_defines "PLATFORM_WIN32=1" "PLATFORM_LINUX=0")
    if(D3D11_SUPPORTED)
        set(_redbolt_d3d11_supported 1)
    else()
        set(_redbolt_d3d11_supported 0)
    endif()
    if(D3D12_SUPPORTED)
        set(_redbolt_d3d12_supported 1)
    else()
        set(_redbolt_d3d12_supported 0)
    endif()
    set(_redbolt_graphics_defines
        "D3D11_SUPPORTED=${_redbolt_d3d11_supported}"
        "D3D12_SUPPORTED=${_redbolt_d3d12_supported}"
        "GL_SUPPORTED=1" "GLES_SUPPORTED=0" "VULKAN_SUPPORTED=1" "WEBGPU_SUPPORTED=0")
else()
    set(_redbolt_platform_defines "PLATFORM_WEB=0" "PLATFORM_EMSCRIPTEN=0" "PLATFORM_LINUX=1")
    set(_redbolt_graphics_defines "GL_SUPPORTED=1" "GLES_SUPPORTED=0" "VULKAN_SUPPORTED=1" "WEBGPU_SUPPORTED=0")
endif()
set(REDBOLT_DILIGENT_BINDING_DEFINES
    ${_redbolt_platform_defines}
    ${_redbolt_graphics_defines}
    "METAL_SUPPORTED=0"
    "DILIGENT_NO_HLSL=1"
    "GLEW_NO_GLU=1"
    CACHE INTERNAL "Diligent compile definitions used by RedBolt SharpCpp generation")

add_library(redbolt-diligent-headers INTERFACE)
target_include_directories(redbolt-diligent-headers SYSTEM INTERFACE
    ${REDBOLT_DILIGENT_INCLUDE_DIRS})

if(CMAKE_SYSTEM_NAME STREQUAL "Linux" OR CMAKE_SYSTEM_NAME STREQUAL "Emscripten" OR WIN32)
    target_compile_definitions(redbolt-diligent-headers INTERFACE ${REDBOLT_DILIGENT_BINDING_DEFINES})
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
if(WIN32)
    if(D3D11_SUPPORTED)
        target_link_libraries(redbolt-diligent-headers INTERFACE Diligent-GraphicsEngineD3D11Interface)
    endif()
    if(D3D12_SUPPORTED)
        target_link_libraries(redbolt-diligent-headers INTERFACE Diligent-GraphicsEngineD3D12Interface)
    endif()
endif()

if(EMSCRIPTEN)
    set(REDBOLT_DILIGENT_OPENGL_TARGET Diligent-GraphicsEngineOpenGL-static)
    set(REDBOLT_DILIGENT_VULKAN_TARGET "")
    set(REDBOLT_GLEW_TARGET "")
    set(REDBOLT_SPIRV_CROSS_TARGET spirv-cross-c)
    set(REDBOLT_SPIRV_TOOLS_TARGET SPIRV-Tools)
else()
    set(REDBOLT_DILIGENT_OPENGL_TARGET Diligent-GraphicsEngineOpenGL-shared)
    set(REDBOLT_DILIGENT_VULKAN_TARGET Diligent-GraphicsEngineVk-shared)
    if(WIN32)
        if(D3D11_SUPPORTED)
            set(REDBOLT_DILIGENT_D3D11_TARGET Diligent-GraphicsEngineD3D11-shared)
        else()
            set(REDBOLT_DILIGENT_D3D11_TARGET "")
        endif()
        if(D3D12_SUPPORTED)
            set(REDBOLT_DILIGENT_D3D12_TARGET Diligent-GraphicsEngineD3D12-shared)
        else()
            set(REDBOLT_DILIGENT_D3D12_TARGET "")
        endif()
        set(REDBOLT_GLEW_TARGET glew-static opengl32)
        set(REDBOLT_SPIRV_TOOLS_TARGET SPIRV-Tools)
    else()
        set(REDBOLT_DILIGENT_D3D11_TARGET "")
        set(REDBOLT_DILIGENT_D3D12_TARGET "")
        set(REDBOLT_GLEW_TARGET "")
        set(REDBOLT_SPIRV_TOOLS_TARGET SPIRV-Tools-shared)
    endif()
    set(REDBOLT_SPIRV_CROSS_TARGET spirv-cross-c-shared)
endif()
set(REDBOLT_DILIGENT_OPENGL_TARGET "${REDBOLT_DILIGENT_OPENGL_TARGET}" CACHE INTERNAL "" FORCE)
set(REDBOLT_DILIGENT_VULKAN_TARGET "${REDBOLT_DILIGENT_VULKAN_TARGET}" CACHE INTERNAL "" FORCE)
set(REDBOLT_DILIGENT_D3D11_TARGET "${REDBOLT_DILIGENT_D3D11_TARGET}" CACHE INTERNAL "" FORCE)
set(REDBOLT_DILIGENT_D3D12_TARGET "${REDBOLT_DILIGENT_D3D12_TARGET}" CACHE INTERNAL "" FORCE)
set(REDBOLT_GLEW_TARGET "${REDBOLT_GLEW_TARGET}" CACHE INTERNAL "" FORCE)
set(REDBOLT_SPIRV_CROSS_TARGET "${REDBOLT_SPIRV_CROSS_TARGET}" CACHE INTERNAL "" FORCE)
set(REDBOLT_SPIRV_TOOLS_TARGET "${REDBOLT_SPIRV_TOOLS_TARGET}" CACHE INTERNAL "" FORCE)
set(REDBOLT_DILIGENT_ENGINE_TARGETS
    Diligent-GraphicsEngine
    ${REDBOLT_DILIGENT_D3D11_TARGET}
    ${REDBOLT_DILIGENT_D3D12_TARGET}
    ${REDBOLT_DILIGENT_OPENGL_TARGET}
    ${REDBOLT_DILIGENT_VULKAN_TARGET})
add_custom_target(redbolt-diligent-build
    DEPENDS ${REDBOLT_DILIGENT_ENGINE_TARGETS}
    COMMENT "Building RedBolt Diligent dependency targets")
