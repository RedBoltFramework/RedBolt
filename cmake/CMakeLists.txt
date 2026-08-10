find_program(DOTNET_EXECUTABLE dotnet REQUIRED)

function(redbolt_require_path path description)
    if(NOT EXISTS "${path}")
        message(FATAL_ERROR "${description} does not exist: ${path}")
    endif()
endfunction()

include(RedBoltSharpCpp.cmake)
