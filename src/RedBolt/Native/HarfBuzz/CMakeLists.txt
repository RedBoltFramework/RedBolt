# Copyright (c) 2026 RedBolt and contributors.
# This work is licensed under the terms of the MIT license.
# For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

redbolt_add_native_binding_generation(
    TARGET redbolt-generate-harfbuzz-bindings
    SURFACE harfbuzz
    HEADER "${REDBOLT_HARFBUZZ_PARSE_HEADER}"
    HEADER_INCLUDE_PATH "HarfBuzzAll.h"
    FILE_STEM "${REDBOLT_HARFBUZZ_BINDINGS_STEM}"
    OUTPUT_DIR "${REDBOLT_HARFBUZZ_GENERATED_DIR}"
    OUTPUT_METADATA "${REDBOLT_HARFBUZZ_BINDINGS_JSON}"
    OUTPUT_CS "${REDBOLT_HARFBUZZ_BINDINGS_CS}"
    OUTPUT_CPP "${REDBOLT_HARFBUZZ_BINDINGS_CPP}"
    NATIVE_LIBRARY_NAME "${REDBOLT_HARFBUZZ_NATIVE_LIBRARY_NAME}"
    INCLUDE_DIRS "${REDBOLT_HARFBUZZ_SOURCE_ROOT}/src"
    OWNED_HEADERS "${REDBOLT_HARFBUZZ_PARSE_HEADER}" "${REDBOLT_HARFBUZZ_SOURCE_ROOT}/src")

add_dependencies(redbolt-generate-harfbuzz-bindings harfbuzz harfbuzz-raster harfbuzz-gpu)
