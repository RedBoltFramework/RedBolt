using Microsoft.Extensions.Logging;
using RedBolt.Bindings.Diligent;

using RedBolt.Container;
using RedBolt.Graphics;
using RedBolt.Logging;
namespace RedBolt.RenderAPI;
using Math = global::System.Math;


public struct RawTextureHandles
{
    /// <summary>Main texture. If texture is multi-sampled, only one mip level is created.</summary>
    public ITexture? Texture;
    /// <summary>If texture is multi-sampled, resolved texture is created with requested number of mip levels.</summary>
    public ITexture? ResolvedTexture;
    /// <summary>Texture view that can be used as readable shader resource.
    /// If resolved texture is created, it is referenced by this view.</summary>
    public ITextureView? ShaderResourceView;
    /// <summary>Texture view that can be used as render target.</summary>
    public ITextureView? RenderTargetView;
    /// <summary>Texture view that can be used as depth-stencil buffer.</summary>
    public ITextureView? DepthStencilView;
    /// <summary>Texture view that can be used as read-only depth-stencil buffer.</summary>
    public ITextureView? ReadOnlyDepthStencilView;
    /// <summary>Texture view that can be attached as unordered access resource.</summary>
    public ITextureView? UnorderedAccessView;
    /// <summary>Array of all 2D render target views for each array slice and for each cube texture face.
    /// Empty for 3D textures.</summary>
    public ITextureView[] RenderSurfaces;
    /// <summary>Same as above, but read-only. Valid only for depth-stencil textures.</summary>
    public ITextureView[] ReadOnlyRenderSurfaces;
    /// <summary>All requested UAVs.</summary>
    public Dictionary<RawTextureUAVKey, ITextureView> UnorderedAccessViews;

    public RawTextureHandles()
    {
        RenderSurfaces = [];
        ReadOnlyRenderSurfaces = [];
        UnorderedAccessViews = [];
    }

    public readonly bool IsValid() => Texture is { IsNull: false };
}

/// <summary>Common class for all GPU textures.
/// By default RawTexture loses data on device lost and does not attempt to recover it.
/// This behavior can be changed in derived classes.
/// RawTexture is RAII object that's not supposed to be in invalid state.
/// However, this behavior can also be changed in derived classes.</summary>
public class RawTexture : DeviceObject
{
    public RawTexture(RawTextureParams parameters)
        : this()
    {
        Create(parameters);
    }

    public override void Dispose()
    {
        DestroyGPU();
        _resolveAttributes?.Dispose();
        _resolveAttributes = null;
        base.Dispose();
    }

    /// <summary>Create from texture handle.</summary>
    public bool CreateFromHandle(ITexture texture, TextureFormat format = TextureFormat.TexFormatUnknown, int msaaLevel = 1)
    {
        ArgumentNullException.ThrowIfNull(texture);
        if (texture.IsNull)
            return false;

        DestroyGPU();

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
        {
            Log.GetLogger().LogError("Unsupported texture type '{TextureType}'", desc.Type);
            return false;
        }

        var resolvedFormat = format;
        if (resolvedFormat == TextureFormat.TexFormatUnknown)
            resolvedFormat = desc.Format;
        if (resolvedFormat == TextureFormat.TexFormatUnknown)
            return false;

        // TODO: Revisit this flag
        var flags = _parameters.Flags | TextureFlag.NoMultiSampledAutoResolve;
        if ((desc.BindFlags & BindFlags.BindRenderTarget) != 0)
            flags |= TextureFlag.BindRenderTarget;
        if ((desc.BindFlags & BindFlags.BindDepthStencil) != 0)
            flags |= TextureFlag.BindDepthStencil;
        if ((desc.BindFlags & BindFlags.BindUnorderedAccess) != 0)
            flags |= TextureFlag.BindUnorderedAccess;

        _parameters = _parameters with
        {
            Type = type.Value,
            Format = resolvedFormat,
            Flags = flags,
            Size = new Vector3i(checked((int)desc.Width), checked((int)desc.Height), checked((int)desc.Depth)),
            ArraySize = checked((int)desc.ArraySize),
            NumLevels = checked((int)desc.MipLevels),
            MultiSample = Math.Max(checked((int)desc.SampleCount), msaaLevel),
            NumLevelsRtv = checked((int)desc.MipLevels)
        };
        _ = texture.AddRef();
        _handles.Texture = texture;
        return InitializeDefaultViews(desc.BindFlags);
    }

    /// <summary>Create texture from raw ID3D11Texture2D pointer.</summary>
    public bool CreateFromD3D11Texture2D(nint d3d11Texture2D, TextureFormat format, int msaaLevel)
    {
#if REDBOLT_D3D11_SUPPORTED
        var renderDevice = GetRenderDevice();
        var nativeDevice = renderDevice?.GetRenderDevice();
        if (renderDevice?.GetBackend() == RenderBackend.D3D11 && nativeDevice is { IsNull: false })
        {
            using var deviceD3D11 = IRenderDeviceD3D11.QueryManagedWrapper(nativeDevice)
                ?? throw new InvalidOperationException("D3D11 render device interface is unavailable.");
            deviceD3D11.CreateTexture2DFromD3DResource(
                d3d11Texture2D, ResourceState.ResourceStateUnknown, out var texture);
            if (texture is null || texture.IsNull)
            {
                Log.GetLogger().LogError("Failed to create texture from existing ID3D11Texture2D pointer");
                return false;
            }

            try
            {
                return CreateFromHandle(texture, format, msaaLevel);
            }
            finally
            {
                Release(texture);
            }
        }
#endif
#if DEBUG
        throw new InvalidOperationException("RawTexture::CreateFromD3D11Texture2D is not supported on this platform");
#else
        return false;
#endif
    }

