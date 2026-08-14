namespace RedBolt.Generator.Dependencies;

using System;
using System.Collections.Generic;

using SharpCpp.Model.Ast;

internal sealed class XAtlasGenerator(string nativeLibraryName)
    : BindingTargetGenerator(
        "xatlas",
        "RedBolt.Bindings.XAtlas",
        "RedBolt.Bindings.XAtlas",
        "XAtlas",
        nativeLibraryName,
        "redbolt_xatlas",
        "-xc++",
        "-std=c++20",
        true)
{
    private static readonly HashSet<string> RetainedFunctions = new(StringComparer.Ordinal)
    {
        "Create",
        "Destroy",
        "AddMesh",
        "AddMeshJoin",
        "Generate"
    };

    private static readonly HashSet<string> RetainedEnums = new(StringComparer.Ordinal)
    {
        "AddMeshError",
        "IndexFormat"
    };

    private static readonly Dictionary<string, HashSet<string>> RetainedRecordFields =
        new(StringComparer.Ordinal)
        {
            ["Vertex"] = new(StringComparer.Ordinal) { "uv", "xref" },
            ["Mesh"] = new(StringComparer.Ordinal) { "indexArray", "indexCount", "vertexArray", "vertexCount" },
            ["Atlas"] = new(StringComparer.Ordinal) { "width", "height", "meshCount", "meshes" },
            ["MeshDecl"] = new(StringComparer.Ordinal)
            {
                "vertexCount",
                "vertexPositionData",
                "vertexPositionStride",
                "vertexNormalData",
                "vertexNormalStride",
                "indexCount",
                "indexData",
                "indexFormat"
            },
            ["ChartOptions"] = new(StringComparer.Ordinal),
            ["PackOptions"] = new(StringComparer.Ordinal) { "padding", "texelsPerUnit" }
        };

    protected override IReadOnlyDictionary<string, string> NamespaceMappings { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["xatlas"] = "RedBolt.Bindings.XAtlas"
        };

    protected override int PruneCompilation(BindingCompilation compilation)
    {
        var prunedCount = 0;
        for (var index = compilation.Members.Count - 1; index >= 0; --index)
        {
            var member = compilation.Members[index];
            if (member is FunctionBinding function && RetainedFunctions.Contains(function.NativeName) ||
                member is EnumBinding @enum && RetainedEnums.Contains(@enum.ManagedName) ||
                member is RecordBinding nativeArray && nativeArray.HasMetadata(MetadataKeys.NativeArrayRecord))
            {
                continue;
            }

            if (member is RecordBinding record &&
                RetainedRecordFields.TryGetValue(record.NativeName, out var retainedFields))
            {
                for (var memberIndex = record.Members.Count - 1; memberIndex >= 0; --memberIndex)
                {
                    if (record.Members[memberIndex] is FieldBinding field &&
                        retainedFields.Contains(field.NativeName) ||
                        record.Members[memberIndex] is FunctionBinding { IsConstructor: true } or EnumBinding)
                    {
                        continue;
                    }

                    record.Members.RemoveAt(memberIndex);
                    prunedCount++;
                }
                continue;
            }

            compilation.Members.RemoveAt(index);
            prunedCount++;
        }

        compilation.InvalidateCaches();
        return prunedCount;
    }
}
