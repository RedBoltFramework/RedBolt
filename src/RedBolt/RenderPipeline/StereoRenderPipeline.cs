namespace RedBolt;

/// <summary>Frustum query for stereo frustums.</summary>
public class StereoFrustumOctreeQuery : OctreeQuery
{
    /// <summary>Stereo frustums.</summary>
    protected readonly Frustum[] Frustums = new Frustum[2];

    public StereoFrustumOctreeQuery(
        List<Drawable> result, Frustum[] frustums, DrawableFlags drawableFlags, uint viewMask)
        : base(result, drawableFlags, viewMask)
    {
        Frustums[0] = new Frustum(frustums[0]);
        Frustums[1] = new Frustum(frustums[1]);
    }

    public override Intersection TestOctant(in BoundingBox box, bool inside)
    {
        if (inside)
            return Intersection.Inside;

        var inter = Frustums[0].IsInside(box);
        inter = (Intersection)Math.Max((int)inter, (int)Frustums[1].IsInside(box));
        return inter;
    }

    public override void TestDrawables(IReadOnlyList<Drawable> drawables, bool inside)
    {
        for (var i = 0; i < drawables.Count; ++i)
        {
            var drawable = drawables[i];
            if ((drawable.GetDrawableFlags() & DrawableFlags) != 0 && (drawable.GetViewMask() & ViewMask) != 0)
            {
                var bb = drawable.GetWorldBoundingBox();
                if (inside
                    || Frustums[0].IsInsideFast(bb) != Intersection.Outside
                    || Frustums[1].IsInsideFast(bb) != Intersection.Outside)
                    Result.Add(drawable);
            }
        }
    }
}

/// <summary>Stereo frustum query that only returns occluders.</summary>
public class StereoOccluderOctreeQuery : StereoFrustumOctreeQuery
{
    /// <summary>Construct with frustum and query parameters.</summary>
    public StereoOccluderOctreeQuery(
        List<Drawable> result, Frustum[] frustums, uint viewMask = Drawable.DefaultViewMask)
        : base(result, frustums, DrawableFlags.Geometry, viewMask)
    {
    }

    /// <summary>Intersection test for drawables.</summary>
    public override void TestDrawables(IReadOnlyList<Drawable> drawables, bool inside)
    {
        for (var i = 0; i < drawables.Count; ++i)
        {
            var drawable = drawables[i];
            var flags = drawable.GetDrawableFlags();
            if (flags == DrawableFlags.Geometry && drawable.IsOccluder() && (drawable.GetViewMask() & ViewMask) != 0)
            {
                var bb = drawable.GetWorldBoundingBox();
                if (inside
                    || Frustums[0].IsInsideFast(bb) != Intersection.Outside
                    || Frustums[1].IsInsideFast(bb) != Intersection.Outside)
                    Result.Add(drawable);
            }
        }
    }
}

/// <summary>Stereo frustum query that also tests against occlusion buffers.</summary>
public class StereoOccludedFrustumOctreeQuery : StereoFrustumOctreeQuery
{
    /// <summary>Occlusion buffers.</summary>
    protected readonly OcclusionBuffer?[] Buffers = new OcclusionBuffer?[2];

    /// <summary>Construct with frustum, occlusion buffer and query parameters.</summary>
    public StereoOccludedFrustumOctreeQuery(List<Drawable> result, Frustum[] frustums,
        OcclusionBuffer?[] buffers, DrawableFlags drawableFlags = DrawableFlags.Any,
        uint viewMask = Drawable.DefaultViewMask)
        : base(result, frustums, drawableFlags, viewMask)
    {
        ArgumentNullException.ThrowIfNull(buffers);
        if (buffers.Length < 2)
            throw new ArgumentException("Stereo occlusion query requires two buffers.", nameof(buffers));
        Buffers[0] = buffers[0];
        Buffers[1] = buffers[1];
    }

    /// <summary>Intersection test for an octant.</summary>
    public override Intersection TestOctant(in BoundingBox box, bool inside)
    {
        if (inside)
        {
            return (Buffers[0]?.IsVisible(box) == true || Buffers[1]?.IsVisible(box) == true)
                ? Intersection.Inside
                : Intersection.Outside;
        }

        var result = Frustums[0].IsInside(box);
        result = (Intersection)Math.Max((int)result, (int)Frustums[1].IsInside(box));
        if (result != Intersection.Outside
            && Buffers[0]?.IsVisible(box) != true
            && Buffers[1]?.IsVisible(box) != true)
            result = Intersection.Outside;
        return result;
    }

