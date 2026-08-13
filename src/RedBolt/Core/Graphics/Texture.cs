namespace RedBolt.Graphics;

using Microsoft.Extensions.Logging;
using SystemMath = global::System.Math;
using System.Runtime.CompilerServices;

using RedBolt.Bindings.Diligent;
using RedBolt.Container;
using RedBolt.Logging;
using RedBolt.Math;
using RedBolt.RenderAPI;
using TextureAddressMode = global::RedBolt.RenderAPI.TextureAddressMode;
using RedBolt.Resource;

internal static class TextureFormatUtility
{
    public static bool IsFloatColorFormat(TextureFormat format)
    {
        return format is TextureFormat.TexFormatRg16Float or TextureFormat.TexFormatRgba16Float or TextureFormat.TexFormatRgba32Float;
    }
}

/// <summary>Base class for texture resources.</summary>
/// <remarks>
/// </remarks>
public class Texture : ResourceWithMetadata
{
    public Texture()
    {
        _rawTexture = new TextureRawTexture(this);
        _mipsToSkip[0] = 2;
        _mipsToSkip[1] = 1;
    }

    public override void Dispose()
    {
        for (var i = _renderSurfaces.Length - 1; i >= 0; --i)
            _renderSurfaces[i]?.Dispose();
        _renderSurfaces = [];
        _backupTexture = null;
        _rawTexture.Dispose();
        base.Dispose();
    }

    public TextureFormat Format
        => _rawTexture.GetParams().Format;

    public int Levels => _rawTexture.GetParams().NumLevels;

    public int Width => _rawTexture.GetParams().Size.X;

    public int Height => _rawTexture.GetParams().Size.Y;

    public int Depth => _rawTexture.GetParams().Size.Z;

    public int MultiSample => _rawTexture.GetParams().MultiSample;

    public TextureFilterMode FilterMode => _rawTexture.GetSamplerStateDesc().FilterMode;

    public uint Anisotropy => _rawTexture.GetSamplerStateDesc().Anisotropy;

    public float MinLod => _rawTexture.GetSamplerStateDesc().MinLod;

    public float MaxLod => _rawTexture.GetSamplerStateDesc().MaxLod;

    public bool ShadowCompare => _rawTexture.GetSamplerStateDesc().ShadowCompare;

    public bool Linear => _linear;

    public bool SRGB => GetSRGB();

    public Texture? BackupTexture => _backupTexture;

    public RawTexture GetRawTexture() => _rawTexture;

    public bool IsDataLost() => _rawTexture.IsDataLost();

    public void ClearDataLost() => _rawTexture.ClearDataLost();

    public bool IsCompressed()
    {
        var formatInfo = RenderAPIUtils.GetTextureFormatInfo(Format);
        return formatInfo.ComponentType == global::RedBolt.Bindings.Diligent.ComponentType.ComponentTypeCompressed;
    }

    public void SetFilterMode(TextureFilterMode mode)
    {
        var desc = _rawTexture.GetSamplerStateDesc();
        desc.FilterMode = mode;
        SetSamplerStateDesc(desc);
    }

    public TextureFilterMode GetFilterMode() => FilterMode;

    public void SetAddressMode(TextureCoordinate coordinate, TextureAddressMode mode)
    {
        var desc = _rawTexture.GetSamplerStateDesc();
        desc[coordinate] = mode;
        SetSamplerStateDesc(desc);
    }

    public TextureAddressMode GetAddressMode(TextureCoordinate coordinate) =>
        _rawTexture.GetSamplerStateDesc()[coordinate];

    public void SetAnisotropy(uint level)
    {
        var desc = _rawTexture.GetSamplerStateDesc();
        desc.Anisotropy = unchecked((byte)level);
        SetSamplerStateDesc(desc);
    }

    public uint GetAnisotropy() => Anisotropy;

    public void SetShadowCompare(bool enable)
    {
        var desc = _rawTexture.GetSamplerStateDesc();
        desc.ShadowCompare = enable;
        SetSamplerStateDesc(desc);
    }

    public bool GetShadowCompare() => ShadowCompare;

    public void SetMinLod(float minLod)
    {
        var desc = _rawTexture.GetSamplerStateDesc();
        desc.MinLod = minLod;
        SetSamplerStateDesc(desc);
    }

    public float GetMinLod() => MinLod;

    public void SetMaxLod(float maxLod)
    {
        var desc = _rawTexture.GetSamplerStateDesc();
        desc.MaxLod = maxLod;
        SetSamplerStateDesc(desc);
    }

    public float GetMaxLod() => MaxLod;

    public void SetSamplerStateDesc(SamplerStateDesc desc)
    {
        _rawTexture.SetSamplerStateDesc(desc);
    }

