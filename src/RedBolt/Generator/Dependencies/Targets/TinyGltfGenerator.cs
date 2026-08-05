namespace RedBolt.Generator.Dependencies;

using System;
using System.Collections.Generic;

using SharpCpp.Model.Ast;

internal sealed class TinyGltfGenerator(string nativeLibraryName)
    : BindingTargetGenerator(
        "tinygltf",
        "RedBolt.Bindings.TinyGltf",
        "RedBolt.Bindings.TinyGltf",
        "TinyGltfNativeBindings",
        nativeLibraryName,
        "redbolt_tinygltf",
        "-xc++",
        "-std=c++20",
        true)
{
    protected override bool RetainStdlibWrapper(RecordBinding record) =>
        record.ManagedNamespace == "Std" && record.ManagedName is "Map" or "String" or "Vector";

    protected override IReadOnlyDictionary<string, string> NamespaceMappings { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["tinygltf"] = "RedBolt.Bindings.TinyGltf"
        };

}