    /// <summary>Intersection test for drawables. Note: drawable occlusion is performed later in worker threads.</summary>
    public override void TestDrawables(IReadOnlyList<Drawable> drawables, bool inside)
    {
        for (var i = 0; i < drawables.Count; ++i)
        {
            var drawable = drawables[i];
            if ((drawable.GetDrawableFlags() & DrawableFlags) != 0 && (drawable.GetViewMask() & ViewMask) != 0)
            {
                var bb = drawable.GetWorldBoundingBox();
                if (inside
                    || Frustums[0].IsInsideFast(bb) != Intersection.Outside
                    || Frustums[1].IsInsideFast(bb) != Intersection.Outside)
                    Result.Add(drawable);
            }
        }
    }
}

/// <summary>Scene processor specialized for stereo viewports.</summary>
public class StereoSceneProcessor : SceneProcessor
{
    private OcclusionBuffer? _secondaryOcclusion;
    private readonly OcclusionBuffer?[] _eyeOcclusion = new OcclusionBuffer?[2];
    private OcclusionBuffer?[] _currentOcclusionBuffers = [null, null];

    public StereoSceneProcessor(
        IRenderPipelineInterface renderPipeInterface, ShadowMapAllocator? shadowAlloc, InstancingBuffer? instBuffer)
        : base(renderPipeInterface, "shadow", shadowAlloc, instBuffer)
    {
    }

    public override void Dispose()
    {
        _secondaryOcclusion?.Dispose();
        base.Dispose();
    }

    public override void Update()
    {
        // Collect occluders
        CurrentOcclusionBuffer = null;
        _currentOcclusionBuffers = [null, null];

        System.Diagnostics.Debug.Assert(FrameInfo.AdditionalCameras.First is not null);

        // get our frustums
        Frustum[] frustums =
        [
            FrameInfo.Camera!.GetFrustum(),
            FrameInfo.AdditionalCameras.Second!.GetFrustum()
        ];
        Camera[] cameras = [FrameInfo.Camera, FrameInfo.AdditionalCameras.Second!];

        if (Settings.MaxOccluderTriangles > 0)
        {
            Occluders.Clear();
            var occluderQuery = new StereoOccluderOctreeQuery(Occluders, frustums, FrameInfo.Camera.GetPrimaryViewMask());
            FrameInfo.Octree!.GetDrawables(occluderQuery);
            DrawableProcessor.ProcessOccluders(Occluders, Settings.OccluderSizeThreshold);

            if (DrawableProcessor.HasOccluders())
            {
                if (OcclusionBuffer is null)
                {
                    OcclusionBuffer = new OcclusionBuffer();
                    _secondaryOcclusion = new OcclusionBuffer();
                    _eyeOcclusion[0] = OcclusionBuffer;
                    _eyeOcclusion[1] = _secondaryOcclusion;
                }

                for (var i = 0; i < 2; ++i)
                {
                    var buff = _eyeOcclusion[i];
                    if (buff is null)
                        continue;
                    var bufferSize = CalculateOcclusionBufferSize(Settings.OcclusionBufferSize, cameras[i]);
                    buff.SetSize(bufferSize.X, bufferSize.Y, Settings.ThreadedOcclusion);
                    // C++ sets view from frameInfo_.camera_ for both eyes
                    buff.SetView(FrameInfo.Camera);
                }

                DrawOccluders();
                if ((_eyeOcclusion[0]?.GetNumTriangles() ?? 0) > 0 || (_eyeOcclusion[1]?.GetNumTriangles() ?? 0) > 0)
                    _currentOcclusionBuffers = [_eyeOcclusion[0], _eyeOcclusion[1]];
            }
        }

        // Collect visible drawables
        Drawables.Clear();
        if (_currentOcclusionBuffers[0] is not null)
        {
            var query = new StereoOccludedFrustumOctreeQuery(Drawables, frustums, _currentOcclusionBuffers,
                DrawableFlags.Geometry | DrawableFlags.Light, FrameInfo.Camera.GetPrimaryViewMask());
            FrameInfo.Octree!.GetDrawables(query);
        }
        else
        {
            var drawableQuery = new StereoFrustumOctreeQuery(
                Drawables, frustums, DrawableFlags.Geometry | DrawableFlags.Light, FrameInfo.Camera.GetPrimaryViewMask());
            FrameInfo.Octree!.GetDrawables(drawableQuery);
        }

        // Process drawables — C++ only forwards currentOcclusionBuffer_ (always null here)
        OcclusionBuffer[]? occlusionSpan = CurrentOcclusionBuffer is not null
            ? [CurrentOcclusionBuffer]
            : null;
        DrawableProcessor.ProcessVisibleDrawables(Drawables, occlusionSpan);
        DrawableProcessor.ProcessLights(this);
        DrawableProcessor.ProcessForwardLighting();

        BatchCompositor.ComposeSceneBatches();
        if (Settings.EnableShadows)
            BatchCompositor.ComposeShadowBatches();
    }

