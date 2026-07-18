redbolt_add_external_subdirectory("${REDBOLT_THIRDPARTY_ROOT}/LZ4" "${CMAKE_BINARY_DIR}/deps/LZ4")
redbolt_add_external_subdirectory("${REDBOLT_THIRDPARTY_ROOT}/WebP" "${CMAKE_BINARY_DIR}/deps/WebP")

set(REDBOLT_LZ4_INCLUDE_DIR "${REDBOLT_THIRDPARTY_ROOT}/LZ4/include/LZ4" CACHE INTERNAL "RedBolt LZ4 include directory")
set(REDBOLT_LZ4_LIBRARY LZ4 CACHE INTERNAL "RedBolt LZ4 target")
set(REDBOLT_WEBP_INCLUDE_DIR "${REDBOLT_THIRDPARTY_ROOT}/WebP/src" CACHE INTERNAL "RedBolt WebP include directory")
set(REDBOLT_WEBP_LIBRARY WebP CACHE INTERNAL "RedBolt WebP target")
