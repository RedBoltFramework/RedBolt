namespace RedBolt;

public sealed class ResourceCache
{
    private readonly Dictionary<(Type Type, string Name), Resource> _cache = new();
    private readonly BackgroundLoader _backgroundLoader;
    private readonly Dictionary<Type, ulong> _memoryBudgets = new();
    private readonly List<string> _resourceDirs = [];
    private readonly Dictionary<Type, Delegate> _loaders = new();
    private readonly List<ResourceRouter> _resourceRouters = [];
    private int _finishBackgroundResourcesMs = 5;
    private bool _returnFailedResources;
    private bool _searchPackagesFirst;

    public ResourceCache()
    {
        _backgroundLoader = new BackgroundLoader(this);
    }

    public void RegisterDefaultResources()
    {
        RegisterLoader<Shader>(name => LoadFileBackedResource(Shader.GetResourceName(name), static resourceName => new Shader(resourceName)));

        RegisterLoader<Model>(name => string.Equals(name, "Models/ProceduralBox", StringComparison.Ordinal)
            ? new Model(name, 36u)
            : LoadFileBackedResource(name, static resourceName => new Model(resourceName)));

        RegisterLoader<Material>(name => LoadFileBackedResource(name, AppendExtensionIfMissing(name, ".xml"),
            static resourceName => new Material(resourceName)));
        RegisterLoader<RenderPath>(name => LoadFileBackedResource(
            name,
            GetRenderPathLookupNames(name),
            static resourceName => new RenderPath(resourceName)));
    }

    public void RegisterLoader<T>(Func<string, T?> loader)
        where T : Resource
    {
        _loaders[typeof(T)] = loader ?? throw new ArgumentNullException(nameof(loader));
    }

    public bool AddManualResource(Resource resource)
    {
        ArgumentNullException.ThrowIfNull(resource);
        var key = (resource.GetType(), resource.Name);
        if (_cache.ContainsKey(key))
            return false;

        resource.OwnerCache = this;
        _cache.Add(key, resource);
        return true;
    }

    public IReadOnlyDictionary<(Type Type, string Name), Resource> AllResources => _cache;

    public IReadOnlyList<string> ResourceDirs => _resourceDirs;

    public IReadOnlyDictionary<(Type Type, string Name), Resource> GetAllResources() => AllResources;

    public IReadOnlyList<string> GetResourceDirs() => ResourceDirs;

    public bool AddResourceDir(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (_resourceDirs.Any(existing => string.Equals(existing, path, StringComparison.Ordinal)))
            return false;

        _resourceDirs.Add(path);
        return true;
    }

    public bool RemoveResourceDir(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return _resourceDirs.Remove(path);
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

        _memoryBudgets[type] = budget;
    }

    public ulong GetMemoryBudget<T>()
        where T : Resource
    {
        return GetMemoryBudget(typeof(T));
    }

    public ulong GetMemoryBudget(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        return _memoryBudgets.GetValueOrDefault(type);
    }

    public ulong GetMemoryUse<T>()
        where T : Resource
    {
        return GetMemoryUse(typeof(T));
    }

    public ulong GetMemoryUse(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        return _cache
            .Where(item => item.Key.Type == type)
            .Aggregate(0ul, static (sum, item) => sum + item.Value.MemoryUse);
    }

    public ulong GetTotalMemoryUse()
    {
        return _cache.Values.Aggregate(0ul, static (sum, resource) => sum + resource.MemoryUse);
    }

    public void SetReturnFailedResources(bool enable)
    {
        _returnFailedResources = enable;
    }

    public bool GetReturnFailedResources() => _returnFailedResources;

    public void SetSearchPackagesFirst(bool value)
    {
        _searchPackagesFirst = value;
    }

    public bool GetSearchPackagesFirst() => _searchPackagesFirst;

    public void SetFinishBackgroundResourcesMs(int ms)
    {
        _finishBackgroundResourcesMs = Math.Max(ms, 1);
    }

    public int GetFinishBackgroundResourcesMs() => _finishBackgroundResourcesMs;

    public void AddResourceRouter(ResourceRouter router, bool addAsFirst = false)
    {
        ArgumentNullException.ThrowIfNull(router);
        if (_resourceRouters.Contains(router))
            return;
        if (addAsFirst)
            _resourceRouters.Insert(0, router);
        else
            _resourceRouters.Add(router);
    }

