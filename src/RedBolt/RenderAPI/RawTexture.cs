using RedBolt.Bindings.Diligent;

namespace RedBolt;

public sealed class RawTextureHandles
{
    private readonly RawTexture _owner;

    internal RawTextureHandles(RawTexture owner) => _owner = owner;

    public ITexture? Texture => _owner.GetHandle();
    public ITexture? ResolvedTexture => _owner.GetResolvedHandle();
    public ITextureView? ShaderResourceView => _owner.GetShaderResourceView();
    public ITextureView? RenderTargetView => _owner.GetRenderTargetView();
    public ITextureView? DepthStencilView => _owner.GetDepthStencilView();
    public ITextureView? ReadOnlyDepthStencilView => _owner.GetReadOnlyDepthStencilView();
    public ITextureView? UnorderedAccessView => _owner.GetDefaultUnorderedAccessView();
    public ReadOnlySpan<ITextureView> RenderSurfaces => _owner.GetRenderSurfaces();
    public ReadOnlySpan<ITextureView> ReadOnlyRenderSurfaces => _owner.GetReadOnlyRenderSurfaces();
    public IReadOnlyDictionary<RawTextureUAVKey, ITextureView> UnorderedAccessViews => _owner.GetUnorderedAccessViews();
    public bool IsValid => Texture is { IsNull: false };
}

/// <summary>Common class for all GPU textures.
/// By default RawTexture loses data on device lost and does not attempt to recover it.</summary>
public class RawTexture : DeviceObject
{
    internal static bool IsCreatingManagedTexture { get; private set; }

    private RawTextureParams _parameters;
    private SamplerStateDesc _samplerStateDesc = new();
    private Texture? _texture;
    private ITexture? _handle;
    private ITexture? _resolvedHandle;
    private ITextureView? _shaderResourceView;
    private ITextureView? _renderTargetView;
    private ITextureView? _depthStencilView;
    private ITextureView? _readOnlyDepthStencilView;
    private ITextureView? _unorderedAccessView;
    private ITextureView[] _renderSurfaces = [];
    private ITextureView[] _readOnlyRenderSurfaces = [];
    private readonly List<ITextureView> _ownedUavs = [];
    private readonly Box _updateBox = new();
    private readonly TextureSubResData _updateSubresource = new();
    private readonly RawTextureHandles _handles;
    private readonly Dictionary<RawTextureUAVKey, ITextureView> _uavs = [];
    private bool _levelsDirty;
    private bool _resolveDirty;

    protected RawTexture()
    {
        _handles = new RawTextureHandles(this);
    }

    public RawTexture(RawTextureParams parameters)
        : this()
    {
        Create(parameters);
    }

    /// <summary>Validate parameters and create GPU texture.</summary>
    protected bool Create(RawTextureParams parameters)
    {
        if (parameters == _parameters && IsValid())
            return true;
        if (!TryCreateManagedTexture(parameters, out var createdTexture, out var finalizedParameters))
        {
            _texture = null;
            this._parameters = default;
            return false;
        }

        _texture = createdTexture;
        this._parameters = finalizedParameters;
        SetDebugName($"RawTexture #{parameters.GetHashCode():X8}");
        DestroyGPU();
        if (!CreateGPU())
            return GetSubsystem<Graphics>()?.GetNativeRenderDevice() is null;
        ApplySamplerState();
        return true;
    }

    /// <summary>Set default sampler to be used for this texture.</summary>
    public void SetSamplerStateDesc(SamplerStateDesc desc)
    {
        _samplerStateDesc = desc ?? throw new ArgumentNullException(nameof(desc));
        ApplySamplerState();
    }

    public RawTextureParams GetParams() => _parameters;

    public SamplerStateDesc GetSamplerStateDesc() => _samplerStateDesc;

    public RawTextureHandles GetHandles() => _handles;

    public Texture? GetTexture() => _texture;

    public ITexture? GetHandle()
        => _handle;

    public ITexture? GetResolvedHandle() => _resolvedHandle;

    public ITextureView? GetShaderResourceView() => _shaderResourceView;

    public ITextureView? GetRenderTargetView() => _renderTargetView;

    public ITextureView? GetDepthStencilView() => _depthStencilView;

    public ITextureView? GetReadOnlyDepthStencilView() => _readOnlyDepthStencilView;

    public ITextureView? GetDefaultUnorderedAccessView() => _unorderedAccessView;

    public ReadOnlySpan<ITextureView> GetRenderSurfaces() => _renderSurfaces;

    public ReadOnlySpan<ITextureView> GetReadOnlyRenderSurfaces() => _readOnlyRenderSurfaces;

    internal IReadOnlyDictionary<RawTextureUAVKey, ITextureView> GetUnorderedAccessViews() => _uavs;

