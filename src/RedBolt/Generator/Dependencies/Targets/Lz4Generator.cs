// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Generator.Dependencies;

using System;
using System.Collections.Generic;

using SharpCpp.Model.Ast;

internal sealed class Lz4Generator(string nativeLibraryName)
    : BindingTargetGenerator(
        "lz4",
        "RedBolt.Bindings.Lz4",
        "RedBolt.Bindings.Lz4",
        "Lz4",
        nativeLibraryName,
        "redbolt_lz4",
        "-xc++",
        "-std=c++20",
        false)
{
    private static readonly HashSet<string> RetainedFunctions = new(StringComparer.Ordinal)
    {
        "LZ4_compressBound",
        "LZ4_compress_HC",
        "LZ4_decompress_fast",
        "LZ4_versionNumber"
    };

    protected override int PruneCompilation(BindingCompilation compilation)
    {
        var prunedCount = 0;
        for (var index = compilation.Members.Count - 1; index >= 0; --index)
        {
            if (compilation.Members[index] is FunctionBinding function &&
                RetainedFunctions.Contains(function.NativeName))
            {
                continue;
            }

            compilation.Members.RemoveAt(index);
            prunedCount++;
        }
        compilation.InvalidateCaches();
        return prunedCount;
    }
}
