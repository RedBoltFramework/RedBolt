add_library(redbolt-native-support INTERFACE)
target_include_directories(redbolt-native-support INTERFACE
    "${CMAKE_CURRENT_SOURCE_DIR}/include")
target_link_libraries(redbolt-native-support INTERFACE
    SharpCpp::NativeSupport
    redbolt-diligent-headers)

add_library(redbolt-diligent-support STATIC
    src/DiligentSupport.cpp)
target_compile_definitions(redbolt-diligent-support PRIVATE
    GLEW_NO_GLU)
target_link_libraries(redbolt-diligent-support
    PUBLIC
        redbolt-native-support
    PRIVATE
        SDL2::SDL2
        ${REDBOLT_GLEW_TARGET})
target_include_directories(redbolt-diligent-support PRIVATE
    "${REDBOLT_DILIGENT_SOURCE_ROOT}/Graphics/GraphicsEngine/include"
    "${REDBOLT_DILIGENT_SOURCE_ROOT}/ThirdParty/glew/include")
set_target_properties(redbolt-diligent-support PROPERTIES
    POSITION_INDEPENDENT_CODE ON)

# ImGui backend library compiled without DILIGENT_SHARP_GEN.
add_library(redbolt-imgui-backend STATIC
    src/ImGuiBackend.cpp
    thirdparty/ImGuiHarfBuzzGpu.cpp
    thirdparty/ImGuiDiligentRenderer.cpp)
target_compile_definitions(redbolt-imgui-backend PRIVATE
    REDBOLT_NATIVE_BUILD
    ${REDBOLT_DILIGENT_BINDING_DEFINES})
target_include_directories(redbolt-imgui-backend PRIVATE
    include
    thirdparty)
target_link_libraries(redbolt-imgui-backend
    PUBLIC
        Diligent-GraphicsEngine
        Diligent-GraphicsTools
        ${REDBOLT_DILIGENT_D3D11_TARGET}
        ${REDBOLT_DILIGENT_D3D12_TARGET}
        ${REDBOLT_DILIGENT_OPENGL_TARGET}
        ImGui
        harfbuzz
        harfbuzz-gpu)
if(NOT EMSCRIPTEN)
    target_link_libraries(redbolt-imgui-backend PUBLIC ${REDBOLT_DILIGENT_VULKAN_TARGET})
endif()
set_target_properties(redbolt-imgui-backend PROPERTIES
    POSITION_INDEPENDENT_CODE ON)

add_library(redbolt-imguizmo STATIC
    thirdparty/ImGuizmo/ImGuizmo.cpp)
target_include_directories(redbolt-imguizmo PUBLIC
    thirdparty/ImGuizmo)
target_link_libraries(redbolt-imguizmo PUBLIC
    ImGui)
set_target_properties(redbolt-imguizmo PROPERTIES
    POSITION_INDEPENDENT_CODE ON)

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
    "${REDBOLT_JOLT_BINDINGS_CPP}"
    "${REDBOLT_LIBDATACHANNEL_BINDINGS_CPP}")
set_source_files_properties(${_redbolt_generated_binding_sources}
    PROPERTIES GENERATED TRUE)

function(redbolt_add_native_wrapper)
    set(options)
    set(oneValueArgs MODULE TARGET OUTPUT_NAME GENERATION_TARGET)
    set(multiValueArgs SOURCES LINK_LIBRARIES COMPILE_DEFINITIONS INCLUDE_DIRS)
    cmake_parse_arguments(ARG "${options}" "${oneValueArgs}" "${multiValueArgs}" ${ARGN})

    foreach(required MODULE TARGET OUTPUT_NAME GENERATION_TARGET)
        if(NOT ARG_${required})
            message(FATAL_ERROR "redbolt_add_native_wrapper missing ${required}")
        endif()
    endforeach()

    add_library(${ARG_TARGET} ${_redbolt_native_library_type} ${ARG_SOURCES})
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
    target_link_libraries(${ARG_TARGET} PRIVATE
        redbolt-native-support
        ${ARG_LINK_LIBRARIES})
    set_target_properties(${ARG_TARGET} PROPERTIES
        OUTPUT_NAME "${ARG_OUTPUT_NAME}"
        ARCHIVE_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/lib/$<CONFIG>"
        BUILD_RPATH "$ORIGIN"
        INSTALL_RPATH "$ORIGIN"
        INSTALL_RPATH_USE_LINK_PATH OFF
        BUILD_WITH_INSTALL_RPATH TRUE)
    if(WIN32)
        set_target_properties(${ARG_TARGET} PROPERTIES PREFIX "")
    endif()
