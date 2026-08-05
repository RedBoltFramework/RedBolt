namespace RedBolt.Generator.Dependencies;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using SharpCpp.Model.Ast;
using SharpCpp.Rendering;

internal sealed class TinyGltfGenerator(string nativeLibraryName)
    : BindingTargetGenerator(
        "tinygltf",
        "RedBolt.Bindings.TinyGltf",
        "RedBolt.Bindings.TinyGltf",
        "TinyGltfNativeBindings",
        nativeLibraryName,
        "redbolt_tinygltf",
        "-xc++",
        "-std=c++20",
        true)
{
    protected override bool RetainStdlibWrapper(RecordBinding record) =>
        record.ManagedNamespace == "Std" && record.ManagedName is "Map" or "String" or "Vector";

    protected override GeneratedBindingSet TransformBindings(GeneratedBindingSet bindings) =>
        new(bindings.Files.Select(static file => file.RelativePath.EndsWith(".g.cs", StringComparison.Ordinal)
            ? file with { Content = RemoveConcreteGenericOverloads(file.Content) }
            : file).ToArray());

    private static string RemoveConcreteGenericOverloads(string content)
    {
        var genericMethods = Regex.Matches(content,
                @"(?m)^\s*public\s+[^\r\n]+?\s+(?<name>[A-Za-z_]\w*)<[^>]+>\((?<parameters>[^)]*)\)")
            .Select(static match => (match.Groups["name"].Value, match.Groups["parameters"].Value))
            .Distinct()
            .ToArray();
        foreach (var (name, parameters) in genericMethods)
        {
            // The generic dispatcher covers each emitted specialization. Strip
            // return-type-only convenience overloads, which C# cannot represent.
            var pattern = new Regex($@"(?m)^[ \t]*public[^\r\n]*\s+{Regex.Escape(name)}\({Regex.Escape(parameters)}\)[ \t]*\r?\n[ \t]*\{{");
            for (var match = pattern.Match(content); match.Success; match = pattern.Match(content))
            {
                var end = FindMethodEnd(content, content.IndexOf('{', match.Index + match.Length - 1));
                content = content.Remove(match.Index, end - match.Index + 1);
            }
        }
        return content;
    }

    private static int FindMethodEnd(string content, int openBrace)
    {
        var depth = 0;
        for (var index = openBrace; index < content.Length; ++index)
        {
            if (content[index] == '{')
                ++depth;
            else if (content[index] == '}' && --depth == 0)
                return index;
        }
        throw new InvalidOperationException("Generated C# method has unbalanced braces.");
    }

    protected override IReadOnlyDictionary<string, string> NamespaceMappings { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["tinygltf"] = "RedBolt.Bindings.TinyGltf"
        };

}
