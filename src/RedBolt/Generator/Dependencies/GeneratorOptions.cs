namespace RedBolt.Generator.Dependencies;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

internal sealed class GeneratorOptions
{
    public required string Surface { get; init; }
    public required string HeaderPath { get; init; }
    public required string HeaderIncludePath { get; init; }
    public required string OutputDirectory { get; init; }
    public required string FileStem { get; init; }
    public required string MetadataPath { get; init; }
    public required string LogPath { get; init; }
    public required string SharpCppRoot { get; init; }
    public required string RedBoltRoot { get; init; }
    public required string NativeLibraryName { get; init; }
    public required string CompilerPath { get; init; }
    public string? CompilerTarget { get; init; }
    public string? CompilerSysroot { get; init; }
    public required IReadOnlyList<string> IncludeDirectories { get; init; }
    public required IReadOnlyList<string> Defines { get; init; }
    public required IReadOnlyList<string> OwnedHeaders { get; init; }

    public static GeneratorOptions Parse(string[] args)
    {
        return new GeneratorOptions
        {
            Surface = RequireOption(args, "--surface"),
            HeaderPath = Path.GetFullPath(RequireOption(args, "--header")),
            HeaderIncludePath = RequireOption(args, "--include-name"),
            OutputDirectory = Path.GetFullPath(RequireOption(args, "--output-dir")),
            FileStem = RequireOption(args, "--file-stem"),
            MetadataPath = Path.GetFullPath(RequireOption(args, "--metadata")),
            LogPath = Path.GetFullPath(RequireOption(args, "--log")),
            SharpCppRoot = Path.GetFullPath(RequireOption(args, "--sharpcpp-root")),
            RedBoltRoot = Path.GetFullPath(RequireOption(args, "--redbolt-root")),
            NativeLibraryName = RequireOption(args, "--native-library-name"),
            CompilerPath = Path.GetFullPath(RequireOption(args, "--compiler")),
            CompilerTarget = GetOption(args, "--compiler-target"),
            CompilerSysroot = GetOptionalPath(args, "--compiler-sysroot"),
            IncludeDirectories = GetRepeatedOption(args, "--include").Select(Path.GetFullPath).ToArray(),
            Defines = GetRepeatedOption(args, "--define")
                .Where(static define => !string.IsNullOrWhiteSpace(define))
                .ToArray(),
            OwnedHeaders = GetRepeatedOption(args, "--owned-header").Select(Path.GetFullPath).ToArray()
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

    private static string? GetOptionalPath(string[] args, string name)
    {
        var value = GetOption(args, name);
        return string.IsNullOrWhiteSpace(value) ? null : Path.GetFullPath(value);
    }
}