endfunction()

redbolt_add_native_wrapper(
    MODULE SDL
    TARGET "${REDBOLT_SDL_NATIVE_TARGET}"
    OUTPUT_NAME "${REDBOLT_SDL_NATIVE_LIBRARY_NAME}"
    GENERATION_TARGET redbolt-generate-sdl-bindings
    COMPILE_DEFINITIONS "$<$<BOOL:${MINGW}>:SDL_STATIC=1>"
    SOURCES "${REDBOLT_SDL_BINDINGS_CPP}"
    LINK_LIBRARIES SDL2::SDL2 redbolt-sdl2-image)

redbolt_add_native_wrapper(
    MODULE DILIGENT
    TARGET "${REDBOLT_DILIGENT_NATIVE_TARGET}"
    OUTPUT_NAME "${REDBOLT_DILIGENT_NATIVE_LIBRARY_NAME}"
    GENERATION_TARGET redbolt-generate-diligent-bindings
    SOURCES "${REDBOLT_DILIGENT_BINDINGS_CPP}"
    LINK_LIBRARIES
        SDL2::SDL2
        Diligent-GraphicsEngine
        ${REDBOLT_DILIGENT_D3D11_TARGET}
        ${REDBOLT_DILIGENT_D3D12_TARGET}
        ${REDBOLT_DILIGENT_OPENGL_TARGET}
        redbolt-native-support
        redbolt-diligent-support
        ${REDBOLT_GLEW_TARGET}
        ${REDBOLT_DILIGENT_VULKAN_TARGET})

redbolt_add_native_wrapper(
    MODULE IMGUI
    TARGET "${REDBOLT_IMGUI_NATIVE_TARGET}"
    OUTPUT_NAME "${REDBOLT_IMGUI_NATIVE_LIBRARY_NAME}"
    GENERATION_TARGET redbolt-generate-dearimgui-bindings
    SOURCES "${REDBOLT_IMGUI_BINDINGS_CPP}"
    LINK_LIBRARIES
        ImGui
        redbolt-imguizmo
        redbolt-imgui-backend
        Diligent-GraphicsEngine
        ${REDBOLT_DILIGENT_OPENGL_TARGET}
        ${REDBOLT_DILIGENT_VULKAN_TARGET})

redbolt_add_native_wrapper(
    MODULE STB
    TARGET "${REDBOLT_STB_NATIVE_TARGET}"
    OUTPUT_NAME "${REDBOLT_STB_NATIVE_LIBRARY_NAME}"
    GENERATION_TARGET redbolt-generate-stb-bindings
    SOURCES "${REDBOLT_STB_BINDINGS_CPP}" src/StbImpl.cpp)

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
    LINK_LIBRARIES "${REDBOLT_LZ4_LIBRARY}"
    COMPILE_DEFINITIONS LZ4_DISABLE_DEPRECATE_WARNINGS)

redbolt_add_native_wrapper(
    MODULE HARFBUZZ
    TARGET "${REDBOLT_HARFBUZZ_NATIVE_TARGET}"
    OUTPUT_NAME "${REDBOLT_HARFBUZZ_NATIVE_LIBRARY_NAME}"
    GENERATION_TARGET redbolt-generate-harfbuzz-bindings
    SOURCES "${REDBOLT_HARFBUZZ_BINDINGS_CPP}"
    LINK_LIBRARIES harfbuzz harfbuzz-raster harfbuzz-gpu)

redbolt_add_native_wrapper(
    MODULE RMLUI
    TARGET "${REDBOLT_RMLUI_NATIVE_TARGET}"
    OUTPUT_NAME "${REDBOLT_RMLUI_NATIVE_LIBRARY_NAME}"
    GENERATION_TARGET redbolt-generate-rmlui-bindings
    SOURCES "${REDBOLT_RMLUI_BINDINGS_CPP}" src/RmlHarfBuzzFontEngine.cpp
    LINK_LIBRARIES
        RmlUi::RmlUi
        redbolt-native-support
        harfbuzz
        harfbuzz-gpu)

redbolt_add_native_wrapper(
    MODULE WEBP
    TARGET "${REDBOLT_WEBP_NATIVE_TARGET}"
    OUTPUT_NAME "${REDBOLT_WEBP_NATIVE_LIBRARY_NAME}"
    GENERATION_TARGET redbolt-generate-webp-bindings
    SOURCES "${REDBOLT_WEBP_BINDINGS_CPP}"
    LINK_LIBRARIES "${REDBOLT_WEBP_LIBRARY}")