    protected override void DrawOccluders()
    {
        var activeOccluders = DrawableProcessor.GetOccluders();

        foreach (var occlusionBuffer in _eyeOcclusion)
        {
            if (occlusionBuffer is null)
                continue;

            occlusionBuffer.SetMaxTriangles(checked((uint)Settings.MaxOccluderTriangles));
            occlusionBuffer.Clear();

            if (!occlusionBuffer.IsThreaded())
            {
                for (var i = 0; i < activeOccluders.Count; ++i)
                {
                    var occluder = activeOccluders[i].Drawable;
                    if (occluder is null)
                        continue;
                    if (i > 0 && !occlusionBuffer.IsVisible(occluder.GetWorldBoundingBox()))
                        continue;

                    var success = occluder.DrawOcclusion(occlusionBuffer);
                    occlusionBuffer.DrawTriangles();
                    if (!success)
                        break;
                }
            }
            else
            {
                for (var i = 0; i < activeOccluders.Count; ++i)
                {
                    if (activeOccluders[i].Drawable is { } drawable && !drawable.DrawOcclusion(occlusionBuffer))
                        break;
                }

                occlusionBuffer.DrawTriangles();
            }

            occlusionBuffer.BuildDepthHierarchy();
        }
    }

    private static Vector2i CalculateOcclusionBufferSize(uint size, Camera cullCamera)
    {
        var width = (int)size;
        var height = (int)MathF.Round(size / cullCamera.GetAspectRatio());
        return new Vector2i(width, height);
    }
}

/// <summary>Stereo render pipeline view.</summary>
public class StereoRenderPipelineView : RenderPipelineView
{
    private RenderPath? _originalRenderPath;
    private RenderPath? _renderPath;
    private bool _parametersDirty;

    private RenderPipelineSettings _settings = new();

    private CommonFrameInfo _frameInfo = new();
    private RenderSurface? _renderTarget;
    private RenderOutputFlag _renderOutputFlags;

    private RenderPipelineStats _stats;
    private readonly SharedRenderPassState _state = new();

    private RenderBufferManager? _renderBufferManager;
    private ShadowMapAllocator? _shadowMapAllocator;
    private InstancingBuffer? _instancingBuffer;
    private StereoSceneProcessor? _sceneProcessor;

    private UnorderedScenePass? _depthPrePass;
    private UnorderedScenePass? _opaquePass;
    private UnorderedScenePass? _postOpaquePass;
    private BackToFrontScenePass? _alphaPass;
    private BackToFrontScenePass? _postAlphaPass;

    private RenderBuffer? _outlineBuffer;
    private OutlineScenePass? _outlineScenePass;

    private uint _settingsHash;
    private bool _settingsDirty = true;
    private bool _linearColorSpace;

    public StereoRenderPipelineView(RenderPipeline pipeline)
        : base(pipeline)
    {
        var settings = pipeline.Settings;
        var sceneProcessor = settings.SceneProcessor;
        sceneProcessor.LightingMode = DirectLightingMode.Forward;
        settings.SceneProcessor = sceneProcessor;
        SetSettings(settings);
        SetRenderPath(pipeline.GetRenderPath());
        MarkParametersDirty();
    }

    public RenderPipelineSettings GetSettings() => _settings;

