namespace RedBolt;

public sealed class ResourceCache : Object
{
    private readonly Dictionary<(Type Type, string Name), Resource> _cache = new();
    private readonly BackgroundLoader _backgroundLoader;
    private readonly Dictionary<Type, ulong> _memoryBudgets = new();
    private readonly List<string> _resourceDirs = [];
    private VirtualFileSystem? _standaloneVirtualFileSystem;
    private readonly Dictionary<Type, Delegate> _loaders = new();
    private readonly List<ResourceRouter> _resourceRouters = [];
    private readonly Dictionary<Resource, HashSet<string>> _resourceDependencies = new();
    private readonly HashSet<string> _ignoredReloads = new(StringComparer.OrdinalIgnoreCase);
    private int _finishBackgroundResourcesMs = 5;
    private bool _returnFailedResources;
    private bool _searchPackagesFirst;
    private bool _resourceReloadSuspended;

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
        RegisterLoader<Sound>(name => LoadFileBackedResource(
            name,
            static resourceName =>
            {
                var sound = new Sound();
                sound.SetName(resourceName);
                return sound;
            }));
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
        foreach (var existing in _resourceDirs)
        {
            if (string.Equals(existing, path, StringComparison.Ordinal))
                return false;
        }

        _resourceDirs.Add(path);
        var virtualFileSystem = GetVirtualFileSystem();
        if (!HasMountForResourcePath(virtualFileSystem, path))
            virtualFileSystem.MountDir(path);
        return true;
    }

    public bool RemoveResourceDir(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!_resourceDirs.Remove(path))
            return false;

        var virtualFileSystem = GetVirtualFileSystem();

        for (var index = 0u; index < virtualFileSystem.NumMountPoints(); ++index)
        {
            if (virtualFileSystem.GetMountPoint(index) is MountedDirectory directory
                && string.Equals(
                    Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory.GetDirectory())),
                    Path.TrimEndingDirectorySeparator(Path.GetFullPath(path)),
                    StringComparison.Ordinal))
            {
                virtualFileSystem.Unmount(directory);
                break;
            }
        }

        return true;
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

    internal bool BackgroundLoadResource(StringHash type, string name, bool sendEventOnFailure = true, Resource? caller = null)
    {
        var resourceType = ResolveResourceType(type);
        return resourceType is not null && BackgroundLoadResource(resourceType, name, sendEventOnFailure, caller);
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
        var virtualFileSystem = GetVirtualFileSystem();
        var file = virtualFileSystem.OpenFile(GetResolvedIdentifier(new FileIdentifier(name)), File.FileMode.Read);
        if (file is null)
            return null;

        try
        {
            var stream = new MemoryStream();
            var buffer = new byte[81920];
            while (true)
            {
                var read = file.Read(buffer);
                if (read == 0)
                    break;
                stream.Write(buffer, 0, checked((int)read));
            }

            stream.Position = 0;
            return stream;
        }
        finally
        {
            file.Close();
        }
    }

    public DateTime GetLastModifiedTime(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var identifier = GetResolvedIdentifier(new FileIdentifier(name));
        var timestamp = GetVirtualFileSystem().GetLastModifiedTime(identifier, false);
        return timestamp == 0
            ? DateTime.MinValue
            : DateTimeOffset.FromUnixTimeSeconds(timestamp).UtcDateTime;
    }

    public string GetResourceFileName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var resourceName = GetResolvedIdentifier(new FileIdentifier(name));
        return GetVirtualFileSystem().GetAbsoluteNameFromIdentifier(resourceName);
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

    public bool ReloadResource(Resource resource)
    {
        ArgumentNullException.ThrowIfNull(resource);
        return resource.Reload();
    }

    public void StoreResourceDependency(Resource resource, string dependency)
    {
        ArgumentNullException.ThrowIfNull(resource);
        ArgumentException.ThrowIfNullOrWhiteSpace(dependency);
        if (!_resourceDependencies.TryGetValue(resource, out var dependencies))
            _resourceDependencies[resource] = dependencies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        dependencies.Add(SanitateResourceName(dependency));
    }

    public void ResetDependencies(Resource resource)
    {
        ArgumentNullException.ThrowIfNull(resource);
        _resourceDependencies.Remove(resource);
    }

    public void ReloadResourceWithDependencies(string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        if (_resourceReloadSuspended)
            return;
        var dependency = SanitateResourceName(fileName);
        if (_ignoredReloads.Remove(dependency))
            return;

        foreach (var resource in _cache.Values.ToArray())
        {
            var isResource = (!string.IsNullOrEmpty(resource.AbsoluteFileName)
                    && string.Equals(SanitateResourceName(resource.AbsoluteFileName), dependency, StringComparison.OrdinalIgnoreCase))
                || string.Equals(resource.Name, dependency, StringComparison.OrdinalIgnoreCase);
            var isDependency = _resourceDependencies.TryGetValue(resource, out var dependencies) && dependencies.Contains(dependency);
            if (isResource || isDependency)
                resource.Reload();
        }
    }

    public void SetResourceReloadSuspended(bool suspended) => _resourceReloadSuspended = suspended;

    public void IgnoreResourceReload(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _ignoredReloads.Add(SanitateResourceName(name));
    }

    public void IgnoreResourceReload(Resource resource)
    {
        ArgumentNullException.ThrowIfNull(resource);
        IgnoreResourceReload(string.IsNullOrEmpty(resource.AbsoluteFileName) ? resource.Name : resource.AbsoluteFileName);
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

    public void Scan(ICollection<string> result, string pathName, string filter, ScanFlags flags)
    {
        ArgumentNullException.ThrowIfNull(result);
        pathName ??= string.Empty;
        filter = string.IsNullOrEmpty(filter) ? "*" : filter;
        if ((flags & ScanFlags.ScanAppend) != ScanFlags.ScanAppend)
            result.Clear();

        var virtualFileSystem = GetVirtualFileSystem();
        virtualFileSystem.Scan(result, new FileIdentifier(pathName), filter, flags);

        // Scan manual resources.
        if ((flags & ScanFlags.ScanFiles) != ScanFlags.ScanFiles)
            return;

        var extension = GetExtensionFromFilter(filter);
        foreach (var resource in _cache.Values)
        {
            var resourceName = resource.Name.Replace('\\', '/');
            if (!MatchesResourceName(resourceName, pathName, extension, (flags & ScanFlags.ScanRecursive) != 0))
                continue;

            if (!virtualFileSystem.Exists(new FileIdentifier(resourceName)))
                result.Add(TrimPathPrefix(resourceName, pathName));
        }
    }

    public string PrintMemoryUsage()
    {
        var lines = _cache.GroupBy(static item => item.Key.Type)
            .OrderBy(static group => group.Key.Name, StringComparer.Ordinal)
            .Select(group => $"{group.Key.Name}: {group.Aggregate(0ul, static (sum, item) => sum + item.Value.MemoryUse)} bytes ({group.Count()} resources)");
        return string.Join(Environment.NewLine, lines);
    }

    public string PrintResources(string typeName = "")
    {
        return string.Join(Environment.NewLine, _cache
            .Where(item => string.IsNullOrEmpty(typeName) || string.Equals(item.Key.Type.Name, typeName, StringComparison.Ordinal))
            .OrderBy(static item => item.Key.Type.Name, StringComparer.Ordinal)
            .ThenBy(static item => item.Key.Name, StringComparer.Ordinal)
            .Select(static item => $"{item.Key.Type.Name};{item.Key.Name};{item.Value.MemoryUse}"));
    }

    internal bool CanBackgroundLoad(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        return _loaders.ContainsKey(type) || type.GetConstructor([typeof(string)]) is not null;
    }

    private VirtualFileSystem GetVirtualFileSystem()
    {
        var virtualFileSystem = GetSubsystem<VirtualFileSystem>();
        if (virtualFileSystem is not null)
            return virtualFileSystem;

        _standaloneVirtualFileSystem ??= new VirtualFileSystem();
        return _standaloneVirtualFileSystem;
    }

    private static bool HasMountForResourcePath(VirtualFileSystem virtualFileSystem, string path)
    {
        var normalizedPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        for (var index = 0u; index < virtualFileSystem.NumMountPoints(); ++index)
        {
            var mountPoint = virtualFileSystem.GetMountPoint(index);
            if (mountPoint is MountedDirectory directory
                && string.Equals(
                    Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory.GetDirectory())),
                    normalizedPath,
                    StringComparison.Ordinal))
            {
                return true;
            }

            if (mountPoint is PackageFile package)
            {
                var packagePath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(package.GetName()));
                if (string.Equals(Path.ChangeExtension(packagePath, null), normalizedPath, StringComparison.Ordinal))
                    return true;
            }
        }

        return false;
    }

    private static Type? ResolveResourceType(StringHash type)
    {
        foreach (var candidate in typeof(Resource).Assembly.GetTypes())
        {
            if (!candidate.IsAbstract && typeof(Resource).IsAssignableFrom(candidate)
                && StringHash.Calculate(candidate.GetClassName()) == type)
            {
                return candidate;
            }
        }

        return null;
    }

    private static string GetExtensionFromFilter(string filter)
    {
        var extension = filter.StartsWith("*.", StringComparison.Ordinal) ? filter[1..] : string.Empty;
        return extension.Contains('*') || extension.Contains('?') ? string.Empty : extension;
    }

    private static bool MatchesResourceName(string resourceName, string pathName, string extension, bool recursive)
    {
        var normalizedPath = pathName.Replace('\\', '/').Trim('/');
        var normalizedName = resourceName.Trim('/');
        if (!string.IsNullOrEmpty(normalizedPath))
        {
            if (!normalizedName.StartsWith(normalizedPath + "/", StringComparison.OrdinalIgnoreCase))
                return false;
            normalizedName = normalizedName[(normalizedPath.Length + 1)..];
        }

        if (!recursive && normalizedName.Contains('/', StringComparison.Ordinal))
            return false;
        return string.IsNullOrEmpty(extension)
            || normalizedName.EndsWith(extension, StringComparison.OrdinalIgnoreCase);
    }

    private static string TrimPathPrefix(string fileName, string prefix)
    {
        var normalizedFileName = fileName.Replace('\\', '/');
        var normalizedPrefix = prefix.Replace('\\', '/').Trim('/');
        if (string.IsNullOrEmpty(normalizedPrefix))
            return normalizedFileName;
        return normalizedFileName.StartsWith(normalizedPrefix + "/", StringComparison.OrdinalIgnoreCase)
            ? normalizedFileName[(normalizedPrefix.Length + 1)..]
            : normalizedFileName;
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
            using var stream = GetFile(name, sendEventOnFailure: false);
            resource.SetAsyncLoadState(stream is not null && resource.BeginLoad(stream)
                ? AsyncLoadState.Success
                : AsyncLoadState.Fail);
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
        return resource.LoadFile(name) || _returnFailedResources ? resource : null;
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
        return resource.LoadFile(name) || _returnFailedResources ? resource : null;
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
        return resource.LoadFile(lookupName) || _returnFailedResources ? resource : null;
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
