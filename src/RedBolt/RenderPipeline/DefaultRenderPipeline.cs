namespace RedBolt;

using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;

/// <summary>Default implementation of render pipeline instance.</summary>
public class DefaultRenderPipelineView : RenderPipelineView
{
    private static readonly FrameInfo DefaultFrameInfo;

    private RenderPath? _originalRenderPath;
    private RenderPath? _renderPath;
    private bool _parametersDirty;

    private RenderPipelineSettings _settings = new();
    private uint _settingsPipelineStateHash;
    private bool _settingsDirty;

    private TextureFormat _albedoFormat = TextureFormat.TexFormatRgba8Unorm;
    private TextureFormat _normalFormat = TextureFormat.TexFormatRgba8Unorm;
    private TextureFormat _specularFormat = TextureFormat.TexFormatRgba8Unorm;

    /// <summary>Previous pipeline state hash.</summary>
    private uint _oldPipelineStateHash;

    private CommonFrameInfo _frameInfo = new();
    private RenderOutputFlag _renderOutputFlags;

    private RenderPipelineStats _stats;
    private readonly RenderPipelineDebugger _debugger = new();
    private readonly SharedRenderPassState _state = new();

    private RenderBufferManager? _renderBufferManager;
    private ShadowMapAllocator? _shadowMapAllocator;
    private InstancingBuffer? _instancingBuffer;
    private SceneProcessor? _sceneProcessor;

    private UnorderedScenePass? _depthPrePass;
    private UnorderedScenePass? _opaquePass;
    private UnorderedScenePass? _postOpaquePass;
    private UnorderedScenePass? _deferredDecalPass;
    private BackToFrontScenePass? _alphaPass;
    private BackToFrontScenePass? _postAlphaPass;

    private RenderBuffer? _outlineBuffer;
    private OutlineScenePass? _outlineScenePass;

    private struct DeferredLightingData
    {
        public RenderBuffer? AlbedoBuffer { get; set; }
        public RenderBuffer? SpecularBuffer { get; set; }
        public RenderBuffer? NormalBuffer { get; set; }
    }

    private DeferredLightingData? _deferred;

    private readonly RenderBuffer[] _gBuffer = new RenderBuffer[4];
    private readonly ShaderResourceDesc[] _deferredDecalResources = new ShaderResourceDesc[1];
    private readonly ShaderResourceDesc[] _geometryBuffer = new ShaderResourceDesc[4];
    private readonly ShaderParameterDesc[] _deferredCameraParameters = new ShaderParameterDesc[2];
    private readonly ShaderParameterDesc[] _cameraParameters = new ShaderParameterDesc[2];
    private readonly ShaderResourceDesc[] _depthAndColorTextures = new ShaderResourceDesc[2];

    public DefaultRenderPipelineView(RenderPipeline renderPipeline)
        : base(renderPipeline)
    {
        SetSettings(renderPipeline.Settings);
        SetRenderPath(renderPipeline.GetRenderPath());
        MarkParametersDirty();

        renderPipeline.SettingsChanged += OnSettingsChanged;
        renderPipeline.RenderPathChanged += OnRenderPathChanged;
        renderPipeline.ParametersChanged += OnParametersChanged;
    }

    public override void Dispose()
    {
        var renderPipeline = GetRenderPipeline();
        renderPipeline.ParametersChanged -= OnParametersChanged;
        renderPipeline.RenderPathChanged -= OnRenderPathChanged;
        renderPipeline.SettingsChanged -= OnSettingsChanged;

        DisposeShaderParameters(_cameraParameters);
        DisposeShaderParameters(_deferredCameraParameters);
        Array.Clear(_depthAndColorTextures);
        Array.Clear(_geometryBuffer);
        Array.Clear(_deferredDecalResources);
        Array.Clear(_gBuffer);

        DisposeDeferred();
        _outlineScenePass?.Dispose();
        _outlineScenePass = null;
        _outlineBuffer?.Dispose();
        _outlineBuffer = null;
        _postAlphaPass?.Dispose();
        _postAlphaPass = null;
        _alphaPass?.Dispose();
        _alphaPass = null;
        _deferredDecalPass?.Dispose();
        _deferredDecalPass = null;
        _postOpaquePass?.Dispose();
        _postOpaquePass = null;
        _opaquePass?.Dispose();
        _opaquePass = null;
        _depthPrePass?.Dispose();
        _depthPrePass = null;
        _sceneProcessor?.Dispose();
        _sceneProcessor = null;
        _instancingBuffer?.Dispose();
        _instancingBuffer = null;
        _shadowMapAllocator?.Dispose();
        _shadowMapAllocator = null;
        _renderBufferManager?.Dispose();
        _renderBufferManager = null;
        _state.Dispose();
        _renderPath?.Dispose();
        _renderPath = null;
        _originalRenderPath = null;
        base.Dispose();
    }

