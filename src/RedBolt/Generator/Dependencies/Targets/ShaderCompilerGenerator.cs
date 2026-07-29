namespace RedBolt.Generator.Dependencies;

internal sealed class ShaderCompilerGenerator(string nativeLibraryName)
    : BindingTargetGenerator(
        "shadercompiler",
        "RedBolt.Bindings.ShaderCompiler",
        "RedBolt.Bindings.ShaderCompiler",
        "ShaderCompilerNativeBindings",
        nativeLibraryName,
        "redbolt_shadercompiler",
        "-xc++",
        "-std=c++20",
        true);