redbolt_add_native_wrapper(
    MODULE XATLAS
    TARGET "${REDBOLT_XATLAS_NATIVE_TARGET}"
    OUTPUT_NAME "${REDBOLT_XATLAS_NATIVE_LIBRARY_NAME}"
    GENERATION_TARGET redbolt-generate-xatlas-bindings
    SOURCES "${REDBOLT_XATLAS_BINDINGS_CPP}"
    LINK_LIBRARIES "${REDBOLT_XATLAS_LIBRARY}")

redbolt_add_native_wrapper(
    MODULE EMBREE
    TARGET "${REDBOLT_EMBREE_NATIVE_TARGET}"
    OUTPUT_NAME "${REDBOLT_EMBREE_NATIVE_LIBRARY_NAME}"
    GENERATION_TARGET redbolt-generate-embree-bindings
    SOURCES "${REDBOLT_EMBREE_BINDINGS_CPP}"
    LINK_LIBRARIES "${REDBOLT_EMBREE_LIBRARY}")

redbolt_add_native_wrapper(
    MODULE GLSLANG
    TARGET "${REDBOLT_GLSLANG_NATIVE_TARGET}"
    OUTPUT_NAME "${REDBOLT_GLSLANG_NATIVE_LIBRARY_NAME}"
    GENERATION_TARGET redbolt-generate-glslang-bindings
    SOURCES "${REDBOLT_GLSLANG_BINDINGS_CPP}"
    LINK_LIBRARIES
        glslang
        glslang-default-resource-limits
        SPIRV)

redbolt_add_native_wrapper(
    MODULE SPIRV_CROSS
    TARGET "${REDBOLT_SPIRV_CROSS_NATIVE_TARGET}"
    OUTPUT_NAME "${REDBOLT_SPIRV_CROSS_NATIVE_LIBRARY_NAME}"
    GENERATION_TARGET redbolt-generate-spirv-cross-bindings
    SOURCES "${REDBOLT_SPIRV_CROSS_BINDINGS_CPP}"
    LINK_LIBRARIES "${REDBOLT_SPIRV_CROSS_TARGET}")

redbolt_add_native_wrapper(
    MODULE SPIRV_TOOLS
    TARGET "${REDBOLT_SPIRV_TOOLS_NATIVE_TARGET}"
    OUTPUT_NAME "${REDBOLT_SPIRV_TOOLS_NATIVE_LIBRARY_NAME}"
    GENERATION_TARGET redbolt-generate-spirv-tools-bindings
    SOURCES "${REDBOLT_SPIRV_TOOLS_BINDINGS_CPP}"
    LINK_LIBRARIES
        ${REDBOLT_SPIRV_TOOLS_TARGET}
        SPIRV-Tools-opt)

redbolt_add_native_wrapper(
    MODULE SHADER_COMPILER
    TARGET "${REDBOLT_SHADER_COMPILER_NATIVE_TARGET}"
    OUTPUT_NAME "${REDBOLT_SHADER_COMPILER_NATIVE_LIBRARY_NAME}"
    GENERATION_TARGET redbolt-generate-shader-compiler-bindings
    SOURCES "${REDBOLT_SHADER_COMPILER_BINDINGS_CPP}" src/ShaderCompilerSupport.cpp)

redbolt_add_native_wrapper(
    MODULE JOLT
    TARGET "${REDBOLT_JOLT_NATIVE_TARGET}"
    OUTPUT_NAME "${REDBOLT_JOLT_NATIVE_LIBRARY_NAME}"
    GENERATION_TARGET redbolt-generate-jolt-bindings
    SOURCES "${REDBOLT_JOLT_BINDINGS_CPP}"
    LINK_LIBRARIES ${REDBOLT_JOLT_LIBRARY})

redbolt_add_native_wrapper(
    MODULE LIBDATACHANNEL
    TARGET "${REDBOLT_LIBDATACHANNEL_NATIVE_TARGET}"
    OUTPUT_NAME "${REDBOLT_LIBDATACHANNEL_NATIVE_LIBRARY_NAME}"
    GENERATION_TARGET redbolt-generate-libdatachannel-bindings
    SOURCES "${REDBOLT_LIBDATACHANNEL_BINDINGS_CPP}"
    LINK_LIBRARIES ${REDBOLT_LIBDATACHANNEL_LIBRARY})

