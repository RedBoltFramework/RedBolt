namespace RedBolt;

/// <summary>Frustum query that only returns occluders.</summary>
file sealed class OccluderOctreeQuery : FrustumOctreeQuery
{
    /// <summary>Construct with frustum and query parameters.</summary>
    public OccluderOctreeQuery(List<Drawable> result, Frustum frustum, uint viewMask = Drawable.DefaultViewMask)
        : base(result, frustum, DrawableFlags.Geometry, viewMask)
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
                if (inside || Frustum.IsInsideFast(drawable.GetWorldBoundingBox()) != Intersection.Outside)
                    Result.Add(drawable);
            }
        }
    }
}

/// <summary>Frustum query that also tests against an occlusion buffer.</summary>
file sealed class OccludedFrustumOctreeQuery : FrustumOctreeQuery
{
    private readonly OcclusionBuffer _buffer;

    /// <summary>Construct with frustum, occlusion buffer and query parameters.</summary>
    public OccludedFrustumOctreeQuery(List<Drawable> result, Frustum frustum, OcclusionBuffer buffer,
        DrawableFlags drawableFlags = DrawableFlags.Any, uint viewMask = Drawable.DefaultViewMask)
        : base(result, frustum, drawableFlags, viewMask)
    {
        _buffer = buffer;
    }

    /// <summary>Intersection test for an octant.</summary>
    public override Intersection TestOctant(BoundingBox box, bool inside)
    {
        if (inside)
            return _buffer.IsVisible(box) ? Intersection.Inside : Intersection.Outside;

        var result = Frustum.IsInside(box);
        if (result != Intersection.Outside && !_buffer.IsVisible(box))
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
                if (inside || Frustum.IsInsideFast(drawable.GetWorldBoundingBox()) != Intersection.Outside)
                    Result.Add(drawable);
            }
        }
    }
}

/// <summary>Scene processor for RenderPipeline.</summary>
public class SceneProcessor : Object, LightProcessorCallback
{
    public const int ShadowMinPixels = 64;

    private readonly Graphics? _graphics;
    private readonly RenderDevice? _renderDevice;
    private readonly RenderContext? _renderContext;
    private readonly IRenderPipelineInterface _renderPipeline;
    private readonly RenderPipelineDebugger? _debugger;

    private readonly ShadowMapAllocator? _shadowMapAllocator;
    private readonly InstancingBuffer? _instancingBuffer;
    private readonly DrawCommandQueue? _drawQueue;

    private readonly CameraProcessor _cameraProcessor;
    private readonly PipelineStateBuilder _pipelineStateBuilder;
    private readonly DrawableProcessor _drawableProcessor;
    private readonly BatchCompositor _batchCompositor;
    private readonly BatchRenderer _batchRenderer;
    private OcclusionBuffer? _occlusionBuffer;
    private readonly BatchStateCacheCallback _batchStateCacheCallback;

    private SceneProcessorSettings _settings = new();
    private readonly List<ScenePass> _passes = [];

    private CommonFrameInfo _frameInfo = new();
    private bool _flipCameraForRendering;

    private OcclusionBuffer? _currentOcclusionBuffer;
    private readonly List<Drawable> _occluders = [];
    private readonly List<Drawable> _drawables = [];

    public SceneProcessor(IRenderPipelineInterface renderPipeline, string shadowTechnique,
        ShadowMapAllocator? shadowMapAllocator, InstancingBuffer? instancingBuffer)
    {
        ArgumentNullException.ThrowIfNull(renderPipeline);
        _renderPipeline = renderPipeline;
        _graphics = GetSubsystem<Graphics>();
        _renderDevice = GetSubsystem<RenderDevice>();
        _renderContext = _renderDevice?.GetRenderContext();
        _debugger = renderPipeline.GetDebugger();
        _shadowMapAllocator = shadowMapAllocator;
        _instancingBuffer = instancingBuffer;
        _drawQueue = _renderDevice?.GetDefaultQueue();

        _cameraProcessor = new CameraProcessor();
        _pipelineStateBuilder = new PipelineStateBuilder(this, _cameraProcessor, shadowMapAllocator, instancingBuffer);
        _drawableProcessor = new DrawableProcessor(renderPipeline);
        _batchStateCacheCallback = _pipelineStateBuilder;
        _batchCompositor = new BatchCompositor(renderPipeline, _drawableProcessor, _batchStateCacheCallback,
            Technique.GetPassIndex(string.IsNullOrEmpty(shadowTechnique) ? "shadow" : shadowTechnique));
        _batchRenderer = new BatchRenderer(renderPipeline, _drawableProcessor, instancingBuffer);

        renderPipeline.UpdateBegin += OnUpdateBegin;
        renderPipeline.RenderBegin += OnRenderBegin;
        renderPipeline.RenderEnd += OnRenderEnd;
        _ = _graphics;
        _ = _flipCameraForRendering;
    }

