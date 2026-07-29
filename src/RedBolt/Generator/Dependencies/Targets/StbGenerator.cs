namespace RedBolt.Generator.Dependencies;

using System.Linq;

using SharpCpp.Model.Ast;

internal sealed class StbGenerator(string nativeLibraryName)
    : BindingTargetGenerator(
        "stb",
        "RedBolt.Bindings.Stb",
        "RedBolt.Bindings.Stb",
        "StbVorbisNativeBindings",
        nativeLibraryName,
        "redbolt_stb",
        "-xc",
        "-std=c17",
        true)
{
    protected override string MarshallerClangLanguageArgument => "-xc++";

    protected override string MarshallerLanguageStandardArgument => "-std=c++20";

    protected override void PostProcessCompilation(BindingCompilation compilation)
    {
        var function = compilation.Functions.FirstOrDefault(static item =>
            item.NativeName == "stb_vorbis_get_samples_short_interleaved");
        if (function is null || function.Parameters.Count != 4)
            return;

        function.Parameters[2].Type = BindingType.IntPtr;
        function.Parameters[2].Direction = BindingParameterDirection.In;
        function.Parameters[2].AbiKind = BindingParameterAbiKind.Value;
    }
}
