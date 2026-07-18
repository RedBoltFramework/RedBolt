if(NOT DEFINED REDBOLT_ARCHIVER OR NOT DEFINED REDBOLT_MRI_FILE OR NOT DEFINED REDBOLT_OUTPUT_ARCHIVE)
    message(FATAL_ERROR "REDBOLT_ARCHIVER, REDBOLT_MRI_FILE and REDBOLT_OUTPUT_ARCHIVE are required")
endif()

file(REMOVE "${REDBOLT_OUTPUT_ARCHIVE}")

execute_process(
    COMMAND "${REDBOLT_EMSCRIPTEN_ENV}" "${REDBOLT_ARCHIVER}" -M
    INPUT_FILE "${REDBOLT_MRI_FILE}"
    RESULT_VARIABLE result)
if(NOT result EQUAL 0)
    message(FATAL_ERROR "Failed to merge RedBolt WebAssembly static archives")
endif()

execute_process(
    COMMAND "${REDBOLT_EMSCRIPTEN_ENV}" "${REDBOLT_ARCHIVER}" s "${REDBOLT_OUTPUT_ARCHIVE}"
    RESULT_VARIABLE result)
if(NOT result EQUAL 0)
    message(FATAL_ERROR "Failed to index RedBolt WebAssembly static archive")
endif()
