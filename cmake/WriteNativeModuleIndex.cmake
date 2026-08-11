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
