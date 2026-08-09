namespace RedBolt.Generator.Dependencies;

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
