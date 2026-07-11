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
        using var stream = System.IO.File.OpenRead(fileName);
        AbsoluteFileName = fileName;
        return Load(stream);
    }

    public bool Reload()
    {
        return !string.IsNullOrWhiteSpace(AbsoluteFileName) && LoadFile(AbsoluteFileName);
    }

    public virtual bool SaveFile(string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        using var stream = System.IO.File.Create(fileName);
        return Save(stream);
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
