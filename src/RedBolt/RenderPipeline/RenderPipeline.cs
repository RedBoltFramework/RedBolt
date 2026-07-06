namespace RedBolt;

public class RenderPipeline : Component
{
    public const string DefaultRenderPathName = "RenderPaths/Default.renderpath";

    private readonly Dictionary<string, object?> renderPathParameters = new(StringComparer.Ordinal);
    private readonly Dictionary<string, object?> frameShaderParameters = new(StringComparer.Ordinal);
    private IReadOnlyList<RenderPassToggle> renderPasses = [];
    private RenderPath? renderPath;
    private RenderPipelineSettings settings = new();
    private uint revision = 1;

    public RenderPipelineSettings Settings => settings;

    public RenderPath? GetRenderPath() => renderPath;

    public IReadOnlyList<RenderPassToggle> GetRenderPasses() => renderPasses;

    public IReadOnlyDictionary<string, object?> GetRenderPathParameters() => renderPathParameters;

    public IReadOnlyDictionary<string, object?> GetFrameShaderParameters() => frameShaderParameters;

    public uint Revision => revision;

    public void SetSettings(RenderPipelineSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        this.settings = settings;
        this.settings.Validate();
        Touch();
    }

    public void SetRenderPath(RenderPath? renderPath)
    {
        this.renderPath = renderPath;
        CollectRenderPathParameters();
        renderPasses = renderPath?.RepairEnabledRenderPasses(renderPasses) ?? [];
        Touch();
    }

    public bool SetRenderPath(string resourceName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceName);

        var cache = Context.Instance.GetSubsystem<ResourceCache>();
        if (cache is null)
            return false;

