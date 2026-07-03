using System.Diagnostics;
using System.Reflection;

namespace RedBolt.Dependencies.Generator;

internal static class CompilerToolchainDiscovery
{
    private const string ClangResourceDirectoryOverrideVariable = "REDBOLT_CLANG_RESOURCE_DIRECTORY";

    public static CompilerToolchainInfo Discover(string compilerPath)
    {
        if (string.IsNullOrWhiteSpace(compilerPath))
            return new CompilerToolchainInfo(Array.Empty<string>(), null, null, null, null);

        var toolchainClangCompilerPath = ResolveToolchainCompiler(compilerPath);
        var targetTriple = DiscoverTargetTriple(compilerPath);
        var parserClangMajorVersion = DiscoverParserClangMajorVersion();
        var includeDirectories = DiscoverVerboseSearchPaths(compilerPath, targetTriple);
        var clangResourceDirectory = DiscoverClangResourceDirectoryOverride();
        var usesResourceDirectoryOverride = clangResourceDirectory is not null;

        if (clangResourceDirectory is null && LooksLikeClangCompiler(toolchainClangCompilerPath))
            clangResourceDirectory = DiscoverClangResourceDirectory(toolchainClangCompilerPath);

        string? ignoredClangResourceDirectory = null;
        if (!usesResourceDirectoryOverride &&
            !string.IsNullOrWhiteSpace(clangResourceDirectory) &&
            parserClangMajorVersion is int parserClangMajor &&
            TryGetClangMajorVersionFromResourceDirectory(clangResourceDirectory, out var resourceClangMajor) &&
            resourceClangMajor != parserClangMajor)
        {
            ignoredClangResourceDirectory = clangResourceDirectory;
            clangResourceDirectory = null;
        }

        includeDirectories = FilterClangBuiltinIncludeDirectories(
            includeDirectories,
            clangResourceDirectory,
            ignoredClangResourceDirectory);

        return new CompilerToolchainInfo(
            includeDirectories.Where(Directory.Exists).Distinct(StringComparer.Ordinal).ToArray(),
            targetTriple,
            clangResourceDirectory,
            parserClangMajorVersion,
            ignoredClangResourceDirectory);
    }

    private static string ResolveToolchainCompiler(string compilerPath)
    {
        if (LooksLikeClangCompiler(compilerPath))
            return compilerPath;

        foreach (var candidate in new[] { "clang++", "clang" })
        {
            if (CommandExists(candidate))
                return candidate;
        }

        return compilerPath;
    }

