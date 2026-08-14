if(EMSCRIPTEN)
    set(_redbolt_native_library_type STATIC)
else()
    set(_redbolt_native_library_type SHARED)
endif()

set(_redbolt_native_common_include_dirs
    "${REDBOLT_STB_INCLUDE_DIR}"
    "${REDBOLT_TINYGLTF_INCLUDE_DIR}"
    "${REDBOLT_LZ4_INCLUDE_DIR}"
    "${REDBOLT_HARFBUZZ_SOURCE_ROOT}/src"
    "${REDBOLT_RMLUI_SOURCE_ROOT}/Include"
    "${REDBOLT_WEBP_INCLUDE_DIR}"
    "${REDBOLT_XATLAS_INCLUDE_DIR}"
    "${REDBOLT_XATLAS_GENERATED_DIR}"
    "${REDBOLT_EMBREE_INCLUDE_DIR}"
    "${REDBOLT_EMBREE_BUILD_INCLUDE_DIR}"
    "${REDBOLT_GLSLANG_SOURCE_ROOT}"
    "${REDBOLT_SPIRV_CROSS_SOURCE_ROOT}"
    "${REDBOLT_SPIRV_TOOLS_SOURCE_ROOT}/include"
    "${REDBOLT_BOX2D_INCLUDE_DIR}"
    "${REDBOLT_JOLT_INCLUDE_DIR}"
    "${REDBOLT_LIBDATACHANNEL_INCLUDE_DIR}")

set(_redbolt_generated_binding_sources
    "${REDBOLT_SDL_BINDINGS_CPP}"
    "${REDBOLT_DILIGENT_BINDINGS_CPP}"
    "${REDBOLT_IMGUI_BINDINGS_CPP}"
    "${REDBOLT_STB_BINDINGS_CPP}"
    "${REDBOLT_TINYGLTF_BINDINGS_CPP}"
    "${REDBOLT_LZ4_BINDINGS_CPP}"
    "${REDBOLT_HARFBUZZ_BINDINGS_CPP}"
    "${REDBOLT_RMLUI_BINDINGS_CPP}"
    "${REDBOLT_WEBP_BINDINGS_CPP}"
    "${REDBOLT_XATLAS_BINDINGS_CPP}"
    "${REDBOLT_EMBREE_BINDINGS_CPP}"
    "${REDBOLT_GLSLANG_BINDINGS_CPP}"
    "${REDBOLT_SPIRV_CROSS_BINDINGS_CPP}"
    "${REDBOLT_SPIRV_TOOLS_BINDINGS_CPP}"
    "${REDBOLT_SHADER_COMPILER_BINDINGS_CPP}"
    "${REDBOLT_BOX2D_BINDINGS_CPP}"
    "${REDBOLT_JOLT_BINDINGS_CPP}"
    "${REDBOLT_LIBDATACHANNEL_BINDINGS_CPP}")
set_source_files_properties(${_redbolt_generated_binding_sources}
    PROPERTIES GENERATED TRUE)

