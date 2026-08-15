foreach(required_variable
        REDBOLT_SOURCE_DIR
        REDBOLT_NATIVE_PRESET
        REDBOLT_NATIVE_BUILD_DIR
        REDBOLT_NATIVE_TARGET
        REDBOLT_ENABLED_NATIVE_MODULES
        REDBOLT_SHARPCPP_ROOT
        REDBOLT_GENERATOR_DLL
        REDBOLT_RUNTIME_OUTPUT_DIR
        REDBOLT_PDB_OUTPUT_DIR)
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

set(_redbolt_configure_native TRUE)
set(_redbolt_native_cache "${REDBOLT_NATIVE_BUILD_DIR}/CMakeCache.txt")
if(EXISTS "${_redbolt_native_cache}")
    file(STRINGS "${_redbolt_native_cache}"
        _redbolt_cached_generator
        REGEX "^REDBOLT_DEPENDENCIES_GENERATOR_DLL:[^=]*="
        LIMIT_COUNT 1)
    string(REGEX REPLACE "^[^=]*=" "" _redbolt_cached_generator "${_redbolt_cached_generator}")
    file(STRINGS "${_redbolt_native_cache}"
        _redbolt_cached_runtime_output
        REGEX "^REDBOLT_RUNTIME_OUTPUT_DIR:[^=]*="
        LIMIT_COUNT 1)
    string(REGEX REPLACE "^[^=]*=" "" _redbolt_cached_runtime_output "${_redbolt_cached_runtime_output}")
    file(STRINGS "${_redbolt_native_cache}"
        _redbolt_cached_pdb_output
        REGEX "^REDBOLT_PDB_OUTPUT_DIR:[^=]*="
        LIMIT_COUNT 1)
    string(REGEX REPLACE "^[^=]*=" "" _redbolt_cached_pdb_output "${_redbolt_cached_pdb_output}")
    file(STRINGS "${_redbolt_native_cache}"
        _redbolt_cached_configuration
        REGEX "^REDBOLT_CONFIGURATION:[^=]*="
        LIMIT_COUNT 1)
    string(REGEX REPLACE "^[^=]*=" "" _redbolt_cached_configuration "${_redbolt_cached_configuration}")
    file(STRINGS "${_redbolt_native_cache}"
        _redbolt_cached_enabled_modules
        REGEX "^REDBOLT_ENABLED_NATIVE_MODULES:[^=]*="
        LIMIT_COUNT 1)
    string(REGEX REPLACE "^[^=]*=" "" _redbolt_cached_enabled_modules "${_redbolt_cached_enabled_modules}")
    file(STRINGS "${_redbolt_native_cache}"
        _redbolt_cached_cmake_command
        REGEX "^CMAKE_COMMAND:[^=]*="
        LIMIT_COUNT 1)
    string(REGEX REPLACE "^[^=]*=" "" _redbolt_cached_cmake_command "${_redbolt_cached_cmake_command}")
    set(_redbolt_cached_build_type_matches TRUE)
    if(REDBOLT_NATIVE_PRESET MATCHES "^android-")
        file(STRINGS "${_redbolt_native_cache}"
            _redbolt_cached_build_type
            REGEX "^CMAKE_BUILD_TYPE:[^=]*="
            LIMIT_COUNT 1)
        string(REGEX REPLACE "^[^=]*=" "" _redbolt_cached_build_type "${_redbolt_cached_build_type}")
        if(NOT _redbolt_cached_build_type STREQUAL REDBOLT_CONFIGURATION)
            set(_redbolt_cached_build_type_matches FALSE)
        endif()
    endif()
    if(_redbolt_cached_generator STREQUAL REDBOLT_GENERATOR_DLL
            AND _redbolt_cached_runtime_output STREQUAL REDBOLT_RUNTIME_OUTPUT_DIR
            AND _redbolt_cached_pdb_output STREQUAL REDBOLT_PDB_OUTPUT_DIR
            AND _redbolt_cached_configuration STREQUAL REDBOLT_CONFIGURATION
            AND _redbolt_cached_enabled_modules STREQUAL REDBOLT_ENABLED_NATIVE_MODULES
            AND _redbolt_cached_cmake_command STREQUAL CMAKE_COMMAND
            AND _redbolt_cached_build_type_matches)
        set(_redbolt_configure_native FALSE)
    endif()
endif()

if(_redbolt_configure_native)
    set(_redbolt_configure_arguments)
    if(REDBOLT_NATIVE_PRESET MATCHES "^android-")
        list(APPEND _redbolt_configure_arguments
            "-DCMAKE_BUILD_TYPE=${REDBOLT_CONFIGURATION}")
    endif()
    execute_process(
        COMMAND "${CMAKE_COMMAND}"
            --preset "${REDBOLT_NATIVE_PRESET}"
            -U "MAKECOMMAND"
            "-DREDBOLT_SHARPCPP_ROOT=${REDBOLT_SHARPCPP_ROOT}"
            "-DREDBOLT_DEPENDENCIES_GENERATOR_DLL=${REDBOLT_GENERATOR_DLL}"
            "-DREDBOLT_CONFIGURATION=${REDBOLT_CONFIGURATION}"
            "-DREDBOLT_ENABLED_NATIVE_MODULES=${REDBOLT_ENABLED_NATIVE_MODULES}"
            "-DREDBOLT_RUNTIME_OUTPUT_DIR=${REDBOLT_RUNTIME_OUTPUT_DIR}"
            "-DREDBOLT_PDB_OUTPUT_DIR=${REDBOLT_PDB_OUTPUT_DIR}"
            ${_redbolt_configure_arguments}
        WORKING_DIRECTORY "${REDBOLT_SOURCE_DIR}"
        COMMAND_ERROR_IS_FATAL ANY)
endif()

set(_redbolt_native_environment)
if(REDBOLT_NATIVE_PRESET STREQUAL "browser-wasm"
        AND EXISTS "${REDBOLT_NATIVE_BUILD_DIR}/redbolt-dotnet-emscripten-env")
    set(_redbolt_native_environment "${REDBOLT_NATIVE_BUILD_DIR}/redbolt-dotnet-emscripten-env")
endif()
set(_redbolt_build_command
    "${CMAKE_COMMAND}" -E env "REDBOLT_MSBUILD_BUILD=1"
    ${_redbolt_native_environment}
    "${CMAKE_COMMAND}"
    --build "${REDBOLT_NATIVE_BUILD_DIR}")
if(DEFINED REDBOLT_CONFIGURATION AND NOT REDBOLT_CONFIGURATION STREQUAL "")
    list(APPEND _redbolt_build_command --config "${REDBOLT_CONFIGURATION}")
endif()
list(APPEND _redbolt_build_command
    --target "${REDBOLT_NATIVE_TARGET}"
    --parallel 4)
execute_process(
    COMMAND ${_redbolt_build_command}
    WORKING_DIRECTORY "${REDBOLT_SOURCE_DIR}"
    COMMAND_ERROR_IS_FATAL ANY)
