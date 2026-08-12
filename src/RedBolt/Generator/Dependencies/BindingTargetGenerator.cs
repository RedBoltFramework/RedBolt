namespace RedBolt.Generator.Dependencies;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using SharpCpp.Metadata.Export;
using SharpCpp.Model.Ast;
using SharpCpp.Parsing;
using SharpCpp.Pipeline;
using SharpCpp.Rendering;

internal abstract class BindingTargetGenerator(
    string surface,
    string projectName,
    string managedNamespace,
    string managedClassName,
    string nativeLibraryName,
    string entryPointPrefix,
    string clangLanguageArgument,
    string languageStandardArgument,
    bool useMarshallerConfiguration)
{
    private static readonly IReadOnlyDictionary<string, FixedWidthAliasProjection> NativeSizeAliases =
        new Dictionary<string, FixedWidthAliasProjection>(StringComparer.Ordinal)
        {
            ["size_t"] = new(BindingPrimitiveKind.NativeUInt, "nuint"),
            ["std::size_t"] = new(BindingPrimitiveKind.NativeUInt, "nuint")
        };

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
        "-mno-avx512vl",
        "-U__SSE__"
    ];

    private static readonly string[] ParseWarningDisableArguments =
    [
        "-Wno-deprecated-literal-operator",
        "-Wno-pragma-once-outside-header",
        "-Wno-unknown-attributes"
    ];

    protected string ManagedNamespace => managedNamespace;

    protected virtual IReadOnlyDictionary<string, string> NamespaceMappings { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal);

    protected virtual IReadOnlySet<string>? DirectorRecordCppTypeNames => null;

    protected virtual string MarshallerClangLanguageArgument => clangLanguageArgument;

    protected virtual string MarshallerLanguageStandardArgument => languageStandardArgument;

    protected virtual bool IncludeCallbackReleaseRegistration => false;

    public string Generate(GeneratorOptions options)
    {
        var clangArguments = BuildClangArguments(options);
        var marshallerClangArguments = BuildMarshallerClangArguments(clangArguments);
        var marshallerHeaders = BuildMarshallerHeaders(options);

        var parser = new ClangSharpHeaderParser();
        var compilation = parser.Parse(new ClangSharpParseRequest(
            ProjectName: projectName,
            ManagedNamespace: managedNamespace,
            ManagedClassName: managedClassName,
            NativeLibraryName: nativeLibraryName,
            HeaderFilePath: options.HeaderPath,
            HeaderIncludePath: options.HeaderIncludePath,
            ClangArguments: clangArguments,
            MarshallerConfigurationFiles: marshallerHeaders,
            EntryPointPrefix: entryPointPrefix,
            OwnedFileRoots: options.OwnedHeaders,
            MarshallerClangArguments: marshallerClangArguments,
            DirectorRecordCppTypeNames: DirectorRecordCppTypeNames));

        compilation.NamespaceMappings = new Dictionary<string, string>(NamespaceMappings, StringComparer.Ordinal);
        foreach (var marshallerHeader in marshallerHeaders)
            compilation.NativeSupportHeaders.Add(marshallerHeader.Replace('\\', '/'));

        FixedWidthAliasNormalizer.Normalize(compilation, NativeSizeAliases);
        ConfigureCompilation(compilation);
        var ignoredStdlibWrapperCount = 0;
        BindingCompilationBootstrapPipeline.Apply(
            compilation,
            marshallerHeaders,
            clangArguments,
            marshallerClangArguments,
            bindingCompilation =>
            {
                ConfigureBeforeValidation(bindingCompilation);
                ignoredStdlibWrapperCount = IgnoreStdlibWrappers(bindingCompilation);
            },
            PostProcessCompilation);

        var prunedCount = ignoredStdlibWrapperCount + PruneCompilation(compilation);
        AfterPruneCompilation(compilation);

        var renderedBindings = BindingOutputRenderer.Render(
            compilation,
            options.FileStem,
            includeCallbackReleaseRegistration: IncludeCallbackReleaseRegistration);
        TransformBindings(renderedBindings).WriteToDirectory(options.OutputDirectory);

        var metadataDocument = BindingMetadataExporter.Export(
            compilation,
            new BindingMetadataExportOptions(
                PackageId: projectName,
                Phase2ClosureHash: $"redbolt-{surface}-phase2",
                ClangArgsHash: $"redbolt-{surface}-clang"));
        File.WriteAllText(options.MetadataPath, BindingMetadataSerializer.Serialize(metadataDocument));

        return
            $"Generated {surface} bindings with {compilation.Functions.Count} functions after pruning {prunedCount} unsupported declarations: {options.OutputDirectory}";
    }

    protected virtual IEnumerable<string> GetAdditionalMarshallerHeaders(GeneratorOptions options) =>
        Array.Empty<string>();

    protected virtual void ConfigureCompilation(BindingCompilation compilation)
    {
    }

    protected virtual void ConfigureBeforeValidation(BindingCompilation compilation)
    {
    }

    protected virtual void PostProcessCompilation(BindingCompilation compilation)
    {
    }

    protected virtual int PruneCompilation(BindingCompilation compilation) => 0;

    protected virtual void AfterPruneCompilation(BindingCompilation compilation)
    {
    }

    protected virtual GeneratedBindingSet TransformBindings(GeneratedBindingSet bindings) => bindings;

    protected virtual bool RetainStdlibWrapper(RecordBinding record) => false;

    private List<string> BuildClangArguments(GeneratorOptions options)
    {
        var clangArguments = new List<string>
        {
            clangLanguageArgument,
            languageStandardArgument
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

        var sharpCppIncludeRoot = Path.Combine(options.SharpCppRoot, "include");
        if (!clangArguments.Contains($"-I{sharpCppIncludeRoot}", StringComparer.Ordinal))
            clangArguments.Add($"-I{sharpCppIncludeRoot}");

        var toolchainInfo = CompilerToolchainDiscovery.Discover(
            options.CompilerPath,
            options.CompilerTarget,
            options.CompilerSysroot);
        var targetTriple = toolchainInfo.TargetTriple;
        var isX86Target = string.IsNullOrWhiteSpace(targetTriple) ||
            targetTriple.StartsWith("x86", StringComparison.OrdinalIgnoreCase) ||
            targetTriple.StartsWith("i386", StringComparison.OrdinalIgnoreCase) ||
            targetTriple.StartsWith("i486", StringComparison.OrdinalIgnoreCase) ||
            targetTriple.StartsWith("i586", StringComparison.OrdinalIgnoreCase) ||
            targetTriple.StartsWith("i686", StringComparison.OrdinalIgnoreCase);
        if (isX86Target)
            clangArguments.AddRange(ParseSimdDisableArguments);

        if (!string.IsNullOrWhiteSpace(toolchainInfo.TargetTriple) &&
            !clangArguments.Any(static argument =>
                argument.StartsWith("--target=", StringComparison.Ordinal) || argument == "-target"))
        {
            clangArguments.Add($"--target={toolchainInfo.TargetTriple}");
        }

        if (!string.IsNullOrWhiteSpace(options.CompilerSysroot))
            clangArguments.Add($"--sysroot={options.CompilerSysroot}");

        // Android's arm64 and x86_64 defaults disagree on plain-char
        // signedness. Use one projection so the managed API is ABI-invariant.
        if (options.CompilerTarget?.Contains("android", StringComparison.OrdinalIgnoreCase) == true)
            clangArguments.Add("-fsigned-char");

        if (!string.IsNullOrWhiteSpace(toolchainInfo.ClangResourceDirectory) &&
            !clangArguments.Any(static argument =>
                argument.StartsWith("-resource-dir=", StringComparison.Ordinal) || argument == "-resource-dir"))
        {
            clangArguments.Add($"-resource-dir={toolchainInfo.ClangResourceDirectory}");
        }

        foreach (var systemIncludeDirectory in toolchainInfo.IncludeDirectories)
            clangArguments.Add($"-isystem{systemIncludeDirectory}");

        return clangArguments.Distinct(StringComparer.Ordinal).ToList();
    }

    private List<string> BuildMarshallerClangArguments(IReadOnlyList<string> clangArguments)
    {
        if (!useMarshallerConfiguration ||
            clangLanguageArgument == MarshallerClangLanguageArgument &&
            languageStandardArgument == MarshallerLanguageStandardArgument)
        {
            return clangArguments.ToList();
        }

        var marshallerArguments = new List<string>(clangArguments.Count);
        foreach (var argument in clangArguments)
        {
            if (string.Equals(argument, clangLanguageArgument, StringComparison.Ordinal))
                marshallerArguments.Add(MarshallerClangLanguageArgument);
            else if (string.Equals(argument, languageStandardArgument, StringComparison.Ordinal))
                marshallerArguments.Add(MarshallerLanguageStandardArgument);
            else
                marshallerArguments.Add(argument);
        }
        return marshallerArguments;
    }

    private List<string> BuildMarshallerHeaders(GeneratorOptions options)
    {
        if (!useMarshallerConfiguration)
            return [];

        return
        [
            .. new[]
            {
                Path.Combine(options.SharpCppRoot, "include", "SharpCpp", "Marshaller.hpp")
            }.Concat(GetAdditionalMarshallerHeaders(options))
                .Select(Path.GetFullPath)
                .Distinct(StringComparer.Ordinal)
        ];
    }

    private int IgnoreStdlibWrappers(BindingCompilation compilation)
    {
        var ignoredCount = 0;
        foreach (var record in compilation.Records)
        {
            if (record.IsIgnored() ||
                !record.CppQualifiedName.StartsWith("std::", StringComparison.Ordinal) ||
                !string.IsNullOrWhiteSpace(record.GetMetadata<string>(MetadataKeys.ManagedWrapperAssembly)) ||
                RetainStdlibWrapper(record))
            {
                continue;
            }

            record.SetIgnoreReason("std library wrapper emission is disabled in RedBolt for now");
            ignoredCount++;
        }
        return ignoredCount;
    }
}
