add_library(redbolt-diligent-support STATIC
    src/DiligentSupport.cpp)
target_link_libraries(redbolt-diligent-support
    PUBLIC
        redbolt-native-support
    PRIVATE
        SDL2::SDL2
        ${REDBOLT_GLEW_TARGET})
target_include_directories(redbolt-diligent-support PRIVATE
    "${CMAKE_SOURCE_DIR}/extern/Diligent/Graphics/GraphicsEngine/include")
set_target_properties(redbolt-diligent-support PROPERTIES
    POSITION_INDEPENDENT_CODE ON)

# ImGui backend library compiled without DILIGENT_SHARP_GEN
add_library(redbolt-imgui-backend STATIC
    src/ImGuiBackend.cpp
    thirdparty/ImGuiDiligentRenderer.cpp)
target_compile_definitions(redbolt-imgui-backend PRIVATE
    REDBOLT_NATIVE_BUILD
    ${REDBOLT_DILIGENT_BINDING_DEFINES})
target_include_directories(redbolt-imgui-backend PRIVATE
    include
    thirdparty
    "${CMAKE_SOURCE_DIR}/extern")
target_link_libraries(redbolt-imgui-backend
    PUBLIC
        Diligent-GraphicsEngine
        ${REDBOLT_DILIGENT_OPENGL_TARGET}
        ImGui)
if(NOT EMSCRIPTEN)
    target_link_libraries(redbolt-imgui-backend PUBLIC ${REDBOLT_DILIGENT_VULKAN_TARGET})
endif()
set_target_properties(redbolt-imgui-backend PROPERTIES
    POSITION_INDEPENDENT_CODE ON)

if(EMSCRIPTEN)
    set(_redbolt_native_library_type STATIC)
else()
    set(_redbolt_native_library_type SHARED)
endif()
add_library(RedBolt.Native ${_redbolt_native_library_type}
    "${REDBOLT_SDL_BINDINGS_CPP}"
    "${REDBOLT_DILIGENT_BINDINGS_CPP}"
    "${REDBOLT_IMGUI_BINDINGS_CPP}"
    "${REDBOLT_STB_BINDINGS_CPP}"
    "${REDBOLT_LZ4_BINDINGS_CPP}"
    "${REDBOLT_HARFBUZZ_BINDINGS_CPP}"
    "${REDBOLT_RMLUI_BINDINGS_CPP}"
    "${REDBOLT_WEBP_BINDINGS_CPP}"
    "${REDBOLT_XATLAS_BINDINGS_CPP}"
    "${REDBOLT_GLSLANG_BINDINGS_CPP}"
    "${REDBOLT_SPIRV_CROSS_BINDINGS_CPP}"
    "${REDBOLT_SPIRV_TOOLS_BINDINGS_CPP}"
    "${REDBOLT_SPIRV_OPTIMIZER_BINDINGS_CPP}"
    "${REDBOLT_SHADER_COMPILER_BINDINGS_CPP}"
    src/RmlHarfBuzzFontEngine.cpp
    src/SpirVOptimizerSupport.cpp
    src/ShaderCompilerSupport.cpp
    src/StbImpl.cpp)

set_source_files_properties(
    "${REDBOLT_SDL_BINDINGS_CPP}"
    "${REDBOLT_DILIGENT_BINDINGS_CPP}"
    "${REDBOLT_IMGUI_BINDINGS_CPP}"
    "${REDBOLT_STB_BINDINGS_CPP}"
    "${REDBOLT_LZ4_BINDINGS_CPP}"
    "${REDBOLT_HARFBUZZ_BINDINGS_CPP}"
    "${REDBOLT_RMLUI_BINDINGS_CPP}"
    "${REDBOLT_WEBP_BINDINGS_CPP}"
    "${REDBOLT_XATLAS_BINDINGS_CPP}"
    "${REDBOLT_GLSLANG_BINDINGS_CPP}"
    "${REDBOLT_SPIRV_CROSS_BINDINGS_CPP}"
    "${REDBOLT_SPIRV_TOOLS_BINDINGS_CPP}"
    PROPERTIES
        GENERATED TRUE)