    public ref readonly RenderPipelineSettings Settings => ref _settings;

    public void SetSettings(in RenderPipelineSettings settings)
    {
        _settings = settings;
        _settings.Validate();
        _settings.AdjustToSupported(Context.Instance);
        _settings.PropagateImpliedSettings();
        _settingsDirty = true;
        _settingsPipelineStateHash = _settings.CalculatePipelineStateHash();
    }

    public void SetRenderPath(RenderPath? renderPath)
    {
        _originalRenderPath = renderPath;
        _renderPath?.Dispose();
        _renderPath = null;
    }

    public void MarkParametersDirty() => _parametersDirty = true;

    /// <summary>
    /// Implement <see cref="IRenderPipelineInterface"/>.
    /// </summary>
    public override RenderPipelineDebugger GetDebugger() => _debugger;

    /// <summary>Implement RenderPipelineView.</summary>
    public override bool Define(RenderSurface? renderTarget, Viewport viewport, Graphics graphics)
    {
        if (viewport.GetScene() is null)
            return false;

        // Ensure that render path is ready
        if (_renderPath is null && _originalRenderPath is not null)
        {
            _renderPath = _originalRenderPath.Clone();
            _renderPath.InitializeView(this);
            _settingsDirty = true;
        }

        // Lazy initialize heavy objects
        if (_sceneProcessor is null)
        {
            _state.RenderPipelineInterface = this;
            _state.RenderBufferManager = new RenderBufferManager(this);

            _renderBufferManager = _state.RenderBufferManager;
            _shadowMapAllocator = new ShadowMapAllocator();
            _instancingBuffer = new InstancingBuffer();
            _sceneProcessor = new SceneProcessor(this, "shadow", _shadowMapAllocator, _instancingBuffer);

            _postOpaquePass = _sceneProcessor.CreatePass<UnorderedScenePass>(
                DrawableProcessorPassFlag.None, "postopaque");
            _deferredDecalPass = _sceneProcessor.CreatePass<UnorderedScenePass>(
                DrawableProcessorPassFlag.HasAmbientLighting | DrawableProcessorPassFlag.NeedReadableDepth,
                "deferred_decal", string.Empty, string.Empty, string.Empty);
            _alphaPass = _sceneProcessor.CreatePass<BackToFrontScenePass>(
                DrawableProcessorPassFlag.HasAmbientLighting | DrawableProcessorPassFlag.NeedReadableDepth
                    | DrawableProcessorPassFlag.RefractionPass | DrawableProcessorPassFlag.ReadOnlyDepth,
                string.Empty, "alpha", "alpha", "litalpha");
            _postAlphaPass = _sceneProcessor.CreatePass<BackToFrontScenePass>(
                DrawableProcessorPassFlag.ReadOnlyDepth, "postalpha");

            var outlineParams = new RenderBufferParams(
                TextureFormat.TexFormatRgba8Unorm, 1, RenderBufferFlag.BilinearFiltering);
            _outlineBuffer = _renderBufferManager.CreateColorBuffer(outlineParams);
            _outlineBuffer.SetEnabled(false);

            _state.RenderBuffers[OutlineRenderPass.ColorBufferId] = _outlineBuffer;
        }

        _frameInfo.Viewport = viewport;
        _frameInfo.RenderTarget = renderTarget;
        _frameInfo.ViewportRect = viewport.GetEffectiveRect(renderTarget);
        _frameInfo.ViewportSize = _frameInfo.ViewportRect.Size;

        if (!_sceneProcessor.Define(_frameInfo))
            return false;

        _sceneProcessor.SetRenderCamera(viewport.GetCamera());
        _state.RenderCamera = viewport.GetCamera() is { } camera ? new WeakReference<Camera>(camera) : null;

        if (_settingsDirty)
        {
            _settingsDirty = false;
            _parametersDirty = true;
            ApplySettings();
            UpdateRenderOutputFlags();
        }

        if (_parametersDirty && _renderPath is not null)
        {
            _parametersDirty = false;
            _renderPath.UpdateParameters(
                _settings, GetRenderPipeline().GetRenderPasses(), GetRenderPipeline().GetRenderPathParameters());
            UpdateRenderOutputFlags();
        }

        _renderBufferManager!.OnViewportDefined(_frameInfo.RenderTarget, _frameInfo.ViewportRect);
        _linearColorSpace = _renderBufferManager.IsLinearColorSpace;

        var outputMultiSample = _renderBufferManager.OutputMultiSample;
        var outputColorFormat = _renderBufferManager.OutputColorFormat;
        var outputDepthFormat = _renderBufferManager.OutputDepthStencilFormat;

        var standardOutputDesc = new PipelineStateOutputDesc
        {
            DepthStencilFormat = outputDepthFormat,
            MultiSample = outputMultiSample
        };
        standardOutputDesc.NumRenderTargets = 1;
        standardOutputDesc.RenderTargetFormats[0] = outputColorFormat;

        var deferredOutputDesc = new PipelineStateOutputDesc
        {
            DepthStencilFormat = outputDepthFormat
        };
        deferredOutputDesc.NumRenderTargets = 4;
        deferredOutputDesc.RenderTargetFormats[0] = outputColorFormat;
        deferredOutputDesc.RenderTargetFormats[1] = _albedoFormat;
        deferredOutputDesc.RenderTargetFormats[2] = _specularFormat;
        deferredOutputDesc.RenderTargetFormats[3] = _normalFormat;

        _opaquePass!.SetDeferredOutputDesc(deferredOutputDesc);
        _deferredDecalPass!.SetDeferredOutputDesc(deferredOutputDesc);

        _opaquePass.SetForwardOutputDesc(standardOutputDesc);
        _depthPrePass?.SetForwardOutputDesc(standardOutputDesc);
        _postOpaquePass!.SetForwardOutputDesc(standardOutputDesc);
        _alphaPass!.SetForwardOutputDesc(standardOutputDesc);
        _postAlphaPass!.SetForwardOutputDesc(standardOutputDesc);

        var batchCompositor = _sceneProcessor.GetBatchCompositor();
        batchCompositor.SetLightVolumesOutputDesc(standardOutputDesc);
        batchCompositor.SetShadowOutputDesc(_shadowMapAllocator!.ShadowOutputDesc);

        return true;
    }

