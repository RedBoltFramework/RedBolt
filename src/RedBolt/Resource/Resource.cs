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
        if (success)
            ReloadFinished?.Invoke(this);
        return success;
    }

    public virtual bool BeginLoad(Stream source) => false;

    public virtual bool EndLoad() => true;

    public virtual bool Save(Stream destination) => false;

    public bool LoadFile(string fileName)
    {
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

    public void SetAbsoluteFileName(string fileName)
    {
        AbsoluteFileName = fileName ?? string.Empty;
    }

    public string GetAbsoluteFileName() => AbsoluteFileName;
}