    public void SetSettings(RenderPipelineSettings settings)
    {
        _settings = settings;
        var renderBufferManager = _settings.RenderBufferManager;
        renderBufferManager.ColorSpace = RenderPipelineColorSpace.Optimized;
        _settings.RenderBufferManager = renderBufferManager;

        _settings.Validate();
        _settings.AdjustToSupported(Context.Instance);
        _settings.PropagateImpliedSettings();

        _settingsHash = _settings.CalculatePipelineStateHash();
        _settingsDirty = true;
    }

    public void SetRenderPath(RenderPath? renderPath)
    {
        _originalRenderPath = renderPath;
        _renderPath = null;
        _settingsDirty = true;
    }

    public void MarkParametersDirty() => _parametersDirty = true;

    public override bool IsLinearColorSpace => _linearColorSpace;

    /// <summary>Implement RenderPipelineView.</summary>
    public override bool Define(RenderSurface? renderTarget, Viewport viewport, Graphics graphics)
    {
        ArgumentNullException.ThrowIfNull(viewport);
        ArgumentNullException.ThrowIfNull(graphics);

        if (viewport.GetScene() is null || !viewport.IsStereo())
            return false;

        _renderTarget = renderTarget;

        // Ensure that render path is ready
        if (_renderPath is null && _originalRenderPath is not null)
        {
            _renderPath = _originalRenderPath.Clone();
            _renderPath.InitializeView(this);
            _settingsDirty = true;
        }

        if (_sceneProcessor is null)
        {
            _state.RenderPipelineInterface = this;
            _state.RenderBufferManager = new RenderBufferManager(this);

            _renderBufferManager = _state.RenderBufferManager;
            _shadowMapAllocator = new ShadowMapAllocator();
            _instancingBuffer = new InstancingBuffer();
            _sceneProcessor = new StereoSceneProcessor(this, _shadowMapAllocator, _instancingBuffer);

            _opaquePass = new UnorderedScenePass(this, _sceneProcessor.GetDrawableProcessor(),
                _sceneProcessor.GetPipelineStateBuilder(),
                DrawableProcessorPassFlag.StereoInstancing | DrawableProcessorPassFlag.HasAmbientLighting,
                string.Empty, "base", "litbase", "light");

            _postOpaquePass = new UnorderedScenePass(this, _sceneProcessor.GetDrawableProcessor(),
                _sceneProcessor.GetPipelineStateBuilder(),
                DrawableProcessorPassFlag.StereoInstancing, "postopaque");

            _alphaPass = new BackToFrontScenePass(this, _sceneProcessor.GetDrawableProcessor(),
                _sceneProcessor.GetPipelineStateBuilder(),
                DrawableProcessorPassFlag.NeedReadableDepth | DrawableProcessorPassFlag.RefractionPass
                | DrawableProcessorPassFlag.StereoInstancing,
                string.Empty, "alpha", "alpha", "litalpha");

            _postAlphaPass = new BackToFrontScenePass(this, _sceneProcessor.GetDrawableProcessor(),
                _sceneProcessor.GetPipelineStateBuilder(),
                DrawableProcessorPassFlag.StereoInstancing, "postalpha");

            var outlineParams = new RenderBufferParams(TextureFormat.TexFormatRgba8Unorm, 1, RenderBufferFlag.BilinearFiltering);
            _outlineBuffer = _renderBufferManager.CreateColorBuffer(outlineParams);
            _outlineBuffer.SetEnabled(false);

            _state.RenderBuffers[OutlineRenderPass.ColorBufferId] = _outlineBuffer;
        }

        _frameInfo.Viewport = viewport;
        _frameInfo.ViewportRect = viewport.GetEffectiveRect(renderTarget);
        _frameInfo.ViewportSize = _frameInfo.ViewportRect.Size;
        _frameInfo.Cameras.First = viewport.GetEye(0);
        _frameInfo.Cameras.Second = viewport.GetEye(1);

        var colorTexture = renderTarget?.GetParentTexture();
        var depthSurface = renderTarget?.GetLinkedDepthStencil();
        var depthTexture = depthSurface?.GetParentTexture();
        _frameInfo.RenderTarget = colorTexture is not null
            ? new RenderOutputTargetInfo(
                renderTarget!.GetView(),
                new IntRect(0, 0, renderTarget.GetWidth(), renderTarget.GetHeight()),
                colorTexture.Format,
                depthSurface?.GetView(),
                depthTexture?.Format ?? TextureFormat.TexFormatUnknown,
                renderTarget.GetMultiSample(),
                colorTexture,
                depthTexture,
                colorTexture.GetFilterMode() is TextureFilterMode.Bilinear or TextureFilterMode.Trilinear
                    or TextureFilterMode.Anisotropic,
                colorTexture.GetLinear() || colorTexture.GetSRGB()
                    || TextureFormatUtility.IsFloatColorFormat(colorTexture.Format),
                TextureFormatUtility.IsFloatColorFormat(colorTexture.Format),
                depthTexture is not null,
                depthTexture?.Format == TextureFormat.TexFormatD24UnormS8Uint)
            : null;

        if (!_sceneProcessor.Define(_frameInfo))
            return false;

        Camera?[] cameras = [viewport.GetEye(0), viewport.GetEye(1)];
        _sceneProcessor.SetRenderCameras(cameras);

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
        _linearColorSpace = _renderBufferManager.IsLinearColorSpace();

        var outputMultiSample = _renderBufferManager.GetOutputMultiSample();
        var outputColorFormat = _renderBufferManager.GetOutputColorFormat();
        var outputDepthFormat = _renderBufferManager.GetOutputDepthStencilFormat();

        var standardOutputDesc = new PipelineStateOutputDesc
        {
            DepthStencilFormat = outputDepthFormat,
            MultiSample = outputMultiSample
        };
        standardOutputDesc.NumRenderTargets = 1;
        standardOutputDesc.RenderTargetFormats[0] = outputColorFormat;

        _opaquePass!.SetForwardOutputDesc(standardOutputDesc);
        _depthPrePass?.SetForwardOutputDesc(standardOutputDesc);
        _postOpaquePass!.SetForwardOutputDesc(standardOutputDesc);
        _alphaPass!.SetForwardOutputDesc(standardOutputDesc);
        _postAlphaPass!.SetForwardOutputDesc(standardOutputDesc);

        var batchCompositor = _sceneProcessor.GetBatchCompositor();
        if (_shadowMapAllocator is not null)
            batchCompositor.SetShadowOutputDesc(_shadowMapAllocator.ShadowOutputDesc);

        return true;
    }

