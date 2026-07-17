namespace RedBolt;

/// <summary>
/// Allocates shadow map regions in texture atlases.
/// </summary>
public sealed class ShadowMapAllocator : Object
{
    private sealed class AtlasPage
    {
        public uint Index;
        public Texture2D? Texture;
        public readonly AreaAllocator AreaAllocator = new();
        public bool ClearBeforeRendering;

        public ShadowMapRegion AllocateRegion(Vector2i size)
        {
            var x = 0;
            var y = 0;
            if (!AreaAllocator.Allocate(size.X, size.Y, ref x, ref y))
                return default;

            ClearBeforeRendering = true;
            return new ShadowMapRegion(Index, Texture, new IntRect(x, y, x + size.X, y + size.Y));
        }
    }

    private readonly RenderDevice _renderDevice;
    private readonly RenderContext _renderContext;
    private ShadowMapAllocatorSettings _settings = new();
    private SamplerStateDesc _shadowMapSampler = new();
    private PipelineStateOutputDesc _shadowMapOutputDesc = new();
    private TextureFormat _shadowMapFormat;
    private Vector2i _shadowAtlasSize;
    private readonly List<AtlasPage> _pages = [];
    private Texture2D? _vsmDepthTexture;

    public ShadowMapAllocator()
    {
        // C++: renderContext_ = renderDevice_->GetRenderContext()
        _renderDevice = GetSubsystem<RenderDevice>()
            ?? throw new InvalidOperationException("RenderDevice subsystem is required.");
        _renderContext = _renderDevice.GetRenderContext();
        CacheSettings();
    }

    public ShadowMapAllocatorSettings Settings => _settings;

    public SamplerStateDesc SamplerStateDesc => _shadowMapSampler;

    public PipelineStateOutputDesc ShadowOutputDesc => _shadowMapOutputDesc;

    public void SetSettings(ShadowMapAllocatorSettings settings)
    {
        if (_settings.Equals(settings))
            return;

        _settings = CopySettings(settings);
        CacheSettings();
        _pages.Clear();
    }

    public void ResetAllShadowMaps()
    {
        foreach (var page in _pages)
        {
            page.AreaAllocator.Reset(_shadowAtlasSize.X, _shadowAtlasSize.Y);
            page.ClearBeforeRendering = false;
        }
    }

    public ShadowMapRegion AllocateShadowMap(Vector2i size)
    {
        if (_settings.ShadowAtlasPageSize == 0 || _shadowMapFormat == TextureFormat.Unknown)
            return default;

        size = new Vector2i(Math.Min(size.X, _shadowAtlasSize.X), Math.Min(size.Y, _shadowAtlasSize.Y));
        foreach (var page in _pages)
        {
            var region = page.AllocateRegion(size);
            if (!region.IsEmpty)
                return region;
        }

        AllocatePage();
        return _pages[^1].AllocateRegion(size);
    }

    public bool BeginShadowMapRendering(ShadowMapRegion region)
    {
        if (region.IsEmpty || region.PageIndex >= _pages.Count)
            return false;

        var page = _pages[(int)region.PageIndex];
        if (page.Texture!.IsDepthStencil())
            _renderContext.SetRenderTargets(RenderTargetView.Texture(page.Texture), []);
        else
            _renderContext.SetRenderTargets(RenderTargetView.Texture(_vsmDepthTexture!), [RenderTargetView.Texture(page.Texture)]);

        if (page.ClearBeforeRendering)
        {
            _renderContext.ClearDepthStencil(ClearTarget.Depth);
            if (_settings.EnableVarianceShadowMaps)
                _renderContext.ClearRenderTarget(0, Color.White);
            page.ClearBeforeRendering = false;
        }

        _renderContext.SetViewport(region.Rect);
        return true;
    }

    private void CacheSettings()
    {
        _shadowMapOutputDesc = new PipelineStateOutputDesc();
        if (_settings.EnableVarianceShadowMaps)
        {
            _shadowMapFormat = TextureFormat.RG16F;
            _shadowMapOutputDesc.DepthStencilFormat = _renderDevice.GetDefaultDepthFormat();
            _shadowMapOutputDesc.SetRenderTargetFormat(_shadowMapFormat);
            _shadowMapOutputDesc.MultiSample = (uint)_settings.VarianceShadowMapMultiSample;
        }
        else
        {
            _shadowMapFormat = _settings.Use16BitShadowMaps ? TextureFormat.Depth16 : _renderDevice.GetDefaultDepthFormat();
            _shadowMapOutputDesc.DepthStencilFormat = _shadowMapFormat;
            _shadowMapOutputDesc.MultiSample = 1;
        }

        _shadowAtlasSize = new Vector2i((int)_settings.ShadowAtlasPageSize, (int)_settings.ShadowAtlasPageSize);
        _shadowMapSampler = new SamplerStateDesc
        {
            ShadowCompare = !_settings.EnableVarianceShadowMaps,
            FilterMode = TextureFilterMode.Bilinear
        };
    }

    private void AllocatePage()
    {
        var isDepthTexture = !_settings.EnableVarianceShadowMaps;
        var flags = isDepthTexture ? TextureFlag.BindDepthStencil : TextureFlag.BindRenderTarget;
        var multiSample = isDepthTexture ? 1 : _settings.VarianceShadowMapMultiSample;
        var texture = new Texture2D($"Dynamic ShadowMap #{_pages.Count}");
        texture.SetNumLevels(1);
        texture.SetFilterMode(TextureFilterMode.Bilinear);
        texture.SetShadowCompare(!_settings.EnableVarianceShadowMaps);
        texture.SetSize(_shadowAtlasSize.X, _shadowAtlasSize.Y, _shadowMapFormat, flags, multiSample);

        var page = new AtlasPage { Index = (uint)_pages.Count, Texture = texture };
        page.AreaAllocator.Reset(_shadowAtlasSize.X, _shadowAtlasSize.Y);
        _pages.Add(page);

        if (!_settings.EnableVarianceShadowMaps)
            _vsmDepthTexture = null;
        else if (_vsmDepthTexture is null || _vsmDepthTexture.Width != _shadowAtlasSize.X || _vsmDepthTexture.Height != _shadowAtlasSize.Y)
        {
            _vsmDepthTexture = new Texture2D("Variance Shadow Map Depth");
            _vsmDepthTexture.SetNumLevels(1);
            _vsmDepthTexture.SetSize(_shadowAtlasSize.X, _shadowAtlasSize.Y,
                _shadowMapOutputDesc.DepthStencilFormat, TextureFlag.BindDepthStencil, multiSample);
        }
    }

    private static ShadowMapAllocatorSettings CopySettings(ShadowMapAllocatorSettings settings) => new()
    {
        EnableVarianceShadowMaps = settings.EnableVarianceShadowMaps,
        VarianceShadowMapMultiSample = settings.VarianceShadowMapMultiSample,
        Use16BitShadowMaps = settings.Use16BitShadowMaps,
        ShadowAtlasPageSize = settings.ShadowAtlasPageSize,
        DepthBiasScale = settings.DepthBiasScale,
        DepthBiasOffset = settings.DepthBiasOffset
    };
}
