using Microsoft.Extensions.Logging;
using RedBolt.Bindings.Diligent;
using System.Runtime.InteropServices;

namespace RedBolt;

/// <summary>
/// Generic pipeline state cache.
/// </summary>
public sealed class PipelineStateCache : DeviceObject
{
    private byte[] _cachedData = [];
    private readonly Dictionary<PipelineStateDesc, WeakReference<PipelineState>> _states = [];
    private IPipelineStateCache? _handle;

    public override void Dispose()
    {
        if (_handle is { IsNull: false })
            _ = _handle.Release();
        _handle = null;
        base.Dispose();
    }

    /// <summary>
    /// Initialize pipeline state cache. Optionally loads cached pipeline states from memory blob.
    /// </summary>
    public void Initialize(byte[] data)
    {
        _cachedData = [.. data];
        if (_handle is { IsNull: false })
            _ = _handle.Release();
        _handle = null;

        var renderDevice = GetRenderDevice();
        if (renderDevice is null || renderDevice.GetBackend() is not (RenderBackend.D3D12 or RenderBackend.Vulkan))
            return;

        using var name = new CString("PipelineStateCache");
        using var cacheDesc = new PipelineStateCacheDesc { Name = name.Pointer, Mode = PsoCacheMode.PsoCacheModeLoadStore };
        using var createInfo = new PipelineStateCacheCreateInfo
        {
            Desc = cacheDesc,
            CacheDataSize = checked((uint)_cachedData.Length)
        };
        if (_cachedData.Length != 0)
        {
            var pinnedData = GCHandle.Alloc(_cachedData, GCHandleType.Pinned);
            try
            {
                createInfo.PCacheData = pinnedData.AddrOfPinnedObject();
                renderDevice.GetRenderDevice()?.CreatePipelineStateCache(createInfo, out _handle!);
            }
            finally
            {
                pinnedData.Free();
            }
        }
        else
            renderDevice.GetRenderDevice()?.CreatePipelineStateCache(createInfo, out _handle!);
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

    public IPipelineStateCache? GetHandle() => _handle;

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
        if (_handle is null || _handle.IsNull)
        {
            _cachedData = [];
            return;
        }

        _handle.GetData(out var blob);
        using (blob)
        {
            var size = checked((int)blob.GetSize());
            _cachedData = new byte[size];
            if (size != 0)
                System.Runtime.InteropServices.Marshal.Copy(blob.GetDataPtr(), _cachedData, 0, size);
        }
    }
}
