namespace RedBolt;

public sealed class ResourceCache
{
    private readonly Dictionary<(Type Type, string Name), Resource> cache = new();
    private readonly Dictionary<Type, ulong> memoryBudgets = new();
    private readonly List<string> resourceDirs = [];
    private readonly Dictionary<Type, Delegate> loaders = new();
    private int finishBackgroundResourcesMs = 5;
    private bool returnFailedResources;
    private bool searchPackagesFirst;

    public void RegisterDefaultResources()
    {
        RegisterLoader<Model>(static name => string.Equals(name, "Models/ProceduralBox", StringComparison.Ordinal)
            ? new Model(name, 36u)
            : null);

        RegisterLoader<Material>(static name => string.Equals(name, "Materials/FirstCube", StringComparison.Ordinal)
            ? new Material(name)
            : null);
    }

    public void RegisterLoader<T>(Func<string, T?> loader)
        where T : Resource
    {
        loaders[typeof(T)] = loader ?? throw new ArgumentNullException(nameof(loader));
    }

    public bool AddManualResource(Resource resource)
    {
        ArgumentNullException.ThrowIfNull(resource);
        var key = (resource.GetType(), resource.Name);
        if (cache.ContainsKey(key))
            return false;

        cache.Add(key, resource);
        return true;
    }

    public IReadOnlyDictionary<(Type Type, string Name), Resource> AllResources => cache;

    public IReadOnlyList<string> ResourceDirs => resourceDirs;

    public IReadOnlyDictionary<(Type Type, string Name), Resource> GetAllResources() => AllResources;

    public IReadOnlyList<string> GetResourceDirs() => ResourceDirs;

    public bool AddResourceDir(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (resourceDirs.Any(existing => string.Equals(existing, path, StringComparison.Ordinal)))
            return false;

        resourceDirs.Add(path);
        return true;
    }

    public bool RemoveResourceDir(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return resourceDirs.Remove(path);
    }

    public void SetMemoryBudget<T>(ulong budget)
        where T : Resource
    {
        SetMemoryBudget(typeof(T), budget);
    }

    public void SetMemoryBudget(Type type, ulong budget)
    {
        ArgumentNullException.ThrowIfNull(type);
        if (!typeof(Resource).IsAssignableFrom(type))
            throw new ArgumentException("Type must derive from Resource.", nameof(type));

        memoryBudgets[type] = budget;
    }

    public ulong GetMemoryBudget<T>()
        where T : Resource
    {
        return GetMemoryBudget(typeof(T));
    }

    public ulong GetMemoryBudget(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        return memoryBudgets.GetValueOrDefault(type);
    }

    public ulong GetMemoryUse<T>()
        where T : Resource
    {
        return GetMemoryUse(typeof(T));
    }

    public ulong GetMemoryUse(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        return cache
            .Where(item => item.Key.Type == type)
            .Aggregate(0ul, static (sum, item) => sum + item.Value.MemoryUse);
    }

    public ulong GetTotalMemoryUse()
    {
        return cache.Values.Aggregate(0ul, static (sum, resource) => sum + resource.MemoryUse);
    }

    public void SetReturnFailedResources(bool enable)
    {
        returnFailedResources = enable;
    }

    public bool GetReturnFailedResources() => returnFailedResources;

    public void SetSearchPackagesFirst(bool value)
    {
        searchPackagesFirst = value;
    }

    public bool GetSearchPackagesFirst() => searchPackagesFirst;

    public void SetFinishBackgroundResourcesMs(int ms)
    {
        finishBackgroundResourcesMs = Math.Max(ms, 1);
    }

    public int GetFinishBackgroundResourcesMs() => finishBackgroundResourcesMs;

    public T Get<T>(string name)
        where T : Resource
    {
        return GetResource<T>(name);
    }

    public T GetResource<T>(string name, bool sendEventOnFailure = true)
        where T : Resource
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var key = (typeof(T), name);
        if (cache.TryGetValue(key, out var cached))
            return (T)cached;

        var resource = TryLoadResource<T>(name)
            ?? throw new InvalidOperationException($"Resource '{name}' of type {typeof(T).Name} was not found.");

