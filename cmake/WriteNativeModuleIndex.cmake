# Copyright (c) 2026 RedBolt and contributors.
# This work is licensed under the terms of the MIT license.
# For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

cmake_minimum_required(VERSION 3.28)

if(NOT DEFINED REDBOLT_MANIFEST_DIRECTORY OR NOT DEFINED REDBOLT_INDEX_FILE)
    message(FATAL_ERROR "Native module index arguments are incomplete")
endif()

if(DEFINED REDBOLT_MODULES AND NOT REDBOLT_MODULES STREQUAL "")
    set(_redbolt_manifest_files)
    foreach(_redbolt_module IN LISTS REDBOLT_MODULES)
        list(APPEND _redbolt_manifest_files
            "${REDBOLT_MANIFEST_DIRECTORY}/${_redbolt_module}.json")
    endforeach()
else()
    file(GLOB _redbolt_manifest_files
        "${REDBOLT_MANIFEST_DIRECTORY}/*.json")
endif()
list(SORT _redbolt_manifest_files)
file(MAKE_DIRECTORY "${REDBOLT_MANIFEST_DIRECTORY}")

# A build directory may be reconfigured with a smaller module set. Remove
# manifests that are no longer represented by the authoritative index so a
# sibling add-on with the same managed dependency cannot be mistaken for a
# base-engine native module.
file(GLOB _redbolt_existing_manifest_files
    "${REDBOLT_MANIFEST_DIRECTORY}/*.json")
foreach(_redbolt_existing_manifest_file IN LISTS
        _redbolt_existing_manifest_files)
    if(_redbolt_existing_manifest_file STREQUAL "${REDBOLT_INDEX_FILE}")
        continue()
    endif()
    list(FIND _redbolt_manifest_files
        "${_redbolt_existing_manifest_file}" _redbolt_manifest_index)
    if(_redbolt_manifest_index EQUAL -1)
        if(DEFINED REDBOLT_RUNTIME_OUTPUT_DIRECTORY
                AND NOT REDBOLT_RUNTIME_OUTPUT_DIRECTORY STREQUAL "")
            file(READ "${_redbolt_existing_manifest_file}"
                _redbolt_stale_manifest)
            string(JSON _redbolt_stale_artifact_count ERROR_VARIABLE
                _redbolt_stale_artifact_error LENGTH
                "${_redbolt_stale_manifest}" artifacts)
            if(NOT _redbolt_stale_artifact_error
                    AND _redbolt_stale_artifact_count GREATER 0)
                math(EXPR _redbolt_stale_artifact_last
                    "${_redbolt_stale_artifact_count} - 1")
                foreach(_redbolt_stale_artifact_index RANGE
                        0 ${_redbolt_stale_artifact_last})
                    string(JSON _redbolt_stale_artifact GET
                        "${_redbolt_stale_manifest}" artifacts
                        ${_redbolt_stale_artifact_index})
                    file(REMOVE
                        "${REDBOLT_RUNTIME_OUTPUT_DIRECTORY}/${_redbolt_stale_artifact}")
                endforeach()
            endif()
        endif()
        file(REMOVE "${_redbolt_existing_manifest_file}")
    endif()
endforeach()

set(_redbolt_modules_json "")
foreach(_redbolt_manifest_file IN LISTS _redbolt_manifest_files)
    if(_redbolt_manifest_file STREQUAL "${REDBOLT_INDEX_FILE}")
        continue()
    endif()
    file(READ "${_redbolt_manifest_file}" _redbolt_manifest)
    string(JSON _redbolt_module GET "${_redbolt_manifest}" module)
    if(NOT _redbolt_modules_json STREQUAL "")
        string(APPEND _redbolt_modules_json ",\n")
    endif()
    string(APPEND _redbolt_modules_json
        "    \"${_redbolt_module}\": ${_redbolt_manifest}")
endforeach()

file(WRITE "${REDBOLT_INDEX_FILE}"
    "{\n"
    "  \"schemaVersion\": 1,\n"
    "  \"modules\": {\n"
    "${_redbolt_modules_json}\n"
    "  }\n"
    "}\n")