function(redbolt_add_native_wrapper)
    set(options)
    set(oneValueArgs MODULE TARGET OUTPUT_NAME GENERATION_TARGET)
    set(multiValueArgs
        SOURCES
        OWNED_LIBRARIES
        WRAPPER_DEPENDENCIES
        LINK_LIBRARIES
        COMPILE_DEFINITIONS
        INCLUDE_DIRS)
    cmake_parse_arguments(ARG "${options}" "${oneValueArgs}" "${multiValueArgs}" ${ARGN})

    foreach(required MODULE TARGET OUTPUT_NAME GENERATION_TARGET)
        if(NOT ARG_${required})
            message(FATAL_ERROR "redbolt_add_native_wrapper missing ${required}")
        endif()
    endforeach()

    foreach(_redbolt_unique_argument IN ITEMS OWNED_LIBRARIES WRAPPER_DEPENDENCIES)
        set(_redbolt_argument_values ${ARG_${_redbolt_unique_argument}})
        list(LENGTH _redbolt_argument_values _redbolt_argument_length)
        list(REMOVE_DUPLICATES _redbolt_argument_values)
        list(LENGTH _redbolt_argument_values _redbolt_unique_argument_length)
        if(NOT _redbolt_argument_length EQUAL _redbolt_unique_argument_length)
            message(FATAL_ERROR
                "${ARG_MODULE} has duplicate ${_redbolt_unique_argument} entries")
        endif()
    endforeach()

    redbolt_collect_owned_static_targets(
        _redbolt_owned_static_targets ${ARG_OWNED_LIBRARIES})
    foreach(_redbolt_owned_static_target IN LISTS _redbolt_owned_static_targets)
        get_property(_redbolt_archive_owner GLOBAL PROPERTY
            "REDBOLT_ARCHIVE_OWNER_${_redbolt_owned_static_target}")
        if(_redbolt_archive_owner
                AND NOT "${_redbolt_archive_owner}" STREQUAL "${ARG_MODULE}")
            message(FATAL_ERROR
                "Static archive '${_redbolt_owned_static_target}' is owned by both ${_redbolt_archive_owner} and ${ARG_MODULE}")
        endif()
        set_property(GLOBAL PROPERTY
            "REDBOLT_ARCHIVE_OWNER_${_redbolt_owned_static_target}" "${ARG_MODULE}")
        get_target_property(_redbolt_owned_imported
            "${_redbolt_owned_static_target}" IMPORTED)
        if(NOT _redbolt_owned_imported)
            set_target_properties("${_redbolt_owned_static_target}" PROPERTIES
                POSITION_INDEPENDENT_CODE ON
                C_VISIBILITY_PRESET default
                CXX_VISIBILITY_PRESET default
                VISIBILITY_INLINES_HIDDEN OFF)
        endif()
    endforeach()

    set(_redbolt_wrapper_dependency_modules)
    foreach(_redbolt_wrapper_dependency IN LISTS ARG_WRAPPER_DEPENDENCIES)
        if(NOT TARGET "${_redbolt_wrapper_dependency}")
            message(FATAL_ERROR
                "${ARG_MODULE} wrapper dependency '${_redbolt_wrapper_dependency}' does not exist")
        endif()
        get_target_property(_redbolt_wrapper_dependency_module
            "${_redbolt_wrapper_dependency}" REDBOLT_NATIVE_MODULE)
        if(NOT _redbolt_wrapper_dependency_module)
            message(FATAL_ERROR
                "${ARG_MODULE} dependency '${_redbolt_wrapper_dependency}' is not a Native wrapper")
        endif()
        list(APPEND _redbolt_wrapper_dependency_modules
            "${_redbolt_wrapper_dependency_module}")
    endforeach()
    set(_redbolt_declared_wrapper_dependencies
        ${REDBOLT_${ARG_MODULE}_NATIVE_REQUIRES})
    list(SORT _redbolt_wrapper_dependency_modules)
    list(SORT _redbolt_declared_wrapper_dependencies)
    if(NOT "${_redbolt_wrapper_dependency_modules}" STREQUAL
            "${_redbolt_declared_wrapper_dependencies}")
        message(FATAL_ERROR
            "${ARG_MODULE} wrapper dependencies do not match its registry requirements: '${_redbolt_wrapper_dependency_modules}' != '${_redbolt_declared_wrapper_dependencies}'")
    endif()

    add_library(${ARG_TARGET} ${_redbolt_native_library_type} ${ARG_SOURCES})
    set_target_properties(${ARG_TARGET} PROPERTIES
        REDBOLT_NATIVE_WRAPPER TRUE
        REDBOLT_NATIVE_MODULE "${ARG_MODULE}"
        REDBOLT_OWNED_STATIC_TARGETS "${_redbolt_owned_static_targets}"
        REDBOLT_WRAPPER_DEPENDENCY_MODULES "${_redbolt_wrapper_dependency_modules}")
    add_dependencies(${ARG_TARGET}
        redbolt-msbuild-build-guard
        ${ARG_GENERATION_TARGET})
    target_compile_definitions(${ARG_TARGET} PRIVATE
        REDBOLT_NATIVE_BUILD
        ${ARG_COMPILE_DEFINITIONS})
    target_include_directories(${ARG_TARGET} PRIVATE
        ${_redbolt_native_common_include_dirs}
        ${ARG_INCLUDE_DIRS})
    target_compile_options(${ARG_TARGET} PRIVATE
        "$<$<CXX_COMPILER_ID:GNU>:-Wno-attributes>"
        "$<$<CXX_COMPILER_ID:Clang>:-Wno-attributes>")
    if(MINGW)
        target_compile_options(${ARG_TARGET} PRIVATE
            -Wa,-mbig-obj
            -include cstdint)
    endif()
    if(MINGW AND ARG_MODULE IN_LIST REDBOLT_ARCHIVE_EXPORT_MODULES)
        target_link_options(${ARG_TARGET} PRIVATE
            LINKER:--export-all-symbols)
    endif()
    target_link_libraries(${ARG_TARGET} PRIVATE
        SharpCpp::NativeSupport
        ${ARG_WRAPPER_DEPENDENCIES}
        ${ARG_LINK_LIBRARIES})
    if(_redbolt_owned_static_targets)
        string(JOIN "," _redbolt_owned_static_targets_csv
            ${_redbolt_owned_static_targets})
        target_link_libraries(${ARG_TARGET} PRIVATE
            "$<LINK_LIBRARY:WHOLE_ARCHIVE,${_redbolt_owned_static_targets_csv}>")
        set_property(TARGET ${ARG_TARGET} PROPERTY LINK_LIBRARY_OVERRIDE
            "WHOLE_ARCHIVE,${_redbolt_owned_static_targets_csv}")
    endif()
    set_target_properties(${ARG_TARGET} PROPERTIES
        OUTPUT_NAME "${ARG_OUTPUT_NAME}"
        ARCHIVE_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/lib/$<CONFIG>"
        BUILD_RPATH "$ORIGIN"
        INSTALL_RPATH "$ORIGIN"
        INSTALL_RPATH_USE_LINK_PATH OFF
        BUILD_WITH_INSTALL_RPATH TRUE
        C_VISIBILITY_PRESET default
        CXX_VISIBILITY_PRESET default
        VISIBILITY_INLINES_HIDDEN OFF
        WINDOWS_EXPORT_ALL_SYMBOLS ON)
    if(WIN32)
        set_target_properties(${ARG_TARGET} PROPERTIES PREFIX "")
    endif()
