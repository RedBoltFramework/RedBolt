add_library(redbolt-diligent-support STATIC
    src/DiligentSupport.cpp)
target_link_libraries(redbolt-diligent-support
    PUBLIC
        redbolt-native-support)
set_target_properties(redbolt-diligent-support PROPERTIES
    POSITION_INDEPENDENT_CODE ON)

# ImGui backend library compiled without DILIGENT_SHARP_GEN
add_library(redbolt-imgui-backend STATIC
    src/ImGuiBackend.cpp
    thirdparty/ImGuiDiligentRenderer.cpp)
target_compile_definitions(redbolt-imgui-backend PRIVATE
    REDBOLT_NATIVE_BUILD
    PLATFORM_LINUX=1
    D3D11_SUPPORTED=0
    D3D12_SUPPORTED=0
    GL_SUPPORTED=1
    GLES_SUPPORTED=0
    VULKAN_SUPPORTED=0
    METAL_SUPPORTED=0
    WEBGPU_SUPPORTED=0
    DILIGENT_NO_HLSL=1)
target_include_directories(redbolt-imgui-backend PRIVATE
    include
    thirdparty
    "${CMAKE_SOURCE_DIR}/extern/thirdparty")
target_link_libraries(redbolt-imgui-backend
    PUBLIC
        Diligent-GraphicsEngine
        Diligent-GraphicsEngineOpenGL-static
        ImGui
        ImGuiSdl2)
set_target_properties(redbolt-imgui-backend PROPERTIES
    POSITION_INDEPENDENT_CODE ON)

add_library(RedBolt.Native SHARED
    "${REDBOLT_SDL_BINDINGS_CPP}"
    "${REDBOLT_DILIGENT_BINDINGS_CPP}"
    "${REDBOLT_IMGUI_BINDINGS_CPP}")

set_source_files_properties(
    "${REDBOLT_SDL_BINDINGS_CPP}"
    "${REDBOLT_DILIGENT_BINDINGS_CPP}"
    "${REDBOLT_IMGUI_BINDINGS_CPP}"
    PROPERTIES GENERATED TRUE)

add_dependencies(RedBolt.Native redbolt-generate-native-bindings)

set_target_properties(RedBolt.Native PROPERTIES
    OUTPUT_NAME "RedBolt.Native"
    RUNTIME_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/bin/$<CONFIG>"
    LIBRARY_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/bin/$<CONFIG>"
    ARCHIVE_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/lib/$<CONFIG>")

target_compile_definitions(RedBolt.Native PRIVATE REDBOLT_NATIVE_BUILD)
target_compile_options(RedBolt.Native PRIVATE
    "$<$<CXX_COMPILER_ID:GNU>:-Wno-attributes>"
    "$<$<CXX_COMPILER_ID:Clang>:-Wno-attributes>")
target_link_libraries(RedBolt.Native
    PRIVATE
        SDL2::SDL2
        PkgConfig::REDBOLT_SDL2_IMAGE
        Diligent-GraphicsEngine
        Diligent-GraphicsEngineOpenGL-static
        ImGui
        ImGuiSdl2
        redbolt-native-support
        redbolt-diligent-support
        -Wl,--whole-archive redbolt-imgui-backend -Wl,--no-whole-archive)

add_executable(redbolt-native-smoke-exe
    smoke/RedBoltNativeSmoke.cpp)

set_target_properties(redbolt-native-smoke-exe PROPERTIES
    RUNTIME_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/bin/$<CONFIG>")

target_link_libraries(redbolt-native-smoke-exe PRIVATE SDL2::SDL2 redbolt-native-support)

add_custom_target(redbolt-native-smoke
    COMMAND redbolt-native-smoke-exe
    DEPENDS redbolt-native-smoke-exe RedBolt.Native
    WORKING_DIRECTORY "${CMAKE_BINARY_DIR}/bin/$<CONFIG>"
    COMMENT "Running C++ RedBolt.Native smoke")
