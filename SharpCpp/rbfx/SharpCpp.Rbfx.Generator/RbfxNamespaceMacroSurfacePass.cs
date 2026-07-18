using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using SharpCpp.Model.Ast;

namespace SharpCpp.Rbfx.Generator;

internal static class RbfxNamespaceMacroSurfacePass
{
    private const string EventBaseTypeName = "global::Urho3D.Detail.EventDefinition";
    private const string EventDetailClassName = "Detail";
    private const string StringHashTypeName = "global::Urho3D.Math.StringHash";
    private static readonly ILogger Logger = SharpCpp.Model.Logging.SharpCppLog.For(nameof(RbfxNamespaceMacroSurfacePass));

    private static readonly Regex EventRegex = new(
        @"^[ \t]*URHO3D_EVENT\(\s*(?<eventId>[A-Za-z_][A-Za-z0-9_]*)\s*,\s*(?<eventName>[A-Za-z_][A-Za-z0-9_]*)\s*\)\s*\{(?<body>.*?)^[ \t]*\}",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Multiline | RegexOptions.Singleline);

    private static readonly Regex EventParamRegex = new(
        @"^[ \t]*URHO3D_PARAM\(\s*(?<paramId>[A-Za-z_][A-Za-z0-9_]*)\s*,\s*(?<paramName>[A-Za-z_][A-Za-z0-9_]*)\s*\)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Multiline);

    private static readonly Regex GlobalConstantRegex = new(
        @"^[ \t]*URHO3D_GLOBAL_CONSTANT\(\s*ConstString\s+(?<symbolName>[A-Za-z_][A-Za-z0-9_]*)\s*\{\s*""(?<value>[^""\\]*(?:\\.[^""\\]*)*)""\s*\}\s*\)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Multiline);

    private static readonly HashSet<string> EventReservedMemberNames = new(StringComparer.Ordinal)
    {
        "Equals",
        "EventType",
        "GetHashCode",
        "GetType",
        "ToString"
    };

    public static int Apply(BindingCompilation compilation, string includeRoot)
    {
        ArgumentNullException.ThrowIfNull(compilation);
        ArgumentException.ThrowIfNullOrWhiteSpace(includeRoot);

        var urhoIncludeRoot = Path.Combine(includeRoot, "Urho3D");
        if (!Directory.Exists(urhoIncludeRoot))
        {
            return 0;
        }

        var synthesizedCount = 0;
        var eventCount = 0;
        var eventParameterCount = 0;
        var constantCount = 0;

        foreach (var headerPath in Directory.EnumerateFiles(urhoIncludeRoot, "*.h", SearchOption.AllDirectories)
                     .OrderBy(static path => path, StringComparer.Ordinal))
        {
            var headerText = File.ReadAllText(headerPath);
            if (!headerText.Contains("URHO3D_EVENT(", StringComparison.Ordinal) &&
                !headerText.Contains("URHO3D_GLOBAL_CONSTANT(", StringComparison.Ordinal))
            {
                continue;
            }

            var events = ParseEvents(headerText);
            var constants = ParseGlobalConstants(headerText);
            if (events.Count == 0 && constants.Count == 0)
            {
                continue;
            }

            var relativeHeaderPath = Path.GetRelativePath(includeRoot, headerPath).Replace('\\', '/');
            var managedClass = GetOrCreateManagedClass(compilation, relativeHeaderPath);

            foreach (var descriptor in constants)
            {
                AddManagedFieldIfMissing(
                    managedClass,
                    MakeManagedConstantName(descriptor.Value, descriptor.SymbolName),
                    "string",
                    QuoteStringLiteral(descriptor.Value),
                    isConst: true);
                constantCount++;
            }

            foreach (var descriptor in events)
            {
                var detailClass = GetOrCreateManagedClass(
                    managedClass,
                    $"{managedClass.SymbolId}::Detail",
                    EventDetailClassName,
                    nativeName: EventDetailClassName);
                detailClass.IsStatic = true;
                detailClass.IsPartial = true;

                var eventClassName = $"{descriptor.EventName}Event";
                var eventClass = GetOrCreateManagedClass(
                    detailClass,
                    $"{managedClass.SymbolId}::Detail::{eventClassName}",
                    eventClassName,
                    nativeName: eventClassName);
                eventClass.IsStatic = false;
                eventClass.IsPartial = false;
                eventClass.IsSealed = true;
                eventClass.BaseTypeName = EventBaseTypeName;

                var constructorCode = $$"""
                    public {{eventClass.ManagedName}}()
                        : base({{QuoteStringLiteral(descriptor.EventName)}})
                    {
                    }
                    """;
                if (!eventClass.BodyCodeBlocks.Contains(constructorCode, StringComparer.Ordinal))
                {
                    eventClass.BodyCodeBlocks.Add(constructorCode);
                }

                AddManagedFieldIfMissing(
                    managedClass,
                    descriptor.EventName,
                    $"{EventDetailClassName}.{eventClassName}",
                    $"new {EventDetailClassName}.{eventClassName}()");

                foreach (var parameterName in descriptor.ParameterNames)
                {
                    AddManagedFieldIfMissing(
                        eventClass,
                        GetEventParameterMemberName(parameterName),
                        StringHashTypeName,
                        QuoteStringLiteral(parameterName),
                        isStatic: false);
                }

                eventCount++;
                eventParameterCount += descriptor.ParameterNames.Count;
            }

            synthesizedCount++;
            Logger.LogInformation(
                "Synthesized macro surface '{Namespace}.{Class}' from '{Header}' with {EventCount} event helper(s) and {ConstantCount} constant(s).",
                managedClass.ManagedNamespace,
                managedClass.ManagedName,
                relativeHeaderPath,
                events.Count,
                constants.Count);
        }

        if (synthesizedCount > 0)
        {
            Logger.LogInformation(
                "Synthesized {ClassCount} macro surface class(es), {EventCount} event helper(s), {EventParameterCount} event parameter hash(es), and {ConstantCount} constant(s).",
                synthesizedCount,
                eventCount,
                eventParameterCount,
                constantCount);
        }

        return synthesizedCount;
    }

    private static IReadOnlyList<EventDescriptor> ParseEvents(string headerText)
    {
        var events = new List<EventDescriptor>();
        foreach (Match eventMatch in EventRegex.Matches(headerText))
        {
            var eventName = eventMatch.Groups["eventName"].Value;
            if (string.IsNullOrWhiteSpace(eventName))
            {
                continue;
            }

            var parameterNames = EventParamRegex.Matches(eventMatch.Groups["body"].Value)
                .Select(static match => match.Groups["paramName"].Value)
                .Where(static paramName => !string.IsNullOrWhiteSpace(paramName))
                .ToArray();

            events.Add(new EventDescriptor(eventName, parameterNames));
        }

        return events;
    }

    private static IReadOnlyList<GlobalConstantDescriptor> ParseGlobalConstants(string headerText)
    {
        return GlobalConstantRegex.Matches(headerText)
            .Select(static match => new GlobalConstantDescriptor(
                match.Groups["symbolName"].Value,
                Regex.Unescape(match.Groups["value"].Value)))
            .Where(static descriptor => !string.IsNullOrWhiteSpace(descriptor.SymbolName) && !string.IsNullOrWhiteSpace(descriptor.Value))
            .ToArray();
    }

    private static ManagedClassBinding GetOrCreateManagedClass(BindingCompilation compilation, string relativeHeaderPath)
    {
        var normalizedHeaderPath = relativeHeaderPath.Replace('\\', '/');
        var pathWithoutExtension = Path.ChangeExtension(normalizedHeaderPath, null)!.Replace('\\', '/');
        var parts = pathWithoutExtension.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            throw new InvalidOperationException($"Cannot derive managed macro surface name from '{relativeHeaderPath}'.");
        }

        var namespaceParts = parts[0] == "Urho3D"
            ? parts.Skip(1).SkipLast(1).ToArray()
            : parts.SkipLast(1).ToArray();
        var managedNamespace = namespaceParts.Length == 0
            ? "Urho3D"
            : $"Urho3D.{string.Join('.', namespaceParts)}";
        var baseManagedName = parts[^1];
        var managedName = ResolveMacroSurfaceClassName(compilation, managedNamespace, baseManagedName);
        var symbolId = $"managed-macro:{normalizedHeaderPath}";

        var existing = compilation.Members
            .OfType<ManagedClassBinding>()
            .FirstOrDefault(candidate => string.Equals(candidate.SymbolId, symbolId, StringComparison.Ordinal));
        if (existing is not null)
        {
            return existing;
        }

        var created = new ManagedClassBinding(symbolId, managedName, managedName)
        {
            ManagedNamespace = managedNamespace,
            HeaderIncludePath = normalizedHeaderPath,
            IsStatic = true,
            IsPartial = true
        };
        compilation.Members.Add(created);
        return created;
    }

    private static string ResolveMacroSurfaceClassName(BindingCompilation compilation, string managedNamespace, string managedName)
    {
        if (!HasNonManagedTypeNameCollision(compilation, managedNamespace, managedName))
        {
            return managedName;
        }

        var candidate = $"{managedName}Macros";
        var index = 2;
        while (HasNonManagedTypeNameCollision(compilation, managedNamespace, candidate))
        {
            candidate = $"{managedName}Macros{index}";
            index++;
        }

        return candidate;
    }

    private static bool HasNonManagedTypeNameCollision(BindingCompilation compilation, string managedNamespace, string managedName)
    {
        _ = managedNamespace;
        return compilation.Records.Any(record => string.Equals(record.ManagedName, managedName, StringComparison.Ordinal)) ||
            compilation.Enums.Any(@enum => string.Equals(@enum.ManagedName, managedName, StringComparison.Ordinal));
    }

    private static ManagedClassBinding GetOrCreateManagedClass(
        BindingContainerNode parent,
        string symbolId,
        string managedName,
        string nativeName)
    {
        var existing = parent.Members
            .OfType<ManagedClassBinding>()
            .FirstOrDefault(candidate => string.Equals(candidate.SymbolId, symbolId, StringComparison.Ordinal));
        if (existing is not null)
        {
            return existing;
        }

        var created = new ManagedClassBinding(symbolId, nativeName, managedName)
        {
            HeaderIncludePath = parent.HeaderIncludePath
        };
        parent.Members.Add(created);
        return created;
    }

    private static ManagedFieldBinding AddManagedFieldIfMissing(
        ManagedClassBinding parent,
        string managedName,
        string managedTypeName,
        string initializer,
        bool isStatic = true,
        bool isReadOnly = true,
        bool isConst = false)
    {
        var existing = parent.Members
            .OfType<ManagedFieldBinding>()
            .FirstOrDefault(candidate => string.Equals(candidate.ManagedName, managedName, StringComparison.Ordinal));
        if (existing is not null)
        {
            return existing;
        }

        var field = new ManagedFieldBinding(
            $"{parent.SymbolId}::{managedName}",
            managedName,
            managedName,
            managedTypeName,
            initializer)
        {
            HeaderIncludePath = parent.HeaderIncludePath,
            IsStatic = isStatic,
            IsReadOnly = isReadOnly,
            IsConst = isConst
        };

        parent.Members.Add(field);
        return field;
    }

    private static string GetEventParameterMemberName(string parameterName)
    {
        return EventReservedMemberNames.Contains(parameterName)
            ? $"{parameterName}Key"
            : parameterName;
    }

    private static string MakeManagedConstantName(string value, string fallbackName)
    {
        var source = string.IsNullOrWhiteSpace(value) ? fallbackName : value;
        var builder = new StringBuilder(source.Length);

        foreach (var character in source)
        {
            if (char.IsLetterOrDigit(character) || character == '_')
            {
                builder.Append(character);
            }
        }

        if (builder.Length == 0)
        {
            return fallbackName;
        }

        if (char.IsDigit(builder[0]))
        {
            builder.Insert(0, '_');
        }

        return builder.ToString();
    }

    private static string QuoteStringLiteral(string value)
    {
        var builder = new StringBuilder(value.Length + 2);
        builder.Append('"');

        foreach (var character in value)
        {
            builder.Append(character switch
            {
                '\\' => "\\\\",
                '"' => "\\\"",
                '\n' => "\\n",
                '\r' => "\\r",
                '\t' => "\\t",
                _ => character.ToString()
            });
        }

        builder.Append('"');
        return builder.ToString();
    }

    private sealed record EventDescriptor(string EventName, IReadOnlyList<string> ParameterNames);

    private sealed record GlobalConstantDescriptor(string SymbolName, string Value);
}
