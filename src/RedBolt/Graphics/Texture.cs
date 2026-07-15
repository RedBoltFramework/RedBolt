namespace RedBolt;

public enum TextureFormat
{
    Unknown,
    Depth16,
    Depth32,
    Depth24Stencil8,
    Depth32Stencil8,
    R8,
    RG16F,
    RGB8,
    RGBA8,
    RGBA16F
}

internal static class TextureFormatUtility
{
    public static bool IsFloatColorFormat(TextureFormat format)
    {
        return format is TextureFormat.RG16F or TextureFormat.RGBA16F;
    }
}

/// <summary>Base class for texture resources.</summary>
/// <remarks>
/// </remarks>
public abstract class Texture : ResourceWithMetadata
{
    private readonly Dictionary<TextureCoordinate, TextureAddressMode> _addressModes = new()
    {
        [TextureCoordinate.U] = TextureAddressMode.Wrap,
        [TextureCoordinate.V] = TextureAddressMode.Wrap,
        [TextureCoordinate.W] = TextureAddressMode.Wrap
    };

    private readonly uint[] _mipsToSkip = [2u, 1u, 0u];
    protected uint _requestedLevels;
    protected bool _requestedSRGB;
    protected bool _resolveDirty;
    protected bool _levelsDirty;
    protected TextureFlag _textureFlags;
    protected RenderSurface?[] _renderSurfaces = [];
    protected uint _mostDetailedLevel;
    private ContextEventSubscription? _renderSurfaceUpdateSubscription;
    private RawTexture? _rawTexture;

    protected Texture(string name)
        : base(name)
    {
    }

    public TextureFormat Format { get; protected set; } = TextureFormat.Unknown;

    public uint Levels { get; protected set; }

    public int Width { get; protected set; }

    public int Height { get; protected set; }

    public int Depth { get; protected set; } = 1;

    public int MultiSample { get; protected set; } = 1;

    public bool NoMultiSampledAutoResolve { get; protected set; }

    public TextureFilterMode FilterMode { get; private set; } = TextureFilterMode.Default;

    public uint Anisotropy { get; private set; }

    public float MinLod { get; private set; }

    public float MaxLod { get; private set; } = float.MaxValue;

    public bool ShadowCompare { get; private set; }

    public bool Linear { get; private set; }

    public bool SRGB
    {
        get => _requestedSRGB;
        private set => _requestedSRGB = value;
    }

    public Texture? BackupTexture { get; private set; }

    public RawTexture? GetRawTexture() => _rawTexture;

    public bool IsCompressed() => false;

    public void SetFilterMode(TextureFilterMode mode)
    {
        FilterMode = mode;
        ApplyRawSamplerState();
    }

    public TextureFilterMode GetFilterMode() => FilterMode;

    public void SetAddressMode(TextureCoordinate coordinate, TextureAddressMode mode)
    {
        _addressModes[coordinate] = mode;
        ApplyRawSamplerState();
    }

    public TextureAddressMode GetAddressMode(TextureCoordinate coordinate) => _addressModes.GetValueOrDefault(coordinate, TextureAddressMode.Wrap);

    public void SetAnisotropy(uint level)
    {
        Anisotropy = level;
        ApplyRawSamplerState();
    }

    public uint GetAnisotropy() => Anisotropy;

    public void SetShadowCompare(bool enable)
    {
        ShadowCompare = enable;
        ApplyRawSamplerState();
    }

    public bool GetShadowCompare() => ShadowCompare;

    public void SetMinLod(float minLod)
    {
        MinLod = minLod;
        ApplyRawSamplerState();
    }

    public float GetMinLod() => MinLod;

    public void SetMaxLod(float maxLod)
    {
        MaxLod = maxLod;
        ApplyRawSamplerState();
    }

    public float GetMaxLod() => MaxLod;

    public SamplerStateDesc GetSamplerStateDesc() => _rawTexture!.GetSamplerStateDesc();

    public void SetNumLevels(uint levels)
    {
        _requestedLevels = levels;
    }

    public uint GetLevels() => Levels;

    public void SetLinear(bool linear)
    {
        Linear = linear;
    }

    public bool GetLinear() => Linear;

    public void SetSRGB(bool enable)
    {
        SRGB = enable;
    }

    public bool GetSRGB() => SRGB;

    public void SetBackupTexture(Texture? texture)
    {
        BackupTexture = texture;
    }

    public Texture? GetBackupTexture() => BackupTexture;