    public void RemoveResourceRouter(ResourceRouter router)
    {
        ArgumentNullException.ThrowIfNull(router);
        _resourceRouters.Remove(router);
    }

    public ResourceRouter? GetResourceRouter(uint index) => index < _resourceRouters.Count ? _resourceRouters[(int)index] : null;

    public void RouteResourceName(ref FileIdentifier name)
    {
        foreach (var router in _resourceRouters)
            router.Route(ref name);
    }

    public FileIdentifier GetCanonicalIdentifier(FileIdentifier name)
    {
        name.FileName = SanitateResourceName(name.FileName);
        return name;
    }

    public FileIdentifier GetResolvedIdentifier(FileIdentifier name)
    {
        name = GetCanonicalIdentifier(name);
        RouteResourceName(ref name);
        return GetCanonicalIdentifier(name);
    }

    public bool BackgroundLoadResource<T>(string name, bool sendEventOnFailure = true, Resource? caller = null)
        where T : Resource
    {
        return BackgroundLoadResource(typeof(T), name, sendEventOnFailure, caller);
    }

    public bool BackgroundLoadResource(Type type, string name, bool sendEventOnFailure = true, Resource? caller = null)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (!typeof(Resource).IsAssignableFrom(type))
            throw new ArgumentException("Type must derive from Resource.", nameof(type));

