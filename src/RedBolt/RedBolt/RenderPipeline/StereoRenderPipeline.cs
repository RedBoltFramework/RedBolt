namespace RedBolt.RenderPipeline;

using SystemMath = global::System.Math;

using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.Input;
using RedBolt.Logging;
using RedBolt.Math;
using RedBolt.RenderAPI;
using RedBolt.Scene;

internal class StereoFrustumOctreeQuery : OctreeQuery
{
    /// <summary>Stereo frustums.</summary>
    public Frustum[] Frustums { get; } = new Frustum[2];

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
        inter = (Intersection)SystemMath.Max((int)inter, (int)Frustums[1].IsInside(box));
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

    public void SetFrustums(Frustum first, Frustum second)
    {
        Array.Copy(first.Planes, Frustums[0].Planes, Frustum.NumFrustumPlanes);
        Array.Copy(first.Vertices, Frustums[0].Vertices, Frustum.NumFrustumVertices);
        Array.Copy(second.Planes, Frustums[1].Planes, Frustum.NumFrustumPlanes);
        Array.Copy(second.Vertices, Frustums[1].Vertices, Frustum.NumFrustumVertices);
    }
}

internal class StereoOccluderOctreeQuery : StereoFrustumOctreeQuery
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

internal class StereoOccludedFrustumOctreeQuery : StereoFrustumOctreeQuery
{
    /// <summary>Occlusion buffers.</summary>
    public OcclusionBuffer?[] Buffers { get; } = new OcclusionBuffer?[2];

    /// <summary>Construct with frustum, occlusion buffer and query parameters.</summary>
    public StereoOccludedFrustumOctreeQuery(List<Drawable> result, Frustum[] frustums,
        OcclusionBuffer?[] buffers, DrawableFlags drawableFlags = DrawableFlags.Any,
        uint viewMask = Drawable.DefaultViewMask)
        : base(result, frustums, drawableFlags, viewMask)
    {
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
        result = (Intersection)SystemMath.Max((int)result, (int)Frustums[1].IsInside(box));
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

    public void SetBuffers(OcclusionBuffer? first, OcclusionBuffer? second)
    {
        Buffers[0] = first;
        Buffers[1] = second;
    }
}

internal class StereoSceneProcessor : SceneProcessor
{
    protected OcclusionBuffer? _secondaryOcclusion;
    protected readonly OcclusionBuffer?[] _eyeOcclusion = new OcclusionBuffer?[2];
    protected readonly OcclusionBuffer?[] _currentOcclusionBuffers = new OcclusionBuffer?[2];
    private readonly Camera?[] _cameras = new Camera?[2];
    private readonly OcclusionBuffer[] _currentOcclusionBufferSpan = new OcclusionBuffer[1];
    private readonly StereoOccluderOctreeQuery _occluderQuery;
    private readonly StereoOccludedFrustumOctreeQuery _occludedQuery;
    private readonly StereoFrustumOctreeQuery _drawableQuery;

    public StereoSceneProcessor(
        IRenderPipelineInterface renderPipeInterface, ShadowMapAllocator? shadowAlloc, InstancingBuffer? instBuffer)
        : base(renderPipeInterface, "shadow", shadowAlloc, instBuffer)
    {
        var frustums = new[] { new Frustum(), new Frustum() };
        _occluderQuery = new StereoOccluderOctreeQuery(Occluders, frustums);
        _occludedQuery = new StereoOccludedFrustumOctreeQuery(Drawables, frustums, _currentOcclusionBuffers,
            DrawableFlags.Geometry | DrawableFlags.Light);
        _drawableQuery = new StereoFrustumOctreeQuery(Drawables, frustums,
            DrawableFlags.Geometry | DrawableFlags.Light, Drawable.DefaultViewMask);
    }

    public override void Dispose()
    {
        _occludedQuery.SetBuffers(null, null);
        Array.Clear(_currentOcclusionBuffers);
        Array.Clear(_eyeOcclusion);
        Array.Clear(_cameras);
        Array.Clear(_currentOcclusionBufferSpan);
        _secondaryOcclusion?.Dispose();
        _secondaryOcclusion = null;
        base.Dispose();
    }