    public void SetMipsToSkip(MaterialQuality quality, int toSkip)
    {
        var index = (uint)quality;
        if (index >= _mipsToSkip.Length)
            return;

        _mipsToSkip[index] = (uint)toSkip;
        for (var i = 1; i < _mipsToSkip.Length; ++i)
            _mipsToSkip[i] = Math.Min(_mipsToSkip[i], _mipsToSkip[i - 1]);
    }

    public int GetMipsToSkip(MaterialQuality quality)
    {
        var index = (uint)quality;
        return index < _mipsToSkip.Length ? (int)_mipsToSkip[index] : 0;
    }

    public int GetLevelWidth(uint level) => level > Levels ? 0 : GetLevelDimension(Width, level);

    public int GetLevelHeight(uint level) => level > Levels ? 0 : GetLevelDimension(Height, level);

    public int GetLevelDepth(uint level) => level > Levels ? 0 : GetLevelDimension(Depth, level);

    public int GetMultiSample() => MultiSample;

    public bool GetNoMultiSampledAutoResolve() => NoMultiSampledAutoResolve;

    public bool GetAutoResolve() => !NoMultiSampledAutoResolve;

    public Vector2i GetSize() => new(Width, Height);

    public IntRect GetRect() => new(0, 0, Width, Height);

    public RenderSurface? GetRenderSurface(uint index = 0) => index < _renderSurfaces.Length ? _renderSurfaces[index] : null;

    public bool IsRenderTarget() => (_textureFlags & TextureFlag.BindRenderTarget) != 0;

    public bool IsDepthStencil() => (_textureFlags & TextureFlag.BindDepthStencil) != 0;

    public bool IsUnorderedAccess() => (_textureFlags & TextureFlag.BindUnorderedAccess) != 0;

    public uint GetComponents()
    {
        if (Width == 0 || IsCompressed())
            return 0u;

        return Format switch
        {
            TextureFormat.Depth16 => 2u,
            TextureFormat.Depth32 => 4u,
            TextureFormat.Depth24Stencil8 => 4u,
            TextureFormat.Depth32Stencil8 => 8u,
            TextureFormat.R8 => 1u,
            TextureFormat.RG16F => 4u,
            TextureFormat.RGB8 => 3u,
            TextureFormat.RGBA8 => 4u,
            TextureFormat.RGBA16F => 8u,
            _ => 0u
        };
    }

    public uint GetDataSize(int width, int height) => GetRowDataSize(width) * (uint)height;

    public uint GetDataSize(int width, int height, int depth)
    {
        return (uint)depth * GetDataSize(width, height);
    }

    public uint GetRowDataSize(int width)
    {
        return (uint)width * GetComponents();
    }

    public void SetParameters(XMLFile? file)
    {
        if (file is null)
            return;

        SetParameters(file.Root);
    }

    public void SetParameters(XMLElement element)
    {
        LoadMetadataFromXML(element);
        for (var paramElement = element.GetChild(); paramElement.NotNull; paramElement = paramElement.GetNext())
        {
            var name = paramElement.Name;

            if (name == "address")
            {
                var coordinate = paramElement.GetAttribute("coord").ToLowerInvariant();
                if (coordinate.Length >= 1)
                {
                    var coordinateIndex = (TextureCoordinate)(coordinate[0] - 'u');
                    var mode = paramElement.GetAttribute("mode").ToLowerInvariant() switch
                    {
                        "mirror" => TextureAddressMode.Mirror,
                        "clamp" => TextureAddressMode.Clamp,
                        "border" => TextureAddressMode.Clamp,
                        _ => TextureAddressMode.Wrap
                    };
                    SetAddressMode(coordinateIndex, mode);
                }
            }

            if (name == "filter")
            {
                var mode = paramElement.GetAttribute("mode").ToLowerInvariant() switch
                {
                    "nearest" => TextureFilterMode.Nearest,
                    "bilinear" => TextureFilterMode.Bilinear,
                    "trilinear" => TextureFilterMode.Trilinear,
                    "anisotropic" => TextureFilterMode.Anisotropic,
                    "nearest_anisotropic" => TextureFilterMode.NearestAnisotropic,
                    _ => TextureFilterMode.Default
                };
                SetFilterMode(mode);
                if (paramElement.HasAttribute("anisotropy"))
                    SetAnisotropy(paramElement.GetUInt("anisotropy"));
            }

            if (name == "mipmap")
            {
                SetNumLevels(paramElement.GetBool("enable") ? 0u : 1u);
                if (paramElement.HasAttribute("minlod"))
                    SetMinLod(paramElement.GetFloat("minlod"));
                if (paramElement.HasAttribute("maxlod"))
                    SetMaxLod(paramElement.GetFloat("maxlod"));
            }

            if (name == "quality")
            {
                if (paramElement.HasAttribute("low"))
                    SetMipsToSkip(MaterialQuality.Low, paramElement.GetInt("low"));
                if (paramElement.HasAttribute("med"))
                    SetMipsToSkip(MaterialQuality.Medium, paramElement.GetInt("med"));
                if (paramElement.HasAttribute("medium"))
                    SetMipsToSkip(MaterialQuality.Medium, paramElement.GetInt("medium"));
                if (paramElement.HasAttribute("high"))
                    SetMipsToSkip(MaterialQuality.High, paramElement.GetInt("high"));
            }

            if (name == "srgb")
                SetSRGB(paramElement.GetBool("enable"));

            if (name == "linear")
                SetLinear(paramElement.GetBool("enable"));
        }
    }

