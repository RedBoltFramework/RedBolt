namespace RedBolt;

public sealed class PipelineStateCache : DeviceObject
{
    private byte[] cachedData = [];
    private readonly Dictionary<PipelineStateDesc, WeakReference<PipelineState>> states = [];

    public void Initialize(byte[]? data = null)
    {
        cachedData = data is null || data.Length == 0 ? [] : [.. data];
    }

    public byte[] GetCachedData() => [.. cachedData];

    public PipelineState? GetPipelineState(PipelineStateDesc desc)
    {
        ArgumentNullException.ThrowIfNull(desc);

        if (states.TryGetValue(desc, out var weakPipelineState) && weakPipelineState.TryGetTarget(out var pipelineState))
            return pipelineState;

        pipelineState = new PipelineState(this, desc);
        states[desc] = new WeakReference<PipelineState>(pipelineState);
        return pipelineState;
    }

    public PipelineState? GetGraphicsPipelineState(GraphicsPipelineStateDesc desc)
    {
        ArgumentNullException.ThrowIfNull(desc);
        return desc.IsInitialized() ? GetPipelineState(new PipelineStateDesc(desc)) : null;
    }

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