    public override void Dispose()
    {
        _renderPipeline.UpdateBegin -= OnUpdateBegin;
        _renderPipeline.RenderBegin -= OnRenderBegin;
        _renderPipeline.RenderEnd -= OnRenderEnd;
        _occlusionBuffer?.Dispose();
        base.Dispose();
    }

    /// <summary>Setup. Slow, called rarely.</summary>
    /// <remarks>It's okay to pass null passes in vector.</remarks>
    public void SetPasses(IReadOnlyList<ScenePass?> passes)
    {
        _passes.Clear();
        foreach (var pass in passes)
        {
            if (pass is not null)
                _passes.Add(pass);
        }

        _drawableProcessor.SetPasses(_passes.Cast<DrawableProcessorPass>().ToList());
        _batchCompositor.SetPasses(_passes.Cast<BatchCompositorPass>().ToList());
    }

    public void SetSettings(ShaderProgramCompositorSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _pipelineStateBuilder.SetSettings(settings);

        if (!_settings.Equals(settings.SceneProcessor))
        {
            _settings = settings.SceneProcessor;
            _drawableProcessor.SetSettings(ToDrawableProcessorSettings(_settings));
            _batchRenderer.SetSettings(new BatchRendererSettings
            {
                CubemapBoxProjection = _settings.CubemapBoxProjection,
                AmbientMode = _settings.AmbientMode,
                VarianceShadowMapParams = _settings.VarianceShadowMapParams
            });
            _batchCompositor.SetShadowMaterialQuality(_settings.MaterialQuality);
        }
    }

    /// <summary>Create a scene pass of type <typeparamref name="T"/>.</summary>
    public T CreatePass<T>(params object[] args) where T : ScenePass
    {
        // C++ template CreatePass forwards constructor args; managed callers construct passes directly.
        throw new NotSupportedException("Construct ScenePass instances directly and pass them to SetPasses.");
    }

    public bool Define(CommonFrameInfo frameInfo)
    {
        _frameInfo.FrameNumber = 0;
        _frameInfo.TimeStep = 0.0f;

        _frameInfo.Viewport = frameInfo.Viewport;
        _frameInfo.RenderTarget = frameInfo.RenderTarget;
        _frameInfo.ViewportRect = frameInfo.ViewportRect;
        _frameInfo.ViewportSize = frameInfo.ViewportSize;

        _frameInfo.Scene = _frameInfo.Viewport?.GetScene();
        _frameInfo.Camera = _frameInfo.Viewport?.GetCullCamera()
            ?? _frameInfo.Viewport?.GetCamera();
        _frameInfo.ViewReferenceNode = _frameInfo.Camera?.Node;

        _frameInfo.Octree = _frameInfo.Scene?.Root.GetComponent<Octree>();
        if (_frameInfo.Octree is not null && _frameInfo.Scene is not null)
            _frameInfo.Scene.Root.GetOrCreateComponent<ReflectionProbeManager>();

        for (var i = 0; i < frameInfo.Cameras.Length && i < _frameInfo.Cameras.Length; ++i)
            _frameInfo.Cameras[i] = frameInfo.Cameras[i];

        return _frameInfo.Octree is not null && _frameInfo.Camera is not null;
    }

    public void SetRenderCameras(ReadOnlySpan<Camera?> cameras) => _cameraProcessor.SetCameras(cameras);

    public void SetRenderCamera(Camera? camera)
    {
        Camera?[] cameras = [camera];
        _cameraProcessor.SetCameras(cameras);
    }

