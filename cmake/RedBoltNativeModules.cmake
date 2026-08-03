# Native wrapper module registry.  The same names are used by the binding
# generator, the CMake targets, and the managed packaging resolver.
set(REDBOLT_NATIVE_MODULES
    SDL
    DILIGENT
    IMGUI
    STB
    LZ4
    HARFBUZZ
    RMLUI
    WEBP
    XATLAS
    EMBREE
    GLSLANG
    SPIRV_CROSS
    SPIRV_TOOLS
    SHADER_COMPILER
    JOLT)

set(REDBOLT_SDL_NATIVE_LIBRARY_NAME "RedBolt.Native.Sdl")
set(REDBOLT_DILIGENT_NATIVE_LIBRARY_NAME "RedBolt.Native.Diligent")
set(REDBOLT_IMGUI_NATIVE_LIBRARY_NAME "RedBolt.Native.DearImGui")
set(REDBOLT_STB_NATIVE_LIBRARY_NAME "RedBolt.Native.Stb")
set(REDBOLT_LZ4_NATIVE_LIBRARY_NAME "RedBolt.Native.Lz4")
set(REDBOLT_HARFBUZZ_NATIVE_LIBRARY_NAME "RedBolt.Native.HarfBuzz")
set(REDBOLT_RMLUI_NATIVE_LIBRARY_NAME "RedBolt.Native.RmlUi")
set(REDBOLT_WEBP_NATIVE_LIBRARY_NAME "RedBolt.Native.WebP")
set(REDBOLT_XATLAS_NATIVE_LIBRARY_NAME "RedBolt.Native.XAtlas")
set(REDBOLT_EMBREE_NATIVE_LIBRARY_NAME "RedBolt.Native.Embree")
set(REDBOLT_GLSLANG_NATIVE_LIBRARY_NAME "RedBolt.Native.Glslang")
set(REDBOLT_SPIRV_CROSS_NATIVE_LIBRARY_NAME "RedBolt.Native.SpirVCross")
set(REDBOLT_SPIRV_TOOLS_NATIVE_LIBRARY_NAME "RedBolt.Native.SpirVTools")
set(REDBOLT_SHADER_COMPILER_NATIVE_LIBRARY_NAME "RedBolt.Native.ShaderCompiler")
set(REDBOLT_JOLT_NATIVE_LIBRARY_NAME "RedBolt.Native.Jolt")

set(REDBOLT_SDL_NATIVE_TARGET redbolt-native-sdl)
set(REDBOLT_DILIGENT_NATIVE_TARGET redbolt-native-diligent)
set(REDBOLT_IMGUI_NATIVE_TARGET redbolt-native-dearimgui)
set(REDBOLT_STB_NATIVE_TARGET redbolt-native-stb)
set(REDBOLT_LZ4_NATIVE_TARGET redbolt-native-lz4)
set(REDBOLT_HARFBUZZ_NATIVE_TARGET redbolt-native-harfbuzz)
set(REDBOLT_RMLUI_NATIVE_TARGET redbolt-native-rmlui)
set(REDBOLT_WEBP_NATIVE_TARGET redbolt-native-webp)
set(REDBOLT_XATLAS_NATIVE_TARGET redbolt-native-xatlas)
set(REDBOLT_EMBREE_NATIVE_TARGET redbolt-native-embree)
set(REDBOLT_GLSLANG_NATIVE_TARGET redbolt-native-glslang)
set(REDBOLT_SPIRV_CROSS_NATIVE_TARGET redbolt-native-spirv-cross)
set(REDBOLT_SPIRV_TOOLS_NATIVE_TARGET redbolt-native-spirv-tools)
set(REDBOLT_SHADER_COMPILER_NATIVE_TARGET redbolt-native-shader-compiler)
set(REDBOLT_JOLT_NATIVE_TARGET redbolt-native-jolt)

