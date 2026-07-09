using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using SharpCpp.Metadata.Export;
using SharpCpp.Model.Ast;
using SharpCpp.Model.Logging;
using SharpCpp.Parsing;
using SharpCpp.Pipeline;
using SharpCpp.Rendering;

namespace RedBolt.Dependencies.Generator;

internal static class Program
{
    private static readonly string[] ParseCompatibilityDefines =
    [
        "SDL_DISABLE_MM3DNOW_H=1",
        "SDL_DISABLE_LSX_H=1",
        "SDL_DISABLE_LASX_H=1",
        "SDL_DISABLE_IMMINTRIN_H=1",
        "SDL_DISABLE_MMINTRIN_H=1",
        "SDL_DISABLE_XMMINTRIN_H=1",
        "SDL_DISABLE_EMMINTRIN_H=1",
        "SDL_DISABLE_PMMINTRIN_H=1"
    ];

    private static readonly string[] ParseSimdDisableArguments =
    [
        "-mno-sse",
        "-mno-sse2",
        "-mno-sse3",
        "-mno-ssse3",
        "-mno-sse4.1",
        "-mno-sse4.2",
        "-mno-avx",
        "-mno-avx2",
        "-mno-avx512f",
        "-mno-avx512bw",
        "-mno-avx512cd",
        "-mno-avx512dq",
        "-mno-avx512vl"
    ];

    private static readonly string[] ParseWarningDisableArguments =
    [
        "-Wno-deprecated-literal-operator",
        "-Wno-pragma-once-outside-header",
        "-Wno-unknown-attributes"
    ];

    private static readonly HashSet<string> RetainedDependencyFunctions = new(StringComparer.Ordinal)
    {
        "SDL_GetVersion",
        "SDL_Init",
        "SDL_InitSubSystem",
        "SDL_Quit",
        "SDL_QuitSubSystem",
        "SDL_WasInit"
    };

    private static readonly HashSet<string> RetainedDependencyRecords = new(StringComparer.Ordinal)
    {
        "SDL_version"
    };

    private static readonly HashSet<string> RetainedDiligentFunctions = new(StringComparer.Ordinal)
    {
        "GetEngineFactoryOpenGL",
        "GetShaderCodeBufferVariableAt"
    };

    private static readonly HashSet<string> RetainedDiligentBaseInterfaceMethods = new(StringComparer.Ordinal)
    {
        "AddRef",
        "Release"
    };

    private static readonly HashSet<string> DiligentSyntheticDefaultConstructors = new(StringComparer.Ordinal)
    {
        "Diligent::ShaderCreateInfo"
    };

    private static int Main(string[] args)
    {
        SharpCppLog.Configure();

        try
        {
            var options = GeneratorOptions.Parse(args);
            var descriptor = BindingSurfaceDescriptor.Create(options.Surface);
            Directory.CreateDirectory(options.OutputDirectory);

            var clangArguments = BuildClangArguments(options, descriptor);
            var marshallerClangArguments = BuildMarshallerClangArguments(clangArguments, descriptor);
            var marshallerHeaders = BuildMarshallerHeaders(options, descriptor);

            var parser = new ClangSharpHeaderParser();
            var compilation = parser.Parse(new ClangSharpParseRequest(
                ProjectName: descriptor.ProjectName,
                ManagedNamespace: descriptor.ManagedNamespace,
                ManagedClassName: descriptor.ManagedClassName,
                NativeLibraryName: descriptor.NativeLibraryName,
                    HeaderFilePath: options.HeaderPath,
                    HeaderIncludePath: options.HeaderIncludePath,
                    ClangArguments: clangArguments,
                    MarshallerConfigurationFiles: marshallerHeaders,
                    EntryPointPrefix: descriptor.EntryPointPrefix,
                    OwnedFileRoots: options.OwnedHeaders,
                    MarshallerClangArguments: marshallerClangArguments));

            compilation.NamespaceMappings = descriptor.Surface switch
            {
                "diligent" => new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["Diligent"] = descriptor.ManagedNamespace
                },
                _ => new Dictionary<string, string>(StringComparer.Ordinal)
            };

            foreach (var marshallerHeader in marshallerHeaders)
                compilation.NativeSupportHeaders.Add(marshallerHeader.Replace('\\', '/'));

            MarkNativeInterfaceWrappers(compilation, descriptor);
            BindingCompilationBootstrapPipeline.Apply(compilation, marshallerHeaders, clangArguments, marshallerClangArguments);
            var prunedCount = IgnoreStdlibWrappers(compilation);
            AddDiligentSyntheticDefaultConstructors(compilation, descriptor);
            prunedCount += PruneDependencySurface(compilation, descriptor);
            prunedCount += PruneImGuiSurfaceIfApplicable(compilation, descriptor);
            ApplyImGuiEnumAliasSurfaceIfApplicable(compilation, descriptor);
            prunedCount += PruneInvalidTypeAliases(compilation);

            var renderedBindings = BindingOutputRenderer.Render(
                compilation,
                options.FileStem,
                includeCallbackReleaseRegistration: descriptor.Surface == "sdl");
            renderedBindings.WriteToDirectory(options.OutputDirectory);

            var metadataDocument = BindingMetadataExporter.Export(
                compilation,
                new BindingMetadataExportOptions(
                    PackageId: descriptor.ProjectName,
                    Phase2ClosureHash: $"redbolt-{descriptor.Surface}-phase2",
                    ClangArgsHash: $"redbolt-{descriptor.Surface}-clang"));

            File.WriteAllText(options.MetadataPath, BindingMetadataSerializer.Serialize(metadataDocument));
            Console.WriteLine($"Generated {descriptor.Surface} bindings with {compilation.Functions.Count} functions after pruning {prunedCount} unsupported declarations: {options.OutputDirectory}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            PrintUsage();
            return 1;
        }
    }

