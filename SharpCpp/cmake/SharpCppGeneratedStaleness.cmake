if(NOT EXISTS "${GENERATOR_DLL}")
  message(FATAL_ERROR "Generator DLL not found: ${GENERATOR_DLL}")
endif()

if(EXISTS "${GENERATED_STAMP}")
  file(TIMESTAMP "${GENERATOR_DLL}" generator_time "%s")
  file(TIMESTAMP "${GENERATED_STAMP}" generated_time "%s")

  if(generator_time GREATER generated_time)
    file(REMOVE_RECURSE "${GENERATED_DIR}")
    message(STATUS "Generator changed; clearing stale bindings in ${GENERATED_DIR}")
  endif()
endif()
