set(RMLUI_SAMPLES OFF CACHE BOOL "Build RmlUi samples" FORCE)
set(RMLUI_FONT_ENGINE "none" CACHE STRING "RmlUi font engine" FORCE)
set(RMLUI_THIRDPARTY_CONTAINERS OFF CACHE BOOL "Use RmlUi third-party containers" FORCE)
set(RMLUI_LUA_BINDINGS OFF CACHE BOOL "Build RmlUi Lua bindings" FORCE)
set(RMLUI_LOTTIE_PLUGIN OFF CACHE BOOL "Build RmlUi Lottie plugin" FORCE)
set(RMLUI_SVG_PLUGIN OFF CACHE BOOL "Build RmlUi SVG plugin" FORCE)
set(RMLUI_COMPILER_OPTIONS OFF CACHE BOOL "Enable RmlUi compiler options" FORCE)

CPMAddPackage(
    NAME RmlUi
    GIT_REPOSITORY https://github.com/mikke89/RmlUi.git
    GIT_TAG 6.2
    CUSTOM_CACHE_KEY "."
    EXCLUDE_FROM_ALL YES)
set(REDBOLT_RMLUI_SOURCE_ROOT "${RmlUi_SOURCE_DIR}" CACHE INTERNAL "RmlUi source root")
redbolt_require_path("${REDBOLT_RMLUI_SOURCE_ROOT}/Include/RmlUi/Core.h" "RmlUi source headers")
