# Copyright (c) 2026 RedBolt and contributors.
# This work is licensed under the terms of the MIT license.
# For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

# file(LOCK) with a TIMEOUT tries one time each second, and many projects wait
# for the same build directory. Poll faster at first, for the short steps of a
# build with no change. A long native build then uses the usual wait.
function(redbolt_lock_native_build_dir build_dir)
    get_filename_component(build_parent "${build_dir}" DIRECTORY)
    file(MAKE_DIRECTORY "${build_parent}")
    foreach(attempt RANGE 1 200)
        file(LOCK "${build_dir}.lock"
            GUARD PROCESS
            TIMEOUT 0
            RESULT_VARIABLE lock_result)
        if(lock_result STREQUAL "0")
            return()
        endif()
        execute_process(COMMAND "${CMAKE_COMMAND}" -E sleep 0.05)
    endforeach()
    file(LOCK "${build_dir}.lock"
        GUARD PROCESS
        TIMEOUT 1800
        RESULT_VARIABLE lock_result)
    if(NOT lock_result STREQUAL "0")
        message(FATAL_ERROR "Could not lock ${build_dir}: ${lock_result}")
    endif()
endfunction()
