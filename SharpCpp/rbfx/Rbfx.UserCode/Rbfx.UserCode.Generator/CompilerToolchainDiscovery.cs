using System.Text.Json;

namespace Rbfx.UserCode.Generator;

internal static class CompilerToolchainDiscovery
{
    public sealed record ToolchainInfo(string? TargetTriple, string? ClangResourceDirectory, IReadOnlyList<string> IncludeDirectories);

    public static ToolchainInfo Discover(string compilerPath)
    {
        if (string.IsNullOrWhiteSpace(compilerPath) || !File.Exists(compilerPath))
        {
            return new ToolchainInfo(null, null, Array.Empty<string>());
        }

        try
        {
            using var process = new System.Diagnostics.Process();
            process.StartInfo.FileName = compilerPath;
            process.StartInfo.ArgumentList.Add("-E");
            process.StartInfo.ArgumentList.Add("-x");
            process.StartInfo.ArgumentList.Add("c++");
            process.StartInfo.ArgumentList.Add("-");
            process.StartInfo.ArgumentList.Add("-v");
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.RedirectStandardInput = true;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.UseShellExecute = false;
            process.Start();
            process.StandardInput.Close();
            var stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();

            var includeDirectories = new List<string>();
            var capture = false;
            foreach (var rawLine in stderr.Split('\n'))
            {
                var line = rawLine.TrimEnd();
                if (line.Contains("#include <...> search starts here:", StringComparison.Ordinal))
                {
                    capture = true;
                    continue;
                }

                if (capture && line.Contains("End of search list.", StringComparison.Ordinal))
                {
                    capture = false;
                    continue;
                }

                if (capture)
                {
                    var candidate = line.Trim();
                    if (!string.IsNullOrWhiteSpace(candidate))
                    {
                        includeDirectories.Add(candidate);
                    }
                }
            }

            string? targetTriple = null;
            string? resourceDir = null;
            foreach (var rawLine in stderr.Split('\n'))
            {
                var line = rawLine.Trim();
                if (line.StartsWith("Target:", StringComparison.Ordinal))
                {
                    targetTriple = line["Target:".Length..].Trim();
                }
                else if (line.StartsWith("Resource dir:", StringComparison.Ordinal))
                {
                    resourceDir = line["Resource dir:".Length..].Trim();
                }
            }

            return new ToolchainInfo(targetTriple, resourceDir, includeDirectories.Distinct(StringComparer.Ordinal).ToArray());
        }
        catch
        {
            return new ToolchainInfo(null, null, Array.Empty<string>());
        }
    }
}
