namespace RedBolt;

using Microsoft.Extensions.Logging;

/// <summary>
/// Scene component that spawns render pipeline instances.
/// </summary>
public class RenderPipeline : Component
{
    public const string DefaultRenderPathName = "RenderPaths/Default.renderpath";

    private readonly Dictionary<string, object?> _renderPathParameters = new(StringComparer.Ordinal);
    private readonly Dictionary<string, object?> _frameShaderParameters = new(StringComparer.Ordinal);
    private IReadOnlyList<RenderPassToggle> _renderPasses = [];
    private RenderPath? _renderPath;
    private RenderPipelineSettings _settings = new();
    private uint _revision = 1;

    [ThreadStatic]
    private static VariantVector? _renderPassesAttributeResult;

    public RenderPipeline()
    {
        SetRenderPathAttr(new ResourceRef(new StringHash(nameof(RenderPath)), DefaultRenderPathName));

        // Enable instancing by default for default render pipeline
        _settings.InstancingBuffer.EnableInstancing = true;
        _settings.Validate();
        _settings.AdjustToSupported(GetSubsystem<Graphics>());
    }

    public RenderPipelineSettings Settings => _settings;

    public RenderPath? GetRenderPath() => _renderPath;

    public ResourceRef GetRenderPathAttr() => Resource.GetResourceRef(_renderPath, new StringHash(nameof(RenderPath)));

    public void SetRenderPathAttr(ResourceRef value)
    {
        // This function may be called from constructor, check for cache just in case.
        if (GetSubsystem<ResourceCache>() is { } cache)
            SetRenderPath(cache.GetResourceOrNull<RenderPath>(value.Name));
    }

    public IReadOnlyList<RenderPassToggle> GetRenderPasses() => _renderPasses;

    public VariantVector GetRenderPassesAttr()
    {
        var result = _renderPassesAttributeResult ??= new VariantVector();
        result.Clear();
        foreach (var pass in _renderPasses)
        {
            result.Add(new Variant(pass.Name));
            result.Add(new Variant(pass.Enabled));
        }
        return result;
    }

    public void SetRenderPassesAttr(VariantVector value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var numPasses = value.Count / 2;
        var renderPasses = new RenderPassToggle[numPasses];
        for (var i = 0; i < numPasses; ++i)
            renderPasses[i] = new RenderPassToggle(value[i * 2].GetString(), value[i * 2 + 1].GetBool());
        SetRenderPasses(renderPasses);
    }

    public IReadOnlyDictionary<string, object?> GetRenderPathParameters() => _renderPathParameters;

    public IReadOnlyDictionary<string, object?> GetFrameShaderParameters() => _frameShaderParameters;

    public uint Revision => _revision;

    /// <summary>
    /// Invoked when settings change.
    /// </summary>
    public event Action<RenderPipeline, RenderPipelineSettings>? SettingsChanged;

    /// <summary>
    /// Invoked when render path parameters change.
    /// </summary>
    public event Action<RenderPipeline>? ParametersChanged;

    /// <summary>
    /// Invoked when render path changes.
    /// </summary>
    public event Action<RenderPipeline, RenderPath?>? RenderPathChanged;

    public void SetSettings(RenderPipelineSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        this._settings = settings;
        MarkSettingsDirty();
    }

    public void SetRenderPath(RenderPath? renderPath)
    {
        if (this._renderPath is not null)
            this._renderPath.ReloadFinished -= OnRenderPathReloadFinished;

        this._renderPath = renderPath;
        OnRenderPathReloaded();
        if (this._renderPath is not null)
            this._renderPath.ReloadFinished += OnRenderPathReloadFinished;
    }

    public bool SetRenderPath(string resourceName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceName);

