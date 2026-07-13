namespace RedBolt;

public enum SkinningMode
{
    Auto,
    Hardware,
    Software
}

public sealed class FrameStatistics
{
    public uint Animations;
    public uint ChangedAnimations;
}

public sealed class Renderer : Object
{
    private readonly record struct ActiveRenderPipelineView(RenderPipeline Pipeline, RenderPipelineView View);

    private readonly Zone _defaultZone = new();
    private Technique? _defaultTechnique;
    private Material? _defaultMaterial;
    private Texture2D? _defaultLightRamp;
    private Texture2D? _defaultLightSpot;
    private TextureCube? _blackCubeMap;
    private RenderSurface? _backbufferSurface;
    private bool _backbufferSurfaceViewportsDirty;
    private readonly List<Viewport?> _viewports = [];
    private readonly List<(RenderSurface? Surface, Viewport Viewport)> _queuedViewports = [];
    private readonly Dictionary<Viewport, ActiveRenderPipelineView> _renderPipelineViews = [];
    private readonly CommonFrameInfo _frameInfo = new();
    private readonly FrameStatistics _frameStats = new();

    public int TextureAnisotropy { get; private set; } = 4;

    public TextureFilterMode TextureFilterMode { get; private set; } = TextureFilterMode.Trilinear;

    public MaterialQuality TextureQuality { get; private set; } = MaterialQuality.High;

    public SkinningMode SkinningMode { get; private set; } = SkinningMode.Auto;

    public uint NumSoftwareSkinningBones { get; private set; } = 4;

    public uint NumViews => GetNumViews();

    public FrameStatistics GetFrameStats() => _frameStats;

    public FrameStatistics GetMutableFrameStats() => _frameStats;

    public CommonFrameInfo GetFrameInfo() => _frameInfo;

    public Zone GetDefaultZone() => _defaultZone;

    public Viewport? GetViewport(uint index = 0)
    {
        return index < _viewports.Count ? _viewports[(int)index] : null;
    }

    public void SetViewport(uint index, Viewport viewport)
    {
        ArgumentNullException.ThrowIfNull(viewport);
        EnsureViewportCount(index + 1);
        _viewports[(int)index] = viewport;
    }

    public void SetViewport(Viewport viewport)
    {
        SetViewport(0, viewport);
    }

    public void SetDefaultTechnique(Technique? technique)
    {
        _defaultTechnique = technique;
    }

    public void ClearViewports()
    {
        _viewports.Clear();
        _renderPipelineViews.Clear();
    }

    public uint GetNumViewports() => (uint)_viewports.Count;

    public uint GetNumViews()
    {
        var count = 0u;
        foreach (var viewport in _viewports)
        {
            if (viewport is not null)
                ++count;
        }

        return count;
    }

    public uint GetNumGeometries()
    {
        var count = 0u;
        foreach (var renderPipelineView in _renderPipelineViews.Values)
            count += renderPipelineView.View.GetStats().NumGeometries;
        return count;
    }

    public uint GetNumLights()
    {
        var count = 0u;
        foreach (var renderPipelineView in _renderPipelineViews.Values)
            count += renderPipelineView.View.GetStats().NumLights;
        return count;
    }

    public uint GetNumShadowMaps()
    {
        var count = 0u;
        foreach (var renderPipelineView in _renderPipelineViews.Values)
            count += renderPipelineView.View.GetStats().NumShadowedLights;
        return count;
    }

    public uint GetNumOccluders()
    {
        var count = 0u;
        foreach (var renderPipelineView in _renderPipelineViews.Values)
            count += renderPipelineView.View.GetStats().NumOccluders;
        return count;
    }

    public Viewport? GetViewportForScene(Scene scene, uint index = 0)
    {
        ArgumentNullException.ThrowIfNull(scene);
        var matchIndex = 0u;
        foreach (var viewport in _viewports)
        {
            if (viewport is null || !ReferenceEquals(viewport.Scene, scene))
                continue;
            if (matchIndex == index)
                return viewport;
            ++matchIndex;
        }

        return null;
    }

    public Technique? GetDefaultTechnique()
    {
        if (_defaultTechnique is null)
            _defaultTechnique = GetSubsystem<ResourceCache>()?.GetResourceOrNull<Technique>("Techniques/NoTexture.xml");

        return _defaultTechnique;
    }

    public Material GetDefaultMaterial()
    {
        return _defaultMaterial ??= new Material(string.Empty);
    }

    public Texture2D? GetDefaultLightRamp()
    {
        if (_defaultLightRamp is null)
            _defaultLightRamp = GetSubsystem<ResourceCache>()?.GetResourceOrNull<Texture2D>("Textures/Ramp.png");

        return _defaultLightRamp;
    }

    public Texture2D? GetDefaultLightSpot()
    {
        if (_defaultLightSpot is null)
            _defaultLightSpot = GetSubsystem<ResourceCache>()?.GetResourceOrNull<Texture2D>("Textures/Spot.png");

        return _defaultLightSpot;
    }

