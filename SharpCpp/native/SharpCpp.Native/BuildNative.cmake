cmake_minimum_required(VERSION 3.28)

foreach(_required
    SHARPCPP_SOURCE_DIR
    SHARPCPP_NATIVE_PRESET
    SHARPCPP_NATIVE_BUILD_DIR
    SHARPCPP_NATIVE_TARGET
    SHARPCPP_CONFIGURATION
    SHARPCPP_NATIVE_LOCK_FILE)
  if(NOT DEFINED ${_required} OR "${${_required}}" STREQUAL "")
    message(FATAL_ERROR "${_required} is required")
  endif()
endforeach()

get_filename_component(_lock_directory "${SHARPCPP_NATIVE_LOCK_FILE}" DIRECTORY)
file(MAKE_DIRECTORY "${_lock_directory}")
file(LOCK "${SHARPCPP_NATIVE_LOCK_FILE}" GUARD PROCESS TIMEOUT 900)

if(SHARPCPP_CONFIGURE_NATIVE)
  set(_configure_command
    "${CMAKE_COMMAND}"
    --preset "${SHARPCPP_NATIVE_PRESET}"
    -S "${SHARPCPP_SOURCE_DIR}"
    -B "${SHARPCPP_NATIVE_BUILD_DIR}"
    "-DSHARPCPP_BUILD_E2E=${SHARPCPP_BUILD_E2E}"
    "-DSHARPCPP_BUILD_RBFX=${SHARPCPP_BUILD_RBFX}"
    "-DSHARPCPP_MANAGED_OUTPUT_ROOT=${SHARPCPP_MANAGED_OUTPUT_ROOT}"
    "-DSHARPCPP_NATIVE_OUTPUT_ROOT=${SHARPCPP_NATIVE_OUTPUT_ROOT}")

  if(NOT "${SHARPCPP_RBFX_SDK_ROOT}" STREQUAL "")
    list(APPEND _configure_command "-DRBFX_SDK_ROOT=${SHARPCPP_RBFX_SDK_ROOT}")
  endif()
  if(NOT "${SHARPCPP_NATIVE_CONFIGURE_ARGUMENTS}" STREQUAL "")
    separate_arguments(_extra_configure_arguments NATIVE_COMMAND "${SHARPCPP_NATIVE_CONFIGURE_ARGUMENTS}")
    list(APPEND _configure_command ${_extra_configure_arguments})
  endif()

  execute_process(
    COMMAND ${_configure_command}
    WORKING_DIRECTORY "${SHARPCPP_SOURCE_DIR}"
    COMMAND_ERROR_IS_FATAL ANY)
endif()

execute_process(
  COMMAND
    "${CMAKE_COMMAND}" --build "${SHARPCPP_NATIVE_BUILD_DIR}"
    --config "${SHARPCPP_CONFIGURATION}"
    --target "${SHARPCPP_NATIVE_TARGET}"
    --parallel 16
  WORKING_DIRECTORY "${SHARPCPP_SOURCE_DIR}"
  COMMAND_ERROR_IS_FATAL ANY)
