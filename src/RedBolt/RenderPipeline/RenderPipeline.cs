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

    public event Action<RenderPipeline, RenderPipelineSettings>? SettingsChanged;

    public event Action<RenderPipeline>? ParametersChanged;

    public event Action<RenderPipeline, RenderPath?>? RenderPathChanged;

    public void SetSettings(RenderPipelineSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        this.settings = settings;
        MarkSettingsDirty();
    }

    public void SetRenderPath(RenderPath? renderPath)
    {
        if (this.renderPath is not null)
            this.renderPath.ReloadFinished -= OnRenderPathReloadFinished;

        this.renderPath = renderPath;
        if (this.renderPath is not null)
            this.renderPath.ReloadFinished += OnRenderPathReloadFinished;
        OnRenderPathReloaded();
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
        var sourcePasses = renderPasses as IReadOnlyList<RenderPassToggle> ?? renderPasses.ToArray();
        this.renderPasses = renderPath?.RepairEnabledRenderPasses(sourcePasses) ?? sourcePasses;
        RaiseParametersChanged();
    }

    public void SetRenderPassEnabled(string passName, bool enabled)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(passName);

        var index = -1;
        for (var i = 0; i < renderPasses.Count; ++i)
        {
            if (string.Equals(renderPasses[i].Name, passName, StringComparison.Ordinal))
            {
                index = i;
                break;
            }
        }

        if (index < 0)
        {
            var updated = new RenderPassToggle[renderPasses.Count + 1];
            for (var i = 0; i < renderPasses.Count; ++i)
                updated[i] = renderPasses[i];
            updated[^1] = new RenderPassToggle(passName, enabled);
            this.renderPasses = renderPath?.RepairEnabledRenderPasses(updated) ?? updated;
            RaiseParametersChanged();
            return;
        }

        if (renderPasses[index].Enabled == enabled)
            return;

        var toggledPasses = renderPasses.ToArray();
        toggledPasses[index] = toggledPasses[index] with { Enabled = enabled };
        renderPasses = toggledPasses;
        RaiseParametersChanged();
    }

    public void SetRenderPathParameters(IEnumerable<KeyValuePair<string, object?>> parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        renderPathParameters.Clear();
        foreach (var (name, value) in parameters)
            renderPathParameters[name] = value;
        RaiseParametersChanged();
    }

    public void UpdateRenderPathParameter(string name, object? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (!renderPathParameters.TryGetValue(name, out var currentValue))
            return;

        if (EqualityComparer<object?>.Default.Equals(currentValue, value))
            return;

        renderPathParameters[name] = value;
        RaiseParametersChanged();
    }

    public void SetFrameShaderParameters(IEnumerable<KeyValuePair<string, object?>> parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        frameShaderParameters.Clear();
        foreach (var (name, value) in parameters)
            frameShaderParameters[name] = value;
    }

    public void SetFrameShaderParameter(string name, object? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        frameShaderParameters[name] = value;
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

    private void MarkSettingsDirty()
    {
        settings.Validate();
        settings.PropagateImpliedSettings();
        settings.AdjustToSupported(Context.TryGetInstance()?.GetSubsystem<Graphics>());
        Touch();
        SettingsChanged?.Invoke(this, settings);
    }

    private void RaiseParametersChanged()
    {
        Touch();
        ParametersChanged?.Invoke(this);
    }

    private void OnRenderPathReloaded()
    {
        if (renderPath is not null)
        {
            CollectRenderPathParameters();
            renderPasses = renderPath.RepairEnabledRenderPasses(renderPasses);
        }
        else
        {
            renderPasses = [];
        }

        Touch();
        RenderPathChanged?.Invoke(this, renderPath);
    }

    private void OnRenderPathReloadFinished(Resource resource)
    {
        if (ReferenceEquals(resource, renderPath))
            OnRenderPathReloaded();
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
        private readonly List<OutlineGroup> outlineGroups = [];
        private TextureRenderBuffer? outlineBuffer;
        private TextureRenderBuffer? normalBuffer;
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

            var engineTime = Context.TryGetInstance()?.GetSubsystem<EngineTime>();
            frameInfo.FrameNumber = checked((uint)(engineTime?.FrameNumber ?? 0));
            frameInfo.TimeStep = engineTime?.TimeStep ?? 0.0f;
            frameInfo.Viewport = viewport;
            frameInfo.ViewportRect = viewportRect;
            frameInfo.ViewportSize = viewportRect.Size;
            frameInfo.RenderTarget = graphics.GetSwapChainOutputTarget();
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
            sharedState.Graphics = graphics;
            sharedState.RenderBuffers.Clear();
            renderBufferManager.PopulateSharedState(sharedState);

            if (renderPath is not null)
            {
                renderPath.UpdateParameters(renderPipeline.Settings, renderPipeline.GetRenderPasses(), effectiveRenderPathParameters);
                var traits = renderPath.GetAggregatedPassTraits();
                renderBufferManager.SetFrameSettings(new RenderBufferManagerFrameSettings
                {
                    ReadableColor = traits.NeedBilinearColorSampler,
                    SupportColorReadWrite = traits.NeedReadWriteColorBuffer,
                    FilteredColor = traits.NeedBilinearColorSampler,
                    ReadableDepth = traits.NeedReadableDepth
                });
                renderBufferManager.OnViewportDefined(frameInfo.RenderTarget, frameInfo.ViewportRect);
                sharedState.RenderBuffers.Clear();
                renderBufferManager.PopulateSharedState(sharedState);
                renderPath.Update(sharedState);
            }

            stats.NumGeometries = (uint)viewport.Scene.StaticModels.Count;
            RaiseUpdateEnd(frameInfo);
        }

        public override void Render(Graphics graphics, MaterialQuality materialQuality, Action? beforePresent = null, string? screenshotPath = null)
        {
            ArgumentNullException.ThrowIfNull(graphics);
            if (viewport is null)
                return;

            RaiseRenderBegin(frameInfo);

            sharedState.Graphics = graphics;
            Context.Instance.SendEvent(GraphicsEvents.BeginRendering);
            renderBufferManager.OnRenderBegin(frameInfo);
            renderBufferManager.SetOutputRenderTargets(graphics);
            graphics.ClearCurrentRenderTargets();
            graphics.RenderScene(viewport, materialQuality, frameInfo.ViewportRect);
            RenderNormalBuffer(graphics, materialQuality);
            RenderOutlines(graphics, materialQuality);
            renderPath?.Render(sharedState);
            renderBufferManager.OnRenderEnd(frameInfo);
            Context.Instance.SendEvent(GraphicsEvents.EndRendering);

            graphics.PresentFrame(screenshotPath, beforePresent);
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

        private void RenderOutlines(Graphics graphics, MaterialQuality materialQuality)
        {
            if (viewport is null || renderPath is null || !HasEnabledOutlinePass())
                return;

            CollectOutlineGroups(viewport.Scene);
            if (outlineGroups.Count == 0)
                return;

            var outlineRenderBuffer = EnsureOutlineBuffer();
            sharedState.RenderBuffers[OutlineRenderPass.ColorBufferId] = outlineRenderBuffer;

            renderBufferManager.SetRenderTargets(graphics, outlineRenderBuffer);
            graphics.ClearCurrentRenderTargets(Color.Transparent, clearColorTarget: true, clearDepth: false, clearStencil: false);
            graphics.RenderOutlineGroups(viewport, materialQuality, outlineGroups, frameInfo.ViewportRect);
            renderBufferManager.SetOutputRenderTargets(graphics);
        }

        private void RenderNormalBuffer(Graphics graphics, MaterialQuality materialQuality)
        {
            if (viewport is null || renderPath is null || !HasEnabledNormalBufferPass())
                return;

            var normalRenderBuffer = EnsureNormalBuffer();
            sharedState.RenderBuffers[SharedRenderPassState.NormalBufferId] = normalRenderBuffer;

            renderBufferManager.SetRenderTargets(
                graphics,
                normalRenderBuffer,
                renderBufferManager.GetDepthStencilOutput(),
                readOnlyDepth: true);
            graphics.ClearCurrentRenderTargets(Color.Transparent, clearColorTarget: true, clearDepth: false, clearStencil: false);
            graphics.RenderNormalBuffer(viewport, materialQuality, frameInfo.ViewportRect);
            renderBufferManager.SetOutputRenderTargets(graphics);
        }

        private bool HasEnabledOutlinePass()
        {
            return renderPath is not null && renderPath.GetPasses().Any(static pass => pass is OutlineRenderPass && pass.IsEnabledEffectively());
        }

        private bool HasEnabledNormalBufferPass()
        {
            return renderPath is not null && renderPath.GetPasses().Any(static pass => pass is AmbientOcclusionPass && pass.IsEnabledEffectively());
        }

        private void CollectOutlineGroups(Scene scene)
        {
            outlineGroups.Clear();
            foreach (var outlineGroup in scene.GetAllComponents().Values.OfType<OutlineGroup>())
            {
                if (!outlineGroup.IsAttached || !outlineGroup.IsEnabledEffective)
                    continue;
                if (outlineGroup.IsDebug() && !viewport!.Camera.GetDrawDebugGeometry())
                    continue;

                outlineGroups.Add(outlineGroup);
            }
        }

        private TextureRenderBuffer EnsureOutlineBuffer()
        {
            if (outlineBuffer?.GetTexture() is Texture texture
                && texture.Width == frameInfo.ViewportRect.Width
                && texture.Height == frameInfo.ViewportRect.Height)
            {
                outlineBuffer.SetViewportRect(frameInfo.ViewportRect);
                return outlineBuffer;
            }

            outlineBuffer = renderBufferManager.CreateColorBuffer(new RenderBufferParams(TextureFormat.RGBA8)) as TextureRenderBuffer
                ?? throw new InvalidOperationException("Outline render buffer must be backed by a TextureRenderBuffer.");
            outlineBuffer.SetViewportRect(frameInfo.ViewportRect);
            return outlineBuffer;
        }

        private TextureRenderBuffer EnsureNormalBuffer()
        {
            if (normalBuffer?.GetTexture() is Texture texture
                && texture.Width == frameInfo.ViewportRect.Width
                && texture.Height == frameInfo.ViewportRect.Height)
            {
                normalBuffer.SetViewportRect(frameInfo.ViewportRect);
                return normalBuffer;
            }

            normalBuffer = renderBufferManager.CreateColorBuffer(new RenderBufferParams(TextureFormat.RGBA8)) as TextureRenderBuffer
                ?? throw new InvalidOperationException("Normal render buffer must be backed by a TextureRenderBuffer.");
            normalBuffer.SetViewportRect(frameInfo.ViewportRect);
            return normalBuffer;
        }
    }
}
