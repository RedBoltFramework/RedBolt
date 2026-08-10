CPMAddPackage(
    NAME xatlas
    GIT_REPOSITORY https://github.com/jpcy/xatlas.git
    GIT_TAG 413be25a3106d4bd894697d2d3e0001881caa97e
    CUSTOM_CACHE_KEY "."
    DOWNLOAD_ONLY YES)
set(REDBOLT_XATLAS_SOURCE_ROOT "${xatlas_SOURCE_DIR}" CACHE INTERNAL "xatlas source root")
set(REDBOLT_XATLAS_INCLUDE_DIR "${REDBOLT_XATLAS_SOURCE_ROOT}" CACHE INTERNAL "xatlas include directory")
set(REDBOLT_XATLAS_HEADER "${REDBOLT_XATLAS_INCLUDE_DIR}/xatlas.h" CACHE INTERNAL "xatlas header")
redbolt_require_path("${REDBOLT_XATLAS_HEADER}" "xatlas header")
redbolt_require_path("${REDBOLT_XATLAS_SOURCE_ROOT}/xatlas.cpp" "xatlas source")

add_library(xatlas STATIC "${REDBOLT_XATLAS_SOURCE_ROOT}/xatlas.cpp")
target_include_directories(xatlas PUBLIC "${REDBOLT_XATLAS_INCLUDE_DIR}")
set_target_properties(xatlas PROPERTIES POSITION_INDEPENDENT_CODE ON)
set(REDBOLT_XATLAS_LIBRARY xatlas CACHE INTERNAL "RedBolt xatlas target")
