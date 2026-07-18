namespace Rbfx.UserCode.Generator;

internal sealed class GeneratorOptions
{
    public required string SdkRoot { get; init; }

    public required string FlagsFilePath { get; init; }

    public required string UserCodeRoot { get; init; }

    public required string GeneratedRoot { get; init; }

    public IReadOnlyList<string> ImportedBindingMetadataFiles { get; init; } = Array.Empty<string>();

    public string? LogFilePath { get; init; }

    public static GeneratorOptions Parse(IReadOnlyList<string> args)
    {
        if (args.Count == 0 || args.Any(static argument => argument is "--help" or "-h"))
        {
            PrintUsage();
            Environment.Exit(0);
        }

        var sdkRoot = GetRequiredOption(args, "--sdk-root");
        var flagsFilePath = GetRequiredOption(args, "--flags-file");
        var userCodeRoot = GetRequiredOption(args, "--user-code-root");
        var generatedRoot = GetRequiredOption(args, "--generated-root");
        var importedBindingMetadataFiles = GetRepeatedPathOption(args, "--import-binding-metadata");
        var logFilePath = GetOptionalPathOption(args, "--log-file");

        ValidatePath(sdkRoot, isDirectory: true, "--sdk-root");
        ValidatePath(flagsFilePath, isDirectory: false, "--flags-file");
        ValidatePath(userCodeRoot, isDirectory: true, "--user-code-root");

        foreach (var metadataFile in importedBindingMetadataFiles)
        {
            ValidatePath(metadataFile, isDirectory: false, "--import-binding-metadata");
        }

        return new GeneratorOptions
        {
            SdkRoot = sdkRoot,
            FlagsFilePath = flagsFilePath,
            UserCodeRoot = userCodeRoot,
            GeneratedRoot = generatedRoot,
            ImportedBindingMetadataFiles = importedBindingMetadataFiles,
            LogFilePath = logFilePath
        };
    }

    private static void PrintUsage()
    {
        Console.WriteLine(
            """
            Usage:
              dotnet Rbfx.UserCode.Generator.dll --sdk-root <path> --flags-file <path> --user-code-root <path> --generated-root <path> [--import-binding-metadata <path> ...] [--log-file <path>]
            """);
    }

    private static string GetRequiredOption(IReadOnlyList<string> args, string optionName)
    {
        for (var index = 0; index < args.Count - 1; index++)
        {
            if (string.Equals(args[index], optionName, StringComparison.Ordinal))
            {
                return Path.GetFullPath(args[index + 1]);
            }
        }

        throw new ArgumentException($"Missing required option '{optionName}'.");
    }

    private static string? GetOptionalPathOption(IReadOnlyList<string> args, string optionName)
    {
        for (var index = 0; index < args.Count - 1; index++)
        {
            if (string.Equals(args[index], optionName, StringComparison.Ordinal))
            {
                return Path.GetFullPath(args[index + 1]);
            }
        }

        return null;
    }

    private static IReadOnlyList<string> GetRepeatedPathOption(IReadOnlyList<string> args, string optionName)
    {
        var values = new List<string>();

        for (var index = 0; index < args.Count - 1; index++)
        {
            if (string.Equals(args[index], optionName, StringComparison.Ordinal))
            {
                values.Add(Path.GetFullPath(args[index + 1]));
                index++;
            }
        }

        return values;
    }

    private static void ValidatePath(string path, bool isDirectory, string optionName)
    {
        var exists = isDirectory ? Directory.Exists(path) : File.Exists(path);
        if (!exists)
        {
            throw new ArgumentException($"The path for '{optionName}' does not exist: '{path}'.");
        }
    }
}