    public override void Update(Graphics graphics)
    {
        var engineTime = GetRenderPipeline().GetSubsystem<Time>()!;
        _frameInfo.FrameNumber = engineTime.FrameNumber;
        _frameInfo.TimeStep = engineTime.TimeStep;

        // Begin debug snapshot
        var input = GetRenderPipeline().GetSubsystem<Input>()!;
        var qualifiers = input.GetQualifiers();
        var takeSnapshot = (qualifiers & (Qualifier.Ctrl | Qualifier.Shift)) == (Qualifier.Ctrl | Qualifier.Shift)
            && input.GetKeyPress(Key.F12);
        if (takeSnapshot)
            _debugger.BeginSnapshot();

        // Begin update. Should happen before pipeline state hash check.
        _shadowMapAllocator!.ResetAllShadowMaps();
        RaiseUpdateBegin(_frameInfo);
        SendBeginViewUpdateEvent();

        // Invalidate pipeline states if necessary
        var pipelineStateHash = RecalculatePipelineStateHash();
        if (_oldPipelineStateHash != pipelineStateHash)
        {
            _oldPipelineStateHash = pipelineStateHash;
            RaisePipelineStatesInvalidated();
        }

        ref readonly var fullFrameInfo = ref _sceneProcessor!.GetFrameInfo();
        var drawDebugGeometry = fullFrameInfo.Camera!.GetDrawDebugGeometry();
        _outlineScenePass!.SetOutlineGroups(fullFrameInfo.Scene!, drawDebugGeometry);

        _sceneProcessor.Update();

        _outlineBuffer!.SetEnabled(_outlineScenePass.IsEnabled && _outlineScenePass.HasBatches);

        _renderPath?.Update(_state);

        UpdateFrameParameters();

        SendEndViewUpdateEvent();
        RaiseUpdateEnd(_frameInfo);
    }