endfunction()

function(redbolt_canonical_target input_target output_variable)
    get_target_property(_redbolt_aliased_target "${input_target}" ALIASED_TARGET)
    if(_redbolt_aliased_target)
        set("${output_variable}" "${_redbolt_aliased_target}" PARENT_SCOPE)
    else()
        set("${output_variable}" "${input_target}" PARENT_SCOPE)
    endif()
endfunction()

function(redbolt_collect_owned_static_targets output_variable)
    set(_redbolt_target_queue ${ARGN})
    set(_redbolt_visited_targets)
    set(_redbolt_static_targets)
    while(_redbolt_target_queue)
        list(POP_FRONT _redbolt_target_queue _redbolt_queued_target)
        if(NOT TARGET "${_redbolt_queued_target}")
            message(FATAL_ERROR
                "Owned library target '${_redbolt_queued_target}' does not exist")
        endif()
        redbolt_canonical_target(
            "${_redbolt_queued_target}" _redbolt_current_target)
        if(_redbolt_current_target IN_LIST _redbolt_visited_targets)
            continue()
        endif()
        list(APPEND _redbolt_visited_targets "${_redbolt_current_target}")

        get_target_property(_redbolt_is_wrapper
            "${_redbolt_current_target}" REDBOLT_NATIVE_WRAPPER)
        if(_redbolt_is_wrapper)
            continue()
        endif()
        get_target_property(_redbolt_imported
            "${_redbolt_current_target}" IMPORTED)
        get_target_property(_redbolt_target_type
            "${_redbolt_current_target}" TYPE)
        if((_redbolt_target_type STREQUAL "SHARED_LIBRARY"
                OR _redbolt_target_type STREQUAL "MODULE_LIBRARY")
                AND NOT _redbolt_imported)
            message(FATAL_ERROR
                "Owned closure contains project shared library '${_redbolt_current_target}'")
        endif()
        if(_redbolt_target_type STREQUAL "STATIC_LIBRARY" AND NOT _redbolt_imported)
            list(APPEND _redbolt_static_targets "${_redbolt_current_target}")
        endif()

        foreach(_redbolt_link_property IN ITEMS
                LINK_LIBRARIES INTERFACE_LINK_LIBRARIES)
            get_target_property(_redbolt_link_items
                "${_redbolt_current_target}" "${_redbolt_link_property}")
            if(NOT _redbolt_link_items OR _redbolt_link_items MATCHES "-NOTFOUND$")
                continue()
            endif()
            foreach(_redbolt_link_item IN LISTS _redbolt_link_items)
                string(REGEX MATCHALL "[A-Za-z0-9_.:+-]+"
                    _redbolt_link_candidates "${_redbolt_link_item}")
                foreach(_redbolt_link_candidate IN LISTS _redbolt_link_candidates)
                    if(TARGET "${_redbolt_link_candidate}")
                        list(APPEND _redbolt_target_queue
                            "${_redbolt_link_candidate}")
                    endif()
                endforeach()
            endforeach()
        endforeach()
    endwhile()
    list(REMOVE_DUPLICATES _redbolt_static_targets)
    set("${output_variable}" "${_redbolt_static_targets}" PARENT_SCOPE)
endfunction()

function(redbolt_replace_link_target target old_target new_target)
    if(NOT TARGET "${target}")
        message(FATAL_ERROR "Link interface target '${target}' does not exist")
    endif()
    foreach(_redbolt_link_property IN ITEMS LINK_LIBRARIES INTERFACE_LINK_LIBRARIES)
        get_target_property(_redbolt_link_items "${target}" "${_redbolt_link_property}")
        if(NOT _redbolt_link_items OR _redbolt_link_items MATCHES "-NOTFOUND$")
            continue()
        endif()
        set(_redbolt_updated_link_items)
        foreach(_redbolt_link_item IN LISTS _redbolt_link_items)
            if(_redbolt_link_item STREQUAL "${old_target}")
                set(_redbolt_link_item "${new_target}")
            elseif(_redbolt_link_item STREQUAL "$<LINK_ONLY:${old_target}>")
                set(_redbolt_link_item "$<LINK_ONLY:${new_target}>")
            elseif(_redbolt_link_item STREQUAL "$<BUILD_INTERFACE:${old_target}>")
                set(_redbolt_link_item "$<BUILD_INTERFACE:${new_target}>")
            endif()
            list(APPEND _redbolt_updated_link_items "${_redbolt_link_item}")
        endforeach()
        list(REMOVE_DUPLICATES _redbolt_updated_link_items)
        set_property(TARGET "${target}" PROPERTY "${_redbolt_link_property}"
            "${_redbolt_updated_link_items}")
    endforeach()
endfunction()

function(redbolt_preserve_target_usage_requirements consumer)
    foreach(_redbolt_usage_target IN LISTS ARGN)
        target_include_directories("${consumer}" PRIVATE
            "$<TARGET_PROPERTY:${_redbolt_usage_target},INTERFACE_INCLUDE_DIRECTORIES>")
        target_compile_definitions("${consumer}" PRIVATE
            "$<TARGET_PROPERTY:${_redbolt_usage_target},INTERFACE_COMPILE_DEFINITIONS>")
        target_compile_options("${consumer}" PRIVATE
            "$<TARGET_PROPERTY:${_redbolt_usage_target},INTERFACE_COMPILE_OPTIONS>")
    endforeach()