    public bool CreateFromHandle(ITexture texture, TextureFormat format = TextureFormat.Unknown, int msaaLevel = 1)
    {
        ArgumentNullException.ThrowIfNull(texture);
        if (texture.IsNull)
            return false;

        using var desc = texture.GetDesc();
        var type = desc.Type switch
        {
            ResourceDimension.ResourceDimTex2d => TextureType.Texture2D,
            ResourceDimension.ResourceDimTexCube => TextureType.TextureCube,
            ResourceDimension.ResourceDimTex2dArray => TextureType.Texture2DArray,
            ResourceDimension.ResourceDimTex3d => TextureType.Texture3D,
            _ => (TextureType?)null
        };
        if (type is null)
            return false;

        var resolvedFormat = format;
        if (resolvedFormat == TextureFormat.Unknown)
            resolvedFormat = Graphics.UnmapTextureFormat(desc.Format, out _);
        if (resolvedFormat == TextureFormat.Unknown)
            return false;

        var flags = TextureFlag.NoMultiSampledAutoResolve;
        if ((desc.BindFlags & BindFlags.BindRenderTarget) != 0)
            flags |= TextureFlag.BindRenderTarget;
        if ((desc.BindFlags & BindFlags.BindDepthStencil) != 0)
            flags |= TextureFlag.BindDepthStencil;
        if ((desc.BindFlags & BindFlags.BindUnorderedAccess) != 0)
            flags |= TextureFlag.BindUnorderedAccess;

        DestroyGPU();
        _parameters = new RawTextureParams(
            type.Value,
            resolvedFormat,
            flags,
            new Vector3i(checked((int)desc.Width), checked((int)desc.Height), checked((int)desc.Depth)),
            desc.ArraySize,
            desc.MipLevels,
            Math.Max(desc.SampleCount, checked((uint)Math.Max(msaaLevel, 1))),
            desc.MipLevels);
        _ = texture.AddRef();
        _handle = texture;
        if (!InitializeDefaultViews(desc.BindFlags))
        {
            DestroyGPU();
            return false;
        }
        OnCreateGPU();
        return true;
    }

    public bool CreateFromD3D11Texture2D(nint d3d11Texture2D, TextureFormat format, int msaaLevel) => false;

    public bool CreateFromD3D12Resource(nint d3d12Resource, TextureFormat format, int msaaLevel) => false;

    public bool CreateFromVulkanImage(ulong vkImage, RawTextureParams parameters) => false;

    public bool CreateFromGLTexture(uint handle, TextureType type, TextureFlag flags, TextureFormat format,
        uint arraySize, int msaaLevel)
    {
        var renderDevice = GetRenderDevice();
        var nativeDevice = GetSubsystem<Graphics>()?.GetNativeRenderDevice();
        if (renderDevice?.GetBackend() != RenderBackend.OpenGL || nativeDevice is null || nativeDevice.IsNull)
            return false;

        var glDevice = IRenderDeviceGL.CreateManagedWrapper(nativeDevice.Handle, ownsHandle: false);
        using var nativeName = new CString("Texture from external resource");
        using var desc = new TextureDesc
        {
            Name = nativeName.Pointer,
            Type = GetResourceDimension(type),
            Usage = Usage.UsageDefault,
            Format = Graphics.MapTextureFormat(format),
            ArraySize = type == TextureType.Texture2DArray ? arraySize : 1u,
            BindFlags = BindFlags.BindShaderResource
        };
        if ((flags & TextureFlag.BindRenderTarget) == TextureFlag.BindRenderTarget)
            desc.BindFlags |= BindFlags.BindRenderTarget;
        if ((flags & TextureFlag.BindDepthStencil) == TextureFlag.BindDepthStencil)
            desc.BindFlags |= BindFlags.BindDepthStencil;
        if ((flags & TextureFlag.BindUnorderedAccess) == TextureFlag.BindUnorderedAccess)
            desc.BindFlags |= BindFlags.BindUnorderedAccess;

        glDevice.CreateTextureFromGLHandle(handle, 0u, desc, ResourceState.ResourceStateUnknown, out var texture);
        if (texture is null || texture.IsNull)
            return false;
        try
        {
            return CreateFromHandle(texture, format, msaaLevel);
        }
        finally
        {
            Release(texture);
        }
    }

    public bool IsValid() => GetSubsystem<Graphics>()?.GetNativeRenderDevice() is null
        ? _parameters.Size != default
        : _handle is { IsNull: false };

    public bool GetLevelsDirty() => _levelsDirty;

    public bool GetResolveDirty() => _resolveDirty;

    public void GenerateLevels()
    {
        if (!_levelsDirty || _texture is null || _parameters.NumLevels <= 1u)
            return;
        var context = GetSubsystem<Graphics>()?.GetImmediateContext();
        if (context is not null && _shaderResourceView is not null)
            context.GenerateMips(_shaderResourceView);
        _levelsDirty = false;
    }

    public void Resolve()
    {
        if (!_resolveDirty)
            return;
        var context = GetSubsystem<Graphics>()?.GetImmediateContext();
        if (context is not null && _handle is not null && _resolvedHandle is not null)
        {
            using var attributes = new ResolveTextureSubresourceAttribs
            {
                SrcTextureTransitionMode = ResourceStateTransitionMode.ResourceStateTransitionModeTransition,
                DstTextureTransitionMode = ResourceStateTransitionMode.ResourceStateTransitionModeTransition
            };
            for (var slice = 0u; slice < _parameters.ArraySize; ++slice)
            {
                attributes.SrcSlice = slice;
                attributes.DstSlice = slice;
                context.ResolveTextureSubresource(_handle, _resolvedHandle, attributes);
            }
            MarkDirty();
        }
        _resolveDirty = false;
    }