    public override void Update()
    {
        // Collect occluders
        CurrentOcclusionBuffer = null;
        Array.Clear(_currentOcclusionBuffers);

        System.Diagnostics.Debug.Assert(FrameInfo.AdditionalCameras.First is not null);

        // get our frustums
        var firstFrustum = FrameInfo.Camera!.GetFrustum();
        var secondFrustum = FrameInfo.AdditionalCameras.Second!.GetFrustum();
        _occluderQuery.SetFrustums(firstFrustum, secondFrustum);
        _occludedQuery.SetFrustums(firstFrustum, secondFrustum);
        _drawableQuery.SetFrustums(firstFrustum, secondFrustum);
        _cameras[0] = FrameInfo.Camera;
        _cameras[1] = FrameInfo.AdditionalCameras.Second;

        if (Settings.MaxOccluderTriangles > 0)
        {
            _occluderQuery.ViewMask = FrameInfo.Camera.GetPrimaryViewMask();
            FrameInfo.Octree!.GetDrawables(_occluderQuery);
            DrawableProcessor.ProcessOccluders(Occluders, Settings.OccluderSizeThreshold);

            if (DrawableProcessor.HasOccluders)
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
                    static Vector2i CalculateOcclusionBufferSize(int size, Camera cullCamera)
                    {
                        var height = (int)MathF.Round((float)size / cullCamera.GetAspectRatio());
                        return new Vector2i(size, height);
                    }

                    var bufferSize = CalculateOcclusionBufferSize(Settings.OcclusionBufferSize, _cameras[i]!);
                    buff.SetSize(bufferSize.X, bufferSize.Y, Settings.ThreadedOcclusion);
                    // C++ sets view from frameInfo_.camera_ for both eyes
                    buff.SetView(FrameInfo.Camera);
                }

                DrawOccluders();
                if ((_eyeOcclusion[0]?.GetNumTriangles() ?? 0) > 0 || (_eyeOcclusion[1]?.GetNumTriangles() ?? 0) > 0)
                {
                    _currentOcclusionBuffers[0] = _eyeOcclusion[0];
                    _currentOcclusionBuffers[1] = _eyeOcclusion[1];
                }
            }
        }
        Array.Clear(_cameras);

        // Collect visible drawables
        if (_currentOcclusionBuffers[0] is not null)
        {
            _occludedQuery.ViewMask = FrameInfo.Camera.GetPrimaryViewMask();
            _occludedQuery.SetBuffers(_currentOcclusionBuffers[0], _currentOcclusionBuffers[1]);
            FrameInfo.Octree!.GetDrawables(_occludedQuery);
        }
        else
        {
            _drawableQuery.ViewMask = FrameInfo.Camera.GetPrimaryViewMask();
            FrameInfo.Octree!.GetDrawables(_drawableQuery);
        }

        // Process drawables — C++ only forwards currentOcclusionBuffer_ (always null here)
        OcclusionBuffer[]? currentOcclusionBuffer = null;
        if (CurrentOcclusionBuffer is not null)
        {
            _currentOcclusionBufferSpan[0] = CurrentOcclusionBuffer;
            currentOcclusionBuffer = _currentOcclusionBufferSpan;
        }
        DrawableProcessor.ProcessVisibleDrawables(Drawables, currentOcclusionBuffer);
        _currentOcclusionBufferSpan[0] = null!;
        DrawableProcessor.ProcessLights(this);
        DrawableProcessor.ProcessForwardLighting();

        BatchCompositor.ComposeSceneBatches();
        if (Settings.EnableShadows)
            BatchCompositor.ComposeShadowBatches();
    }

    protected override void DrawOccluders()
    {
        var activeOccluders = DrawableProcessor.Occluders;

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
                    var occluder = activeOccluders[i].Drawable!;
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
                    if (!activeOccluders[i].Drawable!.DrawOcclusion(occlusionBuffer))
                        break;
                }

                occlusionBuffer.DrawTriangles();
            }

            occlusionBuffer.BuildDepthHierarchy();
        }
    }

}