    public TextureCube GetBlackCubeMap()
    {
        if (_blackCubeMap is not null)
            return _blackCubeMap;

        _blackCubeMap = new TextureCube("BlackCubeMap");
        _blackCubeMap.SetNumLevels(1);
        _blackCubeMap.SetSize(1, TextureFormat.RGBA8);
        _blackCubeMap.SetFilterMode(TextureFilterMode.Nearest);
        var data = new byte[] { 0, 0, 0, 255 };
        for (var face = CubeMapFace.PositiveX; face < CubeMapFace.MaxCubeMapFaces; ++face)
            _blackCubeMap.SetData(face, 0, 0, 0, 1, 1, data);

        return _blackCubeMap;
    }

    public void SetNumViewports(uint count)
    {
        EnsureViewportCount(count);
        while (_viewports.Count > count)
            _viewports.RemoveAt(_viewports.Count - 1);
    }

    public void SetTextureAnisotropy(int level)
    {
        TextureAnisotropy = Math.Max(1, level);
    }

    public int GetTextureAnisotropy() => TextureAnisotropy;

    public void SetTextureFilterMode(TextureFilterMode mode)
    {
        TextureFilterMode = mode;
    }

    public TextureFilterMode GetTextureFilterMode() => TextureFilterMode;

    public void SetTextureQuality(MaterialQuality quality)
    {
        TextureQuality = quality;
    }

    public MaterialQuality GetTextureQuality() => TextureQuality;

    public void SetSkinningMode(SkinningMode mode)
    {
        SkinningMode = mode;
    }

    public SkinningMode GetSkinningMode() => SkinningMode;

    public bool GetUseHardwareSkinning() => SkinningMode is SkinningMode.Auto or SkinningMode.Hardware;

    public void SetNumSoftwareSkinningBones(uint numBones)
    {
        NumSoftwareSkinningBones = numBones;
    }

    public uint GetNumSoftwareSkinningBones() => NumSoftwareSkinningBones;

    /// <summary>Set backbuffer render surface. Null corresponds to the application backbuffer.</summary>
    public void SetBackbufferRenderSurface(RenderSurface? renderSurface)
    {
        if (!ReferenceEquals(_backbufferSurface, renderSurface))
        {
            _backbufferSurface?.SetNumViewports(0);
            _backbufferSurface = renderSurface;
            _backbufferSurfaceViewportsDirty = true;
        }
    }

    public void QueueRenderSurface(RenderSurface? renderTarget)
    {
        if (renderTarget is null)
            return;

        for (var i = 0u; i < renderTarget.GetNumViewports(); ++i)
        {
            if (renderTarget.GetViewport(i) is { } viewport)
                QueueViewport(renderTarget, viewport);
        }
    }

    public void QueueViewport(RenderSurface? renderTarget, Viewport? viewport)
    {
        if (viewport is null)
            return;

        var queuedViewport = (renderTarget, viewport);
        if (!_queuedViewports.Contains(queuedViewport))
            _queuedViewports.Add(queuedViewport);
    }

    internal void RenderFrame(Graphics graphics, Action? beforePresent = null, string? screenshotPath = null)
    {
        ArgumentNullException.ThrowIfNull(graphics);

        Context.SendEvent<GraphicsEvents.RenderSurfaceUpdateEvent>();

        foreach (var (renderSurface, viewport) in _queuedViewports)
        {
            if (renderSurface is not null)
            {
                graphics.RenderViewport(renderSurface, viewport, TextureQuality);
                renderSurface.ResetUpdateQueued();
            }
            else
                graphics.RenderViewport(viewport, TextureQuality);
        }
        _queuedViewports.Clear();

        foreach (var viewport in _viewports)
        {
            if (viewport is null)
                continue;

            var renderPipeline = viewport.Scene.Root.GetComponent<RenderPipeline>();
            if (renderPipeline is null)
            {
                graphics.RenderViewport(viewport, TextureQuality, beforePresent, screenshotPath);
                continue;
            }

            var renderPipelineView = GetOrCreateRenderPipelineView(viewport, renderPipeline);
            if (!renderPipelineView.Define(viewport, graphics))
            {
                graphics.RenderViewport(viewport, TextureQuality, beforePresent, screenshotPath);
                continue;
            }

            renderPipelineView.Update(graphics);
            renderPipelineView.Render(graphics, TextureQuality, beforePresent, screenshotPath);
        }
    }

    private RenderPipelineView GetOrCreateRenderPipelineView(Viewport viewport, RenderPipeline renderPipeline)
    {
        if (_renderPipelineViews.TryGetValue(viewport, out var existing)
            && ReferenceEquals(existing.Pipeline, renderPipeline))
        {
            return existing.View;
        }

        var created = renderPipeline.Instantiate();
        _renderPipelineViews[viewport] = new ActiveRenderPipelineView(renderPipeline, created);
        return created;
    }

    private void EnsureViewportCount(uint count)
    {
        while (_viewports.Count < count)
            _viewports.Add(null);
    }
}
