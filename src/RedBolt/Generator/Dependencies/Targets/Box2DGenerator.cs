namespace RedBolt.Generator.Dependencies;

internal sealed class Box2DGenerator(string nativeLibraryName)
    : BindingTargetGenerator(
        "box2d",
        "RedBolt.Bindings.Box2D",
        "RedBolt.Bindings.Box2D",
        "Box2DNativeBindings",
        nativeLibraryName,
        "redbolt_box2d",
        "-xc",
        "-std=c17",
        true)
{
    protected override string MarshallerClangLanguageArgument => "-xc++";

    protected override string MarshallerLanguageStandardArgument => "-std=c++20";

    protected override bool IncludeCallbackReleaseRegistration => true;
}