endfunction()

redbolt_add_native_wrapper(
    MODULE SDL
    TARGET "${REDBOLT_SDL_NATIVE_TARGET}"
    OUTPUT_NAME "${REDBOLT_SDL_NATIVE_LIBRARY_NAME}"
    GENERATION_TARGET redbolt-generate-sdl-bindings
    COMPILE_DEFINITIONS "$<$<BOOL:${MINGW}>:SDL_STATIC=1>"
    SOURCES "${REDBOLT_SDL_BINDINGS_CPP}"
    OWNED_LIBRARIES "${REDBOLT_SDL_LIBRARY}" SDL2_image)

redbolt_add_native_wrapper(
    MODULE HARFBUZZ
    TARGET "${REDBOLT_HARFBUZZ_NATIVE_TARGET}"
    OUTPUT_NAME "${REDBOLT_HARFBUZZ_NATIVE_LIBRARY_NAME}"
    GENERATION_TARGET redbolt-generate-harfbuzz-bindings
    SOURCES "${REDBOLT_HARFBUZZ_BINDINGS_CPP}"
    OWNED_LIBRARIES harfbuzz harfbuzz-raster harfbuzz-gpu)

redbolt_add_native_wrapper(
    MODULE SPIRV_TOOLS
    TARGET "${REDBOLT_SPIRV_TOOLS_NATIVE_TARGET}"
    OUTPUT_NAME "${REDBOLT_SPIRV_TOOLS_NATIVE_LIBRARY_NAME}"
    GENERATION_TARGET redbolt-generate-spirv-tools-bindings
    SOURCES "${REDBOLT_SPIRV_TOOLS_BINDINGS_CPP}"
    OWNED_LIBRARIES
        "${REDBOLT_SPIRV_TOOLS_TARGET}"
        SPIRV-Tools-opt)

foreach(_redbolt_glslang_target IN ITEMS glslang SPIRV)
    redbolt_preserve_target_usage_requirements(
        "${_redbolt_glslang_target}"
        SPIRV-Tools-static
        SPIRV-Tools-opt)
    foreach(_redbolt_spirv_tools_target IN ITEMS
            SPIRV-Tools SPIRV-Tools-static SPIRV-Tools-opt)
        redbolt_replace_link_target(
            "${_redbolt_glslang_target}"
            "${_redbolt_spirv_tools_target}"
            "${REDBOLT_SPIRV_TOOLS_NATIVE_TARGET}")
    endforeach()
endforeach()

redbolt_add_native_wrapper(
    MODULE GLSLANG
    TARGET "${REDBOLT_GLSLANG_NATIVE_TARGET}"
    OUTPUT_NAME "${REDBOLT_GLSLANG_NATIVE_LIBRARY_NAME}"
    GENERATION_TARGET redbolt-generate-glslang-bindings
    SOURCES "${REDBOLT_GLSLANG_BINDINGS_CPP}"
    OWNED_LIBRARIES
        glslang
        glslang-default-resource-limits
        SPIRV
    WRAPPER_DEPENDENCIES
        "${REDBOLT_SPIRV_TOOLS_NATIVE_TARGET}")

redbolt_add_native_wrapper(
    MODULE SPIRV_CROSS
    TARGET "${REDBOLT_SPIRV_CROSS_NATIVE_TARGET}"
    OUTPUT_NAME "${REDBOLT_SPIRV_CROSS_NATIVE_LIBRARY_NAME}"
    GENERATION_TARGET redbolt-generate-spirv-cross-bindings
    SOURCES "${REDBOLT_SPIRV_CROSS_BINDINGS_CPP}"
    OWNED_LIBRARIES "${REDBOLT_SPIRV_CROSS_TARGET}")

redbolt_preserve_target_usage_requirements(
    Diligent-ShaderTools
    SPIRV-Tools-opt
    glslang
    SPIRV
    spirv-cross-core
    spirv-cross-glsl)
foreach(_redbolt_shader_dependency IN ITEMS
        SPIRV-Tools SPIRV-Tools-static SPIRV-Tools-opt)
    redbolt_replace_link_target(
        Diligent-ShaderTools
        "${_redbolt_shader_dependency}"
        "${REDBOLT_SPIRV_TOOLS_NATIVE_TARGET}")
endforeach()
foreach(_redbolt_shader_dependency IN ITEMS glslang SPIRV)
    redbolt_replace_link_target(
        Diligent-ShaderTools
        "${_redbolt_shader_dependency}"
        "${REDBOLT_GLSLANG_NATIVE_TARGET}")
endforeach()
foreach(_redbolt_shader_dependency IN ITEMS
        spirv-cross-c
        spirv-cross-core
        spirv-cross-glsl
        spirv-cross-hlsl)
    redbolt_replace_link_target(
        Diligent-ShaderTools
        "${_redbolt_shader_dependency}"
        "${REDBOLT_SPIRV_CROSS_NATIVE_TARGET}")
endforeach()

