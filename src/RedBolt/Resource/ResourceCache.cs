namespace RedBolt;

public sealed class ResourceCache
{
    private readonly Dictionary<(Type Type, string Name), Resource> cache = new();
    private readonly List<string> resourceDirs = [];
    private readonly Dictionary<Type, Delegate> loaders = new();

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

    public IReadOnlyList<string> ResourceDirs => resourceDirs;

    public bool AddResourceDir(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (resourceDirs.Any(existing => string.Equals(existing, path, StringComparison.Ordinal)))
            return false;

        resourceDirs.Add(path);
        return true;
    }

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

        if (!loaders.TryGetValue(typeof(T), out var loader))
            throw new InvalidOperationException($"No resource loader is registered for {typeof(T).Name}.");

        var resource = ((Func<string, T?>)loader)(name)
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

        if (!loaders.TryGetValue(typeof(T), out var loader))
            throw new InvalidOperationException($"No resource loader is registered for {typeof(T).Name}.");

        return ((Func<string, T?>)loader)(name)
            ?? throw new InvalidOperationException($"Resource '{name}' of type {typeof(T).Name} was not found.");
    }

    public bool Exists<T>(string name)
        where T : Resource
    {
        return cache.ContainsKey((typeof(T), name));
    }

    public bool Exists(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return resourceDirs.Select(path => Path.Combine(path, name)).Any(File.Exists);
    }

    public void ReleaseResource<T>(string name)
        where T : Resource
    {
        cache.Remove((typeof(T), name));
    }

    public void ReleaseResources<T>(string partialName, bool force = false)
        where T : Resource
    {
        var keys = cache.Keys
            .Where(key => key.Type == typeof(T) && key.Name.Contains(partialName, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        foreach (var key in keys)
            cache.Remove(key);
    }

    public void ReleaseAllResources(bool force = false)
    {
        cache.Clear();
    }
}