    public override void Update(Graphics graphics)
    {
        ArgumentNullException.ThrowIfNull(graphics);
        if (_sceneProcessor is null)
            return;

        var engineTime = GetRenderPipeline().GetSubsystem<Time>();
        _frameInfo.FrameNumber = checked((uint)(engineTime?.FrameNumber ?? 0U));
        _frameInfo.TimeStep = engineTime?.TimeStep ?? 0.0f;

        // Begin debug snapshot
        var input = GetRenderPipeline().GetSubsystem<Input>();
        var qualifiers = input?.GetQualifiers() ?? Qualifier.None;
        var takeSnapshot = (qualifiers & (Qualifier.Ctrl | Qualifier.Shift)) == (Qualifier.Ctrl | Qualifier.Shift)
            && input!.GetKeyPress(Key.F12);
        if (takeSnapshot)
            GetDebugger().BeginSnapshot();

        // Begin update. Should happen before pipeline state hash check.
        _shadowMapAllocator?.ResetAllShadowMaps();
        RaiseUpdateBegin(_frameInfo);
        SendBeginViewUpdateEvent();

        // Invalidate pipeline states if necessary
        var pipelineStateHash = _settings.CalculatePipelineStateHash();
        if (_settingsHash != pipelineStateHash)
        {
            _settingsHash = pipelineStateHash;
            RaisePipelineStatesInvalidated();
        }

        _outlineScenePass?.SetOutlineGroups(_sceneProcessor.GetFrameInfo().Scene!, false);

        _sceneProcessor.Update();

        if (_outlineBuffer is not null && _outlineScenePass is not null)
            _outlineBuffer.SetEnabled(_outlineScenePass.IsEnabled() && _outlineScenePass.HasBatches());

        if (_renderPath is not null)
            _renderPath.Update(_state);

        UpdateFrameParameters();

        SendEndViewUpdateEvent();
        RaiseUpdateEnd(_frameInfo);
    }

