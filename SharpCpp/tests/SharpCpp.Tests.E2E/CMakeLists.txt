set(SHARPCPP_E2E_GENERATED_ROOT "${SHARPCPP_GENERATED_ROOT}/sharpcpp-e2e")
set(SHARPCPP_E2E_GENERATED_STAMP "${SHARPCPP_E2E_GENERATED_ROOT}/generate.stamp")
set(SHARPCPP_E2E_FILE_STEM "SharpCppTestsE2E")
set(SHARPCPP_E2E_GENERATOR_LOG_FILE "${SHARPCPP_E2E_GENERATED_ROOT}/generator.log")
set(SHARPCPP_TOOL_DLL "${SHARPCPP_MANAGED_OUTPUT_ROOT}/$<CONFIG>/SharpCpp.Tool.dll")

file(GLOB_RECURSE SHARPCPP_E2E_INPUT_HEADERS CONFIGURE_DEPENDS "${CMAKE_CURRENT_SOURCE_DIR}/*/*.hpp")
file(GLOB_RECURSE SHARPCPP_E2E_GENERATOR_INPUTS CONFIGURE_DEPENDS
  "${SharpCpp_SOURCE_DIR}/src/SharpCpp.Model/*.cs"
  "${SharpCpp_SOURCE_DIR}/src/SharpCpp.Metadata/*.cs"
  "${SharpCpp_SOURCE_DIR}/src/SharpCpp.Parsing/*.cs"
  "${SharpCpp_SOURCE_DIR}/src/SharpCpp.Rendering/*.cs"
  "${SharpCpp_SOURCE_DIR}/src/SharpCpp.Tool/*.cs"
  "${SharpCpp_SOURCE_DIR}/include/SharpCpp/*.hpp"
  "${SharpCpp_SOURCE_DIR}/include/SharpCpp/*.h")

add_custom_command(
  OUTPUT "${SHARPCPP_E2E_GENERATED_STAMP}"
  COMMAND "${CMAKE_COMMAND}"
          "-DGENERATOR_DLL=${SHARPCPP_TOOL_DLL}"
          "-DGENERATED_STAMP=${SHARPCPP_E2E_GENERATED_STAMP}"
          "-DGENERATED_DIR=${SHARPCPP_E2E_GENERATED_ROOT}"
          -P "${SharpCpp_SOURCE_DIR}/cmake/SharpCppGeneratedStaleness.cmake"
  COMMAND "${CMAKE_COMMAND}" -E make_directory "${SHARPCPP_E2E_GENERATED_ROOT}"
  COMMAND "${DOTNET_EXECUTABLE}" "${SHARPCPP_TOOL_DLL}"
          generate-sample
          --generated-root "${SHARPCPP_E2E_GENERATED_ROOT}"
          --project-root "${SharpCpp_SOURCE_DIR}"
          --file-stem "${SHARPCPP_E2E_FILE_STEM}"
          --log-file "${SHARPCPP_E2E_GENERATOR_LOG_FILE}"
  COMMAND "${CMAKE_COMMAND}" -E touch "${SHARPCPP_E2E_GENERATED_STAMP}"
  DEPENDS "${SHARPCPP_TOOL_DLL}" ${SHARPCPP_E2E_INPUT_HEADERS} ${SHARPCPP_E2E_GENERATOR_INPUTS}
  WORKING_DIRECTORY "${SharpCpp_SOURCE_DIR}"
  VERBATIM
  USES_TERMINAL)

add_custom_target(sharpcpp-generate-e2e DEPENDS "${SHARPCPP_E2E_GENERATED_STAMP}")
set_property(DIRECTORY APPEND PROPERTY CMAKE_CONFIGURE_DEPENDS "${SHARPCPP_E2E_GENERATED_STAMP}")

file(MAKE_DIRECTORY "${SHARPCPP_E2E_GENERATED_ROOT}")
file(GLOB SHARPCPP_E2E_GENERATED_CPP_FILES CONFIGURE_DEPENDS "${SHARPCPP_E2E_GENERATED_ROOT}/*.g.cpp")
if(NOT SHARPCPP_E2E_GENERATED_CPP_FILES)
  file(WRITE "${SHARPCPP_E2E_GENERATED_ROOT}/.stub.cpp" "// Replaced by generated binding sources.\n")
  set(SHARPCPP_E2E_GENERATED_CPP_FILES "${SHARPCPP_E2E_GENERATED_ROOT}/.stub.cpp")
endif()

set_source_files_properties(
  ${SHARPCPP_E2E_GENERATED_CPP_FILES}
  PROPERTIES OBJECT_DEPENDS "${SHARPCPP_E2E_GENERATED_STAMP}")
add_library(SharpCppTestsE2E_Native SHARED ${SHARPCPP_E2E_GENERATED_CPP_FILES})
add_dependencies(SharpCppTestsE2E_Native sharpcpp-generate-e2e)
target_compile_features(SharpCppTestsE2E_Native PRIVATE cxx_std_20)
set_target_properties(
  SharpCppTestsE2E_Native
  PROPERTIES
    CXX_SCAN_FOR_MODULES OFF
    UNITY_BUILD ON
    UNITY_BUILD_BATCH_SIZE 100)
target_include_directories(
  SharpCppTestsE2E_Native
  PRIVATE
    "${SHARPCPP_E2E_GENERATED_ROOT}"
    "${CMAKE_CURRENT_SOURCE_DIR}")
target_link_libraries(SharpCppTestsE2E_Native PRIVATE SharpCpp::NativeSupport)
target_compile_options(
  SharpCppTestsE2E_Native
  PRIVATE $<$<COMPILE_LANG_AND_ID:CXX,GNU,Clang,AppleClang>:-Wno-attributes>)
sharpcpp_set_native_output(SharpCppTestsE2E_Native)