        var loaded = cache.GetResource<RenderPath>(resourceName);
        SetRenderPath(loaded);
        return true;
    }

    public void SetRenderPasses(IEnumerable<RenderPassToggle> renderPasses)
    {
        ArgumentNullException.ThrowIfNull(renderPasses);
        this.renderPasses = renderPath?.RepairEnabledRenderPasses(renderPasses) ?? renderPasses.ToArray();
        Touch();
    }

    public void SetRenderPassEnabled(string passName, bool enabled)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(passName);

        var updated = renderPasses.ToList();
        var index = updated.FindIndex(item => string.Equals(item.Name, passName, StringComparison.Ordinal));
        if (index < 0)
            updated.Add(new RenderPassToggle(passName, enabled));
        else
            updated[index] = updated[index] with { Enabled = enabled };
        SetRenderPasses(updated);
    }

    public void SetRenderPathParameters(IEnumerable<KeyValuePair<string, object?>> parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        renderPathParameters.Clear();
        foreach (var (name, value) in parameters)
            renderPathParameters[name] = value;
        Touch();
    }

    public void UpdateRenderPathParameter(string name, object? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        renderPathParameters[name] = value;
        Touch();
    }

    public void SetFrameShaderParameters(IEnumerable<KeyValuePair<string, object?>> parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        frameShaderParameters.Clear();
        foreach (var (name, value) in parameters)
            frameShaderParameters[name] = value;
        Touch();
    }

    public void SetFrameShaderParameter(string name, object? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        frameShaderParameters[name] = value;
        Touch();
    }

    public virtual RenderPipelineView Instantiate() => new DefaultRenderPipelineView(this);

    protected internal override void OnSceneSet(Scene? previousScene, Scene? scene)
    {
        base.OnSceneSet(previousScene, scene);

        if (scene is not null)
            EnsureDefaultRenderPath();
    }

    private void CollectRenderPathParameters()
    {
        if (renderPath is null)
            return;

        var collected = new Dictionary<string, object?>(StringComparer.Ordinal);
        renderPath.CollectParameters(collected);
        foreach (var (name, value) in collected)
        {
            if (!renderPathParameters.ContainsKey(name))
                renderPathParameters[name] = value;
        }
    }

    private void Touch()
    {
        unchecked
        {
            ++revision;
        }
    }

    private void EnsureDefaultRenderPath()
    {
        if (renderPath is not null)
            return;

        var cache = Context.TryGetInstance()?.GetSubsystem<ResourceCache>();
        if (cache?.GetResourceOrNull<RenderPath>(DefaultRenderPathName) is { } defaultRenderPath)
            SetRenderPath(defaultRenderPath);
    }

    private sealed class DefaultRenderPipelineView : RenderPipelineView
    {
        private readonly RenderPipeline renderPipeline;
        private readonly RenderBufferManager renderBufferManager = new();
        private readonly SharedRenderPassState sharedState = new();
        private readonly Dictionary<string, object?> effectiveRenderPathParameters = new(StringComparer.Ordinal);
        private readonly RenderPipelineStats stats = new();
        private CommonFrameInfo frameInfo = new();
        private RenderPath? renderPath;
        private Viewport? viewport;
        private uint syncedRevision;

        public DefaultRenderPipelineView(RenderPipeline renderPipeline)
        {
            this.renderPipeline = renderPipeline ?? throw new ArgumentNullException(nameof(renderPipeline));
        }

        public override bool IsLinearColorSpace => renderBufferManager.IsLinearColorSpace();

        public override bool Define(Viewport viewport, Graphics graphics)
        {
            ArgumentNullException.ThrowIfNull(viewport);
            ArgumentNullException.ThrowIfNull(graphics);

            if (!renderPipeline.IsAttached || !ReferenceEquals(renderPipeline.Scene, viewport.Scene))
                return false;

            this.viewport = viewport;
            SyncFromPipeline();

            var viewportRect = viewport.Rect != IntRect.Zero
                ? viewport.Rect
                : new IntRect(0, 0, graphics.Width, graphics.Height);

            frameInfo = new CommonFrameInfo
            {
                FrameNumber = checked((uint)(Context.Instance.GetSubsystem<EngineTime>()?.FrameNumber ?? 0)),
                TimeStep = Context.Instance.GetSubsystem<EngineTime>()?.TimeStep ?? 0.0f,
                Viewport = viewport,
                ViewportRect = viewportRect,
                ViewportSize = viewportRect.Size,
                RenderTarget = null
            };
            frameInfo.Cameras[0] = viewport.Camera;
            frameInfo.Cameras[1] = viewport.RightEye;

            renderBufferManager.SetSettings(renderPipeline.Settings.RenderBufferManager);
            renderBufferManager.SetFrameSettings(new RenderBufferManagerFrameSettings());
            renderBufferManager.OnViewportDefined(null, viewportRect);
            return true;
        }

        public override void Update(Graphics graphics)
        {
            ArgumentNullException.ThrowIfNull(graphics);
            if (viewport is null)
                return;

            SyncFromPipeline();
            RaiseUpdateBegin(frameInfo);

            sharedState.RenderPipelineInterface = this;
            sharedState.RenderCamera = viewport.Camera;
            sharedState.RenderBuffers.Clear();
            renderBufferManager.PopulateSharedState(sharedState);

            if (renderPath is not null)
            {
                renderPath.UpdateParameters(renderPipeline.Settings, renderPipeline.GetRenderPasses(), effectiveRenderPathParameters);
                var traits = renderPath.GetAggregatedPassTraits();
                renderBufferManager.SetFrameSettings(new RenderBufferManagerFrameSettings
                {
                    ReadableColor = traits.NeedBilinearColorSampler,
                    SupportColorReadWrite = traits.NeedReadWriteColorBuffer
                });
                renderBufferManager.OnViewportDefined(frameInfo.RenderTarget, frameInfo.ViewportRect);
                sharedState.RenderBuffers.Clear();
                renderBufferManager.PopulateSharedState(sharedState);
                renderPath.Update(sharedState);
            }

            stats.NumGeometries = (uint)viewport.Scene.StaticModels.Count;
            RaiseUpdateEnd(frameInfo);
        }

        public override void Render(Graphics graphics, MaterialQuality materialQuality, string? screenshotPath = null)
        {
            ArgumentNullException.ThrowIfNull(graphics);
            if (viewport is null)
                return;

            RaiseRenderBegin(frameInfo);

            renderBufferManager.SetOutputRenderTargets(graphics);
            graphics.ClearCurrentRenderTargets();
            graphics.RenderScene(viewport, materialQuality, frameInfo.ViewportRect);
            renderPath?.Render(sharedState);

            graphics.PresentFrame(screenshotPath);
            graphics.UnbindRenderTargets();
            RaiseCollectStatistics(stats);
            RaiseRenderEnd(frameInfo);
        }

        public override CommonFrameInfo GetFrameInfo() => frameInfo;

        public override RenderPipelineStats GetStats() => stats;

        private void SyncFromPipeline()
        {
            if (syncedRevision == renderPipeline.Revision)
                return;

            syncedRevision = renderPipeline.Revision;
            renderPath = renderPipeline.GetRenderPath()?.Clone();
            effectiveRenderPathParameters.Clear();
            if (renderPath is not null)
                renderPath.CollectParameters(effectiveRenderPathParameters);
            foreach (var (name, value) in renderPipeline.GetRenderPathParameters())
                effectiveRenderPathParameters[name] = value;
            renderPath?.InitializeView(this);
        }
    }
}
