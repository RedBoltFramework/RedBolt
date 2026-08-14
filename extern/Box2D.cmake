set(BOX2D_COMPILE_WARNING_AS_ERROR OFF CACHE BOOL "Disable Box2D warnings as errors" FORCE)
set(BOX2D_DISABLE_SIMD OFF CACHE BOOL "Use Box2D portable SIMD" FORCE)
set(BOX2D_AVX2 OFF CACHE BOOL "Disable Box2D AVX2" FORCE)

set(_redbolt_saved_build_shared_libs "${BUILD_SHARED_LIBS}")
if(ANDROID)
    set(BUILD_SHARED_LIBS OFF)
endif()
CPMAddPackage(
    NAME Box2D
    GIT_REPOSITORY https://github.com/erincatto/box2d.git
    GIT_TAG 8c661469c9507d3ad6fbd2fea3f1aa71669c2fe3
    CUSTOM_CACHE_KEY "."
    EXCLUDE_FROM_ALL YES)
set(BUILD_SHARED_LIBS "${_redbolt_saved_build_shared_libs}")

set(REDBOLT_BOX2D_SOURCE_ROOT "${Box2D_SOURCE_DIR}" CACHE INTERNAL "Box2D source root")
redbolt_require_path("${REDBOLT_BOX2D_SOURCE_ROOT}/include/box2d/box2d.h" "Box2D public header")
set(REDBOLT_BOX2D_INCLUDE_DIR "${REDBOLT_BOX2D_SOURCE_ROOT}/include" CACHE INTERNAL "Box2D include directory")
set(REDBOLT_BOX2D_LIBRARY box2d CACHE INTERNAL "Box2D target")
