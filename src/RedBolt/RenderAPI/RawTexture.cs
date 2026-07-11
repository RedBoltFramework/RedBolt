using RedBolt.Bindings.Diligent;

namespace RedBolt;

/// <summary>Common class for all GPU textures.
/// By default RawTexture loses data on device lost and does not attempt to recover it.</summary>
public sealed class RawTexture : DeviceObject
{
    private RawTextureParams _parameters;
    private SamplerStateDesc _samplerStateDesc = new();
    private Texture? _texture;
    private readonly Dictionary<RawTextureUAVKey, ITextureView> _uavs = [];
    private bool _levelsDirty;
    private bool _resolveDirty;

    public RawTexture()
    {
    }

    public RawTexture(RawTextureParams parameters)
    {
        if (!Create(parameters))
            throw new InvalidOperationException($"Failed to create raw texture for type '{parameters.Type}' and format '{parameters.Format}'.");
    }

    /// <summary>Validate parameters and create GPU texture.</summary>
    public bool Create(RawTextureParams parameters)
    {
        if (!TryCreateManagedTexture(parameters, out var createdTexture, out var finalizedParameters))
        {
            _texture = null;
            this._parameters = default;
            return false;
        }

        _texture = createdTexture;
        this._parameters = finalizedParameters;
        _uavs.Clear();
        ApplySamplerState();
        SetDebugName($"RawTexture #{parameters.GetHashCode():X8}");
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

    public Texture? GetTexture() => _texture;

    public ITexture? GetHandle()
    {
        var renderDevice = GetRenderDevice();
        return _texture is not null ? renderDevice?.GetTextureHandle(_texture) : null;
    }

    public bool IsValid() => _texture is not null;

    public bool GetLevelsDirty() => _levelsDirty;

    public bool GetResolveDirty() => _resolveDirty;

    public void GenerateLevels()
    {
        if (!_levelsDirty || _texture is null || _parameters.NumLevels <= 1u)
            return;
        var handle = GetHandle();
        var context = GetSubsystem<Graphics>()?.GetImmediateContext();
        var view = handle?.GetDefaultView(TextureViewType.TextureViewShaderResource);
        if (context is not null && view is not null)
            context.GenerateMips(view);
        _levelsDirty = false;
    }

    public void Resolve()
    {
        if (!_resolveDirty) return;
        // Managed Texture owns the auto-resolve representation used by Graphics.
        // Explicit resolve is only required as a synchronization point here.
        _resolveDirty = false;
    }

    public void Update(uint level, Vector3i offset, Vector3i size, uint arraySlice, ReadOnlySpan<byte> data,
        uint rowStride = 0u, uint sliceStride = 0u)
    {
        if (_texture is null) throw new InvalidOperationException("Raw texture is not initialized.");
        if (level >= _parameters.NumLevels || arraySlice >= _parameters.ArraySize)
            throw new ArgumentOutOfRangeException(level >= _parameters.NumLevels ? nameof(level) : nameof(arraySlice));
        if (rowStride != 0u && rowStride < _texture.GetRowDataSize(size.X))
            throw new ArgumentOutOfRangeException(nameof(rowStride));
        if (sliceStride != 0u && sliceStride < rowStride * (uint)Math.Max(size.Y, 1))
            throw new ArgumentOutOfRangeException(nameof(sliceStride));

        var success = _texture switch
        {
            Texture2D texture => texture.SetData(level, offset.X, offset.Y, size.X, size.Y, data),
            Texture3D texture => texture.SetData(level, offset.X, offset.Y, offset.Z, size.X, size.Y, size.Z, data),
            TextureCube texture => texture.SetData((CubeMapFace)arraySlice, level, offset.X, offset.Y, size.X, size.Y, data),
            Texture2DArray texture => texture.SetData(arraySlice, level, offset.X, offset.Y, size.X, size.Y, data),
            _ => false
        };
        if (!success) throw new InvalidOperationException("Failed to update raw texture subresource.");
        MarkDirty();
    }

    public void Update(uint level, Vector3i offset, Vector3i size, uint arraySlice, byte[] data,
        uint rowStride = 0u, uint sliceStride = 0u)
    {
        ArgumentNullException.ThrowIfNull(data);
        Update(level, offset, size, arraySlice, data.AsSpan(), rowStride, sliceStride);
    }

    public bool Read(uint slice, uint level, Span<byte> buffer)
    {
        if (_texture is null || level >= _parameters.NumLevels || slice >= _parameters.ArraySize)
            return false;
        var data = _texture switch
        {
            Texture2D texture when slice == 0 => texture.GetData(level),
            Texture3D texture when slice == 0 => texture.GetData(level),
            TextureCube texture => texture.GetData((CubeMapFace)slice, level),
            Texture2DArray texture => texture.GetData(slice, level),
            _ => null
        };
        if (data is null || buffer.Length < data.Length) return false;
        data.CopyTo(buffer);
        return true;
    }

    public bool Read(uint slice, uint level, byte[] buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        return Read(slice, level, buffer.AsSpan());
    }

    public ulong CalculateMemoryUseGPU()
    {
        if (_texture is null) return 0;
        ulong total = 0;
        for (var level = 0u; level < _parameters.NumLevels; ++level)
        {
            total += _texture.GetDataSize(
                _texture.GetLevelWidth(level), _texture.GetLevelHeight(level),
                _parameters.Type == TextureType.Texture3D ? _texture.GetLevelDepth(level) : 1);
        }
        total *= _parameters.Type is TextureType.TextureCube ? 6u : _parameters.ArraySize;
        total *= Math.Max(_parameters.MultiSample, 1u);
        return total;
    }

    /// <summary>Create UAV for given array slices and mip levels.</summary>
    public ITextureView? CreateUAV(RawTextureUAVKey key)
    {
        if (_texture is null || !_parameters.Flags.HasFlag(TextureFlag.BindUnorderedAccess))
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

        var createdView = GetRenderDevice()?.CreateTextureUnorderedAccessView(this, effectiveKey);
        if (createdView is null || createdView.IsNull)
            return null;

        _uavs[key] = createdView;
        _uavs[effectiveKey] = createdView;
        return createdView;
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

        RenderTargetView.Texture(this).MarkDirty();
        if (_parameters.MultiSample > 1u && !_parameters.Flags.HasFlag(TextureFlag.NoMultiSampledAutoResolve))
            _resolveDirty = true;
        else if (_parameters.NumLevels > 1u)
            _levelsDirty = true;
    }

    public override void Invalidate()
    {
        base.Invalidate();
        _uavs.Clear();
        _levelsDirty = false;
        _resolveDirty = false;
    }

    public override void Restore()
    {
        base.Restore();
        _uavs.Clear();
        _levelsDirty = false;
        _resolveDirty = false;
    }

    public override void Destroy()
    {
        base.Destroy();
        _uavs.Clear();
        _texture = null;
        _levelsDirty = false;
        _resolveDirty = false;
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

        var normalizedArraySize = Math.Max(parameters.ArraySize, 1u);
        var normalizedMultiSample = Math.Max(parameters.MultiSample, 1u);
        var normalizedSize = new Vector3i(
            Math.Max(parameters.Size.X, 1),
            Math.Max(parameters.Size.Y, 1),
            Math.Max(parameters.Size.Z, 1));

        Texture textureCandidate;
        switch (parameters.Type)
        {
            case TextureType.Texture2D:
            {
                var texture2D = new Texture2D($"RawTexture2D.{Guid.NewGuid():N}");
                texture2D.SetNumLevels(parameters.NumLevels);
                if (!texture2D.SetSize(normalizedSize.X, normalizedSize.Y, parameters.Format, (int)normalizedMultiSample,
                    parameters.Flags.HasFlag(TextureFlag.NoMultiSampledAutoResolve)))
                    return false;
                textureCandidate = texture2D;
                break;
            }

            case TextureType.TextureCube:
            {
                var textureCube = new TextureCube($"RawTextureCube.{Guid.NewGuid():N}");
                textureCube.SetNumLevels(parameters.NumLevels);
                var edge = Math.Max(normalizedSize.X, normalizedSize.Y);
                if (!textureCube.SetSize(edge, parameters.Format, (int)normalizedMultiSample,
                    parameters.Flags.HasFlag(TextureFlag.NoMultiSampledAutoResolve)))
                    return false;
                normalizedSize = new Vector3i(edge, edge, 1);
                textureCandidate = textureCube;
                break;
            }

            case TextureType.Texture2DArray:
            {
                var textureArray = new Texture2DArray($"RawTextureArray.{Guid.NewGuid():N}");
                textureArray.SetNumLevels(parameters.NumLevels);
                if (!textureArray.SetLayers(normalizedArraySize, normalizedSize.X, normalizedSize.Y, parameters.Format, (int)normalizedMultiSample,
                    parameters.Flags.HasFlag(TextureFlag.NoMultiSampledAutoResolve)))
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

    private static uint CalculateNumLevelsRtv(Texture texture, RawTextureParams parameters)
    {
        if (parameters.NumLevelsRtv != 0u)
            return parameters.NumLevelsRtv;

        return texture.MultiSample > 1 && !parameters.Flags.HasFlag(TextureFlag.NoMultiSampledAutoResolve)
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
