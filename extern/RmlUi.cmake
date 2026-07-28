set(REDBOLT_RMLUI_SOURCE_ROOT "${REDBOLT_EXTERN_ROOT}/RmlUi" CACHE INTERNAL "RmlUi source root")
redbolt_require_path("${REDBOLT_RMLUI_SOURCE_ROOT}/Include/RmlUi/Core.h" "RmlUi source headers")

set(RMLUI_SAMPLES OFF CACHE BOOL "Build RmlUi samples" FORCE)
set(RMLUI_FONT_ENGINE "none" CACHE STRING "RmlUi font engine" FORCE)
set(RMLUI_LUA_BINDINGS OFF CACHE BOOL "Build RmlUi Lua bindings" FORCE)
set(RMLUI_LOTTIE_PLUGIN OFF CACHE BOOL "Build RmlUi Lottie plugin" FORCE)
set(RMLUI_SVG_PLUGIN OFF CACHE BOOL "Build RmlUi SVG plugin" FORCE)
set(RMLUI_COMPILER_OPTIONS OFF CACHE BOOL "Enable RmlUi compiler options" FORCE)

add_subdirectory("${REDBOLT_RMLUI_SOURCE_ROOT}" "${CMAKE_BINARY_DIR}/deps/RmlUi" EXCLUDE_FROM_ALL)