    public SamplerStateDesc GetSamplerStateDesc() => _rawTexture.GetSamplerStateDesc();

    public void SetNumLevels(int levels)
    {
        _requestedLevels = levels;
    }

    public int GetLevels() => Levels;

    public void SetLinear(bool linear)
    {
        _linear = linear;
    }

    public bool GetLinear() => Linear;

    public void SetSRGB(bool enable)
    {
        _requestedSRGB = enable;
    }

    public bool GetSRGB()
    {
        return RenderAPIUtils.IsTextureFormatSRGB(Format);
    }

    public void SetBackupTexture(Texture? texture)
    {
        _backupTexture = texture;
    }

    public Texture? GetBackupTexture() => BackupTexture;

    public void SetMipsToSkip(MaterialQuality quality, int toSkip)
    {
        var index = (int)quality;
        if (index >= (int)MaterialQuality.Low && index < MaxTextureQualityLevels)
        {
            _mipsToSkip[index] = toSkip;
            for (var i = 1; i < MaxTextureQualityLevels; ++i)
                _mipsToSkip[i] = SystemMath.Min(_mipsToSkip[i], _mipsToSkip[i - 1]);
        }
    }

    public int GetMipsToSkip(MaterialQuality quality)
    {
        var index = (int)quality;
        return index >= (int)MaterialQuality.Low && index < MaxTextureQualityLevels ? _mipsToSkip[index] : 0;
    }

    public int GetLevelWidth(int level) => level > Levels ? 0 : SystemMath.Max(Width >> level, 1);

    public int GetLevelHeight(int level) => level > Levels ? 0 : SystemMath.Max(Height >> level, 1);

    public int GetLevelDepth(int level) => level > Levels ? 0 : SystemMath.Max(Depth >> level, 1);

    public int GetMultiSample() => MultiSample;

    public bool GetAutoResolve() =>
        (_rawTexture.GetParams().Flags & TextureFlag.NoMultiSampledAutoResolve) == 0;

    public Vector2i GetSize() => new(Width, Height);

    public IntRect GetRect() => new(0, 0, Width, Height);

    public RenderSurface? GetRenderSurface(int index = 0) => index < _renderSurfaces.Length ? _renderSurfaces[index] : null;

    public bool IsRenderTarget() => (_rawTexture.GetParams().Flags & TextureFlag.BindRenderTarget) != 0;

    public bool IsDepthStencil() => (_rawTexture.GetParams().Flags & TextureFlag.BindDepthStencil) != 0;

    public bool IsUnorderedAccess() => (_rawTexture.GetParams().Flags & TextureFlag.BindUnorderedAccess) != 0;

    public int GetComponents()
    {
        if (Width == 0 || IsCompressed())
            return 0;

        return GetRowDataSize(Width) / Width;
    }

    public int GetDataSize(int width, int height)
    {
        var formatInfo = RenderAPIUtils.GetTextureFormatInfo(Format);
        return checked(GetRowDataSize(width) *
            ((height + checked((int)formatInfo.BlockHeight) - 1) / checked((int)formatInfo.BlockHeight)));
    }

    public int GetDataSize(int width, int height, int depth)
    {
        return checked(depth * GetDataSize(width, height));
    }

    public int GetRowDataSize(int width)
    {
        var formatInfo = RenderAPIUtils.GetTextureFormatInfo(Format);
        return checked((int)formatInfo.GetElementSize() *
            ((width + checked((int)formatInfo.BlockWidth) - 1) / checked((int)formatInfo.BlockWidth)));
    }