        cache.Add(key, resource);
        return resource;
    }

    public T? GetExistingResource<T>(string name)
        where T : Resource
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return cache.TryGetValue((typeof(T), name), out var resource) ? (T)resource : null;
    }

    public IReadOnlyList<T> GetResources<T>()
        where T : Resource
    {
        return cache
            .Where(static item => item.Key.Type == typeof(T))
            .Select(static item => (T)item.Value)
            .ToArray();
    }

    public T GetTempResource<T>(string name, bool sendEventOnFailure = true)
        where T : Resource
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return TryLoadResource<T>(name)
            ?? throw new InvalidOperationException($"Resource '{name}' of type {typeof(T).Name} was not found.");
    }

    public bool Exists<T>(string name)
        where T : Resource
    {
        return cache.ContainsKey((typeof(T), name)) || !string.IsNullOrEmpty(GetResourceFileName(name));
    }

    public bool Exists(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return !string.IsNullOrEmpty(GetResourceFileName(name));
    }

    public Stream? GetFile(string name, bool sendEventOnFailure = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var fileName = GetResourceFileName(name);
        return string.IsNullOrEmpty(fileName) ? null : File.OpenRead(fileName);
    }

    public string GetResourceFileName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var resourceName = SanitateResourceName(name);
        if (Path.IsPathFullyQualified(resourceName) && File.Exists(resourceName))
            return resourceName;

        foreach (var resourceDir in resourceDirs)
        {
            var candidate = Path.Combine(resourceDir, resourceName);
            if (File.Exists(candidate))
                return candidate;
        }

        return string.Empty;
    }

    public string SanitateResourceName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var normalized = name.Trim().Replace('\\', '/');
        while (normalized.StartsWith("./", StringComparison.Ordinal))
            normalized = normalized[2..];

        while (normalized.Contains("//", StringComparison.Ordinal))
            normalized = normalized.Replace("//", "/", StringComparison.Ordinal);

        if (!Path.IsPathFullyQualified(normalized))
            return normalized.TrimStart('/');

        var fullName = Path.GetFullPath(normalized).Replace('\\', '/');
        foreach (var resourceDir in resourceDirs)
        {
            var fullDir = Path.GetFullPath(resourceDir).Replace('\\', '/').TrimEnd('/') + "/";
            if (fullName.StartsWith(fullDir, StringComparison.Ordinal))
                return fullName[fullDir.Length..];
        }

        return fullName;
    }

    public void ReleaseResource<T>(string name)
        where T : Resource
    {
        cache.Remove((typeof(T), name));
    }

    public void ReleaseResource(string resourceName, bool force = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceName);
        var keys = cache.Keys
            .Where(key => string.Equals(key.Name, resourceName, StringComparison.Ordinal))
            .ToArray();
        foreach (var key in keys)
            cache.Remove(key);
    }

    public void ReleaseResources<T>(bool force = false)
        where T : Resource
    {
        var keys = cache.Keys
            .Where(static key => key.Type == typeof(T))
            .ToArray();
        foreach (var key in keys)
            cache.Remove(key);
    }

    public void ReleaseResources<T>(string partialName, bool force = false)
        where T : Resource
    {
        ArgumentNullException.ThrowIfNull(partialName);
        var keys = cache.Keys
            .Where(key => key.Type == typeof(T) && key.Name.Contains(partialName, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        foreach (var key in keys)
            cache.Remove(key);
    }

    public void ReleaseResources(string partialName, bool force = false)
    {
        ArgumentNullException.ThrowIfNull(partialName);
        var keys = cache.Keys
            .Where(key => key.Name.Contains(partialName, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        foreach (var key in keys)
            cache.Remove(key);
    }

    public void ReleaseAllResources(bool force = false)
    {
        cache.Clear();
    }

    public void Clear() => ReleaseAllResources(force: true);

    private T? TryLoadResource<T>(string name)
        where T : Resource
    {
        if (loaders.TryGetValue(typeof(T), out var loader))
        {
            var loaded = ((Func<string, T?>)loader)(name);
            if (loaded is not null)
                return loaded;
        }

        var fileName = GetResourceFileName(name);
        if (string.IsNullOrEmpty(fileName))
            return null;

        T? resource;
        try
        {
            resource = Activator.CreateInstance(typeof(T), name) as T;
        }
        catch (MissingMethodException)
        {
            return null;
        }

        if (resource is null)
            return null;

        return resource.LoadFile(fileName) || returnFailedResources ? resource : null;
    }
}
