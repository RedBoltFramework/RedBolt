set(REDBOLT_IMGUI_SOURCE_ROOT "${REDBOLT_EXTERN_ROOT}/imgui" CACHE INTERNAL "Dear ImGui source root")
redbolt_require_path("${REDBOLT_IMGUI_SOURCE_ROOT}/imgui.h" "Dear ImGui source headers")

add_library(ImGui ${REDBOLT_EXTERNAL_LIBRARY_TYPE}
    "${REDBOLT_IMGUI_SOURCE_ROOT}/imgui.cpp"
    "${REDBOLT_IMGUI_SOURCE_ROOT}/imgui_draw.cpp"
    "${REDBOLT_IMGUI_SOURCE_ROOT}/imgui_tables.cpp"
    "${REDBOLT_IMGUI_SOURCE_ROOT}/imgui_widgets.cpp"
    "${REDBOLT_IMGUI_SOURCE_ROOT}/imgui_demo.cpp")
target_include_directories(ImGui PUBLIC
    "${REDBOLT_IMGUI_SOURCE_ROOT}"
    "${REDBOLT_IMGUI_SOURCE_ROOT}/backends")
target_compile_definitions(ImGui PUBLIC IMGUI_DEFINE_MATH_OPERATORS=1)

add_library(ImGuiSdl2 ${REDBOLT_EXTERNAL_LIBRARY_TYPE}
    "${REDBOLT_IMGUI_SOURCE_ROOT}/backends/imgui_impl_sdl2.cpp")
target_include_directories(ImGuiSdl2 PUBLIC
    "${REDBOLT_IMGUI_SOURCE_ROOT}"
    "${REDBOLT_IMGUI_SOURCE_ROOT}/backends")
target_link_libraries(ImGuiSdl2 PUBLIC ImGui SDL2::SDL2)