    public override void Render(Graphics graphics, MaterialQuality materialQuality)
    {
        var caps = GetRenderPipeline().GetSubsystem<RenderDevice>()!.GetCaps();
        var canReadDepth = _settings.RenderBufferManager.ReadableDepth && caps.ReadOnlyDepth;

        ref readonly var fullFrameInfo = ref _sceneProcessor!.GetFrameInfo();
        var hasRefraction = _alphaPass!.HasRefractionBatches();
        var frameSettings = new RenderBufferManagerFrameSettings
        {
            SupportColorReadWrite = (_renderOutputFlags & RenderOutputFlag.NeedColorOutputReadAndWrite) != 0
        };
        if (hasRefraction)
            frameSettings.SupportColorReadWrite = true;
        _renderBufferManager!.SetFrameSettings(frameSettings);

        RaiseRenderBegin(_frameInfo);
        SendBeginViewRenderEvent();
        SendViewBuffersReadyEvent();

        _sceneProcessor.PrepareDrawablesBeforeRendering();
        _sceneProcessor.PrepareInstancingBuffer();
        _sceneProcessor.RenderShadowMaps();

        var camera = fullFrameInfo.Camera!;
        var fogColorInGammaSpace = camera.GetEffectiveFogColor();
        var effectiveFogColor = _linearColorSpace ? fogColorInGammaSpace.GammaToLinear() : fogColorInGammaSpace;
        var frameParameters = CollectionsMarshal.AsSpan(_frameParameters);

        try
        {
            if (_settings.SceneProcessor.IsDeferredLighting())
            {
                var deferred = _deferred!.Value;
                _renderBufferManager.ClearColor(deferred.AlbedoBuffer!, Color.TransparentBlack);
                _renderBufferManager.ClearColor(deferred.SpecularBuffer!, Color.TransparentBlack);
                _renderBufferManager.ClearColor(deferred.NormalBuffer!, Color.TransparentBlack);
                _renderBufferManager.ClearOutput(effectiveFogColor, 1.0f, 0);

                if (_depthPrePass is not null)
                {
                    _sceneProcessor.RenderSceneBatches(
                        "DepthPrePass", camera, _depthPrePass.GetBaseBatches(), default, default, frameParameters);
                }

                _gBuffer[0] = _renderBufferManager.ColorOutput!;
                _gBuffer[1] = deferred.AlbedoBuffer!;
                _gBuffer[2] = deferred.SpecularBuffer!;
                _gBuffer[3] = deferred.NormalBuffer!;
                _renderBufferManager.SetRenderTargets(_renderBufferManager.DepthStencilOutput, _gBuffer);
                _sceneProcessor.RenderSceneBatches(
                    "GeometryBuffer", camera, _opaquePass!.GetDeferredBatches(), default, default, frameParameters);

                if (canReadDepth && _deferredDecalPass!.GetDeferredBatches().Batches!.Count != 0)
                {
                    _renderBufferManager.SetRenderTargets(_renderBufferManager.DepthStencilOutput, _gBuffer, true);
                    _deferredDecalResources[0] = new ShaderResourceDesc
                    {
                        Name = ShaderResources.DepthBuffer,
                        Texture = _renderBufferManager.DepthStencilTexture
                    };
                    _sceneProcessor.RenderSceneBatches("DeferredDecals", camera,
                        _deferredDecalPass.GetDeferredBatches(), _deferredDecalResources, default, frameParameters);
                }

                _geometryBuffer[0] = new ShaderResourceDesc
                {
                    Name = ShaderResources.Albedo,
                    Texture = deferred.AlbedoBuffer!.GetTexture()
                };
                _geometryBuffer[1] = new ShaderResourceDesc
                {
                    Name = ShaderResources.Properties,
                    Texture = deferred.SpecularBuffer!.GetTexture()
                };
                _geometryBuffer[2] = new ShaderResourceDesc
                {
                    Name = ShaderResources.Normal,
                    Texture = deferred.NormalBuffer!.GetTexture()
                };
                _geometryBuffer[3] = new ShaderResourceDesc
                {
                    Name = ShaderResources.DepthBuffer,
                    Texture = _renderBufferManager.DepthStencilTexture
                };
                SetCameraParameters(_deferredCameraParameters);

                _renderBufferManager.SetOutputRenderTargets(true);
                _sceneProcessor.RenderLightVolumeBatches(
                    "LightVolumes", camera, _geometryBuffer, _deferredCameraParameters, frameParameters);
                _renderBufferManager.SetOutputRenderTargets();
            }
            else
            {
                _renderBufferManager.ClearOutput(effectiveFogColor, 1.0f, 0);
                _renderBufferManager.SetOutputRenderTargets();

                if (_depthPrePass is not null)
                {
                    _sceneProcessor.RenderSceneBatches(
                        "DepthPrePass", camera, _depthPrePass.GetBaseBatches(), default, frameParameters);
                }
            }

            SetCameraParameters(_cameraParameters);

            _sceneProcessor.RenderSceneBatches(
                "OpaqueBase", camera, _opaquePass!.GetBaseBatches(), default, _cameraParameters, frameParameters);
            _sceneProcessor.RenderSceneBatches(
                "OpaqueLight", camera, _opaquePass.GetLightBatches(), default, _cameraParameters, frameParameters);
            _sceneProcessor.RenderSceneBatches(
                "PostOpaque", camera, _postOpaquePass!.GetBaseBatches(), default, _cameraParameters, frameParameters);

            if (hasRefraction)
                _renderBufferManager.SwapColorBuffers(true);

            _depthAndColorTextures[0] = new ShaderResourceDesc
            {
                Name = ShaderResources.DepthBuffer,
                Texture = canReadDepth ? _renderBufferManager.DepthStencilTexture : null
            };
            _depthAndColorTextures[1] = new ShaderResourceDesc
            {
                Name = ShaderResources.Emission,
                Texture = _renderBufferManager.SecondaryColorTexture
            };

            if (canReadDepth)
                _renderBufferManager.SetOutputRenderTargets(true);

            _sceneProcessor.RenderSceneBatches(
                "Alpha", camera, _alphaPass.GetBatches(), _depthAndColorTextures, _cameraParameters, frameParameters);
            _sceneProcessor.RenderSceneBatches(
                "PostAlpha", camera, _postAlphaPass!.GetBatches(), default, _cameraParameters, frameParameters);

            if (_outlineBuffer!.IsEnabled)
            {
                // TODO: Do we want it dynamic?
                const int outlinePadding = 2;

                var batches = _outlineScenePass!.Batches;
                batches.ScissorRect = _outlineBuffer.GetViewportRect();
                if (batches.ScissorRect.Width > outlinePadding * 2
                    && batches.ScissorRect.Height > outlinePadding * 2)
                {
                    var rect = batches.ScissorRect;
                    batches.ScissorRect = new IntRect(
                        rect.Left + outlinePadding,
                        rect.Top + outlinePadding,
                        rect.Right - outlinePadding,
                        rect.Bottom - outlinePadding);
                }

                _renderBufferManager.SetRenderTargets(null, _outlineBuffer);
                _renderBufferManager.ClearColor(_outlineBuffer, Color.TransparentBlack);
                _sceneProcessor.RenderSceneBatches(
                    "Outline", camera, batches, default, _cameraParameters, frameParameters);
            }

            _renderPath?.Render(_state);

            var drawDebugGeometry = _settings.DrawDebugGeometry && camera.GetDrawDebugGeometry();
            var debug = fullFrameInfo.Scene!.GetComponent<DebugRenderer>();
            if (drawDebugGeometry && debug is not null && debug.IsEnabledEffective && debug.HasContent())
            {
                _renderBufferManager.SetOutputRenderTargets();
                debug.SetView(camera);
                debug.Render();
            }

            RaiseRenderEnd(_frameInfo);
            SendEndViewRenderEvent();

            // Update statistics
            _stats = default;
            RaiseCollectStatistics(ref _stats);

            // End debug snapshot
            if (_debugger.IsSnapshotInProgress())
            {
                _debugger.EndSnapshot();
                Log.GetLogger().LogInformation(
                    "RenderPipeline snapshot:\n\n{Snapshot}\n", _debugger.GetSnapshot().ToString());
            }
        }
        finally
        {
            Array.Clear(_depthAndColorTextures);
            DisposeShaderParameters(_cameraParameters);
            DisposeShaderParameters(_deferredCameraParameters);
            Array.Clear(_geometryBuffer);
            Array.Clear(_deferredDecalResources);
            Array.Clear(_gBuffer);
        }
    }

