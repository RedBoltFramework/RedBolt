using SystemMath = global::System.Math;
using System.Runtime.InteropServices;

using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.RenderAPI;

namespace RedBolt.RenderPipeline;

/// <summary>Utility to allocate shadow maps in texture atlas.</summary>
public class ShadowMapAllocator : RedObject
{
    private struct AtlasPage
    {
        public int Index { get; set; }
        public Texture2D? Texture { get; set; }
        public AreaAllocator AreaAllocator { get; set; }
        public bool ClearBeforeRendering { get; set; }

        public AtlasPage()
        {
            Index = 0;
            Texture = null;
            AreaAllocator = new AreaAllocator();
            ClearBeforeRendering = false;
        }

        /// <summary>Allocate shadow map.</summary>
        public ShadowMapRegion AllocateRegion(in Vector2i size)
        {
            var x = 0;
            var y = 0;
            if (!AreaAllocator.Allocate(size.X, size.Y, ref x, ref y))
                return default;

            ClearBeforeRendering = true;
            return new ShadowMapRegion
            {
                PageIndex = Index,
                Texture = Texture,
                Rect = new IntRect(x, y, x + size.X, y + size.Y)
            };
        }
    }

    private readonly RenderDevice _renderDevice;
    private readonly RenderContext _renderContext;
    private ShadowMapAllocatorSettings _settings = new();
    private SamplerStateDesc _samplerStateDesc = new();
    private PipelineStateOutputDesc _shadowOutputDesc = new();
    private TextureFormat _shadowMapFormat;
    private Vector2i _shadowAtlasPageSize;
    private readonly List<AtlasPage> _pages = [];
    private Texture2D? _vsmDepthTexture;
    private readonly RenderTargetView[] _renderTargets = new RenderTargetView[1];

    public ShadowMapAllocator()
    {
        _renderDevice = GetSubsystem<RenderDevice>()!;
        _renderContext = _renderDevice.GetRenderContext();
        CacheSettings();
    }

    public ref readonly ShadowMapAllocatorSettings Settings => ref _settings;

    public ref readonly SamplerStateDesc SamplerStateDesc => ref _samplerStateDesc;

    public ref readonly PipelineStateOutputDesc ShadowOutputDesc => ref _shadowOutputDesc;

    public void SetSettings(in ShadowMapAllocatorSettings settings)
    {
        if (_settings != settings)
        {
            _settings = settings;
            CacheSettings();

            _pages.Clear();
        }
    }

    /// <summary>Reset allocated shadow maps.</summary>
    public void ResetAllShadowMaps()
    {
        var pages = CollectionsMarshal.AsSpan(_pages);
        for (var i = 0; i < pages.Length; ++i)
        {
            ref var page = ref pages[i];
            page.AreaAllocator.Reset(_shadowAtlasPageSize.X, _shadowAtlasPageSize.Y,
                _shadowAtlasPageSize.X, _shadowAtlasPageSize.Y);
            page.ClearBeforeRendering = false;
        }
    }

    /// <summary>Allocate shadow map of given size. It is better to allocate from bigger to smaller sizes.</summary>
    public ShadowMapRegion AllocateShadowMap(in Vector2i size)
    {
        if (_settings.ShadowAtlasPageSize == 0 || _shadowMapFormat == TextureFormat.TexFormatUnknown)
            return default;

        var clampedSize = new Vector2i(SystemMath.Min(size.X, _shadowAtlasPageSize.X), SystemMath.Min(size.Y, _shadowAtlasPageSize.Y));
        var pages = CollectionsMarshal.AsSpan(_pages);
        for (var i = 0; i < pages.Length; ++i)
        {
            var region = pages[i].AllocateRegion(in clampedSize);
            if (region)
                return region;
        }

        AllocatePage();
        ref var page = ref CollectionsMarshal.AsSpan(_pages)[^1];
        return page.AllocateRegion(in clampedSize);
    }

    /// <summary>Begin shadow map rendering. Clears shadow map if necessary.</summary>
    public bool BeginShadowMapRendering(in ShadowMapRegion region)
    {
        if (!(bool)region || region.PageIndex >= _pages.Count)
            return false;

        var shadowMapTexture = region.Texture!;
        ref var page = ref CollectionsMarshal.AsSpan(_pages)[region.PageIndex];
        if (shadowMapTexture.IsDepthStencil())
            _renderContext.SetRenderTargets(RenderTargetView.Texture(shadowMapTexture), []);
        else
        {
            _renderTargets[0] = RenderTargetView.Texture(shadowMapTexture);
            _renderContext.SetRenderTargets(RenderTargetView.Texture(_vsmDepthTexture!), _renderTargets);
            _renderTargets[0] = default;
        }

        // Clear whole texture if needed
        if (page.ClearBeforeRendering)
        {
            page.ClearBeforeRendering = false;

            _renderContext.ClearDepthStencil(ClearTarget.Depth);
            if (_settings.EnableVarianceShadowMaps)
                _renderContext.ClearRenderTarget(0, Color.White);
        }

        _renderContext.SetViewport(region.Rect);
        return true;
    }

    private void CacheSettings()
    {
        _shadowOutputDesc = new PipelineStateOutputDesc();
        if (_settings.EnableVarianceShadowMaps)
        {
            _shadowMapFormat = TextureFormat.TexFormatRg32Float;
            _shadowOutputDesc.DepthStencilFormat = _renderDevice.GetDefaultDepthFormat();
            _shadowOutputDesc.NumRenderTargets = 1;
            _shadowOutputDesc.RenderTargetFormats[0] = _shadowMapFormat;
            _shadowOutputDesc.MultiSample = _settings.VarianceShadowMapMultiSample;
        }
        else
        {
            _shadowMapFormat = _settings.Use16BitShadowMaps ? TextureFormat.TexFormatD16Unorm : _renderDevice.GetDefaultDepthFormat();
            _shadowOutputDesc.DepthStencilFormat = _shadowMapFormat;
            _shadowOutputDesc.NumRenderTargets = 0;
            _shadowOutputDesc.MultiSample = 1;
        }

        _shadowAtlasPageSize = new Vector2i(_settings.ShadowAtlasPageSize, _settings.ShadowAtlasPageSize);
        _samplerStateDesc = new SamplerStateDesc
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
        var texture = new Texture2D();
        texture.SetNumLevels(1);
        texture.SetFilterMode(TextureFilterMode.Bilinear);
        texture.SetShadowCompare(!_settings.EnableVarianceShadowMaps);
        texture.SetSize(_shadowAtlasPageSize.X, _shadowAtlasPageSize.Y, _shadowMapFormat, flags, multiSample);

        _pages.Add(new AtlasPage());
        ref var page = ref CollectionsMarshal.AsSpan(_pages)[^1];
        page.Index = _pages.Count - 1;
        page.Texture = texture;
        page.AreaAllocator.Reset(_shadowAtlasPageSize.X, _shadowAtlasPageSize.Y,
            _shadowAtlasPageSize.X, _shadowAtlasPageSize.Y);

        if (!_settings.EnableVarianceShadowMaps)
            _vsmDepthTexture = null;
        else if (_vsmDepthTexture is null || _vsmDepthTexture.Width != _shadowAtlasPageSize.X
            || _vsmDepthTexture.Height != _shadowAtlasPageSize.Y)
        {
            _vsmDepthTexture = new Texture2D();
            _vsmDepthTexture.SetSize(_shadowAtlasPageSize.X, _shadowAtlasPageSize.Y,
                _shadowOutputDesc.DepthStencilFormat, TextureFlag.BindDepthStencil, multiSample);
        }
    }
}
