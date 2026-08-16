# Copyright (c) 2026 RedBolt and contributors.
# This work is licensed under the terms of the MIT license.
# For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

file(GENERATE
    OUTPUT "${CMAKE_CURRENT_BINARY_DIR}/RedBoltDiligentBindingConfiguration.txt"
    CONTENT "REDBOLT_ENABLE_OPENXR=${REDBOLT_ENABLE_OPENXR}\n")

redbolt_add_native_binding_generation(
    TARGET redbolt-generate-diligent-bindings
    SURFACE diligent
    HEADER "${REDBOLT_DILIGENT_PARSE_HEADER}"
    HEADER_INCLUDE_PATH "${REDBOLT_DILIGENT_PARSE_HEADER}"
    FILE_STEM "${REDBOLT_DILIGENT_BINDINGS_STEM}"
    OUTPUT_DIR "${REDBOLT_DILIGENT_GENERATED_DIR}"
    OUTPUT_METADATA "${REDBOLT_DILIGENT_BINDINGS_JSON}"
    OUTPUT_CS "${REDBOLT_DILIGENT_BINDINGS_CS}"
    OUTPUT_CPP "${REDBOLT_DILIGENT_BINDINGS_CPP}"
    NATIVE_LIBRARY_NAME "${REDBOLT_DILIGENT_NATIVE_LIBRARY_NAME}"
    INCLUDE_DIRS
        ${REDBOLT_DILIGENT_INCLUDE_DIRS}
        ${REDBOLT_DILIGENT_OPENXR_INCLUDE_DIRS}
        "${REDBOLT_DILIGENT_SUPPORT_INCLUDE_DIR}"
    OWNED_HEADERS ${REDBOLT_DILIGENT_OWNED_HEADERS}
    DEPENDS "${CMAKE_CURRENT_BINARY_DIR}/RedBoltDiligentBindingConfiguration.txt")

add_dependencies(redbolt-generate-diligent-bindings redbolt-generate-sdl-bindings redbolt-diligent-build)
if(REDBOLT_ENABLE_OPENXR AND TARGET generate_openxr_header)
    add_dependencies(redbolt-generate-diligent-bindings generate_openxr_header)
endif()
