set(
  RBFX_USERCODE_GENERATED_CPP_FILES
  "${RBFX_USERCODE_GENERATED_ROOT}/Rbfx.UserCode.Bindings.g.cpp")

set_source_files_properties(
  ${RBFX_USERCODE_GENERATED_CPP_FILES}
  PROPERTIES
    GENERATED TRUE
    OBJECT_DEPENDS "${RBFX_USERCODE_GENERATED_STAMP}")
add_library(
  SharpCppRbfxUserCode_Native
  SHARED
    ${RBFX_USERCODE_GENERATED_CPP_FILES}
    "${RBFX_USERCODE_NATIVE_SOURCE}")
add_dependencies(
  SharpCppRbfxUserCode_Native
  sharpcpp-generate-rbfx-usercode
  sharpcpp-rbfx-runtime)
target_compile_features(SharpCppRbfxUserCode_Native PRIVATE cxx_std_20)
set_target_properties(
  SharpCppRbfxUserCode_Native
  PROPERTIES
    CXX_SCAN_FOR_MODULES OFF
    UNITY_BUILD ON
    UNITY_BUILD_BATCH_SIZE 100
    OUTPUT_NAME "SharpCpp.Rbfx.UserCode.Native"
    BUILD_RPATH "$ORIGIN"
    INSTALL_RPATH "$ORIGIN")
target_include_directories(
  SharpCppRbfxUserCode_Native
  PRIVATE
    "${RBFX_USERCODE_GENERATED_ROOT}"
    "${CMAKE_CURRENT_SOURCE_DIR}/../include")
target_link_libraries(
  SharpCppRbfxUserCode_Native
  PRIVATE
    SharpCpp::NativeSupport
    "${RBFX_URHO3D_TARGET}")
target_compile_options(
  SharpCppRbfxUserCode_Native
  PRIVATE $<$<COMPILE_LANG_AND_ID:CXX,GNU,Clang,AppleClang>:-Wno-attributes>)
sharpcpp_set_native_output(SharpCppRbfxUserCode_Native)
