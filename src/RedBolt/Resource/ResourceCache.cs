namespace RedBolt;

public sealed class ResourceGroup
{
    /// <summary>Memory budget.</summary>
    public ulong MemoryBudget;

    /// <summary>Current memory use.</summary>
    public ulong MemoryUse;

    /// <summary>Resources.</summary>
    public readonly Dictionary<StringHash, Resource> Resources = new();
}

public sealed class ResourceCache : Object
{
    private readonly Dictionary<(Type Type, string Name), Resource> _cache = new();
    private readonly Dictionary<Type, ResourceGroup> _resourceGroups = new();
    private readonly BackgroundLoader _backgroundLoader;
    private readonly Dictionary<Type, ulong> _memoryBudgets = new();
    private readonly List<string> _resourceDirs = [];
    private VirtualFileSystem? _standaloneVirtualFileSystem;
    private readonly Dictionary<Type, Delegate> _loaders = new();
    [ThreadStatic]
    private static bool _routeResourceNameReentrancyGuard;
    private readonly List<ResourceRouter> _resourceRouters = [];
    private readonly Dictionary<Resource, HashSet<string>> _resourceDependencies = new();
    private readonly HashSet<string> _ignoredReloads = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _pendingResourceReloads = new(StringComparer.OrdinalIgnoreCase);
    private int _finishBackgroundResourcesMs = 5;
    private bool _returnFailedResources;
    private bool _searchPackagesFirst;
    private bool _resourceReloadSuspended;

    public ResourceCache()
    {
        _backgroundLoader = new BackgroundLoader(this);
        SubscribeToEvent<CoreEvents.BeginFrameEvent>((ref CoreEvents.BeginFrameEvent _) => HandleBeginFrame());
        SubscribeToEvent<ResourceEvents.FileChanged>(HandleFileChanged);
        Context.Instance.ReflectionRemoved += HandleReflectionRemoved;
    }

    public override void Dispose()
    {
        Context.Instance.ReflectionRemoved -= HandleReflectionRemoved;
        _backgroundLoader.Dispose();
        base.Dispose();
    }

    public void RegisterDefaultResources()
    {
        RegisterLoader<Shader>(name => LoadFileBackedResource(Shader.GetResourceName(name), static resourceName => new Shader(resourceName)));

        RegisterLoader<Model>(name => LoadFileBackedResource(name, static resourceName => new Model(resourceName)));

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
        AddResourceToGroup(resource.GetType(), resource);
        UpdateResourceGroup(resource.GetType());
        return true;
    }

    public IReadOnlyDictionary<(Type Type, string Name), Resource> AllResources => _cache;

    public IReadOnlyList<string> ResourceDirs => _resourceDirs;