    public virtual void Update()
    {
        // Collect occluders
        _currentOcclusionBuffer = null;
        var frustum = _frameInfo.Camera!.GetFrustum();
        if (_settings.MaxOccluderTriangles > 0)
        {
            _occluders.Clear();
            var occluderQuery = new OccluderOctreeQuery(_occluders, frustum, _frameInfo.Camera.GetPrimaryViewMask());
            _frameInfo.Octree!.GetDrawables(occluderQuery);
            _drawableProcessor.ProcessOccluders(_occluders, _settings.OccluderSizeThreshold);

            if (_drawableProcessor.HasOccluders())
            {
                _occlusionBuffer ??= new OcclusionBuffer();
                var bufferSize = CalculateOcclusionBufferSize(_settings.OcclusionBufferSize, _frameInfo.Camera);
                _occlusionBuffer.SetSize(bufferSize.X, bufferSize.Y, _settings.ThreadedOcclusion);
                _occlusionBuffer.SetView(_frameInfo.Camera);

                DrawOccluders();
                if (_occlusionBuffer.GetNumTriangles() > 0)
                    _currentOcclusionBuffer = _occlusionBuffer;
            }
        }

        // Collect visible drawables
        _drawables.Clear();
        if (_currentOcclusionBuffer is not null)
        {
            var query = new OccludedFrustumOctreeQuery(_drawables, frustum,
                _currentOcclusionBuffer, DrawableFlags.Geometry | DrawableFlags.Light,
                _frameInfo.Camera.GetPrimaryViewMask());
            _frameInfo.Octree!.GetDrawables(query);
        }
        else
        {
            var drawableQuery = new FrustumOctreeQuery(_drawables, frustum,
                DrawableFlags.Geometry | DrawableFlags.Light, _frameInfo.Camera.GetPrimaryViewMask());
            _frameInfo.Octree!.GetDrawables(drawableQuery);
        }

        // Process drawables
        OcclusionBuffer[]? occlusionSpan = _currentOcclusionBuffer is not null
            ? [_currentOcclusionBuffer]
            : null;
        _drawableProcessor.ProcessVisibleDrawables(_drawables, occlusionSpan);
        _drawableProcessor.ProcessLights(this);
        _drawableProcessor.ProcessForwardLighting();

        _batchCompositor.ComposeSceneBatches();
        if (_settings.EnableShadows)
            _batchCompositor.ComposeShadowBatches();
        if (_settings.IsDeferredLighting())
            _batchCompositor.ComposeLightVolumeBatches();
    }

    public void PrepareInstancingBuffer()
    {
        if (_instancingBuffer is null || !_instancingBuffer.IsEnabled())
            return;

        _instancingBuffer.Begin();

        var visibleLights = _drawableProcessor.GetLightProcessors();
        foreach (var sceneLight in visibleLights)
        {
            if (sceneLight is null)
                continue;
            foreach (var split in sceneLight.GetMutableSplits())
                _batchRenderer.PrepareInstancingBuffer(ref split.GetMutableShadowBatches());
        }

        foreach (var pass in _passes)
        {
            if (pass.IsEnabled())
                pass.PrepareInstancingBuffer(_batchRenderer);
        }

        _instancingBuffer.End();
    }

    public void PrepareDrawablesBeforeRendering() => _drawableProcessor.UpdateGeometries();

    public void RenderShadowMaps()
    {
        if (!_settings.EnableShadows || _drawQueue is null || _renderContext is null)
            return;

        using var renderScope = new RenderScope(_renderContext, "SceneProcessor::RenderShadowMaps");

        var lightsByShadowMap = _drawableProcessor.GetLightProcessorsByShadowMap();
        foreach (var sceneLight in lightsByShadowMap)
        {
            if (sceneLight is null)
                continue;
            using var renderScopeLight = new RenderScope(_renderContext, "Light 0x{0} '{1}'",
                sceneLight.GetLight().GetHashCode(), sceneLight.GetLight().Node?.Name ?? string.Empty);

            foreach (var split in sceneLight.GetSplits())
            {
                using var renderScopeSplit = new RenderScope(_renderContext, "Split #{0}", split.GetSplitIndex());

                if (RenderPipelineDebugger.IsSnapshotInProgress(_debugger))
                {
                    var passName = $"ShadowMap.[{split.GetLight().GetFullNameDebug()}].{split.GetSplitIndex()}";
                    _debugger!.BeginPass(passName);
                }

                _drawQueue.Reset();
                _batchRenderer.RenderBatches(new BatchRenderingContext(_drawQueue, split), split.GetShadowBatches());
                _shadowMapAllocator?.BeginShadowMapRendering(split.GetShadowMap());
                _renderContext.Execute(_drawQueue);

                if (RenderPipelineDebugger.IsSnapshotInProgress(_debugger))
                    _debugger!.EndPass();
            }
        }
    }

