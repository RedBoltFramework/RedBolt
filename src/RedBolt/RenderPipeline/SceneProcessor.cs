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
    public override Intersection TestOctant(in BoundingBox box, bool inside)
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

    protected readonly Graphics? GraphicsDevice;
    protected readonly RenderDevice? RenderDevice;
    protected readonly RenderContext? RenderContext;
    protected readonly IRenderPipelineInterface RenderPipeline;
    protected readonly RenderPipelineDebugger? Debugger;

    protected readonly ShadowMapAllocator? ShadowMapAllocator;
    protected readonly InstancingBuffer? InstancingBuffer;
    protected readonly DrawCommandQueue? DrawQueue;

    protected readonly CameraProcessor CameraProcessor;
    protected readonly PipelineStateBuilder PipelineStateBuilder;
    protected readonly DrawableProcessor DrawableProcessor;
    protected readonly BatchCompositor BatchCompositor;
    protected readonly BatchRenderer BatchRenderer;
    protected OcclusionBuffer? OcclusionBuffer;
    protected readonly BatchStateCacheCallback BatchStateCacheCallback;

    protected SceneProcessorSettings Settings = new();
    protected readonly List<ScenePass> Passes = [];

    protected CommonFrameInfo FrameInfo = new();
    protected bool FlipCameraForRendering;

    protected OcclusionBuffer? CurrentOcclusionBuffer;
    protected readonly List<Drawable> Occluders = [];
    protected readonly List<Drawable> Drawables = [];

    public SceneProcessor(IRenderPipelineInterface renderPipeline, string shadowTechnique,
        ShadowMapAllocator? shadowMapAllocator, InstancingBuffer? instancingBuffer)
    {
        ArgumentNullException.ThrowIfNull(renderPipeline);
        RenderPipeline = renderPipeline;
        GraphicsDevice = GetSubsystem<Graphics>();
        RenderDevice = GetSubsystem<RenderDevice>();
        RenderContext = RenderDevice?.GetRenderContext();
        Debugger = renderPipeline.GetDebugger();
        ShadowMapAllocator = shadowMapAllocator;
        InstancingBuffer = instancingBuffer;
        DrawQueue = RenderDevice?.GetDefaultQueue();

        CameraProcessor = new CameraProcessor();
        PipelineStateBuilder = new PipelineStateBuilder(this, CameraProcessor, shadowMapAllocator, instancingBuffer);
        DrawableProcessor = new DrawableProcessor(renderPipeline);
        BatchStateCacheCallback = PipelineStateBuilder;
        BatchCompositor = new BatchCompositor(renderPipeline, DrawableProcessor, BatchStateCacheCallback,
            Technique.GetPassIndex(string.IsNullOrEmpty(shadowTechnique) ? "shadow" : shadowTechnique));
        BatchRenderer = new BatchRenderer(renderPipeline, DrawableProcessor, instancingBuffer);

        renderPipeline.UpdateBegin += OnUpdateBegin;
        renderPipeline.RenderBegin += OnRenderBegin;
        renderPipeline.RenderEnd += OnRenderEnd;
        _ = GraphicsDevice;
        _ = FlipCameraForRendering;
    }

    public override void Dispose()
    {
        RenderPipeline.UpdateBegin -= OnUpdateBegin;
        RenderPipeline.RenderBegin -= OnRenderBegin;
        RenderPipeline.RenderEnd -= OnRenderEnd;
        OcclusionBuffer?.Dispose();
        base.Dispose();
    }

    /// <summary>Setup. Slow, called rarely.</summary>
    /// <remarks>It's okay to pass null passes in vector.</remarks>
    public void SetPasses(IReadOnlyList<ScenePass?> passes)
    {
        Passes.Clear();
        foreach (var pass in passes)
        {
            if (pass is not null)
                Passes.Add(pass);
        }

        DrawableProcessor.SetPasses(Passes.Cast<DrawableProcessorPass>().ToList());
        BatchCompositor.SetPasses(Passes.Cast<BatchCompositorPass>().ToList());
    }

    public void SetSettings(ShaderProgramCompositorSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        PipelineStateBuilder.SetSettings(settings);

        if (!Settings.Equals(settings.SceneProcessor))
        {
            Settings = settings.SceneProcessor;
            DrawableProcessor.SetSettings(ToDrawableProcessorSettings(Settings));
            BatchRenderer.SetSettings(new BatchRendererSettings
            {
                CubemapBoxProjection = Settings.CubemapBoxProjection,
                AmbientMode = Settings.AmbientMode,
                VarianceShadowMapParams = Settings.VarianceShadowMapParams
            });
            BatchCompositor.SetShadowMaterialQuality(Settings.MaterialQuality);
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
        FrameInfo.FrameNumber = 0;
        FrameInfo.TimeStep = 0.0f;

        FrameInfo.Viewport = frameInfo.Viewport;
        FrameInfo.RenderTarget = frameInfo.RenderTarget;
        FrameInfo.ViewportRect = frameInfo.ViewportRect;
        FrameInfo.ViewportSize = frameInfo.ViewportSize;

        FrameInfo.Scene = FrameInfo.Viewport?.GetScene();
        FrameInfo.Camera = FrameInfo.Viewport?.GetCullCamera()
            ?? FrameInfo.Viewport?.GetCamera();
        FrameInfo.ViewReferenceNode = FrameInfo.Camera?.Node;

        FrameInfo.Octree = FrameInfo.Scene?.Root.GetComponent<Octree>();
        if (FrameInfo.Octree is not null && FrameInfo.Scene is not null)
            FrameInfo.Scene.Root.GetOrCreateComponent<ReflectionProbeManager>();

        FrameInfo.Cameras = frameInfo.Cameras;

        return FrameInfo.Octree is not null && FrameInfo.Camera is not null;
    }

    public void SetRenderCameras(ReadOnlySpan<Camera?> cameras) => CameraProcessor.SetCameras(cameras);

    public void SetRenderCamera(Camera? camera)
    {
        Camera?[] cameras = [camera];
        CameraProcessor.SetCameras(cameras);
    }

    public virtual void Update()
    {
        // Collect occluders
        CurrentOcclusionBuffer = null;
        var frustum = FrameInfo.Camera!.GetFrustum();
        if (Settings.MaxOccluderTriangles > 0)
        {
            Occluders.Clear();
            var occluderQuery = new OccluderOctreeQuery(Occluders, frustum, FrameInfo.Camera.GetPrimaryViewMask());
            FrameInfo.Octree!.GetDrawables(occluderQuery);
            DrawableProcessor.ProcessOccluders(Occluders, Settings.OccluderSizeThreshold);

            if (DrawableProcessor.HasOccluders())
            {
                OcclusionBuffer ??= new OcclusionBuffer();
                var bufferSize = CalculateOcclusionBufferSize(Settings.OcclusionBufferSize, FrameInfo.Camera);
                OcclusionBuffer.SetSize(bufferSize.X, bufferSize.Y, Settings.ThreadedOcclusion);
                OcclusionBuffer.SetView(FrameInfo.Camera);

                DrawOccluders();
                if (OcclusionBuffer.GetNumTriangles() > 0)
                    CurrentOcclusionBuffer = OcclusionBuffer;
            }
        }

        // Collect visible drawables
        Drawables.Clear();
        if (CurrentOcclusionBuffer is not null)
        {
            var query = new OccludedFrustumOctreeQuery(Drawables, frustum,
                CurrentOcclusionBuffer, DrawableFlags.Geometry | DrawableFlags.Light,
                FrameInfo.Camera.GetPrimaryViewMask());
            FrameInfo.Octree!.GetDrawables(query);
        }
        else
        {
            var drawableQuery = new FrustumOctreeQuery(Drawables, frustum,
                DrawableFlags.Geometry | DrawableFlags.Light, FrameInfo.Camera.GetPrimaryViewMask());
            FrameInfo.Octree!.GetDrawables(drawableQuery);
        }

        // Process drawables
        OcclusionBuffer[]? occlusionSpan = CurrentOcclusionBuffer is not null
            ? [CurrentOcclusionBuffer]
            : null;
        DrawableProcessor.ProcessVisibleDrawables(Drawables, occlusionSpan);
        DrawableProcessor.ProcessLights(this);
        DrawableProcessor.ProcessForwardLighting();

        BatchCompositor.ComposeSceneBatches();
        if (Settings.EnableShadows)
            BatchCompositor.ComposeShadowBatches();
        if (Settings.IsDeferredLighting())
            BatchCompositor.ComposeLightVolumeBatches();
    }

    public void PrepareInstancingBuffer()
    {
        if (InstancingBuffer is null || !InstancingBuffer.IsEnabled)
            return;

        InstancingBuffer.Begin();

        var visibleLights = DrawableProcessor.GetLightProcessors();
        foreach (var sceneLight in visibleLights)
        {
            if (sceneLight is null)
                continue;
            foreach (var split in sceneLight.MutableSplits)
                BatchRenderer.PrepareInstancingBuffer(ref split.MutableShadowBatches);
        }

        foreach (var pass in Passes)
        {
            if (pass.IsEnabled())
                pass.PrepareInstancingBuffer(BatchRenderer);
        }

        InstancingBuffer.End();
    }

    public void PrepareDrawablesBeforeRendering() => DrawableProcessor.UpdateGeometries();

    public void RenderShadowMaps()
    {
        if (!Settings.EnableShadows || DrawQueue is null || RenderContext is null)
            return;

        using var renderScope = new RenderScope(RenderContext, "SceneProcessor::RenderShadowMaps");

        var lightsByShadowMap = DrawableProcessor.GetLightProcessorsByShadowMap();
        foreach (var sceneLight in lightsByShadowMap)
        {
            if (sceneLight is null)
                continue;
            using var renderScopeLight = new RenderScope(RenderContext, "Light 0x{0} '{1}'",
                sceneLight.Light.GetHashCode(), sceneLight.Light.Node?.Name ?? string.Empty);

            foreach (var split in sceneLight.Splits)
            {
                using var renderScopeSplit = new RenderScope(RenderContext, "Split #{0}", checked((uint)split.SplitIndex));

                if (RenderPipelineDebugger.IsSnapshotInProgress(Debugger))
                {
                    var passName = $"ShadowMap.[{split.Light.GetFullNameDebug()}].{split.SplitIndex}";
                    Debugger!.BeginPass(passName);
                }

                DrawQueue.Reset();
                BatchRenderer.RenderBatches(new BatchRenderingContext(DrawQueue, split), split.ShadowBatches);
                ShadowMapAllocator?.BeginShadowMapRendering(split.ShadowMap);
                RenderContext.Execute(DrawQueue);

                if (RenderPipelineDebugger.IsSnapshotInProgress(Debugger))
                    Debugger!.EndPass();
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
        if (DrawQueue is null || RenderContext is null)
            return;

        if (RenderPipelineDebugger.IsSnapshotInProgress(Debugger))
            Debugger!.BeginPass(debugName);

        using var renderScope = new RenderScope(RenderContext, debugName);

        DrawQueue.Reset();

        var ctx = new BatchRenderingContext(DrawQueue, camera)
        {
            InstanceMultiplier = instanceMultiplier,
            GlobalResources = globalResources.ToArray(),
            CameraParameters = cameraParameters.ToArray(),
            FrameParameters = frameParameters.ToArray()
        };

        var lightVolumeBatches = GetLightVolumeBatches();
        BatchRenderer.RenderLightVolumeBatches(ctx,
            System.Runtime.InteropServices.CollectionsMarshal.AsSpan((List<PipelineBatchByState>)lightVolumeBatches));

        RenderContext.Execute(DrawQueue);

        if (RenderPipelineDebugger.IsSnapshotInProgress(Debugger))
            Debugger!.EndPass();
    }

    public SceneProcessorSettings GetSettings() => Settings;

    public CommonFrameInfo GetFrameInfo() => FrameInfo;

    /// <summary>
    /// Takes pass object from BatchStateCreateContext.
    /// Returns user-configured pass (inherited from BatchCompositorPass) if possible.
    /// Returns null if internal pass from BatchCompositor itself (e.g. shadow or light volume pass).
    /// </summary>
    public BatchCompositorPass? GetUserPass(Object? pass)
    {
        System.Diagnostics.Debug.Assert(pass is not null);
        if (ReferenceEquals(pass, BatchCompositor))
            return null;
        return pass as BatchCompositorPass;
    }

    public IReadOnlyList<PipelineBatchByState> GetLightVolumeBatches() => BatchCompositor.GetLightVolumeBatches();

    public CameraProcessor GetCameraProcessor() => CameraProcessor;

    public PipelineStateBuilder GetPipelineStateBuilder() => PipelineStateBuilder;

    public DrawableProcessor GetDrawableProcessor() => DrawableProcessor;

    public BatchCompositor GetBatchCompositor() => BatchCompositor;

    public BatchRenderer GetBatchRenderer() => BatchRenderer;

    protected virtual void DrawOccluders()
    {
        if (OcclusionBuffer is null)
            return;

        var activeOccluders = DrawableProcessor.GetOccluders();

        OcclusionBuffer.SetMaxTriangles(Settings.MaxOccluderTriangles);
        OcclusionBuffer.Clear();

        if (!OcclusionBuffer.IsThreaded())
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
                    if (!OcclusionBuffer.IsVisible(occluder.GetWorldBoundingBox()))
                        continue;
                }

                // Check for running out of triangles
                var success = occluder.DrawOcclusion(OcclusionBuffer);
                // Draw triangles submitted by this occluder
                OcclusionBuffer.DrawTriangles();
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
                if (activeOccluders[i].Drawable is { } drawable && !drawable.DrawOcclusion(OcclusionBuffer))
                    break;
            }

            OcclusionBuffer.DrawTriangles();
        }

        // Finally build the depth mip levels
        OcclusionBuffer.BuildDepthHierarchy();
    }

    private void RenderBatchesInternal<T>(string debugName, Camera camera,
        PipelineBatchGroup<T> batchGroup,
        ReadOnlySpan<ShaderResourceDesc> globalResources,
        ReadOnlySpan<ShaderParameterDesc> cameraParameters,
        ReadOnlySpan<ShaderParameterDesc> frameParameters,
        uint instanceMultiplier)
    {
        if (DrawQueue is null || RenderContext is null)
            return;

        if (RenderPipelineDebugger.IsSnapshotInProgress(Debugger))
            Debugger!.BeginPass(debugName);

        using var renderScope = new RenderScope(RenderContext, debugName);

        DrawQueue.Reset();

        var needClipping = camera.GetUseClipping() || FrameInfo.Cameras.Second is not null;
        DrawQueue.SetClipPlaneMask(needClipping ? 1u << 0 : 0u);

        var ctx = new BatchRenderingContext(DrawQueue, camera)
        {
            InstanceMultiplier = instanceMultiplier,
            GlobalResources = globalResources.ToArray(),
            CameraParameters = cameraParameters.ToArray(),
            FrameParameters = frameParameters.ToArray()
        };

        if (batchGroup.ScissorRect != IntRect.Zero)
            DrawQueue.SetScissorRect(batchGroup.ScissorRect);

        if (typeof(T) == typeof(PipelineBatchByState))
            BatchRenderer.RenderBatches(ctx, (PipelineBatchGroup<PipelineBatchByState>)(object)batchGroup);
        else if (typeof(T) == typeof(PipelineBatchBackToFront))
            BatchRenderer.RenderBatches(ctx, (PipelineBatchGroup<PipelineBatchBackToFront>)(object)batchGroup);

        RenderContext.Execute(DrawQueue);

        if (RenderPipelineDebugger.IsSnapshotInProgress(Debugger))
            Debugger!.EndPass();
    }

    private void OnUpdateBegin(CommonFrameInfo frameInfo)
    {
        FrameInfo.FrameNumber = frameInfo.FrameNumber;
        FrameInfo.TimeStep = frameInfo.TimeStep;

        Occluders.Clear();
        Drawables.Clear();

        CameraProcessor.OnUpdateBegin(FrameInfo);
        DrawableProcessor.OnUpdateBegin(FrameInfo);
        PipelineStateBuilder.UpdateFrameSettings(RenderPipeline.IsLinearColorSpace);
    }

    private void OnRenderBegin(CommonFrameInfo frameInfo) => CameraProcessor.OnRenderBegin(frameInfo);

    private void OnRenderEnd(CommonFrameInfo frameInfo) => CameraProcessor.OnRenderEnd(frameInfo);

    public bool IsLightShadowed(Light light)
    {
        var shadowsEnabled = Settings.EnableShadows
            && light.GetCastShadows()
            && light.GetLightImportance() != LightImportance.NotImportant
            && light.GetShadowIntensity() < 1.0f;

        if (!shadowsEnabled)
            return false;

        if (light.GetShadowDistance() > 0.0f && light.GetDistance() > light.GetShadowDistance())
            return false;

        return true;
    }

    public int GetShadowMapSize(Light light, int numActiveSplits)
    {
        _ = numActiveSplits;
        var parameters = light.GetShadowFocus();
        var shadowResolutionScale = light.GetShadowResolution();
        var pageSize = ShadowMapAllocator?.Settings.ShadowAtlasPageSize ?? 2048;
        var maxShadowSize = GetMaxShadowSplitSize(Settings, pageSize, light);

        var lightSizeInPixels = GetLightSizeInPixels(light, FrameInfo);
        var baseShadowSize = parameters.AutoSize ? lightSizeInPixels : maxShadowSize;
        var shadowSize = checked((int)MathDefs.NextPowerOfTwo((uint)(baseShadowSize * shadowResolutionScale)));

        return Math.Clamp(shadowSize, ShadowMinPixels, maxShadowSize);
    }

    public ShadowMapRegion AllocateTransientShadowMap(Vector2i size) =>
        ShadowMapAllocator?.AllocateShadowMap(size) ?? default;

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

    private static int GetMaxShadowSplitSize(SceneProcessorSettings settings, int pageSize, Light light) =>
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
