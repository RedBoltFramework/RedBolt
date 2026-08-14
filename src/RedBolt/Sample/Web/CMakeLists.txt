set(redbolt_wasm_static_targets)
foreach(redbolt_wasm_native_module IN LISTS REDBOLT_SELECTED_NATIVE_MODULES)
    redbolt_native_module_value(
        "${redbolt_wasm_native_module}" TARGET redbolt_wasm_native_module_target)
    get_target_property(redbolt_wasm_owned_static_targets
        "${redbolt_wasm_native_module_target}" REDBOLT_OWNED_STATIC_TARGETS)
    list(APPEND redbolt_wasm_static_targets
        "${redbolt_wasm_native_module_target}")
    if(redbolt_wasm_owned_static_targets)
        list(APPEND redbolt_wasm_static_targets
            ${redbolt_wasm_owned_static_targets})
    endif()
endforeach()
set(redbolt_wasm_unique_static_targets ${redbolt_wasm_static_targets})
list(REMOVE_DUPLICATES redbolt_wasm_unique_static_targets)
list(LENGTH redbolt_wasm_static_targets redbolt_wasm_static_target_count)
list(LENGTH redbolt_wasm_unique_static_targets redbolt_wasm_unique_static_target_count)
if(NOT redbolt_wasm_static_target_count EQUAL redbolt_wasm_unique_static_target_count)
    message(FATAL_ERROR "Web archive ownership contains duplicate static targets")
endif()
set(REDBOLT_WASM_ENGINE_ARCHIVE "${CMAKE_BINARY_DIR}/lib/Release/wasm/RedBolt.Native.Web.a")
set(REDBOLT_WASM_MRI_FILE "${CMAKE_BINARY_DIR}/redbolt-wasm-archives.mri")
set(redbolt_wasm_mri "create ${REDBOLT_WASM_ENGINE_ARCHIVE}\n")
foreach(target IN LISTS redbolt_wasm_static_targets)
    string(APPEND redbolt_wasm_mri "addlib $<TARGET_FILE:${target}>\n")
endforeach()
string(APPEND redbolt_wasm_mri "save\nend\n")
file(GENERATE OUTPUT "${REDBOLT_WASM_MRI_FILE}" CONTENT "${redbolt_wasm_mri}")

set(REDBOLT_WASM_RESOURCE_MANIFEST "${CMAKE_BINARY_DIR}/generated/web/resources.manifest.json")
file(MAKE_DIRECTORY "${CMAKE_BINARY_DIR}/generated/web")
file(WRITE "${REDBOLT_WASM_RESOURCE_MANIFEST}"
    "[\n"
    "  {\"name\":\"../resources/CoreData.pak\",\"virtualPath\":\"CoreData.pak\"},\n"
    "  {\"name\":\"../resources/Data.pak\",\"virtualPath\":\"Data.pak\"}\n"
    "]\n")

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