    /// <summary>Create texture from raw ID3D12Resource pointer.</summary>
    public bool CreateFromD3D12Resource(nint d3d12Resource, TextureFormat format, int msaaLevel)
    {
#if REDBOLT_D3D12_SUPPORTED
        var renderDevice = GetRenderDevice();
        var nativeDevice = renderDevice?.GetRenderDevice();
        if (renderDevice?.GetBackend() == RenderBackend.D3D12 && nativeDevice is { IsNull: false })
        {
            using var deviceD3D12 = IRenderDeviceD3D12.QueryManagedWrapper(nativeDevice)
                ?? throw new InvalidOperationException("D3D12 render device interface is unavailable.");
            deviceD3D12.CreateTextureFromD3DResource(
                d3d12Resource, ResourceState.ResourceStateUnknown, out var texture);
            if (texture is null || texture.IsNull)
            {
                Log.GetLogger().LogError("Failed to create texture from existing ID3D12Resource pointer");
                return false;
            }

            try
            {
                return CreateFromHandle(texture, format, msaaLevel);
            }
            finally
            {
                Release(texture);
            }
        }
#endif
#if DEBUG
        throw new InvalidOperationException("RawTexture::CreateFromD3D12Resource is not supported on this platform");
#else
        return false;
#endif
    }

    /// <summary>Create texture from VkImage handle.</summary>
    public bool CreateFromVulkanImage(ulong vkImage, RawTextureParams parameters)
    {
#if REDBOLT_VULKAN_SUPPORTED
        var renderDevice = GetRenderDevice();
        var nativeDevice = renderDevice?.GetRenderDevice();
        if (renderDevice?.GetBackend() == RenderBackend.Vulkan && nativeDevice is { IsNull: false })
        {
            using var nativeName = new CString("Texture from external resource");
            using var textureDesc = new TextureDesc
            {
                Name = nativeName.Pointer,
                Type = GetResourceDimension(parameters.Type),
                Usage = Usage.UsageDefault,
                Format = parameters.Format,
                Width = checked((uint)parameters.Size.X),
                Height = checked((uint)parameters.Size.Y),
                MipLevels = checked((uint)parameters.NumLevels),
                SampleCount = checked((uint)parameters.MultiSample)
            };
            if (parameters.Type == TextureType.Texture3D)
                textureDesc.Depth = checked((uint)parameters.Size.Z);
            else
                textureDesc.ArraySize = checked((uint)parameters.ArraySize);

            if ((parameters.Flags & TextureFlag.BindRenderTarget) == TextureFlag.BindRenderTarget)
                textureDesc.BindFlags |= BindFlags.BindRenderTarget;
            if ((parameters.Flags & TextureFlag.BindDepthStencil) == TextureFlag.BindDepthStencil)
                textureDesc.BindFlags |= BindFlags.BindDepthStencil;
            if ((parameters.Flags & TextureFlag.BindUnorderedAccess) == TextureFlag.BindUnorderedAccess)
                textureDesc.BindFlags |= BindFlags.BindUnorderedAccess;

            using var deviceVk = IRenderDeviceVk.QueryManagedWrapper(nativeDevice)
                ?? throw new InvalidOperationException("Vulkan render device interface is unavailable.");
            deviceVk.CreateTextureFromVulkanImage(
                (nint)vkImage, textureDesc, ResourceState.ResourceStateUnknown, out var texture);
            if (texture is null || texture.IsNull)
            {
                Log.GetLogger().LogError("Failed to create texture from existing VkImage pointer");
                return false;
            }

            try
            {
                return CreateFromHandle(texture, parameters.Format, parameters.MultiSample);
            }
            finally
            {
                Release(texture);
            }
        }
#endif
#if DEBUG
        throw new InvalidOperationException("RawTexture::CreateFromVulkanImage is not supported on this platform");
#else
        return false;
#endif
    }

    /// <summary>Create texture from raw OpenGL handle.</summary>
    public bool CreateFromGLTexture(uint handle, TextureType type, TextureFlag flags, TextureFormat format,
        int arraySize, int msaaLevel)
    {
        var renderDevice = GetRenderDevice();
        var nativeDevice = renderDevice?.GetRenderDevice();
        if (renderDevice?.GetBackend() != RenderBackend.OpenGL || nativeDevice is null || nativeDevice.IsNull)
#if DEBUG
            throw new InvalidOperationException("RawTexture::CreateFromGLTexture is not supported on this platform");
#else
            return false;
#endif

        using var glDevice = IRenderDeviceGL.QueryManagedWrapper(nativeDevice)
            ?? throw new InvalidOperationException("OpenGL render device interface is unavailable.");
        using var nativeName = new CString("Texture from external resource");
        using var desc = new TextureDesc
        {
            Name = nativeName.Pointer,
            Type = GetResourceDimension(type),
            Usage = Usage.UsageDefault,
            Format = format,
            ArraySize = checked((uint)(type == TextureType.Texture2DArray ? arraySize : 1)),
        };
        if ((flags & TextureFlag.BindRenderTarget) == TextureFlag.BindRenderTarget)
            desc.BindFlags |= BindFlags.BindRenderTarget;
        if ((flags & TextureFlag.BindDepthStencil) == TextureFlag.BindDepthStencil)
            desc.BindFlags |= BindFlags.BindDepthStencil;
        if ((flags & TextureFlag.BindUnorderedAccess) == TextureFlag.BindUnorderedAccess)
            desc.BindFlags |= BindFlags.BindUnorderedAccess;

        glDevice.CreateTextureFromGLHandle(handle, 0u, desc, ResourceState.ResourceStateUnknown, out var texture);
        if (texture is null || texture.IsNull)
        {
            Log.GetLogger().LogError("Failed to create texture from existing GL texture handle");
            return false;
        }
        try
        {
            return CreateFromHandle(texture, format, msaaLevel);
        }
        finally
        {
            Release(texture);
        }
    }

