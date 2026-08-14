namespace RedBolt.Generator.Dependencies;

internal sealed class SpirVToolsGenerator(string nativeLibraryName)
    : BindingTargetGenerator(
        "spirvtools",
        "RedBolt.Bindings.SpirVTools",
        "RedBolt.Bindings.SpirVTools",
        "SpirVTools",
        nativeLibraryName,
        "redbolt_spirvtools",
        "-xc",
        "-std=c17",
        false);