    public void RenderSceneBatches(string debugName, Camera camera,
        PipelineBatchGroup<PipelineBatchByState> batchGroup,
        ReadOnlySpan<ShaderResourceDesc> globalResources = default,
        ReadOnlySpan<ShaderParameterDesc> cameraParameters = default,
        ReadOnlySpan<ShaderParameterDesc> frameParameters = default,
        uint instanceMultiplier = 1u)
    {
        RenderBatchesInternal(debugName, camera, batchGroup, globalResources, cameraParameters, frameParameters,
            instanceMultiplier);
    }

    public void RenderSceneBatches(string debugName, Camera camera,
        PipelineBatchGroup<PipelineBatchBackToFront> batchGroup,
        ReadOnlySpan<ShaderResourceDesc> globalResources = default,
        ReadOnlySpan<ShaderParameterDesc> cameraParameters = default,
        ReadOnlySpan<ShaderParameterDesc> frameParameters = default,
        uint instanceMultiplier = 1u)
    {
        RenderBatchesInternal(debugName, camera, batchGroup, globalResources, cameraParameters, frameParameters,
            instanceMultiplier);
    }

    public void RenderLightVolumeBatches(string debugName, Camera camera,
        ReadOnlySpan<ShaderResourceDesc> globalResources,
        ReadOnlySpan<ShaderParameterDesc> cameraParameters,
        ReadOnlySpan<ShaderParameterDesc> frameParameters = default,
        uint instanceMultiplier = 1u)
    {
        if (_drawQueue is null || _renderContext is null)
            return;

        if (RenderPipelineDebugger.IsSnapshotInProgress(_debugger))
            _debugger!.BeginPass(debugName);

        using var renderScope = new RenderScope(_renderContext, debugName);

        _drawQueue.Reset();

        var ctx = new BatchRenderingContext(_drawQueue, camera)
        {
            InstanceMultiplier = instanceMultiplier,
            GlobalResources = globalResources.ToArray(),
            CameraParameters = cameraParameters.ToArray(),
            FrameParameters = frameParameters.ToArray()
        };

        var lightVolumeBatches = GetLightVolumeBatches();
        _batchRenderer.RenderLightVolumeBatches(ctx,
            System.Runtime.InteropServices.CollectionsMarshal.AsSpan((List<PipelineBatchByState>)lightVolumeBatches));

        _renderContext.Execute(_drawQueue);

        if (RenderPipelineDebugger.IsSnapshotInProgress(_debugger))
            _debugger!.EndPass();
    }

    public SceneProcessorSettings GetSettings() => _settings;

    public CommonFrameInfo GetFrameInfo() => _frameInfo;

    /// <summary>
    /// Takes pass object from BatchStateCreateContext.
    /// Returns user-configured pass (inherited from BatchCompositorPass) if possible.
    /// Returns null if internal pass from BatchCompositor itself (e.g. shadow or light volume pass).
    /// </summary>
    public BatchCompositorPass? GetUserPass(Object? pass)
    {
        System.Diagnostics.Debug.Assert(pass is not null);
        if (ReferenceEquals(pass, _batchCompositor))
            return null;
        return pass as BatchCompositorPass;
    }

    public IReadOnlyList<PipelineBatchByState> GetLightVolumeBatches() => _batchCompositor.GetLightVolumeBatches();

    public CameraProcessor GetCameraProcessor() => _cameraProcessor;

    public PipelineStateBuilder GetPipelineStateBuilder() => _pipelineStateBuilder;

    public DrawableProcessor GetDrawableProcessor() => _drawableProcessor;

    public BatchCompositor GetBatchCompositor() => _batchCompositor;

    public BatchRenderer GetBatchRenderer() => _batchRenderer;