redbolt_add_native_wrapper(
    MODULE DILIGENT
    TARGET "${REDBOLT_DILIGENT_NATIVE_TARGET}"
    OUTPUT_NAME "${REDBOLT_DILIGENT_NATIVE_LIBRARY_NAME}"
    GENERATION_TARGET redbolt-generate-diligent-bindings
    SOURCES
        "${REDBOLT_DILIGENT_BINDINGS_CPP}"
        Diligent/DiligentSupport.cpp
    INCLUDE_DIRS
        Diligent
        "${REDBOLT_DILIGENT_SOURCE_ROOT}/Graphics/GraphicsEngine/include"
        "${REDBOLT_DILIGENT_SOURCE_ROOT}/ThirdParty/glew/include"
        "$<TARGET_PROPERTY:${REDBOLT_SDL_LIBRARY},INTERFACE_INCLUDE_DIRECTORIES>"
    COMPILE_DEFINITIONS
        GLEW_NO_GLU
        ${REDBOLT_DILIGENT_BINDING_DEFINES}
    OWNED_LIBRARIES
        Diligent-GraphicsEngine
        Diligent-GraphicsTools
        ${REDBOLT_DILIGENT_D3D11_TARGET}
        ${REDBOLT_DILIGENT_D3D12_TARGET}
        ${REDBOLT_DILIGENT_OPENGL_TARGET}
        ${REDBOLT_DILIGENT_VULKAN_TARGET}
    WRAPPER_DEPENDENCIES
        "${REDBOLT_SDL_NATIVE_TARGET}"
        "${REDBOLT_GLSLANG_NATIVE_TARGET}"
        "${REDBOLT_SPIRV_CROSS_NATIVE_TARGET}"
        "${REDBOLT_SPIRV_TOOLS_NATIVE_TARGET}"
    LINK_LIBRARIES
        redbolt-diligent-headers
        $<$<BOOL:${ANDROID}>:GLESv3>
        $<$<BOOL:${ANDROID}>:EGL>)

redbolt_preserve_target_usage_requirements(
    ImGui "${REDBOLT_SDL_LIBRARY}")
redbolt_replace_link_target(
    ImGui SDL2::SDL2 "${REDBOLT_SDL_NATIVE_TARGET}")
redbolt_replace_link_target(
    ImGui SDL2::SDL2-static "${REDBOLT_SDL_NATIVE_TARGET}")

redbolt_add_native_wrapper(
    MODULE IMGUI
    TARGET "${REDBOLT_IMGUI_NATIVE_TARGET}"
    OUTPUT_NAME "${REDBOLT_IMGUI_NATIVE_LIBRARY_NAME}"
    GENERATION_TARGET redbolt-generate-dearimgui-bindings
    SOURCES
        "${REDBOLT_IMGUI_BINDINGS_CPP}"
        DearImGui/ImGuiBackend.cpp
        DearImGui/ImGuiHarfBuzzGpu.cpp
        DearImGui/ImGuiDiligentRenderer.cpp
        "${REDBOLT_IMGUIZMO_SOURCE_ROOT}/ImGuizmo.cpp"
    INCLUDE_DIRS
        DearImGui
        HarfBuzz
        "${REDBOLT_IMGUIZMO_SOURCE_ROOT}"
        "$<TARGET_PROPERTY:${REDBOLT_SDL_LIBRARY},INTERFACE_INCLUDE_DIRECTORIES>"
    COMPILE_DEFINITIONS ${REDBOLT_DILIGENT_BINDING_DEFINES}
    OWNED_LIBRARIES ImGui
    WRAPPER_DEPENDENCIES
        "${REDBOLT_SDL_NATIVE_TARGET}"
        "${REDBOLT_DILIGENT_NATIVE_TARGET}"
        "${REDBOLT_HARFBUZZ_NATIVE_TARGET}"
    LINK_LIBRARIES
        redbolt-diligent-headers)

redbolt_add_native_wrapper(
    MODULE STB
    TARGET "${REDBOLT_STB_NATIVE_TARGET}"
    OUTPUT_NAME "${REDBOLT_STB_NATIVE_LIBRARY_NAME}"
    GENERATION_TARGET redbolt-generate-stb-bindings
    SOURCES "${REDBOLT_STB_BINDINGS_CPP}" Stb/StbImpl.cpp)

redbolt_add_native_wrapper(
    MODULE TINYGLTF
    TARGET "${REDBOLT_TINYGLTF_NATIVE_TARGET}"
    OUTPUT_NAME "${REDBOLT_TINYGLTF_NATIVE_LIBRARY_NAME}"
    GENERATION_TARGET redbolt-generate-tinygltf-bindings
    SOURCES "${REDBOLT_TINYGLTF_BINDINGS_CPP}"
    COMPILE_DEFINITIONS TINYGLTF_IMPLEMENTATION TINYGLTF_NO_STB_IMAGE TINYGLTF_NO_STB_IMAGE_WRITE)

redbolt_add_native_wrapper(
    MODULE LZ4
    TARGET "${REDBOLT_LZ4_NATIVE_TARGET}"
    OUTPUT_NAME "${REDBOLT_LZ4_NATIVE_LIBRARY_NAME}"
    GENERATION_TARGET redbolt-generate-lz4-bindings
    SOURCES "${REDBOLT_LZ4_BINDINGS_CPP}"
    OWNED_LIBRARIES "${REDBOLT_LZ4_LIBRARY}"
    COMPILE_DEFINITIONS LZ4_DISABLE_DEPRECATE_WARNINGS)

