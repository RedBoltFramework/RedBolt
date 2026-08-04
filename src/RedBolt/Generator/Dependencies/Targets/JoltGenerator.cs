namespace RedBolt.Generator.Dependencies;

using System;
using System.Collections.Generic;
using System.Linq;

using SharpCpp.Model.Ast;

internal sealed class JoltGenerator(string nativeLibraryName)
    : BindingTargetGenerator(
        "jolt",
        "RedBolt.Bindings.Jolt",
        "RedBolt.Bindings.Jolt",
        "JoltNativeBindings",
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
        NormalizeBVec16UInt64Array(compilation);
    }

    protected override IEnumerable<string> GetAdditionalMarshallerHeaders(GeneratorOptions options)
    {
        yield return System.IO.Path.Combine(
            options.RedBoltRoot, "src", "RedBolt", "Native", "include", "RedBolt", "JoltMarshallers.hpp");
    }

    private static void NormalizeBVec16UInt64Array(BindingCompilation compilation)
    {
        foreach (var record in compilation.Records)
        {
            foreach (var field in record.Members.OfType<FieldBinding>())
                field.Type = NormalizeBVec16UInt64Array(field);
        }
    }

    private static BindingType NormalizeBVec16UInt64Array(FieldBinding field)
    {
        if (field.OwningRecordCppTypeName != "JPH::BVec16" ||
            field.NativeName != "mU64" ||
            field.Type.CppTypeName != "NativeArray<unsigned long, 2>*" ||
            field.Type.FixedArrayLength != 2 ||
            field.Type.FixedArrayElementType is not
            {
                Kind: BindingTypeKind.Primitive,
                PrimitiveKind: BindingPrimitiveKind.NativeUInt,
                CppTypeName: "unsigned long"
            } elementType ||
            field.Type.GenericArguments.Count != 1 ||
            field.Type.GenericArguments[0] is not
            {
                Kind: BindingTypeKind.Primitive,
                PrimitiveKind: BindingPrimitiveKind.NativeUInt,
                CppTypeName: "unsigned long"
            } genericElementType)
        {
            return field.Type;
        }

        var projectedElementType = FixedWidthAliasNormalizer.ProjectType(
            elementType,
            new FixedWidthAliasProjection(BindingPrimitiveKind.UInt64, "ulong"));
        var projectedGenericElementType = FixedWidthAliasNormalizer.ProjectType(
            genericElementType,
            new FixedWidthAliasProjection(BindingPrimitiveKind.UInt64, "ulong"));
        return field.Type
            .WithGenericArguments([projectedGenericElementType])
            .WithFixedArray(projectedElementType, 2);
    }

}
