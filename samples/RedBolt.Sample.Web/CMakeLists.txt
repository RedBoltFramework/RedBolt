function(redbolt_collect_static_targets root_target output_variable)
    set(queue "${root_target}")
    set(visited)
    set(static_targets)
    while(queue)
        list(POP_FRONT queue target)
        if(target IN_LIST visited OR NOT TARGET "${target}")
            continue()
        endif()
        list(APPEND visited "${target}")
        get_target_property(aliased_target "${target}" ALIASED_TARGET)
        if(aliased_target)
            list(APPEND queue "${aliased_target}")
            continue()
        endif()
        get_target_property(target_type "${target}" TYPE)
        if(target_type STREQUAL "STATIC_LIBRARY")
            list(APPEND static_targets "${target}")
        endif()
        get_target_property(link_libraries "${target}" LINK_LIBRARIES)
        get_target_property(interface_link_libraries "${target}" INTERFACE_LINK_LIBRARIES)
        foreach(link_item IN LISTS link_libraries interface_link_libraries)
            if(TARGET "${link_item}")
                list(APPEND queue "${link_item}")
            endif()
        endforeach()
    endwhile()
    list(REMOVE_DUPLICATES static_targets)
    set("${output_variable}" "${static_targets}" PARENT_SCOPE)
endfunction()

redbolt_collect_static_targets(RedBolt.Native redbolt_wasm_static_targets)
set(REDBOLT_WASM_ENGINE_ARCHIVE "${CMAKE_BINARY_DIR}/lib/Release/wasm/RedBolt.Native.a")
set(REDBOLT_WASM_MRI_FILE "${CMAKE_BINARY_DIR}/redbolt-wasm-archives.mri")
set(redbolt_wasm_mri "create ${REDBOLT_WASM_ENGINE_ARCHIVE}\n")
foreach(target IN LISTS redbolt_wasm_static_targets)
    string(APPEND redbolt_wasm_mri "addlib $<TARGET_FILE:${target}>\n")
endforeach()
string(APPEND redbolt_wasm_mri "save\nend\n")
file(GENERATE OUTPUT "${REDBOLT_WASM_MRI_FILE}" CONTENT "${redbolt_wasm_mri}")

file(GLOB_RECURSE redbolt_wasm_resource_files CONFIGURE_DEPENDS
    RELATIVE "${PROJECT_SOURCE_DIR}/bin"
    "${PROJECT_SOURCE_DIR}/bin/CoreData/*"
    "${PROJECT_SOURCE_DIR}/bin/Data/*")
set(REDBOLT_WASM_RESOURCE_MANIFEST "${CMAKE_BINARY_DIR}/generated/web/resources.manifest.json")
set(redbolt_wasm_resource_manifest "[\n")
set(redbolt_wasm_resource_separator "")
foreach(resource_file IN LISTS redbolt_wasm_resource_files)
    string(APPEND redbolt_wasm_resource_manifest
        "${redbolt_wasm_resource_separator}  {\"name\":\"../resources/${resource_file}\",\"virtualPath\":\"${resource_file}\"}")
    set(redbolt_wasm_resource_separator ",\n")
endforeach()
string(APPEND redbolt_wasm_resource_manifest "\n]\n")
file(MAKE_DIRECTORY "${CMAKE_BINARY_DIR}/generated/web")
file(WRITE "${REDBOLT_WASM_RESOURCE_MANIFEST}" "${redbolt_wasm_resource_manifest}")

add_custom_command(
    OUTPUT "${REDBOLT_WASM_ENGINE_ARCHIVE}"
    COMMAND "${CMAKE_COMMAND}"
        "-DREDBOLT_EMSCRIPTEN_ENV=${_redbolt_emscripten_environment}"
        "-DREDBOLT_ARCHIVER=${CMAKE_AR}"
        "-DREDBOLT_MRI_FILE=${REDBOLT_WASM_MRI_FILE}"
        "-DREDBOLT_OUTPUT_ARCHIVE=${REDBOLT_WASM_ENGINE_ARCHIVE}"
        -P "${PROJECT_SOURCE_DIR}/cmake/MergeStaticArchives.cmake"
    DEPENDS ${redbolt_wasm_static_targets}
    COMMENT "Merging RedBolt WebAssembly engine archive"
    VERBATIM)
add_custom_target(redbolt-wasm-native DEPENDS "${REDBOLT_WASM_ENGINE_ARCHIVE}")
