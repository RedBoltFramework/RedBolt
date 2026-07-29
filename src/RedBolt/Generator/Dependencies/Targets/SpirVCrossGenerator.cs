namespace RedBolt.Generator.Dependencies;

internal sealed class SpirVCrossGenerator(string nativeLibraryName)
    : BindingTargetGenerator(
        "spirvcross",
        "RedBolt.Bindings.SpirVCross",
        "RedBolt.Bindings.SpirVCross",
        "SpirVCrossNativeBindings",
        nativeLibraryName,
        "redbolt_spirvcross",
        "-xc",
        "-std=c17",
        false);
