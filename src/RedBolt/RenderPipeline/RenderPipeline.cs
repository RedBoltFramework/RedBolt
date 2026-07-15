namespace RedBolt;

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

    public RenderPipelineSettings Settings => _settings;

    public RenderPath? GetRenderPath() => _renderPath;

    public ResourceRef GetRenderPathAttr() => Resource.GetResourceRef(_renderPath, new StringHash(nameof(RenderPath)));

    public void SetRenderPathAttr(ResourceRef value)
    {
        if (GetSubsystem<ResourceCache>() is { } cache)
            SetRenderPath(cache.GetResource<RenderPath>(value.Name));
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

    // C++: settings_.Validate(); settings_.AdjustToSupported(context_);
    // OnSettingsChanged(this, settings_);
    // No PropagateImpliedSettings() call, no revision increment.
    private void MarkSettingsDirty()
    {
        _settings.Validate();
        _settings.PropagateImpliedSettings();
        _settings.AdjustToSupported(GetSubsystem<Graphics>());
        Touch();
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
    // TODO: port - C++ code:
    private sealed class DefaultRenderPipelineView : RenderPipelineView
    {
        private readonly RenderPipeline _renderPipeline;
        private readonly RenderBufferManager _renderBufferManager = new();
        private readonly SharedRenderPassState _sharedState = new();
        private readonly Dictionary<string, object?> _effectiveRenderPathParameters = new(StringComparer.Ordinal);
        private readonly RenderPipelineStats _stats = new();
        private readonly List<OutlineGroup> _outlineGroups = [];
        private TextureRenderBuffer? _outlineBuffer;
        private TextureRenderBuffer? _normalBuffer;
        private CommonFrameInfo _frameInfo = new();
        private RenderPath? _renderPath;
        private Viewport? _viewport;
        private uint _syncedRevision;

        public DefaultRenderPipelineView(RenderPipeline renderPipeline)
            : base(renderPipeline)
        {
            this._renderPipeline = renderPipeline ?? throw new ArgumentNullException(nameof(renderPipeline));
        }

        public override bool IsLinearColorSpace => _renderBufferManager.IsLinearColorSpace();

        public override bool Define(Viewport viewport, Graphics graphics)
        {
            ArgumentNullException.ThrowIfNull(viewport);
            ArgumentNullException.ThrowIfNull(graphics);

            if (!_renderPipeline.IsAttached || !ReferenceEquals(_renderPipeline.Scene, viewport.Scene))
                return false;

            this._viewport = viewport;
            SyncFromPipeline();

            var viewportRect = viewport.Rect != IntRect.Zero
                ? viewport.Rect
                : new IntRect(0, 0, graphics.Width, graphics.Height);

            var engineTime = _renderPipeline.GetSubsystem<Time>();
            _frameInfo.FrameNumber = checked((uint)(engineTime?.FrameNumber ?? 0U));
            _frameInfo.TimeStep = engineTime?.TimeStep ?? 0.0f;
            _frameInfo.Viewport = viewport;
            _frameInfo.ViewportRect = viewportRect;
            _frameInfo.ViewportSize = viewportRect.Size;
            _frameInfo.RenderTarget = graphics.GetSwapChainOutputTarget();
            _frameInfo.Cameras[0] = viewport.Camera;
            _frameInfo.Cameras[1] = viewport.RightEye;

            _renderBufferManager.SetSettings(_renderPipeline.Settings.RenderBufferManager);
            _renderBufferManager.SetFrameSettings(new RenderBufferManagerFrameSettings());
            return true;
        }

        public override void Update(Graphics graphics)
        {
            ArgumentNullException.ThrowIfNull(graphics);
            if (_viewport is null)
                return;

            SyncFromPipeline();
            var cameraPosition = _viewport.Camera.IsAttached
                ? _viewport.Camera.Node.WorldPosition
                : Vector3f.Zero;
            _viewport.Camera.SetZone(graphics.FindZone(_viewport.Scene, cameraPosition));
            RaiseUpdateBegin(_frameInfo);
            SendBeginViewUpdateEvent();

            _sharedState.RenderPipelineInterface = this;
            _sharedState.RenderCamera = _viewport.Camera;
            _sharedState.Graphics = graphics;
            _sharedState.RenderBuffers.Clear();
            _renderBufferManager.PopulateSharedState(_sharedState);

            if (_renderPath is not null)
            {
                _renderPath.UpdateParameters(_renderPipeline.Settings, _renderPipeline.GetRenderPasses(), _effectiveRenderPathParameters);
                var traits = _renderPath.GetAggregatedPassTraits();
                _renderBufferManager.SetFrameSettings(new RenderBufferManagerFrameSettings
                {
                    SupportColorReadWrite = traits.NeedReadWriteColorBuffer
                });
                _renderBufferManager.OnViewportDefined(_frameInfo.RenderTarget, _frameInfo.ViewportRect);
                _sharedState.RenderBuffers.Clear();
                _renderBufferManager.PopulateSharedState(_sharedState);
                _renderPath.Update(_sharedState);
            }

            UpdateFrameParameters();
            _stats.NumGeometries = (uint)_viewport.Scene.StaticModels.Count;
            SendEndViewUpdateEvent();
            RaiseUpdateEnd(_frameInfo);
        }

        public override void Render(Graphics graphics, MaterialQuality materialQuality, Action? beforePresent = null, string? screenshotPath = null)
        {
            ArgumentNullException.ThrowIfNull(graphics);
            if (_viewport is null)
                return;

            RaiseRenderBegin(_frameInfo);
            SendBeginViewRenderEvent();

            _sharedState.Graphics = graphics;
            Context.Instance.SendEvent<GraphicsEvents.BeginRenderingEvent>();
            _renderBufferManager.OnRenderBegin(_frameInfo, graphics);
            SendViewBuffersReadyEvent();
            var fogColor = _viewport.Camera.GetEffectiveFogColor();
            var effectiveFogColor = IsLinearColorSpace ? fogColor.GammaToLinear() : fogColor;
            _renderBufferManager.ClearOutput(graphics, effectiveFogColor);
            _renderBufferManager.SetOutputRenderTargets(graphics);
            graphics.RenderScene(_viewport, materialQuality, _frameInfo.ViewportRect);
            RenderNormalBuffer(graphics, materialQuality);
            RenderOutlines(graphics, materialQuality);
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
            Context.Instance.SendEvent<GraphicsEvents.EndRenderingEvent>();

            graphics.UnbindRenderTargets();
            RaiseCollectStatistics(_stats);
            SendEndViewRenderEvent();
            RaiseRenderEnd(_frameInfo);
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
                Texture = _frameInfo.RenderTarget?.ColorTexture,
                Surface = null,
                Scene = _frameInfo.Viewport?.Scene,
                Camera = _frameInfo.Cameras[0]
            };
            Context.Instance.SendEvent(ref eventData);
        }

        private void SendEndViewUpdateEvent()
        {
            var eventData = new GraphicsEvents.EndViewUpdateEvent
            {
                RenderPipelineView = this,
                Texture = _frameInfo.RenderTarget?.ColorTexture,
                Surface = null,
                Scene = _frameInfo.Viewport?.Scene,
                Camera = _frameInfo.Cameras[0]
            };
            Context.Instance.SendEvent(ref eventData);
        }

        private void SendBeginViewRenderEvent()
        {
            var eventData = new GraphicsEvents.BeginViewRenderEvent
            {
                RenderPipelineView = this,
                Texture = _frameInfo.RenderTarget?.ColorTexture,
                Surface = null,
                Scene = _frameInfo.Viewport?.Scene,
                Camera = _frameInfo.Cameras[0]
            };
            Context.Instance.SendEvent(ref eventData);
        }

        private void SendViewBuffersReadyEvent()
        {
            var eventData = new GraphicsEvents.ViewBuffersReadyEvent
            {
                RenderPipelineView = this,
                Texture = _frameInfo.RenderTarget?.ColorTexture,
                Surface = null,
                Scene = _frameInfo.Viewport?.Scene,
                Camera = _frameInfo.Cameras[0]
            };
            Context.Instance.SendEvent(ref eventData);
        }

        private void SendEndViewRenderEvent()
        {
            var eventData = new GraphicsEvents.EndViewRenderEvent
            {
                RenderPipelineView = this,
                Texture = _frameInfo.RenderTarget?.ColorTexture,
                Surface = null,
                Scene = _frameInfo.Viewport?.Scene,
                Camera = _frameInfo.Cameras[0]
            };
            Context.Instance.SendEvent(ref eventData);
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

        private void RenderNormalBuffer(Graphics graphics, MaterialQuality materialQuality)
        {
            if (_viewport is null || _renderPath is null || !HasEnabledNormalBufferPass())
                return;

            var normalRenderBuffer = EnsureNormalBuffer();
            _sharedState.RenderBuffers[SharedRenderPassState.NormalBufferId] = normalRenderBuffer;

            _renderBufferManager.SetRenderTargets(
                graphics,
                normalRenderBuffer,
                _renderBufferManager.GetDepthStencilOutput(),
                readOnlyDepth: true);
            graphics.ClearCurrentRenderTargets(Color.Transparent, clearColorTarget: true, clearDepth: false, clearStencil: false);
            graphics.RenderNormalBuffer(_viewport, materialQuality, _frameInfo.ViewportRect);
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

        private bool HasEnabledNormalBufferPass()
        {
            if (_renderPath is null)
                return false;

            var passes = _renderPath.GetPasses();
            for (var passIndex = 0; passIndex < passes.Count; ++passIndex)
            {
                var pass = passes[passIndex];
                if (pass is AmbientOcclusionPass && pass.IsEnabledEffectively())
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

        private TextureRenderBuffer EnsureNormalBuffer()
        {
            if (_normalBuffer?.GetTexture() is Texture texture
                && texture.Width == _frameInfo.ViewportRect.Width
                && texture.Height == _frameInfo.ViewportRect.Height)
            {
                _normalBuffer.SetViewportRect(_frameInfo.ViewportRect);
                return _normalBuffer;
            }

            _normalBuffer = _renderBufferManager.CreateColorBuffer(new RenderBufferParams(TextureFormat.RGBA8)) as TextureRenderBuffer
                ?? throw new InvalidOperationException("Normal render buffer must be backed by a TextureRenderBuffer.");
            _normalBuffer.SetViewportRect(_frameInfo.ViewportRect);
            return _normalBuffer;
        }
    }
}