public class StereoRenderPipelineView : RenderPipelineView
{
    private static readonly FrameInfo DefaultFrameInfo;

    protected RenderPath? _originalRenderPath;
    protected RenderPath? _renderPath;
    protected bool _parametersDirty;

    protected RenderPipelineSettings _settings = new();

    protected CommonFrameInfo _frameInfo = new();
    protected RenderOutputFlag _renderOutputFlags;

    protected RenderPipelineStats _stats;
    protected readonly RenderPipelineDebugger _debugger = new();
    protected readonly SharedRenderPassState _state = new();

    protected RenderBufferManager? _renderBufferManager;
    protected ShadowMapAllocator? _shadowMapAllocator;
    protected InstancingBuffer? _instancingBuffer;
    protected SceneProcessor? _sceneProcessor;

    protected UnorderedScenePass? _depthPrePass;
    protected UnorderedScenePass? _opaquePass;
    protected UnorderedScenePass? _postOpaquePass;
    protected BackToFrontScenePass? _alphaPass;
    protected BackToFrontScenePass? _postAlphaPass;

    protected RenderBuffer? _outlineBuffer;
    protected OutlineScenePass? _outlineScenePass;

    private readonly Camera?[] _cameras = new Camera?[2];
    private readonly ShaderParameterDesc[] _cameraParameters = new ShaderParameterDesc[2];
    private readonly ShaderResourceDesc[] _depthAndColorTextures = new ShaderResourceDesc[2];
    private static IntRect[]? _viewportRects;

    protected uint _settingsHash;
    protected uint _oldPipelineStateHash;
    protected bool _settingsDirty;

    public StereoRenderPipelineView(RenderPipelineComponent pipeline)
        : base(pipeline)
    {
        var settings = pipeline.Settings;
        var sceneProcessor = settings.SceneProcessor;
        sceneProcessor.LightingMode = DirectLightingMode.Forward;
        settings.SceneProcessor = sceneProcessor;
        SetSettings(settings);
        SetRenderPath(pipeline.GetRenderPath());
        MarkParametersDirty();

        pipeline.SettingsChanged += OnSettingsChanged;
        pipeline.RenderPathChanged += OnRenderPathChanged;
        pipeline.ParametersChanged += OnParametersChanged;
    }

    public ref readonly RenderPipelineSettings Settings => ref _settings;

