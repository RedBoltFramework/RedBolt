// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Generator.Dependencies;

using System.Linq;

using SharpCpp.Model.Ast;

internal sealed class HarfBuzzGenerator(string nativeLibraryName)
    : BindingTargetGenerator(
        "harfbuzz",
        "RedBolt.Bindings.HarfBuzz",
        "RedBolt.Bindings.HarfBuzz",
        "HarfBuzz",
        nativeLibraryName,
        "redbolt_harfbuzz",
        "-xc",
        "-std=c17",
        false)
{
    private static readonly string[] NullTerminatedShaperListFunctions =
    [
        "hb_shape_full",
        "hb_shape_plan_create",
        "hb_shape_plan_create_cached",
        "hb_shape_plan_create2",
        "hb_shape_plan_create_cached2",
    ];

    // The wrapper appends the NULL terminator HarfBuzz requires after the shaper names.
    protected override void ConfigureBeforeValidation(BindingCompilation compilation)
    {
        foreach (var name in NullTerminatedShaperListFunctions)
        {
            compilation.Functions.First(function => function.NativeName == name)
                .Parameters.First(static parameter => parameter.NativeName == "shaper_list")
                .SetMetadata(MetadataKeys.NullTerminatedStringArray, true);
        }
    }

    protected override void PostProcessCompilation(BindingCompilation compilation)
    {
        var createBlob = compilation.Functions.First(static function =>
            function.NativeName == "hb_blob_create_from_file_or_fail");
        var fileName = createBlob.Parameters.First(static parameter => parameter.NativeName == "file_name");
        fileName.Type = BindingType.CreateConfigured("const char*", "string");
        fileName.Direction = BindingParameterDirection.In;
        fileName.AbiKind = BindingParameterAbiKind.Value;
        fileName.CppTypeName = "const char*";
    }
}