        var cache = GetSubsystem<ResourceCache>()!;
        var loaded = cache.GetResource<RenderPath>(resourceName);
        SetRenderPath(loaded);
        return true;
    }

    public void SetRenderPasses(IEnumerable<RenderPassToggle> renderPasses)
    {
        ArgumentNullException.ThrowIfNull(renderPasses);
        var sourcePasses = renderPasses as IReadOnlyList<RenderPassToggle> ?? renderPasses.ToArray();
        this._renderPasses = _renderPath?.RepairEnabledRenderPasses(sourcePasses) ?? sourcePasses;
        RaiseParametersChanged();
    }

    /// <summary>
    /// Update render pass enabled state.
    /// </summary>
    public void SetRenderPassEnabled(string passName, bool enabled)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(passName);

        var index = -1;
        for (var i = 0; i < _renderPasses.Count; ++i)
        {
            if (string.Equals(_renderPasses[i].Name, passName, StringComparison.Ordinal))
            {
                index = i;
                break;
            }
        }

        if (index < 0)
        {
            var updated = new RenderPassToggle[_renderPasses.Count + 1];
            for (var i = 0; i < _renderPasses.Count; ++i)
                updated[i] = _renderPasses[i];
            updated[^1] = new RenderPassToggle(passName, enabled);
            this._renderPasses = _renderPath?.RepairEnabledRenderPasses(updated) ?? updated;
            RaiseParametersChanged();
            return;
        }

        if (_renderPasses[index].Enabled == enabled)
            return;

        var toggledPasses = _renderPasses.ToArray();
        toggledPasses[index] = toggledPasses[index] with { Enabled = enabled };
        _renderPasses = toggledPasses;
        RaiseParametersChanged();
    }

    public void SetRenderPathParameters(IEnumerable<KeyValuePair<string, object?>> parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        _renderPathParameters.Clear();
        foreach (var (name, value) in parameters)
            _renderPathParameters[name] = value;
        RaiseParametersChanged();
    }

    public void UpdateRenderPathParameters(IReadOnlyDictionary<StringHash, object?> parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        var modified = false;
        foreach (var (nameHash, newValue) in parameters)
        {
            foreach (var (name, currentValue) in _renderPathParameters)
            {
                if (StringHash.Calculate(name) != nameHash.Value)
                    continue;

                if (!EqualityComparer<object?>.Default.Equals(currentValue, newValue))
                {
                    _renderPathParameters[name] = newValue;
                    modified = true;
                }
                break;
            }
        }

        if (modified)
            RaiseParametersChanged();
    }

    /// <summary>
    /// Update existing render path parameter.
    /// </summary>
    public void UpdateRenderPathParameter(string name, object? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (!_renderPathParameters.TryGetValue(name, out var currentValue))
            return;

        if (EqualityComparer<object?>.Default.Equals(currentValue, value))
            return;

        _renderPathParameters[name] = value;
        RaiseParametersChanged();
    }

    public void SetFrameShaderParameters(IEnumerable<KeyValuePair<string, object?>> parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        _frameShaderParameters.Clear();
        foreach (var (name, value) in parameters)
            _frameShaderParameters[name] = value;
    }

    public void UpdateFrameShaderParameters(IReadOnlyDictionary<StringHash, object?> parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        foreach (var (nameHash, newValue) in parameters)
        {
            foreach (var name in _frameShaderParameters.Keys)
            {
                if (StringHash.Calculate(name) != nameHash.Value)
                    continue;

                _frameShaderParameters[name] = newValue;
                break;
            }
        }
    }

    /// <summary>
    /// Update existing render path parameter.
    /// </summary>
    public void UpdateFrameShaderParameter(StringHash nameHash, object? value)
    {
        foreach (var name in _frameShaderParameters.Keys)
        {
            if (StringHash.Calculate(name) != nameHash.Value)
                continue;

            _frameShaderParameters[name] = value;
            return;
        }
    }

    /// <summary>
    /// Add or update render path parameter.
    /// </summary>
    public void SetFrameShaderParameter(string name, object? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _frameShaderParameters[name] = value;
    }

    /// <summary>
    /// Create new instance of render pipeline.
    /// </summary>
    public virtual RenderPipelineView Instantiate() => new DefaultRenderPipelineView(this);

    protected internal override void OnSceneSet(Scene? previousScene, Scene? scene)
    {
        base.OnSceneSet(previousScene, scene);

        if (scene is not null)
            EnsureDefaultRenderPath();
    }

    private void CollectRenderPathParameters()
    {
        if (_renderPath is null)
            return;

        var collected = new Dictionary<string, object?>(StringComparer.Ordinal);
        _renderPath.CollectParameters(collected);
        foreach (var (name, value) in collected)
        {
            if (!_renderPathParameters.ContainsKey(name))
                _renderPathParameters[name] = value;
        }
    }

    // OnSettingsChanged(this, settings_);
    // No PropagateImpliedSettings() call, no revision increment.
    private void MarkSettingsDirty()
    {
        _settings.Validate();
        _settings.AdjustToSupported(GetSubsystem<Graphics>());
        SettingsChanged?.Invoke(this, _settings);
    }

    private void RaiseParametersChanged()
    {
        Touch();
        ParametersChanged?.Invoke(this);
    }

    private void OnRenderPathReloaded()
    {
        if (_renderPath is not null)
        {
            CollectRenderPathParameters();
            _renderPasses = _renderPath.RepairEnabledRenderPasses(_renderPasses);
        }
        else
        {
            _renderPasses = [];
        }

        Touch();
        RenderPathChanged?.Invoke(this, _renderPath);
    }

    private void OnRenderPathReloadFinished(Resource resource)
    {
        if (ReferenceEquals(resource, _renderPath))
            OnRenderPathReloaded();
    }

    private void Touch()
    {
        unchecked
        {
            ++_revision;
        }
    }

    private void EnsureDefaultRenderPath()
    {
        if (_renderPath is not null)
            return;

        var cache = GetSubsystem<ResourceCache>();
        if (cache?.GetResourceOrNull<RenderPath>(DefaultRenderPathName) is { } defaultRenderPath)
            SetRenderPath(defaultRenderPath);
    }

    /// <summary>
    /// Default implementation of render pipeline instance.
    /// </summary>
    private sealed class DefaultRenderPipelineView : RenderPipelineView
    {
        private readonly RenderPipeline _renderPipeline;
        private RenderBufferManager? _renderBufferManager;
        private ShadowMapAllocator? _shadowMapAllocator;
        private InstancingBuffer? _instancingBuffer;
        private SceneProcessor? _sceneProcessor;
        private UnorderedScenePass? _opaquePass;
        private UnorderedScenePass? _postOpaquePass;
        private UnorderedScenePass? _depthPrePass;
        private BackToFrontScenePass? _alphaPass;
        private BackToFrontScenePass? _postAlphaPass;
        private OutlineScenePass? _outlineScenePass;
        private readonly SharedRenderPassState _sharedState = new();
        private readonly Dictionary<string, object?> _effectiveRenderPathParameters = new(StringComparer.Ordinal);
        private readonly RenderPipelineStats _stats = new();
        private readonly List<OutlineGroup> _outlineGroups = [];
        private TextureRenderBuffer? _outlineBuffer;
        private CommonFrameInfo _frameInfo = new();
        private RenderPath? _renderPath;
        private Viewport? _viewport;
        private RenderSurface? _renderTarget;
        private uint _syncedRevision;
        private bool _settingsDirty = true;
        private bool _parametersDirty = true;
        private uint _settingsHash;
        private bool _linearColorSpace;
        private RenderOutputFlag _renderOutputFlags;

        public DefaultRenderPipelineView(RenderPipeline renderPipeline)
            : base(renderPipeline)
        {
            this._renderPipeline = renderPipeline ?? throw new ArgumentNullException(nameof(renderPipeline));
        }

        public override bool IsLinearColorSpace => _linearColorSpace;

        public override bool Define(RenderSurface? renderTarget, Viewport viewport, Graphics graphics)
        {
            ArgumentNullException.ThrowIfNull(viewport);
            ArgumentNullException.ThrowIfNull(graphics);

            if (!_renderPipeline.IsAttached || !ReferenceEquals(_renderPipeline.Scene, viewport.Scene))
                return false;

            this._viewport = viewport;
            _renderTarget = renderTarget;
            SyncFromPipeline();

            if (_sceneProcessor is null)
            {
                _sharedState.RenderPipelineInterface = this;
                _renderBufferManager = new RenderBufferManager(this);
                _sharedState.RenderBufferManager = _renderBufferManager;
                _shadowMapAllocator = new ShadowMapAllocator();
                _instancingBuffer = new InstancingBuffer();
                _sceneProcessor = new SceneProcessor(this, "shadow", _shadowMapAllocator, _instancingBuffer);

                var drawableProcessor = _sceneProcessor.GetDrawableProcessor();
                var pipelineStateBuilder = _sceneProcessor.GetPipelineStateBuilder();

                _opaquePass = new UnorderedScenePass(this, drawableProcessor, pipelineStateBuilder,
                    DrawableProcessorPassFlag.HasAmbientLighting,
                    string.Empty, "base", "litbase", "light");
                _postOpaquePass = new UnorderedScenePass(this, drawableProcessor, pipelineStateBuilder,
                    DrawableProcessorPassFlag.None, "postopaque");
                _alphaPass = new BackToFrontScenePass(this, drawableProcessor, pipelineStateBuilder,
                    DrawableProcessorPassFlag.HasAmbientLighting | DrawableProcessorPassFlag.NeedReadableDepth
                    | DrawableProcessorPassFlag.RefractionPass | DrawableProcessorPassFlag.ReadOnlyDepth,
                    string.Empty, "alpha", "alpha", "litalpha");
                _postAlphaPass = new BackToFrontScenePass(this, drawableProcessor, pipelineStateBuilder,
                    DrawableProcessorPassFlag.ReadOnlyDepth, "postalpha");
                _outlineScenePass = new OutlineScenePass(this, drawableProcessor, pipelineStateBuilder,
                    ["deferred", "deferred_decal", "base", "alpha"]);

                _sceneProcessor.SetPasses(
                [
                    _depthPrePass, _opaquePass, null, _postOpaquePass, _alphaPass, _postAlphaPass, _outlineScenePass
                ]);

                var outlineParams = new RenderBufferParams(TextureFormat.RGBA8, 1, RenderBufferFlag.BilinearFiltering);
                _outlineBuffer = _renderBufferManager.CreateColorBuffer(outlineParams) as TextureRenderBuffer;
                _outlineBuffer?.SetEnabled(false);
                if (_outlineBuffer is not null)
                    _sharedState.RenderBuffers[OutlineRenderPass.ColorBufferId] = _outlineBuffer;
            }

            var viewportRect = viewport.GetEffectiveRect(renderTarget);
            var engineTime = _renderPipeline.GetSubsystem<Time>();
            _frameInfo.FrameNumber = checked((uint)(engineTime?.FrameNumber ?? 0U));
            _frameInfo.TimeStep = engineTime?.TimeStep ?? 0.0f;
            _frameInfo.Viewport = viewport;
            _frameInfo.ViewportRect = viewportRect;
            _frameInfo.ViewportSize = viewportRect.Size;
            _frameInfo.Scene = viewport.Scene;
            _frameInfo.Camera = viewport.Camera;
            _frameInfo.Cameras.First = viewport.Camera;
            _frameInfo.Cameras.Second = viewport.RightEye;

            var colorTexture = renderTarget?.GetParentTexture();
            var depthSurface = renderTarget?.GetLinkedDepthStencil();
            var depthTexture = depthSurface?.GetParentTexture();
            _frameInfo.RenderTarget = colorTexture is not null
                ? new RenderOutputTargetInfo(
                    renderTarget!.GetView(),
                    new IntRect(0, 0, renderTarget.GetWidth(), renderTarget.GetHeight()),
                    colorTexture.Format,
                    depthSurface?.GetView(),
                    depthTexture?.Format ?? TextureFormat.Unknown,
                    renderTarget.GetMultiSample(),
                    colorTexture,
                    depthTexture,
                    colorTexture.GetFilterMode() is TextureFilterMode.Bilinear or TextureFilterMode.Trilinear
                        or TextureFilterMode.Anisotropic,
                    colorTexture.GetLinear() || colorTexture.GetSRGB()
                        || TextureFormatUtility.IsFloatColorFormat(colorTexture.Format),
                    TextureFormatUtility.IsFloatColorFormat(colorTexture.Format),
                    depthTexture is not null,
                    depthTexture?.Format == TextureFormat.Depth24Stencil8)
                : null;

            if (!_sceneProcessor!.Define(_frameInfo))
                return false;

            _sceneProcessor.SetRenderCamera(viewport.Camera);
            _sharedState.RenderCamera = viewport.Camera;

            if (_settingsDirty)
            {
                _settingsDirty = false;
                _parametersDirty = true;
                ApplySettings();
            }

            if (_parametersDirty && _renderPath is not null)
            {
                _parametersDirty = false;
                _renderPath.UpdateParameters(
                    _renderPipeline.Settings, _renderPipeline.GetRenderPasses(), _effectiveRenderPathParameters);
            }

            _renderBufferManager!.SetSettings(_renderPipeline.Settings.RenderBufferManager);
            _renderBufferManager.OnViewportDefined(_frameInfo.RenderTarget, _frameInfo.ViewportRect);
            _linearColorSpace = _renderBufferManager.IsLinearColorSpace();

            var standardOutputDesc = new PipelineStateOutputDesc
            {
                DepthStencilFormat = _renderBufferManager.GetOutputDepthStencilFormat(),
                MultiSample = (uint)_renderBufferManager.GetOutputMultiSample()
            };
            standardOutputDesc.SetRenderTargetFormat(_renderBufferManager.GetOutputColorFormat());

            _opaquePass!.SetForwardOutputDesc(standardOutputDesc);
            _depthPrePass?.SetForwardOutputDesc(standardOutputDesc);
            _postOpaquePass!.SetForwardOutputDesc(standardOutputDesc);
            _alphaPass!.SetForwardOutputDesc(standardOutputDesc);
            _postAlphaPass!.SetForwardOutputDesc(standardOutputDesc);

            var batchCompositor = _sceneProcessor.GetBatchCompositor();
            batchCompositor.SetLightVolumesOutputDesc(standardOutputDesc);
            if (_shadowMapAllocator is not null)
                batchCompositor.SetShadowOutputDesc(_shadowMapAllocator.ShadowOutputDesc);

            return true;
        }

        public override void Update(Graphics graphics)
        {
            ArgumentNullException.ThrowIfNull(graphics);
            if (_viewport is null || _sceneProcessor is null)
                return;

            SyncFromPipeline();

            var engineTime = _renderPipeline.GetSubsystem<Time>();
            _frameInfo.FrameNumber = checked((uint)(engineTime?.FrameNumber ?? 0U));
            _frameInfo.TimeStep = engineTime?.TimeStep ?? 0.0f;

            var input = _renderPipeline.GetSubsystem<Input>();
            var qualifiers = input?.GetQualifiers() ?? Qualifier.None;
            var takeSnapshot = (qualifiers & (Qualifier.Ctrl | Qualifier.Shift)) == (Qualifier.Ctrl | Qualifier.Shift)
                && input!.GetKeyPress(Key.F12);
            if (takeSnapshot)
                GetDebugger().BeginSnapshot();

            _shadowMapAllocator?.ResetAllShadowMaps();
            RaiseUpdateBegin(_frameInfo);
            SendBeginViewUpdateEvent();

            var pipelineStateHash = _renderPipeline.Settings.CalculatePipelineStateHash();
            if (_settingsHash != pipelineStateHash)
            {
                _settingsHash = pipelineStateHash;
                _settingsDirty = true;
                RaisePipelineStatesInvalidated();
            }

            var drawDebugGeometry = _renderPipeline.Settings.DrawDebugGeometry && _viewport.Camera.GetDrawDebugGeometry();
            _outlineScenePass?.SetOutlineGroups(_sceneProcessor.GetFrameInfo().Scene!, drawDebugGeometry);

            _sceneProcessor.Update();

            if (_outlineBuffer is not null && _outlineScenePass is not null)
                _outlineBuffer.SetEnabled(_outlineScenePass.IsEnabled() && _outlineScenePass.HasBatches());

            _sharedState.RenderPipelineInterface = this;
            _sharedState.RenderCamera = _viewport.Camera;
            _sharedState.Graphics = graphics;
            _sharedState.RenderBuffers.Clear();
            _renderBufferManager?.PopulateSharedState(_sharedState);

            if (_renderPath is not null)
            {
                _renderPath.UpdateParameters(_renderPipeline.Settings, _renderPipeline.GetRenderPasses(), _effectiveRenderPathParameters);
                _renderPath.Update(_sharedState);
            }

            UpdateFrameParameters();
            _stats.NumGeometries = _sceneProcessor.GetFrameInfo().Scene is { } scene
                ? (uint)scene.StaticModels.Count
                : 0u;

            SendEndViewUpdateEvent();
            RaiseUpdateEnd(_frameInfo);
        }

        public override void Render(Graphics graphics, MaterialQuality materialQuality)
        {
            ArgumentNullException.ThrowIfNull(graphics);
            if (_viewport is null || _sceneProcessor is null || _renderBufferManager is null)
                return;

            var caps = graphics.GetSubsystem<RenderDevice>()?.GetCaps() ?? default;
            var canReadDepth = _renderPipeline.Settings.RenderBufferManager.ReadableDepth && caps.ReadOnlyDepth;

            var hasRefraction = _alphaPass?.HasRefractionBatches() == true;
            var frameSettings = new RenderBufferManagerFrameSettings
            {
                SupportColorReadWrite = (_renderOutputFlags & RenderOutputFlag.NeedColorOutputReadAndWrite) != 0
            };
            if (hasRefraction)
                frameSettings.SupportColorReadWrite = true;
            _renderBufferManager.SetFrameSettings(frameSettings);

            RaiseRenderBegin(_frameInfo);
            SendBeginViewRenderEvent();
            _renderBufferManager.OnRenderBegin(_frameInfo, graphics);
            SendViewBuffersReadyEvent();

            _sceneProcessor.PrepareDrawablesBeforeRendering();
            _sceneProcessor.PrepareInstancingBuffer();
            _sceneProcessor.RenderShadowMaps();

            var camera = _sceneProcessor.GetFrameInfo().Camera ?? _viewport.Camera;
            var fogColorInGammaSpace = camera.GetEffectiveFogColor();
            var effectiveFogColor = _linearColorSpace ? fogColorInGammaSpace.GammaToLinear() : fogColorInGammaSpace;

            // Forward path (C++ DefaultRenderPipelineView::Render non-deferred branch)
            _renderBufferManager.ClearOutput(graphics, effectiveFogColor);
            _renderBufferManager.SetOutputRenderTargets(graphics);

            if (_depthPrePass is not null)
            {
                _sceneProcessor.RenderSceneBatches(
                    "DepthPrePass", camera, _depthPrePass.GetBaseBatches());
            }

            var cameraParameters = new ShaderParameterDesc[]
            {
                new(ShaderConsts.Camera_GBufferOffsets, new Variant(_renderBufferManager.GetDefaultClipToUVSpaceOffsetAndScale())),
                new(ShaderConsts.Camera_GBufferInvSize, new Variant(_renderBufferManager.GetInvOutputSize())),
            };
            var frameParameters = System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_frameParameters);

            _sceneProcessor.RenderSceneBatches(
                "OpaqueBase", camera, _opaquePass!.GetBaseBatches(), default, cameraParameters, frameParameters);
            _sceneProcessor.RenderSceneBatches(
                "OpaqueLight", camera, _opaquePass.GetLightBatches(), default, cameraParameters, frameParameters);
            _sceneProcessor.RenderSceneBatches(
                "PostOpaque", camera, _postOpaquePass!.GetBaseBatches(), default, cameraParameters, frameParameters);

            if (hasRefraction)
                _renderBufferManager.SwapColorBuffers(true);

            var depthAndColorTextures = new ShaderResourceDesc[]
            {
                new(ShaderResources.DepthBuffer, canReadDepth ? _renderBufferManager.GetDepthStencilTexture() : null),
                new(ShaderResources.Emission, _renderBufferManager.GetSecondaryColorTexture()),
            };

            if (canReadDepth)
                _renderBufferManager.SetOutputRenderTargets(graphics, true);

            _sceneProcessor.RenderSceneBatches(
                "Alpha", camera, _alphaPass!.GetBatches(), depthAndColorTextures, cameraParameters, frameParameters);
            _sceneProcessor.RenderSceneBatches(
                "PostAlpha", camera, _postAlphaPass!.GetBatches(), default, cameraParameters, frameParameters);

            if (_outlineBuffer is not null && _outlineBuffer.IsEnabled)
            {
                _renderBufferManager.SetRenderTargets(graphics, _outlineBuffer);
                graphics.ClearCurrentRenderTargets(Color.Transparent, clearColorTarget: true, clearDepth: false, clearStencil: false);
                _sceneProcessor.RenderSceneBatches(
                    "Outline", camera, _outlineScenePass!.GetBatches(), default, cameraParameters, frameParameters);
                _renderBufferManager.SetOutputRenderTargets(graphics);
            }

            _renderPath?.Render(_sharedState);

            var drawDebugGeometry = _renderPipeline.Settings.DrawDebugGeometry && _viewport.Camera.GetDrawDebugGeometry();
            var debug = _viewport.Scene.Root.GetComponent<DebugRenderer>();
            if (drawDebugGeometry && debug is not null && debug.IsEnabledEffective && debug.HasContent())
            {
                _renderBufferManager.SetOutputRenderTargets(graphics);
                debug.SetView(_viewport.Camera);
                debug.Render();
            }

            _renderBufferManager.OnRenderEnd(_frameInfo);
            graphics.UnbindRenderTargets();
            RaiseRenderEnd(_frameInfo);
            SendEndViewRenderEvent();
            RaiseCollectStatistics(_stats);

            if (GetDebugger().IsSnapshotInProgress())
            {
                GetDebugger().EndSnapshot();
                Log.Logger.LogInformation("RenderPipeline snapshot:\n\n{Snapshot}\n", GetDebugger().GetSnapshot().ToString());
            }
        }

        private void ApplySettings()
        {
            _renderPipeline.Settings.Validate();
            _renderPipeline.Settings.AdjustToSupported(_renderPipeline.GetSubsystem<Graphics>()!);
            _renderPipeline.Settings.PropagateImpliedSettings();
            _sceneProcessor?.SetSettings(_renderPipeline.Settings);
            _instancingBuffer?.SetSettings(_renderPipeline.Settings.InstancingBuffer);
            _shadowMapAllocator?.SetSettings(_renderPipeline.Settings.ShadowMapAllocator);
            _renderBufferManager?.SetSettings(_renderPipeline.Settings.RenderBufferManager);

            if (_renderPipeline.Settings.SceneProcessor.DepthPrePass && _depthPrePass is null && _sceneProcessor is not null)
            {
                _depthPrePass = new UnorderedScenePass(this, _sceneProcessor.GetDrawableProcessor(),
                    _sceneProcessor.GetPipelineStateBuilder(), DrawableProcessorPassFlag.None, "depth");
                _sceneProcessor.SetPasses(
                [
                    _depthPrePass, _opaquePass, null, _postOpaquePass, _alphaPass, _postAlphaPass, _outlineScenePass
                ]);
            }
        }

        public override CommonFrameInfo GetFrameInfo() => _frameInfo;

        public override RenderPipelineStats GetStats() => _stats;

        public override void DrawDebugGeometries(bool depthTest)
        {
            var scene = _frameInfo.Scene;
            var debug = scene?.Root.GetComponent<DebugRenderer>();
            if (scene is null || debug is null)
                return;

            foreach (var drawable in scene.GetAllComponents().Values.OfType<Drawable>())
            {
                if ((drawable.GetDrawableFlags() & DrawableFlags.Light) == 0)
                    drawable.DrawDebugGeometry(debug, depthTest);
            }
        }

        public override void DrawDebugLights(bool depthTest)
        {
            var scene = _frameInfo.Scene;
            var debug = scene?.Root.GetComponent<DebugRenderer>();
            if (scene is null || debug is null)
                return;

            foreach (var drawable in scene.GetAllComponents().Values.OfType<Light>())
                drawable.DrawDebugGeometry(debug, depthTest);
        }

        private void SyncFromPipeline()
        {
            if (_syncedRevision == _renderPipeline.Revision)
                return;

            _syncedRevision = _renderPipeline.Revision;
            _renderPath = _renderPipeline.GetRenderPath()?.Clone();
            _effectiveRenderPathParameters.Clear();
            if (_renderPath is not null)
                _renderPath.CollectParameters(_effectiveRenderPathParameters);
            foreach (var (name, value) in _renderPipeline.GetRenderPathParameters())
                _effectiveRenderPathParameters[name] = value;
            _renderPath?.InitializeView(this);
        }

        private void SendBeginViewUpdateEvent()
        {
            var eventData = new GraphicsEvents.BeginViewUpdateEvent
            {
                RenderPipelineView = this,
                Texture = _renderTarget?.GetParentTexture(),
                Surface = _renderTarget,
                Scene = _sceneProcessor!.GetFrameInfo().Scene,
                Camera = _sceneProcessor.GetFrameInfo().Camera
            };
            var sender = (Object?)eventData.Texture ?? _renderPipeline.GetSubsystem<Renderer>()!;
            sender.SendEvent(ref eventData);
        }

        private void SendEndViewUpdateEvent()
        {
            var eventData = new GraphicsEvents.EndViewUpdateEvent
            {
                RenderPipelineView = this,
                Texture = _renderTarget?.GetParentTexture(),
                Surface = _renderTarget,
                Scene = _sceneProcessor!.GetFrameInfo().Scene,
                Camera = _sceneProcessor.GetFrameInfo().Camera
            };
            var sender = (Object?)eventData.Texture ?? _renderPipeline.GetSubsystem<Renderer>()!;
            sender.SendEvent(ref eventData);
        }

        private void SendBeginViewRenderEvent()
        {
            var eventData = new GraphicsEvents.BeginViewRenderEvent
            {
                RenderPipelineView = this,
                Texture = _renderTarget?.GetParentTexture(),
                Surface = _renderTarget,
                Scene = _sceneProcessor!.GetFrameInfo().Scene,
                Camera = _sceneProcessor.GetFrameInfo().Camera
            };
            var sender = (Object?)eventData.Texture ?? _renderPipeline.GetSubsystem<Renderer>()!;
            sender.SendEvent(ref eventData);
        }

        private void SendViewBuffersReadyEvent()
        {
            var eventData = new GraphicsEvents.ViewBuffersReadyEvent
            {
                RenderPipelineView = this,
                Texture = _renderTarget?.GetParentTexture(),
                Surface = _renderTarget,
                Scene = _sceneProcessor!.GetFrameInfo().Scene,
                Camera = _sceneProcessor.GetFrameInfo().Camera
            };
            var sender = (Object?)eventData.Texture ?? _renderPipeline.GetSubsystem<Renderer>()!;
            sender.SendEvent(ref eventData);
        }

        private void SendEndViewRenderEvent()
        {
            var eventData = new GraphicsEvents.EndViewRenderEvent
            {
                RenderPipelineView = this,
                Texture = _renderTarget?.GetParentTexture(),
                Surface = _renderTarget,
                Scene = _sceneProcessor!.GetFrameInfo().Scene,
                Camera = _sceneProcessor.GetFrameInfo().Camera
            };
            var sender = (Object?)eventData.Texture ?? _renderPipeline.GetSubsystem<Renderer>()!;
            sender.SendEvent(ref eventData);
        }

        private void RenderOutlines(Graphics graphics, MaterialQuality materialQuality)
        {
            if (_viewport is null || _renderPath is null || !HasEnabledOutlinePass())
                return;

            CollectOutlineGroups(_viewport.Scene);
            if (_outlineGroups.Count == 0)
                return;

            var outlineRenderBuffer = EnsureOutlineBuffer();
            _sharedState.RenderBuffers[OutlineRenderPass.ColorBufferId] = outlineRenderBuffer;

            _renderBufferManager.SetRenderTargets(graphics, outlineRenderBuffer);
            graphics.ClearCurrentRenderTargets(Color.Transparent, clearColorTarget: true, clearDepth: false, clearStencil: false);
            graphics.RenderOutlineGroups(_viewport, materialQuality, _outlineGroups, _frameInfo.ViewportRect);
            _renderBufferManager.SetOutputRenderTargets(graphics);
        }

        private bool HasEnabledOutlinePass()
        {
            if (_renderPath is null)
                return false;

            var passes = _renderPath.GetPasses();
            for (var passIndex = 0; passIndex < passes.Count; ++passIndex)
            {
                var pass = passes[passIndex];
                if (pass is OutlineRenderPass && pass.IsEnabledEffectively())
                    return true;
            }

            return false;
        }

        private void CollectOutlineGroups(Scene scene)
        {
            scene.GetComponents(_outlineGroups);

            var drawDebugGeometry = _viewport!.Camera.GetDrawDebugGeometry();
            for (var i = _outlineGroups.Count - 1; i >= 0; --i)
            {
                var outlineGroup = _outlineGroups[i];
                if (!outlineGroup.IsAttached || !outlineGroup.IsEnabledEffective)
                {
                    _outlineGroups.RemoveAt(i);
                    continue;
                }

                if (outlineGroup.IsDebug() && !drawDebugGeometry)
                {
                    _outlineGroups.RemoveAt(i);
                    continue;
                }

                if (!outlineGroup.HasDrawables())
                    _outlineGroups.RemoveAt(i);
            }

            _outlineGroups.Sort(static (lhs, rhs) => lhs.GetRenderOrder().CompareTo(rhs.GetRenderOrder()));
        }

        private TextureRenderBuffer EnsureOutlineBuffer()
        {
            if (_outlineBuffer?.GetTexture() is Texture texture
                && texture.Width == _frameInfo.ViewportRect.Width
                && texture.Height == _frameInfo.ViewportRect.Height)
            {
                _outlineBuffer.SetViewportRect(_frameInfo.ViewportRect);
                return _outlineBuffer;
            }

            _outlineBuffer = _renderBufferManager.CreateColorBuffer(new RenderBufferParams(TextureFormat.RGBA8)) as TextureRenderBuffer
                ?? throw new InvalidOperationException("Outline render buffer must be backed by a TextureRenderBuffer.");
            _outlineBuffer.SetViewportRect(_frameInfo.ViewportRect);
            return _outlineBuffer;
        }

    }
}