    public override ref readonly FrameInfo GetFrameInfo()
    {
        if (_sceneProcessor is not null)
            return ref _sceneProcessor.GetFrameInfo();
        return ref DefaultFrameInfo;
    }

    public override ref readonly RenderPipelineStats GetStats() => ref _stats;

    public override void DrawDebugGeometries(bool depthTest)
    {
        ref readonly var fullFrameInfo = ref _sceneProcessor!.GetFrameInfo();
        var debug = fullFrameInfo.Scene!.GetComponent<DebugRenderer>();

        var geometries = _sceneProcessor.GetDrawableProcessor().Geometries;
        foreach (var geometry in geometries)
            geometry.DrawDebugGeometry(debug, depthTest);
    }

    public override void DrawDebugLights(bool depthTest)
    {
        ref readonly var fullFrameInfo = ref _sceneProcessor!.GetFrameInfo();
        var debug = fullFrameInfo.Scene!.GetComponent<DebugRenderer>();

        var lights = _sceneProcessor.GetDrawableProcessor().Lights;
        foreach (var light in lights)
            light!.DrawDebugGeometry(debug, depthTest);
    }

    protected uint RecalculatePipelineStateHash()
    {
        var hash = _settingsPipelineStateHash;
        CombineHash(ref hash, _sceneProcessor!.GetCameraProcessor().PipelineStateHash);
        return hash;
    }

