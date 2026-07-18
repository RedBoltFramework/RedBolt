file(GLOB_RECURSE RBFX_GENERATOR_INPUTS CONFIGURE_DEPENDS
  "${SharpCpp_SOURCE_DIR}/src/SharpCpp.Model/*.cs"
  "${SharpCpp_SOURCE_DIR}/src/SharpCpp.Metadata/*.cs"
  "${SharpCpp_SOURCE_DIR}/src/SharpCpp.Parsing/*.cs"
  "${SharpCpp_SOURCE_DIR}/src/SharpCpp.Pipeline/*.cs"
  "${SharpCpp_SOURCE_DIR}/src/SharpCpp.Rendering/*.cs"
  "${CMAKE_CURRENT_SOURCE_DIR}/*.cs"
  "${SharpCpp_SOURCE_DIR}/include/SharpCpp/*.hpp"
  "${SharpCpp_SOURCE_DIR}/include/SharpCpp/*.h"
  "${CMAKE_CURRENT_SOURCE_DIR}/../include/SharpCpp/*.hpp"
  "${CMAKE_CURRENT_SOURCE_DIR}/../include/SharpCpp/*.h")

add_custom_command(
  OUTPUT "${RBFX_GENERATED_STAMP}"
  COMMAND "${CMAKE_COMMAND}"
          "-DGENERATOR_DLL=${SHARPCPP_RBFX_GENERATOR_DLL}"
          "-DGENERATED_STAMP=${RBFX_GENERATED_STAMP}"
          "-DGENERATED_DIR=${RBFX_GENERATED_ROOT}"
          -P "${SharpCpp_SOURCE_DIR}/cmake/SharpCppGeneratedStaleness.cmake"
  COMMAND "${CMAKE_COMMAND}" -E make_directory "${RBFX_GENERATED_ROOT}"
  COMMAND "${DOTNET_EXECUTABLE}" "${SHARPCPP_RBFX_GENERATOR_DLL}"
          --sdk-root "${RBFX_SDK_ROOT}"
          --flags-file "${RBFX_SDK_FLAGS_FILE}"
          --generated-root "${RBFX_GENERATED_ROOT}"
          --project-root "${SharpCpp_SOURCE_DIR}"
          --sdk-library "$<TARGET_FILE:${RBFX_URHO3D_TARGET}>"
          --log-file "${RBFX_GENERATOR_LOG_FILE}"
  COMMAND "${CMAKE_COMMAND}" -E touch "${RBFX_GENERATED_STAMP}"
  DEPENDS
    "${SHARPCPP_RBFX_GENERATOR_DLL}"
    "${RBFX_SDK_FLAGS_FILE}"
    ${RBFX_SDK_INPUT_HEADERS}
    ${RBFX_GENERATOR_INPUTS}
  WORKING_DIRECTORY "${SharpCpp_SOURCE_DIR}"
  VERBATIM
  USES_TERMINAL)

add_custom_target(sharpcpp-generate-rbfx DEPENDS "${RBFX_GENERATED_STAMP}")
set_property(DIRECTORY APPEND PROPERTY CMAKE_CONFIGURE_DEPENDS "${RBFX_GENERATED_STAMP}")
