namespace SharpCpp.Rbfx.Generator;

internal sealed class SdkParseFlags
{
    private static readonly HashSet<string> IgnoredDefines = new(StringComparer.Ordinal)
    {
        "URHO3D_SSE",
        "URHO3D_AVX",
        "URHO3D_NEON",
        "BT_USE_SSE",
        "BT_USE_AVX",
        "BT_USE_NEON"
    };

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

    public bool HasDefine(string name, string? expectedValue = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        foreach (var define in Defines)
        {
            var separatorIndex = define.IndexOf('=');
            var defineName = separatorIndex >= 0 ? define[..separatorIndex] : define;
            if (!string.Equals(defineName, name, StringComparison.Ordinal))
            {
                continue;
            }

            if (expectedValue is null)
            {
                return true;
            }

            var defineValue = separatorIndex >= 0 ? define[(separatorIndex + 1)..] : string.Empty;
            if (string.Equals(defineValue, expectedValue, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    public static SdkParseFlags Load(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        string? compilerPath = null;
        string? languageStandard = null;
        var includeDirectories = new List<string>();
        var defines = new List<string>();
        var additionalArguments = new List<string>();

        foreach (var rawLine in File.ReadAllLines(filePath))
        {
            var line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var separatorIndex = line.IndexOf('=');
            if (separatorIndex <= 0)
            {
                continue;
            }

            var key = line[..separatorIndex];
            var value = line[(separatorIndex + 1)..];

            switch (key)
            {
                case "compiler_path":
                    compilerPath = Path.GetFullPath(value);
                    break;
                case "language_standard":
                    languageStandard = NormalizeLanguageStandard(value);
                    break;
                case "include_directory":
                    includeDirectories.Add(Path.GetFullPath(value));
                    break;
                case "define":
                    if (!TryGetIgnoredDefineReason(value, out _))
                    {
                        defines.Add(value);
                    }
                    break;
                case "clang_argument":
                    if (!TryGetIgnoredArgumentReason(value, out _))
                    {
                        additionalArguments.Add(value);
                    }
                    break;
            }
        }

        if (string.IsNullOrWhiteSpace(compilerPath))
        {
            throw new InvalidOperationException($"SDK flags file '{filePath}' did not contain a compiler path.");
        }

        return new SdkParseFlags(
            compilerPath,
            string.IsNullOrWhiteSpace(languageStandard) ? "gnu++20" : languageStandard,
            includeDirectories.Distinct(StringComparer.Ordinal).ToArray(),
            defines.Distinct(StringComparer.Ordinal).ToArray(),
            additionalArguments.ToArray());
    }

    private static bool TryGetIgnoredArgumentReason(string argument, out string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(argument);

        if (argument.StartsWith("-W", StringComparison.Ordinal) ||
            argument.StartsWith("/W", StringComparison.Ordinal))
        {
            reason = "warning level flags do not affect libclang parsing";
            return true;
        }

        if (argument.StartsWith("/arch:", StringComparison.OrdinalIgnoreCase))
        {
            reason = "MSVC architecture flags are not understood by libclang on this path";
            return true;
        }

        if (argument.StartsWith("-march=", StringComparison.Ordinal) ||
            argument.StartsWith("-mcpu=", StringComparison.Ordinal) ||
            argument.StartsWith("-mtune=", StringComparison.Ordinal))
        {
            reason = "architecture tuning flags can imply SIMD features that are intentionally disabled during parsing";
            return true;
        }

        if (!argument.StartsWith("-m", StringComparison.Ordinal))
        {
            reason = string.Empty;
            return false;
        }

        var feature = argument[2..].ToLowerInvariant();
        if (feature.StartsWith("sse", StringComparison.Ordinal) ||
            feature.StartsWith("ssse", StringComparison.Ordinal) ||
            feature.StartsWith("avx", StringComparison.Ordinal) ||
            feature.StartsWith("mmx", StringComparison.Ordinal) ||
            feature.Contains("neon", StringComparison.Ordinal))
        {
            reason = "SIMD-specific codegen flags are intentionally filtered from parsing";
            return true;
        }

        reason = string.Empty;
        return false;
    }

    private static bool TryGetIgnoredDefineReason(string define, out string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(define);

        var separatorIndex = define.IndexOf('=');
        var name = separatorIndex >= 0 ? define[..separatorIndex] : define;
        if (IgnoredDefines.Contains(name))
        {
            reason = "architecture-specific defines are intentionally filtered from parsing";
            return true;
        }

        reason = string.Empty;
        return false;
    }

    private static string NormalizeLanguageStandard(string languageStandard)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(languageStandard);

        var normalized = languageStandard.Trim();
        if (normalized.StartsWith("c++", StringComparison.Ordinal))
        {
            normalized = $"gnu++{normalized[3..]}";
        }

        if (!normalized.StartsWith("gnu++", StringComparison.Ordinal))
        {
            return normalized;
        }

        var versionText = normalized["gnu++".Length..];
        return int.TryParse(versionText, out var version) && version < 20
            ? "gnu++20"
            : normalized;
    }
}
