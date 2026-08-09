if(NOT EMSCRIPTEN)
    redbolt_require_path("${REDBOLT_EXTERN_ROOT}/openssl" "OpenSSL source tree")

    set(REDBOLT_OPENSSL_SOURCE_DIR
        "${REDBOLT_EXTERN_ROOT}/openssl"
        CACHE INTERNAL "OpenSSL source directory")
    list(PREPEND CMAKE_MODULE_PATH "${CMAKE_SOURCE_DIR}/cmake/Modules")
    add_subdirectory(
        "${CMAKE_SOURCE_DIR}/cmake/OpenSSL"
        "${CMAKE_BINARY_DIR}/deps/OpenSSL"
        EXCLUDE_FROM_ALL)
endif()
