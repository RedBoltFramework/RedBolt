set(NO_MEDIA ON CACHE BOOL "Disable libdatachannel media transport" FORCE)
set(NO_EXAMPLES ON CACHE BOOL "Disable libdatachannel examples" FORCE)
set(NO_TESTS ON CACHE BOOL "Disable libdatachannel tests" FORCE)
set(sctp_werror OFF CACHE BOOL "Disable usrsctp warnings as errors" FORCE)

if(EMSCRIPTEN)
    add_library(datachannel-static INTERFACE)
else()
    set(CMAKE_POLICY_VERSION_MINIMUM 3.25)
    add_subdirectory(libdatachannel EXCLUDE_FROM_ALL)
    target_compile_options(juice PRIVATE
        "$<$<COMPILE_LANG_AND_ID:C,GNU,Clang,AppleClang>:-std=gnu11>")
    target_compile_options(juice-static PRIVATE
        "$<$<COMPILE_LANG_AND_ID:C,GNU,Clang,AppleClang>:-std=gnu11>")
endif()

set(REDBOLT_LIBDATACHANNEL_INCLUDE_DIR
    "${REDBOLT_EXTERN_ROOT}/libdatachannel/include"
    CACHE INTERNAL "libdatachannel include directory")
set(REDBOLT_LIBDATACHANNEL_LIBRARY
    datachannel-static
    CACHE INTERNAL "libdatachannel static target")
