namespace RedBolt;

internal static class ResourceLoading
{
    public static string ResolveRelativeResourceName(Resource owner, string name)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (Path.IsPathFullyQualified(name))
            return name;

        var normalized = name.Replace('\\', '/');
        if (HasPath(normalized))
            return normalized;

        var ownerName = owner.Name.Replace('\\', '/');
        var separatorIndex = ownerName.LastIndexOf('/');
        return separatorIndex >= 0
            ? ownerName[..(separatorIndex + 1)] + normalized
            : normalized;
    }

    public static string ResolveRelativeFileName(Resource owner, string name)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (Path.IsPathFullyQualified(name))
            return name;

        if (owner.OwnerCache is not null)
        {
            var resolvedName = ResolveRelativeResourceName(owner, name);
            var fileName = owner.OwnerCache.GetResourceFileName(resolvedName);
            if (!string.IsNullOrEmpty(fileName))
                return fileName;
        }

        if (!HasPath(name) && !string.IsNullOrEmpty(owner.AbsoluteFileName))
        {
            var directory = Path.GetDirectoryName(owner.AbsoluteFileName);
            if (!string.IsNullOrEmpty(directory))
                return Path.Combine(directory, name);
        }

        return name;
    }

    public static T? LoadDependentResource<T>(Resource owner, string name)
        where T : Resource
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var resourceName = ResolveRelativeResourceName(owner, name);
        var fileName = ResolveRelativeFileName(owner, name);
        if (string.IsNullOrEmpty(fileName) || !File.Exists(fileName))
            return null;

        if (Activator.CreateInstance(typeof(T), resourceName) is not T resource)
            return null;

        resource.OwnerCache = owner.OwnerCache;
        return resource.LoadFile(fileName) ? resource : null;
    }

    public static Stream? OpenDependentResource(Resource owner, string name)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var fileName = ResolveRelativeFileName(owner, name);
        return string.IsNullOrEmpty(fileName) || !File.Exists(fileName)
            ? null
            : File.OpenRead(fileName);
    }

    private static bool HasPath(string name)
    {
        return name.IndexOf('/') >= 0 || name.IndexOf('\\') >= 0;
    }
}
