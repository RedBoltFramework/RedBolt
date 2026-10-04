# Copyright (c) 2026 RedBolt and contributors.
# This work is licensed under the terms of the MIT license.
# For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

set(_redbolt_native_module_manifest_script_directory "${CMAKE_CURRENT_LIST_DIR}")

# Runtime libraries a native module may import without shipping them itself.
# Toolchain files are copied next to the module by the build and listed in its
# manifest; system files are provided by the platform and never listed.
set(REDBOLT_NATIVE_MODULE_TOOLCHAIN_RUNTIME_NAMES)
set(REDBOLT_NATIVE_MODULE_SYSTEM_RUNTIME_NAMES)
set(REDBOLT_NATIVE_MODULE_RUNTIME_DEPENDENCY_ARGUMENTS)
if(ANDROID)
    list(APPEND REDBOLT_NATIVE_MODULE_TOOLCHAIN_RUNTIME_NAMES libc++_shared.so)
    set(REDBOLT_NATIVE_MODULE_SYSTEM_RUNTIME_NAMES
        libc.so
        libdl.so
        libm.so
        liblog.so
        libandroid.so
        libEGL.so
        libGLESv1_CM.so
        libGLESv2.so
        libGLESv3.so
        libOpenSLES.so)
endif()
if(MINGW)
    if(CMAKE_CXX_COMPILER_ID MATCHES "Clang")
        list(APPEND REDBOLT_NATIVE_MODULE_TOOLCHAIN_RUNTIME_NAMES
            libc++.dll
            libunwind.dll
            libwinpthread-1.dll)
    else()
        list(APPEND REDBOLT_NATIVE_MODULE_TOOLCHAIN_RUNTIME_NAMES
            libgcc_s_seh-1.dll
            libstdc++-6.dll
            libwinpthread-1.dll)
    endif()
endif()
if(WIN32)
    list(APPEND REDBOLT_NATIVE_MODULE_RUNTIME_DEPENDENCY_ARGUMENTS
        "-DCMAKE_GET_RUNTIME_DEPENDENCIES_PLATFORM=windows+pe"
        "-DCMAKE_GET_RUNTIME_DEPENDENCIES_TOOL=objdump"
        "-DCMAKE_GET_RUNTIME_DEPENDENCIES_COMMAND=${CMAKE_OBJDUMP}")
elseif(ANDROID)
    list(APPEND REDBOLT_NATIVE_MODULE_RUNTIME_DEPENDENCY_ARGUMENTS
        "-DCMAKE_GET_RUNTIME_DEPENDENCIES_PLATFORM=linux+elf"
        "-DCMAKE_GET_RUNTIME_DEPENDENCIES_TOOL=objdump"
        "-DCMAKE_GET_RUNTIME_DEPENDENCIES_COMMAND=${CMAKE_OBJDUMP}")
endif()

# Writes <OUTPUT_DIR>/<MODULE>.json after TARGET is linked, listing the native
# library and the runtime files it imports. Creates the <TARGET>-module target.
function(redbolt_write_native_module_manifest)
    if(EMSCRIPTEN)
        return()
    endif()
    cmake_parse_arguments(PARSE_ARGV 0 ARG ""
        "MODULE;TARGET;SELECTOR;LIBRARY_NAME;OUTPUT_DIR"
        "REQUIRES;REQUIREMENT_CLOSURE;WRAPPERS;RUNTIME_ARTIFACTS")
    foreach(_redbolt_required_argument IN ITEMS
            MODULE TARGET SELECTOR LIBRARY_NAME OUTPUT_DIR)
        if(NOT ARG_${_redbolt_required_argument})
            message(FATAL_ERROR
                "redbolt_write_native_module_manifest requires ${_redbolt_required_argument}")
        endif()
    endforeach()

    string(REPLACE ";" "," _redbolt_requires_csv "${ARG_REQUIRES}")
    string(REPLACE ";" "," _redbolt_requirement_closure_csv
        "${ARG_REQUIREMENT_CLOSURE}")
    string(REPLACE ";" "," _redbolt_wrappers_csv "${ARG_WRAPPERS}")
    string(REPLACE ";" "," _redbolt_runtime_artifacts_csv
        "${ARG_RUNTIME_ARTIFACTS}")
    string(REPLACE ";" "," _redbolt_toolchain_runtime_files_csv
        "${REDBOLT_NATIVE_MODULE_TOOLCHAIN_RUNTIME_NAMES}")
    string(REPLACE ";" "," _redbolt_system_runtime_files_csv
        "${REDBOLT_NATIVE_MODULE_SYSTEM_RUNTIME_NAMES}")

    set(_redbolt_manifest_script
        "${_redbolt_native_module_manifest_script_directory}/WriteNativeModuleManifest.cmake")
    set(_redbolt_manifest_file "${ARG_OUTPUT_DIR}/${ARG_MODULE}.json")
    add_custom_command(
        OUTPUT "${_redbolt_manifest_file}"
        COMMAND "${CMAKE_COMMAND}"
            "-DREDBOLT_NATIVE_LIBRARY_FILE=$<TARGET_FILE:${ARG_TARGET}>"
            "-DREDBOLT_MANIFEST_FILE=${_redbolt_manifest_file}"
            "-DREDBOLT_MODULE=${ARG_MODULE}"
            "-DREDBOLT_MANAGED_SELECTOR=${ARG_SELECTOR}"
            "-DREDBOLT_NATIVE_LIBRARY_NAME=${ARG_LIBRARY_NAME}"
            "-DREDBOLT_REQUIRES=${_redbolt_requires_csv}"
            "-DREDBOLT_REQUIREMENT_CLOSURE=${_redbolt_requirement_closure_csv}"
            "-DREDBOLT_NATIVE_WRAPPERS=${_redbolt_wrappers_csv}"
            "-DREDBOLT_NATIVE_RUNTIME_ARTIFACTS=${_redbolt_runtime_artifacts_csv}"
            "-DREDBOLT_TOOLCHAIN_RUNTIME_FILES=${_redbolt_toolchain_runtime_files_csv}"
            "-DREDBOLT_SYSTEM_RUNTIME_FILES=${_redbolt_system_runtime_files_csv}"
            ${REDBOLT_NATIVE_MODULE_RUNTIME_DEPENDENCY_ARGUMENTS}
            -P "${_redbolt_manifest_script}"
        # Required modules may come from another build tree, so their manifests are not dependencies.
        DEPENDS "${ARG_TARGET}" "${_redbolt_manifest_script}"
        COMMENT "Writing ${ARG_MODULE} native module manifest"
        VERBATIM)
    add_custom_target(${ARG_TARGET}-module DEPENDS "${_redbolt_manifest_file}")
endfunction()
