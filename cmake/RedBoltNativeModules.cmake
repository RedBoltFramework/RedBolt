# Native wrapper module registry.  The same names are used by the binding
# generator, the CMake targets, and the managed packaging resolver.
set(REDBOLT_NATIVE_MODULES
    SDL
    DILIGENT
    IMGUI
    STB
    TINYGLTF
    LZ4
    HARFBUZZ
    WEBP
    GLSLANG
    SPIRV_CROSS
    SPIRV_TOOLS
    SHADER_COMPILER)

set(REDBOLT_SDL_NATIVE_LIBRARY_NAME "RedBolt.Native.Sdl")
set(REDBOLT_DILIGENT_NATIVE_LIBRARY_NAME "RedBolt.Native.Diligent")
set(REDBOLT_IMGUI_NATIVE_LIBRARY_NAME "RedBolt.Native.DearImGui")
set(REDBOLT_STB_NATIVE_LIBRARY_NAME "RedBolt.Native.Stb")
set(REDBOLT_TINYGLTF_NATIVE_LIBRARY_NAME "RedBolt.Native.TinyGltf")
set(REDBOLT_LZ4_NATIVE_LIBRARY_NAME "RedBolt.Native.Lz4")
set(REDBOLT_HARFBUZZ_NATIVE_LIBRARY_NAME "RedBolt.Native.HarfBuzz")
set(REDBOLT_WEBP_NATIVE_LIBRARY_NAME "RedBolt.Native.WebP")
set(REDBOLT_GLSLANG_NATIVE_LIBRARY_NAME "RedBolt.Native.Glslang")
set(REDBOLT_SPIRV_CROSS_NATIVE_LIBRARY_NAME "RedBolt.Native.SpirVCross")
set(REDBOLT_SPIRV_TOOLS_NATIVE_LIBRARY_NAME "RedBolt.Native.SpirVTools")
set(REDBOLT_SHADER_COMPILER_NATIVE_LIBRARY_NAME "RedBolt.Native.ShaderCompiler")

set(REDBOLT_SDL_NATIVE_TARGET redbolt-native-sdl)
set(REDBOLT_DILIGENT_NATIVE_TARGET redbolt-native-diligent)
set(REDBOLT_IMGUI_NATIVE_TARGET redbolt-native-dearimgui)
set(REDBOLT_STB_NATIVE_TARGET redbolt-native-stb)
set(REDBOLT_TINYGLTF_NATIVE_TARGET redbolt-native-tinygltf)
set(REDBOLT_LZ4_NATIVE_TARGET redbolt-native-lz4)
set(REDBOLT_HARFBUZZ_NATIVE_TARGET redbolt-native-harfbuzz)
set(REDBOLT_WEBP_NATIVE_TARGET redbolt-native-webp)
set(REDBOLT_GLSLANG_NATIVE_TARGET redbolt-native-glslang)
set(REDBOLT_SPIRV_CROSS_NATIVE_TARGET redbolt-native-spirv-cross)
set(REDBOLT_SPIRV_TOOLS_NATIVE_TARGET redbolt-native-spirv-tools)
set(REDBOLT_SHADER_COMPILER_NATIVE_TARGET redbolt-native-shader-compiler)

set(REDBOLT_SDL_NATIVE_MANAGED_SELECTOR "RedBolt.Bindings.Sdl")
set(REDBOLT_DILIGENT_NATIVE_MANAGED_SELECTOR "RedBolt.Bindings.Diligent")
set(REDBOLT_IMGUI_NATIVE_MANAGED_SELECTOR "RedBolt.Bindings.DearImGui")
set(REDBOLT_STB_NATIVE_MANAGED_SELECTOR "RedBolt.Bindings.Stb")
set(REDBOLT_TINYGLTF_NATIVE_MANAGED_SELECTOR "RedBolt.Bindings.TinyGltf")
set(REDBOLT_LZ4_NATIVE_MANAGED_SELECTOR "RedBolt.Bindings.Lz4")
set(REDBOLT_HARFBUZZ_NATIVE_MANAGED_SELECTOR "RedBolt.Bindings.HarfBuzz")
set(REDBOLT_WEBP_NATIVE_MANAGED_SELECTOR "RedBolt.Bindings.WebP")
set(REDBOLT_GLSLANG_NATIVE_MANAGED_SELECTOR "RedBolt.Bindings.Glslang")
set(REDBOLT_SPIRV_CROSS_NATIVE_MANAGED_SELECTOR "RedBolt.Bindings.SpirVCross")
set(REDBOLT_SPIRV_TOOLS_NATIVE_MANAGED_SELECTOR "RedBolt.Bindings.SpirVTools")
set(REDBOLT_SHADER_COMPILER_NATIVE_MANAGED_SELECTOR "RedBolt.Bindings.ShaderCompiler")