    public void SetSettings(in RenderPipelineSettings settings)
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
        _renderPath?.Dispose();
        _renderPath = null;
    }

    public void MarkParametersDirty() => _parametersDirty = true;

    public override RenderPipelineDebugger? GetDebugger() => null;

    public override void Dispose()
    {
        var pipeline = GetRenderPipeline();
        pipeline.ParametersChanged -= OnParametersChanged;
        pipeline.RenderPathChanged -= OnRenderPathChanged;
        pipeline.SettingsChanged -= OnSettingsChanged;

        _renderPath?.Dispose();
        _renderPath = null;
        _originalRenderPath = null;
        _outlineScenePass?.Dispose();
        _outlineScenePass = null;
        _outlineBuffer = null;
        _postAlphaPass?.Dispose();
        _postAlphaPass = null;
        _alphaPass?.Dispose();
        _alphaPass = null;
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
        for (var i = _cameraParameters.Length - 1; i >= 0; --i)
        {
            _cameraParameters[i].Dispose();
            _cameraParameters[i] = default;
        }
        Array.Clear(_depthAndColorTextures);
        base.Dispose();
    }

    /// <summary>Implement RenderPipelineView.</summary>
    public override bool Define(RenderSurface? renderTarget, Viewport viewport, Graphics graphics)
    {
        if (viewport.GetScene() is null || !viewport.IsStereo())
            return false;

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

            _opaquePass = _sceneProcessor.CreatePass<UnorderedScenePass>(
                DrawableProcessorPassFlag.StereoInstancing | DrawableProcessorPassFlag.HasAmbientLighting,
                string.Empty, "base", "litbase", "light");

            _postOpaquePass = _sceneProcessor.CreatePass<UnorderedScenePass>(
                DrawableProcessorPassFlag.StereoInstancing, "postopaque");

            _alphaPass = _sceneProcessor.CreatePass<BackToFrontScenePass>(
                DrawableProcessorPassFlag.NeedReadableDepth | DrawableProcessorPassFlag.RefractionPass
                | DrawableProcessorPassFlag.StereoInstancing,
                string.Empty, "alpha", "alpha", "litalpha");

            _postAlphaPass = _sceneProcessor.CreatePass<BackToFrontScenePass>(
                DrawableProcessorPassFlag.StereoInstancing, "postalpha");

            var outlineParams = new RenderBufferParams(TextureFormat.TexFormatRgba8Unorm, 1, RenderBufferFlag.BilinearFiltering);
            _outlineBuffer = _renderBufferManager.CreateColorBuffer(outlineParams);
            _outlineBuffer.SetEnabled(false);

            _state.RenderBuffers[OutlineRenderPass.ColorBufferId] = _outlineBuffer;
        }

        _frameInfo.Viewport = viewport;
        _frameInfo.RenderTarget = renderTarget;
        _frameInfo.ViewportRect = viewport.GetEffectiveRect(renderTarget);
        _frameInfo.ViewportSize = _frameInfo.ViewportRect.Size;
        _frameInfo.Cameras.First = viewport.GetEye(0);
        _frameInfo.Cameras.Second = viewport.GetEye(1);

        if (!_sceneProcessor.Define(_frameInfo))
            return false;

        _cameras[0] = viewport.GetEye(0);
        _cameras[1] = viewport.GetEye(1);
        _sceneProcessor.SetRenderCameras(_cameras);
        Array.Clear(_cameras);

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

        _opaquePass!.SetForwardOutputDesc(standardOutputDesc);
        _depthPrePass?.SetForwardOutputDesc(standardOutputDesc);
        _postOpaquePass!.SetForwardOutputDesc(standardOutputDesc);
        _alphaPass!.SetForwardOutputDesc(standardOutputDesc);
        _postAlphaPass!.SetForwardOutputDesc(standardOutputDesc);

        var batchCompositor = _sceneProcessor.GetBatchCompositor();
        batchCompositor.SetShadowOutputDesc(_shadowMapAllocator!.ShadowOutputDesc);

        return true;
    }

    public override void Update(Graphics graphics)
    {
        var engineTime = GetRenderPipeline().GetSubsystem<Time>()!;
        _frameInfo.FrameNumber = engineTime.FrameNumber;
        _frameInfo.TimeStep = engineTime.TimeStep;

        // Begin debug snapshot
        var input = GetRenderPipeline().GetSubsystem<InputSystem>()!;
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
        var pipelineStateHash = _settings.CalculatePipelineStateHash();
        if (_settingsHash != pipelineStateHash)
        {
            _oldPipelineStateHash = pipelineStateHash;
            RaisePipelineStatesInvalidated();
        }

        _outlineScenePass!.SetOutlineGroups(_sceneProcessor!.GetFrameInfo().Scene!, false);

        _sceneProcessor.Update();

        _outlineBuffer!.SetEnabled(_outlineScenePass.IsEnabled && _outlineScenePass.HasBatches);

        if (_renderPath is not null)
            _renderPath.Update(_state);

        UpdateFrameParameters();

        SendEndViewUpdateEvent();
        RaiseUpdateEnd(_frameInfo);
    }

    public override void Render(Graphics graphics, MaterialQuality materialQuality)
    {
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

        _sceneProcessor!.PrepareDrawablesBeforeRendering();
        _sceneProcessor.PrepareInstancingBuffer();
        _sceneProcessor.RenderShadowMaps();

        var camera = _sceneProcessor.GetFrameInfo().Camera!;

        var fogColorInGammaSpace = camera.GetEffectiveFogColor();
        var effectiveFogColor = _linearColorSpace ? fogColorInGammaSpace.GammaToLinear() : fogColorInGammaSpace;

        _renderBufferManager.ClearOutput(effectiveFogColor);

        _cameraParameters[0] = new ShaderParameterDesc
        {
            Name = ShaderConsts.Camera_GBufferOffsets,
            Value = new Variant(_renderBufferManager.DefaultClipToUVSpaceOffsetAndScale)
        };
        _cameraParameters[1] = new ShaderParameterDesc
        {
            Name = ShaderConsts.Camera_GBufferInvSize,
            Value = new Variant(_renderBufferManager.InvOutputSize)
        };
        var frameParameters = System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_frameParameters);

        try
        {

            if (_depthPrePass is not null)
            {
                _sceneProcessor.RenderSceneBatches(
                    "DepthPrePass", camera, _depthPrePass.GetBaseBatches(), default, _cameraParameters, frameParameters, 2);
            }

            _sceneProcessor.RenderSceneBatches(
                "OpaqueBase", camera, _opaquePass!.GetBaseBatches(), default, _cameraParameters, frameParameters, 2);
            _sceneProcessor.RenderSceneBatches(
                "OpaqueLight", camera, _opaquePass.GetLightBatches(), default, _cameraParameters, frameParameters, 2);
            _sceneProcessor.RenderSceneBatches(
                "PostOpaque", camera, _postOpaquePass!.GetBaseBatches(), default, _cameraParameters, frameParameters, 2);

            if (hasRefraction)
                _renderBufferManager.SwapColorBuffers(true);

            _depthAndColorTextures[0] = new ShaderResourceDesc
            {
                Name = ShaderResources.DepthBuffer,
                Texture = _renderBufferManager.DepthStencilTexture
            };
            _depthAndColorTextures[1] = new ShaderResourceDesc
            {
                Name = ShaderResources.Emission,
                Texture = _renderBufferManager.SecondaryColorTexture
            };

            _sceneProcessor.RenderSceneBatches(
                "Alpha", camera, _alphaPass!.GetBatches(), _depthAndColorTextures, _cameraParameters, frameParameters, 2);
            _sceneProcessor.RenderSceneBatches(
                "PostAlpha", camera, _postAlphaPass!.GetBatches(), default, _cameraParameters, frameParameters, 2);

            if (_outlineBuffer!.IsEnabled)
            {
                // TODO: Do we want it dynamic?
                const int outlinePadding = 2;

                var batches = _outlineScenePass!.Batches;
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

                _renderBufferManager.SetRenderTargets(null, _outlineBuffer);
                _renderBufferManager.ClearColor(_outlineBuffer, Color.TransparentBlack);
                _sceneProcessor.RenderSceneBatches("Outline", camera, batches, default, _cameraParameters);
            }

            // Not going to work, something will need to be done
            var outSize = _renderBufferManager.OutputSize; // used for debug view

            if (_renderPath is not null)
                _renderPath.Render(_state);

            // draw debug geometry into each half
            var debug = _sceneProcessor.GetFrameInfo().Scene?.GetComponent<DebugRenderer>();
            if (_settings.DrawDebugGeometry && debug is not null && debug.IsEnabledEffective && debug.HasContent())
            {
                var renderDevice = GetRenderPipeline().GetSubsystem<RenderDevice>();
                var renderContext = renderDevice!.GetRenderContext();
                _renderBufferManager.SetOutputRenderTargets();
                _viewportRects ??=
                [
                    new IntRect(0, 0, outSize.X / 2, outSize.Y),
                new IntRect(outSize.X / 2, 0, outSize.X, outSize.Y)
                ];
                for (var i = 0; i < 2; ++i)
                {
                    renderContext.SetViewport(_viewportRects[i]);
                    debug.SetView(_frameInfo.Viewport?.GetEye(i));
                    debug.Render();
                }
            }

            RaiseRenderEnd(_frameInfo);
            SendEndViewRenderEvent();

            // Update statistics
            _stats = default;
            RaiseCollectStatistics(ref _stats);

            if (_debugger.IsSnapshotInProgress())
            {
                _debugger.EndSnapshot();
                Microsoft.Extensions.Logging.LoggerExtensions.LogInformation(
                    Log.GetLogger(), "RenderPipeline snapshot:\n\n{Snapshot}\n", _debugger.GetSnapshot().ToString());
            }
        }
        finally
        {
            Array.Clear(_depthAndColorTextures);
            for (var i = _cameraParameters.Length - 1; i >= 0; --i)
            {
                _cameraParameters[i].Dispose();
                _cameraParameters[i] = default;
            }
        }
    }

    protected void SendBeginViewUpdateEvent()
    {
        var eventData = new GraphicsEvents.BeginViewUpdateEvent
        {
            RenderPipelineView = this,
            Texture = _frameInfo.RenderTarget?.GetParentTexture(),
            Surface = _frameInfo.RenderTarget,
            Scene = _sceneProcessor!.GetFrameInfo().Scene,
            Camera = _sceneProcessor.GetFrameInfo().Camera
        };
        var sender = (RedObject?)eventData.Texture ?? GetRenderPipeline().GetSubsystem<Renderer>()!;
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
        var sender = (RedObject?)eventData.Texture ?? GetRenderPipeline().GetSubsystem<Renderer>()!;
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
        var sender = (RedObject?)eventData.Texture ?? GetRenderPipeline().GetSubsystem<Renderer>()!;
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
        var sender = (RedObject?)eventData.Texture ?? GetRenderPipeline().GetSubsystem<Renderer>()!;
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
        var sender = (RedObject?)eventData.Texture ?? GetRenderPipeline().GetSubsystem<Renderer>()!;
        sender.SendEvent(ref eventData);
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
    }

    public override void DrawDebugLights(bool depthTest)
    {
    }

    protected void ApplySettings()
    {
        var oldDepthPrePass = _depthPrePass;
        var oldOutlineScenePass = _outlineScenePass;

        _sceneProcessor!.SetSettings(_settings);
        _instancingBuffer!.SetSettings(_settings.InstancingBuffer);
        _shadowMapAllocator!.SetSettings(_settings.ShadowMapAllocator);

        if (_settings.SceneProcessor.DepthPrePass && _depthPrePass is null)
        {
            _depthPrePass = _sceneProcessor.CreatePass<UnorderedScenePass>(
                DrawableProcessorPassFlag.DepthOnlyPass | DrawableProcessorPassFlag.StereoInstancing,
                "depth");
        }
        else
            _depthPrePass = null;

        _outlineScenePass = _sceneProcessor.CreatePass<OutlineScenePass>(
            new string[] { "base", "alpha" }, DrawableProcessorPassFlag.StereoInstancing);

        _sceneProcessor.SetPasses(
        [
            _depthPrePass, _opaquePass, _postOpaquePass, _alphaPass, _postAlphaPass, _outlineScenePass
        ]);

        if (!ReferenceEquals(oldOutlineScenePass, _outlineScenePass))
            oldOutlineScenePass?.Dispose();
        if (!ReferenceEquals(oldDepthPrePass, _depthPrePass))
            oldDepthPrePass?.Dispose();
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

    private void OnSettingsChanged(RenderPipelineComponent pipeline, RenderPipelineSettings settings) => SetSettings(in settings);

    private void OnRenderPathChanged(RenderPipelineComponent pipeline, RenderPath? renderPath) => SetRenderPath(renderPath);

    private void OnParametersChanged(RenderPipelineComponent pipeline) => MarkParametersDirty();
}

[Reflected]
public partial class StereoRenderPipeline : RenderPipelineComponent
{
    public StereoRenderPipeline()
    {
    }

    public override void Dispose()
    {
        base.Dispose();
    }


    public override RenderPipelineView Instantiate() => new StereoRenderPipelineView(this);
}