redbolt_add_native_wrapper(
    MODULE RMLUI
    TARGET "${REDBOLT_RMLUI_NATIVE_TARGET}"
    OUTPUT_NAME "${REDBOLT_RMLUI_NATIVE_LIBRARY_NAME}"
    GENERATION_TARGET redbolt-generate-rmlui-bindings
    SOURCES
        "${REDBOLT_RMLUI_BINDINGS_CPP}"
        RmlUi/RmlHarfBuzzFontEngine.cpp
    INCLUDE_DIRS RmlUi HarfBuzz
    OWNED_LIBRARIES RmlUi::RmlUi
    WRAPPER_DEPENDENCIES "${REDBOLT_HARFBUZZ_NATIVE_TARGET}")

redbolt_add_native_wrapper(
    MODULE WEBP
    TARGET "${REDBOLT_WEBP_NATIVE_TARGET}"
    OUTPUT_NAME "${REDBOLT_WEBP_NATIVE_LIBRARY_NAME}"
    GENERATION_TARGET redbolt-generate-webp-bindings
    SOURCES "${REDBOLT_WEBP_BINDINGS_CPP}"
    OWNED_LIBRARIES "${REDBOLT_WEBP_LIBRARY}")

redbolt_add_native_wrapper(
    MODULE XATLAS
    TARGET "${REDBOLT_XATLAS_NATIVE_TARGET}"
    OUTPUT_NAME "${REDBOLT_XATLAS_NATIVE_LIBRARY_NAME}"
    GENERATION_TARGET redbolt-generate-xatlas-bindings
    SOURCES "${REDBOLT_XATLAS_BINDINGS_CPP}"
    OWNED_LIBRARIES "${REDBOLT_XATLAS_LIBRARY}")

redbolt_add_native_wrapper(
    MODULE EMBREE
    TARGET "${REDBOLT_EMBREE_NATIVE_TARGET}"
    OUTPUT_NAME "${REDBOLT_EMBREE_NATIVE_LIBRARY_NAME}"
    GENERATION_TARGET redbolt-generate-embree-bindings
    SOURCES "${REDBOLT_EMBREE_BINDINGS_CPP}"
    OWNED_LIBRARIES "${REDBOLT_EMBREE_LIBRARY}")

redbolt_add_native_wrapper(
    MODULE SHADER_COMPILER
    TARGET "${REDBOLT_SHADER_COMPILER_NATIVE_TARGET}"
    OUTPUT_NAME "${REDBOLT_SHADER_COMPILER_NATIVE_LIBRARY_NAME}"
    GENERATION_TARGET redbolt-generate-shader-compiler-bindings
    SOURCES
        "${REDBOLT_SHADER_COMPILER_BINDINGS_CPP}"
        ShaderCompiler/ShaderCompilerSupport.cpp
    INCLUDE_DIRS ShaderCompiler)

redbolt_add_native_wrapper(
    MODULE BOX2D
    TARGET "${REDBOLT_BOX2D_NATIVE_TARGET}"
    OUTPUT_NAME "${REDBOLT_BOX2D_NATIVE_LIBRARY_NAME}"
    GENERATION_TARGET redbolt-generate-box2d-bindings
    SOURCES "${REDBOLT_BOX2D_BINDINGS_CPP}"
    OWNED_LIBRARIES ${REDBOLT_BOX2D_LIBRARY})

redbolt_add_native_wrapper(
    MODULE JOLT
    TARGET "${REDBOLT_JOLT_NATIVE_TARGET}"
    OUTPUT_NAME "${REDBOLT_JOLT_NATIVE_LIBRARY_NAME}"
    GENERATION_TARGET redbolt-generate-jolt-bindings
    SOURCES "${REDBOLT_JOLT_BINDINGS_CPP}"
    INCLUDE_DIRS JoltPhysics
    OWNED_LIBRARIES ${REDBOLT_JOLT_LIBRARY})

redbolt_add_native_wrapper(
    MODULE LIBDATACHANNEL
    TARGET "${REDBOLT_LIBDATACHANNEL_NATIVE_TARGET}"
    OUTPUT_NAME "${REDBOLT_LIBDATACHANNEL_NATIVE_LIBRARY_NAME}"
    GENERATION_TARGET redbolt-generate-libdatachannel-bindings
    SOURCES "${REDBOLT_LIBDATACHANNEL_BINDINGS_CPP}"
    OWNED_LIBRARIES ${REDBOLT_LIBDATACHANNEL_LIBRARY})

if(NOT EMSCRIPTEN AND NOT ANDROID)
    if(NOT WIN32)
        find_library(REDBOLT_VULKAN_LIBRARY NAMES vulkan REQUIRED)
        target_link_libraries(${REDBOLT_DILIGENT_NATIVE_TARGET} PRIVATE
            ${REDBOLT_VULKAN_LIBRARY})
    endif()
endif()

if(D3D11_SUPPORTED OR D3D12_SUPPORTED)
    target_link_libraries(${REDBOLT_SHADER_COMPILER_NATIVE_TARGET} PRIVATE d3dcompiler)