set(REDBOLT_SDL_NATIVE_REQUIRES "")
set(REDBOLT_DILIGENT_NATIVE_REQUIRES "SDL;GLSLANG;SPIRV_CROSS;SPIRV_TOOLS")
set(REDBOLT_IMGUI_NATIVE_REQUIRES "SDL;DILIGENT;HARFBUZZ")
set(REDBOLT_STB_NATIVE_REQUIRES "")
set(REDBOLT_TINYGLTF_NATIVE_REQUIRES "")
set(REDBOLT_LZ4_NATIVE_REQUIRES "")
set(REDBOLT_HARFBUZZ_NATIVE_REQUIRES "")
set(REDBOLT_WEBP_NATIVE_REQUIRES "")
set(REDBOLT_GLSLANG_NATIVE_REQUIRES "SPIRV_TOOLS")
set(REDBOLT_SPIRV_CROSS_NATIVE_REQUIRES "")
set(REDBOLT_SPIRV_TOOLS_NATIVE_REQUIRES "")
set(REDBOLT_SHADER_COMPILER_NATIVE_REQUIRES "")

# These owners expose their static library APIs to dependent Native wrappers.
# Diligent uses interface-only calls from DearImGui and does not need archive exports.
set(REDBOLT_ARCHIVE_EXPORT_MODULES
    SDL
    HARFBUZZ
    GLSLANG
    SPIRV_CROSS
    SPIRV_TOOLS)

function(redbolt_native_module_value module field output_variable)
    string(TOUPPER "${module}" module_upper)
    set(value "${REDBOLT_${module_upper}_NATIVE_${field}}")
    if(value STREQUAL "")
        message(FATAL_ERROR "Unknown native module field '${module}.${field}'")
    endif()
    set("${output_variable}" "${value}" PARENT_SCOPE)
endfunction()

function(redbolt_native_module_requirement_closure module output_variable)
    set(_redbolt_requirement_closure)
    set(_redbolt_pending_requirements ${REDBOLT_${module}_NATIVE_REQUIRES})
    while(_redbolt_pending_requirements)
        list(POP_FRONT _redbolt_pending_requirements _redbolt_requirement)
        if(NOT _redbolt_requirement IN_LIST _redbolt_requirement_closure)
            list(APPEND _redbolt_requirement_closure "${_redbolt_requirement}")
            list(APPEND _redbolt_pending_requirements
                ${REDBOLT_${_redbolt_requirement}_NATIVE_REQUIRES})
        endif()
    endwhile()
    set("${output_variable}" "${_redbolt_requirement_closure}" PARENT_SCOPE)
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

set(REDBOLT_ENABLED_NATIVE_MODULES "" CACHE STRING
    "Comma-separated RedBolt native wrapper modules to build")
if(REDBOLT_ENABLED_NATIVE_MODULES STREQUAL "")
    set(REDBOLT_SELECTED_NATIVE_MODULES ${REDBOLT_NATIVE_MODULES})
else()
    string(REPLACE "," ";" REDBOLT_SELECTED_NATIVE_MODULES
        "${REDBOLT_ENABLED_NATIVE_MODULES}")
    list(TRANSFORM REDBOLT_SELECTED_NATIVE_MODULES TOUPPER)
    list(REMOVE_DUPLICATES REDBOLT_SELECTED_NATIVE_MODULES)
    # Migrate existing build directories after these wrappers moved to their
    # external module repositories. Fresh configurations never select them.
    list(REMOVE_ITEM REDBOLT_SELECTED_NATIVE_MODULES RMLUI LIBDATACHANNEL XATLAS EMBREE)
    foreach(_redbolt_selected_native_module IN LISTS REDBOLT_SELECTED_NATIVE_MODULES)
        list(FIND REDBOLT_NATIVE_MODULES
            "${_redbolt_selected_native_module}" _redbolt_selected_native_index)
        if(_redbolt_selected_native_index EQUAL -1)
            message(FATAL_ERROR
                "Unknown RedBolt native module '${_redbolt_selected_native_module}'")
        endif()
    endforeach()
endif()

set(_redbolt_available_native_modules ${REDBOLT_NATIVE_MODULES})

set(_redbolt_required_native_modules ${REDBOLT_SELECTED_NATIVE_MODULES})
while(_redbolt_required_native_modules)
    list(POP_FRONT _redbolt_required_native_modules _redbolt_required_native_module)
    foreach(_redbolt_native_requirement IN LISTS
            REDBOLT_${_redbolt_required_native_module}_NATIVE_REQUIRES)
        if(NOT _redbolt_native_requirement IN_LIST _redbolt_available_native_modules)
            message(FATAL_ERROR
                "Native module '${_redbolt_required_native_module}' requires unavailable module '${_redbolt_native_requirement}'")
        endif()
        if(NOT _redbolt_native_requirement IN_LIST REDBOLT_SELECTED_NATIVE_MODULES)
            list(APPEND REDBOLT_SELECTED_NATIVE_MODULES "${_redbolt_native_requirement}")
            list(APPEND _redbolt_required_native_modules "${_redbolt_native_requirement}")
        endif()
    endforeach()
endwhile()

set(_redbolt_ordered_native_modules)
foreach(_redbolt_native_module IN LISTS REDBOLT_NATIVE_MODULES)
    if(_redbolt_native_module IN_LIST REDBOLT_SELECTED_NATIVE_MODULES)
        list(APPEND _redbolt_ordered_native_modules "${_redbolt_native_module}")
    endif()
endforeach()
set(REDBOLT_SELECTED_NATIVE_MODULES ${_redbolt_ordered_native_modules})
