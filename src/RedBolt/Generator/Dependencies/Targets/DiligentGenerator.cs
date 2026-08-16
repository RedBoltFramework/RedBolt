// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Generator.Dependencies;

using System;
using System.Collections.Generic;
using System.Linq;

using SharpCpp.Model.Ast;
using SharpCpp.Rendering;

internal sealed class DiligentGenerator(string nativeLibraryName)
    : BindingTargetGenerator(
        "diligent",
        "RedBolt.Bindings.Diligent",
        "RedBolt.Bindings.Diligent",
        "Diligent",
        nativeLibraryName,
        "redbolt_diligent",
        "-xc++",
        "-std=c++20",
        true)
{
    private readonly HashSet<string> _byteEnums = new(StringComparer.Ordinal);
    private readonly HashSet<string> _sbyteEnums = new(StringComparer.Ordinal);

    private static readonly HashSet<string> RetainedFunctions = new(StringComparer.Ordinal)
    {
        "LoadAndGetEngineFactoryOpenGL",
        "LoadAndGetEngineFactoryD3D11",
        "LoadAndGetEngineFactoryD3D12",
        "LoadAndGetEngineFactoryVk",
        "InstallDebugMessageCallback",
        "CreateTemporaryGLProgram",
        "CreateProxySwapChainGL",
        "CreateSecondaryProxySwapChainGL",
        "CreateProxySwapChainMS",
        "GetVulkanSurfaceExtent",
        "ReleaseTemporaryGLProgramHandle",
        "DeleteTemporaryGLProgram",
        "GetTemporaryGLProgramVertexAttributeCount",
        "GetTemporaryGLProgramVertexAttributeName",
        "GetTemporaryGLProgramVertexAttributeLocation",
        "BindTemporaryGLProgramVertexAttributes",
        "GetTemporaryGLProgramUniformBlockCount",
        "GetTemporaryGLProgramUniformBlockName",
        "GetTemporaryGLProgramUniformBlockDataSize",
        "GetTemporaryGLProgramUniformBlockIndex",
        "GetTemporaryGLProgramUniformCount",
        "GetTemporaryGLProgramUniformName",
        "GetTemporaryGLProgramUniformBlockOffset",
        "GetTemporaryGLProgramUniformSize",
        "GetTextureFormatAttribs"
    };

    private static readonly HashSet<string> RetainedBaseInterfaceMethods = new(StringComparer.Ordinal)
    {
        "AddRef",
        "Release"
    };

    private static readonly IReadOnlyDictionary<string, FixedWidthAliasProjection> FixedWidthAliases =
        new Dictionary<string, FixedWidthAliasProjection>(StringComparer.Ordinal)
        {
            ["Diligent::Uint64"] = new(BindingPrimitiveKind.UInt64, "ulong"),
            ["Diligent::Int64"] = new(BindingPrimitiveKind.Int64, "long")
        };

    private static readonly IReadOnlyDictionary<string, string> QueryableInterfaces =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Diligent::IEngineFactoryVk"] = "Diligent::IID_EngineFactoryVk",
            ["Diligent::IDeviceContextGL"] = "Diligent::IID_DeviceContextGL",
            ["Diligent::ISwapChainVk"] = "Diligent::IID_SwapChainVk",
            ["Diligent::IRenderDeviceVk"] = "Diligent::IID_RenderDeviceVk",
            ["Diligent::IRenderDeviceD3D11"] = "Diligent::IID_RenderDeviceD3D11",
            ["Diligent::IRenderDeviceD3D12"] = "Diligent::IID_RenderDeviceD3D12",
            ["Diligent::IRenderDeviceGL"] = "Diligent::IID_RenderDeviceGL",
            ["Diligent::IRenderDeviceGLES"] = "Diligent::IID_RenderDeviceGLES"
        };

    protected override IReadOnlyDictionary<string, string> NamespaceMappings { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Diligent"] = "RedBolt.Bindings.Diligent"
        };

    protected override void ConfigureCompilation(BindingCompilation compilation)
    {
        foreach (var record in compilation.Records)
        {
            if (!IsInterfaceRecord(record))
                continue;

            record.SetMetadata(MetadataKeys.NativeInterfaceWrapper, true);
            if (record.CppQualifiedName is not ("Diligent::IMemoryAllocator" or "Diligent::IReferenceCounters"))
                record.SetMetadata(MetadataKeys.NativeInterfaceReleaseMethod, "Release");
            if (QueryableInterfaces.TryGetValue(record.CppQualifiedName, out var interfaceIdExpression))
            {
                record.SetMetadata(
                    MetadataKeys.NativeInterfaceQuery,
                    new NativeInterfaceQueryAnnotation("Diligent::IObject", interfaceIdExpression));
            }
        }
    }

    protected override void ConfigureBeforeValidation(BindingCompilation compilation)
    {
        foreach (var @enum in compilation.Enums)
        {
            @enum.UnderlyingPrimitiveKind = @enum.UnderlyingTypeName switch
            {
                "Diligent::Int8" or "Int8" => BindingPrimitiveKind.Int8,
                "Diligent::Uint8" or "Uint8" => BindingPrimitiveKind.UInt8,
                "Diligent::Uint16" or "Uint16" => BindingPrimitiveKind.UInt16,
                "Diligent::Uint32" or "Uint32" => BindingPrimitiveKind.UInt32,
                _ => @enum.UnderlyingPrimitiveKind
            };
            if (@enum.UnderlyingPrimitiveKind is BindingPrimitiveKind.UInt8
                or BindingPrimitiveKind.UInt16 or BindingPrimitiveKind.UInt32)
            {
                var managedType = @enum.UnderlyingPrimitiveKind switch
                {
                    BindingPrimitiveKind.UInt8 => "byte",
                    BindingPrimitiveKind.UInt16 => "ushort",
                    _ => "uint"
                };
                foreach (var value in @enum.Values)
                {
                    if (value.ValueExpression.StartsWith("-", StringComparison.Ordinal))
                        value.ValueExpression = $"unchecked(({managedType}){value.ValueExpression})";
                }
            }
            if (@enum.UnderlyingPrimitiveKind == BindingPrimitiveKind.UInt8)
                _byteEnums.Add(@enum.ManagedName);
            else if (@enum.UnderlyingPrimitiveKind == BindingPrimitiveKind.Int8)
                _sbyteEnums.Add(@enum.ManagedName);
        }

        FixedWidthAliasNormalizer.Normalize(compilation, FixedWidthAliases);
        ConfigureSetVertexBuffersOffsets(compilation);
        ConfigureAndroidResume(compilation);
    }

    protected override GeneratedBindingSet TransformBindings(GeneratedBindingSet bindings)
    {
        return new(bindings.Files.Select(file => file.RelativePath.EndsWith(".g.cs", StringComparison.Ordinal)
            ? file with { Content = CorrectByteEnumUnderlyingTypes(file.Content) }
            : file).ToArray());
    }

    private string CorrectByteEnumUnderlyingTypes(string content)
    {
        foreach (var managedName in _byteEnums)
            content = content.Replace($"public enum {managedName} : int", $"public enum {managedName} : byte", StringComparison.Ordinal);
        foreach (var managedName in _sbyteEnums)
            content = content.Replace($"public enum {managedName} : int", $"public enum {managedName} : sbyte", StringComparison.Ordinal);
        return content;
    }

    protected override void PostProcessCompilation(BindingCompilation compilation)
    {
        ConfigureBlendFactors(compilation);
    }

    protected override int PruneCompilation(BindingCompilation compilation)
    {
        var prunedCount = PruneTopLevelFunctions(compilation.Members);
        foreach (var record in compilation.Records.ToArray())
        {
            if (record.HasMetadata(MetadataKeys.NativeInterfaceWrapper))
                prunedCount += PruneInterfaceMethods(record);
        }

        prunedCount += PruneAccessoryRecords(compilation);
        compilation.InvalidateCaches();
        return prunedCount;
    }

    private static void ConfigureBlendFactors(BindingCompilation compilation)
    {
        var deviceContext = compilation.Records.FirstOrDefault(static record =>
            record.CppQualifiedName == "Diligent::IDeviceContext");
        var setBlendFactors = deviceContext?.Members.OfType<FunctionBinding>().FirstOrDefault(static method =>
            method.NativeName == "SetBlendFactors");
        var blendFactors = setBlendFactors?.Parameters.FirstOrDefault(static parameter =>
            parameter.NativeName == "pBlendFactors");
        if (blendFactors is null)
            return;

        blendFactors.Type = BindingType.CreateConfigured(
            "const float*",
            "global::RedBolt.Bindings.Diligent.BlendFactors");
    }

    private static void ConfigureSetVertexBuffersOffsets(BindingCompilation compilation)
    {
        var deviceContext = compilation.Records.FirstOrDefault(static record =>
            record.CppQualifiedName == "Diligent::IDeviceContext");
        var setVertexBuffers = deviceContext?.Members.OfType<FunctionBinding>().FirstOrDefault(static method =>
            method.NativeName == "SetVertexBuffers");
        var offsets = setVertexBuffers?.Parameters.FirstOrDefault(static parameter =>
            parameter.NativeName == "pOffsets" && parameter.CppTypeName == "const Diligent::Uint64*");
        if (offsets is null || offsets.Type is not
            {
                Kind: BindingTypeKind.Primitive,
                PrimitiveKind: BindingPrimitiveKind.NativeUInt,
                CppTypeName: "unsigned long"
            })
        {
            return;
        }

        offsets.Type = FixedWidthAliasNormalizer.ProjectType(
            offsets.Type,
            new FixedWidthAliasProjection(BindingPrimitiveKind.UInt64, "ulong"));
    }

    private static void ConfigureAndroidResume(BindingCompilation compilation)
    {
        var renderDevice = compilation.Records.FirstOrDefault(static record =>
            record.CppQualifiedName == "Diligent::IRenderDeviceGLES");
        var resume = renderDevice?.Members.OfType<FunctionBinding>().FirstOrDefault(static method =>
            method.NativeName == "Resume");
        var window = resume?.Parameters.FirstOrDefault(static parameter =>
            parameter.NativeName == "window" && parameter.CppTypeName == "ANativeWindow*");
        if (window is null)
            return;

        window.Type = BindingType.IntPtr;
        window.Direction = BindingParameterDirection.In;
        window.AbiKind = BindingParameterAbiKind.Value;
    }

    private static int PruneTopLevelFunctions(IList<BindingNode> members)
    {
        var prunedCount = 0;
        for (var index = members.Count - 1; index >= 0; --index)
        {
            if (members[index] is not FunctionBinding function || IsRetainedFunction(function))
                continue;

            members.RemoveAt(index);
            prunedCount++;
        }
        return prunedCount;
    }

    private static int PruneInterfaceMethods(RecordBinding record)
    {
        var prunedCount = 0;
        for (var index = record.Members.Count - 1; index >= 0; --index)
        {
            if (record.Members[index] is not FunctionBinding method ||
                IsRetainedInterfaceMethod(record, method))
            {
                continue;
            }

            record.Members.RemoveAt(index);
            prunedCount++;
        }
        return prunedCount;
    }

    private static int PruneAccessoryRecords(BindingCompilation compilation)
    {
        var accessoryRecordNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "HashMapStringKey",
            "RefCountersImpl",
            "Matrix2x2",
            "Matrix3x3",
            "Matrix4x4",
            "Quaternion",
            "Vector2",
            "Vector3",
            "Vector4"
        };
        var prunedCount = 0;
        for (var index = compilation.Members.Count - 1; index >= 0; --index)
        {
            if (compilation.Members[index] is not RecordBinding record ||
                !accessoryRecordNames.Contains(record.NativeName))
            {
                continue;
            }

            compilation.Members.RemoveAt(index);
            prunedCount++;
        }

        var remainingRecordIds = compilation.Records
            .Select(static record => record.SymbolId)
            .ToHashSet(StringComparer.Ordinal);
        for (var index = compilation.TypeAliases.Count - 1; index >= 0; --index)
        {
            var alias = compilation.TypeAliases[index];
            if (alias.ManagedName == "String" ||
                alias.TargetType.RecordId is not null && !remainingRecordIds.Contains(alias.TargetType.RecordId))
            {
                compilation.TypeAliases.RemoveAt(index);
                prunedCount++;
            }
        }
        return prunedCount;
    }

    private static bool IsInterfaceRecord(RecordBinding record)
    {
        if (!record.CppQualifiedName.StartsWith("Diligent::", StringComparison.Ordinal) ||
            record.NativeName == "INTERFACE_ID" ||
            record.Members.OfType<FieldBinding>().Any())
        {
            return false;
        }

        return record.IsAbstract &&
            record.Members.OfType<FunctionBinding>().Any(static method => method is
            {
                IsVirtual: true,
                IsPureVirtual: true,
                AccessModifier: BindingAccessModifier.Public
            });
    }

    private static bool IsRetainedFunction(FunctionBinding function)
    {
        return RetainedFunctions.Contains(function.ManagedName) ||
            RetainedFunctions.Contains(function.NativeName) ||
            RetainedFunctions.Any(name =>
                function.CppQualifiedName.EndsWith($"::{name}", StringComparison.Ordinal));
    }

    private static bool IsRetainedInterfaceMethod(RecordBinding record, FunctionBinding method)
    {
        if (HasUnsupportedInterfaceSignature(method))
            return false;

        return record.CppQualifiedName switch
        {
            "Diligent::IObject" =>
                RetainedBaseInterfaceMethods.Contains(method.ManagedName) ||
                RetainedBaseInterfaceMethods.Contains(method.NativeName) ||
                RetainedBaseInterfaceMethods.Any(name =>
                    method.CppQualifiedName.EndsWith($"::{name}", StringComparison.Ordinal)),
            _ => true
        };
    }

    private static bool HasUnsupportedInterfaceSignature(FunctionBinding method)
    {
        if (method.ReturnType.Kind == BindingTypeKind.Primitive &&
            method.ReturnType.PrimitiveKind == BindingPrimitiveKind.Unsupported)
        {
            return true;
        }

        return method.Parameters.Any(static parameter =>
            parameter.CppTypeName.Contains("&*", StringComparison.Ordinal) ||
            parameter.CppTypeName.Contains("const void**", StringComparison.Ordinal) ||
            parameter.CppTypeName.Contains("void**", StringComparison.Ordinal));
    }
}