    public override void Render(Graphics graphics, MaterialQuality materialQuality)
    {
        ArgumentNullException.ThrowIfNull(graphics);
        if (_sceneProcessor is null || _renderBufferManager is null)
            return;

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
        SendViewBuffersReadyEvent();

        _sceneProcessor.PrepareDrawablesBeforeRendering();
        _sceneProcessor.PrepareInstancingBuffer();
        _sceneProcessor.RenderShadowMaps();

        var camera = _sceneProcessor.GetFrameInfo().Camera;
        if (camera is null)
            return;

        var fogColorInGammaSpace = camera.GetEffectiveFogColor();
        var effectiveFogColor = _linearColorSpace ? fogColorInGammaSpace.GammaToLinear() : fogColorInGammaSpace;

        _renderBufferManager.ClearOutput(graphics, effectiveFogColor);

        var cameraParameters = new ShaderParameterDesc[]
        {
            new(ShaderConsts.Camera_GBufferOffsets, new Variant(_renderBufferManager.GetDefaultClipToUVSpaceOffsetAndScale())),
            new(ShaderConsts.Camera_GBufferInvSize, new Variant(_renderBufferManager.GetInvOutputSize())),
        };
        var frameParameters = System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_frameParameters);

        if (_depthPrePass is not null)
        {
            _sceneProcessor.RenderSceneBatches(
                "DepthPrePass", camera, _depthPrePass.GetBaseBatches(), default, cameraParameters, frameParameters, 2);
        }

        _sceneProcessor.RenderSceneBatches(
            "OpaqueBase", camera, _opaquePass!.GetBaseBatches(), default, cameraParameters, frameParameters, 2);
        _sceneProcessor.RenderSceneBatches(
            "OpaqueLight", camera, _opaquePass.GetLightBatches(), default, cameraParameters, frameParameters, 2);
        _sceneProcessor.RenderSceneBatches(
            "PostOpaque", camera, _postOpaquePass!.GetBaseBatches(), default, cameraParameters, frameParameters, 2);

        if (hasRefraction)
            _renderBufferManager.SwapColorBuffers(true);

        var depthAndColorTextures = new ShaderResourceDesc[]
        {
            new(ShaderResources.DepthBuffer, _renderBufferManager.GetDepthStencilTexture()),
            new(ShaderResources.Emission, _renderBufferManager.GetSecondaryColorTexture()),
        };

        _sceneProcessor.RenderSceneBatches(
            "Alpha", camera, _alphaPass!.GetBatches(), depthAndColorTextures, cameraParameters, frameParameters, 2);
        _sceneProcessor.RenderSceneBatches(
            "PostAlpha", camera, _postAlphaPass!.GetBatches(), default, cameraParameters, frameParameters, 2);

        if (_outlineBuffer?.IsEnabled == true && _outlineScenePass is not null)
        {
            // TODO: Do we want it dynamic?
            const uint outlinePadding = 2;

            var batches = _outlineScenePass.GetBatches();
            batches.ScissorRect = _outlineBuffer.GetViewportRect();
            if (batches.ScissorRect.Width > outlinePadding * 2 && batches.ScissorRect.Height > outlinePadding * 2)
            {
                var rect = batches.ScissorRect;
                batches.ScissorRect = new IntRect(
                    rect.Left + (int)outlinePadding,
                    rect.Top + (int)outlinePadding,
                    rect.Right - (int)outlinePadding,
                    rect.Bottom - (int)outlinePadding);
            }

            RenderBuffer[] renderTargets = [_outlineBuffer];
            _renderBufferManager.SetRenderTargets(graphics, null, renderTargets);
            _renderBufferManager.ClearColor(graphics, _outlineBuffer, Color.TransparentBlack);
            _sceneProcessor.RenderSceneBatches("Outline", camera, batches, default, cameraParameters);
        }

        // Not going to work, something will need to be done
        var outSize = _renderBufferManager.GetOutputSize(); // used for debug view

        if (_renderPath is not null)
            _renderPath.Render(_state);

