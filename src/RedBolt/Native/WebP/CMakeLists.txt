# Copyright (c) 2026 RedBolt and contributors.
# This work is licensed under the terms of the MIT license.
# For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

redbolt_add_native_binding_generation(
    TARGET redbolt-generate-webp-bindings
    SURFACE webp
    HEADER "${REDBOLT_WEBP_PARSE_HEADER}"
    HEADER_INCLUDE_PATH "WebPAll.hpp"
    FILE_STEM "${REDBOLT_WEBP_BINDINGS_STEM}"
    OUTPUT_DIR "${REDBOLT_WEBP_GENERATED_DIR}"
    OUTPUT_METADATA "${REDBOLT_WEBP_BINDINGS_JSON}"
    OUTPUT_CS "${REDBOLT_WEBP_BINDINGS_CS}"
    OUTPUT_CPP "${REDBOLT_WEBP_BINDINGS_CPP}"
    NATIVE_LIBRARY_NAME "${REDBOLT_WEBP_NATIVE_LIBRARY_NAME}"
    INCLUDE_DIRS "${REDBOLT_WEBP_INCLUDE_DIR}"
    OWNED_HEADERS "${REDBOLT_WEBP_PARSE_HEADER}" "${REDBOLT_WEBP_INCLUDE_DIR}/webp/decode.h" "${REDBOLT_WEBP_INCLUDE_DIR}/webp/encode.h" "${REDBOLT_WEBP_INCLUDE_DIR}/webp/types.h")
