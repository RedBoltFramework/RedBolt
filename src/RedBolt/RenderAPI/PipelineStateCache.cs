using Microsoft.Extensions.Logging;

namespace RedBolt;

/// <summary>
/// Generic pipeline state cache.
/// </summary>
public sealed class PipelineStateCache : DeviceObject
{
    private byte[] _cachedData = [];
    private readonly Dictionary<PipelineStateDesc, WeakReference<PipelineState>> _states = [];

    /// <summary>
    /// Initialize pipeline state cache. Optionally loads cached pipeline states from memory blob.
    /// </summary>
    public void Initialize(byte[] data)
    {
        _cachedData = [.. data];

        // TODO: port the backend-specific Diligent pipeline state cache creation:
        // const RenderBackend renderBackend = renderDevice_->GetBackend();
        // const bool isSupported = renderBackend == RenderBackend::D3D12 || renderBackend == RenderBackend::Vulkan;
        // Diligent::PipelineStateCacheCreateInfo ci;
        // ci.Desc.Name = "PipelineStateCache";
        // ci.CacheDataSize = cachedData_.size();
        // ci.pCacheData = cachedData_.data();
        // renderDevice_->GetRenderDevice()->CreatePipelineStateCache(ci, &handle_);
    }

    /// <summary>
    /// Stores cached pipeline states to memory blob.
    /// </summary>
    public byte[] GetCachedData()
    {
        UpdateCachedData();
        return [.. _cachedData];
    }

    /// <summary>
    /// Create new or return existing pipeline state. Returned state may be invalid.
    /// Return null if description is malformed.
    /// </summary>
    public PipelineState? GetPipelineState(PipelineStateDesc desc)
    {
        if (_states.TryGetValue(desc, out var weakPipelineState) && weakPipelineState.TryGetTarget(out var pipelineState))
            return pipelineState;

        pipelineState = new PipelineState(this, desc);
        _states[desc] = new WeakReference<PipelineState>(pipelineState);
        return pipelineState;
    }

    /// <summary>
    /// Create new or return existing graphics pipeline state.
    /// </summary>
    public PipelineState? GetGraphicsPipelineState(GraphicsPipelineStateDesc desc)
    {
        return desc.IsInitialized() ? GetPipelineState(new PipelineStateDesc(desc)) : null;
    }

    /// <summary>
    /// Create new or return existing compute pipeline state.
    /// </summary>
    public PipelineState? GetComputePipelineState(ComputePipelineStateDesc desc)
    {
        return desc.IsInitialized() ? GetPipelineState(new PipelineStateDesc(desc)) : null;
    }

    // TODO: port: Diligent::IPipelineStateCache* GetHandle() { return handle_; }

    /// <summary>
    /// Internal. Remove pipeline state with given description from cache.
    /// </summary>
    public void ReleasePipelineState(PipelineStateDesc desc)
    {
        if (!_states.Remove(desc))
            Log.Logger.LogError("Unexpected call of PipelineStateCache::ReleasePipelineState");
    }

    private void UpdateCachedData()
    {
        _cachedData = [];
    }
}
