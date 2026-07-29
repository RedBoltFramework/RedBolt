namespace RedBolt.Generator.Dependencies;

using System;
using System.Collections.Generic;
using System.Linq;

using SharpCpp.Model.Ast;

internal sealed class HarfBuzzGenerator(string nativeLibraryName)
    : BindingTargetGenerator(
        "harfbuzz",
        "RedBolt.Bindings.HarfBuzz",
        "RedBolt.Bindings.HarfBuzz",
        "HarfBuzzNativeBindings",
        nativeLibraryName,
        "redbolt_harfbuzz",
        "-xc",
        "-std=c17",
        false)
{
    private static readonly HashSet<string> RetainedFunctions = new(StringComparer.Ordinal)
    {
        "hb_blob_create_from_file_or_fail",
        "hb_blob_destroy",
        "hb_face_create_or_fail",
        "hb_face_destroy",
        "hb_face_get_upem",
        "hb_font_create",
        "hb_font_destroy",
        "hb_font_get_glyph",
        "hb_font_get_glyph_extents",
        "hb_font_get_glyph_h_advance",
        "hb_font_set_scale",
        "hb_ot_font_set_funcs",
        "hb_raster_draw_create_or_fail",
        "hb_raster_draw_destroy",
        "hb_raster_draw_glyph_or_fail",
        "hb_raster_draw_render",
        "hb_raster_draw_reset",
        "hb_raster_draw_set_glyph_extents",
        "hb_raster_image_destroy",
        "hb_raster_image_get_buffer",
        "hb_raster_image_get_extents",
        "hb_raster_image_get_format"
    };

    private static readonly HashSet<string> RetainedRecords = new(StringComparer.Ordinal)
    {
        "hb_blob_t",
        "hb_face_t",
        "hb_font_t",
        "hb_glyph_extents_t",
        "hb_raster_draw_t",
        "hb_raster_extents_t",
        "hb_raster_image_t"
    };

    private static readonly HashSet<string> RetainedEnums = new(StringComparer.Ordinal)
    {
        "hb_memory_mode_t",
        "hb_raster_format_t"
    };

    private static readonly HashSet<string> RetainedAliases = new(StringComparer.Ordinal)
    {
        "hb_bool_t",
        "hb_codepoint_t",
        "hb_position_t"
    };

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

    protected override int PruneCompilation(BindingCompilation compilation)
    {
        var prunedCount = 0;
        for (var index = compilation.Members.Count - 1; index >= 0; --index)
        {
            var member = compilation.Members[index];
            if (member is FunctionBinding function && RetainedFunctions.Contains(function.NativeName) ||
                member is RecordBinding record && RetainedRecords.Contains(record.NativeName) ||
                member is EnumBinding @enum && RetainedEnums.Contains(@enum.NativeName))
            {
                continue;
            }

            compilation.Members.RemoveAt(index);
            prunedCount++;
        }

        for (var index = compilation.TypeAliases.Count - 1; index >= 0; --index)
        {
            if (RetainedAliases.Contains(compilation.TypeAliases[index].NativeName))
                continue;

            compilation.TypeAliases.RemoveAt(index);
            prunedCount++;
        }

        compilation.InvalidateCaches();
        return prunedCount;
    }
}
