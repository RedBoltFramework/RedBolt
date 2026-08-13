namespace RedBolt.RenderAPI;

using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;

using RedBolt.Bindings.Diligent;
using RedBolt.Container;
using RedBolt.Logging;

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
            _ = IObject_Release(_handle);
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
        using var createInfo = new PipelineStateCacheCreateInfo
        {
            DescDeviceObjectAttribsName = name.Pointer,
            CacheDataSize = (uint)_cachedData.Length
        };
        IPipelineStateCache? newHandle;
        if (_cachedData.Length != 0)
        {
            var pinnedData = GCHandle.Alloc(_cachedData, GCHandleType.Pinned);
            try
            {
                createInfo.PCacheData = pinnedData.AddrOfPinnedObject();
                IRenderDevice_CreatePipelineStateCache(_renderDevice.GetRenderDevice()!, createInfo,
                    out newHandle!);
            }
            finally
            {
                pinnedData.Free();
            }
        }
        else
            IRenderDevice_CreatePipelineStateCache(_renderDevice.GetRenderDevice()!, createInfo,
                out newHandle!);

        if (newHandle is { IsNull: false })
        {
            if (_handle is { IsNull: false })
                _ = IObject_Release(_handle);
            _handle = newHandle;
        }
        else
            _handle = null;

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

        IPipelineStateCache_GetData(_handle, out var blob);
        if (blob is null || blob.IsNull)
            return;

        using (blob)
        {
            var size = checked((int)IDataBlob_GetSize(blob));
            _cachedData = new byte[size];
            if (size != 0)
                System.Runtime.InteropServices.Marshal.Copy(IDataBlob_GetDataPtr(blob, 0),
                    _cachedData, 0, size);
        }
    }
}
