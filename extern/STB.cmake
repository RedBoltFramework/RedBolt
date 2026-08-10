CPMAddPackage(
    NAME STB
    GIT_REPOSITORY https://github.com/nothings/stb.git
    GIT_TAG 31c1ad37456438565541f4919958214b6e762fb4
    CUSTOM_CACHE_KEY "."
    DOWNLOAD_ONLY YES)
set(REDBOLT_STB_SOURCE_ROOT "${STB_SOURCE_DIR}" CACHE INTERNAL "STB source root")
set(REDBOLT_STB_INCLUDE_DIR "${REDBOLT_STB_SOURCE_ROOT}" CACHE INTERNAL "STB include directory")
set(REDBOLT_STB_VORBIS_HEADER "${REDBOLT_STB_INCLUDE_DIR}/stb_vorbis.c" CACHE INTERNAL "stb_vorbis header")
set(REDBOLT_STB_IMAGE_HEADER "${REDBOLT_STB_INCLUDE_DIR}/stb_image.h" CACHE INTERNAL "stb_image header")
set(REDBOLT_STB_IMAGE_WRITE_HEADER "${REDBOLT_STB_INCLUDE_DIR}/stb_image_write.h" CACHE INTERNAL "stb_image_write header")

redbolt_require_path("${REDBOLT_STB_VORBIS_HEADER}" "stb_vorbis header")
redbolt_require_path("${REDBOLT_STB_IMAGE_HEADER}" "stb_image header")
redbolt_require_path("${REDBOLT_STB_IMAGE_WRITE_HEADER}" "stb_image_write header")

add_library(STB INTERFACE)
target_include_directories(STB SYSTEM INTERFACE "${REDBOLT_STB_INCLUDE_DIR}")