    public unsafe void Update(uint level, Vector3i offset, Vector3i size, uint arraySlice, ReadOnlySpan<byte> data,
        uint rowStride = 0u, uint sliceStride = 0u)
    {
        if (_parameters.Size == default)
            throw new InvalidOperationException("Raw texture is not initialized.");
        if (level >= _parameters.NumLevelsRtv || arraySlice >= _parameters.ArraySize)
            throw new ArgumentOutOfRangeException(level >= _parameters.NumLevelsRtv ? nameof(level) : nameof(arraySlice));
        var levelSize = GetMipLevelSize(_parameters.Size, level);
        if (offset.X < 0 || offset.Y < 0 || offset.Z < 0 || size.X <= 0 || size.Y <= 0 || size.Z <= 0 ||
            offset.X + size.X > levelSize.X || offset.Y + size.Y > levelSize.Y || offset.Z + size.Z > levelSize.Z)
            throw new ArgumentOutOfRangeException(nameof(size));

        var packedRowStride = checked((uint)size.X * GetTexelSize(_parameters.Format));
        rowStride = rowStride != 0u ? rowStride : packedRowStride;
        sliceStride = sliceStride != 0u ? sliceStride : checked(rowStride * (uint)size.Y);
        if (rowStride < packedRowStride)
            throw new ArgumentOutOfRangeException(nameof(rowStride));
        if (sliceStride < rowStride * (uint)size.Y)
            throw new ArgumentOutOfRangeException(nameof(sliceStride));
        var requiredDataSize = checked((ulong)sliceStride * (uint)(size.Z - 1) +
            (ulong)rowStride * (uint)size.Y);
        if ((ulong)data.Length < requiredDataSize)
            throw new ArgumentException("Texture update data is smaller than the specified region and strides.", nameof(data));

        var context = GetSubsystem<Graphics>()?.GetImmediateContext();
        if (context is null || _handle is null)
            return;

        _updateBox.MinX = checked((uint)offset.X);
        _updateBox.MaxX = checked((uint)(offset.X + size.X));
        _updateBox.MinY = checked((uint)offset.Y);
        _updateBox.MaxY = checked((uint)(offset.Y + size.Y));
        _updateBox.MinZ = checked((uint)offset.Z);
        _updateBox.MaxZ = checked((uint)(offset.Z + size.Z));

        nint scratchBuffer = 0;
        try
        {
            fixed (byte* sourceData = data)
            {
                var uploadData = sourceData;
                if ((rowStride & 3u) != 0u || (sliceStride & 3u) != 0u)
                {
                    var alignedRowStride = (rowStride + 3u) & ~3u;
                    var alignedSliceStride = checked(alignedRowStride * (uint)size.Y);
                    scratchBuffer = GetRenderDevice()?.GetRenderPool().AllocateScratchBuffer(
                        checked(alignedSliceStride * (uint)size.Z)) ?? 0;
                    if (scratchBuffer == 0)
                        throw new OutOfMemoryException("Failed to allocate texture upload scratch buffer.");
                    var destination = (byte*)scratchBuffer;
                    for (var z = 0; z < size.Z; ++z)
                    {
                        for (var y = 0; y < size.Y; ++y)
                        {
                            Buffer.MemoryCopy(sourceData + z * sliceStride + y * rowStride,
                                destination + z * alignedSliceStride + y * alignedRowStride,
                                alignedRowStride, rowStride);
                        }
                    }
                    uploadData = destination;
                    rowStride = alignedRowStride;
                    sliceStride = alignedSliceStride;
                }

                _updateSubresource.PData = (nint)uploadData;
                _updateSubresource.Stride = rowStride;
                _updateSubresource.DepthStride = sliceStride;
                context.UpdateTexture(_handle, level, arraySlice, _updateBox, _updateSubresource,
                    ResourceStateTransitionMode.ResourceStateTransitionModeNone,
                    ResourceStateTransitionMode.ResourceStateTransitionModeTransition);
                _updateSubresource.PData = 0;
            }
        }
        finally
        {
            if (scratchBuffer != 0)
                GetRenderDevice()?.GetRenderPool().ReleaseScratchBuffer(scratchBuffer);
        }
        MarkDirty();
    }

    public void Update(uint level, Vector3i offset, Vector3i size, uint arraySlice, byte[] data,
        uint rowStride = 0u, uint sliceStride = 0u)
    {
        ArgumentNullException.ThrowIfNull(data);
        Update(level, offset, size, arraySlice, data.AsSpan(), rowStride, sliceStride);
    }