    protected void SendBeginViewUpdateEvent()
    {
        var eventData = CreateBeginViewUpdateEvent();
        var sender = (Object?)eventData.Texture ?? _renderer;
        sender.SendEvent(ref eventData);
    }

    protected void SendEndViewUpdateEvent()
    {
        var eventData = new GraphicsEvents.EndViewUpdateEvent
        {
            RenderPipelineView = this,
            Texture = _frameInfo.RenderTarget?.GetParentTexture(),
            Surface = _frameInfo.RenderTarget,
            Scene = _sceneProcessor!.GetFrameInfo().Scene,
            Camera = _sceneProcessor.GetFrameInfo().Camera
        };
        var sender = (Object?)eventData.Texture ?? _renderer;
        sender.SendEvent(ref eventData);
    }

    protected void SendBeginViewRenderEvent()
    {
        var eventData = new GraphicsEvents.BeginViewRenderEvent
        {
            RenderPipelineView = this,
            Texture = _frameInfo.RenderTarget?.GetParentTexture(),
            Surface = _frameInfo.RenderTarget,
            Scene = _sceneProcessor!.GetFrameInfo().Scene,
            Camera = _sceneProcessor.GetFrameInfo().Camera
        };
        var sender = (Object?)eventData.Texture ?? _renderer;
        sender.SendEvent(ref eventData);
    }

