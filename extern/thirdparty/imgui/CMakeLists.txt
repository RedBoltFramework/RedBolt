project(ImGui)

set(IMGUI_SOURCES
    imgui.cpp
    imgui_draw.cpp
    imgui_tables.cpp
    imgui_widgets.cpp
    imgui_demo.cpp)

set(IMGUI_SDL2_SOURCES
    backends/imgui_impl_sdl2.cpp)

add_library(ImGui STATIC ${IMGUI_SOURCES})
target_include_directories(ImGui PUBLIC
    $<BUILD_INTERFACE:${CMAKE_CURRENT_SOURCE_DIR}>
    $<BUILD_INTERFACE:${CMAKE_CURRENT_SOURCE_DIR}/backends>)
target_compile_definitions(ImGui PUBLIC IMGUI_DEFINE_MATH_OPERATORS=1)

add_library(ImGuiSdl2 STATIC ${IMGUI_SDL2_SOURCES})
target_include_directories(ImGuiSdl2 PUBLIC
    $<BUILD_INTERFACE:${CMAKE_CURRENT_SOURCE_DIR}>
    $<BUILD_INTERFACE:${CMAKE_CURRENT_SOURCE_DIR}/backends>)
target_link_libraries(ImGuiSdl2 PUBLIC ImGui SDL2::SDL2)
