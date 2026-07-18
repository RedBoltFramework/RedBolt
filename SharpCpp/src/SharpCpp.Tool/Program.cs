using SharpCpp.Metadata.Export;
using SharpCpp.Model.Ast;
using SharpCpp.Parsing;
using SharpCpp.Pipeline;
using SharpCpp.Rendering;
using Microsoft.Extensions.Logging;
using SharpCpp.Model.Logging;

namespace SharpCpp.Tool;

internal static class Program
{
    private static readonly ILogger Logger = SharpCppLog.For(nameof(Program));

    private static int Main(string[] args)
    {
        if (args.Length == 0 || args[0] is "-h" or "--help")
        {
            PrintUsage();
            return 0;
        }

        SharpCppLog.Configure();

        return args[0] switch
        {
            "sample-metadata" => EmitSampleMetadata(args.Skip(1).ToArray()),
            "generate-sample" => GenerateSampleBindings(args.Skip(1).ToArray()),
            _ => UnknownCommand(args[0])
        };
    }

    private static int EmitSampleMetadata(string[] args)
    {
        var compilation = SampleCompilationFactory.CreateMetadataSample();
        var document = BindingMetadataExporter.Export(
            compilation,
            new BindingMetadataExportOptions(
                PackageId: compilation.ProjectName,
                Phase2ClosureHash: "sample-phase2",
                ClangArgsHash: "sample-clang"));

        var json = BindingMetadataSerializer.Serialize(document);

        if (args.Length == 0)
        {
            Console.Write(json);
            return 0;
        }

        var outputPath = Path.GetFullPath(args[0]);
        var outputDirectory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        File.WriteAllText(outputPath, json);
        Logger.LogInformation("Wrote sample metadata to {OutputPath}", outputPath);
        return 0;
    }

    private static int GenerateSampleBindings(string[] args)
    {
        var generatedRoot = GetPathOption(args, "--generated-root");
        if (string.IsNullOrWhiteSpace(generatedRoot))
        {
            Logger.LogError("Missing required option '--generated-root'.");
            PrintUsage();
            return 1;
        }

        var logFilePath = GetPathOption(args, "--log-file");
        using var logWriter = string.IsNullOrWhiteSpace(logFilePath) ? null
            : new StreamWriter(logFilePath, append: false) { AutoFlush = true };
        SharpCppLog.Configure(fileWriter: logWriter);

        var fileStem = GetOptionValue(args, "--file-stem") ?? "SharpCppTestsE2E";
        var projectRoot = GetPathOption(args, "--project-root") ?? Environment.CurrentDirectory;
        var fixtureRoot = Path.Combine(projectRoot, "tests", "SharpCpp.Tests.E2E");
        var parser = new ClangSharpHeaderParser();
        var clangArguments = new List<string>
        {
            "-xc++",
            "-std=c++20",
            "-Wno-unknown-attributes",
            $"-I{Path.Combine(projectRoot, "include")}",
            $"-I{fixtureRoot}"
        };
        clangArguments.AddRange(GetSystemCppIncludeArguments());
        var marshallerConfigurationFiles = new[]
        {
            Path.Combine(projectRoot, "include", "SharpCpp", "Marshaller.hpp")
        }
        .Concat(Directory.EnumerateFiles(fixtureRoot, "*Marshallers.h", SearchOption.AllDirectories)
            .OrderBy(static path => path, StringComparer.Ordinal))
        .ToArray();

        var compilation = new BindingCompilation(
            "SharpCpp.Tests.E2E",
            "SharpCpp.Tests.E2E.Generated",
            "E2EBindings",
            "SharpCppTestsE2E_Native",
            Array.Empty<string>());
        foreach (var marshallerHeader in marshallerConfigurationFiles)
        {
            compilation.NativeSupportHeaders.Add(marshallerHeader.Replace('\\', '/'));
        }

        var headerFiles = Directory
            .EnumerateFiles(fixtureRoot, "*.hpp", SearchOption.AllDirectories)
            .OrderBy(static path => path, StringComparer.Ordinal)
            .ToArray();

        foreach (var headerFilePath in headerFiles)
        {
            var relativeHeaderPath = Path.GetRelativePath(fixtureRoot, headerFilePath).Replace('\\', '/');
            var parsedHeaderCompilation = parser.Parse(
                new ClangSharpParseRequest(
                    ProjectName: compilation.ProjectName,
                    ManagedNamespace: compilation.ManagedNamespace,
                    ManagedClassName: compilation.ManagedClassName,
                    NativeLibraryName: compilation.NativeLibraryName,
                    HeaderFilePath: headerFilePath,
                    HeaderIncludePath: relativeHeaderPath,
                    ClangArguments: clangArguments,
                    MarshallerConfigurationFiles: marshallerConfigurationFiles,
                    EntryPointPrefix: "sharpcpp_e2e"));

            compilation.HeaderFiles.Add(relativeHeaderPath);
            foreach (var member in parsedHeaderCompilation.Members.ToArray())
            {
                compilation.Members.Add(member);
            }
        }

        BindingCompilationBootstrapPipeline.Apply(compilation, marshallerConfigurationFiles, clangArguments);

        var renderedBindings = BindingOutputRenderer.Render(compilation, fileStem, headerPath =>
        {
            if (string.IsNullOrEmpty(headerPath))
                return $"{fileStem}.Bindings";
            var group = Path.ChangeExtension(headerPath, null).Replace('/', '.').Replace('\\', '.');
            return $"{fileStem}.{group}";
        });
        var metadataDocument = BindingMetadataExporter.Export(
            compilation,
            new BindingMetadataExportOptions(
                PackageId: compilation.ProjectName,
                Phase2ClosureHash: "e2e-sample-phase2",
                ClangArgsHash: "e2e-sample-clang"));

        Directory.CreateDirectory(generatedRoot);
        renderedBindings.WriteToDirectory(generatedRoot);
        File.WriteAllText(
            Path.Combine(generatedRoot, $"{fileStem}.Bindings.json"),
            BindingMetadataSerializer.Serialize(metadataDocument));
        Logger.LogInformation("Wrote sample bindings to {GeneratedRoot}", generatedRoot);
        return 0;
    }

