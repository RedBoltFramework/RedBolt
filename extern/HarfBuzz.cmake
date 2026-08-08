set(REDBOLT_HARFBUZZ_SOURCE_ROOT "${REDBOLT_EXTERN_ROOT}/harfbuzz" CACHE INTERNAL "HarfBuzz source root")
redbolt_require_path("${REDBOLT_HARFBUZZ_SOURCE_ROOT}/src/hb-raster.h" "HarfBuzz raster headers")
redbolt_require_path("${REDBOLT_HARFBUZZ_SOURCE_ROOT}/src/hb-gpu.h" "HarfBuzz GPU headers")

set(HB_HAVE_FREETYPE OFF CACHE BOOL "Enable HarfBuzz FreeType integration" FORCE)
set(HB_BUILD_UTILS OFF CACHE BOOL "Build HarfBuzz utilities" FORCE)
set(HB_BUILD_SUBSET OFF CACHE BOOL "Build HarfBuzz subset library" FORCE)
set(HB_BUILD_RASTER ON CACHE BOOL "Build HarfBuzz raster library" FORCE)
set(HB_BUILD_VECTOR OFF CACHE BOOL "Build HarfBuzz vector library" FORCE)
set(HB_BUILD_GPU ON CACHE BOOL "Build HarfBuzz GPU library" FORCE)

add_subdirectory("${REDBOLT_HARFBUZZ_SOURCE_ROOT}" "${CMAKE_BINARY_DIR}/deps/harfbuzz" EXCLUDE_FROM_ALL)
