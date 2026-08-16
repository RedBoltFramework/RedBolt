// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Generator.Dependencies;

using System;
using System.Collections.Generic;
using System.Linq;

using SharpCpp.Model.Ast;

internal sealed class WebPGenerator(string nativeLibraryName)
    : BindingTargetGenerator(
        "webp",
        "RedBolt.Bindings.WebP",
        "RedBolt.Bindings.WebP",
        "WebP",
        nativeLibraryName,
        "redbolt_webp",
        "-xc++",
        "-std=c++20",
        false)
{
    private static readonly HashSet<string> RetainedFunctions = new(StringComparer.Ordinal)
    {
        "WebPGetFeaturesInternal",
        "WebPDecodeRGBInto",
        "WebPDecodeRGBAInto",
        "WebPConfigInitInternal",
        "WebPPictureInitInternal",
        "WebPPictureImportRGB",
        "WebPPictureImportRGBA",
        "WebPPictureFree",
        "WebPMemoryWriterInit",
        "WebPMemoryWrite",
        "WebPMemoryWriterClear",
        "WebPEncode"
    };

    private static readonly Dictionary<string, HashSet<string>> RetainedRecordFields =
        new(StringComparer.Ordinal)
        {
            ["WebPBitstreamFeatures"] = new(StringComparer.Ordinal) { "width", "height", "has_alpha" },
            ["WebPConfig"] = new(StringComparer.Ordinal) { "lossless", "exact" },
            ["WebPPicture"] = new(StringComparer.Ordinal)
            {
                "use_argb",
                "width",
                "height",
                "writer",
                "custom_ptr",
                "error_code"
            },
            ["WebPMemoryWriter"] = new(StringComparer.Ordinal) { "mem", "size" }
        };

    private static readonly HashSet<string> RetainedEnums = new(StringComparer.Ordinal)
    {
        "VP8StatusCode",
        "WebPEncodingError",
        "WebPPreset"
    };

    protected override void PostProcessCompilation(BindingCompilation compilation)
    {
        compilation.Functions.First(static function => function.NativeName == "WebPMemoryWrite")
            .SetMetadata(MetadataKeys.ExposeNativeFunctionPointer, true);
    }

    protected override int PruneCompilation(BindingCompilation compilation)
    {
        var prunedCount = 0;
        for (var index = compilation.Members.Count - 1; index >= 0; --index)
        {
            var member = compilation.Members[index];
            if (member is FunctionBinding function &&
                (RetainedFunctions.Contains(function.NativeName) || RetainedFunctions.Contains(function.ManagedName)))
            {
                continue;
            }

            if (member is RecordBinding record &&
                RetainedRecordFields.TryGetValue(record.NativeName, out var retainedFields))
            {
                for (var memberIndex = record.Members.Count - 1; memberIndex >= 0; --memberIndex)
                {
                    if (record.Members[memberIndex] is FieldBinding field &&
                        retainedFields.Contains(field.NativeName) ||
                        record.Members[memberIndex] is FunctionBinding { IsConstructor: true })
                    {
                        continue;
                    }

                    record.Members.RemoveAt(memberIndex);
                    prunedCount++;
                }
                continue;
            }

            if (member is EnumBinding @enum &&
                (RetainedEnums.Contains(@enum.NativeName) || RetainedEnums.Contains(@enum.ManagedName)))
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
