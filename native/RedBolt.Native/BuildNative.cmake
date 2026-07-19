foreach(required_variable
        REDBOLT_SOURCE_DIR
        REDBOLT_NATIVE_PRESET
        REDBOLT_NATIVE_BUILD_DIR
        REDBOLT_NATIVE_TARGET
        REDBOLT_SHARPCPP_ROOT
        REDBOLT_GENERATOR_DLL
        REDBOLT_RUNTIME_OUTPUT_DIR)
    if(NOT DEFINED ${required_variable} OR "${${required_variable}}" STREQUAL "")
        message(FATAL_ERROR "${required_variable} is required")
    endif()
endforeach()

get_filename_component(_redbolt_native_build_parent "${REDBOLT_NATIVE_BUILD_DIR}" DIRECTORY)
file(MAKE_DIRECTORY "${_redbolt_native_build_parent}")
file(LOCK "${REDBOLT_NATIVE_BUILD_DIR}.lock"
    GUARD PROCESS
    TIMEOUT 1800
    RESULT_VARIABLE _redbolt_lock_result)
if(NOT _redbolt_lock_result STREQUAL "0")
    message(FATAL_ERROR "Could not lock ${REDBOLT_NATIVE_BUILD_DIR}: ${_redbolt_lock_result}")
endif()

if(NOT IS_DIRECTORY "${REDBOLT_NATIVE_BUILD_DIR}")
    execute_process(
        COMMAND "${CMAKE_COMMAND}"
            --preset "${REDBOLT_NATIVE_PRESET}"
            "-DREDBOLT_SHARPCPP_ROOT=${REDBOLT_SHARPCPP_ROOT}"
            "-DREDBOLT_DEPENDENCIES_GENERATOR_DLL=${REDBOLT_GENERATOR_DLL}"
            "-DREDBOLT_RUNTIME_OUTPUT_DIR=${REDBOLT_RUNTIME_OUTPUT_DIR}"
        WORKING_DIRECTORY "${REDBOLT_SOURCE_DIR}"
        COMMAND_ERROR_IS_FATAL ANY)
endif()

set(_redbolt_build_command
    "${CMAKE_COMMAND}" -E env "REDBOLT_MSBUILD_BUILD=1"
    "${CMAKE_COMMAND}"
    --build "${REDBOLT_NATIVE_BUILD_DIR}")
if(DEFINED REDBOLT_CONFIGURATION AND NOT REDBOLT_CONFIGURATION STREQUAL "")
    list(APPEND _redbolt_build_command --config "${REDBOLT_CONFIGURATION}")
endif()
list(APPEND _redbolt_build_command
    --target "${REDBOLT_NATIVE_TARGET}"
    --parallel 16)
execute_process(
    COMMAND ${_redbolt_build_command}
    WORKING_DIRECTORY "${REDBOLT_SOURCE_DIR}"
    COMMAND_ERROR_IS_FATAL ANY)
