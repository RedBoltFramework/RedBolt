// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Generator.Dependencies;

internal sealed class SpirVCrossGenerator(string nativeLibraryName)
    : BindingTargetGenerator(
        "spirvcross",
        "RedBolt.Bindings.SpirVCross",
        "RedBolt.Bindings.SpirVCross",
        "SpirVCross",
        nativeLibraryName,
        "redbolt_spirvcross",
        "-xc",
        "-std=c17",
        false);