    private void OnCreateGPU()
    {
        ApplyRawSamplerState();
        if (_rawTexture is not null)
            RefreshMemoryUse((uint)Math.Min(_rawTexture.CalculateMemoryUseGPU(), uint.MaxValue));
    }

    private void OnDestroyGPU()
    {
        foreach (var renderSurface in _renderSurfaces)
            renderSurface?.Invalidate();
        _rawTexture?.Dispose();
        _rawTexture = null;
    }

    private bool TryRestore()
    {
        var cache = GetSubsystem<ResourceCache>()!;
        if (cache.Exists(Name))
            return cache.ReloadResource(this);
        return false;
    }

    private void HandleRenderSurfaceUpdate()
    {
        var renderer = GetSubsystem<Renderer>();
        foreach (var renderSurface in _renderSurfaces)
        {
            if (renderSurface is null)
                continue;

            if (renderSurface.GetUpdateMode() == RenderSurfaceUpdateMode.UpdateAlways || renderSurface.IsUpdateQueued())
            {
                renderer?.QueueRenderSurface(renderSurface);
                renderSurface.ResetUpdateQueued();
            }
        }
    }

    protected void CheckTextureBudget(StringHash type)
    {
        var cache = GetSubsystem<ResourceCache>()!;
        var textureBudget = cache.GetMemoryBudget(type);
        var textureUse = cache.GetMemoryUse(type);
        if (textureBudget == 0)
            return;

        if (textureUse > textureBudget)
            cache.ReleaseResources<Material>();
    }

    protected bool CreateForImage(RawTextureParams baseParams, Image? image)
    {
        if (image is null)
            return false;

        var quality = GetSubsystem<Renderer>()?.GetTextureQuality() ?? MaterialQuality.High;
        var maxLevels = image.IsCompressed()
            ? Math.Max(image.NumCompressedLevels, 1u)
            : CalculateFullMipChainLength(Math.Max(image.Width, Math.Max(image.Height, image.Depth)));
        var mostDetailedLevel = Math.Min((uint)GetMipsToSkip(quality), maxLevels - 1u);
        var numLevels = baseParams.NumLevels == 0
            ? maxLevels - mostDetailedLevel
            : Math.Min(baseParams.NumLevels, maxLevels - mostDetailedLevel);
        var baseImage = image.GetDecompressedImageLevel(mostDetailedLevel);
        if (baseImage is null)
            return false;

        _mostDetailedLevel = mostDetailedLevel;
        var parameters = baseParams with
        {
            Size = new Vector3i(baseImage.Width, baseImage.Height, baseImage.Depth),
            NumLevels = numLevels,
            Format = baseImage.GetGPUFormat(),
            SRGB = _requestedSRGB
        };

        OnDestroyGPU();
        _rawTexture = new RawTexture(parameters);
        Format = parameters.Format;
        Width = parameters.Size.X;
        Height = parameters.Size.Y;
        Depth = parameters.Size.Z;
        Levels = parameters.NumLevels;
        MultiSample = (int)parameters.MultiSample;
        _textureFlags = parameters.Flags;
        return true;
    }

    protected bool UpdateFromImage(uint arraySlice, Image? image)
    {
        if (image is null || _rawTexture is null)
            return false;

        for (var level = 0u; level < Levels; ++level)
        {
            var mipImage = image.GetDecompressedImageLevel(_mostDetailedLevel + level);
            if (mipImage is null)
                return false;

            var size = new Vector3i(mipImage.Width, mipImage.Height, mipImage.Depth);
            _rawTexture.Update(level, Vector3i.Zero, size, arraySlice, mipImage.GetData());
        }

        return true;
    }

