namespace RedBolt.Generator.Dependencies;

internal sealed class LibDataChannelGenerator(string nativeLibraryName)
    : BindingTargetGenerator(
        "libdatachannel",
        "RedBolt.Bindings.LibDataChannel",
        "RedBolt.Bindings.LibDataChannel",
        "LibDataChannel",
        nativeLibraryName,
        "redbolt_libdatachannel",
        "-xc",
        "-std=c17",
        true)
{
    protected override string MarshallerClangLanguageArgument => "-xc++";

    protected override string MarshallerLanguageStandardArgument => "-std=c++20";

    protected override bool IncludeCallbackReleaseRegistration => true;
}
