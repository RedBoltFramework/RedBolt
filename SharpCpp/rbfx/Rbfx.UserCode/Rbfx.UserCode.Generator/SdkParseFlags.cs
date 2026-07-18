namespace Rbfx.UserCode.Generator;

internal sealed class SdkParseFlags
{
    private SdkParseFlags(
        string compilerPath,
        string languageStandard,
        IReadOnlyList<string> includeDirectories,
        IReadOnlyList<string> defines,
        IReadOnlyList<string> additionalArguments)
    {
        CompilerPath = compilerPath;
        LanguageStandard = languageStandard;
        IncludeDirectories = includeDirectories;
        Defines = defines;
        AdditionalArguments = additionalArguments;
    }

    public string CompilerPath { get; }

    public string LanguageStandard { get; }

    public IReadOnlyList<string> IncludeDirectories { get; }

    public IReadOnlyList<string> Defines { get; }

    public IReadOnlyList<string> AdditionalArguments { get; }

    public static SdkParseFlags Load(string filePath)
    {
        var compilerPath = string.Empty;
        var languageStandard = "gnu++20";
        var includeDirectories = new List<string>();
        var defines = new List<string>();
        var additionalArguments = new List<string>();

        foreach (var rawLine in File.ReadLines(filePath))
        {
            var line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
            {
                continue;
            }

            var separatorIndex = line.IndexOf('=');
            if (separatorIndex < 0)
            {
                continue;
            }

            var key = line[..separatorIndex];
            var value = line[(separatorIndex + 1)..];
            switch (key)
            {
                case "compiler_path":
                    compilerPath = value;
                    break;
                case "language_standard":
                    languageStandard = value;
                    break;
                case "include_directory":
                    includeDirectories.Add(value);
                    break;
                case "define":
                    defines.Add(value);
                    break;
                case "clang_argument":
                    additionalArguments.Add(value);
                    break;
            }
        }

        return new SdkParseFlags(
            compilerPath,
            languageStandard,
            includeDirectories.Distinct(StringComparer.Ordinal).ToArray(),
            defines.Distinct(StringComparer.Ordinal).ToArray(),
            additionalArguments.Distinct(StringComparer.Ordinal).ToArray());
    }
}
