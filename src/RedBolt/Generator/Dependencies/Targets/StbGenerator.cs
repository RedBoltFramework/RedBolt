// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Generator.Dependencies;

internal sealed class StbGenerator(string nativeLibraryName)
    : BindingTargetGenerator(
        "stb",
        "RedBolt.Bindings.Stb",
        "RedBolt.Bindings.Stb",
        "Stb",
        nativeLibraryName,
        "redbolt_stb",
        "-xc",
        "-std=c17",
        true)
{
    protected override string MarshallerClangLanguageArgument => "-xc++";

    protected override string MarshallerLanguageStandardArgument => "-std=c++20";
}
