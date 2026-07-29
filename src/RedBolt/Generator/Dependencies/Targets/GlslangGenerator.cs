namespace RedBolt.Generator.Dependencies;

internal sealed class GlslangGenerator(string nativeLibraryName)
    : BindingTargetGenerator(
        "glslang",
        "RedBolt.Bindings.Glslang",
        "RedBolt.Bindings.Glslang",
        "GlslangNativeBindings",
        nativeLibraryName,
        "redbolt_glslang",
        "-xc",
        "-std=c17",
        false)
{
}
