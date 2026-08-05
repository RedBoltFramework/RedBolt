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

    protected override void AfterPruneCompilation(BindingCompilation compilation)
    {
        var vector = compilation.Records.Single(record => record.CppQualifiedName == "std::vector");
        foreach (var field in compilation.Descendants<FieldBinding>())
            field.Type = NormalizeMapElement(field.Type);

        for (var index = 0; index < vector.SupportedGenericInstantiations.Count; ++index)
        {
            var instantiation = vector.SupportedGenericInstantiations[index];
            vector.SupportedGenericInstantiations[index] = new(
                instantiation.TypeArguments.Select(NormalizeMapElement));
        }
    }

    protected override GeneratedBindingSet TransformBindings(GeneratedBindingSet bindings)
    {
        var unsupportedSymbols = bindings.Files
            .Where(static file => file.RelativePath.EndsWith(".g.cpp", StringComparison.Ordinal))
            .SelectMany(static file => FindUnsupportedNativeSymbols(file.Content))
            .ToHashSet(StringComparer.Ordinal);
        return new(bindings.Files.Select(file => file.RelativePath.EndsWith(".g.cs", StringComparison.Ordinal)
            ? file with { Content = RemoveUnsupportedManagedBindings(RemoveConcreteGenericOverloads(file.Content), unsupportedSymbols) }
            : file.RelativePath.EndsWith(".g.cpp", StringComparison.Ordinal)
                ? file with { Content = RemoveUnsupportedNativeBindings(file.Content, unsupportedSymbols) }
                : file).ToArray());
    }

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

    private static IEnumerable<string> FindUnsupportedNativeSymbols(string content)
    {
        var symbols = new List<string>();
        foreach (Match match in Regex.Matches(content, @"SHARPCPP_EXPORT_NAMED\((?<symbol>[^)]+)\)"))
        {
            var bodyStart = content.IndexOf('{', match.Index);
            var bodyEnd = FindMethodEnd(content, bodyStart);
            var function = content[match.Index..(bodyEnd + 1)];
            var genericGetStart = function.IndexOf("&tinygltf::Value::Get<", StringComparison.Ordinal);
            var hasWrapperGenericArgument = genericGetStart >= 0 && function[(genericGetStart + "&tinygltf::Value::Get<".Length)..FindMatchingAngleBracket(function, genericGetStart + "&tinygltf::Value::Get<".Length - 1)].Contains('*', StringComparison.Ordinal);
            if (hasWrapperGenericArgument ||
                function.Contains("MarshallerInvoker::construct<tinygltf::Value,", StringComparison.Ordinal) && function.Contains("&&", StringComparison.Ordinal))
            {
                symbols.Add(match.Groups["symbol"].Value);
            }
        }
        return symbols;
    }

    private static string RemoveUnsupportedNativeBindings(string content, IReadOnlySet<string> unsupportedSymbols)
    {
        var ranges = Regex.Matches(content, @"SHARPCPP_EXPORT_NAMED\((?<symbol>[^)]+)\)")
            .Cast<Match>()
            .Where(match => unsupportedSymbols.Contains(match.Groups["symbol"].Value))
            .Select(match =>
            {
                var start = content.LastIndexOf("#if defined(__EMSCRIPTEN__)", match.Index, StringComparison.Ordinal);
                var end = FindMethodEnd(content, content.IndexOf('{', match.Index)) + 1;
                return (start, end);
            })
            .OrderByDescending(static range => range.start);
        foreach (var (start, end) in ranges)
            content = content.Remove(start, end - start);
        return content;
    }

    private static string RemoveUnsupportedManagedBindings(string content, IReadOnlySet<string> unsupportedSymbols)
    {
        var pInvokeNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var symbol in unsupportedSymbols)
        {
            var entryPoint = content.IndexOf($"EntryPoint = \"{symbol}\"", StringComparison.Ordinal);
            if (entryPoint < 0)
                continue;
            var declarationEnd = content.IndexOf(';', entryPoint);
            var declaration = content[entryPoint..(declarationEnd + 1)];
            var pInvokeName = Regex.Match(declaration, @"internal\s+static\s+partial\s+.+\s+(?<name>\w+)\(");
            if (pInvokeName.Success)
                pInvokeNames.Add(pInvokeName.Groups["name"].Value);
        }

        foreach (var pInvokeName in pInvokeNames)
        {
            for (var helperStart = content.IndexOf($"private static nint __CreateHandle_{pInvokeName}", StringComparison.Ordinal);
                 helperStart >= 0;
                 helperStart = content.IndexOf($"private static nint __CreateHandle_{pInvokeName}", StringComparison.Ordinal))
            {
                var constructorStart = content.LastIndexOf("public ", helperStart, StringComparison.Ordinal);
                var helperEnd = FindMethodEnd(content, content.IndexOf('{', helperStart)) + 1;
                content = content.Remove(constructorStart, helperEnd - constructorStart);
            }
            for (var use = content.IndexOf($"__PInvoke.{pInvokeName}(", StringComparison.Ordinal); use >= 0;
                 use = content.IndexOf($"__PInvoke.{pInvokeName}(", StringComparison.Ordinal))
            {
                var dispatchStart = content.LastIndexOf("if (typeof(T)", use, StringComparison.Ordinal);
                if (dispatchStart < 0)
                    break;
                content = content.Remove(dispatchStart, FindMethodEnd(content, content.IndexOf('{', dispatchStart)) - dispatchStart + 1);
            }
        }

        const string methodPattern = @"(?m)^[ \t]*public\s+[^\r\n(]+\([^\r\n]*\)\r?\n[ \t]*\{";
        for (var method = Regex.Match(content, methodPattern); method.Success; )
        {
            var end = FindMethodEnd(content, content.IndexOf('{', method.Index + method.Length - 1));
            var body = content[method.Index..(end + 1)];
            if (pInvokeNames.Any(name => body.Contains($"__PInvoke.{name}(", StringComparison.Ordinal)))
            {
                content = content.Remove(method.Index, end - method.Index + 1);
                method = Regex.Match(content, methodPattern);
            }
            else
                method = method.NextMatch();
        }

        foreach (var symbol in unsupportedSymbols)
        {
            var entryPoint = content.IndexOf($"EntryPoint = \"{symbol}\"", StringComparison.Ordinal);
            if (entryPoint < 0)
                continue;
            var start = content.LastIndexOf('[', entryPoint);
            var end = content.IndexOf(';', entryPoint) + 1;
            content = content.Remove(start, end - start);
        }
        return content;
    }

    private static int FindMatchingAngleBracket(string content, int openBracket)
    {
        var depth = 0;
        for (var index = openBracket; index < content.Length; ++index)
        {
            if (content[index] == '<')
                ++depth;
            else if (content[index] == '>' && --depth == 0)
                return index;
        }
        throw new InvalidOperationException("Generated C++ template specialization has unbalanced brackets.");
    }

    private static BindingType NormalizeMapElement(BindingType type)
    {
        var arguments = type.GenericArguments.Select(NormalizeMapElement).ToArray();
        if (type.RecordId == "record:std::map" && type.CppTypeName.EndsWith('*'))
            type = type.WithCppTypeName(type.CppTypeName[..^1].TrimEnd());
        return type.WithGenericArguments(arguments);
    }

    protected override IReadOnlyDictionary<string, string> NamespaceMappings { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["tinygltf"] = "RedBolt.Bindings.TinyGltf"
        };

}