    /// <summary>Set default sampler to be used for this texture.</summary>
    public void SetSamplerStateDesc(SamplerStateDesc desc) => _samplerStateDesc = desc;

    /// <summary>Create UAV for given array slices and mip levels.</summary>
    public ITextureView? CreateUAV(RawTextureUAVKey key)
    {
        if (!_handles.IsValid())
        {
            Log.GetLogger().LogError("RawTexture::CreateUAV is ignored for uninitialized texture");
            return null;
        }

        if (GetUAV(key) is { } cachedView)
            return cachedView;

        var effectiveKey = key;
        if (!ValidateUavKey(ref effectiveKey))
            return null;

        if (GetUAV(effectiveKey) is { } normalizedView)
        {
            _ = normalizedView.AddRef();
            _handles.UnorderedAccessViews[key] = normalizedView;
            return normalizedView;
        }

        var createdView = CreateUnorderedAccessView(effectiveKey);
        if (createdView is null || createdView.IsNull)
            return null;

        _handles.UnorderedAccessViews[key] = createdView;
        if (key != effectiveKey)
        {
            _ = createdView.AddRef();
            _handles.UnorderedAccessViews[effectiveKey] = createdView;
        }
        return createdView;
    }

    public ITextureView? GetUAV(RawTextureUAVKey key)
    {
        return _handles.UnorderedAccessViews.TryGetValue(key, out var view) ? view : null;
    }

    /// <summary>Generate mip levels from the topmost level. Avoid calling it during the rendering.</summary>
    public void GenerateLevels()
    {
        if (_parameters.NumLevels > 1)
        {
            if (_handles.ShaderResourceView is null)
                return;
            _renderDevice!.GetImmediateContext()!.GenerateMips(_handles.ShaderResourceView);
        }
        _levelsDirty = false;
    }

    /// <summary>Resolve multi-sampled texture to the simple resolved texture.</summary>
    public void Resolve()
    {
        if (_handles.ResolvedTexture is not null)
        {
            var context = _renderDevice!.GetImmediateContext()!;
            var attributes = _resolveAttributes ??= new ResolveTextureSubresourceAttribs
            {
                SrcTextureTransitionMode = ResourceStateTransitionMode.ResourceStateTransitionModeTransition,
                DstTextureTransitionMode = ResourceStateTransitionMode.ResourceStateTransitionModeTransition
            };
            for (var slice = 0; slice < _parameters.ArraySize; ++slice)
            {
                attributes.SrcSlice = checked((uint)slice);
                attributes.DstSlice = checked((uint)slice);
                context.ResolveTextureSubresource(_handles.Texture!, _handles.ResolvedTexture, attributes);
            }
            MarkDirty();
        }
        _resolveDirty = false;
    }

