using Microsoft.Extensions.Logging;
using SharpCpp.Metadata.Export;
using SharpCpp.Metadata.Import;
using SharpCpp.Model.Ast;
using SharpCpp.Model.Logging;
using SharpCpp.Parsing;
using SharpCpp.Pipeline;
using SharpCpp.Rendering;

namespace Rbfx.UserCode.Generator;

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
        "SDL_DISABLE_PMMINTRIN_H=1",
        "IMGUI_DISABLE_SSE=1"
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
        "-Wno-pragma-once-outside-header"
    ];

    private static readonly ILogger Logger = SharpCppLog.For(nameof(Program));

    private const string ProjectName = "Rbfx.UserCode.Bindings";
    private const string ManagedNamespace = "RbfxUserCode";
    private const string ManagedClassName = "Bindings";
    private const string NativeLibraryName = "SharpCpp.Rbfx.UserCode.Native";
    private const string GeneratedFileStem = "Rbfx.UserCode";
    private const string EntryPointPrefix = "scrbfxuc";
    private const string HeaderIncludePath = "RbfxUserCode/RotatingComponent.hpp";

    public static int Main(string[] args)
    {
        var options = GeneratorOptions.Parse(args);
        var fileWriter = options.LogFilePath is not null
            ? new StreamWriter(options.LogFilePath, append: false) { AutoFlush = true }
            : null;
        SharpCppLog.Configure(minLevel: LogLevel.Information, fileWriter: fileWriter);

        try
        {
            var sdkRoot = Path.GetFullPath(options.SdkRoot);
            var includeRoot = Path.Combine(sdkRoot, "include");
            var projectRoot = Environment.CurrentDirectory;
            var userCodeRoot = Path.GetFullPath(options.UserCodeRoot);
            var userIncludeRoot = Path.Combine(userCodeRoot, "include");
            var headerPath = Path.Combine(userIncludeRoot, HeaderIncludePath);
            if (!File.Exists(headerPath))
            {
                throw new InvalidOperationException($"User code header was not found at '{headerPath}'.");
            }

            var marshallerConfigurationFiles = new[]
            {
                Path.Combine(projectRoot, "include", "SharpCpp", "Marshaller.hpp"),
                Path.Combine(projectRoot, "rbfx", "include", "SharpCpp", "detail", "RbfxRefCountedMarshallers.hpp")
            }.Select(Path.GetFullPath).ToArray();

            Logger.LogInformation("rbfx SDK root: {SdkRoot}", sdkRoot);
            Logger.LogInformation("Flags file: {FlagsFilePath}", options.FlagsFilePath);
            Logger.LogInformation("User code root: {UserCodeRoot}", userCodeRoot);
            Logger.LogInformation("Generated output root: {GeneratedRoot}", options.GeneratedRoot);

            var importedBindings = BindingMetadataImportCatalog.Load(options.ImportedBindingMetadataFiles);
            foreach (var metadataFile in options.ImportedBindingMetadataFiles)
            {
                Logger.LogInformation("Imported binding metadata: '{MetadataFile}'", metadataFile);
            }

            var sdkFlags = SdkParseFlags.Load(options.FlagsFilePath);
            var clangArguments = BuildClangArguments(projectRoot, includeRoot, userIncludeRoot, sdkFlags);
            var parser = new ClangSharpHeaderParser();
            var compilation = parser.Parse(
                new ClangSharpParseRequest(
                    ProjectName,
                    ManagedNamespace,
                    ManagedClassName,
                    NativeLibraryName,
                    headerPath,
                    HeaderIncludePath,
                    clangArguments,
                    marshallerConfigurationFiles,
                    EntryPointPrefix,
                    OwnedFileRoots: new[] { userIncludeRoot },
                    ImportedBindingMetadata: importedBindings));

            compilation.NamespaceMappings = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Urho3D"] = "Urho3D",
                ["RbfxUserCode"] = "RbfxUserCode"
            };
            foreach (var marshallerHeader in marshallerConfigurationFiles)
            {
                compilation.NativeSupportHeaders.Add(marshallerHeader.Replace('\\', '/'));
            }

            BindingCompilationBootstrapPipeline.Apply(compilation, marshallerConfigurationFiles, clangArguments);

            var renderedBindings = BindingOutputRenderer.Render(compilation, GeneratedFileStem);
            var metadataDocument = BindingMetadataExporter.Export(
                compilation,
                new BindingMetadataExportOptions(
                    PackageId: ProjectName,
                    Phase2ClosureHash: "rbfx-usercode-phase2",
                    ClangArgsHash: "rbfx-usercode-clang",
                    Imports: importedBindings.Imports));

            Directory.CreateDirectory(options.GeneratedRoot);
            renderedBindings.WriteToDirectory(options.GeneratedRoot);
            File.WriteAllText(
                Path.Combine(options.GeneratedRoot, $"{GeneratedFileStem}.Bindings.json"),
                BindingMetadataSerializer.Serialize(metadataDocument));

            Logger.LogInformation("Generated user-code bindings for {Retained} retained local record(s).", compilation.Records.Count(record => !record.IsIgnored() && !record.GetMetadata<bool>(MetadataKeys.SuppressEmission)));
            return 0;
        }
        catch (Exception exception)
        {
            Logger.LogError(exception, "Unhandled exception in user-code generator");
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static IReadOnlyList<string> BuildClangArguments(string projectRoot, string sdkIncludeRoot, string userIncludeRoot, SdkParseFlags sdkFlags)
    {
        var arguments = new List<string>
        {
            "-xc++",
            $"-std={sdkFlags.LanguageStandard}",
            $"-I{userIncludeRoot}"
        };

        arguments.AddRange(ParseWarningDisableArguments.Where(argument => !arguments.Contains(argument, StringComparer.Ordinal)));

        if (!sdkFlags.IncludeDirectories.Contains(sdkIncludeRoot, StringComparer.Ordinal))
        {
            arguments.Add($"-I{sdkIncludeRoot}");
        }

        foreach (var includeDirectory in sdkFlags.IncludeDirectories)
        {
            arguments.Add($"-I{includeDirectory}");
        }

        var projectInclude = Path.Combine(projectRoot, "include");
        if (!arguments.Contains($"-I{projectInclude}", StringComparer.Ordinal))
        {
            arguments.Add($"-I{projectInclude}");
        }

        foreach (var define in sdkFlags.Defines)
        {
            arguments.Add($"-D{define}");
        }

        foreach (var define in ParseCompatibilityDefines)
        {
            var argument = $"-D{define}";
            if (!arguments.Contains(argument, StringComparer.Ordinal))
            {
                arguments.Add(argument);
            }
        }

        arguments.AddRange(sdkFlags.AdditionalArguments);
        arguments.AddRange(ParseSimdDisableArguments);

        var toolchainInfo = CompilerToolchainDiscovery.Discover(sdkFlags.CompilerPath);
        if (!string.IsNullOrWhiteSpace(toolchainInfo.TargetTriple) &&
            !arguments.Any(static argument => argument.StartsWith("--target=", StringComparison.Ordinal) || argument == "-target"))
        {
            arguments.Add($"--target={toolchainInfo.TargetTriple}");
        }

        if (!string.IsNullOrWhiteSpace(toolchainInfo.ClangResourceDirectory) &&
            !arguments.Any(static argument => argument.StartsWith("-resource-dir=", StringComparison.Ordinal) || argument == "-resource-dir"))
        {
            arguments.Add($"-resource-dir={toolchainInfo.ClangResourceDirectory}");
        }

        foreach (var systemIncludeDirectory in toolchainInfo.IncludeDirectories)
        {
            arguments.Add($"-isystem{systemIncludeDirectory}");
        }

        return arguments.Distinct(StringComparer.Ordinal).ToArray();
    }
}