    private static IReadOnlyList<string> DiscoverVerboseSearchPaths(string compilerPath, string? targetTriple)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = compilerPath,
                RedirectStandardInput = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false
            };

            if (!string.IsNullOrWhiteSpace(targetTriple) && LooksLikeClangCompiler(compilerPath))
                startInfo.ArgumentList.Add($"--target={targetTriple}");

            startInfo.ArgumentList.Add("-E");
            startInfo.ArgumentList.Add("-x");
            startInfo.ArgumentList.Add("c++");
            startInfo.ArgumentList.Add("-");
            startInfo.ArgumentList.Add("-v");

            using var process = Process.Start(startInfo);
            if (process is null)
                return Array.Empty<string>();

            process.StandardInput.Close();
            _ = process.StandardOutput.ReadToEnd();
            var errorOutput = process.StandardError.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode != 0)
                return Array.Empty<string>();

            var includeDirectories = new List<string>();
            var capture = false;
            foreach (var rawLine in errorOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var line = rawLine.Trim();
                if (line.Contains("#include <...> search starts here:", StringComparison.Ordinal) ||
                    line.Contains("#include \"...\" search starts here:", StringComparison.Ordinal))
                {
                    capture = true;
                    continue;
                }

                if (!capture)
                    continue;

                if (line.StartsWith("End of search list.", StringComparison.Ordinal))
                    break;

                if (line.StartsWith("(framework directory)", StringComparison.Ordinal))
                    continue;

                var normalizedPath = NormalizeSearchPath(line);
                if (!string.IsNullOrWhiteSpace(normalizedPath))
                    includeDirectories.Add(normalizedPath);
            }

            return includeDirectories;
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private static string? DiscoverClangResourceDirectoryOverride()
    {
        var overrideValue = Environment.GetEnvironmentVariable(ClangResourceDirectoryOverrideVariable);
        if (string.IsNullOrWhiteSpace(overrideValue))
            return null;

        var normalizedPath = NormalizeResourceDirectory(overrideValue);
        return Directory.Exists(normalizedPath) ? normalizedPath : null;
    }

    private static string? DiscoverClangResourceDirectory(string compilerPath)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = compilerPath,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false
            };
            startInfo.ArgumentList.Add("-print-resource-dir");

            using var process = Process.Start(startInfo);
            if (process is null)
                return null;

            var standardOutput = process.StandardOutput.ReadToEnd().Trim();
            _ = process.StandardError.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(standardOutput))
                return null;

            var resourceDirectory = NormalizeResourceDirectory(standardOutput);
            return Directory.Exists(Path.Combine(resourceDirectory, "include")) ? resourceDirectory : null;
        }
        catch
        {
            return null;
        }
    }

    private static string? DiscoverTargetTriple(string compilerPath)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = compilerPath,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false
            };
            startInfo.ArgumentList.Add("-dumpmachine");

            using var process = Process.Start(startInfo);
            if (process is null)
                return null;

            var standardOutput = process.StandardOutput.ReadToEnd().Trim();
            _ = process.StandardError.ReadToEnd();
            process.WaitForExit();
            return process.ExitCode == 0 && !string.IsNullOrWhiteSpace(standardOutput) ? standardOutput : null;
        }
        catch
        {
            return null;
        }
    }

    private static int? DiscoverParserClangMajorVersion()
    {
        try
        {
            var clangSharpAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(static assembly => string.Equals(assembly.GetName().Name, "ClangSharp", StringComparison.Ordinal))
                ?? Assembly.Load("ClangSharp");
            return clangSharpAssembly.GetName().Version?.Major;
        }
        catch
        {
            return null;
        }
    }

    private static IReadOnlyList<string> FilterClangBuiltinIncludeDirectories(
        IReadOnlyList<string> includeDirectories,
        string? clangResourceDirectory,
        string? ignoredClangResourceDirectory)
    {
        if (includeDirectories.Count == 0)
            return includeDirectories;

        var keptIncludeDirectories = new List<string>(includeDirectories.Count);
        foreach (var includeDirectory in includeDirectories)
        {
            if (string.IsNullOrWhiteSpace(includeDirectory))
                continue;

            if (string.IsNullOrWhiteSpace(clangResourceDirectory) && string.IsNullOrWhiteSpace(ignoredClangResourceDirectory))
            {
                keptIncludeDirectories.Add(includeDirectory);
                continue;
            }

            if (!LooksLikeClangBuiltinIncludeDirectory(includeDirectory))
            {
                keptIncludeDirectories.Add(includeDirectory);
                continue;
            }

            if (!string.IsNullOrWhiteSpace(clangResourceDirectory) &&
                IsPathUnderDirectory(includeDirectory, Path.Combine(clangResourceDirectory, "include")))
            {
                keptIncludeDirectories.Add(includeDirectory);
            }
        }

        return keptIncludeDirectories;
    }

    private static bool LooksLikeClangBuiltinIncludeDirectory(string path)
    {
        var normalizedPath = NormalizePath(path).Replace('\\', '/');
        return normalizedPath.Contains("/lib/clang/", StringComparison.Ordinal) ||
               normalizedPath.Contains("/lib64/clang/", StringComparison.Ordinal);
    }

    private static bool IsPathUnderDirectory(string path, string candidateRoot)
    {
        var normalizedPath = NormalizePath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var normalizedRoot = NormalizePath(candidateRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return normalizedPath.StartsWith(normalizedRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal) ||
               string.Equals(normalizedPath, normalizedRoot, StringComparison.Ordinal);
    }

    private static bool TryGetClangMajorVersionFromResourceDirectory(string resourceDirectory, out int majorVersion)
    {
        var normalizedResourceDirectory = NormalizeResourceDirectory(resourceDirectory).Replace('\\', '/');
        var marker = "/lib/clang/";
        var markerIndex = normalizedResourceDirectory.LastIndexOf(marker, StringComparison.Ordinal);
        if (markerIndex < 0)
        {
            majorVersion = 0;
            return false;
        }

        var versionStart = markerIndex + marker.Length;
        var versionEnd = normalizedResourceDirectory.IndexOf('/', versionStart);
        var versionText = versionEnd >= 0
            ? normalizedResourceDirectory[versionStart..versionEnd]
            : normalizedResourceDirectory[versionStart..];

        var majorText = versionText.Split('.', 2)[0];
        return int.TryParse(majorText, out majorVersion);
    }

    private static bool CommandExists(string command)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = command,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false
            };
            startInfo.ArgumentList.Add("--version");

            using var process = Process.Start(startInfo);
            if (process is null)
                return false;

            _ = process.StandardOutput.ReadToEnd();
            _ = process.StandardError.ReadToEnd();
            process.WaitForExit();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool LooksLikeClangCompiler(string compilerPath)
    {
        var fileName = Path.GetFileNameWithoutExtension(compilerPath);
        return fileName.Contains("clang", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeSearchPath(string path)
    {
        const string frameworkSuffix = " (framework directory)";
        var normalizedPath = path.EndsWith(frameworkSuffix, StringComparison.Ordinal)
            ? path[..^frameworkSuffix.Length]
            : path;
        return NormalizePath(normalizedPath);
    }

    private static string NormalizeResourceDirectory(string resourceDirectory)
    {
        return NormalizePath(resourceDirectory);
    }

    private static string NormalizePath(string path)
    {
        var trimmedPath = path.Trim();
        return string.IsNullOrWhiteSpace(trimmedPath) ? trimmedPath : Path.GetFullPath(trimmedPath);
    }
}

internal sealed record CompilerToolchainInfo(
    IReadOnlyList<string> IncludeDirectories,
    string? TargetTriple,
    string? ClangResourceDirectory,
    int? ParserClangMajorVersion,
    string? IgnoredClangResourceDirectory);
