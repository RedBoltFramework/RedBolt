// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Generator.Dependencies;

internal sealed class SpirVToolsGenerator(string nativeLibraryName)
    : BindingTargetGenerator(
        "spirvtools",
        "RedBolt.Bindings.SpirVTools",
        "RedBolt.Bindings.SpirVTools",
        "SpirVTools",
        nativeLibraryName,
        "redbolt_spirvtools",
        "-xc",
        "-std=c17",
        false);
