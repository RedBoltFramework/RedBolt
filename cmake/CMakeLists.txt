# Copyright (c) 2026 RedBolt and contributors.
# This work is licensed under the terms of the MIT license.
# For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

find_program(DOTNET_EXECUTABLE dotnet REQUIRED)

function(redbolt_require_path path description)
    if(NOT EXISTS "${path}")
        message(FATAL_ERROR "${description} does not exist: ${path}")
    endif()
endfunction()

include(RedBoltSharpCpp.cmake)