        // draw debug geometry into each half
        var debug = _sceneProcessor.GetFrameInfo().Scene?.Root.GetComponent<DebugRenderer>();
        if (_settings.DrawDebugGeometry && debug is not null && debug.IsEnabledEffective && debug.HasContent())
        {
            var renderDevice = GetRenderPipeline().GetSubsystem<RenderDevice>();
            var renderContext = renderDevice?.GetRenderContext();
            _renderBufferManager.SetOutputRenderTargets(graphics);
            IntRect[] viewportRects =
            [
                new(0, 0, outSize.X / 2, outSize.Y),
                new(outSize.X / 2, 0, outSize.X, outSize.Y)
            ];
            for (var i = 0; i < 2; ++i)
            {
                renderContext?.SetViewport(viewportRects[i]);
                debug.SetView(_frameInfo.Viewport?.GetEye(i));
                debug.Render();
            }
        }

        RaiseRenderEnd(_frameInfo);
        SendEndViewRenderEvent();

        // Update statistics
        _stats = default;
        RaiseCollectStatistics(ref _stats);

        if (GetDebugger().IsSnapshotInProgress())
        {
            GetDebugger().EndSnapshot();
            Microsoft.Extensions.Logging.LoggerExtensions.LogInformation(
                Log.GetLogger(), "RenderPipeline snapshot:\n\n{Snapshot}\n", GetDebugger().GetSnapshot().ToString());
        }
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
        var sender = (Object?)eventData.Texture ?? GetRenderPipeline().GetSubsystem<Renderer>()!;
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
        var sender = (Object?)eventData.Texture ?? GetRenderPipeline().GetSubsystem<Renderer>()!;
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
        var sender = (Object?)eventData.Texture ?? GetRenderPipeline().GetSubsystem<Renderer>()!;
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
        var sender = (Object?)eventData.Texture ?? GetRenderPipeline().GetSubsystem<Renderer>()!;
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
        var sender = (Object?)eventData.Texture ?? GetRenderPipeline().GetSubsystem<Renderer>()!;
        sender.SendEvent(ref eventData);
    }

    public override FrameInfo GetFrameInfo() => _sceneProcessor?.GetFrameInfo() ?? default;

    public override ref readonly RenderPipelineStats GetStats() => ref _stats;

    public override void DrawDebugGeometries(bool depthTest)
    {
        var scene = _sceneProcessor?.GetFrameInfo().Scene;
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
        var scene = _sceneProcessor?.GetFrameInfo().Scene;
        var debug = scene?.Root.GetComponent<DebugRenderer>();
        if (scene is null || debug is null)
            return;

        foreach (var drawable in scene.GetAllComponents().Values.OfType<Light>())
            drawable.DrawDebugGeometry(debug, depthTest);
    }

    protected void ApplySettings()
    {
        if (_sceneProcessor is null)
            return;

        _sceneProcessor.SetSettings(_settings);
        _instancingBuffer?.SetSettings(_settings.InstancingBuffer);
        _shadowMapAllocator?.SetSettings(_settings.ShadowMapAllocator);

        if (_settings.SceneProcessor.DepthPrePass && _depthPrePass is null)
        {
            _depthPrePass = new UnorderedScenePass(this, _sceneProcessor.GetDrawableProcessor(),
                _sceneProcessor.GetPipelineStateBuilder(),
                DrawableProcessorPassFlag.DepthOnlyPass | DrawableProcessorPassFlag.StereoInstancing,
                "depth");
        }
        else if (!_settings.SceneProcessor.DepthPrePass)
            _depthPrePass = null;

        _outlineScenePass = new OutlineScenePass(this, _sceneProcessor.GetDrawableProcessor(),
            _sceneProcessor.GetPipelineStateBuilder(),
            ["base", "alpha"], DrawableProcessorPassFlag.StereoInstancing);

        if (_outlineBuffer is not null)
            _state.RenderBuffers[OutlineRenderPass.ColorBufferId] = _outlineBuffer;

        _sceneProcessor.SetPasses(
        [
            _depthPrePass, _opaquePass, _postOpaquePass, _alphaPass, _postAlphaPass, _outlineScenePass
        ]);
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
        _renderBufferManager?.SetSettings(_settings.RenderBufferManager);
    }
}

/// <summary>Stereo render pipeline component.</summary>
public class StereoRenderPipeline : RenderPipeline
{
    public StereoRenderPipeline()
    {
    }

    public override RenderPipelineView Instantiate() => new StereoRenderPipelineView(this);
}
