namespace RedBolt.Generator.Dependencies;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

using SharpCpp.Model.Ast;
using SharpCpp.Rendering;

internal sealed class DiligentGenerator(string nativeLibraryName)
    : BindingTargetGenerator(
        "diligent",
        "RedBolt.Bindings.Diligent",
        "RedBolt.Bindings.Diligent",
        "DiligentNativeBindings",
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
            ["Uint64"] = new(BindingPrimitiveKind.UInt64, "ulong"),
            ["Int64"] = new(BindingPrimitiveKind.Int64, "long")
        };

    protected override IReadOnlyDictionary<string, string> NamespaceMappings { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal);

    protected override void ConfigureCompilation(BindingCompilation compilation)
    {
        foreach (var record in compilation.Records)
        {
            if (!IsCInterfaceRecord(record))
                continue;

            record.SetMetadata(MetadataKeys.NativeInterfaceWrapper, true);
            record.EmitDefaultNativeReleaseOverride = false;
        }

        ConfigureCInterfaceManagedBases(compilation);
        AddCInterfaceMacroFunctions(compilation);
        ConfigureSupportFunctionOwnership(compilation);
        compilation.InvalidateCaches();
    }

    protected override void ConfigureBeforeValidation(BindingCompilation compilation)
    {
        foreach (var @enum in compilation.Enums)
        {
            @enum.UnderlyingPrimitiveKind = @enum.UnderlyingTypeName switch
            {
                "Int8" => BindingPrimitiveKind.Int8,
                "Uint8" => BindingPrimitiveKind.UInt8,
                "Uint16" => BindingPrimitiveKind.UInt16,
                "Uint32" => BindingPrimitiveKind.UInt32,
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
            ? file with
            {
                Content = CorrectByteEnumUnderlyingTypes(file.Content) +
                    (file.RelativePath.EndsWith(".Bindings.g.cs", StringComparison.Ordinal)
                        ? RenderCInterfaceReleaseOverride()
                        : string.Empty)
            }
            : file).ToArray());
    }

    private string RenderCInterfaceReleaseOverride() =>
        $$"""

        namespace RedBolt.Bindings.Diligent
        {
            public static partial class DiligentNativeBindings
            {
                [global::System.Runtime.InteropServices.LibraryImport(
                    "{{NativeLibraryName}}", EntryPoint = "redbolt_diligent_IObject_Release")]
                internal static partial int IObject_ReleaseRaw(nint instance);
            }

            public partial class IObject
            {
                protected override bool ReleaseNativeHandle(nint handle)
                {
                    if (OwnsNativeHandle && handle != 0)
                        _ = DiligentNativeBindings.IObject_ReleaseRaw(handle);
                    return true;
                }
            }
        }
        """;

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
        ConfigureCInterfaceManagedBases(compilation);
        RestoreCDerivedFieldNames(compilation);
        ConfigureBlendFactors(compilation);
        compilation.InvalidateCaches();
    }

    protected override int PruneCompilation(BindingCompilation compilation) => 0;

    private static void RestoreCDerivedFieldNames(BindingCompilation compilation)
    {
        foreach (var field in compilation.Records.SelectMany(static record => record.Members.OfType<FieldBinding>()))
        {
            var accessPath = field.GetMetadata<string>(MetadataKeys.NativeFieldAccessExpression);
            if (accessPath is null || !accessPath.StartsWith('_'))
                continue;

            var separator = field.NativeName.IndexOf('_');
            if (separator <= 0)
                continue;

            var prefix = field.NativeName[..separator];
            if (field.ManagedName.StartsWith(prefix, StringComparison.Ordinal) &&
                field.ManagedName.Length > prefix.Length)
            {
                field.ManagedName = field.ManagedName[prefix.Length..];
            }
        }
    }

    private static void ConfigureSupportFunctionOwnership(BindingCompilation compilation)
    {
        foreach (var function in compilation.Functions.Where(static function =>
                     function.NativeName.StartsWith("RedBoltQueryI", StringComparison.Ordinal)))
        {
            function.ReturnsOwnedHandle = true;
        }
    }

    private static void ConfigureBlendFactors(BindingCompilation compilation)
    {
        var setBlendFactors = compilation.Functions.FirstOrDefault(static function =>
            function.NativeName == "IDeviceContext_SetBlendFactors");
        var blendFactors = setBlendFactors?.Parameters.ElementAtOrDefault(1);
        if (blendFactors is null)
            return;

        blendFactors.Type = BindingType.CreateConfigured(
            "const float*",
            "global::RedBolt.Bindings.Diligent.BlendFactors");
    }

    private static void ConfigureSetVertexBuffersOffsets(BindingCompilation compilation)
    {
        var setVertexBuffers = compilation.Functions.FirstOrDefault(static function =>
            function.NativeName == "IDeviceContext_SetVertexBuffers");
        var offsets = setVertexBuffers?.Parameters.ElementAtOrDefault(4);
        if (offsets is null)
            return;

        offsets.Type = BindingType.UInt64;
        offsets.Direction = BindingParameterDirection.In;
        offsets.AbiKind = BindingParameterAbiKind.Pointer;
        offsets.SetMetadata(MetadataKeys.InputPointerArray, true);
    }

    private static void ConfigureAndroidResume(BindingCompilation compilation)
    {
        var resume = compilation.Functions.FirstOrDefault(static function =>
            function.NativeName == "IRenderDeviceGLES_Resume");
        var window = resume?.Parameters.ElementAtOrDefault(1);
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

    private static void ConfigureCInterfaceManagedBases(BindingCompilation compilation)
    {
        var recordsByName = compilation.Records.ToDictionary(static record => record.NativeName, StringComparer.Ordinal);
        foreach (var interfaceRecord in compilation.Records.Where(IsCInterfaceRecord))
        {
            if (!recordsByName.TryGetValue(interfaceRecord.NativeName + "Vtbl", out var vtableRecord))
                continue;

            var methodGroups = vtableRecord.Members
                .OfType<FieldBinding>()
                .Select(static field =>
                {
                    var path = field.GetMetadata<string>(MetadataKeys.NativeFieldAccessExpression);
                    return !string.IsNullOrWhiteSpace(path) && path!.Contains('.', StringComparison.Ordinal)
                        ? path[..path.IndexOf('.', StringComparison.Ordinal)]
                        : field.NativeName;
                })
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            var ownGroup = interfaceRecord.NativeName[1..];
            var ownGroupIndex = Array.IndexOf(methodGroups, ownGroup);
            if (ownGroupIndex <= 0)
                continue;

            var baseInterfaceName = "I" + methodGroups[ownGroupIndex - 1];
            if (recordsByName.ContainsKey(baseInterfaceName))
                interfaceRecord.ManagedBaseTypeName = baseInterfaceName;
        }
    }

    private void AddCInterfaceMacroFunctions(BindingCompilation compilation)
    {
        var interfaceNames = compilation.Records
            .Where(IsCInterfaceRecord)
            .Select(static record => record.NativeName)
            .ToHashSet(StringComparer.Ordinal);
        var macros = LoadCInterfaceMacros();
        var functions = new List<FunctionBinding>();
        foreach (var methodsRecord in compilation.Records.Where(static record =>
                     record.NativeName.StartsWith('I') &&
                     record.NativeName.EndsWith("Methods", StringComparison.Ordinal)))
        {
            var interfaceName = methodsRecord.NativeName[..^"Methods".Length];
            if (!interfaceNames.Contains(interfaceName))
                continue;

            foreach (var field in methodsRecord.Members.OfType<FieldBinding>())
            {
                if (field.FunctionPointerSignature is not { } signature)
                    continue;

                var macroName = $"{interfaceName}_{field.NativeName}";
                if (!macros.ContainsKey(macroName))
                    continue;
                var function = new FunctionBinding(
                    $"c-interface-macro:{macroName}",
                    macroName,
                    macroName,
                    signature.ReturnType,
                    entryPointName: $"redbolt_diligent_{macroName}",
                    cppReturnTypeName: signature.CppReturnTypeName,
                    cppQualifiedName: macroName)
                {
                    ReturnPointerDepth = signature.ReturnPointerDepth,
                    ReturnsOwnedHandle = signature.ReturnsOwnedHandle
                };
                function.SetMetadata(MetadataKeys.NativeDefaultArgumentAdapter, true);

                for (var parameterIndex = 0; parameterIndex < signature.Parameters.Count; ++parameterIndex)
                {
                    var source = signature.Parameters[parameterIndex];
                    var parameterName = parameterIndex == 0 ? "This" : source.NativeName;
                    function.Parameters.Add(new ParameterBinding(
                        $"parameter:{macroName}:{parameterName}",
                        parameterName,
                        parameterName,
                        source.Type,
                        source.Direction,
                        source.AbiKind,
                        source.CppTypeName));
                }

                if (signature.IsIgnored())
                    function.SetIgnoreReason(signature.GetIgnoreReason()!);
                functions.Add(function);
            }
        }

        foreach (var function in functions)
            compilation.Members.Add(function);

        AddCInterfaceForwardingMacros(compilation, macros, functions);
    }

    private static void AddCInterfaceForwardingMacros(
        BindingCompilation compilation,
        IReadOnlyDictionary<string, string> macros,
        ICollection<FunctionBinding> functions)
    {
        var existingNames = functions.Select(static function => function.NativeName).ToHashSet(StringComparer.Ordinal);
        var recordsByName = compilation.Records.ToDictionary(static record => record.NativeName, StringComparer.Ordinal);
        foreach (var (macroName, replacement) in macros)
        {
            var match = Regex.Match(
                replacement,
                @"^\s*\(const\s+struct\s+(?<record>[A-Za-z0-9_]+)\s*\*\)\s*I[A-Za-z0-9]+_GetDesc\s*\(\s*This\s*\)\s*$",
                RegexOptions.CultureInvariant);
            if (!match.Success || existingNames.Contains(macroName))
                continue;

            var separator = macroName.IndexOf('_');
            var interfaceName = separator > 0 ? macroName[..separator] : string.Empty;
            var descriptorName = match.Groups["record"].Value;
            if (!recordsByName.TryGetValue(interfaceName, out var interfaceRecord) ||
                !recordsByName.TryGetValue(descriptorName, out var descriptorRecord))
            {
                continue;
            }

            var cppReturnTypeName = $"const struct {descriptorName}*";
            var function = new FunctionBinding(
                $"c-interface-macro:{macroName}",
                macroName,
                macroName,
                descriptorRecord.HandleType.WithCppTypeName(cppReturnTypeName),
                entryPointName: $"redbolt_diligent_{macroName}",
                cppReturnTypeName: cppReturnTypeName,
                cppQualifiedName: macroName)
            {
                ReturnPointerDepth = 1
            };
            function.SetMetadata(MetadataKeys.NativeDefaultArgumentAdapter, true);
            function.Parameters.Add(new ParameterBinding(
                $"parameter:{macroName}:This",
                "This",
                "This",
                interfaceRecord.HandleType,
                BindingParameterDirection.In,
                BindingParameterAbiKind.Value,
                interfaceRecord.HandleType.CppTypeName));
            compilation.Members.Add(function);
            existingNames.Add(macroName);
        }
    }

    private Dictionary<string, string> LoadCInterfaceMacros()
    {
        var macros = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var ownedRoot in Options.OwnedHeaders.Where(static root =>
                     Directory.Exists(root) &&
                     !root.Contains($"{Path.DirectorySeparatorChar}ThirdParty{Path.DirectorySeparatorChar}", StringComparison.Ordinal)))
        {
            foreach (var headerPath in Directory.EnumerateFiles(ownedRoot, "*.h", SearchOption.AllDirectories))
            {
                foreach (Match match in Regex.Matches(
                             File.ReadAllText(headerPath),
                             @"^\s*#\s*define\s+(I[A-Za-z0-9]+_[A-Za-z0-9_]+)\s*\([^\r\n]*?\)\s*([^\r\n]*)$",
                             RegexOptions.Multiline | RegexOptions.CultureInvariant))
                {
                    macros.TryAdd(match.Groups[1].Value, match.Groups[2].Value);
                }
            }
        }
        return macros;
    }

    private static bool IsCInterfaceRecord(RecordBinding record)
    {
        return record.NativeName.StartsWith('I') &&
            record.NativeName != "INTERFACE_ID" &&
            record.Members.OfType<FieldBinding>().Count() == 1 &&
            record.Members.OfType<FieldBinding>().Single().NativeName == "pVtbl";
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
