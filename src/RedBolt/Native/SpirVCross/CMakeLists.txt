# Copyright (c) 2026 RedBolt and contributors.
# This work is licensed under the terms of the MIT license.
# For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

redbolt_add_native_binding_generation(
    TARGET redbolt-generate-spirv-cross-bindings
    SURFACE spirvcross
    HEADER "${REDBOLT_SPIRV_CROSS_C_HEADER}"
    HEADER_INCLUDE_PATH "spirv_cross_c.h"
    FILE_STEM "${REDBOLT_SPIRV_CROSS_BINDINGS_STEM}"
    OUTPUT_DIR "${REDBOLT_SPIRV_CROSS_GENERATED_DIR}"
    OUTPUT_METADATA "${REDBOLT_SPIRV_CROSS_BINDINGS_JSON}"
    OUTPUT_CS "${REDBOLT_SPIRV_CROSS_BINDINGS_CS}"
    OUTPUT_CPP "${REDBOLT_SPIRV_CROSS_BINDINGS_CPP}"
    NATIVE_LIBRARY_NAME "${REDBOLT_SPIRV_CROSS_NATIVE_LIBRARY_NAME}"
    INCLUDE_DIRS "${REDBOLT_SPIRV_CROSS_SOURCE_ROOT}"
    OWNED_HEADERS "${REDBOLT_SPIRV_CROSS_SOURCE_ROOT}")
