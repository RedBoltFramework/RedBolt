// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Generator.Dependencies;

internal sealed class GlslangGenerator(string nativeLibraryName)
    : BindingTargetGenerator(
        "glslang",
        "RedBolt.Bindings.Glslang",
        "RedBolt.Bindings.Glslang",
        "Glslang",
        nativeLibraryName,
        "redbolt_glslang",
        "-xc",
        "-std=c17",
        false)
{
}