    public IReadOnlyDictionary<Type, ResourceGroup> GetAllResources() => _resourceGroups;

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
        GetResourceGroup(type).MemoryBudget = budget;
        UpdateResourceGroup(type);
    }

    public void SetMemoryBudget(StringHash type, ulong budget)
    {
        if (ResolveResourceType(type) is { } resourceType)
            SetMemoryBudget(resourceType, budget);
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

    public ulong GetMemoryBudget(StringHash type)
    {
        return ResolveResourceType(type) is { } resourceType
            ? GetMemoryBudget(resourceType)
            : 0;
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

    public ulong GetMemoryUse(StringHash type)
    {
        return ResolveResourceType(type) is { } resourceType
            ? GetMemoryUse(resourceType)
            : 0;
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
        name = GetVirtualFileSystem().GetCanonicalIdentifier(name);

        if (_routeResourceNameReentrancyGuard)
            return;

        _routeResourceNameReentrancyGuard = true;
        foreach (var router in _resourceRouters)
            router.Route(ref name);
        _routeResourceNameReentrancyGuard = false;
    }

    public FileIdentifier GetCanonicalIdentifier(FileIdentifier name)
    {
        return GetVirtualFileSystem().GetCanonicalIdentifier(name);
    }

    public FileIdentifier GetResolvedIdentifier(FileIdentifier name)
    {
        var result = name;
        RouteResourceName(ref result);
        return result;
    }

    public Resource? GetResource(StringHash type, string name, bool sendEventOnFailure = true)
    {
        var resourceType = ResolveResourceType(type);
        if (resourceType is null)
        {
            if (sendEventOnFailure)
            {
                var eventData = new ResourceEvents.UnknownResourceType { ResourceType = type };
                SendEvent(ref eventData);
            }

            return null;
        }

        return GetResourceByType(resourceType, name, sendEventOnFailure);
    }

    public Resource? GetTempResource(StringHash type, string name, bool sendEventOnFailure = true)
    {
        var sanitatedName = SanitateResourceName(name);
        if (string.IsNullOrEmpty(sanitatedName))
            return null;

        var resourceType = ResolveResourceType(type);
        if (resourceType is null)
            return null;

        return TryLoadResource(resourceType, sanitatedName);
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

    public bool BackgroundLoadResource(StringHash type, string name, bool sendEventOnFailure = true, Resource? caller = null)
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

    public Resource? GetExistingResource(StringHash type, string name)
    {
        var resourceType = ResolveResourceType(type);
        return resourceType is null
            ? null
            : GetExistingResource(resourceType, name);
    }

    public IReadOnlyList<Resource> GetResources(StringHash type)
    {
        var resourceType = ResolveResourceType(type);
        return resourceType is null
            ? Array.Empty<Resource>()
            : GetResources(resourceType);
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
            ?? LoadFailure<T>(name, sendEventOnFailure);

        resource.OwnerCache = this;
        _cache.Add(key, resource);
        AddResourceToGroup(typeof(T), resource);
        UpdateResourceGroup(typeof(T));
        return resource;
    }

    public T? GetResourceOrNull<T>(string name)
        where T : Resource
    {
        ArgumentNullException.ThrowIfNull(name);
        if (name.Length == 0)
            return null;

        _backgroundLoader.WaitForResource(typeof(T), name);

        var key = (typeof(T), name);
        if (_cache.TryGetValue(key, out var cached))
            return (T)cached;

        var resource = TryLoadResource<T>(name);
        if (resource is null)
            return null;

        resource.OwnerCache = this;
        _cache[key] = resource;
        AddResourceToGroup(typeof(T), resource);
        UpdateResourceGroup(typeof(T));
        return resource;
    }

    public T? GetExistingResource<T>(string name)
        where T : Resource
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return _cache.TryGetValue((typeof(T), name), out var resource) ? (T)resource : null;
    }

    public Resource? GetExistingResource(Type type, string name)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return FindSpecificResource(type, StringHash.Calculate(name));
    }

    public IReadOnlyList<T> GetResources<T>()
        where T : Resource
    {
        return _cache
            .Where(static item => item.Key.Type == typeof(T))
            .Select(static item => (T)item.Value)
            .ToArray();
    }

    public IReadOnlyList<Resource> GetResources(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        return _resourceGroups.TryGetValue(type, out var group)
            ? group.Resources.Values.ToArray()
            : Array.Empty<Resource>();
    }

    public T? GetTempResource<T>(string name, bool sendEventOnFailure = true)
        where T : Resource
    {
        var sanitatedName = SanitateResourceName(name);
        if (string.IsNullOrEmpty(sanitatedName))
            return null;

        return TryLoadResource<T>(sanitatedName);
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
        var resolvedName = GetResolvedIdentifier(new FileIdentifier(name));
        var file = virtualFileSystem.OpenFile(resolvedName, FileMode.Read);
        if (file is null)
        {
            if (sendEventOnFailure)
                SendResourceNotFound(name, resolvedName);
            return null;
        }

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
        return GetCanonicalIdentifier(FileIdentifier.FromUri(name)).ToUri();
    }

    public void ReleaseResource<T>(string name)
        where T : Resource
    {
        RemoveCachedResource((typeof(T), name));
        UpdateResourceGroup(typeof(T));
    }

    public void ReleaseResource(StringHash type, string name, bool force = false)
    {
        var resourceType = ResolveResourceType(type);
        if (resourceType is null)
            return;

        RemoveCachedResource((resourceType, SanitateResourceName(name)));
        UpdateResourceGroup(resourceType);
    }

    public void ReleaseResource(string resourceName, bool force = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceName);
        var keys = _cache.Keys
            .Where(key => string.Equals(key.Name, resourceName, StringComparison.Ordinal))
            .ToArray();
        var affectedTypes = new HashSet<Type>();
        foreach (var key in keys)
        {
            RemoveCachedResource(key);
            affectedTypes.Add(key.Type);
        }
        foreach (var type in affectedTypes)
            UpdateResourceGroup(type);
    }

    public void ReleaseResources<T>(bool force = false)
        where T : Resource
    {
        var keys = _cache.Keys
            .Where(static key => key.Type == typeof(T))
            .ToArray();
        foreach (var key in keys)
            RemoveCachedResource(key);
        UpdateResourceGroup(typeof(T));
    }

    public void ReleaseResources(StringHash type, bool force = false)
    {
        var resourceType = ResolveResourceType(type);
        if (resourceType is null)
            return;

        var keys = _cache.Keys
            .Where(key => key.Type == resourceType)
            .ToArray();
        foreach (var key in keys)
            RemoveCachedResource(key);
        UpdateResourceGroup(resourceType);
    }

    public void ReleaseResources(StringHash type, string partialName, bool force = false)
    {
        var resourceType = ResolveResourceType(type);
        if (resourceType is null)
            return;

        ArgumentNullException.ThrowIfNull(partialName);
        var keys = _cache.Keys
            .Where(key => key.Type == resourceType && key.Name.Contains(partialName, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        foreach (var key in keys)
            RemoveCachedResource(key);
        UpdateResourceGroup(resourceType);
    }

    public bool ReloadResource<T>(string name)
        where T : Resource
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (!_cache.TryGetValue((typeof(T), name), out var resource))
            return false;

        return ReloadResource(resource);
    }

    public bool ReloadResource(Resource resource)
    {
        ArgumentNullException.ThrowIfNull(resource);
        resource.SendEvent<ResourceEvents.ReloadStarted>();

        var success = false;
        using (var file = GetFile(resource.Name, sendEventOnFailure: false))
        {
            if (file is not null)
                success = resource.Load(file);
        }

        if (success)
        {
            resource.NotifyReloadFinished();
            resource.SendEvent<ResourceEvents.ReloadFinished>();
        }
        else
            resource.SendEvent<ResourceEvents.ReloadFailed>();

        return success;
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
        var dependency = SanitateResourceName(fileName);

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

    public void SetResourceReloadSuspended(bool suspended)
    {
        _resourceReloadSuspended = suspended;
        if (suspended)
            return;

        foreach (var fileName in _pendingResourceReloads)
            ReloadResourceWithDependencies(fileName);
        _pendingResourceReloads.Clear();
    }

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
            RemoveCachedResource(key);
        UpdateResourceGroup(typeof(T));
    }

    public void ReleaseResources(string partialName, bool force = false)
    {
        ArgumentNullException.ThrowIfNull(partialName);
        var keys = _cache.Keys
            .Where(key => key.Name.Contains(partialName, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var affectedTypes = new HashSet<Type>();
        foreach (var key in keys)
        {
            RemoveCachedResource(key);
            affectedTypes.Add(key.Type);
        }
        foreach (var type in affectedTypes)
            UpdateResourceGroup(type);
    }

    public void ReleaseAllResources(bool force = false)
    {
        _cache.Clear();
        foreach (var group in _resourceGroups.Values)
        {
            group.Resources.Clear();
            group.MemoryUse = 0;
        }
    }

    public void ReleasePackageResources(PackageFile package, bool force = false)
    {
        ArgumentNullException.ThrowIfNull(package);
        var packageNameHashes = package.GetEntries().Keys
            .Select(StringHash.Calculate)
            .ToHashSet();
        var keys = _cache
            .Where(entry => packageNameHashes.Contains(entry.Value.NameHash))
            .Select(entry => entry.Key)
            .ToArray();
        var affectedTypes = new HashSet<Type>();
        foreach (var key in keys)
        {
            RemoveCachedResource(key);
            affectedTypes.Add(key.Type);
        }
        foreach (var type in affectedTypes)
            UpdateResourceGroup(type);
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
        var output = new System.Text.StringBuilder("Resource Type                 Cnt       Avg       Max    Budget     Total\n\n");
        uint totalResourceCount = 0;
        ulong totalLargest = 0;
        ulong totalUse = 0;

        foreach (var (type, group) in _resourceGroups.OrderBy(static pair => pair.Key.Name, StringComparer.Ordinal))
        {
            var resourceCount = (uint)group.Resources.Count;
            var average = resourceCount == 0 ? 0UL : group.MemoryUse / resourceCount;
            var largest = group.Resources.Values.Select(static resource => (ulong)resource.MemoryUse).DefaultIfEmpty().Max();
            totalResourceCount += resourceCount;
            totalLargest = Math.Max(totalLargest, largest);
            totalUse += group.MemoryUse;

            output.AppendFormat(
                "{0,-28} {1,4} {2,9} {3,9} {4,9} {5,9}\n",
                type.Name, resourceCount, average, largest, group.MemoryBudget, group.MemoryUse);
        }

        var totalAverage = totalResourceCount == 0 ? 0UL : totalUse / totalResourceCount;
        output.AppendFormat(
            "{0,-28} {1,4} {2,9} {3,9} {4,9} {5,9}\n",
            "All", totalResourceCount, totalAverage, totalLargest, "-", totalUse);
        return output.ToString();
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

    private ResourceGroup GetResourceGroup(Type type)
    {
        if (!_resourceGroups.TryGetValue(type, out var group))
        {
            group = new ResourceGroup();
            _resourceGroups.Add(type, group);
        }

        return group;
    }

    private void AddResourceToGroup(Type type, Resource resource)
    {
        GetResourceGroup(type).Resources[resource.GetNameHash()] = resource;
    }

    private void RemoveCachedResource((Type Type, string Name) key)
    {
        if (!_cache.Remove(key, out var resource))
            return;

        if (_resourceGroups.TryGetValue(key.Type, out var group))
            group.Resources.Remove(resource.GetNameHash());
    }

    private Resource? FindSpecificResource(Type type, StringHash nameHash)
    {
        return _resourceGroups.TryGetValue(type, out var group)
            && group.Resources.TryGetValue(nameHash, out var resource)
            ? resource
            : null;
    }

    private void FindMatchingResources(StringHash nameHash, ICollection<Resource> resources)
    {
        foreach (var group in _resourceGroups.Values)
        {
            if (group.Resources.TryGetValue(nameHash, out var resource))
                resources.Add(resource);
        }
    }

    private void UpdateResourceGroup(Type type)
    {
        if (!_resourceGroups.TryGetValue(type, out var group))
            return;

        var memoryBudget = group.MemoryBudget;

        for (;;)
        {
            ulong memoryUse = 0;
            uint oldestTimer = 0;
            Resource? oldestResource = null;
            (Type Type, string Name) oldestKey = default;

            foreach (var resource in group.Resources.Values)
            {
                memoryUse += resource.MemoryUse;
                var useTimer = resource.GetUseTimer();
                if (useTimer > oldestTimer)
                {
                    oldestTimer = useTimer;
                    oldestResource = resource;
                    oldestKey = (type, resource.Name);
                }
            }

            group.MemoryUse = memoryUse;
            if (memoryBudget == 0 || memoryUse <= memoryBudget || oldestResource is null)
                return;

            RemoveCachedResource(oldestKey);
        }
    }

    private void HandleReflectionRemoved(ObjectReflection reflection)
    {
        var keys = _cache.Keys
            .Where(key => key.Type == reflection.ManagedType)
            .ToArray();
        foreach (var key in keys)
            RemoveCachedResource(key);
        UpdateResourceGroup(reflection.ManagedType);
    }

    internal Resource? LoadBackgroundResource(Type type, string name, bool sendEventOnFailure)
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
            using var stream = GetFile(name, sendEventOnFailure);
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

    private Resource? GetResourceByType(Type type, string name, bool sendEventOnFailure)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var sanitizedName = SanitateResourceName(name);
        if (string.IsNullOrEmpty(sanitizedName))
            return null;

        _backgroundLoader.WaitForResource(type, sanitizedName);
        var key = (type, sanitizedName);
        if (_cache.TryGetValue(key, out var cached))
            return cached;

        var resource = TryLoadResource(type, sanitizedName);
        if (resource is null)
        {
            if (sendEventOnFailure)
            {
                if (string.IsNullOrEmpty(GetResourceFileName(sanitizedName)))
                    SendResourceNotFound(sanitizedName, FileIdentifier.Empty);
                else
                {
                    var eventData = new ResourceEvents.LoadFailed { ResourceName = sanitizedName };
                    SendEvent(ref eventData);
                }
            }

            return null;
        }

        resource.OwnerCache = this;
        resource.SetName(sanitizedName);
        _cache[key] = resource;
        AddResourceToGroup(type, resource);
        UpdateResourceGroup(type);
        return resource;
    }

    internal void StoreBackgroundResource(Type type, string name, Resource resource)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(resource);
        resource.OwnerCache = this;
        RemoveCachedResource((type, name));
        _cache[(type, name)] = resource;
        AddResourceToGroup(type, resource);
        UpdateResourceGroup(type);
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

    private T LoadFailure<T>(string name, bool sendEventOnFailure)
        where T : Resource
    {
        if (sendEventOnFailure)
        {
            if (string.IsNullOrEmpty(GetResourceFileName(name)))
                SendResourceNotFound(name, FileIdentifier.Empty);
            else
            {
                var eventData = new ResourceEvents.LoadFailed { ResourceName = name };
                SendEvent(ref eventData);
            }
        }

        throw new InvalidOperationException($"Resource '{name}' of type {typeof(T).Name} was not found.");
    }

    private void SendResourceNotFound(string requestedName, FileIdentifier resolvedName)
    {
        var eventData = new ResourceEvents.ResourceNotFound
        {
            ResourceName = resolvedName.IsEmpty() ? requestedName : resolvedName.ToUri()
        };
        SendEvent(ref eventData);
    }

    private void HandleBeginFrame()
    {
        _backgroundLoader.FinishResources(_finishBackgroundResourcesMs);
    }

    private void HandleFileChanged(ref ResourceEvents.FileChanged eventData)
    {
        var resourceName = eventData.ResourceName;
        if (string.IsNullOrWhiteSpace(resourceName))
            return;

        if (_ignoredReloads.Remove(SanitateResourceName(resourceName)))
            return;

        if (_resourceReloadSuspended)
            _pendingResourceReloads.Add(resourceName);
        else
            ReloadResourceWithDependencies(resourceName);
    }
}