    private static List<string> BuildClangArguments(GeneratorOptions options, BindingSurfaceDescriptor descriptor)
    {
        var clangArguments = new List<string>
        {
            descriptor.ClangLanguageArgument,
            descriptor.LanguageStandardArgument
        };

        clangArguments.AddRange(ParseWarningDisableArguments);

        foreach (var define in options.Defines)
            clangArguments.Add($"-D{define}");

        foreach (var define in ParseCompatibilityDefines)
        {
            var argument = $"-D{define}";
            if (!clangArguments.Contains(argument, StringComparer.Ordinal))
                clangArguments.Add(argument);
        }

        foreach (var includeDirectory in options.IncludeDirectories)
            clangArguments.Add($"-I{includeDirectory}");

        // Match the upstream rbfx generator so marshaller config headers can resolve
        // project-relative includes such as <SharpCpp/Marshaller.hpp>.
        var sharpCppIncludeRoot = Path.Combine(options.SharpCppRoot, "include");
        if (!clangArguments.Contains($"-I{sharpCppIncludeRoot}", StringComparer.Ordinal))
            clangArguments.Add($"-I{sharpCppIncludeRoot}");

        clangArguments.AddRange(ParseSimdDisableArguments);

        var toolchainInfo = CompilerToolchainDiscovery.Discover(options.CompilerPath);
        if (!string.IsNullOrWhiteSpace(toolchainInfo.TargetTriple) &&
            !clangArguments.Any(static argument => argument.StartsWith("--target=", StringComparison.Ordinal) || argument == "-target"))
        {
            clangArguments.Add($"--target={toolchainInfo.TargetTriple}");
        }

        if (!string.IsNullOrWhiteSpace(toolchainInfo.ClangResourceDirectory) &&
            !clangArguments.Any(static argument => argument.StartsWith("-resource-dir=", StringComparison.Ordinal) || argument == "-resource-dir"))
        {
            clangArguments.Add($"-resource-dir={toolchainInfo.ClangResourceDirectory}");
        }

        foreach (var systemIncludeDirectory in toolchainInfo.IncludeDirectories)
            clangArguments.Add($"-isystem{systemIncludeDirectory}");

        return clangArguments.Distinct(StringComparer.Ordinal).ToList();
    }

    private static List<string> BuildMarshallerClangArguments(IReadOnlyList<string> clangArguments, BindingSurfaceDescriptor descriptor)
    {
        if (!descriptor.UseMarshallerConfiguration)
            return clangArguments.ToList();

        if (descriptor.Surface != "sdl" && descriptor.Surface != "dearimgui")
            return clangArguments.ToList();

        var marshallerArguments = new List<string>(clangArguments.Count);
        var replacedLanguageMode = false;
        var replacedLanguageStandard = false;

        foreach (var argument in clangArguments)
        {
            if (!replacedLanguageMode && string.Equals(argument, descriptor.ClangLanguageArgument, StringComparison.Ordinal))
            {
                marshallerArguments.Add("-xc++");
                replacedLanguageMode = true;
                continue;
            }

            if (!replacedLanguageStandard && string.Equals(argument, descriptor.LanguageStandardArgument, StringComparison.Ordinal))
            {
                marshallerArguments.Add("-std=c++20");
                replacedLanguageStandard = true;
                continue;
            }

            marshallerArguments.Add(argument);
        }

        if (!replacedLanguageMode)
            marshallerArguments.Insert(0, "-xc++");

        if (!replacedLanguageStandard)
            marshallerArguments.Insert(replacedLanguageMode ? 1 : 0, "-std=c++20");

        return marshallerArguments;
    }