    protected bool ReadToImage(uint arraySlice, uint level, Image? image)
    {
        if (image is null || _rawTexture is null || Format != TextureFormat.RGBA8)
            return false;

        var size = new Vector3i(GetLevelWidth(level), GetLevelHeight(level), GetLevelDepth(level));
        if (!image.SetSize(size.X, size.Y, size.Z, 4u))
            return false;

        return _rawTexture.Read(arraySlice, level, image.GetData());
    }

    protected void SetDimensions(int width, int height, int depth, TextureFormat format, int multiSample = 1,
        bool noMultiSampledAutoResolve = false, TextureFlag flags = TextureFlag.None)
    {
        Width = Math.Max(width, 0);
        Height = Math.Max(height, 0);
        Depth = Math.Max(depth, 1);
        MultiSample = Math.Max(multiSample, 1);
        NoMultiSampledAutoResolve = noMultiSampledAutoResolve;
        _textureFlags = flags | (noMultiSampledAutoResolve ? TextureFlag.NoMultiSampledAutoResolve : TextureFlag.None);
        Format = format;
        Levels = _requestedLevels == 0
            ? CalculateFullMipChainLength(Math.Max(Width, Math.Max(Height, Depth)))
            : Math.Max(_requestedLevels, 1u);

        if ((flags & TextureFlag.BindRenderTarget) != 0)
        {
            _renderSurfaceUpdateSubscription ??= SubscribeToEvent<GraphicsEvents.RenderSurfaceUpdateEvent>(
                (ref GraphicsEvents.RenderSurfaceUpdateEvent _) => HandleRenderSurfaceUpdate());
        }
        else
        {
            _renderSurfaceUpdateSubscription?.Dispose();
            _renderSurfaceUpdateSubscription = null;
        }

        SetRenderSurfaceCount((flags & (TextureFlag.BindRenderTarget | TextureFlag.BindDepthStencil)) != 0
            ? (uint)Math.Max(Depth, 1)
            : 0u);

        OnDestroyGPU();
        if (!RawTexture.IsCreatingManagedTexture)
        {
            _rawTexture = new RawTexture(new RawTextureParams(
                GetRawTextureType(), Format, _textureFlags,
                new Vector3i(Width, Height, Depth), GetRawTextureArraySize(), Levels, (uint)MultiSample, SRGB: _requestedSRGB));
            OnCreateGPU();
        }
    }

    protected virtual TextureType GetRawTextureType() => TextureType.Texture2D;

    protected virtual uint GetRawTextureArraySize() => 1u;

    private void ApplyRawSamplerState()
    {
        if (_rawTexture is null)
            return;

        var samplerStateDesc = _rawTexture.GetSamplerStateDesc();
        samplerStateDesc.FilterMode = FilterMode;
        samplerStateDesc[TextureCoordinate.U] = GetAddressMode(TextureCoordinate.U);
        samplerStateDesc[TextureCoordinate.V] = GetAddressMode(TextureCoordinate.V);
        samplerStateDesc[TextureCoordinate.W] = GetAddressMode(TextureCoordinate.W);
        samplerStateDesc.Anisotropy = checked((byte)Math.Min(Anisotropy, byte.MaxValue));
        samplerStateDesc.ShadowCompare = ShadowCompare;
        samplerStateDesc.MinLod = MinLod;
        samplerStateDesc.MaxLod = MaxLod;
        _rawTexture.SetSamplerStateDesc(samplerStateDesc);
    }

    protected void SetRenderSurfaceCount(uint count)
    {
        if (_renderSurfaces.Length == count)
            return;

        _renderSurfaces = new RenderSurface?[count];
        for (var i = 0u; i < count; ++i)
            _renderSurfaces[i] = new RenderSurface(this, i);
    }

    protected void RefreshMemoryUse(uint totalBytes)
    {
        MemoryUse = totalBytes;
    }

    private static int GetLevelDimension(int size, uint level)
    {
        return Math.Max(size >> (int)level, 1);
    }

    private static uint CalculateFullMipChainLength(int largestDimension)
    {
        if (largestDimension <= 0)
            return 1u;

        var levels = 1u;
        while (largestDimension > 1)
        {
            largestDimension = Math.Max(largestDimension / 2, 1);
            ++levels;
        }

        return levels;
    }
}
