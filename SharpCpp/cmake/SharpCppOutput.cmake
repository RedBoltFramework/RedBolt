function(sharpcpp_set_native_output target_name)
  set(output_directory "${SHARPCPP_NATIVE_OUTPUT_ROOT}/$<CONFIG>")
  set_target_properties(
    "${target_name}"
    PROPERTIES
      RUNTIME_OUTPUT_DIRECTORY "${output_directory}"
      LIBRARY_OUTPUT_DIRECTORY "${output_directory}"
      ARCHIVE_OUTPUT_DIRECTORY "${output_directory}")
endfunction()
