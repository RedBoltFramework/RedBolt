namespace RedBolt;

public enum TextureFilterMode : byte
{
    Nearest,
    Bilinear,
    Trilinear,
    Anisotropic,
    NearestAnisotropic,
    Default
}

public enum SkinningMode
{
    Auto,
    Hardware,
    Software
}

public sealed class Renderer
{
    private readonly record struct ActiveRenderPipelineView(RenderPipeline Pipeline, RenderPipelineView View);

    private readonly List<Viewport?> viewports = [];
    private readonly Dictionary<Viewport, ActiveRenderPipelineView> renderPipelineViews = [];

    public int TextureAnisotropy { get; private set; } = 4;

    public TextureFilterMode TextureFilterMode { get; private set; } = TextureFilterMode.Default;

    public MaterialQuality TextureQuality { get; private set; } = MaterialQuality.High;

    public SkinningMode SkinningMode { get; private set; } = SkinningMode.Auto;

    public uint NumSoftwareSkinningBones { get; private set; } = 64;

    public uint NumViews => GetNumViews();

    public Viewport? GetViewport(uint index = 0)
    {
        return index < viewports.Count ? viewports[(int)index] : null;
    }

    public void SetViewport(uint index, Viewport viewport)
    {
        ArgumentNullException.ThrowIfNull(viewport);
        EnsureViewportCount(index + 1);
        viewports[(int)index] = viewport;
    }

    public void SetViewport(Viewport viewport)
    {
        SetViewport(0, viewport);
    }

    public void ClearViewports()
    {
        viewports.Clear();
        renderPipelineViews.Clear();
    }

    public uint GetNumViewports() => (uint)viewports.Count;

    public uint GetNumViews() => (uint)viewports.Count(static viewport => viewport is not null);

    public Viewport? GetViewportForScene(Scene scene, uint index = 0)
    {
        ArgumentNullException.ThrowIfNull(scene);
        var matchIndex = 0u;
        foreach (var viewport in viewports)
        {
            if (viewport is null || !ReferenceEquals(viewport.Scene, scene))
                continue;
            if (matchIndex == index)
                return viewport;
            ++matchIndex;
        }

        return null;
    }

    public void SetNumViewports(uint count)
    {
        EnsureViewportCount(count);
        while (viewports.Count > count)
            viewports.RemoveAt(viewports.Count - 1);
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

    internal void RenderFrame(Graphics graphics, string? screenshotPath = null)
    {
        ArgumentNullException.ThrowIfNull(graphics);

        if (viewports.Count > 0 && viewports[0] is { } viewport)
        {
            var renderPipeline = viewport.Scene.Root.GetComponent<RenderPipeline>();
            if (renderPipeline is null)
            {
                graphics.RenderViewport(viewport, TextureQuality, screenshotPath);
                return;
            }

            var renderPipelineView = GetOrCreateRenderPipelineView(viewport, renderPipeline);
            if (!renderPipelineView.Define(viewport, graphics))
            {
                graphics.RenderViewport(viewport, TextureQuality, screenshotPath);
                return;
            }

            renderPipelineView.Update(graphics);
            renderPipelineView.Render(graphics, TextureQuality, screenshotPath);
        }
    }

    private RenderPipelineView GetOrCreateRenderPipelineView(Viewport viewport, RenderPipeline renderPipeline)
    {
        if (renderPipelineViews.TryGetValue(viewport, out var existing)
            && ReferenceEquals(existing.Pipeline, renderPipeline))
        {
            return existing.View;
        }

        var created = renderPipeline.Instantiate();
        renderPipelineViews[viewport] = new ActiveRenderPipelineView(renderPipeline, created);
        return created;
    }

    private void EnsureViewportCount(uint count)
    {
        while (viewports.Count < count)
            viewports.Add(null);
    }
}
