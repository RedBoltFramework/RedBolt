CPMAddPackage(
    NAME imgui
    GIT_REPOSITORY https://github.com/RedBoltFramework/imgui.git
    GIT_TAG docking-string-view
    CUSTOM_CACHE_KEY "."
    PATCHES "${CMAKE_CURRENT_LIST_DIR}/DearImGui.patch"
    DOWNLOAD_ONLY YES)
CPMAddPackage(
    NAME ImGuizmo
    GIT_REPOSITORY https://github.com/cedricguillemet/imguizmo.git
    GIT_TAG 18cef5e031d8c6973d80284c67f60549fafd78c1
    CUSTOM_CACHE_KEY "."
    DOWNLOAD_ONLY YES)
set(REDBOLT_IMGUI_SOURCE_ROOT "${imgui_SOURCE_DIR}" CACHE INTERNAL "Dear ImGui source root")
set(REDBOLT_IMGUIZMO_SOURCE_ROOT "${ImGuizmo_SOURCE_DIR}/src" CACHE INTERNAL "ImGuizmo source root")
redbolt_require_path("${REDBOLT_IMGUI_SOURCE_ROOT}/imgui.h" "Dear ImGui source headers")
redbolt_require_path("${REDBOLT_IMGUIZMO_SOURCE_ROOT}/ImGuizmo.h" "ImGuizmo source headers")

add_library(ImGui ${REDBOLT_EXTERNAL_LIBRARY_TYPE}
    "${REDBOLT_IMGUI_SOURCE_ROOT}/imgui.cpp"
    "${REDBOLT_IMGUI_SOURCE_ROOT}/imgui_draw.cpp"
    "${REDBOLT_IMGUI_SOURCE_ROOT}/imgui_tables.cpp"
    "${REDBOLT_IMGUI_SOURCE_ROOT}/imgui_widgets.cpp"
    "${REDBOLT_IMGUI_SOURCE_ROOT}/imgui_demo.cpp"
    "${REDBOLT_IMGUI_SOURCE_ROOT}/backends/imgui_impl_sdl2.cpp")
target_include_directories(ImGui PUBLIC
    "${REDBOLT_IMGUI_SOURCE_ROOT}"
    "${REDBOLT_IMGUI_SOURCE_ROOT}/backends")
target_compile_definitions(ImGui PUBLIC IMGUI_DEFINE_MATH_OPERATORS=1)
target_link_libraries(ImGui PUBLIC SDL2::SDL2)
