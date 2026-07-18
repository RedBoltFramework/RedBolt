find_program(DOTNET_EXECUTABLE dotnet REQUIRED)

function(redbolt_require_path path description)
    if(NOT EXISTS "${path}")
        message(FATAL_ERROR "${description} does not exist: ${path}")
    endif()
endfunction()

macro(redbolt_add_external_subdirectory source_dir binary_dir)
    set(_redbolt_saved_c_flags "${CMAKE_C_FLAGS}")
    set(_redbolt_saved_cxx_flags "${CMAKE_CXX_FLAGS}")

    if(MSVC)
        set(CMAKE_C_FLAGS "${CMAKE_C_FLAGS} /W0")
        set(CMAKE_CXX_FLAGS "${CMAKE_CXX_FLAGS} /W0")
    else()
        set(CMAKE_C_FLAGS "${CMAKE_C_FLAGS} -w")
        set(CMAKE_CXX_FLAGS "${CMAKE_CXX_FLAGS} -w")
    endif()

    add_subdirectory("${source_dir}" "${binary_dir}" EXCLUDE_FROM_ALL)

    set(CMAKE_C_FLAGS "${_redbolt_saved_c_flags}")
    set(CMAKE_CXX_FLAGS "${_redbolt_saved_cxx_flags}")
endmacro()

include(RedBoltSharpCpp.cmake)
