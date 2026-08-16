# Copyright (c) 2026 RedBolt and contributors.
# This work is licensed under the terms of the MIT license.
# For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

if(NOT "$ENV{REDBOLT_MSBUILD_BUILD}" STREQUAL "1")
    message(FATAL_ERROR "RedBolt CMake builds must be executed through the MSBuild build system")
endif()