        return _backgroundLoader.QueueResource(type, name, sendEventOnFailure, caller);
    }

    public uint GetNumBackgroundLoadResources() => (uint)_backgroundLoader.GetNumQueuedResources();

    public void FinishBackgroundResources(int? maxMs = null)
    {
        _backgroundLoader.FinishResources(maxMs ?? _finishBackgroundResourcesMs);
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

        _backgroundLoader.WaitForResource(typeof(T), name);

        var key = (typeof(T), name);
        if (_cache.TryGetValue(key, out var cached))
            return (T)cached;

        var resource = TryLoadResource<T>(name)
            ?? throw new InvalidOperationException($"Resource '{name}' of type {typeof(T).Name} was not found.");

        resource.OwnerCache = this;
        _cache.Add(key, resource);
        return resource;
    }

    public T? GetResourceOrNull<T>(string name)
        where T : Resource
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        _backgroundLoader.WaitForResource(typeof(T), name);

        var key = (typeof(T), name);
        if (_cache.TryGetValue(key, out var cached))
            return (T)cached;

        var resource = TryLoadResource<T>(name);
        if (resource is null)
            return null;

        resource.OwnerCache = this;
        _cache[key] = resource;
        return resource;
    }

    public T? GetExistingResource<T>(string name)
        where T : Resource
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return _cache.TryGetValue((typeof(T), name), out var resource) ? (T)resource : null;
    }

    public IReadOnlyList<T> GetResources<T>()
        where T : Resource
    {
        return _cache
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
        return _cache.ContainsKey((typeof(T), name)) || !string.IsNullOrEmpty(GetResourceFileName(name));
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
        var resourceName = GetResolvedIdentifier(new FileIdentifier(name)).FileName;
        if (Path.IsPathFullyQualified(resourceName) && File.Exists(resourceName))
            return resourceName;

        foreach (var resourceDir in _resourceDirs)
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
        foreach (var resourceDir in _resourceDirs)
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
        _cache.Remove((typeof(T), name));
    }

    public void ReleaseResource(string resourceName, bool force = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceName);
        var keys = _cache.Keys
            .Where(key => string.Equals(key.Name, resourceName, StringComparison.Ordinal))
            .ToArray();
        foreach (var key in keys)
            _cache.Remove(key);
    }

    public void ReleaseResources<T>(bool force = false)
        where T : Resource
    {
        var keys = _cache.Keys
            .Where(static key => key.Type == typeof(T))
            .ToArray();
        foreach (var key in keys)
            _cache.Remove(key);
    }

    public bool ReloadResource<T>(string name)
        where T : Resource
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (!_cache.TryGetValue((typeof(T), name), out var resource))
            return false;

        return resource.Reload();
    }

    public void ReleaseResources<T>(string partialName, bool force = false)
        where T : Resource
    {
        ArgumentNullException.ThrowIfNull(partialName);
        var keys = _cache.Keys
            .Where(key => key.Type == typeof(T) && key.Name.Contains(partialName, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        foreach (var key in keys)
            _cache.Remove(key);
    }

    public void ReleaseResources(string partialName, bool force = false)
    {
        ArgumentNullException.ThrowIfNull(partialName);
        var keys = _cache.Keys
            .Where(key => key.Name.Contains(partialName, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        foreach (var key in keys)
            _cache.Remove(key);
    }

    public void ReleaseAllResources(bool force = false)
    {
        _cache.Clear();
    }

    public void Clear() => ReleaseAllResources(force: true);

    internal bool CanBackgroundLoad(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        return _loaders.ContainsKey(type) || type.GetConstructor([typeof(string)]) is not null;
    }

    internal bool HasResource(Type type, string name)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return _cache.ContainsKey((type, name));
    }

    internal Resource? LoadBackgroundResource(Type type, string name)
    {
        ArgumentNullException.ThrowIfNull(type);
        var fileName = GetResourceFileName(name);
        if (string.IsNullOrEmpty(fileName))
            return null;

        Resource? resource;
        try
        {
            resource = Activator.CreateInstance(type, name) as Resource;
        }
        catch (MissingMethodException)
        {
            return null;
        }

        if (resource is null)
            return null;

        resource.OwnerCache = this;
        resource.SetAbsoluteFileName(fileName);
        resource.SetAsyncLoadState(AsyncLoadState.Loading);
        try
        {
            using var stream = File.OpenRead(fileName);
            resource.SetAsyncLoadState(resource.BeginLoad(stream) ? AsyncLoadState.Success : AsyncLoadState.Fail);
        }
        catch
        {
            resource.SetAsyncLoadState(AsyncLoadState.Fail);
        }
        return resource;
    }

    internal void StoreBackgroundResource(Type type, string name, Resource resource)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(resource);
        resource.OwnerCache = this;
        _cache[(type, name)] = resource;
    }

    private T? TryLoadResource<T>(string name)
        where T : Resource
    {
        return TryLoadResource(typeof(T), name) as T;
    }

    private Resource? TryLoadResource(Type type, string name)
    {
        ArgumentNullException.ThrowIfNull(type);

        if (_loaders.TryGetValue(type, out var loader))
        {
            var loaded = loader.DynamicInvoke(name) as Resource;
            if (loaded is not null)
                return loaded;
        }

        var fileName = GetResourceFileName(name);
        if (string.IsNullOrEmpty(fileName))
            return null;

        Resource? resource;
        try
        {
            resource = Activator.CreateInstance(type, name) as Resource;
        }
        catch (MissingMethodException)
        {
            return null;
        }

        if (resource is null)
            return null;

        resource.OwnerCache = this;
        return resource.LoadFile(fileName) || _returnFailedResources ? resource : null;
    }

    private T? LoadFileBackedResource<T>(string name, Func<string, T> factory)
        where T : Resource
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(factory);

        var fileName = GetResourceFileName(name);
        if (string.IsNullOrEmpty(fileName))
            return null;

        var resource = factory(name);
        resource.OwnerCache = this;
        return resource.LoadFile(fileName) || _returnFailedResources ? resource : null;
    }

    private T? LoadFileBackedResource<T>(string logicalName, string lookupName, Func<string, T> factory)
        where T : Resource
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(logicalName);
        ArgumentException.ThrowIfNullOrWhiteSpace(lookupName);
        ArgumentNullException.ThrowIfNull(factory);

        var fileName = GetResourceFileName(lookupName);
        if (string.IsNullOrEmpty(fileName))
            return null;

        var resource = factory(logicalName);
        resource.OwnerCache = this;
        return resource.LoadFile(fileName) || _returnFailedResources ? resource : null;
    }

    private T? LoadFileBackedResource<T>(string logicalName, IReadOnlyList<string> lookupNames, Func<string, T> factory)
        where T : Resource
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(logicalName);
        ArgumentNullException.ThrowIfNull(lookupNames);
        ArgumentNullException.ThrowIfNull(factory);

        foreach (var lookupName in lookupNames)
        {
            if (string.IsNullOrWhiteSpace(lookupName))
                continue;

            var loaded = LoadFileBackedResource(logicalName, lookupName, factory);
            if (loaded is not null)
                return loaded;
        }

        return null;
    }

    private static string AppendExtensionIfMissing(string name, string extension)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(extension);
        return Path.HasExtension(name) ? name : name + extension;
    }

    private static IReadOnlyList<string> GetRenderPathLookupNames(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return Path.HasExtension(name)
            ? [name]
            : [name + ".renderpath", name + ".xml"];
    }
}
