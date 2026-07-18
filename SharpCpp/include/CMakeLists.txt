add_library(sharpcpp-native-support INTERFACE)
add_library(SharpCpp::NativeSupport ALIAS sharpcpp-native-support)

target_compile_features(sharpcpp-native-support INTERFACE cxx_std_20)
target_include_directories(
  sharpcpp-native-support
  INTERFACE
    "$<BUILD_INTERFACE:${CMAKE_CURRENT_SOURCE_DIR}>")
set_target_properties(
  sharpcpp-native-support
  PROPERTIES INTERFACE_CXX_SCAN_FOR_MODULES OFF)
