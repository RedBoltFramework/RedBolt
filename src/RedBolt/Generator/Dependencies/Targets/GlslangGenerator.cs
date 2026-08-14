namespace RedBolt.Generator.Dependencies;

internal sealed class GlslangGenerator(string nativeLibraryName)
    : BindingTargetGenerator(
        "glslang",
        "RedBolt.Bindings.Glslang",
        "RedBolt.Bindings.Glslang",
        "Glslang",
        nativeLibraryName,
        "redbolt_glslang",
        "-xc",
        "-std=c17",
        false)
{
}
