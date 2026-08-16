# Copyright (c) 2026 RedBolt and contributors.
# This work is licensed under the terms of the MIT license.
# For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

redbolt_add_native_binding_generation(
    TARGET redbolt-generate-glslang-bindings
    SURFACE glslang
    HEADER "${REDBOLT_GLSLANG_C_HEADER}"
    HEADER_INCLUDE_PATH "glslang/Public/resource_limits_c.h"
    FILE_STEM "${REDBOLT_GLSLANG_BINDINGS_STEM}"
    OUTPUT_DIR "${REDBOLT_GLSLANG_GENERATED_DIR}"
    OUTPUT_METADATA "${REDBOLT_GLSLANG_BINDINGS_JSON}"
    OUTPUT_CS "${REDBOLT_GLSLANG_BINDINGS_CS}"
    OUTPUT_CPP "${REDBOLT_GLSLANG_BINDINGS_CPP}"
    NATIVE_LIBRARY_NAME "${REDBOLT_GLSLANG_NATIVE_LIBRARY_NAME}"
    INCLUDE_DIRS "${REDBOLT_GLSLANG_SOURCE_ROOT}"
    OWNED_HEADERS "${REDBOLT_GLSLANG_SOURCE_ROOT}/glslang/Include" "${REDBOLT_GLSLANG_SOURCE_ROOT}/glslang/Public")