    protected virtual void DrawOccluders()
    {
        if (_occlusionBuffer is null)
            return;

        var activeOccluders = _drawableProcessor.GetOccluders();

        _occlusionBuffer.SetMaxTriangles(_settings.MaxOccluderTriangles);
        _occlusionBuffer.Clear();

        if (!_occlusionBuffer.IsThreaded())
        {
            // If not threaded, draw occluders one by one and test the next occluder against already rasterized depth
            for (var i = 0; i < activeOccluders.Count; ++i)
            {
                var occluder = activeOccluders[i].Drawable;
                if (occluder is null)
                    continue;
                if (i > 0)
                {
                    // For subsequent occluders, do a test against the pixel-level occlusion buffer to see if rendering is necessary
                    if (!_occlusionBuffer.IsVisible(occluder.GetWorldBoundingBox()))
                        continue;
                }

                // Check for running out of triangles
                var success = occluder.DrawOcclusion(_occlusionBuffer);
                // Draw triangles submitted by this occluder
                _occlusionBuffer.DrawTriangles();
                if (!success)
                    break;
            }
        }
        else
        {
            // In threaded mode submit all triangles first, then render (cannot test in this case)
            for (var i = 0; i < activeOccluders.Count; ++i)
            {
                // Check for running out of triangles
                if (activeOccluders[i].Drawable is { } drawable && !drawable.DrawOcclusion(_occlusionBuffer))
                    break;
            }

            _occlusionBuffer.DrawTriangles();
        }

        // Finally build the depth mip levels
        _occlusionBuffer.BuildDepthHierarchy();
    }

    private void RenderBatchesInternal<T>(string debugName, Camera camera,
        PipelineBatchGroup<T> batchGroup,
        ReadOnlySpan<ShaderResourceDesc> globalResources,
        ReadOnlySpan<ShaderParameterDesc> cameraParameters,
        ReadOnlySpan<ShaderParameterDesc> frameParameters,
        uint instanceMultiplier)
    {
        if (_drawQueue is null || _renderContext is null)
            return;

        if (RenderPipelineDebugger.IsSnapshotInProgress(_debugger))
            _debugger!.BeginPass(debugName);

        using var renderScope = new RenderScope(_renderContext, debugName);

        _drawQueue.Reset();

        var needClipping = camera.GetUseClipping() || _frameInfo.Cameras.Length > 1 && _frameInfo.Cameras[1] is not null;
        _drawQueue.SetClipPlaneMask(needClipping ? 1u << 0 : 0u);

        var ctx = new BatchRenderingContext(_drawQueue, camera)
        {
            InstanceMultiplier = instanceMultiplier,
            GlobalResources = globalResources.ToArray(),
            CameraParameters = cameraParameters.ToArray(),
            FrameParameters = frameParameters.ToArray()
        };

        if (batchGroup.ScissorRect != IntRect.Zero)
            _drawQueue.SetScissorRect(batchGroup.ScissorRect);

        if (typeof(T) == typeof(PipelineBatchByState))
            _batchRenderer.RenderBatches(ctx, (PipelineBatchGroup<PipelineBatchByState>)(object)batchGroup);
        else if (typeof(T) == typeof(PipelineBatchBackToFront))
            _batchRenderer.RenderBatches(ctx, (PipelineBatchGroup<PipelineBatchBackToFront>)(object)batchGroup);

        _renderContext.Execute(_drawQueue);

        if (RenderPipelineDebugger.IsSnapshotInProgress(_debugger))
            _debugger!.EndPass();
    }

    private void OnUpdateBegin(CommonFrameInfo frameInfo)
    {
        _frameInfo.FrameNumber = frameInfo.FrameNumber;
        _frameInfo.TimeStep = frameInfo.TimeStep;

        _occluders.Clear();
        _drawables.Clear();

        _cameraProcessor.OnUpdateBegin(_frameInfo);
        _drawableProcessor.OnUpdateBegin(_frameInfo);
        _pipelineStateBuilder.UpdateFrameSettings(_renderPipeline.IsLinearColorSpace);
    }

    private void OnRenderBegin(CommonFrameInfo frameInfo) => _cameraProcessor.OnRenderBegin(frameInfo);

    private void OnRenderEnd(CommonFrameInfo frameInfo) => _cameraProcessor.OnRenderEnd(frameInfo);