    public unsafe bool Read(uint slice, uint level, Span<byte> buffer)
    {
        if (level >= _parameters.NumLevels || slice >= _parameters.ArraySize)
            return false;
        var levelSize = GetMipLevelSize(_parameters.Size, level);
        var rowSize = checked((uint)levelSize.X * GetTexelSize(_parameters.Format));
        var requiredSize = checked(rowSize * (uint)levelSize.Y * (uint)levelSize.Z);
        if ((uint)buffer.Length < requiredSize)
            return false;
        if (_handle is null)
            return false;
        if (_resolveDirty)
            Resolve();
        if (_levelsDirty)
            GenerateLevels();

        var graphics = GetSubsystem<Graphics>();
        var device = graphics?.GetNativeRenderDevice();
        var context = graphics?.GetImmediateContext();
        if (device is null || context is null)
            return false;

        using var stagingName = new CString("RawTexture::Read staging texture");
        using var stagingDesc = new TextureDesc
        {
            Name = stagingName.Pointer,
            Type = _parameters.Type == TextureType.Texture3D
                ? ResourceDimension.ResourceDimTex3d
                : ResourceDimension.ResourceDimTex2d,
            Usage = Usage.UsageStaging,
            CPUAccessFlags = CpuAccessFlags.CpuAccessRead,
            Format = Graphics.MapTextureFormat(_parameters.Format),
            Width = checked((uint)levelSize.X),
            Height = checked((uint)levelSize.Y),
            MipLevels = 1u,
            SampleCount = 1u
        };
        if (_parameters.Type == TextureType.Texture3D)
            stagingDesc.Depth = checked((uint)levelSize.Z);
        else
            stagingDesc.ArraySize = 1u;
        device.CreateTexture(stagingDesc, null!, out var stagingTexture);
        if (stagingTexture is null || stagingTexture.IsNull)
            return false;

        try
        {
            using var copy = new CopyTextureAttribs(
                _resolvedHandle ?? _handle,
                ResourceStateTransitionMode.ResourceStateTransitionModeTransition,
                stagingTexture,
                ResourceStateTransitionMode.ResourceStateTransitionModeTransition)
            {
                SrcMipLevel = level,
                SrcSlice = slice
            };
            context.CopyTexture(copy);
            context.WaitForIdle();

            using var mapped = new MappedTextureSubresource();
            context.MapTextureSubresource(stagingTexture, 0u, 0u, MapType.MapRead, MapFlags.MapFlagNone, null!, mapped);
            if (mapped.PData == IntPtr.Zero)
                return false;
            try
            {
                var source = (byte*)mapped.PData;
                var sourceRowStride = Math.Max((nuint)mapped.Stride, (nuint)((rowSize + 3u) & ~3u));
                var sourceDepthStride = Math.Max((nuint)mapped.DepthStride,
                    checked(sourceRowStride * (nuint)levelSize.Y));
                fixed (byte* destinationStart = buffer)
                {
                    for (var z = 0; z < levelSize.Z; ++z)
                    {
                        for (var y = 0; y < levelSize.Y; ++y)
                        {
                            var sourceOffset = checked((nint)z * (nint)sourceDepthStride + (nint)y * (nint)sourceRowStride);
                            var destinationOffset = checked((z * levelSize.Y + y) * (int)rowSize);
                            Buffer.MemoryCopy(source + sourceOffset,
                                destinationStart + destinationOffset, rowSize, rowSize);
                        }
                    }
                }
            }
            finally
            {
                context.UnmapTextureSubresource(stagingTexture, 0u, 0u);
            }
            return true;
        }
        finally
        {
            Release(stagingTexture);
        }
    }

    public bool Read(uint slice, uint level, byte[] buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        return Read(slice, level, buffer.AsSpan());
    }

    public ulong CalculateMemoryUseGPU()
    {
        if (_handle is null)
            return 0;
        ulong sliceMemory = 0;
        if (_resolvedHandle is not null)
        {
            sliceMemory += _parameters.MultiSample * GetMipLevelSizeInBytes(0u);
        }
        for (var level = 0u; level < _parameters.NumLevels; ++level)
            sliceMemory += GetMipLevelSizeInBytes(level);
        return _parameters.ArraySize * sliceMemory;
    }

    private ulong GetMipLevelSizeInBytes(uint level)
    {
        var size = GetMipLevelSize(_parameters.Size, level);
        return checked((ulong)(uint)size.X * (uint)size.Y * (uint)size.Z * GetTexelSize(_parameters.Format));
    }

    /// <summary>Create UAV for given array slices and mip levels.</summary>
    public ITextureView? CreateUAV(RawTextureUAVKey key)
    {
        if (_texture is null || (_parameters.Flags & TextureFlag.BindUnorderedAccess) != TextureFlag.BindUnorderedAccess)
            return null;
        if (!GetRenderDevice()?.IsUnorderedAccessFormatSupported(_parameters.Format) ?? true)
            return null;

        if (GetUAV(key) is { } cachedView)
            return cachedView;

        var effectiveKey = key;
        if (!ValidateUavKey(ref effectiveKey))
            return null;

        if (GetUAV(effectiveKey) is { } normalizedView)
        {
            _uavs[key] = normalizedView;
            return normalizedView;
        }

        var createdView = CreateUnorderedAccessView(effectiveKey);
        if (createdView is null || createdView.IsNull)
            return null;

        _uavs[key] = createdView;
        _uavs[effectiveKey] = createdView;
        return createdView;
    }

