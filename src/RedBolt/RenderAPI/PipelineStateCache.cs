namespace RedBolt;

/// <summary>
/// Generic pipeline state cache.
/// </summary>
public sealed class PipelineStateCache : DeviceObject
{
    private byte[] cachedData = [];
    private readonly Dictionary<PipelineStateDesc, WeakReference<PipelineState>> states = [];

    /// <summary>
    /// Initialize pipeline state cache. Optionally loads cached pipeline states from memory blob.
    /// </summary>
    public void Initialize(byte[]? data = null)
    {
        cachedData = data is null || data.Length == 0 ? [] : [.. data];
    }

    /// <summary>
    /// Stores cached pipeline states to memory blob.
    /// </summary>
    public byte[] GetCachedData() => [.. cachedData];

    /// <summary>
    /// Create new or return existing pipeline state. Returned state may be invalid.
    /// Return null if description is malformed.
    /// </summary>
    public PipelineState? GetPipelineState(PipelineStateDesc desc)
    {
        ArgumentNullException.ThrowIfNull(desc);

        if (states.TryGetValue(desc, out var weakPipelineState) && weakPipelineState.TryGetTarget(out var pipelineState))
            return pipelineState;

        pipelineState = new PipelineState(this, desc);
        states[desc] = new WeakReference<PipelineState>(pipelineState);
        return pipelineState;
    }

    /// <summary>
    /// Create new or return existing graphics pipeline state.
    /// </summary>
    public PipelineState? GetGraphicsPipelineState(GraphicsPipelineStateDesc desc)
    {
        ArgumentNullException.ThrowIfNull(desc);
        return desc.IsInitialized() ? GetPipelineState(new PipelineStateDesc(desc)) : null;
    }

    /// <summary>
    /// Create new or return existing compute pipeline state.
    /// </summary>
    public PipelineState? GetComputePipelineState(ComputePipelineStateDesc desc)
    {
        ArgumentNullException.ThrowIfNull(desc);
        return desc.IsInitialized() ? GetPipelineState(new PipelineStateDesc(desc)) : null;
    }

    public override void Invalidate()
    {
        base.Invalidate();
        foreach (var pipelineState in GetLiveStates())
            pipelineState.Invalidate();
    }

    public override void Restore()
    {
        base.Restore();
        foreach (var pipelineState in GetLiveStates())
            pipelineState.Restore();
    }

    public override void Destroy()
    {
        base.Destroy();
        states.Clear();
    }

    /// <summary>
    /// Internal. Remove pipeline state with given description from cache.
    /// </summary>
    internal void ReleasePipelineState(PipelineStateDesc desc)
    {
        ArgumentNullException.ThrowIfNull(desc);
        states.Remove(desc);
    }

    private IEnumerable<PipelineState> GetLiveStates()
    {
        foreach (var weakPipelineState in states.Values)
        {
            if (weakPipelineState.TryGetTarget(out var pipelineState))
                yield return pipelineState;
        }
    }
}