    protected void SendViewBuffersReadyEvent()
    {
        var eventData = new GraphicsEvents.ViewBuffersReadyEvent
        {
            RenderPipelineView = this,
            Texture = _frameInfo.RenderTarget?.GetParentTexture(),
            Surface = _frameInfo.RenderTarget,
            Scene = _sceneProcessor!.GetFrameInfo().Scene,
            Camera = _sceneProcessor.GetFrameInfo().Camera
        };
        var sender = (Object?)eventData.Texture ?? _renderer;
        sender.SendEvent(ref eventData);
    }

    protected void SendEndViewRenderEvent()
    {
        var eventData = new GraphicsEvents.EndViewRenderEvent
        {
            RenderPipelineView = this,
            Texture = _frameInfo.RenderTarget?.GetParentTexture(),
            Surface = _frameInfo.RenderTarget,
            Scene = _sceneProcessor!.GetFrameInfo().Scene,
            Camera = _sceneProcessor.GetFrameInfo().Camera
        };
        var sender = (Object?)eventData.Texture ?? _renderer;
        sender.SendEvent(ref eventData);
    }

    private GraphicsEvents.BeginViewUpdateEvent CreateBeginViewUpdateEvent() => new()
    {
        RenderPipelineView = this,
        Texture = _frameInfo.RenderTarget?.GetParentTexture(),
        Surface = _frameInfo.RenderTarget,
        Scene = _sceneProcessor!.GetFrameInfo().Scene,
        Camera = _sceneProcessor.GetFrameInfo().Camera
    };

    protected void ApplySettings()
    {
        var oldDepthPrePass = _depthPrePass;
        var oldOpaquePass = _opaquePass;
        var oldOutlineScenePass = _outlineScenePass;
        var oldDeferred = _deferred;

        _sceneProcessor!.SetSettings(_settings);
        _instancingBuffer!.SetSettings(_settings.InstancingBuffer);
        _shadowMapAllocator!.SetSettings(_settings.ShadowMapAllocator);

        if (_settings.SceneProcessor.DepthPrePass && _depthPrePass is null)
        {
            _depthPrePass = _sceneProcessor.CreatePass<UnorderedScenePass>(
                DrawableProcessorPassFlag.DepthOnlyPass, "depth");
        }
        else
            _depthPrePass = null;

        if (_opaquePass is null || _settings.SceneProcessor.IsDeferredLighting() != _deferred.HasValue)
        {
            if (_settings.SceneProcessor.IsDeferredLighting())
            {
                _opaquePass = _sceneProcessor.CreatePass<UnorderedScenePass>(
                    DrawableProcessorPassFlag.HasAmbientLighting
                        | DrawableProcessorPassFlag.DeferredLightMaskToStencil,
                    "deferred", "base", "litbase", "light");

                _deferred = new DeferredLightingData
                {
                    AlbedoBuffer = _renderBufferManager!.CreateColorBuffer(
                        new RenderBufferParams(TextureFormat.TexFormatRgba8Unorm)),
                    SpecularBuffer = _renderBufferManager.CreateColorBuffer(
                        new RenderBufferParams(TextureFormat.TexFormatRgba8Unorm)),
                    NormalBuffer = _renderBufferManager.CreateColorBuffer(
                        new RenderBufferParams(TextureFormat.TexFormatRgba8Unorm))
                };

                var deferred = _deferred.Value;
                _state.RenderBuffers[SharedRenderPassState.AlbedoBufferId] = deferred.AlbedoBuffer;
                _state.RenderBuffers[SharedRenderPassState.SpecularBufferId] = deferred.SpecularBuffer;
                _state.RenderBuffers[SharedRenderPassState.NormalBufferId] = deferred.NormalBuffer;
            }
            else
            {
                _opaquePass = _sceneProcessor.CreatePass<UnorderedScenePass>(
                    DrawableProcessorPassFlag.HasAmbientLighting,
                    string.Empty, "base", "litbase", "light");

                _deferred = null;
                _state.RenderBuffers[SharedRenderPassState.AlbedoBufferId] = null;
                _state.RenderBuffers[SharedRenderPassState.SpecularBufferId] = null;
                _state.RenderBuffers[SharedRenderPassState.NormalBufferId] = null;
            }
        }

        _outlineScenePass = _sceneProcessor.CreatePass<OutlineScenePass>(
            new string[] { "deferred", "deferred_decal", "base", "alpha" },
            DrawableProcessorPassFlag.None);

        _sceneProcessor.SetPasses(
        [
            _depthPrePass, _opaquePass, _deferredDecalPass, _postOpaquePass,
            _alphaPass, _postAlphaPass, _outlineScenePass
        ]);

        if (!ReferenceEquals(oldOutlineScenePass, _outlineScenePass))
            oldOutlineScenePass?.Dispose();
        if (!ReferenceEquals(oldOpaquePass, _opaquePass))
            oldOpaquePass?.Dispose();
        if (!ReferenceEquals(oldDepthPrePass, _depthPrePass))
            oldDepthPrePass?.Dispose();
        DisposeDeferred(oldDeferred, _deferred);
    }

