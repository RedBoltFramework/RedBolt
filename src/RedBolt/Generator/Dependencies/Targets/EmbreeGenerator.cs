namespace RedBolt.Generator.Dependencies;

internal sealed class EmbreeGenerator(string nativeLibraryName)
    : BindingTargetGenerator(
        "embree",
        "RedBolt.Bindings.Embree",
        "RedBolt.Bindings.Embree",
        "Embree",
        nativeLibraryName,
        "redbolt_embree",
        "-xc",
        "-std=c17",
        true)
{

    protected override string MarshallerClangLanguageArgument => "-xc++";

    protected override string MarshallerLanguageStandardArgument => "-std=c++20";
}
