# Copyright (c) 2026 RedBolt and contributors.
# This work is licensed under the terms of the MIT license.
# For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

foreach(required_variable
        REDBOLT_SOURCE_DIR
        REDBOLT_PLATFORM
        REDBOLT_NATIVE_BUILD_DIR
        REDBOLT_NATIVE_TARGET)
    if(NOT DEFINED ${required_variable} OR "${${required_variable}}" STREQUAL "")
        message(FATAL_ERROR "${required_variable} is required")
    endif()
endforeach()

include("${CMAKE_CURRENT_LIST_DIR}/NativeBuildLock.cmake")
redbolt_lock_native_build_dir("${REDBOLT_NATIVE_BUILD_DIR}")

set(_redbolt_native_environment)
if(REDBOLT_PLATFORM STREQUAL "browser-wasm"
        AND EXISTS "${REDBOLT_NATIVE_BUILD_DIR}/redbolt-dotnet-emscripten-env")
    set(_redbolt_native_environment "${REDBOLT_NATIVE_BUILD_DIR}/redbolt-dotnet-emscripten-env")
endif()
set(_redbolt_build_command
    "${CMAKE_COMMAND}" -E env "REDBOLT_MSBUILD_BUILD=1"
    ${_redbolt_native_environment}
    "${CMAKE_COMMAND}"
    --build "${REDBOLT_NATIVE_BUILD_DIR}")
list(APPEND _redbolt_build_command
    --target "${REDBOLT_NATIVE_TARGET}"
    --parallel)
execute_process(
    COMMAND ${_redbolt_build_command}
    WORKING_DIRECTORY "${REDBOLT_SOURCE_DIR}"
    COMMAND_ERROR_IS_FATAL ANY)