set_property(
    SOURCE "${REDBOLT_LZ4_BINDINGS_CPP}"
    APPEND
    PROPERTY COMPILE_DEFINITIONS
        LZ4_DISABLE_DEPRECATE_WARNINGS)

add_dependencies(RedBolt.Native redbolt-msbuild-build-guard redbolt-generate-native-bindings)

set_target_properties(RedBolt.Native PROPERTIES
    OUTPUT_NAME "RedBolt.Native"
    ARCHIVE_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/lib/$<CONFIG>"
    BUILD_RPATH "$ORIGIN"
    INSTALL_RPATH "$ORIGIN"
    INSTALL_RPATH_USE_LINK_PATH OFF
    BUILD_WITH_INSTALL_RPATH TRUE)

target_compile_definitions(RedBolt.Native PRIVATE REDBOLT_NATIVE_BUILD)
target_include_directories(RedBolt.Native PRIVATE "${REDBOLT_STB_INCLUDE_DIR}" "${REDBOLT_LZ4_INCLUDE_DIR}" "${REDBOLT_HARFBUZZ_SOURCE_ROOT}/src" "${REDBOLT_RMLUI_SOURCE_ROOT}/Include" "${REDBOLT_WEBP_INCLUDE_DIR}" "${REDBOLT_XATLAS_INCLUDE_DIR}" "${REDBOLT_XATLAS_GENERATED_DIR}" "${REDBOLT_GLSLANG_SOURCE_ROOT}" "${REDBOLT_SPIRV_CROSS_SOURCE_ROOT}" "${REDBOLT_SPIRV_TOOLS_SOURCE_ROOT}/include")
target_compile_options(RedBolt.Native PRIVATE
    "$<$<CXX_COMPILER_ID:GNU>:-Wno-attributes>"
    "$<$<CXX_COMPILER_ID:Clang>:-Wno-attributes>")
target_link_libraries(RedBolt.Native
    PRIVATE
        SDL2::SDL2
        redbolt-sdl2-image
        Diligent-GraphicsEngine
        ${REDBOLT_DILIGENT_OPENGL_TARGET}
        ImGui
        harfbuzz
        harfbuzz-raster
        RmlUi::RmlUi
        redbolt-native-support
        redbolt-diligent-support
        ${REDBOLT_GLEW_TARGET}
        "${REDBOLT_LZ4_LIBRARY}"
        "${REDBOLT_WEBP_LIBRARY}"
        "${REDBOLT_XATLAS_LIBRARY}"
        glslang
        glslang-default-resource-limits
        SPIRV
        ${REDBOLT_SPIRV_CROSS_TARGET}
        ${REDBOLT_SPIRV_TOOLS_TARGET}
        SPIRV-Tools-opt
        -Wl,--whole-archive redbolt-imgui-backend -Wl,--no-whole-archive)

if(NOT EMSCRIPTEN)
    find_library(REDBOLT_VULKAN_LIBRARY NAMES vulkan REQUIRED)
    target_link_libraries(RedBolt.Native PRIVATE
        ${REDBOLT_DILIGENT_VULKAN_TARGET}
        ${REDBOLT_VULKAN_LIBRARY})
endif()

if(D3D11_SUPPORTED OR D3D12_SUPPORTED)
    target_link_libraries(RedBolt.Native PRIVATE d3dcompiler)
endif()
add_library(redbolt-native-support INTERFACE)
target_include_directories(redbolt-native-support INTERFACE
    "${CMAKE_CURRENT_SOURCE_DIR}/include")
target_link_libraries(redbolt-native-support INTERFACE
    SharpCpp::NativeSupport
    redbolt-diligent-headers)