    private static int UnknownCommand(string command)
    {
        Logger.LogError("Unknown command '{Command}'.", command);
        PrintUsage();
        return 1;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("SharpCpp bootstrap tool");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  sample-metadata [output-path]   Emit deterministic sample metadata.");
        Console.WriteLine("  generate-sample --generated-root <dir> [--project-root <dir>] [--file-stem <stem>] [--log-file <path>]   Emit sample bindings artifacts.");
    }

    private static IEnumerable<string> GetSystemCppIncludeArguments()
    {
        foreach (var includePath in GetSystemCppIncludePaths())
        {
            yield return $"-I{includePath}";
        }
    }

    private static IEnumerable<string> GetSystemCppIncludePaths()
    {
        var cppRoot = "/usr/include/c++";
        var cppVersionDir = Directory.Exists(cppRoot)
            ? Directory.EnumerateDirectories(cppRoot)
                .OrderByDescending(Path.GetFileName, StringComparer.Ordinal)
                .FirstOrDefault()
            : null;

        if (!string.IsNullOrWhiteSpace(cppVersionDir))
        {
            yield return cppVersionDir;

            var targetDir = Path.Combine(cppVersionDir, "x86_64-pc-linux-gnu");
            if (Directory.Exists(targetDir))
            {
                yield return targetDir;
            }

            var backwardDir = Path.Combine(cppVersionDir, "backward");
            if (Directory.Exists(backwardDir))
            {
                yield return backwardDir;
            }
        }

        var gccRoot = "/usr/lib/gcc/x86_64-pc-linux-gnu";
        var gccIncludeDir = Directory.Exists(gccRoot)
            ? Directory.EnumerateDirectories(gccRoot)
                .OrderByDescending(Path.GetFileName, StringComparer.Ordinal)
                .Select(path => Path.Combine(path, "include"))
                .FirstOrDefault(Directory.Exists)
            : null;
        if (!string.IsNullOrWhiteSpace(gccIncludeDir))
        {
            yield return gccIncludeDir;
        }

        yield return "/usr/include";
    }

    private static string? GetOptionValue(IReadOnlyList<string> args, string name)
    {
        for (var index = 0; index < args.Count - 1; index++)
        {
            if (string.Equals(args[index], name, StringComparison.Ordinal))
            {
                return args[index + 1];
            }
        }

        return null;
    }

    private static string? GetPathOption(IReadOnlyList<string> args, string name)
    {
        var value = GetOptionValue(args, name);
        return string.IsNullOrWhiteSpace(value) ? null : Path.GetFullPath(value);
    }
}
