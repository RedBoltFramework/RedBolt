if(NOT "$ENV{REDBOLT_MSBUILD_BUILD}" STREQUAL "1")
    message(FATAL_ERROR "RedBolt CMake builds must be executed through the MSBuild build system")
endif()
