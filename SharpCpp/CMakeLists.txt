cmake_minimum_required(VERSION 3.28)

project(SharpCpp LANGUAGES CXX)

option(SHARPCPP_BUILD_E2E "Build the SharpCpp synthetic native E2E fixture." ${PROJECT_IS_TOP_LEVEL})
option(SHARPCPP_BUILD_RBFX "Build the rbfx binding native libraries." ${PROJECT_IS_TOP_LEVEL})

set(
  SHARPCPP_NATIVE_OUTPUT_ROOT
  "${SharpCpp_SOURCE_DIR}/build/bin"
  CACHE PATH
  "Root directory for SharpCpp native artifacts.")
set(
  SHARPCPP_MANAGED_OUTPUT_ROOT
  "${SharpCpp_SOURCE_DIR}/build/bin"
  CACHE PATH
  "Root directory containing the prebuilt SharpCpp managed tools.")
set(
  SHARPCPP_GENERATED_ROOT
  "${SharpCpp_BINARY_DIR}/generated"
  CACHE PATH
  "Root directory for generated binding sources.")

list(APPEND CMAKE_MODULE_PATH "${SharpCpp_SOURCE_DIR}/cmake")
include(SharpCppOutput)

add_subdirectory(include)

if(SHARPCPP_BUILD_E2E OR SHARPCPP_BUILD_RBFX)
  find_program(DOTNET_EXECUTABLE dotnet REQUIRED)
endif()

if(SHARPCPP_BUILD_E2E)
  add_subdirectory(tests)
endif()

if(SHARPCPP_BUILD_RBFX)
  add_subdirectory(rbfx)
endif()

add_custom_target(sharpcpp-native)
if(TARGET SharpCppTestsE2E_Native)
  add_dependencies(sharpcpp-native SharpCppTestsE2E_Native)
endif()
if(TARGET sharpcpp-rbfx-native)
  add_dependencies(sharpcpp-native sharpcpp-rbfx-native)
endif()
