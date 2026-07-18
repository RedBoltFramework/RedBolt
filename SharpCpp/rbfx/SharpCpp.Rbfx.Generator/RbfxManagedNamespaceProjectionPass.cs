using SharpCpp.Model.Ast;

namespace SharpCpp.Rbfx.Generator;

public static class RbfxManagedNamespaceProjectionPass
{
    public static ManagedBindingProjection Apply(BindingCompilation compilation)
    {
        ArgumentNullException.ThrowIfNull(compilation);

        var projection = new ManagedBindingProjection(compilation.ManagedNamespace);

        foreach (var record in compilation.Records)
        {
            projection.AddDeclaration(ResolveManagedNamespace(record.CppQualifiedName, record.HeaderIncludePath, record.ManagedNamespace ?? compilation.ManagedNamespace), record);
        }

        foreach (var enumBinding in compilation.Enums)
        {
            projection.AddDeclaration(ResolveManagedNamespace(enumBinding.CppQualifiedName, enumBinding.HeaderIncludePath, enumBinding.ManagedNamespace ?? compilation.ManagedNamespace), enumBinding);
        }

        compilation.SetMetadata(MetadataKeys.ManagedBindingProjection, projection);
        return projection;
    }

    private static string ResolveManagedNamespace(string cppQualifiedName, string? headerIncludePath, string defaultNamespace)
    {
        if (!cppQualifiedName.StartsWith("Urho3D::", StringComparison.Ordinal))
        {
            return defaultNamespace;
        }

        if (string.IsNullOrWhiteSpace(headerIncludePath))
        {
            return defaultNamespace;
        }

        var normalizedHeaderPath = headerIncludePath.Replace('\\', '/');
        const string urhoPrefix = "Urho3D/";
        if (normalizedHeaderPath.StartsWith(urhoPrefix, StringComparison.Ordinal))
        {
            normalizedHeaderPath = normalizedHeaderPath[urhoPrefix.Length..];
        }
        else
        {
            const string urhoMarker = "/Urho3D/";
            var markerIndex = normalizedHeaderPath.IndexOf(urhoMarker, StringComparison.Ordinal);
            if (markerIndex >= 0)
            {
                normalizedHeaderPath = normalizedHeaderPath[(markerIndex + urhoMarker.Length)..];
            }
            else if (normalizedHeaderPath.StartsWith("/", StringComparison.Ordinal) || normalizedHeaderPath.Contains(":", StringComparison.Ordinal))
            {
                return defaultNamespace;
            }
        }

        var parts = normalizedHeaderPath.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length >= 2 ? $"Urho3D.{parts[0]}" : defaultNamespace;
    }
}
