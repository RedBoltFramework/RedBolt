namespace RedBolt;

public abstract class Resource : Object
{
    private readonly System.Diagnostics.Stopwatch _useTimer = System.Diagnostics.Stopwatch.StartNew();

    protected Resource(string name) : base()
    {
        SetName(name);
    }

    public string Name { get; private set; } = string.Empty;

    public string AbsoluteFileName { get; set; } = string.Empty;

    internal ResourceCache? OwnerCache { get; set; }

    public AsyncLoadState AsyncLoadState { get; internal set; } = AsyncLoadState.Done;

    public uint MemoryUse { get; set; }

    public event Action<Resource>? ReloadFinished;

    public uint NameHash => StringHash.Calculate(Name);

    public bool Load(Stream source)
    {
        ArgumentNullException.ThrowIfNull(source);

        AsyncLoadState = Environment.CurrentManagedThreadId == WorkQueue.GetMainThreadId()
            ? AsyncLoadState.Done
            : AsyncLoadState.Loading;
        var success = BeginLoad(source);
        if (success)
            success &= EndLoad();
        AsyncLoadState = AsyncLoadState.Done;
        return success;
    }

    internal void NotifyReloadFinished()
    {
        ReloadFinished?.Invoke(this);
    }

    public virtual bool BeginLoad(Stream source) => false;

    public virtual bool EndLoad() => true;

    public virtual bool Save(Stream destination) => false;

    public bool LoadFile(string fileName)
    {
        // C++: delegates directly to VFS: vfs->OpenFile(fileName, FILE_READ) then Load(*file)
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        var virtualFileSystem = GetSubsystem<VirtualFileSystem>();
        using var temporaryFileSystem = virtualFileSystem is null ? new VirtualFileSystem() : null;
        if (temporaryFileSystem is not null)
        {
            temporaryFileSystem.MountRoot();
            if (OwnerCache is not null)
            {
                foreach (var resourceDir in OwnerCache.ResourceDirs)
                    temporaryFileSystem.MountDir(resourceDir);
            }

            virtualFileSystem = temporaryFileSystem;
        }

        var activeFileSystem = virtualFileSystem!;

        var identifier = new FileIdentifier(fileName);
        using var rootMount = temporaryFileSystem is null && identifier.Scheme == "file"
            ? new MountPointGuard(activeFileSystem.MountRoot())
            : null;
        var file = activeFileSystem.OpenFile(identifier, File.FileMode.Read);
        if (file is null)
            return false;

        try
        {
            var data = new byte[checked((int)file.Size)];
            var offset = 0;
            while (offset < data.Length)
            {
                var read = file.Read(data.AsSpan(offset));
                if (read == 0)
                    return false;
                offset += checked((int)read);
            }

            AbsoluteFileName = file.AbsoluteName;
            using var stream = new MemoryStream(data, writable: false);
            return Load(stream);
        }
        finally
        {
            file.Close();
        }
    }

    public bool Reload()
    {
        return !string.IsNullOrWhiteSpace(AbsoluteFileName) && LoadFile(AbsoluteFileName);
    }

    public virtual bool SaveFile(string fileName)
    {
        // C++: delegates directly to VFS: vfs->OpenFile(fileName, FILE_WRITE) then Save(*file)
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        using var stream = new MemoryStream();
        if (!Save(stream))
            return false;

        var virtualFileSystem = GetSubsystem<VirtualFileSystem>();
        using var temporaryFileSystem = virtualFileSystem is null ? new VirtualFileSystem() : null;
        if (temporaryFileSystem is not null)
        {
            temporaryFileSystem.MountRoot();
            if (OwnerCache is not null)
            {
                foreach (var resourceDir in OwnerCache.ResourceDirs)
                    temporaryFileSystem.MountDir(resourceDir);
            }

            virtualFileSystem = temporaryFileSystem;
        }

        var activeFileSystem = virtualFileSystem!;

        var identifier = new FileIdentifier(fileName);
        using var rootMount = temporaryFileSystem is null && identifier.Scheme == "file"
            ? new MountPointGuard(activeFileSystem.MountRoot())
            : null;
        var file = activeFileSystem.OpenFile(identifier, File.FileMode.Write);
        if (file is null)
            return false;

        try
        {
            var data = stream.ToArray();
            return file.Write(data) == data.Length;
        }
        finally
        {
            file.Close();
        }
    }

    public void SetName(string name)
    {
        // C++: name_ = name; nameHash_ = name;
        Name = name ?? string.Empty;
    }

    public string GetName() => Name;

    public StringHash GetNameHash() => Name;

    public void SetMemoryUse(uint size)
    {
        MemoryUse = size;
    }

    public uint GetMemoryUse() => MemoryUse;

    public uint GetUseTimer()
    {
        // C++: if (Refs() > 1) { useTimer_.Reset(); return 0; } else return useTimer_.GetMSec(false);
        return (uint)Math.Min(uint.MaxValue, _useTimer.ElapsedMilliseconds);
    }

    public void ResetUseTimer()
    {
        _useTimer.Restart();
    }

    public void SetAsyncLoadState(AsyncLoadState newState)
    {
        AsyncLoadState = newState;
    }

    public AsyncLoadState GetAsyncLoadState() => AsyncLoadState;

    public static Resource? LoadFromCache(Context context, StringHash type, string name)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (string.IsNullOrEmpty(name))
            return null;

        return context.GetSubsystem<ResourceCache>()?.GetResource(type, name);
    }

    public void SetAbsoluteFileName(string fileName)
    {
        AbsoluteFileName = fileName ?? string.Empty;
    }

    public string GetAbsoluteFileName() => AbsoluteFileName;

    public static string GetResourceName(Resource? resource)
    {
        return resource?.GetName() ?? string.Empty;
    }

    public static StringHash GetResourceType(Resource? resource, StringHash defaultType)
    {
        return resource?.GetTypeNameHash() ?? defaultType;
    }

    public static ResourceRef GetResourceRef(Resource? resource, StringHash defaultType)
    {
        return new ResourceRef(GetResourceType(resource, defaultType), GetResourceName(resource));
    }

    public static IReadOnlyList<string> GetResourceNames<T>(IReadOnlyList<T?> resources)
        where T : Resource
    {
        var result = new string[resources.Count];
        for (var i = 0; i < resources.Count; ++i)
            result[i] = GetResourceName(resources[i]);

        return result;
    }

    public static ResourceRefList GetResourceRefList<T>(IReadOnlyList<T?> resources)
        where T : Resource
    {
        return new ResourceRefList(typeof(T).GetTypeId(), GetResourceNames(resources));
    }
}
