if(NOT DEFINED REDBOLT_NATIVE_LIBRARY_FILE
        OR NOT DEFINED REDBOLT_MANIFEST_FILE
        OR NOT DEFINED REDBOLT_MANAGED_SELECTOR
        OR NOT DEFINED REDBOLT_NATIVE_LIBRARY_NAME)
    message(FATAL_ERROR "Native module manifest arguments are incomplete")
endif()

get_filename_component(_redbolt_manifest_directory
    "${REDBOLT_MANIFEST_FILE}" DIRECTORY)
get_filename_component(_redbolt_native_filename
    "${REDBOLT_NATIVE_LIBRARY_FILE}" NAME)
get_filename_component(_redbolt_native_directory
    "${REDBOLT_NATIVE_LIBRARY_FILE}" DIRECTORY)
file(MAKE_DIRECTORY "${_redbolt_manifest_directory}")

set(_redbolt_resolved_dependencies)
set(_redbolt_unresolved_dependencies)
set(_redbolt_requires)
if(DEFINED REDBOLT_REQUIRES AND NOT REDBOLT_REQUIRES STREQUAL "")
    string(REPLACE "," ";" _redbolt_requires "${REDBOLT_REQUIRES}")
endif()
set(_redbolt_requirement_closure)
if(DEFINED REDBOLT_REQUIREMENT_CLOSURE
        AND NOT REDBOLT_REQUIREMENT_CLOSURE STREQUAL "")
    string(REPLACE "," ";" _redbolt_requirement_closure
        "${REDBOLT_REQUIREMENT_CLOSURE}")
endif()
set(_redbolt_native_wrapper_modules)
set(_redbolt_native_wrapper_filenames)
if(DEFINED REDBOLT_NATIVE_WRAPPERS AND NOT REDBOLT_NATIVE_WRAPPERS STREQUAL "")
    string(REPLACE "," ";" _redbolt_native_wrapper_entries
        "${REDBOLT_NATIVE_WRAPPERS}")
    foreach(_redbolt_native_wrapper_entry IN LISTS _redbolt_native_wrapper_entries)
        if(NOT _redbolt_native_wrapper_entry MATCHES "^([^=]+)=(.+)$")
            message(FATAL_ERROR
                "Invalid Native wrapper manifest entry '${_redbolt_native_wrapper_entry}'")
        endif()
        list(APPEND _redbolt_native_wrapper_modules "${CMAKE_MATCH_1}")
        list(APPEND _redbolt_native_wrapper_filenames "${CMAKE_MATCH_2}")
    endforeach()
endif()
set(_redbolt_toolchain_runtime_files)
if(DEFINED REDBOLT_TOOLCHAIN_RUNTIME_FILES
        AND NOT REDBOLT_TOOLCHAIN_RUNTIME_FILES STREQUAL "")
    string(REPLACE "," ";" _redbolt_toolchain_runtime_files
        "${REDBOLT_TOOLCHAIN_RUNTIME_FILES}")
endif()
if(CMAKE_GET_RUNTIME_DEPENDENCIES_PLATFORM STREQUAL "windows+pe"
        AND CMAKE_GET_RUNTIME_DEPENDENCIES_TOOL STREQUAL "objdump")
    file(GLOB _redbolt_runtime_candidates
        LIST_DIRECTORIES FALSE "${_redbolt_native_directory}/*")
    set(_redbolt_dependency_queue "${REDBOLT_NATIVE_LIBRARY_FILE}")
    set(_redbolt_visited_dependencies "${REDBOLT_NATIVE_LIBRARY_FILE}")
    while(_redbolt_dependency_queue)
        list(POP_FRONT _redbolt_dependency_queue _redbolt_dependency_file)
        execute_process(
            COMMAND "${CMAKE_GET_RUNTIME_DEPENDENCIES_COMMAND}" -p "${_redbolt_dependency_file}"
            RESULT_VARIABLE _redbolt_objdump_result
            OUTPUT_VARIABLE _redbolt_objdump_output
            ERROR_VARIABLE _redbolt_objdump_error)
        if(NOT _redbolt_objdump_result EQUAL 0)
            message(FATAL_ERROR
                "Unable to inspect ${_redbolt_dependency_file}: ${_redbolt_objdump_error}")
        endif()

        string(REGEX MATCHALL
            "[ \t]DLL Name:[ \t]*[^\r\n]+"
            _redbolt_dependency_names "${_redbolt_objdump_output}")
        foreach(_redbolt_dependency_name IN LISTS _redbolt_dependency_names)
            string(REGEX REPLACE
                "^[ \t]*DLL Name:[ \t]*" ""
                _redbolt_dependency_name "${_redbolt_dependency_name}")
            string(TOLOWER "${_redbolt_dependency_name}" _redbolt_dependency_name_lower)
            set(_redbolt_resolved_dependency "")
            foreach(_redbolt_runtime_candidate IN LISTS _redbolt_runtime_candidates)
                get_filename_component(_redbolt_runtime_candidate_name
                    "${_redbolt_runtime_candidate}" NAME)
                string(TOLOWER "${_redbolt_runtime_candidate_name}"
                    _redbolt_runtime_candidate_name_lower)
                if(_redbolt_runtime_candidate_name_lower STREQUAL _redbolt_dependency_name_lower)
                    set(_redbolt_resolved_dependency "${_redbolt_runtime_candidate}")
                    break()
                endif()
            endforeach()

            if(_redbolt_resolved_dependency)
                list(FIND _redbolt_visited_dependencies
                    "${_redbolt_resolved_dependency}" _redbolt_visited_index)
                if(_redbolt_visited_index EQUAL -1)
                    list(APPEND _redbolt_visited_dependencies "${_redbolt_resolved_dependency}")
                    list(APPEND _redbolt_dependency_queue "${_redbolt_resolved_dependency}")
                    list(APPEND _redbolt_resolved_dependencies "${_redbolt_resolved_dependency}")
                endif()
            else()
                list(APPEND _redbolt_unresolved_dependencies "${_redbolt_dependency_name}")
            endif()
        endforeach()
    endwhile()
    list(REMOVE_DUPLICATES _redbolt_unresolved_dependencies)