endif()

if(MINGW)
    set(_redbolt_mingw_runtime_files)
    if(CMAKE_CXX_COMPILER_ID MATCHES "Clang")
        get_filename_component(_redbolt_mingw_root "${CMAKE_CXX_COMPILER}" DIRECTORY)
        get_filename_component(_redbolt_mingw_root "${_redbolt_mingw_root}" DIRECTORY)
        set(_redbolt_mingw_runtime_names
            libc++.dll
            libunwind.dll
            libwinpthread-1.dll)
    else()
        set(_redbolt_mingw_runtime_names
            libgcc_s_seh-1.dll
            libstdc++-6.dll
            libwinpthread-1.dll)
    endif()
    foreach(_redbolt_mingw_runtime IN LISTS _redbolt_mingw_runtime_names)
        if(CMAKE_CXX_COMPILER_ID MATCHES "Clang")
            set(_redbolt_mingw_runtime_file
                "${_redbolt_mingw_root}/${CMAKE_SYSTEM_PROCESSOR}-w64-mingw32/bin/${_redbolt_mingw_runtime}")
        else()
            execute_process(
                COMMAND "${CMAKE_CXX_COMPILER}" "-print-file-name=${_redbolt_mingw_runtime}"
                OUTPUT_VARIABLE _redbolt_mingw_runtime_file
                OUTPUT_STRIP_TRAILING_WHITESPACE)
            if(NOT EXISTS "${_redbolt_mingw_runtime_file}")
                execute_process(
                    COMMAND "${CMAKE_CXX_COMPILER}" -dumpmachine
                    OUTPUT_VARIABLE _redbolt_mingw_target
                    OUTPUT_STRIP_TRAILING_WHITESPACE)
                get_filename_component(_redbolt_mingw_root "${CMAKE_CXX_COMPILER}" DIRECTORY)
                get_filename_component(_redbolt_mingw_root "${_redbolt_mingw_root}" DIRECTORY)
                set(_redbolt_mingw_runtime_file
                    "${_redbolt_mingw_root}/${_redbolt_mingw_target}/bin/${_redbolt_mingw_runtime}")
            endif()
        endif()
        if(NOT EXISTS "${_redbolt_mingw_runtime_file}")
            message(FATAL_ERROR "MinGW runtime was not found: ${_redbolt_mingw_runtime}")
        endif()
        list(APPEND _redbolt_mingw_runtime_files "${_redbolt_mingw_runtime_file}")
    endforeach()
    add_custom_command(TARGET ${REDBOLT_SDL_NATIVE_TARGET} POST_BUILD
        COMMAND "${CMAKE_COMMAND}" -E copy_if_different
            ${_redbolt_mingw_runtime_files}
            "$<TARGET_FILE_DIR:${REDBOLT_SDL_NATIVE_TARGET}>"
        COMMENT "Copying MinGW runtime libraries"
        VERBATIM)
endif()

