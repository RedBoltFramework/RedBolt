namespace RedBolt.Generator.Dependencies;

using System;
using System.Collections.Generic;

using SharpCpp.Model.Ast;

internal sealed class JoltGenerator(string nativeLibraryName)
    : BindingTargetGenerator(
        "jolt",
        "RedBolt.Bindings.Jolt",
        "RedBolt.Bindings.Jolt",
        "Jolt",
        nativeLibraryName,
        "redbolt_jolt",
        "-xc++",
        "-std=c++20",
        true)
{
    private static readonly HashSet<string> DirectorRecords = new(StringComparer.Ordinal)
    {
        "JPH::ContactListener",
        "JPH::ObjectLayerFilter",
        "JPH::ObjectLayerPairFilter"
    };

    private static readonly IReadOnlyDictionary<string, FixedWidthAliasProjection> FixedWidthAliases =
        new Dictionary<string, FixedWidthAliasProjection>(StringComparer.Ordinal)
        {
            ["JPH::uint64"] = new(BindingPrimitiveKind.UInt64, "ulong"),
            ["JPH::int64"] = new(BindingPrimitiveKind.Int64, "long")
        };

    protected override IReadOnlyDictionary<string, string> NamespaceMappings { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["JPH"] = "RedBolt.Bindings.Jolt"
        };

    protected override IReadOnlySet<string>? DirectorRecordCppTypeNames => DirectorRecords;

    // Jolt exposes director callbacks as well; keep the callback lifetime
    // support local to this optional wrapper rather than relying on SDL.
    protected override bool IncludeCallbackReleaseRegistration => true;

    protected override void ConfigureBeforeValidation(BindingCompilation compilation)
    {
        FixedWidthAliasNormalizer.Normalize(compilation, FixedWidthAliases);
    }

    protected override IEnumerable<string> GetAdditionalMarshallerHeaders(GeneratorOptions options)
    {
        yield return System.IO.Path.Combine(
            options.RedBoltRoot, "src", "RedBolt", "Native", "JoltPhysics", "JoltMarshallers.hpp");
    }

}
