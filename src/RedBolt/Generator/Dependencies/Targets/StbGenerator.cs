namespace RedBolt.Generator.Dependencies;

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
}
