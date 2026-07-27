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

public sealed class ResourceCache : RedObject
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
    private readonly Dictionary<StringHash, HashSet<StringHash>> _dependentResources = new();
    private readonly HashSet<string> _ignoredReloads = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _pendingResourceReloads = new(StringComparer.OrdinalIgnoreCase);
    private int _finishBackgroundResourcesMs = 5;
    private bool _returnFailedResources;
    private bool _searchPackagesFirst = true;
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
        RegisterLoader<Shader>(name => LoadFileBackedResource(Shader.GetResourceName(name), static () => new Shader()));

        RegisterLoader<Model>(name => LoadFileBackedResource(name, static () => new Model()));

        RegisterLoader<Material>(name => LoadFileBackedResource(name, AppendExtensionIfMissing(name, ".xml"),
            static () => new Material()));
        RegisterLoader<RenderPath>(name => LoadFileBackedResource(
            name,
            GetRenderPathLookupNames(name),
            static () => new RenderPath()));
        RegisterLoader<Sound>(name => LoadFileBackedResource(
            name,
            static () => new Sound()));
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
        return _resourceGroups.TryGetValue(type, out var group) ? group.MemoryUse : 0;
    }

    public ulong GetMemoryUse(StringHash type)
    {
        return ResolveResourceType(type) is { } resourceType
            ? GetMemoryUse(resourceType)
            : 0;
    }

    public ulong GetTotalMemoryUse()
    {
        ulong total = 0;
        foreach (var group in _resourceGroups.Values)
            total += group.MemoryUse;
        return total;
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
        // Check for duplicate
        for (var i = 0; i < _resourceRouters.Count; ++i)
        {
            if (ReferenceEquals(_resourceRouters[i], router))
                return;
        }

        if (addAsFirst)
            _resourceRouters.Insert(0, router);
        else
            _resourceRouters.Add(router);
    }

    public void RemoveResourceRouter(ResourceRouter router)
    {
        for (var i = 0; i < _resourceRouters.Count; ++i)
        {
            if (ReferenceEquals(_resourceRouters[i], router))
            {
                _resourceRouters.RemoveAt(i);
                return;
            }
        }
    }

    public ResourceRouter? GetResourceRouter(int index) => index >= 0 && index < _resourceRouters.Count ? _resourceRouters[index] : null;

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

        var sanitatedName = SanitateResourceName(name);
        if (string.IsNullOrEmpty(sanitatedName))
            return false;
        if (_cache.ContainsKey((type, sanitatedName)))
            return false;

        return _backgroundLoader.QueueResource(type, sanitatedName, sendEventOnFailure, caller);
    }

    public bool BackgroundLoadResource(StringHash type, string name, bool sendEventOnFailure = true, Resource? caller = null)
    {
        var resourceType = ResolveResourceType(type);
        return resourceType is not null && BackgroundLoadResource(resourceType, name, sendEventOnFailure, caller);
    }

    public int GetNumBackgroundLoadResources() => _backgroundLoader.GetNumQueuedResources();

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
        var resolvedName = GetResolvedIdentifier(FileIdentifier.FromUri(name));
        if (resolvedName.IsEmpty())
            return false;

        return GetVirtualFileSystem().Exists(resolvedName);
    }

    public AbstractFile? GetFile(string name, bool sendEventOnFailure = true)
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

        return file;
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

    public void ReleaseResource<T>(string name, bool force = false)
        where T : Resource
    {
        ReleaseResource(new StringHash(typeof(T).GetClassName()), name, force);
    }

    public void ReleaseResource(StringHash type, string name, bool force = false)
    {
        var resourceType = ResolveResourceType(type);
        if (resourceType is null)
            return;

        var nameHash = new StringHash(name);
        var existingResource = FindSpecificResource(resourceType, nameHash);
        if (existingResource is null)
            return;

        RemoveCachedResource((resourceType, existingResource.Name));
        UpdateResourceGroup(resourceType);
    }

    public void ReleaseResource(string resourceName, bool force = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceName);
        var nameHash = new StringHash(resourceName);
        bool released;
        do
        {
            released = false;

            foreach (var (type, group) in _resourceGroups)
            {
                if (group.Resources.TryGetValue(nameHash, out var resource))
                {
                    RemoveCachedResource((type, resource.Name));
                    released = true;
                }
                if (released)
                    UpdateResourceGroup(type);
            }
        } while (released && !force);
    }

    public void ReleaseResources<T>(bool force = false)
        where T : Resource
    {
        ReleaseResources(new StringHash(typeof(T).GetClassName()), force);
    }

    public void ReleaseResources(StringHash type, bool force = false)
    {
        var resourceType = ResolveResourceType(type);
        if (resourceType is null)
            return;

        var released = false;
        if (_resourceGroups.TryGetValue(resourceType, out var group))
        {
            foreach (var resource in group.Resources.Values)
            {
                RemoveCachedResource((resourceType, resource.Name));
                released = true;
            }
        }

        if (released)
            UpdateResourceGroup(resourceType);
    }

    public void ReleaseResources(StringHash type, string partialName, bool force = false)
    {
        var resourceType = ResolveResourceType(type);
        if (resourceType is null)
            return;

        ArgumentNullException.ThrowIfNull(partialName);
        var released = false;
        if (_resourceGroups.TryGetValue(resourceType, out var group))
        {
            foreach (var resource in group.Resources.Values)
            {
                if (!resource.Name.Contains(partialName, StringComparison.Ordinal))
                    continue;

                RemoveCachedResource((resourceType, resource.Name));
                released = true;
            }
        }

        if (released)
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

    public bool ReloadResource(Resource? resource)
    {
        if (resource is null)
            return false;

        resource.SendEvent<ResourceEvents.ReloadStarted>();

        var success = false;
        using (var file = GetFile(resource.Name))
        {
            if (file is not null)
                success = resource.Load(file);
        }

        if (success)
        {
            resource.ResetUseTimer();
            UpdateResourceGroup(resource.GetType());
            resource.SendEvent<ResourceEvents.ReloadFinished>();
            return true;
        }

        // If reloading failed, do not remove the resource from cache, to allow for a new live edit to
        // attempt loading again
        resource.SendEvent<ResourceEvents.ReloadFailed>();
        return false;
    }

    public void StoreResourceDependency(Resource resource, string dependency)
    {
        var nameHash = new StringHash(resource.Name);
        var dependencyHash = new StringHash(dependency);
        if (!_dependentResources.TryGetValue(dependencyHash, out var dependents))
            _dependentResources[dependencyHash] = dependents = [];
        dependents.Add(nameHash);
    }

    public void ResetDependencies(Resource resource)
    {
        var nameHash = new StringHash(resource.Name);
        List<StringHash> emptyDependencies = [];
        foreach (var pair in _dependentResources)
        {
            pair.Value.Remove(nameHash);
            if (pair.Value.Count == 0)
                emptyDependencies.Add(pair.Key);
        }

        foreach (var dependency in emptyDependencies)
            _dependentResources.Remove(dependency);
    }

    public void ReloadResourceWithDependencies(string fileName)
    {
        var fileNameHash = new StringHash(fileName);

        List<Resource> resources = [];
        FindMatchingResources(fileNameHash, resources);
        foreach (var resource in resources)
            ReloadResource(resource);

        if (!_dependentResources.TryGetValue(fileNameHash, out var dependents))
            return;

        resources.Clear();
        foreach (var dependentResourceNameHash in dependents)
            FindMatchingResources(dependentResourceNameHash, resources);

        foreach (var resource in resources)
            ReloadResource(resource);
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
        ReleaseResources(new StringHash(typeof(T).GetClassName()), partialName, force);
    }

    public void ReleaseResources(string partialName, bool force = false)
    {
        ArgumentNullException.ThrowIfNull(partialName);
        bool released;
        do
        {
            released = false;

            foreach (var (type, group) in _resourceGroups)
            {
                foreach (var resource in group.Resources.Values)
                {
                    if (!resource.Name.Contains(partialName, StringComparison.Ordinal))
                        continue;

                    RemoveCachedResource((type, resource.Name));
                    released = true;
                }
                if (released)
                    UpdateResourceGroup(type);
            }
        } while (released && !force);
    }

    public void ReleaseAllResources(bool force = false)
    {
        bool released;
        do
        {
            released = false;

            foreach (var (type, group) in _resourceGroups)
            {
                foreach (var resource in group.Resources.Values)
                {
                    RemoveCachedResource((type, resource.Name));
                    released = true;
                }
                if (released)
                    UpdateResourceGroup(type);
            }
        } while (released && !force);
    }

    public void ReleasePackageResources(PackageFile package, bool force = false)
    {
        ArgumentNullException.ThrowIfNull(package);
        var affectedTypes = new HashSet<Type>();
        foreach (var entry in package.GetEntries())
        {
            var nameHash = new StringHash(entry.Key);

            foreach (var (type, group) in _resourceGroups)
            {
                if (group.Resources.TryGetValue(nameHash, out var resource))
                {
                    RemoveCachedResource((type, resource.Name));
                    affectedTypes.Add(type);
                    break;
                }
            }
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

    private static Type? ResolveResourceType(StringHash type) =>
        Context.Instance.GetReflection(type)?.ManagedType;

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
                memoryUse += checked((ulong)resource.MemoryUse);
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

        resource.SetName(sanitizedName);
        _cache[key] = resource;
        AddResourceToGroup(type, resource);
        UpdateResourceGroup(type);
        return resource;
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

        using var file = GetFile(name);
        if (file is null)
            return null;

        var resource = Context.Instance.CreateObject(new StringHash(type.GetClassName())) as Resource;
        if (resource is null)
            return null;

        resource.SetName(name);
        resource.SetAbsoluteFileName(file.AbsoluteName);
        return resource.Load(file) || _returnFailedResources ? resource : null;
    }

    private T? LoadFileBackedResource<T>(string name, Func<T> factory)
        where T : Resource
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(factory);

        using var file = GetFile(name);
        if (file is null)
            return null;

        var resource = factory();
        resource.SetName(name);
        resource.SetAbsoluteFileName(file.AbsoluteName);
        return resource.Load(file) || _returnFailedResources ? resource : null;
    }

    private T? LoadFileBackedResource<T>(string logicalName, string lookupName, Func<T> factory)
        where T : Resource
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(logicalName);
        ArgumentException.ThrowIfNullOrWhiteSpace(lookupName);
        ArgumentNullException.ThrowIfNull(factory);

        using var file = GetFile(lookupName);
        if (file is null)
            return null;

        var resource = factory();
        resource.SetName(logicalName);
        resource.SetAbsoluteFileName(file.AbsoluteName);
        return resource.Load(file) || _returnFailedResources ? resource : null;
    }

    private T? LoadFileBackedResource<T>(string logicalName, IReadOnlyList<string> lookupNames, Func<T> factory)
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
