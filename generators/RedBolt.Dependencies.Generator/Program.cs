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

    private static int Main(string[] args)
    {
        SharpCppLog.Configure();

        try
        {
            var options = GeneratorOptions.Parse(args);
            var descriptor = BindingSurfaceDescriptor.Create(options.Surface);
            Directory.CreateDirectory(options.OutputDirectory);

            var clangArguments = BuildClangArguments(options, descriptor);
            var marshallerHeaders = descriptor.UseMarshallerConfiguration
                ? new[] { Path.Combine(options.SharpCppRoot, "include", "SharpCpp", "Marshaller.hpp") }
                : Array.Empty<string>();

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
                    OwnedFileRoots: options.OwnedHeaders));

            compilation.NamespaceMappings = descriptor.Surface == "diligent"
                ? new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Diligent"] = descriptor.ManagedNamespace
            }
                : new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (var marshallerHeader in marshallerHeaders)
                compilation.NativeSupportHeaders.Add(marshallerHeader.Replace('\\', '/'));

            BindingCompilationBootstrapPipeline.Apply(compilation, marshallerHeaders, clangArguments);
            var prunedCount = PruneDependencySurface(compilation, descriptor);
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

    private static int PruneDependencySurface(BindingCompilation compilation, BindingSurfaceDescriptor descriptor)
    {
        if (descriptor.Surface is "sdl" or "diligent")
            return 0;

        var prunedCount = PruneMembers(compilation.Members, descriptor);
        compilation.InvalidateCaches();
        return prunedCount;
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
        Console.Error.WriteLine("Usage: RedBolt.Dependencies.Generator --surface <sdl|diligent> --header <path> --include-name <include> --output-dir <dir> --file-stem <stem> --metadata <path> --sharpcpp-root <path> --compiler <path> [--include <dir>]... [--define <name[=value]>]... [--owned-header <path>]...");
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
                    false),
                "diligent" => new BindingSurfaceDescriptor(
                    "diligent",
                    "RedBolt.Bindings.Diligent",
                    "RedBolt.Bindings.Diligent",
                    "DiligentNativeBindings",
                    "RedBolt.Native",
                    "redbolt_diligent",
                    "-xc++",
                    "-std=c++20",
                    false),
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
