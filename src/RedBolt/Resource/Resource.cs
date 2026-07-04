namespace RedBolt;

public abstract class Resource
{
    private readonly System.Diagnostics.Stopwatch useTimer = System.Diagnostics.Stopwatch.StartNew();

    protected Resource(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
    }

    public string Name { get; }

    public string AbsoluteFileName { get; set; } = string.Empty;

    public AsyncLoadState AsyncLoadState { get; internal set; } = AsyncLoadState.Done;

    public uint MemoryUse { get; set; }

    public uint NameHash => StringHash.Calculate(Name);

    public virtual bool BeginLoad(Stream source) => false;

    public virtual bool EndLoad() => true;

    public virtual bool Save(Stream destination) => false;

    public uint GetUseTimer()
    {
        return (uint)Math.Min(uint.MaxValue, useTimer.ElapsedMilliseconds);
    }

    public void ResetUseTimer()
    {
        useTimer.Restart();
    }
}