    /// <summary>Update texture data. If strides are not specified, they are deduced automatically from <paramref name="size"/>.</summary>
    public unsafe void Update(int level, Vector3i offset, Vector3i size, int arraySlice, ReadOnlySpan<byte> data,
        int rowStride = 0, int sliceStride = 0)
    {
        if (_parameters.Size == default)
            throw new InvalidOperationException("Raw texture is not initialized.");
        if (level >= _parameters.NumLevelsRtv || arraySlice >= _parameters.ArraySize)
            throw new ArgumentOutOfRangeException(level >= _parameters.NumLevelsRtv ? nameof(level) : nameof(arraySlice));
        var levelSize = GetMipLevelSize(_parameters.Size, level);
        if (offset.X < 0 || offset.Y < 0 || offset.Z < 0 || size.X <= 0 || size.Y <= 0 || size.Z <= 0 ||
            offset.X + size.X > levelSize.X || offset.Y + size.Y > levelSize.Y || offset.Z + size.Z > levelSize.Z)
            throw new ArgumentOutOfRangeException(nameof(size));
        if (_parameters.Type != TextureType.Texture3D &&
            (offset.X % _formatBlockWidth != 0 || offset.Y % _formatBlockHeight != 0))
            throw new ArgumentException("Unaligned update region offset", nameof(offset));

        if (_renderDevice is null)
            return;
        if (!_handles.IsValid())
        {
            Log.GetLogger().LogWarning("RawTexture::Update is ignored for uninitialized texture");
            return;
        }

        var widthInBlocks = (size.X + _formatBlockWidth - 1) / _formatBlockWidth;
        var heightInBlocks = (size.Y + _formatBlockHeight - 1) / _formatBlockHeight;
        var packedRowStride = checked(widthInBlocks * _formatElementSize);
        rowStride = rowStride != 0 ? rowStride : packedRowStride;
        sliceStride = sliceStride != 0 ? sliceStride : checked(rowStride * heightInBlocks);
        if (rowStride < packedRowStride)
            throw new ArgumentOutOfRangeException(nameof(rowStride));
        if (sliceStride < rowStride * heightInBlocks)
            throw new ArgumentOutOfRangeException(nameof(sliceStride));
        var isDataAligned = (rowStride & 3) == 0 && (sliceStride & 3) == 0;
        var requiredDataSize = isDataAligned
            ? checked(sliceStride * (size.Z - 1) + rowStride * heightInBlocks)
            : checked(rowStride * heightInBlocks * size.Z);
        if (data.Length < requiredDataSize)
            throw new ArgumentException("Texture update data is smaller than the specified region and strides.", nameof(data));

        var context = _renderDevice.GetImmediateContext()!;

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
                if (!isDataAligned)
                {
                    Log.GetLogger().LogWarning(
                        "RawTexture::Update is called with unaligned data with stride {RowStride} and depth stride {SliceStride}. " +
                        "The data is being repacked. Consider aligning the data rows to {Alignment} bytes.",
                        rowStride, sliceStride, 4);

                    var alignedRowStride = (rowStride + 3) & ~3;
                    var alignedSliceStride = checked(alignedRowStride * heightInBlocks);
                    scratchBuffer = GetRenderDevice()?.GetRenderPool().AllocateScratchBuffer(
                        checked(alignedSliceStride * size.Z)) ?? 0;
                    if (scratchBuffer == 0)
                        throw new OutOfMemoryException("Failed to allocate texture upload scratch buffer.");
                    var destination = (byte*)scratchBuffer;
                    var source = sourceData;
                    for (var z = 0; z < size.Z; ++z)
                    {
                        for (var y = 0; y < heightInBlocks; ++y)
                        {
                            Buffer.MemoryCopy(source, destination, alignedRowStride, rowStride);
                            destination += alignedRowStride;
                            source += rowStride;
                        }
                    }
                    uploadData = destination;
                    rowStride = alignedRowStride;
                    sliceStride = alignedSliceStride;
                }

                _updateSubresource.PData = (nint)uploadData;
                _updateSubresource.Stride = checked((ulong)rowStride);
                _updateSubresource.DepthStride = checked((ulong)sliceStride);
                context.UpdateTexture(_handles.Texture!, checked((uint)level), checked((uint)arraySlice), _updateBox, _updateSubresource,
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
    }

    public void Update(int level, Vector3i offset, Vector3i size, int arraySlice, byte[] data,
        int rowStride = 0, int sliceStride = 0)
    {
        ArgumentNullException.ThrowIfNull(data);
        Update(level, offset, size, arraySlice, data.AsSpan(), rowStride, sliceStride);
    }

    /// <summary>Read texture data from GPU. This operation is very slow and shouldn't be used in real time.</summary>
    public unsafe bool Read(int slice, int level, Span<byte> buffer)
    {
        if (!_handles.IsValid())
        {
            Log.GetLogger().LogWarning("RawTexture::Read is ignored for uninitialized texture");
            return false;
        }
        if (level >= _parameters.NumLevels)
        {
            Log.GetLogger().LogError("Trying to read invalid mip level {Level}", level);
            return false;
        }
        if (slice >= _parameters.ArraySize)
        {
            Log.GetLogger().LogError("Trying to read invalid array slice {Slice}", slice);
            return false;
        }
        var levelSize = GetMipLevelSize(_parameters.Size, level);
        var widthInBlocks = (levelSize.X + _formatBlockWidth - 1) / _formatBlockWidth;
        var heightInBlocks = (levelSize.Y + _formatBlockHeight - 1) / _formatBlockHeight;
        var rowSize = checked(widthInBlocks * _formatElementSize);
        var requiredSize = checked(rowSize * heightInBlocks * levelSize.Z);
        if (buffer.Length < requiredSize)
        {
            Log.GetLogger().LogError("Trying to read {SizeInBytes} bytes of texture to the buffer of size {BufferSize}",
                requiredSize, buffer.Length);
            return false;
        }
        if (_resolveDirty)
            Resolve();
        if (_levelsDirty)
            GenerateLevels();

        var device = _renderDevice!.GetRenderDevice()!;
        var context = _renderDevice.GetImmediateContext()!;

        using var stagingName = new CString("RawTexture::Read staging texture");
        using var stagingDesc = new TextureDesc
        {
            Name = stagingName.Pointer,
            Type = _parameters.Type == TextureType.Texture3D
                ? ResourceDimension.ResourceDimTex3d
                : ResourceDimension.ResourceDimTex2d,
            Usage = Usage.UsageStaging,
            CPUAccessFlags = CpuAccessFlags.CpuAccessRead,
            Format = _parameters.Format,
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
        {
            Log.GetLogger().LogError("Failed to create staging texture for RawTexture::Read");
            return false;
        }

        try
        {
            using var copy = new CopyTextureAttribs(
                _handles.ResolvedTexture ?? _handles.Texture!,
                ResourceStateTransitionMode.ResourceStateTransitionModeTransition,
                stagingTexture,
                ResourceStateTransitionMode.ResourceStateTransitionModeTransition)
            {
                SrcMipLevel = checked((uint)level),
                SrcSlice = checked((uint)slice)
            };
            context.CopyTexture(copy);
            context.WaitForIdle();

            using var mapped = new MappedTextureSubresource();
            context.MapTextureSubresource(stagingTexture, 0u, 0u, MapType.MapRead, MapFlags.MapFlagNone, null!, mapped);
            if (mapped.PData == IntPtr.Zero)
            {
                Log.GetLogger().LogError("Failed to map staging texture for RawTexture::Read");
                return false;
            }
            try
            {
                var sizeInBlocks = new Vector3i(
                    (_parameters.Size.X + _formatBlockWidth - 1) / _formatBlockWidth,
                    (_parameters.Size.Y + _formatBlockHeight - 1) / _formatBlockHeight,
                    _parameters.Size.Z);
                rowSize = checked(sizeInBlocks.X * _formatElementSize);
                var source = (byte*)mapped.PData;
                var sourceRowStride = (nuint)mapped.Stride;
                var sourceDepthStride = (nuint)mapped.DepthStride;
                fixed (byte* destinationStart = buffer)
                {
                    for (var z = 0; z < sizeInBlocks.Z; ++z)
                    {
                        for (var y = 0; y < sizeInBlocks.Y; ++y)
                        {
                            var sourceOffset = checked((nint)z * (nint)sourceDepthStride + (nint)y * (nint)sourceRowStride);
                            var destinationOffset = checked((nuint)((z * sizeInBlocks.Y + y) * rowSize));
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

    public bool Read(int slice, int level, byte[] buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        return Read(slice, level, buffer.AsSpan());
    }

    /// <summary>For render target and depth-stencil textures, mark shader resource view dirty.</summary>
    public void MarkDirty()
    {
        if (_parameters.NumLevels > 1u)
            _levelsDirty = true;
        if (_parameters.MultiSample > 1u && (_parameters.Flags & TextureFlag.NoMultiSampledAutoResolve) != TextureFlag.NoMultiSampledAutoResolve)
            _resolveDirty = true;
    }

    /// <summary>Evaluate approximate memory footprint of the texture on GPU.</summary>
    public ulong CalculateMemoryUseGPU()
    {
        if (!_handles.IsValid())
            return 0;
        ulong sliceMemory = 0;
        if (_handles.ResolvedTexture is not null)
        {
            sliceMemory += checked((ulong)_parameters.MultiSample) * GetMipLevelSizeInBytes(0);
        }
        for (var level = 0; level < _parameters.NumLevels; ++level)
            sliceMemory += GetMipLevelSizeInBytes(level);
        return checked((ulong)_parameters.ArraySize) * sliceMemory;
    }

    public override void Invalidate()
    {
        DestroyGPU();
    }

    public override void Restore()
    {
        if (_parameters.Size == default)
            _dataLost = false;
        else if (TryRestore())
            _dataLost = false;
        else
        {
            CreateGPU();
            _dataLost = true;
        }
    }

    public override void Destroy()
    {
        DestroyGPU();
    }

    public RawTextureParams GetParams() => _parameters;

    public SamplerStateDesc GetSamplerStateDesc() => _samplerStateDesc;

    public ref readonly RawTextureHandles GetHandles() => ref _handles;

    public bool GetLevelsDirty() => _levelsDirty;

    public bool GetResolveDirty() => _resolveDirty;

    /// <summary>Create empty texture.</summary>
    protected RawTexture()
    {
    }

    /// <summary>Validate parameters and create GPU texture.</summary>
    protected bool Create(RawTextureParams parameters)
    {
        // Optimize repeated calls.
        if (parameters == _parameters && _handles.IsValid())
            return true;

        Destroy();
        _parameters = parameters;

        if (!ValidateBindings(ref _parameters))
            return false;
        if (!ValidateDimensions(ref _parameters))
            return false;
        if (!ValidateMultiSample(ref _parameters))
            return false;
        if (!ValidateLevels(ref _parameters))
            return false;
        if (!ValidateCaps(ref _parameters, _renderDevice))
            return false;

        var nativeFormat = _parameters.Format;
        using (var formatInfo = TextureFormatAttribs.CreateManagedWrapper(
                   DiligentNativeBindings.GetTextureFormatAttribs(nativeFormat), false))
        {
            _formatBlockWidth = checked((int)formatInfo.BlockWidth);
            _formatBlockHeight = checked((int)formatInfo.BlockHeight);
            _formatElementSize = checked((int)formatInfo.GetElementSize());
        }
        if (_renderDevice?.GetRenderDevice() is null)
            return true;

        if (!CreateGPU())
        {
            ReleaseHandles();
            return false;
        }
        return true;
    }

    /// <summary>Create GPU texture from current parameters.</summary>
    protected bool CreateGPU()
    {
        var device = _renderDevice!.GetRenderDevice()!;

        using var nativeName = new CString(GetDebugName());
        using var textureDesc = new TextureDesc
        {
            Name = nativeName.Pointer,
            Type = GetResourceDimension(_parameters.Type),
            Usage = Usage.UsageDefault,
            Format = _parameters.Format,
            Width = checked((uint)_parameters.Size.X),
            Height = checked((uint)_parameters.Size.Y),
            MipLevels = checked((uint)_parameters.NumLevelsRtv),
            SampleCount = checked((uint)_parameters.MultiSample),
            BindFlags = BindFlags.BindShaderResource,
            ImmediateContextMask = 1
        };
        if (_parameters.Type == TextureType.Texture3D)
            textureDesc.Depth = checked((uint)_parameters.Size.Z);
        else
            textureDesc.ArraySize = checked((uint)_parameters.ArraySize);
        if ((_parameters.Flags & TextureFlag.BindRenderTarget) == TextureFlag.BindRenderTarget)
            textureDesc.BindFlags |= BindFlags.BindRenderTarget;
        if ((_parameters.Flags & TextureFlag.BindDepthStencil) == TextureFlag.BindDepthStencil)
            textureDesc.BindFlags |= BindFlags.BindDepthStencil;
        if ((_parameters.Flags & TextureFlag.BindUnorderedAccess) == TextureFlag.BindUnorderedAccess)
            textureDesc.BindFlags |= BindFlags.BindUnorderedAccess;
        if (_parameters.MultiSample == 1u && (_parameters.Flags & TextureFlag.BindRenderTarget) == TextureFlag.BindRenderTarget && _parameters.NumLevelsRtv != 1u)
            textureDesc.MiscFlags |= MiscTextureFlags.MiscTextureFlagGenerateMips;

        // Create main texture.
        // It is used as render target for auto-resolved multi-sampled texture.
        device.CreateTexture(textureDesc, null!, out _handles.Texture);
        if (_handles.Texture is null || _handles.Texture.IsNull)
        {
            _handles.Texture = null;
            Log.GetLogger().LogError(
                "Failed to create texture: type={Type} format={Format} size={Width}x{Height}x{Depth} " +
                "arraySize={ArraySize} numLevels={NumLevels} multiSample={MultiSample} flags=0b{Flags}",
                _parameters.Type, _parameters.Format, _parameters.Size.X, _parameters.Size.Y, _parameters.Size.Z,
                _parameters.ArraySize, _parameters.NumLevelsRtv, _parameters.MultiSample,
                Convert.ToString((int)_parameters.Flags, 2));
            return false;
        }

        // Create resolve texture if necessary.
        // It is used as shader resource for auto-resolved multi-sampled texture.
        if (_parameters.MultiSample != 1u && (_parameters.Flags & TextureFlag.NoMultiSampledAutoResolve) != TextureFlag.NoMultiSampledAutoResolve)
        {
            textureDesc.MipLevels = checked((uint)_parameters.NumLevels);
            textureDesc.SampleCount = 1u;
            if (_parameters.NumLevels != 1u)
                textureDesc.MiscFlags |= MiscTextureFlags.MiscTextureFlagGenerateMips;
            device.CreateTexture(textureDesc, null!, out _handles.ResolvedTexture);
            if (_handles.ResolvedTexture is null || _handles.ResolvedTexture.IsNull)
            {
                Log.GetLogger().LogError(
                    "Failed to create resolve texture: type={Type} format={Format} size={Width}x{Height}x{Depth} " +
                    "arraySize={ArraySize} numLevels={NumLevels} flags=0b{Flags}",
                    _parameters.Type, _parameters.Format, _parameters.Size.X, _parameters.Size.Y,
                    _parameters.Size.Z, _parameters.ArraySize, _parameters.NumLevels,
                    Convert.ToString((int)_parameters.Flags, 2));
                return false;
            }
        }

        if (!InitializeDefaultViews(textureDesc.BindFlags))
            return false;
        return true;
    }

    /// <summary>Destroy all GPU resources.</summary>
    protected void DestroyGPU()
    {
        OnDestroyGPU();
        ReleaseHandles();
    }

    /// <summary>Called when GPU handles are created.</summary>
    protected virtual void OnCreateGPU()
    {
    }

    /// <summary>Called when GPU handles are destroyed.</summary>
    protected virtual void OnDestroyGPU()
    {
    }

    /// <summary>Try to recover texture data after device loss.</summary>
    protected virtual bool TryRestore() => false;

    private bool InitializeDefaultViews(BindFlags bindFlags)
    {
        if ((bindFlags & BindFlags.BindShaderResource) != 0)
        {
            var shaderTexture = _handles.ResolvedTexture ?? _handles.Texture!;
            _handles.ShaderResourceView = GetDefaultView(shaderTexture, TextureViewType.TextureViewShaderResource);
            if (_handles.ShaderResourceView is null)
            {
                Log.GetLogger().LogError("Failed to create shader resource view for texture");
                return false;
            }
        }

        if ((bindFlags & BindFlags.BindRenderTarget) != 0)
        {
            _handles.RenderTargetView = GetDefaultView(_handles.Texture!, TextureViewType.TextureViewRenderTarget);
            if (_handles.RenderTargetView is null)
            {
                Log.GetLogger().LogError("Failed to create render target view for texture");
                return false;
            }
            if (!CreateRenderSurfaces(_handles.RenderTargetView, TextureViewType.TextureViewRenderTarget,
                    out _handles.RenderSurfaces))
            {
                Log.GetLogger().LogError("Failed to create render surfaces for texture");
                return false;
            }
        }

        if ((bindFlags & BindFlags.BindDepthStencil) != 0)
        {
            _handles.DepthStencilView = GetDefaultView(_handles.Texture!, TextureViewType.TextureViewDepthStencil);
            if (_handles.DepthStencilView is null)
            {
                Log.GetLogger().LogError("Failed to create depth-stencil view for texture");
                return false;
            }
            if (!CreateRenderSurfaces(_handles.DepthStencilView, TextureViewType.TextureViewDepthStencil,
                    out _handles.RenderSurfaces))
            {
                Log.GetLogger().LogError("Failed to create render surfaces for texture");
                return false;
            }

            using var depthStencilDesc = _handles.DepthStencilView.GetDesc();
            using var readOnlyDesc = depthStencilDesc.Clone();
            readOnlyDesc.ViewType = TextureViewType.TextureViewReadOnlyDepthStencil;
            _handles.Texture!.CreateView(readOnlyDesc, out _handles.ReadOnlyDepthStencilView);
            if (_handles.ReadOnlyDepthStencilView is null || _handles.ReadOnlyDepthStencilView.IsNull)
            {
                _handles.ReadOnlyDepthStencilView = null;
                Log.GetLogger().LogError("Failed to create read-only depth-stencil view for texture");
                return false;
            }
            if (!CreateRenderSurfaces(_handles.ReadOnlyDepthStencilView,
                    TextureViewType.TextureViewReadOnlyDepthStencil, out _handles.ReadOnlyRenderSurfaces))
            {
                Log.GetLogger().LogError("Failed to create read-only render surfaces for texture");
                return false;
            }
        }

        if ((bindFlags & BindFlags.BindUnorderedAccess) != 0)
        {
            _handles.UnorderedAccessView = GetDefaultView(_handles.Texture!, TextureViewType.TextureViewUnorderedAccess);
            if (_handles.UnorderedAccessView is null)
            {
                Log.GetLogger().LogError("Failed to create unordered access view for texture");
                return false;
            }
            _ = _handles.UnorderedAccessView.AddRef();
            _handles.UnorderedAccessViews[new RawTextureUAVKey()] = _handles.UnorderedAccessView;
        }

        OnCreateGPU();
        return true;
    }

    private ITextureView? GetDefaultView(ITexture texture, TextureViewType viewType)
    {
        var view = texture.GetDefaultView(viewType);
        if (view is not null && !view.IsNull)
        {
            _ = view.AddRef();
            return view;
        }

        return CreateView(texture, viewType);
    }

    private ITextureView? CreateView(ITexture texture, TextureViewType viewType, int firstSlice = 0, int numSlices = 0)
    {
        using var viewDesc = new TextureViewDesc
        {
            ViewType = viewType,
            Format = _parameters.Format
        };
        if (viewType == TextureViewType.TextureViewShaderResource)
        {
            using var textureDesc = texture.GetDesc();
            if ((textureDesc.MiscFlags & MiscTextureFlags.MiscTextureFlagGenerateMips) != 0)
                viewDesc.Flags |= TextureViewFlags.TextureViewFlagAllowMipMapGeneration;
        }
        if (viewType == TextureViewType.TextureViewUnorderedAccess)
            viewDesc.AccessFlags = UavAccessFlag.UavAccessFlagReadWrite;
        if (numSlices != 0u)
        {
            viewDesc.TextureDim = ResourceDimension.ResourceDimTex2dArray;
            viewDesc.FirstArraySlice = checked((uint)firstSlice);
            viewDesc.NumArraySlices = checked((uint)numSlices);
        }
        texture.CreateView(viewDesc, out var view);
        return view is { IsNull: false } ? view : null;
    }

    private bool CreateRenderSurfaces(ITextureView defaultView, TextureViewType viewType,
        out ITextureView[] renderSurfaces)
    {
        if (_parameters.Type == TextureType.Texture2D)
        {
            _ = defaultView.AddRef();
            renderSurfaces = [defaultView];
            return true;
        }
        if (_parameters.Type == TextureType.Texture3D)
        {
            renderSurfaces = [];
            return true;
        }

        var surfaces = new List<ITextureView>(_parameters.ArraySize);
        for (var slice = 0; slice < _parameters.ArraySize; ++slice)
        {
            var view = CreateView(_handles.Texture!, viewType, slice, 1);
            if (view is null)
            {
                Log.GetLogger().LogError("Failed to create texture view for render surface");
                renderSurfaces = surfaces.ToArray();
                return false;
            }
            surfaces.Add(view);
        }
        renderSurfaces = surfaces.ToArray();
        return true;
    }

    private void ReleaseHandles()
    {
        Release(_handles.Texture);
        Release(_handles.ResolvedTexture);
        Release(_handles.ShaderResourceView);
        Release(_handles.RenderTargetView);
        Release(_handles.DepthStencilView);
        Release(_handles.ReadOnlyDepthStencilView);
        Release(_handles.UnorderedAccessView);
        foreach (var view in _handles.RenderSurfaces)
            Release(view);
        foreach (var view in _handles.ReadOnlyRenderSurfaces)
            Release(view);
        foreach (var view in _handles.UnorderedAccessViews.Values)
            Release(view);
        _handles = new RawTextureHandles();
    }

    private ITextureView? CreateUnorderedAccessView(RawTextureUAVKey key)
    {
        if (_handles.Texture is null)
            return null;

        using var name = new CString(
            $"{GetDebugName()}:{key.FirstSlice}-{key.FirstSlice + key.NumSlices}:" +
            $"{key.FirstLevel}-{key.FirstLevel + key.NumLevels}:" +
            $"{(key.CanRead ? 'r' : ' ')}{(key.CanWrite ? 'w' : ' ')}");
        using var viewDesc = new TextureViewDesc
        {
            Name = name.Pointer,
            ViewType = TextureViewType.TextureViewUnorderedAccess,
            TextureDim = GetViewResourceDimension(_parameters.Type),
            Format = _parameters.Format,
            MostDetailedMip = checked((uint)key.FirstLevel),
            NumMipLevels = checked((uint)key.NumLevels),
            AccessFlags = (key.CanRead ? UavAccessFlag.UavAccessFlagRead : UavAccessFlag.UavAccessUnspecified) |
                (key.CanWrite ? UavAccessFlag.UavAccessFlagWrite : UavAccessFlag.UavAccessUnspecified)
        };
        if (_parameters.Type == TextureType.Texture3D)
        {
            viewDesc.FirstDepthSlice = checked((uint)key.FirstSlice);
            viewDesc.NumDepthSlices = checked((uint)key.NumSlices);
        }
        else
        {
            viewDesc.FirstArraySlice = checked((uint)key.FirstSlice);
            viewDesc.NumArraySlices = checked((uint)key.NumSlices);
        }
        _handles.Texture.CreateView(viewDesc, out var view);
        if (view is null || view.IsNull)
        {
            Log.GetLogger().LogError("Failed to create UAV for texture");
            return null;
        }
        return view;
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

    private static bool ValidateBindings(ref RawTextureParams parameters)
    {
        var isRenderTarget = (parameters.Flags & TextureFlag.BindRenderTarget) != 0;
        var isDepthStencil = (parameters.Flags & TextureFlag.BindDepthStencil) != 0;
        if (isRenderTarget && isDepthStencil)
        {
            Log.GetLogger().LogError("Texture cannot be both render target and depth-stencil");
            return false;
        }

        return true;
    }

    private static bool ValidateMultiSample(ref RawTextureParams parameters)
    {
        parameters = parameters with { MultiSample = Math.Clamp(parameters.MultiSample, 1, 16) };
        if (parameters.MultiSample == 1)
            parameters = parameters with { Flags = parameters.Flags | TextureFlag.NoMultiSampledAutoResolve };

        var isRenderTarget = (parameters.Flags & TextureFlag.BindRenderTarget) != 0;
        var isDepthStencil = (parameters.Flags & TextureFlag.BindDepthStencil) != 0;
        if (parameters.MultiSample != 1 && !(isRenderTarget || isDepthStencil))
        {
            Log.GetLogger().LogError(
                "Multi-sampling is only supported for render target or depth-stencil textures");
            return false;
        }

        return true;
    }

    private static bool ValidateLevels(ref RawTextureParams parameters)
    {
        var maxLevels = CalculateMipLevelCount(parameters.Size);
        var numLevels = parameters.NumLevels != 0 ? Math.Min(parameters.NumLevels, maxLevels) : maxLevels;

        if (numLevels > 1 && (parameters.Flags & TextureFlag.BindDepthStencil) != 0)
        {
            Log.GetLogger().LogWarning("Depth-stencil texture cannot have mipmaps.");
            numLevels = 1;
        }

        if (numLevels > 1 && parameters.MultiSample != 1
            && (parameters.Flags & TextureFlag.NoMultiSampledAutoResolve) != 0)
        {
            Log.GetLogger().LogWarning("Multi-sampled texture cannot have mipmaps.");
            numLevels = 1;
        }

        parameters = parameters with
        {
            NumLevels = numLevels,
            NumLevelsRtv = parameters.MultiSample != 1 ? 1 : numLevels
        };
        return true;
    }

    private static bool ValidateCaps(ref RawTextureParams parameters, RenderDevice? renderDevice)
    {
        var device = renderDevice?.GetRenderDevice();
        if (device is null)
            return true;
        var nativeFormat = parameters.Format;

        if (parameters.Format == TextureFormat.TexFormatD24UnormS8Uint)
        {
            using var depth24Info = device.GetTextureFormatInfoExt(nativeFormat);
            if ((depth24Info.BindFlags & BindFlags.BindDepthStencil) == 0)
            {
                parameters = parameters with { Format = TextureFormat.TexFormatD32FloatS8x24Uint };
                nativeFormat = parameters.Format;
            }
        }

        using var formatInfo = device.GetTextureFormatInfoExt(nativeFormat);
        var allowedFlags = formatInfo.BindFlags;

        if (parameters.MultiSample != 1 && ((int)formatInfo.SampleCounts & parameters.MultiSample) == 0)
        {
            Log.GetLogger().LogWarning(
                "Multi-sampling is not supported for this texture format, demoting to simple texture.");
            parameters = parameters with
            {
                MultiSample = 1,
                Flags = parameters.Flags | TextureFlag.NoMultiSampledAutoResolve
            };
        }

        if ((parameters.Flags & TextureFlag.BindRenderTarget) != 0
            && (allowedFlags & BindFlags.BindRenderTarget) == 0)
        {
            Log.GetLogger().LogWarning("Render target binding is not supported for this texture format.");
            return false;
        }

        if ((parameters.Flags & TextureFlag.BindDepthStencil) != 0
            && (allowedFlags & BindFlags.BindDepthStencil) == 0)
        {
            Log.GetLogger().LogWarning("Depth-stencil binding is not supported for this texture format.");
            return false;
        }

        if ((parameters.Flags & TextureFlag.BindUnorderedAccess) != 0
            && (allowedFlags & BindFlags.BindUnorderedAccess) == 0)
        {
            Log.GetLogger().LogWarning("Unordered access binding is not supported for this texture format.");
            return false;
        }

        return true;
    }

    private static bool ValidateDimensions(ref RawTextureParams parameters)
    {
        switch (parameters.Type)
        {
            case TextureType.Texture2D:
                if (parameters.Size.X <= 0 || parameters.Size.Y <= 0)
                {
                    Log.GetLogger().LogError("Zero or negative texture dimensions");
                    return false;
                }
                parameters = parameters with { Size = new Vector3i(parameters.Size.X, parameters.Size.Y, 1), ArraySize = 1 };
                return true;
            case TextureType.TextureCube:
                if (parameters.Size.X <= 0)
                {
                    Log.GetLogger().LogError("Zero or negative texture dimensions");
                    return false;
                }
                parameters = parameters with { Size = new Vector3i(parameters.Size.X, parameters.Size.X, 1), ArraySize = 6 };
                return true;
            case TextureType.Texture3D:
                if (parameters.Size.X <= 0 || parameters.Size.Y <= 0 || parameters.Size.Z <= 0)
                {
                    Log.GetLogger().LogError("Zero or negative texture dimensions");
                    return false;
                }
                parameters = parameters with { ArraySize = 1 };
                return true;
            case TextureType.Texture2DArray:
                if (parameters.Size.X <= 0 || parameters.Size.Y <= 0 || parameters.ArraySize == 0)
                {
                    Log.GetLogger().LogError("Zero or negative texture dimensions");
                    return false;
                }
                parameters = parameters with { Size = new Vector3i(parameters.Size.X, parameters.Size.Y, 1) };
                return true;
            default:
                throw new ArgumentOutOfRangeException(nameof(parameters.Type));
        }
    }

    private static int CalculateMipLevelCount(Vector3i size)
    {
        var largestDimension = Math.Max(size.X, Math.Max(size.Y, size.Z));
        var levels = 1;
        while (largestDimension > 1)
        {
            largestDimension >>= 1;
            ++levels;
        }
        return levels;
    }

    private static Vector3i GetMipLevelSize(Vector3i size, int level) => new(
        Math.Max(size.X >> level, 1),
        Math.Max(size.Y >> level, 1),
        Math.Max(size.Z >> level, 1));

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

    private ulong GetMipLevelSizeInBytes(int level)
    {
        var size = GetMipLevelSize(_parameters.Size, level);
        var widthInBlocks = (size.X + _formatBlockWidth - 1) / _formatBlockWidth;
        var heightInBlocks = (size.Y + _formatBlockHeight - 1) / _formatBlockHeight;
        return checked((ulong)widthInBlocks * (ulong)heightInBlocks * (ulong)size.Z * (ulong)_formatElementSize);
    }

    private RawTextureParams _parameters;
    private SamplerStateDesc _samplerStateDesc = new();

    private RawTextureHandles _handles = new();
    private ResolveTextureSubresourceAttribs? _resolveAttributes;

    private bool _levelsDirty;
    private bool _resolveDirty;

    private int _formatBlockWidth;
    private int _formatBlockHeight;
    private int _formatElementSize;
    private readonly Box _updateBox = new();
    private readonly TextureSubResData _updateSubresource = new();
}
