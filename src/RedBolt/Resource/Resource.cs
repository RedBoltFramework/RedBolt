using Microsoft.Extensions.Logging;

namespace RedBolt;

/// <summary>Base class for resources.</summary>
public class Resource : Object
{
    /// <summary>Construct.</summary>
    public Resource()
    {
    }

    public Resource(string name)
        : this()
    {
        SetName(name);
    }

    /// <summary>Load resource by reference.</summary>
    public static Resource? LoadFromCache(Context context, StringHash type, string name)
    {
        if (string.IsNullOrEmpty(name))
            return null;

        var cache = context.GetSubsystem<ResourceCache>();
        return cache!.GetResource(type, name);
    }

    /// <summary>Load resource synchronously. Call both BeginLoad() and EndLoad() and return true if both succeeded.</summary>
    public bool Load(Stream source)
    {
        AsyncLoadState = Environment.CurrentManagedThreadId == WorkQueue.GetMainThreadId()
            ? AsyncLoadState.Done
            : AsyncLoadState.Loading;
        var success = BeginLoad(source);
        if (success)
            success &= EndLoad();
        AsyncLoadState = AsyncLoadState.Done;
        return success;
    }

    /// <summary>Load resource from stream. May be called from a worker thread. Return true if successful.</summary>
    public virtual bool BeginLoad(Stream source) => false;

    /// <summary>Finish resource loading. Always called from the main thread. Return true if successful.</summary>
    public virtual bool EndLoad() => true;

    /// <summary>Save resource. Return true if successful.</summary>
    public virtual bool Save(Stream destination)
    {
        Log.GetLogger().LogError("Save not supported for " + GetTypeName());
        return false;
    }

    /// <summary>Load resource from file.</summary>
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
        var file = activeFileSystem.OpenFile(identifier, FileMode.Read);
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

    /// <summary>Save resource to file.</summary>
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
        var file = activeFileSystem.OpenFile(identifier, FileMode.Write);
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

    /// <summary>Set name.</summary>
    public void SetName(string name)
    {
        _name = name;
        _nameHash = name;
    }

    /// <summary>Set memory use in bytes, possibly approximate.</summary>
    public void SetMemoryUse(int size)
    {
        _memoryUse = size;
    }

    /// <summary>Reset last used timer.</summary>
    public void ResetUseTimer()
    {
        _useTimer.Restart();
    }

    /// <summary>Set the asynchronous loading state. Called by ResourceCache. Resources in the middle of asynchronous loading are not normally returned to user.</summary>
    public void SetAsyncLoadState(AsyncLoadState newState)
    {
        _asyncLoadState = newState;
    }

    /// <summary>Set absolute file name.</summary>
    public void SetAbsoluteFileName(string fileName)
    {
        _absoluteFileName = fileName;
    }

    /// <summary>Return name.</summary>
    public string GetName() => _name;

    /// <summary>Return name hash.</summary>
    public StringHash GetNameHash() => _nameHash;

    /// <summary>Return memory use in bytes, possibly approximate.</summary>
    public int GetMemoryUse() => _memoryUse;

    /// <summary>Return time since last use in milliseconds.</summary>
    public uint GetUseTimer()
    {
        return unchecked((uint)_useTimer.ElapsedMilliseconds);
    }

    /// <summary>Return the asynchronous loading state.</summary>
    public AsyncLoadState GetAsyncLoadState() => _asyncLoadState;

    /// <summary>Return absolute file name.</summary>
    public string GetAbsoluteFileName() => _absoluteFileName;

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

    public string Name
    {
        get => _name;
        private set => SetName(value);
    }

    public StringHash NameHash => _nameHash;

    public string AbsoluteFileName
    {
        get => _absoluteFileName;
        set => SetAbsoluteFileName(value);
    }

    public int MemoryUse
    {
        get => _memoryUse;
        set => SetMemoryUse(value);
    }

    public AsyncLoadState AsyncLoadState
    {
        get => _asyncLoadState;
        internal set => SetAsyncLoadState(value);
    }

    internal ResourceCache? OwnerCache { get; set; }

    /// <summary>Name.</summary>
    private string _name = string.Empty;
    /// <summary>Name hash.</summary>
    private StringHash _nameHash = new();
    /// <summary>Absolute file name.</summary>
    private string _absoluteFileName = string.Empty;
    /// <summary>Last used timer.</summary>
    private readonly System.Diagnostics.Stopwatch _useTimer = System.Diagnostics.Stopwatch.StartNew();
    /// <summary>Memory use in bytes.</summary>
    private int _memoryUse;
    /// <summary>Asynchronous loading state.</summary>
    private AsyncLoadState _asyncLoadState = AsyncLoadState.Done;
}
