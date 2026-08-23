if(TARGET SDL2-static AND NOT TARGET SDL2::SDL2)
    add_library(SDL2::SDL2 ALIAS SDL2-static)
elseif(TARGET SDL2 AND NOT TARGET SDL2::SDL2)
    add_library(SDL2::SDL2 ALIAS SDL2)
endif()
if(TARGET SDL2-static AND NOT TARGET SDL2::SDL2-static)
    add_library(SDL2::SDL2-static ALIAS SDL2-static)
elseif(TARGET SDL2 AND NOT TARGET SDL2::SDL2-static)
    add_library(SDL2::SDL2-static ALIAS SDL2)
endif()
set(SDL2IMAGE_INSTALL OFF CACHE BOOL "" FORCE)
set(SDL2IMAGE_SAMPLES OFF CACHE BOOL "" FORCE)
set(SDL2IMAGE_TESTS OFF CACHE BOOL "" FORCE)
set(SDL2IMAGE_DEPS_SHARED OFF CACHE BOOL "" FORCE)
set(SDL2IMAGE_BACKEND_STB ON CACHE BOOL "" FORCE)
set(SDL2IMAGE_AVIF OFF CACHE BOOL "" FORCE)
set(SDL2IMAGE_JXL OFF CACHE BOOL "" FORCE)
set(SDL2IMAGE_TIF OFF CACHE BOOL "" FORCE)
set(SDL2IMAGE_WEBP OFF CACHE BOOL "" FORCE)
set(_redbolt_saved_build_shared_libs "${BUILD_SHARED_LIBS}")
set(BUILD_SHARED_LIBS OFF)

redbolt_cpm_add_package(
    NAME SDL_image
    GIT_REPOSITORY https://github.com/libsdl-org/SDL_image.git
    GIT_TAG 1fe71e1086e00b73999a8b63ad6afabb8ce43b23
    CUSTOM_CACHE_KEY "."
    GIT_SUBMODULES ":!*"
    EXCLUDE_FROM_ALL YES)
set(BUILD_SHARED_LIBS "${_redbolt_saved_build_shared_libs}")
set(REDBOLT_SDL_IMAGE_SOURCE_ROOT "${SDL_image_SOURCE_DIR}" CACHE INTERNAL "SDL_image source root")
redbolt_require_path("${REDBOLT_SDL_IMAGE_SOURCE_ROOT}/CMakeLists.txt" "SDL_image source")

add_library(redbolt-sdl2-image INTERFACE)
if(MINGW)
    target_compile_definitions(SDL2_image PRIVATE SDL_STATIC=1)
endif()
target_link_libraries(redbolt-sdl2-image INTERFACE SDL2_image)
target_include_directories(redbolt-sdl2-image SYSTEM INTERFACE
    "${REDBOLT_SDL_IMAGE_SOURCE_ROOT}/include")
set(REDBOLT_SDL_IMAGE_INCLUDE_DIRS
    "${REDBOLT_SDL_IMAGE_SOURCE_ROOT}/include"
    CACHE INTERNAL "SDL_image include directories used by RedBolt")
set(REDBOLT_SDL_IMAGE_HEADER
    "${REDBOLT_SDL_IMAGE_SOURCE_ROOT}/include/SDL_image.h"
    CACHE INTERNAL "Primary SDL_image header used by RedBolt")