    private ITextureView? CreateUnorderedAccessView(RawTextureUAVKey key)
    {
        if (_handle is null)
            return null;

        using var viewDesc = new TextureViewDesc
        {
            ViewType = TextureViewType.TextureViewUnorderedAccess,
            TextureDim = GetViewResourceDimension(_parameters.Type),
            Format = Graphics.MapTextureFormat(_parameters.Format),
            MostDetailedMip = key.FirstLevel,
            NumMipLevels = key.NumLevels,
            AccessFlags = (key.CanRead ? UavAccessFlag.UavAccessFlagRead : UavAccessFlag.UavAccessUnspecified) |
                (key.CanWrite ? UavAccessFlag.UavAccessFlagWrite : UavAccessFlag.UavAccessUnspecified)
        };
        if (_parameters.Type == TextureType.Texture3D)
        {
            viewDesc.FirstDepthSlice = key.FirstSlice;
            viewDesc.NumDepthSlices = key.NumSlices;
        }
        else
        {
            viewDesc.FirstArraySlice = key.FirstSlice;
            viewDesc.NumArraySlices = key.NumSlices;
        }
        _handle.CreateView(viewDesc, out var view);
        if (view is null || view.IsNull)
            return null;
        _ownedUavs.Add(view);
        return view;
    }

    public ITextureView? GetUAV(RawTextureUAVKey key)
    {
        return _uavs.TryGetValue(key, out var view) ? view : null;
    }

    /// <summary>For render target and depth-stencil textures, mark shader resource view dirty.</summary>
    public void MarkDirty()
    {
        if (_texture is null)
            return;

        if (_parameters.MultiSample > 1u && (_parameters.Flags & TextureFlag.NoMultiSampledAutoResolve) != TextureFlag.NoMultiSampledAutoResolve)
            _resolveDirty = true;
        else if (_parameters.NumLevels > 1u)
            _levelsDirty = true;
    }

    public override void Invalidate()
    {
        base.Invalidate();
        DestroyGPU();
        _levelsDirty = false;
        _resolveDirty = false;
    }

    public override void Restore()
    {
        base.Restore();
        DestroyGPU();
        if (_parameters.Size == default)
            ClearDataLost();
        else if (TryRestore())
            ClearDataLost();
        else
        {
            CreateGPU();
            SetDataLost(true);
        }
        _levelsDirty = false;
        _resolveDirty = false;
    }

    public override void Destroy()
    {
        base.Destroy();
        DestroyGPU();
        _texture = null;
        _levelsDirty = false;
        _resolveDirty = false;
    }

    protected virtual void OnCreateGPU()
    {
    }

    protected virtual void OnDestroyGPU()
    {
    }

    protected virtual bool TryRestore() => false;

    protected bool CreateGPU()
    {
        var graphics = GetSubsystem<Graphics>();
        var device = graphics?.GetNativeRenderDevice();
        if (device is null || device.IsNull)
            return true;

        using var nativeName = new CString(GetDebugName());
        using var textureDesc = new TextureDesc
        {
            Name = nativeName.Pointer,
            Type = GetResourceDimension(_parameters.Type),
            Usage = Usage.UsageDefault,
            Format = Graphics.MapTextureFormat(_parameters.Format),
            Width = checked((uint)_parameters.Size.X),
            Height = checked((uint)_parameters.Size.Y),
            MipLevels = _parameters.NumLevelsRtv,
            SampleCount = _parameters.MultiSample,
            BindFlags = BindFlags.BindShaderResource
        };
        if (textureDesc.Format == 0)
            return false;
        if (_parameters.Type == TextureType.Texture3D)
            textureDesc.Depth = checked((uint)_parameters.Size.Z);
        else
            textureDesc.ArraySize = _parameters.ArraySize;
        if ((_parameters.Flags & TextureFlag.BindRenderTarget) == TextureFlag.BindRenderTarget)
            textureDesc.BindFlags |= BindFlags.BindRenderTarget;
        if ((_parameters.Flags & TextureFlag.BindDepthStencil) == TextureFlag.BindDepthStencil)
            textureDesc.BindFlags |= BindFlags.BindDepthStencil;
        if ((_parameters.Flags & TextureFlag.BindUnorderedAccess) == TextureFlag.BindUnorderedAccess)
            textureDesc.BindFlags |= BindFlags.BindUnorderedAccess;
        if (_parameters.MultiSample == 1u && (_parameters.Flags & TextureFlag.BindRenderTarget) == TextureFlag.BindRenderTarget && _parameters.NumLevelsRtv != 1u)
            textureDesc.MiscFlags |= MiscTextureFlags.MiscTextureFlagGenerateMips;

        device.CreateTexture(textureDesc, null!, out _handle);
        if (_handle is null || _handle.IsNull)
        {
            _handle = null;
            return false;
        }

        if (_parameters.MultiSample != 1u && (_parameters.Flags & TextureFlag.NoMultiSampledAutoResolve) != TextureFlag.NoMultiSampledAutoResolve)
        {
            textureDesc.MipLevels = _parameters.NumLevels;
            textureDesc.SampleCount = 1u;
            if (_parameters.NumLevels != 1u)
                textureDesc.MiscFlags |= MiscTextureFlags.MiscTextureFlagGenerateMips;
            device.CreateTexture(textureDesc, null!, out _resolvedHandle);
            if (_resolvedHandle is null || _resolvedHandle.IsNull)
            {
                DestroyGPU();
                return false;
            }
        }

        if (!InitializeDefaultViews(textureDesc.BindFlags))
        {
            DestroyGPU();
            return false;
        }

        OnCreateGPU();
        return true;
    }