    protected void UpdateRenderOutputFlags()
    {
        _renderOutputFlags = default;
        if (_renderPath is not null)
        {
            var aggregatedTraits = _renderPath.AggregatedPassTraits;
            if (aggregatedTraits.NeedReadWriteColorBuffer)
                _renderOutputFlags |= RenderOutputFlag.NeedColorOutputReadAndWrite;
            if (aggregatedTraits.NeedBilinearColorSampler)
                _renderOutputFlags |= RenderOutputFlag.NeedColorOutputBilinear;
        }

        _settings.AdjustForRenderPath(_renderOutputFlags);
        _renderBufferManager!.SetSettings(_settings.RenderBufferManager);
    }

    private void SetCameraParameters(ShaderParameterDesc[] parameters)
    {
        parameters[0] = new ShaderParameterDesc
        {
            Name = ShaderConsts.Camera_GBufferOffsets,
            Value = new Variant(_renderBufferManager!.DefaultClipToUVSpaceOffsetAndScale)
        };
        parameters[1] = new ShaderParameterDesc
        {
            Name = ShaderConsts.Camera_GBufferInvSize,
            Value = new Variant(_renderBufferManager.InvOutputSize)
        };
    }

    private void DisposeDeferred()
    {
        DisposeDeferred(_deferred, null);
        _deferred = null;
    }

    private static void DisposeDeferred(DeferredLightingData? oldValue, DeferredLightingData? newValue)
    {
        if (!oldValue.HasValue)
            return;

        var old = oldValue.Value;
        var current = newValue.GetValueOrDefault();
        if (!ReferenceEquals(old.NormalBuffer, current.NormalBuffer))
            old.NormalBuffer?.Dispose();
        if (!ReferenceEquals(old.SpecularBuffer, current.SpecularBuffer))
            old.SpecularBuffer?.Dispose();
        if (!ReferenceEquals(old.AlbedoBuffer, current.AlbedoBuffer))
            old.AlbedoBuffer?.Dispose();
    }

    private static void DisposeShaderParameters(ShaderParameterDesc[] parameters)
    {
        for (var i = parameters.Length - 1; i >= 0; --i)
        {
            parameters[i].Dispose();
            parameters[i] = default;
        }
    }

    private static void CombineHash(ref uint result, uint hash)
    {
        result ^= hash + 0x9e3779b9u + (result << 6) + (result >> 2);
    }

    private void OnSettingsChanged(RenderPipeline pipeline, RenderPipelineSettings settings) => SetSettings(in settings);

    private void OnRenderPathChanged(RenderPipeline pipeline, RenderPath? renderPath) => SetRenderPath(renderPath);

    private void OnParametersChanged(RenderPipeline pipeline) => MarkParametersDirty();
}
