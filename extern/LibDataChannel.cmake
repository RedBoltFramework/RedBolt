set(NO_MEDIA ON CACHE BOOL "Disable libdatachannel media transport" FORCE)
set(NO_EXAMPLES ON CACHE BOOL "Disable libdatachannel examples" FORCE)
set(NO_TESTS ON CACHE BOOL "Disable libdatachannel tests" FORCE)
set(sctp_werror OFF CACHE BOOL "Disable usrsctp warnings as errors" FORCE)

if(EMSCRIPTEN)
    set(_redbolt_libdatachannel_cpm_args DOWNLOAD_ONLY YES)
else()
    set(CMAKE_POLICY_VERSION_MINIMUM 3.25)
endif()

CPMAddPackage(
    NAME libdatachannel
    GIT_REPOSITORY https://github.com/rbfx/libdatachannel.git
    GIT_TAG 400c57e7fc1e4f4ce866135da028722083517ac9
    CUSTOM_CACHE_KEY "."
    EXCLUDE_FROM_ALL YES
    ${_redbolt_libdatachannel_cpm_args})

if(EMSCRIPTEN)
    add_library(datachannel-static INTERFACE)
else()
    target_compile_options(juice PRIVATE
        "$<$<COMPILE_LANG_AND_ID:C,GNU,Clang,AppleClang>:-std=gnu11>")
    target_compile_options(juice-static PRIVATE
        "$<$<COMPILE_LANG_AND_ID:C,GNU,Clang,AppleClang>:-std=gnu11>")
endif()

set(REDBOLT_LIBDATACHANNEL_INCLUDE_DIR
    "${libdatachannel_SOURCE_DIR}/include"
    CACHE INTERNAL "libdatachannel include directory")
set(REDBOLT_LIBDATACHANNEL_LIBRARY
    datachannel-static
    CACHE INTERNAL "libdatachannel static target")