    protected void DestroyGPU()
    {
        OnDestroyGPU();
        foreach (var view in _ownedUavs)
            Release(view);
        _ownedUavs.Clear();
        foreach (var view in _renderSurfaces)
        {
            if (!ReferenceEquals(view, _renderTargetView) && !ReferenceEquals(view, _depthStencilView))
                Release(view);
        }
        foreach (var view in _readOnlyRenderSurfaces)
        {
            if (!ReferenceEquals(view, _readOnlyDepthStencilView))
                Release(view);
        }
        _renderSurfaces = [];
        _readOnlyRenderSurfaces = [];
        _uavs.Clear();
        Release(_unorderedAccessView);
        Release(_readOnlyDepthStencilView);
        Release(_depthStencilView);
        Release(_renderTargetView);
        Release(_shaderResourceView);
        Release(_resolvedHandle);
        Release(_handle);
        _unorderedAccessView = null;
        _readOnlyDepthStencilView = null;
        _depthStencilView = null;
        _renderTargetView = null;
        _shaderResourceView = null;
        _resolvedHandle = null;
        _handle = null;
    }

    private bool InitializeDefaultViews(BindFlags bindFlags)
    {
        var shaderTexture = _resolvedHandle ?? _handle!;
        if ((bindFlags & BindFlags.BindShaderResource) != 0)
            _shaderResourceView = CreateView(shaderTexture, TextureViewType.TextureViewShaderResource);
        if ((bindFlags & BindFlags.BindRenderTarget) != 0)
        {
            _renderTargetView = CreateView(_handle!, TextureViewType.TextureViewRenderTarget);
            _renderSurfaces = CreateRenderSurfaces(_renderTargetView, TextureViewType.TextureViewRenderTarget);
        }
        if ((bindFlags & BindFlags.BindDepthStencil) != 0)
        {
            _depthStencilView = CreateView(_handle!, TextureViewType.TextureViewDepthStencil);
            _renderSurfaces = CreateRenderSurfaces(_depthStencilView, TextureViewType.TextureViewDepthStencil);
            _readOnlyDepthStencilView = CreateView(_handle!, TextureViewType.TextureViewReadOnlyDepthStencil);
            _readOnlyRenderSurfaces = CreateRenderSurfaces(_readOnlyDepthStencilView,
                TextureViewType.TextureViewReadOnlyDepthStencil);
        }
        if ((bindFlags & BindFlags.BindUnorderedAccess) != 0)
        {
            _unorderedAccessView = CreateView(_handle!, TextureViewType.TextureViewUnorderedAccess);
            if (_unorderedAccessView is not null)
                _uavs[default] = _unorderedAccessView;
        }
        return _shaderResourceView is not null &&
            ((bindFlags & BindFlags.BindRenderTarget) == 0 || _renderTargetView is not null) &&
            ((bindFlags & BindFlags.BindDepthStencil) == 0 || _depthStencilView is not null && _readOnlyDepthStencilView is not null) &&
            ((bindFlags & BindFlags.BindUnorderedAccess) == 0 || _unorderedAccessView is not null);
    }

    private ITextureView? CreateView(ITexture texture, TextureViewType viewType, uint firstSlice = 0u, uint numSlices = 0u)
    {
        using var viewDesc = new TextureViewDesc
        {
            ViewType = viewType,
            Format = Graphics.MapTextureFormat(_parameters.Format)
        };
        if (viewType == TextureViewType.TextureViewShaderResource && _parameters.NumLevels > 1u)
            viewDesc.Flags |= TextureViewFlags.TextureViewFlagAllowMipMapGeneration;
        if (viewType == TextureViewType.TextureViewUnorderedAccess)
            viewDesc.AccessFlags = UavAccessFlag.UavAccessFlagReadWrite;
        if (numSlices != 0u)
        {
            viewDesc.TextureDim = ResourceDimension.ResourceDimTex2dArray;
            viewDesc.FirstArraySlice = firstSlice;
            viewDesc.NumArraySlices = numSlices;
        }
        texture.CreateView(viewDesc, out var view);
        return view is { IsNull: false } ? view : null;
    }

