using SharpCpp.Model.Ast;

namespace SharpCpp.Pipeline;

public static class NormalizeManagedNamespacesPass
{
    public static void Apply(BindingCompilation compilation)
    {
        ArgumentNullException.ThrowIfNull(compilation);

        var namespaceMappings = compilation.NamespaceMappings;

        foreach (var record in compilation.Records)
        {
            if (record.GetMetadata<bool>(MetadataKeys.SuppressEmission))
            {
                continue;
            }

            record.ManagedNamespace = ResolveManagedNamespace(record.CppQualifiedName, compilation.ManagedNamespace, namespaceMappings, record.NativeName);
        }

        foreach (var @enum in compilation.Enums)
        {
            if (@enum.GetMetadata<bool>(MetadataKeys.SuppressEmission))
            {
                continue;
            }

            @enum.ManagedNamespace = ResolveManagedNamespace(@enum.CppQualifiedName, compilation.ManagedNamespace, namespaceMappings, @enum.NativeName);
        }

        foreach (var alias in compilation.TypeAliases)
        {
            alias.ManagedNamespace = ResolveManagedNamespace(alias.CppQualifiedName, compilation.ManagedNamespace, namespaceMappings, alias.NativeName);
        }
    }

    private static string ResolveManagedNamespace(
        string cppQualifiedName,
        string defaultNamespace,
        IReadOnlyDictionary<string, string> namespaceMappings,
        string nativeName)
    {
        var strippedName = StripTemplateArguments(cppQualifiedName);
        var lastSeparator = strippedName.LastIndexOf("::", StringComparison.Ordinal);
        if (lastSeparator < 0)
        {
            return defaultNamespace;
        }

        var cppNamespace = strippedName[..lastSeparator];

        if (namespaceMappings.TryGetValue(cppNamespace, out var mappedNamespace))
        {
            return mappedNamespace;
        }

        var segments = cppNamespace.Split("::");
        for (var index = 0; index < segments.Length; index++)
        {
            segments[index] = ToPascalCase(segments[index]);
        }

        return string.Join(".", segments);
    }

    private static string StripTemplateArguments(string name)
    {
        var genericStart = name.IndexOf('<');
        return genericStart >= 0 ? name[..genericStart] : name;
    }

    private static string ToPascalCase(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return name;
        }

        return char.ToUpperInvariant(name[0]) + name[1..];
    }
}