if(NOT EMSCRIPTEN)
    if(WIN32)
        set(REDBOLT_VULKAN_LIBRARY volk::volk)
    else()
        find_library(REDBOLT_VULKAN_LIBRARY NAMES vulkan REQUIRED)
    endif()
    target_link_libraries(${REDBOLT_DILIGENT_NATIVE_TARGET} PRIVATE
        ${REDBOLT_VULKAN_LIBRARY})
    target_link_libraries(${REDBOLT_IMGUI_NATIVE_TARGET} PRIVATE
        ${REDBOLT_VULKAN_LIBRARY})
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
    endif()
    set(_redbolt_native_manifest_files)
    set(_redbolt_previous_native_manifest_file)
    foreach(_redbolt_native_module IN LISTS REDBOLT_NATIVE_MODULES)
        redbolt_native_module_value(
            "${_redbolt_native_module}" TARGET _redbolt_manifest_target)
        redbolt_native_module_value(
            "${_redbolt_native_module}" MANAGED_SELECTOR _redbolt_manifest_selector)
        redbolt_native_module_value(
            "${_redbolt_native_module}" LIBRARY_NAME _redbolt_manifest_library_name)
        string(TOUPPER "${_redbolt_native_module}" _redbolt_manifest_module_upper)
        set(_redbolt_manifest_requires
            "${REDBOLT_${_redbolt_manifest_module_upper}_NATIVE_REQUIRES}")
        string(REPLACE ";" "," _redbolt_manifest_requires_csv
            "${_redbolt_manifest_requires}")
        set(_redbolt_manifest_file
            "${_redbolt_native_manifest_directory}/${_redbolt_native_module}.json")
        set(_redbolt_manifest_depends
            "${_redbolt_manifest_target}"
            "${CMAKE_SOURCE_DIR}/cmake/WriteNativeModuleManifest.cmake")
        set(_redbolt_manifest_extra_artifacts)
        if(_redbolt_native_module STREQUAL "DILIGENT")
            foreach(_redbolt_diligent_runtime_target IN ITEMS
                    "${REDBOLT_DILIGENT_D3D11_TARGET}"
                    "${REDBOLT_DILIGENT_D3D12_TARGET}"
                    "${REDBOLT_DILIGENT_OPENGL_TARGET}"
                    "${REDBOLT_DILIGENT_VULKAN_TARGET}")
                if(_redbolt_diligent_runtime_target)
                    list(APPEND _redbolt_manifest_extra_artifacts
                        "$<TARGET_FILE_NAME:${_redbolt_diligent_runtime_target}>")
                    list(APPEND _redbolt_manifest_depends
                        "${_redbolt_diligent_runtime_target}")
                endif()
            endforeach()
        endif()
        string(REPLACE ";" "," _redbolt_manifest_extra_artifacts_csv
            "${_redbolt_manifest_extra_artifacts}")
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
                "-DREDBOLT_EXTRA_ARTIFACTS=${_redbolt_manifest_extra_artifacts_csv}"
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
            -P "${CMAKE_SOURCE_DIR}/cmake/WriteNativeModuleIndex.cmake"
        DEPENDS
            ${_redbolt_native_manifest_files}
            "${CMAKE_SOURCE_DIR}/cmake/WriteNativeModuleIndex.cmake"
        COMMENT "Writing RedBolt native module index"
        VERBATIM)
    add_custom_target(redbolt-native-manifests
        DEPENDS "${_redbolt_native_manifest_index}")
endif()

add_custom_target(redbolt-native-all
    DEPENDS
        ${REDBOLT_SDL_NATIVE_TARGET}
        ${REDBOLT_DILIGENT_NATIVE_TARGET}
        ${REDBOLT_IMGUI_NATIVE_TARGET}
        ${REDBOLT_STB_NATIVE_TARGET}
        ${REDBOLT_TINYGLTF_NATIVE_TARGET}
        ${REDBOLT_LZ4_NATIVE_TARGET}
        ${REDBOLT_HARFBUZZ_NATIVE_TARGET}
        ${REDBOLT_RMLUI_NATIVE_TARGET}
        ${REDBOLT_WEBP_NATIVE_TARGET}
        ${REDBOLT_XATLAS_NATIVE_TARGET}
        ${REDBOLT_EMBREE_NATIVE_TARGET}
        ${REDBOLT_GLSLANG_NATIVE_TARGET}
        ${REDBOLT_SPIRV_CROSS_NATIVE_TARGET}
        ${REDBOLT_SPIRV_TOOLS_NATIVE_TARGET}
        ${REDBOLT_SHADER_COMPILER_NATIVE_TARGET}
        ${REDBOLT_JOLT_NATIVE_TARGET}
        ${REDBOLT_LIBDATACHANNEL_NATIVE_TARGET})
if(NOT EMSCRIPTEN)
    add_dependencies(redbolt-native-all redbolt-native-manifests)
endif()