    private ITextureView[] CreateRenderSurfaces(ITextureView? defaultView, TextureViewType viewType)
    {
        if (defaultView is null || _parameters.Type == TextureType.Texture3D)
            return [];
        if (_parameters.Type == TextureType.Texture2D)
            return [defaultView];

        var surfaces = new ITextureView[_parameters.ArraySize];
        for (var slice = 0u; slice < _parameters.ArraySize; ++slice)
            surfaces[slice] = CreateView(_handle!, viewType, slice, 1u)
                ?? throw new InvalidOperationException("Failed to create texture render surface.");
        return surfaces;
    }

    private static ResourceDimension GetResourceDimension(TextureType type) => type switch
    {
        TextureType.Texture2D => ResourceDimension.ResourceDimTex2d,
        TextureType.TextureCube => ResourceDimension.ResourceDimTexCube,
        TextureType.Texture3D => ResourceDimension.ResourceDimTex3d,
        TextureType.Texture2DArray => ResourceDimension.ResourceDimTex2dArray,
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };

    private static ResourceDimension GetViewResourceDimension(TextureType type) => type switch
    {
        TextureType.Texture2D => ResourceDimension.ResourceDimTex2d,
        TextureType.TextureCube => ResourceDimension.ResourceDimTex2dArray,
        TextureType.Texture3D => ResourceDimension.ResourceDimTex3d,
        TextureType.Texture2DArray => ResourceDimension.ResourceDimTex2dArray,
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };

    private static void Release(IObject? value)
    {
        if (value is { IsNull: false })
            _ = value.Release();
    }

    private void ApplySamplerState()
    {
        if (_texture is null)
            return;

        _texture.SetFilterMode(_samplerStateDesc.FilterMode);
        _texture.SetAddressMode(TextureCoordinate.U, _samplerStateDesc[TextureCoordinate.U]);
        _texture.SetAddressMode(TextureCoordinate.V, _samplerStateDesc[TextureCoordinate.V]);
        _texture.SetAddressMode(TextureCoordinate.W, _samplerStateDesc[TextureCoordinate.W]);
        _texture.SetAnisotropy(_samplerStateDesc.Anisotropy);
        _texture.SetShadowCompare(_samplerStateDesc.ShadowCompare);
    }

    private static bool TryCreateManagedTexture(RawTextureParams parameters, out Texture createdTexture, out RawTextureParams finalizedParameters)
    {
        createdTexture = null!;
        finalizedParameters = default;

        if (parameters.Format == TextureFormat.Unknown)
            return false;

        var isRenderTarget = (parameters.Flags & TextureFlag.BindRenderTarget) == TextureFlag.BindRenderTarget;
        var isDepthStencil = (parameters.Flags & TextureFlag.BindDepthStencil) == TextureFlag.BindDepthStencil;
        if (isRenderTarget && isDepthStencil)
            return false;

        if (!ValidateDimensions(ref parameters))
            return false;

        var multiSample = Math.Clamp(parameters.MultiSample, 1u, 16u);
        var flags = parameters.Flags;
        if (multiSample == 1u)
            flags |= TextureFlag.NoMultiSampledAutoResolve;
        if (multiSample != 1u && !isRenderTarget && !isDepthStencil)
            return false;

        var maxLevels = CalculateMipLevelCount(parameters.Size);
        var numLevels = parameters.NumLevels == 0u ? maxLevels : Math.Min(parameters.NumLevels, maxLevels);
        if (isDepthStencil || (multiSample != 1u && (flags & TextureFlag.NoMultiSampledAutoResolve) == TextureFlag.NoMultiSampledAutoResolve))
            numLevels = 1u;
        parameters = parameters with
        {
            Flags = flags,
            MultiSample = multiSample,
            NumLevels = numLevels,
            NumLevelsRtv = multiSample != 1u ? 1u : numLevels
        };

        var normalizedArraySize = parameters.ArraySize;
        var normalizedMultiSample = parameters.MultiSample;
        var normalizedSize = parameters.Size;

        Texture textureCandidate;
        IsCreatingManagedTexture = true;
        try
        {
            switch (parameters.Type)
            {
                case TextureType.Texture2D:
                {
                    var texture2D = new Texture2D($"RawTexture2D.{Guid.NewGuid():N}");
                    texture2D.SetNumLevels(parameters.NumLevels);
                    if (!texture2D.SetSize(normalizedSize.X, normalizedSize.Y, parameters.Format, (int)normalizedMultiSample,
                        (parameters.Flags & TextureFlag.NoMultiSampledAutoResolve) == TextureFlag.NoMultiSampledAutoResolve))
                        return false;
                    textureCandidate = texture2D;
                    break;
                }

                case TextureType.TextureCube:
                {
                    var textureCube = new TextureCube($"RawTextureCube.{Guid.NewGuid():N}");
                    textureCube.SetNumLevels(parameters.NumLevels);
                    var edge = normalizedSize.X;
                    if (!textureCube.SetSize(edge, parameters.Format, (int)normalizedMultiSample,
                        (parameters.Flags & TextureFlag.NoMultiSampledAutoResolve) == TextureFlag.NoMultiSampledAutoResolve))
                        return false;
                    normalizedSize = new Vector3i(edge, edge, 1);
                    textureCandidate = textureCube;
                    break;
                }

                case TextureType.Texture2DArray:
                {
                    var textureArray = new Texture2DArray($"RawTextureArray.{Guid.NewGuid():N}");
                    textureArray.SetNumLevels(parameters.NumLevels);
                    if (!textureArray.SetLayers(normalizedArraySize, normalizedSize.X, normalizedSize.Y, parameters.Format, parameters.Flags,
                        (int)normalizedMultiSample,
                        (parameters.Flags & TextureFlag.NoMultiSampledAutoResolve) == TextureFlag.NoMultiSampledAutoResolve))
                        return false;
                    textureCandidate = textureArray;
                    break;
                }

                case TextureType.Texture3D:
                {
                    var texture3D = new Texture3D($"RawTexture3D.{Guid.NewGuid():N}");
                    texture3D.SetNumLevels(parameters.NumLevels);
                    if (!texture3D.SetSize(normalizedSize.X, normalizedSize.Y, normalizedSize.Z, parameters.Format))
                        return false;
                    textureCandidate = texture3D;
                    break;
                }

                default:
                    return false;
            }
        }
        finally
        {
            IsCreatingManagedTexture = false;
        }

        createdTexture = textureCandidate;
        finalizedParameters = new RawTextureParams(
            parameters.Type,
            parameters.Format,
            parameters.Flags,
            new Vector3i(textureCandidate.Width, textureCandidate.Height, textureCandidate.Depth),
            parameters.Type == TextureType.Texture2DArray ? (uint)textureCandidate.Depth : normalizedArraySize,
            textureCandidate.Levels,
            (uint)textureCandidate.MultiSample,
            CalculateNumLevelsRtv(textureCandidate, parameters));
        return true;
    }

