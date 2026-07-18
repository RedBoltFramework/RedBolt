namespace SharpCpp.Rbfx.Generator;

internal sealed class GeneratorOptions
{
    public required string SdkRoot { get; init; }

    public required string FlagsFilePath { get; init; }

    public required string GeneratedRoot { get; init; }

    public required string ProjectRoot { get; init; }

    public string? SdkLibraryPath { get; init; }

    public string? LogFilePath { get; init; }

    public static GeneratorOptions Parse(IReadOnlyList<string> args)
    {
        if (args.Count == 0 || args.Any(static argument => argument is "--help" or "-h"))
        {
            PrintUsage();
            Environment.Exit(0);
        }

        var sdkRoot = GetRequiredPathOption(args, "--sdk-root", isDirectory: true);
        var flagsFilePath = GetRequiredPathOption(args, "--flags-file", isDirectory: false);
        var generatedRoot = GetRequiredPathOption(args, "--generated-root", isDirectory: false, mustExist: false);
        var projectRoot = GetRequiredPathOption(args, "--project-root", isDirectory: true);
        var sdkLibraryPath = GetOptionalPathOption(args, "--sdk-library", isDirectory: false);
        var logFilePath = GetOptionalPathOption(args, "--log-file", isDirectory: false, mustExist: false);

        ValidateKnownOptions(args, new Dictionary<string, bool>(StringComparer.Ordinal)
        {
            ["--sdk-root"] = true,
            ["--flags-file"] = true,
            ["--generated-root"] = true,
            ["--project-root"] = true,
            ["--sdk-library"] = true,
            ["--log-file"] = true
        });

        return new GeneratorOptions
        {
            SdkRoot = sdkRoot,
            FlagsFilePath = flagsFilePath,
            GeneratedRoot = generatedRoot,
            ProjectRoot = projectRoot,
            SdkLibraryPath = sdkLibraryPath,
            LogFilePath = logFilePath
        };
    }

    public static void PrintUsage()
    {
        Console.WriteLine(
            "Usage: dotnet SharpCpp.Rbfx.Generator.dll --sdk-root <path> --flags-file <path> --generated-root <path> --project-root <path> [--sdk-library <path>] [--log-file <path>]");
    }

    private static void ValidateKnownOptions(IReadOnlyList<string> args, IReadOnlyDictionary<string, bool> knownOptions)
    {
        for (var index = 0; index < args.Count; index++)
        {
            var argument = args[index];
            if (!argument.StartsWith("--", StringComparison.Ordinal))
            {
                continue;
            }

            if (!knownOptions.TryGetValue(argument, out var requiresValue))
            {
                throw new ArgumentException($"Unknown option '{argument}'.");
            }

            if (!requiresValue)
            {
                continue;
            }

            if (index == args.Count - 1 || args[index + 1].StartsWith("--", StringComparison.Ordinal))
            {
                throw new ArgumentException($"Missing value for option '{argument}'.");
            }

            index++;
        }
    }

    private static string GetRequiredPathOption(IReadOnlyList<string> args, string optionName, bool isDirectory, bool mustExist = true)
    {
        var value = GetOptionalPathOption(args, optionName, isDirectory, mustExist);
        return value ?? throw new ArgumentException($"Missing required option '{optionName}'.");
    }

    private static string? GetOptionalPathOption(IReadOnlyList<string> args, string optionName, bool isDirectory, bool mustExist = true)
    {
        for (var index = 0; index < args.Count - 1; index++)
        {
            if (!string.Equals(args[index], optionName, StringComparison.Ordinal))
            {
                continue;
            }

            var path = Path.GetFullPath(args[index + 1]);
            if (!mustExist)
            {
                return path;
            }

            var exists = isDirectory ? Directory.Exists(path) : File.Exists(path);
            if (!exists)
            {
                throw new ArgumentException($"The path for '{optionName}' does not exist: '{path}'.");
            }

            return path;
        }

        return null;
    }
}
