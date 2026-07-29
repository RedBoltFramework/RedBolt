namespace RedBolt.Generator.Dependencies;

internal sealed class SpirVOptimizerGenerator(string nativeLibraryName)
    : BindingTargetGenerator(
        "spirvoptimizer",
        "RedBolt.Bindings.SpirVTools",
        "RedBolt.Bindings.SpirVTools",
        "SpirVOptimizerNativeBindings",
        nativeLibraryName,
        "redbolt_spirvoptimizer",
        "-xc++",
        "-std=c++20",
        true);