    private static bool ValidateDimensions(ref RawTextureParams parameters)
    {
        switch (parameters.Type)
        {
            case TextureType.Texture2D when parameters.Size.X > 0 && parameters.Size.Y > 0:
                parameters = parameters with { Size = new Vector3i(parameters.Size.X, parameters.Size.Y, 1), ArraySize = 1u };
                return true;
            case TextureType.TextureCube when parameters.Size.X > 0:
                parameters = parameters with { Size = new Vector3i(parameters.Size.X, parameters.Size.X, 1), ArraySize = 6u };
                return true;
            case TextureType.Texture3D when parameters.Size.X > 0 && parameters.Size.Y > 0 && parameters.Size.Z > 0:
                parameters = parameters with { ArraySize = 1u };
                return true;
            case TextureType.Texture2DArray when parameters.Size.X > 0 && parameters.Size.Y > 0 && parameters.ArraySize != 0u:
                parameters = parameters with { Size = new Vector3i(parameters.Size.X, parameters.Size.Y, 1) };
                return true;
            default:
                return false;
        }
    }

    private static uint CalculateMipLevelCount(Vector3i size)
    {
        var largestDimension = Math.Max(size.X, Math.Max(size.Y, size.Z));
        var levels = 1u;
        while (largestDimension > 1)
        {
            largestDimension >>= 1;
            ++levels;
        }
        return levels;
    }

    private static Vector3i GetMipLevelSize(Vector3i size, uint level) => new(
        Math.Max(size.X >> (int)level, 1),
        Math.Max(size.Y >> (int)level, 1),
        Math.Max(size.Z >> (int)level, 1));

    private static uint GetTexelSize(TextureFormat format) => format switch
    {
        TextureFormat.R8 => 1u,
        TextureFormat.RG16F => 4u,
        TextureFormat.RGB8 => 4u,
        TextureFormat.RGBA8 => 4u,
        TextureFormat.RGBA16F => 8u,
        TextureFormat.Depth32 => 4u,
        TextureFormat.Depth24Stencil8 => 4u,
        _ => throw new NotSupportedException($"Texture format '{format}' is not supported.")
    };

    private static uint CalculateNumLevelsRtv(Texture texture, RawTextureParams parameters)
    {
        if (parameters.NumLevelsRtv != 0u)
            return parameters.NumLevelsRtv;

        return texture.MultiSample > 1 && (parameters.Flags & TextureFlag.NoMultiSampledAutoResolve) != TextureFlag.NoMultiSampledAutoResolve
            ? 1u
            : texture.Levels;
    }

    private bool ValidateUavKey(ref RawTextureUAVKey key)
    {
        if (!key.CanRead && !key.CanWrite)
            return false;
        if (key.FirstSlice >= _parameters.ArraySize)
            return false;
        if (key.FirstLevel >= _parameters.NumLevelsRtv)
            return false;

        if (key.NumLevels == 0u)
            key = key with { NumLevels = _parameters.NumLevelsRtv - key.FirstLevel };
        if (key.NumSlices == 0u)
            key = key with { NumSlices = _parameters.ArraySize - key.FirstSlice };

        if (key.FirstSlice + key.NumSlices > _parameters.ArraySize)
            return false;
        if (key.FirstLevel + key.NumLevels > _parameters.NumLevelsRtv)
            return false;

        return true;
    }
}