    public void SetParameters(XMLFile? file)
    {
        if (file is null)
            return;

        SetParameters(file.GetRoot());
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
                    "nearestanisotropic" => TextureFilterMode.NearestAnisotropic,
                    _ => TextureFilterMode.Default
                };
                SetFilterMode(mode);
                if (paramElement.HasAttribute("anisotropy"))
                    SetAnisotropy(paramElement.GetUInt("anisotropy"));
            }

            if (name == "mipmap")
            {
                SetNumLevels(paramElement.GetBool("enable") ? 0 : 1);
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
        var isRenderTarget = (_rawTexture.GetParams().Flags & TextureFlag.BindRenderTarget) != 0;
        var isDepthStencil = (_rawTexture.GetParams().Flags & TextureFlag.BindDepthStencil) != 0;

        if (isRenderTarget)
        {
            SubscribeToEvent<GraphicsEvents.RenderSurfaceUpdateEvent>(
                (ref GraphicsEvents.RenderSurfaceUpdateEvent _) => HandleRenderSurfaceUpdate());
        }
        else
        {
            UnsubscribeFromEvent<GraphicsEvents.RenderSurfaceUpdateEvent>();
        }

        if (isRenderTarget || isDepthStencil)
        {
            var sourceHandles = _rawTexture.GetHandles().RenderSurfaces;
            var renderSurfaceHandles = new ITextureView[sourceHandles.Length];
            for (var i = 0; i < sourceHandles.Length; ++i)
            {
                renderSurfaceHandles[i] = sourceHandles[i];
                _ = IObject_AddRef(renderSurfaceHandles[i]);
            }

            try
            {
                if (_renderSurfaces.Length != renderSurfaceHandles.Length)
                {
                    for (var i = _renderSurfaces.Length - 1; i >= 0; --i)
                        _renderSurfaces[i].Dispose();
                    _renderSurfaces = new RenderSurface[renderSurfaceHandles.Length];
                    for (var i = 0; i < renderSurfaceHandles.Length; ++i)
                        _renderSurfaces[i] = new RenderSurface(this, i);
                }

                for (var i = 0; i < renderSurfaceHandles.Length; ++i)
                    _renderSurfaces[i].Restore(renderSurfaceHandles[i]);
            }
            finally
            {
                foreach (var renderSurfaceHandle in renderSurfaceHandles)
                    Release(renderSurfaceHandle);
            }
        }

        MemoryUse = checked((int)_rawTexture.CalculateMemoryUseGPU());
    }

    private void OnDestroyGPU()
    {
        foreach (var renderSurface in _renderSurfaces)
            renderSurface.Invalidate();
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
        var renderer = GetSubsystem<Renderer>()!;
        foreach (var renderSurface in _renderSurfaces)
        {
            if (renderSurface.GetUpdateMode() == RenderSurfaceUpdateMode.UpdateAlways || renderSurface.IsUpdateQueued())
            {
                renderer.QueueRenderSurface(renderSurface);
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

    protected bool Create(RawTextureParams parameters)
    {
        return _rawTexture.CreateTexture(parameters);
    }

    protected bool CreateForImage(RawTextureParams baseParams, Image? image)
    {
        if (image is null)
            return false;

        var renderDevice = GetSubsystem<RenderDevice>();
        var renderer = GetSubsystem<Renderer>();

        var quality = renderer is not null ? renderer.GetTextureQuality() : MaterialQuality.High;
        var (mostDetailedLevel, numLevels) =
            GetLevelsOffsetAndCount(image, baseParams.NumLevels, GetMipsToSkip(quality));

        _mostDetailedLevel = mostDetailedLevel;
        var parameters = baseParams with
        {
            Size = RenderAPIUtils.GetMipLevelSize(image.GetSize(), mostDetailedLevel),
            NumLevels = numLevels,
            Format = ToHardwareFormat(image.GetGPUFormat(), renderDevice)
        };
        if (_requestedSRGB)
            parameters = parameters with { Format = RenderAPIUtils.SetTextureFormatSRGB(parameters.Format) };
        return _rawTexture.CreateTexture(parameters);
    }

    protected bool UpdateFromImage(int arraySlice, Image? image)
    {
        if (image is null)
            return false;

        var internalFormat = RenderAPIUtils.SetTextureFormatSRGB(Format, false);
        var imageFormat = image.GetGPUFormat();

        if (!image.IsCompressed() && internalFormat == imageFormat)
        {
            var currentLevel = image;
            for (var level = 0; level < _mostDetailedLevel; ++level)
                currentLevel = currentLevel.GetNextLevel()!;

            for (var level = 0; level < Levels; ++level)
            {
                _rawTexture.Update(level, Vector3i.Zero, currentLevel.GetSize(), arraySlice, currentLevel.GetData());
                currentLevel = currentLevel.GetNextLevel()!;
            }
        }
        else if (internalFormat == TextureFormat.TexFormatRgba8Unorm
            && image.CompressedFormat != TextureFormat.TexFormatRgba8Unorm)
        {
            var isRgb8 = !image.IsCompressed() && image.Components == 3;
            if (isRgb8)
                Log.GetLogger().LogDebug("Image '{Name}' is converted from RGB8 to RGBA8 format on upload to GPU", Name);
            else
                Log.GetLogger().LogWarning("Image '{Name}' is converted to RGBA8 format on upload to GPU", Name);

            for (var level = 0; level < Levels; ++level)
            {
                using var decompressedLevel = image.GetDecompressedImageLevel(_mostDetailedLevel + level);
                if (decompressedLevel is null)
                    return false;

                _rawTexture.Update(level, Vector3i.Zero, decompressedLevel.GetSize(), arraySlice,
                    decompressedLevel.GetData());
            }
        }
        else
        {
            System.Diagnostics.Debug.Assert(image.IsCompressed());

            // Upload compressed image data as is
            for (var level = 0; level < Levels; ++level)
            {
                var imageLevel = image.GetCompressedLevel(_mostDetailedLevel + level);
                var levelSize = new Vector3i(imageLevel.Width, imageLevel.Height, imageLevel.Depth);
                _rawTexture.Update(level, Vector3i.Zero, levelSize, arraySlice, imageLevel.Data.Span);
            }
        }

        return true;
    }

    protected bool ReadToImage(int arraySlice, int level, Image? image)
    {
        if (image is null)
            return false;

        var imageFormat = RenderAPIUtils.SetTextureFormatSRGB(Format, false);
        if (imageFormat is not TextureFormat.TexFormatRgba8Unorm and not TextureFormat.TexFormatBgra8Unorm and not TextureFormat.TexFormatBgrx8Unorm)
        {
            Log.GetLogger().LogWarning("Unsupported texture format, can not convert to Image");
            return false;
        }

        var size = new Vector3i(GetLevelWidth(level), GetLevelHeight(level), GetLevelDepth(level));
        if (!image.SetSize(size.X, size.Y, size.Z, 4))
            return false;

        var imageData = image.GetData();
        if (!_rawTexture.Read(arraySlice, level, imageData))
            return false;

        if (imageFormat is TextureFormat.TexFormatBgra8Unorm or TextureFormat.TexFormatBgrx8Unorm)
        {
            var numTexels = checked(size.X * size.Y * size.Z);
            for (var i = 0; i < numTexels; ++i)
                (imageData[i * 4], imageData[i * 4 + 2]) = (imageData[i * 4 + 2], imageData[i * 4]);
        }
        if (imageFormat == TextureFormat.TexFormatBgrx8Unorm)
        {
            var numTexels = checked(size.X * size.Y * size.Z);
            for (var i = 0; i < numTexels; ++i)
                imageData[i * 4 + 3] = 255;
        }
        return true;
    }

    private static (int MostDetailedLevel, int NumLevels) GetLevelsOffsetAndCount(
        Image image, int numLevels, int mostDetailedMip)
    {
        var maxNumLevels = image.IsCompressed()
            ? checked((int)image.NumCompressedLevels)
            : RenderAPIUtils.GetMipLevelCount(image.GetSize());
        var effectiveMostDetailedMip = SystemMath.Min(mostDetailedMip, maxNumLevels - 1);
        var effectiveNumLevels = numLevels == 0
            ? maxNumLevels - effectiveMostDetailedMip
            : SystemMath.Min(numLevels, maxNumLevels - effectiveMostDetailedMip);
        return (effectiveMostDetailedMip, effectiveNumLevels);
    }

    private static TextureFormat ToHardwareFormat(TextureFormat format, RenderDevice? renderDevice)
    {
        if (format == TextureFormat.TexFormatUnknown)
            return TextureFormat.TexFormatRgba8Unorm;

        if (renderDevice?.GetRenderDevice() is not null && !renderDevice.IsTextureFormatSupported(format))
            return TextureFormat.TexFormatRgba8Unorm;

        return format;
    }

    private static bool IsCompressedEffective(Image image, RenderDevice? renderDevice)
    {
        if (!image.IsCompressed())
            return false;
        return renderDevice?.GetRenderDevice() is null || renderDevice.IsTextureFormatSupported(image.GetGPUFormat());
    }

    private static void Release(IObject? value)
    {
        if (value is { IsNull: false })
            _ = IObject_Release(value);
    }

    [InlineArray(MaxTextureQualityLevels)]
    private struct MipsToSkipArray
    {
        private int _element0;
    }

    private sealed class TextureRawTexture : RawTexture
    {
        private readonly Texture _owner;

        public TextureRawTexture(Texture owner)
        {
            _owner = owner;
        }

        public bool CreateTexture(RawTextureParams parameters) => Create(parameters);

        protected override void OnCreateGPU() => _owner.OnCreateGPU();

        protected override void OnDestroyGPU() => _owner.OnDestroyGPU();

        protected override bool TryRestore() => _owner.TryRestore();
    }

    protected int _requestedLevels;
    protected bool _requestedSRGB;
    private MipsToSkipArray _mipsToSkip;
    private bool _linear;
    protected bool _resolveDirty;
    protected bool _levelsDirty;
    private Texture? _backupTexture;
    protected RenderSurface[] _renderSurfaces = [];
    protected int _mostDetailedLevel;

    private readonly TextureRawTexture _rawTexture;
    private const int MaxTextureQualityLevels = 3;
}