else()
    file(GET_RUNTIME_DEPENDENCIES
        LIBRARIES "${REDBOLT_NATIVE_LIBRARY_FILE}"
        RESOLVED_DEPENDENCIES_VAR _redbolt_resolved_dependencies
        UNRESOLVED_DEPENDENCIES_VAR _redbolt_unresolved_dependencies
        DIRECTORIES "${_redbolt_native_directory}")
endif()

set(_redbolt_artifacts "${_redbolt_native_filename}")
foreach(_redbolt_dependency IN LISTS _redbolt_resolved_dependencies)
    get_filename_component(_redbolt_dependency_filename
        "${_redbolt_dependency}" NAME)
    get_filename_component(_redbolt_dependency_directory
        "${_redbolt_dependency}" DIRECTORY)
    set(_redbolt_dependency_wrapper_module)
    string(TOLOWER "${_redbolt_dependency_filename}"
        _redbolt_dependency_filename_lower)
    list(LENGTH _redbolt_native_wrapper_filenames _redbolt_wrapper_count)
    if(_redbolt_wrapper_count GREATER 0)
        math(EXPR _redbolt_last_wrapper_index "${_redbolt_wrapper_count} - 1")
        foreach(_redbolt_wrapper_index RANGE 0 ${_redbolt_last_wrapper_index})
            list(GET _redbolt_native_wrapper_filenames ${_redbolt_wrapper_index}
                _redbolt_wrapper_filename)
            string(TOLOWER "${_redbolt_wrapper_filename}"
                _redbolt_wrapper_filename_lower)
            if(_redbolt_dependency_filename_lower STREQUAL
                    _redbolt_wrapper_filename_lower)
                list(GET _redbolt_native_wrapper_modules ${_redbolt_wrapper_index}
                    _redbolt_dependency_wrapper_module)
                break()
            endif()
        endforeach()
    endif()
    if(_redbolt_dependency_wrapper_module)
        if(NOT _redbolt_dependency_wrapper_module IN_LIST
                _redbolt_requirement_closure)
            message(FATAL_ERROR
                "${REDBOLT_MODULE} resolves Native wrapper '${_redbolt_dependency_wrapper_module}' outside its requires closure")
        endif()
    elseif(_redbolt_dependency_directory STREQUAL "${_redbolt_native_directory}"
            AND NOT _redbolt_dependency_filename STREQUAL "${_redbolt_native_filename}")
        set(_redbolt_allowed_toolchain_runtime FALSE)
        foreach(_redbolt_toolchain_runtime_file IN LISTS
                _redbolt_toolchain_runtime_files)
            string(TOLOWER "${_redbolt_toolchain_runtime_file}"
                _redbolt_toolchain_runtime_file_lower)
            if(_redbolt_dependency_filename_lower STREQUAL
                    _redbolt_toolchain_runtime_file_lower)
                set(_redbolt_allowed_toolchain_runtime TRUE)
                break()
            endif()
        endforeach()
        if(NOT _redbolt_allowed_toolchain_runtime)
            message(FATAL_ERROR
                "${REDBOLT_MODULE} imports undeclared local library '${_redbolt_dependency_filename}'")
        endif()
        list(APPEND _redbolt_artifacts "${_redbolt_dependency_filename}")
    endif()
endforeach()
list(REMOVE_DUPLICATES _redbolt_artifacts)
list(SORT _redbolt_artifacts)

set(_redbolt_requires_json "")
if(_redbolt_requires)
    foreach(_redbolt_require IN LISTS _redbolt_requires)
        if(NOT _redbolt_requires_json STREQUAL "")
            string(APPEND _redbolt_requires_json ",")
        endif()
        string(APPEND _redbolt_requires_json "\"${_redbolt_require}\"")
    endforeach()
endif()

set(_redbolt_artifacts_json "")
foreach(_redbolt_artifact IN LISTS _redbolt_artifacts)
    if(NOT _redbolt_artifacts_json STREQUAL "")
        string(APPEND _redbolt_artifacts_json ",")
    endif()
    string(APPEND _redbolt_artifacts_json "\"${_redbolt_artifact}\"")
endforeach()

file(WRITE "${REDBOLT_MANIFEST_FILE}"
    "{\n"
    "  \"schemaVersion\": 1,\n"
    "  \"module\": \"${REDBOLT_MODULE}\",\n"
    "  \"managedSelector\": \"${REDBOLT_MANAGED_SELECTOR}\",\n"
    "  \"nativeLibrary\": \"${REDBOLT_NATIVE_LIBRARY_NAME}\",\n"
    "  \"artifacts\": [${_redbolt_artifacts_json}],\n"
    "  \"requires\": [${_redbolt_requires_json}]\n"
    "}\n")

if(_redbolt_unresolved_dependencies)
    message(WARNING
        "Unresolved dependencies for ${REDBOLT_NATIVE_LIBRARY_FILE}: "
        "${_redbolt_unresolved_dependencies}")
endif()