set(REDBOLT_SDL_NATIVE_MANAGED_SELECTOR "RedBolt.Bindings.Sdl")
set(REDBOLT_DILIGENT_NATIVE_MANAGED_SELECTOR "RedBolt.Bindings.Diligent")
set(REDBOLT_IMGUI_NATIVE_MANAGED_SELECTOR "RedBolt.Bindings.DearImGui")
set(REDBOLT_STB_NATIVE_MANAGED_SELECTOR "RedBolt.Bindings.Stb")
set(REDBOLT_LZ4_NATIVE_MANAGED_SELECTOR "RedBolt.Bindings.Lz4")
set(REDBOLT_HARFBUZZ_NATIVE_MANAGED_SELECTOR "RedBolt.Bindings.HarfBuzz")
set(REDBOLT_RMLUI_NATIVE_MANAGED_SELECTOR "RedBolt.Bindings.RmlUi")
set(REDBOLT_WEBP_NATIVE_MANAGED_SELECTOR "RedBolt.Bindings.WebP")
set(REDBOLT_XATLAS_NATIVE_MANAGED_SELECTOR "RedBolt.Bindings.XAtlas")
set(REDBOLT_EMBREE_NATIVE_MANAGED_SELECTOR "RedBolt.Bindings.Embree")
set(REDBOLT_GLSLANG_NATIVE_MANAGED_SELECTOR "RedBolt.Bindings.Glslang")
set(REDBOLT_SPIRV_CROSS_NATIVE_MANAGED_SELECTOR "RedBolt.Bindings.SpirVCross")
set(REDBOLT_SPIRV_TOOLS_NATIVE_MANAGED_SELECTOR "RedBolt.Bindings.SpirVTools")
set(REDBOLT_SHADER_COMPILER_NATIVE_MANAGED_SELECTOR "RedBolt.Bindings.ShaderCompiler")
set(REDBOLT_JOLT_NATIVE_MANAGED_SELECTOR "RedBolt.Bindings.Jolt")

set(REDBOLT_SDL_NATIVE_REQUIRES "")
set(REDBOLT_DILIGENT_NATIVE_REQUIRES "SDL")
set(REDBOLT_IMGUI_NATIVE_REQUIRES "DILIGENT;SDL")
set(REDBOLT_STB_NATIVE_REQUIRES "")
set(REDBOLT_LZ4_NATIVE_REQUIRES "")
set(REDBOLT_HARFBUZZ_NATIVE_REQUIRES "")
set(REDBOLT_RMLUI_NATIVE_REQUIRES "HARFBUZZ")
set(REDBOLT_WEBP_NATIVE_REQUIRES "")
set(REDBOLT_XATLAS_NATIVE_REQUIRES "")
set(REDBOLT_EMBREE_NATIVE_REQUIRES "")
set(REDBOLT_GLSLANG_NATIVE_REQUIRES "")
set(REDBOLT_SPIRV_CROSS_NATIVE_REQUIRES "")
set(REDBOLT_SPIRV_TOOLS_NATIVE_REQUIRES "")
set(REDBOLT_SHADER_COMPILER_NATIVE_REQUIRES "")
set(REDBOLT_JOLT_NATIVE_REQUIRES "")

function(redbolt_native_module_value module field output_variable)
    string(TOUPPER "${module}" module_upper)
    set(value "${REDBOLT_${module_upper}_NATIVE_${field}}")
    if(value STREQUAL "")
        message(FATAL_ERROR "Unknown native module field '${module}.${field}'")
    endif()
    set("${output_variable}" "${value}" PARENT_SCOPE)
endfunction()

foreach(_redbolt_native_module IN LISTS REDBOLT_NATIVE_MODULES)
    redbolt_native_module_value(
        "${_redbolt_native_module}" LIBRARY_NAME _redbolt_native_library_name)
    redbolt_native_module_value(
        "${_redbolt_native_module}" TARGET _redbolt_native_target)
    redbolt_native_module_value(
        "${_redbolt_native_module}" MANAGED_SELECTOR _redbolt_native_selector)
    if(_redbolt_native_library_name STREQUAL ""
            OR _redbolt_native_target STREQUAL ""
            OR _redbolt_native_selector STREQUAL "")
        message(FATAL_ERROR "Incomplete native module '${_redbolt_native_module}'")
    endif()
endforeach()