if(NOT EMSCRIPTEN)
    set(_redbolt_native_manifest_directory
        "${REDBOLT_GENERATED_ROOT}/native-modules/$<CONFIG>")
    set(_redbolt_runtime_dependency_arguments)
    if(WIN32)
        list(APPEND _redbolt_runtime_dependency_arguments
            "-DCMAKE_GET_RUNTIME_DEPENDENCIES_PLATFORM=windows+pe"
            "-DCMAKE_GET_RUNTIME_DEPENDENCIES_TOOL=objdump"
            "-DCMAKE_GET_RUNTIME_DEPENDENCIES_COMMAND=${CMAKE_OBJDUMP}")
    elseif(ANDROID)
        list(APPEND _redbolt_runtime_dependency_arguments
            "-DCMAKE_GET_RUNTIME_DEPENDENCIES_PLATFORM=linux+elf"
            "-DCMAKE_GET_RUNTIME_DEPENDENCIES_TOOL=objdump"
            "-DCMAKE_GET_RUNTIME_DEPENDENCIES_COMMAND=${CMAKE_OBJDUMP}")
    endif()
    set(_redbolt_native_manifest_files)
    set(_redbolt_previous_native_manifest_file)
    set(_redbolt_native_wrapper_entries)
    foreach(_redbolt_native_module IN LISTS REDBOLT_SELECTED_NATIVE_MODULES)
        redbolt_native_module_value(
            "${_redbolt_native_module}" TARGET _redbolt_wrapper_entry_target)
        list(APPEND _redbolt_native_wrapper_entries
            "${_redbolt_native_module}=$<TARGET_FILE_NAME:${_redbolt_wrapper_entry_target}>")
    endforeach()
    string(REPLACE ";" "," _redbolt_native_wrapper_entries_csv
        "${_redbolt_native_wrapper_entries}")
    string(REPLACE ";" "," _redbolt_toolchain_runtime_files_csv
        "${_redbolt_mingw_runtime_names}")
    foreach(_redbolt_native_module IN LISTS REDBOLT_SELECTED_NATIVE_MODULES)
        redbolt_native_module_value(
            "${_redbolt_native_module}" TARGET _redbolt_manifest_target)
        redbolt_native_module_value(
            "${_redbolt_native_module}" MANAGED_SELECTOR _redbolt_manifest_selector)
        redbolt_native_module_value(
            "${_redbolt_native_module}" LIBRARY_NAME _redbolt_manifest_library_name)
        get_target_property(_redbolt_manifest_requires
            "${_redbolt_manifest_target}" REDBOLT_WRAPPER_DEPENDENCY_MODULES)
        string(REPLACE ";" "," _redbolt_manifest_requires_csv
            "${_redbolt_manifest_requires}")
        redbolt_native_module_requirement_closure(
            "${_redbolt_native_module}" _redbolt_manifest_requirement_closure)
        string(REPLACE ";" "," _redbolt_manifest_requirement_closure_csv
            "${_redbolt_manifest_requirement_closure}")
        set(_redbolt_manifest_file
            "${_redbolt_native_manifest_directory}/${_redbolt_native_module}.json")
        set(_redbolt_manifest_depends
            "${_redbolt_manifest_target}"
            "${CMAKE_SOURCE_DIR}/cmake/WriteNativeModuleManifest.cmake")
        if(_redbolt_previous_native_manifest_file)
            list(APPEND _redbolt_manifest_depends
                "${_redbolt_previous_native_manifest_file}")
        endif()
        add_custom_command(
            OUTPUT "${_redbolt_manifest_file}"
            COMMAND "${CMAKE_COMMAND}"
                "-DREDBOLT_NATIVE_LIBRARY_FILE=$<TARGET_FILE:${_redbolt_manifest_target}>"
                "-DREDBOLT_MANIFEST_FILE=${_redbolt_manifest_file}"
                "-DREDBOLT_MODULE=${_redbolt_native_module}"
                "-DREDBOLT_MANAGED_SELECTOR=${_redbolt_manifest_selector}"
                "-DREDBOLT_NATIVE_LIBRARY_NAME=${_redbolt_manifest_library_name}"
                "-DREDBOLT_REQUIRES=${_redbolt_manifest_requires_csv}"
                "-DREDBOLT_REQUIREMENT_CLOSURE=${_redbolt_manifest_requirement_closure_csv}"
                "-DREDBOLT_NATIVE_WRAPPERS=${_redbolt_native_wrapper_entries_csv}"
                "-DREDBOLT_TOOLCHAIN_RUNTIME_FILES=${_redbolt_toolchain_runtime_files_csv}"
                ${_redbolt_runtime_dependency_arguments}
                -P "${CMAKE_SOURCE_DIR}/cmake/WriteNativeModuleManifest.cmake"
            DEPENDS ${_redbolt_manifest_depends}
            COMMENT "Writing ${_redbolt_native_module} native module manifest"
            VERBATIM)
        list(APPEND _redbolt_native_manifest_files "${_redbolt_manifest_file}")
        set(_redbolt_previous_native_manifest_file "${_redbolt_manifest_file}")
    endforeach()

    set(_redbolt_native_manifest_index
        "${_redbolt_native_manifest_directory}/index.json")
    add_custom_command(
        OUTPUT "${_redbolt_native_manifest_index}"
        COMMAND "${CMAKE_COMMAND}"
            "-DREDBOLT_MANIFEST_DIRECTORY=${_redbolt_native_manifest_directory}"
            "-DREDBOLT_INDEX_FILE=${_redbolt_native_manifest_index}"
            "-DREDBOLT_MODULES=${REDBOLT_SELECTED_NATIVE_MODULES}"
            -P "${CMAKE_SOURCE_DIR}/cmake/WriteNativeModuleIndex.cmake"
        DEPENDS
            ${_redbolt_native_manifest_files}
            "${CMAKE_SOURCE_DIR}/cmake/WriteNativeModuleIndex.cmake"
        COMMENT "Writing RedBolt native module index"
        VERBATIM)
    add_custom_target(redbolt-native-manifests
        DEPENDS "${_redbolt_native_manifest_index}")
endif()

set(_redbolt_selected_native_targets)
foreach(_redbolt_selected_native_module IN LISTS REDBOLT_SELECTED_NATIVE_MODULES)
    redbolt_native_module_value(
        "${_redbolt_selected_native_module}" TARGET _redbolt_selected_native_target)
    list(APPEND _redbolt_selected_native_targets "${_redbolt_selected_native_target}")
endforeach()

add_custom_target(redbolt-native-all DEPENDS ${_redbolt_selected_native_targets})
if(NOT EMSCRIPTEN)
    add_dependencies(redbolt-native-all redbolt-native-manifests)
endif()

if(ANDROID)
    set(_redbolt_android_java_directory "${REDBOLT_GENERATED_ROOT}/android/java")
    add_custom_target(redbolt-android-java
        COMMAND "${CMAKE_COMMAND}" -E rm -rf "${_redbolt_android_java_directory}"
        COMMAND "${CMAKE_COMMAND}" -E copy_directory
            "${REDBOLT_SDL_SOURCE_ROOT}/android-project/app/src/main/java"
            "${_redbolt_android_java_directory}"
        COMMENT "Staging SDL Android Java sources"
        VERBATIM)
    add_custom_target(redbolt-android-native
        DEPENDS
            redbolt-android-java
            redbolt-native-manifests
            ${_redbolt_selected_native_targets})
endif()
