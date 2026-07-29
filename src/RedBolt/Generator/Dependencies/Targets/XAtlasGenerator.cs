namespace RedBolt.Generator.Dependencies;

using System;
using System.Collections.Generic;
using System.Linq;

using SharpCpp.Model.Ast;

internal sealed class XAtlasGenerator(string nativeLibraryName)
    : BindingTargetGenerator(
        "xatlas",
        "RedBolt.Bindings.XAtlas",
        "RedBolt.Bindings.XAtlas",
        "XAtlasNativeBindings",
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
            ["Mesh"] = new(StringComparer.Ordinal) { "indexArray", "indexCount", "Vertices", "NumVertices" },
            ["Atlas"] = new(StringComparer.Ordinal) { "width", "height", "NumMeshes", "Meshes" },
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

    protected override void PostProcessCompilation(BindingCompilation compilation)
    {
        var indexFormat = PromoteEnum(compilation, "IndexFormat");
        var addMeshError = PromoteEnum(compilation, "AddMeshError");
        compilation.InvalidateCaches();

        var meshDecl = compilation.Records.First(static record => record.NativeName == "MeshDecl");
        var indexFormatField = meshDecl.Members.OfType<FieldBinding>().First(static field =>
            field.NativeName == "indexFormat");
        indexFormatField.Type = BindingType.CreateEnum(
            indexFormat.CppQualifiedName,
            indexFormat.ManagedName,
            enumId: indexFormat.SymbolId);
        indexFormatField.CppTypeName = indexFormat.CppQualifiedName;
        indexFormatField.ClearIgnoreReason();

        var mesh = compilation.Records.First(static record => record.NativeName == "Mesh");
        var vertex = compilation.Records.First(static record => record.NativeName == "Vertex");
        var atlas = compilation.Records.First(static record => record.NativeName == "Atlas");
        var meshesField = atlas.Members.OfType<FieldBinding>().First(static field => field.NativeName == "meshes");
        var meshPointerType = BindingType.CreateHandle(
            "xatlas::Mesh*",
            meshesField.Type.ManagedTypeName,
            mesh.SymbolId,
            mesh.HandleMarshallerTypeName);
        ReplaceField(atlas, "meshes", "Meshes", meshPointerType);
        ReplaceField(atlas, "meshCount", "NumMeshes");

        var vertexArrayField = mesh.Members.OfType<FieldBinding>().First(static field =>
            field.NativeName == "vertexArray");
        var vertexPointerType = BindingType.CreateHandle(
            "xatlas::Vertex*",
            vertexArrayField.Type.ManagedTypeName,
            vertex.SymbolId,
            vertex.HandleMarshallerTypeName);
        ReplaceField(mesh, "vertexArray", "Vertices", vertexPointerType);
        ReplaceField(mesh, "vertexCount", "NumVertices");
        var indexArrayField = mesh.Members.OfType<FieldBinding>().First(static field =>
            field.NativeName == "indexArray");
        indexArrayField.Type = BindingType.IntPtr;
        indexArrayField.IsPointer = true;
        indexArrayField.ClearIgnoreReason();

        var addMesh = compilation.Functions.First(static function => function.NativeName == "AddMesh");
        addMesh.ReturnType = BindingType.CreateEnum(
            addMeshError.CppQualifiedName,
            addMeshError.ManagedName,
            enumId: addMeshError.SymbolId);
        addMesh.CppReturnTypeName = addMeshError.CppQualifiedName;
        addMesh.ClearIgnoreReason();

        var generate = compilation.Functions.First(static function => function.NativeName == "Generate");
        var parameterizeFunc = generate.Parameters.First(static parameter => parameter.NativeName == "paramFunc");
        parameterizeFunc.ClearIgnoreReason();
        generate.ClearIgnoreReason();
    }

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

    private static EnumBinding PromoteEnum(BindingCompilation compilation, string recordName)
    {
        var @enum = new EnumBinding(
            $"enum:xatlas::{recordName}::Enum",
            "Enum",
            recordName,
            cppQualifiedName: $"xatlas::{recordName}::Enum");
        EnumValueBinding[] values = recordName switch
        {
            "IndexFormat" =>
            [
                new EnumValueBinding("UInt16", "UInt16", "0"),
                new EnumValueBinding("UInt32", "UInt32", "1")
            ],
            "AddMeshError" =>
            [
                new EnumValueBinding("Success", "Success", "0"),
                new EnumValueBinding("Error", "Error", "1"),
                new EnumValueBinding("IndexOutOfRange", "IndexOutOfRange", "2"),
                new EnumValueBinding("InvalidIndexCount", "InvalidIndexCount", "3")
            ],
            _ => throw new ArgumentOutOfRangeException(nameof(recordName))
        };
        foreach (var value in values)
            @enum.Values.Add(value);
        compilation.Members.Add(@enum);
        return @enum;
    }

    private static FieldBinding ReplaceField(
        RecordBinding record,
        string nativeName,
        string replacementNativeName,
        BindingType? type = null)
    {
        var index = record.Members
            .Select(static (member, index) => (Member: member, Index: index))
            .First(pair => pair.Member is FieldBinding field && field.NativeName == nativeName).Index;
        var source = (FieldBinding)record.Members[index];
        var replacement = new FieldBinding(
            source.SymbolId,
            replacementNativeName,
            source.ManagedName,
            type ?? source.Type,
            source.CppTypeName,
            source.OwningRecordCppTypeName,
            source.IsReadOnly,
            source.IsReference,
            source.IsPointer)
        {
            AccessModifier = source.AccessModifier,
            ReturnsOwnedHandle = source.ReturnsOwnedHandle
        };
        replacement.SetMetadata(MetadataKeys.NativeFieldAccessExpression, nativeName);
        record.Members[index] = replacement;
        return replacement;
    }
}