    public bool IsLightShadowed(Light light)
    {
        var shadowsEnabled = _settings.EnableShadows
            && light.GetCastShadows()
            && light.GetLightImportance() != LightImportance.NotImportant
            && light.GetShadowIntensity() < 1.0f;

        if (!shadowsEnabled)
            return false;

        if (light.GetShadowDistance() > 0.0f && light.GetDistance() > light.GetShadowDistance())
            return false;

        return true;
    }

    public uint GetShadowMapSize(Light light, uint numActiveSplits)
    {
        _ = numActiveSplits;
        var parameters = light.GetShadowFocus();
        var shadowResolutionScale = light.GetShadowResolution();
        var pageSize = _shadowMapAllocator?.Settings.ShadowAtlasPageSize ?? 2048u;
        var maxShadowSize = GetMaxShadowSplitSize(_settings, pageSize, light);

        var lightSizeInPixels = GetLightSizeInPixels(light, _frameInfo);
        var baseShadowSize = parameters.AutoSize ? lightSizeInPixels : maxShadowSize;
        var shadowSize = MathDefs.NextPowerOfTwo((uint)(baseShadowSize * shadowResolutionScale));

        return Math.Clamp(shadowSize, (uint)ShadowMinPixels, maxShadowSize);
    }

    public ShadowMapRegion AllocateTransientShadowMap(Vector2i size) =>
        _shadowMapAllocator?.AllocateShadowMap(size) ?? default;

    private static Vector2i CalculateOcclusionBufferSize(uint size, Camera cullCamera)
    {
        var width = (int)size;
        var height = (int)MathF.Round(size / cullCamera.GetAspectRatio());
        return new Vector2i(width, height);
    }

    private static BoundingBox GetViewSpaceLightBoundingBox(Light light, Camera camera)
    {
        var view = camera.GetView();

        switch (light.GetLightType())
        {
            case LightType.Point:
            {
                var center = view * light.Node!.GetWorldPosition();
                var extent = 0.58f * light.GetRange();
                return new BoundingBox(center - extent * Vector3f.One, center + extent * Vector3f.One);
            }
            case LightType.Spot:
            {
                var lightFrustum = light.GetViewSpaceFrustum(view);
                return new BoundingBox(lightFrustum.Vertices.AsSpan(4, 4).ToArray());
            }
            default:
                System.Diagnostics.Debug.Assert(false);
                return default;
        }
    }

    private static float GetLightSizeInPixels(Light light, CommonFrameInfo frameInfo)
    {
        var viewSize = new Vector2f(frameInfo.ViewportSize.X, frameInfo.ViewportSize.Y);
        var lightType = light.GetLightType();
        if (lightType == LightType.Directional)
            return Math.Max(viewSize.X, viewSize.Y);

        var lightBox = GetViewSpaceLightBoundingBox(light, frameInfo.Camera!);
        var projection = frameInfo.Camera!.GetProjection();
        var projectionSize = 0.5f * lightBox.Projected(projection).Size * viewSize;
        return Math.Max(projectionSize.X, projectionSize.Y);
    }

    private static uint GetMaxShadowSplitSize(SceneProcessorSettings settings, uint pageSize, Light light) =>
        light.GetLightType() switch
        {
            LightType.Directional => Math.Min(settings.DirectionalShadowSize,
                light.GetNumShadowSplits() == 1 ? pageSize : pageSize / 2),
            LightType.Point => Math.Min(settings.PointShadowSize, pageSize / 4),
            _ => Math.Min(settings.SpotShadowSize, pageSize)
        };

    private static DrawableProcessorSettings ToDrawableProcessorSettings(SceneProcessorSettings settings)
    {
        var result = new DrawableProcessorSettings
        {
            MaterialQuality = settings.MaterialQuality,
            MaxVertexLights = settings.MaxVertexLights,
            MaxPixelLights = settings.MaxPixelLights,
            PcfKernelSize = settings.PcfKernelSize,
            NormalOffsetScale = settings.NormalOffsetScale
        };
        result.LightProcessorCache.Budget = settings.LightProcessorCache.Budget;
        result.LightProcessorCache.NormalTimeToLive = settings.LightProcessorCache.NormalTimeToLive;
        result.LightProcessorCache.AggressiveTimeToLive = settings.LightProcessorCache.AggressiveTimeToLive;
        return result;
    }
}
