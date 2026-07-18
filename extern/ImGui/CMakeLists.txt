set(REDBOLT_IMGUI_SOURCE_ROOT "${REDBOLT_THIRDPARTY_ROOT}/imgui" CACHE INTERNAL "ImGui source root")
redbolt_require_path("${REDBOLT_IMGUI_SOURCE_ROOT}/imgui.h" "ImGui headers")
redbolt_add_external_subdirectory("${REDBOLT_IMGUI_SOURCE_ROOT}" "${CMAKE_BINARY_DIR}/deps/ImGui")