    private static List<string> BuildMarshallerHeaders(GeneratorOptions options, BindingSurfaceDescriptor descriptor)
    {
        if (!descriptor.UseMarshallerConfiguration)
            return new List<string>();

        var marshallerHeaders = new List<string>
        {
            Path.Combine(options.SharpCppRoot, "include", "SharpCpp", "Marshaller.hpp")
        };

        if (descriptor.Surface == "dearimgui")
        {
            marshallerHeaders.Add(Path.Combine(
                options.SharpCppRoot,
                "..",
                "native",
                "RedBolt.Native",
                "include",
                "RedBolt",
                "ImGuiValueMarshallers.hpp"));
        }

        return marshallerHeaders
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private static void MarkNativeInterfaceWrappers(BindingCompilation compilation, BindingSurfaceDescriptor descriptor)
    {
        if (descriptor.Surface != "diligent")
            return;

        foreach (var record in compilation.Records)
        {
            if (IsDiligentInterfaceRecord(record))
                record.SetMetadata(MetadataKeys.NativeInterfaceWrapper, true);
        }
    }

    private static bool IsDiligentInterfaceRecord(RecordBinding record)
    {
        if (!record.CppQualifiedName.StartsWith("Diligent::", StringComparison.Ordinal) ||
            string.Equals(record.NativeName, "INTERFACE_ID", StringComparison.Ordinal) ||
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

    private static void AddDiligentSyntheticDefaultConstructors(BindingCompilation compilation, BindingSurfaceDescriptor descriptor)
    {
        if (descriptor.Surface != "diligent")
            return;

        foreach (var record in compilation.Records)
        {
            if (!DiligentSyntheticDefaultConstructors.Contains(record.CppQualifiedName) ||
                record.Members.OfType<FunctionBinding>().Any(static member => member.IsConstructor && member.Parameters.Count == 0))
            {
                continue;
            }

            var constructor = new FunctionBinding(
                $"ctor:{record.CppQualifiedName}:synthetic-default",
                record.NativeName,
                record.ManagedName,
                record.HandleType,
                entryPointName: $"{descriptor.EntryPointPrefix}_{record.CppQualifiedName.Replace("::", "_")}_ctor",
                cppReturnTypeName: $"{record.CppQualifiedName}*",
                cppQualifiedName: $"{record.CppQualifiedName}::{record.NativeName}")
            {
                IsConstructor = true,
                IsStatic = false,
                OwningRecordCppTypeName = record.CppQualifiedName,
                AccessModifier = BindingAccessModifier.Public
            };
            constructor.SetMetadata(MetadataKeys.DeclarationDisplayText, $"{record.NativeName}();");
            record.Members.Add(constructor);
        }
    }

    private static int IgnoreStdlibWrappers(BindingCompilation compilation)
    {
        var ignoredCount = 0;
        foreach (var record in compilation.Records)
        {
            if (record.IsIgnored() ||
                !record.CppQualifiedName.StartsWith("std::", StringComparison.Ordinal))
            {
                continue;
            }

            record.SetIgnoreReason("std library wrapper emission is disabled in RedBolt for now");
            ignoredCount++;
        }

        return ignoredCount;
    }

    private static int PruneDependencySurface(BindingCompilation compilation, BindingSurfaceDescriptor descriptor)
    {
        if (descriptor.Surface == "sdl" || descriptor.Surface == "dearimgui")
            return 0;

        if (descriptor.Surface == "diligent")
        {
            var diligentPrunedCount = PruneDiligentSurface(compilation);
            compilation.InvalidateCaches();
            return diligentPrunedCount;
        }

        var prunedCount = PruneMembers(compilation.Members, descriptor);
        compilation.InvalidateCaches();
        return prunedCount;
    }

    private static int PruneImGuiSurfaceIfApplicable(BindingCompilation compilation, BindingSurfaceDescriptor descriptor)
    {
        if (descriptor.Surface != "dearimgui")
            return 0;

        return PruneImGuiSurface(compilation);
    }

    private static void ApplyImGuiEnumAliasSurfaceIfApplicable(BindingCompilation compilation, BindingSurfaceDescriptor descriptor)
    {
        if (descriptor.Surface != "dearimgui")
            return;

        ApplyImGuiEnumAliasSurface(compilation);
    }

    private static int PruneDiligentSurface(BindingCompilation compilation)
    {
        var prunedCount = 0;
        prunedCount += PruneDiligentTopLevelFunctions(compilation.Members);

        foreach (var record in compilation.Records.ToArray())
        {
            if (!record.HasMetadata(MetadataKeys.NativeInterfaceWrapper))
                continue;

            prunedCount += PruneDiligentInterfaceMethods(record);
        }

        return prunedCount;
    }

    private static int PruneDiligentTopLevelFunctions(IList<BindingNode> members)
    {
        var prunedCount = 0;
        for (var index = members.Count - 1; index >= 0; --index)
        {
            if (members[index] is not FunctionBinding function)
                continue;

            if (IsRetainedDiligentFunction(function))
                continue;

            members.RemoveAt(index);
            prunedCount++;
        }

        return prunedCount;
    }

    private static int PruneDiligentInterfaceMethods(RecordBinding record)
    {
        var prunedCount = 0;
        for (var index = record.Members.Count - 1; index >= 0; --index)
        {
            if (record.Members[index] is not FunctionBinding method)
                continue;

            if (IsRetainedDiligentInterfaceMethod(record, method))
                continue;

            record.Members.RemoveAt(index);
            prunedCount++;
        }

        return prunedCount;
    }

    private static readonly HashSet<string> ImGuiProblematicRecordCppNames = new(StringComparer.Ordinal)
    {
        "ImFontGlyph",
        "ImFontBaked",
        "ImGuiStoragePair",
        "ImGuiInputTextCallbackData",
        "ImFontAtlas"
    };

    private static readonly HashSet<string> ImGuiProblematicFunctionNames = new(StringComparer.Ordinal)
    {
        "IsMouseReleasedWithDelay",
        "GetItemFlags",
        "GetMouseCursor",
        "TableGetColumnFlags"
    };

    private static int PruneImGuiSurface(BindingCompilation compilation)
    {
        var prunedCount = 0;

        foreach (var record in compilation.Records)
        {
            if (ImGuiProblematicRecordCppNames.Contains(record.CppQualifiedName))
            {
                record.SetSilentExclusionReason("Dear ImGui record contains bit-fields, anonymous unions, or char* fields not yet supported by SharpCpp rendering");
                prunedCount++;
            }
        }

        foreach (var function in compilation.Functions)
        {
            if (ImGuiProblematicFunctionNames.Contains(function.NativeName))
            {
                function.SetSilentExclusionReason("Dear ImGui function has unsupported C++ syntax in the managed surface");
                prunedCount++;
            }
        }

        PruneImGuiFunctionsWithExcludedTypes(compilation);
        compilation.InvalidateCaches();
        return prunedCount;
    }

    private static void ApplyImGuiEnumAliasSurface(BindingCompilation compilation)
    {
        var enumsByNativeName = compilation.Enums.ToDictionary(static @enum => @enum.NativeName, StringComparer.Ordinal);
        foreach (var @enum in compilation.Enums)
        {
            if (@enum.NativeName.EndsWith("_", StringComparison.Ordinal))
                @enum.ManagedName = @enum.NativeName.TrimEnd('_');
            if (@enum.NativeName.EndsWith("Flags_", StringComparison.Ordinal))
                @enum.IsFlags = true;
        }

        var enumsByAliasName = new Dictionary<string, EnumBinding>(StringComparer.Ordinal);
        for (var index = compilation.TypeAliases.Count - 1; index >= 0; --index)
        {
            var alias = compilation.TypeAliases[index];
            if (alias.TargetType.Kind != BindingTypeKind.Primitive ||
                alias.TargetType.PrimitiveKind != BindingPrimitiveKind.Int32 ||
                !enumsByNativeName.TryGetValue(alias.NativeName + "_", out var @enum))
            {
                continue;
            }

            enumsByAliasName[alias.NativeName] = @enum;
            compilation.TypeAliases.RemoveAt(index);
        }

        foreach (var function in compilation.Functions)
        {
            function.ReturnType = RemapImGuiEnumAliases(function.ReturnType, enumsByAliasName);

            foreach (var parameter in function.Parameters)
                parameter.Type = RemapImGuiEnumAliases(parameter.Type, enumsByAliasName);
        }

        foreach (var field in compilation.Records.SelectMany(static record => record.Members.OfType<FieldBinding>()))
            field.Type = RemapImGuiEnumAliases(field.Type, enumsByAliasName);

        compilation.InvalidateCaches();
    }

    private static BindingType RemapImGuiEnumAliases(
        BindingType type,
        IReadOnlyDictionary<string, EnumBinding> enumsByAliasName)
    {
        if (!string.IsNullOrWhiteSpace(type.AliasNativeName) &&
            enumsByAliasName.TryGetValue(type.AliasNativeName, out var @enum) &&
            type.Kind == BindingTypeKind.Primitive &&
            type.PrimitiveKind == BindingPrimitiveKind.Int32)
        {
            var nativeCppTypeName = type.AliasCppQualifiedName ?? type.AliasNativeName ?? type.CppTypeName;
            var remappedType = BindingType.CreateEnum(nativeCppTypeName, @enum.ManagedName, enumId: @enum.SymbolId)
                .WithCppIsReference(type.Marshaller.CppIsReference);
            if (type.FixedArrayElementType is not null && type.FixedArrayLength is not null)
                remappedType = remappedType.WithFixedArray(type.FixedArrayElementType, type.FixedArrayLength.Value);
            if (type.DynamicArrayElementType is not null)
                remappedType = remappedType.WithDynamicArray(type.DynamicArrayElementType);
            return remappedType;
        }

        if (type.IsCallback)
        {
            var callbackReturnType = type.CallbackReturnType is null
                ? null
                : RemapImGuiEnumAliases(type.CallbackReturnType, enumsByAliasName);
            var callbackParameterTypes = type.CallbackParameterTypes
                .Select(parameter => RemapImGuiEnumAliases(parameter, enumsByAliasName))
                .ToArray();
            if (callbackReturnType is not null)
                type = type.WithCallbackSignature(callbackReturnType, callbackParameterTypes);
        }

        if (type.GenericArguments.Count > 0)
            type = type.WithGenericArguments(type.GenericArguments.Select(argument => RemapImGuiEnumAliases(argument, enumsByAliasName)).ToArray());
        if (type.FixedArrayElementType is not null && type.FixedArrayLength is not null)
            type = type.WithFixedArray(RemapImGuiEnumAliases(type.FixedArrayElementType, enumsByAliasName), type.FixedArrayLength.Value);
        if (type.DynamicArrayElementType is not null)
            type = type.WithDynamicArray(RemapImGuiEnumAliases(type.DynamicArrayElementType, enumsByAliasName));

        return type;
    }

    private static void PruneImGuiFunctionsWithExcludedTypes(BindingCompilation compilation)
    {
        var excludedRecordSymbolIds = compilation.Records
            .Where(static record => record.IsIgnored())
            .Select(static record => record.SymbolId)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var function in compilation.Functions)
        {
            if (function.IsIgnored())
                continue;

            if (ReferencesExcludedRecord(function.ReturnType, excludedRecordSymbolIds) ||
                function.Parameters.Any(p => ReferencesExcludedRecord(p.Type, excludedRecordSymbolIds)))
            {
                function.SetSilentExclusionReason("Dear ImGui function references an excluded record type");
            }
        }

        foreach (var field in compilation.Records.SelectMany(static r => r.Members.OfType<FieldBinding>()))
        {
            if (field.IsIgnored())
                continue;

            if (ReferencesExcludedRecord(field.Type, excludedRecordSymbolIds))
            {
                field.SetSilentExclusionReason("Dear ImGui field references an excluded record type");
            }
        }
    }

    private static bool ReferencesExcludedRecord(BindingType type, HashSet<string> excludedSymbolIds)
    {
        if (type.RecordId is not null && excludedSymbolIds.Contains(type.RecordId))
            return true;

        return type.GenericArguments.Any(a => ReferencesExcludedRecord(a, excludedSymbolIds)) ||
            (type.FixedArrayElementType is not null && ReferencesExcludedRecord(type.FixedArrayElementType, excludedSymbolIds)) ||
            (type.DynamicArrayElementType is not null && ReferencesExcludedRecord(type.DynamicArrayElementType, excludedSymbolIds));
    }

    private static bool IsRetainedDiligentFunction(FunctionBinding function)
    {
        return RetainedDiligentFunctions.Contains(function.ManagedName) ||
            RetainedDiligentFunctions.Contains(function.NativeName) ||
            RetainedDiligentFunctions.Any(name =>
                function.CppQualifiedName.EndsWith($"::{name}", StringComparison.Ordinal));
    }

    private static bool IsRetainedDiligentInterfaceMethod(RecordBinding record, FunctionBinding method)
    {
        if (HasUnsupportedDiligentInterfaceSignature(method))
            return false;

        return record.CppQualifiedName switch
        {
            "Diligent::IObject" =>
                RetainedDiligentBaseInterfaceMethods.Contains(method.ManagedName) ||
                RetainedDiligentBaseInterfaceMethods.Contains(method.NativeName) ||
                RetainedDiligentBaseInterfaceMethods.Any(name => method.CppQualifiedName.EndsWith($"::{name}", StringComparison.Ordinal)),
            _ => true
        };
    }

    private static bool HasUnsupportedDiligentInterfaceSignature(FunctionBinding method)
    {
        if (method.ReturnType.Kind == BindingTypeKind.Primitive && method.ReturnType.PrimitiveKind == BindingPrimitiveKind.Unsupported)
            return true;

        foreach (var parameter in method.Parameters)
        {
            if (parameter.Type.Kind == BindingTypeKind.Primitive &&
                parameter.Type.PrimitiveKind == BindingPrimitiveKind.Unsupported &&
                parameter.Direction != BindingParameterDirection.In)
                return true;

            if (parameter.CppTypeName.Contains("&*", StringComparison.Ordinal) ||
                parameter.CppTypeName.Contains("const void**", StringComparison.Ordinal) ||
                parameter.CppTypeName.Contains("void**", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static int PruneInvalidTypeAliases(BindingCompilation compilation)
    {
        var prunedCount = 0;
        for (var index = compilation.TypeAliases.Count - 1; index >= 0; --index)
        {
            var alias = compilation.TypeAliases[index];
            if (alias.TargetType.Kind == BindingTypeKind.Primitive &&
                alias.TargetType.PrimitiveKind == BindingPrimitiveKind.Void)
            {
                compilation.TypeAliases.RemoveAt(index);
                prunedCount++;
            }
        }

        return prunedCount;
    }

    private static int PruneMembers(IList<BindingNode> members, BindingSurfaceDescriptor descriptor)
    {
        var prunedCount = 0;
        for (var index = members.Count - 1; index >= 0; --index)
        {
            var member = members[index];
            if (member is RecordBinding record)
            {
                prunedCount += PruneMembers(record.Members, descriptor);
                if (!IsRetainedDependencyRecord(record, descriptor))
                {
                    members.RemoveAt(index);
                    prunedCount++;
                }
            }
            else if (member is EnumBinding)
            {
                members.RemoveAt(index);
                prunedCount++;
            }
            else if (member is FunctionBinding function)
            {
                if (!IsRetainedDependencyFunction(function, descriptor))
                {
                    members.RemoveAt(index);
                    prunedCount++;
                }
            }
            else if (member is ManagedClassBinding)
            {
                members.RemoveAt(index);
                prunedCount++;
            }
        }

        return prunedCount;
    }

    private static bool IsRetainedDependencyFunction(FunctionBinding function, BindingSurfaceDescriptor descriptor)
    {
        if (descriptor.Surface != "sdl")
            return false;

        return RetainedDependencyFunctions.Contains(function.CppQualifiedName) ||
            RetainedDependencyFunctions.Contains(function.NativeName) ||
            RetainedDependencyFunctions.Contains(function.ManagedName) ||
            RetainedDependencyFunctions.Any(name =>
                function.CppQualifiedName.EndsWith($"::{name}", StringComparison.Ordinal) ||
                function.NativeName.EndsWith(name, StringComparison.Ordinal));
    }

    private static bool IsRetainedDependencyRecord(RecordBinding record, BindingSurfaceDescriptor descriptor)
    {
        return descriptor.Surface == "sdl" &&
            (RetainedDependencyRecords.Contains(record.CppQualifiedName) ||
             RetainedDependencyRecords.Contains(record.NativeName) ||
             RetainedDependencyRecords.Contains(record.ManagedName));
    }

    private static void PrintUsage()
    {
        Console.Error.WriteLine("Usage: RedBolt.Dependencies.Generator --surface <sdl|diligent|dearimgui> --header <path> --include-name <include> --output-dir <dir> --file-stem <stem> --metadata <path> --sharpcpp-root <path> --compiler <path> [--include <dir>]... [--define <name[=value]>]... [--owned-header <path>]...");
    }

    private sealed record BindingSurfaceDescriptor(
        string Surface,
        string ProjectName,
        string ManagedNamespace,
        string ManagedClassName,
        string NativeLibraryName,
        string EntryPointPrefix,
        string ClangLanguageArgument,
        string LanguageStandardArgument,
        bool UseMarshallerConfiguration)
    {
        public static BindingSurfaceDescriptor Create(string surface)
        {
            return surface.ToLowerInvariant() switch
            {
                "sdl" => new BindingSurfaceDescriptor(
                    "sdl",
                    "RedBolt.Bindings.Sdl",
                    "RedBolt.Bindings.Sdl",
                    "SdlNativeBindings",
                "RedBolt.Native",
                "redbolt_sdl",
                "-xc",
                "-std=c17",
                true),
                "diligent" => new BindingSurfaceDescriptor(
                    "diligent",
                    "RedBolt.Bindings.Diligent",
                    "RedBolt.Bindings.Diligent",
                    "DiligentNativeBindings",
                    "RedBolt.Native",
                    "redbolt_diligent",
                    "-xc++",
                    "-std=c++20",
                    true),
                "dearimgui" => new BindingSurfaceDescriptor(
                    "dearimgui",
                    "RedBolt.Bindings.DearImGui",
                    "RedBolt.Bindings.DearImGui",
                    "ImGui",
                    "RedBolt.Native",
                    "redbolt_dearimgui",
                    "-xc++",
                    "-std=c++17",
                    true),
                _ => throw new ArgumentException($"Unknown binding surface '{surface}'.", nameof(surface))
            };
        }
    }

    private sealed class GeneratorOptions
    {
        public required string Surface { get; init; }
        public required string HeaderPath { get; init; }
        public required string HeaderIncludePath { get; init; }
        public required string OutputDirectory { get; init; }
        public required string FileStem { get; init; }
        public required string MetadataPath { get; init; }
        public required string SharpCppRoot { get; init; }
        public required string CompilerPath { get; init; }
        public required IReadOnlyList<string> IncludeDirectories { get; init; }
        public required IReadOnlyList<string> Defines { get; init; }
        public required IReadOnlyList<string> OwnedHeaders { get; init; }

        public static GeneratorOptions Parse(string[] args)
        {
            var includes = GetRepeatedOption(args, "--include").Select(Path.GetFullPath).ToArray();
            var defines = GetRepeatedOption(args, "--define").ToArray();
            var ownedHeaders = GetRepeatedOption(args, "--owned-header").Select(Path.GetFullPath).ToArray();

            return new GeneratorOptions
            {
                Surface = RequireOption(args, "--surface"),
                HeaderPath = Path.GetFullPath(RequireOption(args, "--header")),
                HeaderIncludePath = RequireOption(args, "--include-name"),
                OutputDirectory = Path.GetFullPath(RequireOption(args, "--output-dir")),
                FileStem = RequireOption(args, "--file-stem"),
                MetadataPath = Path.GetFullPath(RequireOption(args, "--metadata")),
                SharpCppRoot = Path.GetFullPath(RequireOption(args, "--sharpcpp-root")),
                CompilerPath = Path.GetFullPath(RequireOption(args, "--compiler")),
                IncludeDirectories = includes,
                Defines = defines,
                OwnedHeaders = ownedHeaders
            };
        }

        private static string RequireOption(string[] args, string name)
        {
            var value = GetOption(args, name);
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException($"Missing required option '{name}'.");

            return value;
        }

        private static string? GetOption(string[] args, string name)
        {
            for (var i = 0; i < args.Length - 1; ++i)
            {
                if (string.Equals(args[i], name, StringComparison.Ordinal))
                    return args[i + 1];
            }

            return null;
        }

        private static IEnumerable<string> GetRepeatedOption(string[] args, string name)
        {
            for (var i = 0; i < args.Length - 1; ++i)
            {
                if (string.Equals(args[i], name, StringComparison.Ordinal))
                    yield return args[i + 1];
            }
        }
    }
}
