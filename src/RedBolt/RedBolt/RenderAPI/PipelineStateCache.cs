using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;

using RedBolt.Bindings.Diligent;
using RedBolt.Container;
using RedBolt.Logging;

namespace RedBolt.RenderAPI;

/// <summary>
/// Generic pipeline state cache.
/// </summary>
public class PipelineStateCache : DeviceObject
{
    private byte[] _cachedData = [];
    private IPipelineStateCache? _handle;
    private readonly Dictionary<PipelineStateDesc, WeakReference<PipelineState>> _states = [];

    public PipelineStateCache()
    {
    }

    public override void Dispose()
    {
        foreach (var weakPipelineState in _states.Values)
        {
            if (weakPipelineState.TryGetTarget(out var pipelineState))
                pipelineState.ReleaseOwner(this);
        }
        _states.Clear();

        if (_handle is { IsNull: false })
            _ = _handle.Release();
        _handle = null;
        _cachedData = [];
        base.Dispose();
    }

    /// <summary>
    /// Initialize pipeline state cache. Optionally loads cached pipeline states from memory blob.
    /// </summary>
    public void Initialize(byte[] cachedData)
    {
        _cachedData = [.. cachedData];

        var renderBackend = _renderDevice!.GetBackend();
        var isSupported = renderBackend is RenderBackend.D3D12 or RenderBackend.Vulkan;
        if (!isSupported)
            return;

        using var name = new CString("PipelineStateCache");
        using var cacheDesc = new PipelineStateCacheDesc { Name = name.Pointer };
        using var createInfo = new PipelineStateCacheCreateInfo
        {
            Desc = cacheDesc,
            CacheDataSize = (uint)_cachedData.Length
        };
        IPipelineStateCache? newHandle;
        if (_cachedData.Length != 0)
        {
            var pinnedData = GCHandle.Alloc(_cachedData, GCHandleType.Pinned);
            try
            {
                createInfo.PCacheData = pinnedData.AddrOfPinnedObject();
                _renderDevice.GetRenderDevice()!.CreatePipelineStateCache(createInfo, out newHandle!);
            }
            finally
            {
                pinnedData.Free();
            }
        }
        else
            _renderDevice.GetRenderDevice()!.CreatePipelineStateCache(createInfo, out newHandle!);

        if (newHandle is not null)
        {
            if (_handle is { IsNull: false })
                _ = _handle.Release();
            _handle = newHandle;
        }

        if (_handle is null || _handle.IsNull)
            Log.GetLogger().LogError("Failed to create GPU Pipeline State Cache.");
        else
            Log.GetLogger().LogDebug("GPU Pipeline State Cache has been created.");
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
    public PipelineState GetPipelineState(PipelineStateDesc desc)
    {
        if (_states.TryGetValue(desc, out var weakPipelineState) && weakPipelineState.TryGetTarget(out var pipelineState))
            return pipelineState;

        pipelineState = new PipelineState(this, desc);
        _states[desc] = new WeakReference<PipelineState>(pipelineState);
        return pipelineState;
    }

    public IPipelineStateCache? GetHandle() => _handle;

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

    /// <summary>
    /// Internal. Remove pipeline state with given description from cache.
    /// </summary>
    public void ReleasePipelineState(PipelineStateDesc desc)
    {
        if (!_states.Remove(desc))
            Log.GetLogger().LogError("Unexpected call of PipelineStateCache::ReleasePipelineState");
    }

    private void UpdateCachedData()
    {
        _cachedData = [];
        if (_handle is null || _handle.IsNull)
            return;

        _handle.GetData(out var blob);
        if (blob is null || blob.IsNull)
            return;

        using (blob)
        {
            var size = checked((int)blob.GetSize());
            _cachedData = new byte[size];
            if (size != 0)
                System.Runtime.InteropServices.Marshal.Copy(blob.GetDataPtr(), _cachedData, 0, size);
        }
    }
}
