namespace RedBolt;

/// <summary>
/// Flags related to geometry rendering.
/// Use old enum for simplified flag manipulation, avoid FlagSet for quick reset.
/// </summary>
[Flags]
public enum GeometryRenderFlag : byte
{
    VisibleInCullCamera = 1 << 0,
    Lit = 1 << 1,
    ForwardLit = 1 << 2
}

public struct SortedOccluder : IComparable<SortedOccluder>
{
    public float SortValue;
    public Drawable? Drawable;

    public readonly int CompareTo(SortedOccluder other) => SortValue.CompareTo(other.SortValue);
}

/// <summary>
/// Reference to SourceBatch of Drawable geometry, with resolved material passes.
/// </summary>
public struct GeometryBatch
{
    public static GeometryBatch Deferred(Drawable drawable, int sourceBatchIndex, Pass deferredPass, object? userData = null)
    {
        GeometryBatch batch = default;
        batch.Drawable = drawable;
        batch.SourceBatchIndex = sourceBatchIndex;
        batch.DeferredPass = deferredPass;
        batch.UserData = userData;
        return batch;
    }

    public static GeometryBatch Forward(Drawable drawable, int sourceBatchIndex,
        Pass unlitBasePass, Pass? litBasePass, Pass? lightPass, object? userData = null)
    {
        GeometryBatch batch = default;
        batch.Drawable = drawable;
        batch.SourceBatchIndex = sourceBatchIndex;
        batch.UnlitBasePass = unlitBasePass;
        batch.LitBasePass = litBasePass;
        batch.LightPass = lightPass;
        batch.UserData = userData;
        return batch;
    }

    public Drawable? Drawable;
    public int SourceBatchIndex;
    public object? UserData;

    /// <summary>
    /// If deferred pass is present, unlit base, lit base and light passes are ignored.
    /// </summary>
    public Pass? DeferredPass;
    /// <summary>
    /// Unlit base pass (no per-pixel lighting, optional ambient lighting).
    /// </summary>
    public Pass? UnlitBasePass;
    /// <summary>
    /// Lit base pass (per-pixel lighting from one light source and ambient lighting).
    /// </summary>
    public Pass? LitBasePass;
    /// <summary>
    /// Additive light pass (per-pixel lighting from one light source).
    /// </summary>
    public Pass? LightPass;
}

/// <summary>
/// Interface of scene pass used by drawable processor.
///
/// There are 4 types of batches:
/// 1) Deferred Pass: if present, used unconditionally. Unlit base, lit base and light passes are ignored.
/// 2) Unlit Base + Lit Base + Light:
///    Batch is rendered in N passes if the first per-pixel light can be rendered together with ambient.
///    Batch is rendered in N + 1 passes otherwise.
///    N is equal to the number per-pixel lights affecting object.
/// 3) Unlit Base + Light: batch is rendered with per-pixel forward lighting in N + 1 passes.
/// 4) Unlit Base: batch is rendered once, per-pixel lighting is not applied.
///
/// Other pass combinations are invalid.
/// </summary>
public class DrawableProcessorPass : Object
{
    public readonly record struct AddBatchResult(bool Added, bool ForwardLitAdded);

    public DrawableProcessorPass(IRenderPipelineInterface renderPipeline, DrawableProcessorPassFlag flags,
        int deferredPassIndex, int unlitBasePassIndex, int litBasePassIndex, int lightPassIndex)
    {
        RenderPipeline = renderPipeline;
        _flags = flags;
        _useBatchCallback = IsFlagSet(DrawableProcessorPassFlag.BatchCallback);
        _deferredPassIndex = deferredPassIndex;
        _unlitBasePassIndex = unlitBasePassIndex;
        _litBasePassIndex = litBasePassIndex;
        _lightPassIndex = lightPassIndex;

        renderPipeline.UpdateBegin += OnUpdateBegin;
    }

    public override void Dispose()
    {
        RenderPipeline.UpdateBegin -= OnUpdateBegin;
        base.Dispose();
    }

    public bool IsEnabled
    {
        get => _enabled;
        set => _enabled = value;
    }

    /// <summary>
    /// Custom callback for adding batches.
    /// </summary>
    public virtual AddBatchResult AddCustomBatch(int threadIndex, Drawable drawable, int sourceBatchIndex, Technique technique) => default;

    public AddBatchResult AddBatch(int threadIndex, Drawable drawable, int sourceBatchIndex, Technique technique)
    {
        if (_useBatchCallback)
            return AddCustomBatch(threadIndex, drawable, sourceBatchIndex, technique);

        var deferredPass = technique.GetPass(_deferredPassIndex);
        if (deferredPass is not null)
        {
            GeometryBatches.PushBack(threadIndex, GeometryBatch.Deferred(drawable, sourceBatchIndex, deferredPass));
            return new AddBatchResult(true, false);
        }

        var unlitBasePass = technique.GetPass(_unlitBasePassIndex);
        var lightPass = technique.GetPass(_lightPassIndex);
        var litBasePass = lightPass is not null ? technique.GetPass(_litBasePassIndex) : null;

        if (unlitBasePass is null)
            return default;

        GeometryBatches.PushBack(threadIndex,
            GeometryBatch.Forward(drawable, sourceBatchIndex, unlitBasePass, litBasePass, lightPass));
        return new AddBatchResult(true, lightPass is not null);
    }

    public DrawableProcessorPassFlag Flags => _flags;

    public bool IsFlagSet(DrawableProcessorPassFlag flag) => (_flags & flag) != 0;

    public bool HasLightPass => _lightPassIndex != int.MaxValue;

    /// <summary>
    /// RenderPipeline callbacks
    /// </summary>
    protected virtual void OnUpdateBegin(CommonFrameInfo frameInfo)
    {
        GeometryBatches.Clear();
        LinearColorSpace = RenderPipeline.IsLinearColorSpace;
    }

    private readonly DrawableProcessorPassFlag _flags;
    private readonly bool _useBatchCallback;

    private readonly int _deferredPassIndex;
    private readonly int _unlitBasePassIndex;
    private readonly int _litBasePassIndex;
    private readonly int _lightPassIndex;

    private bool _enabled = true;

    protected readonly IRenderPipelineInterface RenderPipeline;
    protected readonly WorkQueueVector<GeometryBatch> GeometryBatches = new();

    protected bool LinearColorSpace;
}

/// <summary>Utility used to update and process visible or shadow caster Drawables.</summary>
public class DrawableProcessor : Object
{
    private readonly IRenderPipelineInterface _renderPipeline;
    private readonly WorkQueue _workQueue;
    private readonly List<DrawableProcessorPass> _allPasses = [];
    private readonly List<DrawableProcessorPass> _passes = [];
    private readonly List<Light?> _lights = [];
    private readonly List<LightProcessor?> _lightProcessors = [];
    private readonly List<LightProcessor?> _lightProcessorsByShadowMapSize = [];
    private readonly List<LightProcessor?> _lightProcessorsByShadowMapTexture = [];
    private readonly WorkQueueVector<Drawable> _geometries = new();
    private readonly List<SortedOccluder> _sortedOccluders = [];
    private LightAccumulator[] _geometryLighting = [];
    private GeometryRenderFlag[] _geometryRenderFlags = [];
    private FloatRange[] _geometryZRanges = [];
    private int[] _isDrawableUpdated = [];
    private readonly WorkQueueVector<Drawable> _queuedDrawableUpdates = new();
    private readonly WorkQueueVector<Drawable> _threadedGeometryUpdates = new();
    private readonly WorkQueueVector<Drawable> _nonThreadedGeometryUpdates = new();
    private readonly WorkQueueVector<Light> _lightsTemp = new();
    private FloatRange[] _sceneZRangeTemp = [];
    private readonly LightProcessorCache _lightProcessorCache = new();
    private readonly List<LightDataForAccumulator> _lightDataForAccumulator = [];
    private Material? _defaultMaterial;
    private MaterialQuality _materialQuality = MaterialQuality.High;
    private GlobalIllumination? _gi;
    private int _numShadowedLights;
    private Vector3f _cullCameraZAxis;
    private Vector3f _cullCameraZAxisAbs;
    private Matrix3x4 _cullCameraViewMatrix;
    private DrawableProcessorSettings _settings = new();
    private FrameInfo _frameInfo = new();
    private FloatRange _sceneZRange;

    public DrawableProcessor(IRenderPipelineInterface renderPipeline)
    {
        _renderPipeline = renderPipeline;
        _workQueue = GetSubsystem<WorkQueue>()!;
        _defaultMaterial = GetSubsystem<Renderer>()!.GetDefaultMaterial();
        _renderPipeline.CollectStatistics += OnCollectStatistics;
    }

    public override void Dispose()
    {
        _renderPipeline.CollectStatistics -= OnCollectStatistics;
        _lightProcessorCache.Dispose();
        base.Dispose();
    }

    public void SetPasses(IReadOnlyList<DrawableProcessorPass> passes)
    {
        _allPasses.Clear();
        _allPasses.AddRange(passes);
    }

    public void SetSettings(DrawableProcessorSettings settings)
    {
        _settings = settings;
        _lightProcessorCache.SetSettings(settings.LightProcessorCache);
    }

    public FrameInfo FrameInfo => _frameInfo;

    public DrawableProcessorSettings Settings => _settings;

    public bool HasOccluders => _sortedOccluders.Count != 0;

    public IReadOnlyList<SortedOccluder> Occluders => _sortedOccluders;

    public IEnumerable<Drawable> Geometries => _geometries;

    public FloatRange SceneZRange => _sceneZRange;

    public IReadOnlyList<Light?> Lights => _lights;

    public Light? GetLight(int lightIndex) => _lights[lightIndex];

    public IReadOnlyList<LightProcessor?> LightProcessors => _lightProcessors;

    public IReadOnlyList<LightProcessor?> LightProcessorsByShadowMap => _lightProcessorsByShadowMapTexture;

    public LightProcessor? GetLightProcessor(int lightIndex) => _lightProcessors[lightIndex];

    public ref readonly LightAccumulator GetGeometryLighting(int drawableIndex) => ref _geometryLighting[drawableIndex];

    public GeometryRenderFlag GetGeometryRenderFlags(int drawableIndex) => _geometryRenderFlags[drawableIndex];

    public FloatRange GetGeometryZRange(int drawableIndex) => _geometryZRanges[drawableIndex];

    public void OnUpdateBegin(FrameInfo frameInfo)
    {
        _passes.Clear();
        foreach (var pass in _allPasses)
        {
            if (pass.IsEnabled)
                _passes.Add(pass);
        }

        // Initialize frame constants
        _frameInfo = frameInfo;
        var numDrawables = _frameInfo.Octree!.GetAllDrawables().Count;
        _cullCameraViewMatrix = _frameInfo.Camera!.GetView();
        _cullCameraZAxis = new Vector3f(
            _cullCameraViewMatrix.M20,
            _cullCameraViewMatrix.M21,
            _cullCameraViewMatrix.M22);
        _cullCameraZAxisAbs = new Vector3f(
            MathF.Abs(_cullCameraZAxis.X),
            MathF.Abs(_cullCameraZAxis.Y),
            MathF.Abs(_cullCameraZAxis.Z));

        _materialQuality = _settings.MaterialQuality;
        if ((_frameInfo.Camera.GetViewOverrideFlags() & ViewOverride.LowMaterialQuality) != 0)
            _materialQuality = MaterialQuality.Low;

        _gi = _frameInfo.Scene!.Root.GetComponent<GlobalIllumination>();

        // Clean temporary containers
        Array.Resize(ref _sceneZRangeTemp, WorkQueue.GetThreadIndexCount());
        Array.Clear(_sceneZRangeTemp);
        _sceneZRange = default;

        Array.Resize(ref _isDrawableUpdated, numDrawables);
        Array.Clear(_isDrawableUpdated);
        Array.Resize(ref _geometryRenderFlags, numDrawables);
        Array.Clear(_geometryRenderFlags);
        Array.Resize(ref _geometryZRanges, numDrawables);
        Array.Resize(ref _geometryLighting, numDrawables);

        _sortedOccluders.Clear();
        _geometries.Clear();
        _threadedGeometryUpdates.Clear();
        _nonThreadedGeometryUpdates.Clear();
        _lightsTemp.Clear();
        _queuedDrawableUpdates.Clear();

        // Update caches
        _lightProcessorCache.Update(_frameInfo.TimeStep);
    }

    public void ProcessVisibleDrawables(IReadOnlyList<Drawable> drawables, OcclusionBuffer[]? occlusionBuffers)
    {
        WorkQueue.ForEachParallel(_workQueue, drawables, (_, drawable) =>
        {
            // do occlusion test for occludees if possible
            if (occlusionBuffers is { Length: > 0 } && drawable.IsOccludee())
            {
                // check for visible
                var anyPass = false;
                foreach (var o in occlusionBuffers)
                    anyPass |= o.IsVisible(drawable.GetWorldBoundingBox());

                if (!anyPass)
                    return;
            }

            ProcessVisibleDrawable(drawable);
        });

        // Sort lights by component ID for stability
        _lights.Clear();
        foreach (var light in _lightsTemp)
            _lights.Add(light);
        _lights.Sort(static (lhs, rhs) =>
        {
            if (lhs is null || rhs is null)
                return 0;
            var lhsNegative = lhs.IsNegative();
            var rhsNegative = rhs.IsNegative();
            if (lhsNegative != rhsNegative)
                return lhsNegative.CompareTo(rhsNegative);
            return lhs.Id.CompareTo(rhs.Id);
        });

        _lightProcessors.Clear();
        foreach (var light in _lights)
            _lightProcessors.Add(light is null ? null : _lightProcessorCache.GetLightProcessor(light));

        // Cook lights for forward light evaluation on CPU
        _lightDataForAccumulator.Clear();
        foreach (var light in _lights)
            _lightDataForAccumulator.Add(light is null ? default : new LightDataForAccumulator(light));

        // Expand scene Z range so it's never too small
        foreach (var range in _sceneZRangeTemp)
            _sceneZRange |= range;

        const float minSceneZRange = 1.0f;
        if (_sceneZRange.IsValid() && _sceneZRange.Second - _sceneZRange.First < minSceneZRange)
            _sceneZRange.Second = _sceneZRange.First + minSceneZRange;
    }

    public void ProcessLights(LightProcessorCallback callback)
    {
        foreach (var lightProcessor in _lightProcessors)
            lightProcessor?.BeginUpdate(this, callback);

        WorkQueue.ForEachParallel(_workQueue, _lightProcessors, (_, lightProcessor) =>
        {
            lightProcessor?.Update(this, callback);
        });

        SortLightProcessorsByShadowMapSize();

        _numShadowedLights = 0;
        foreach (var lightProcessor in _lightProcessorsByShadowMapSize)
        {
            if (lightProcessor is null)
                continue;
            lightProcessor.EndUpdate(this, callback, checked((int)_settings.PcfKernelSize));
            if (lightProcessor.HasShadow)
                ++_numShadowedLights;
        }

        SortLightProcessorsByShadowMapTexture();

        ProcessShadowCasters();
    }

    public void ProcessForwardLighting()
    {
        var hasForwardLights = false;
        for (var i = 0; i < _lightProcessors.Count; ++i)
        {
            var lightProcessor = _lightProcessors[i];
            if (lightProcessor is not null && lightProcessor.HasForwardLitGeometries)
            {
                ProcessForwardLightingForLight((uint)i, lightProcessor.LitGeometries);
                hasForwardLights = true;
            }
        }

        if (hasForwardLights)
            FinalizeForwardLighting();
    }

    public void UpdateGeometries()
    {
        // Update in worker threads
        var list = new List<Drawable>();
        _threadedGeometryUpdates.CopyTo(list);
        WorkQueue.ForEachParallel(_workQueue, list, (_, drawable) =>
        {
            if (drawable.GetUpdateGeometryType() == UpdateGeometryType.MainThread)
                _nonThreadedGeometryUpdates.Insert(drawable);
            else
                drawable.UpdateGeometry(_frameInfo);
        });

        // Update in main thread
        foreach (var drawable in _nonThreadedGeometryUpdates)
            drawable.UpdateGeometry(_frameInfo);

        _threadedGeometryUpdates.Clear();
        _nonThreadedGeometryUpdates.Clear();
    }

    private void ProcessForwardLightingForLight(uint lightIndex, IReadOnlyList<Drawable> litGeometries)
    {
        if (lightIndex >= (uint)_lights.Count)
            return;

        var light = _lights[(int)lightIndex];
        if (light is null)
            return;

        var lightType = light.GetLightType();
        var lightIntensityPenalty = 1.0f / light.GetIntensityDivisor();
        var hasShadow = _lightProcessors[(int)lightIndex]?.HasShadow == true;
        var isNegative = light.IsNegative();
        var lightImportance = hasShadow ? LightImportance.Important : light.GetLightImportance();

        var ctx = new LightAccumulatorContext
        {
            MaxVertexLights = checked((int)_settings.MaxVertexLights),
            MaxPixelLights = checked((int)_settings.MaxPixelLights),
            Lights = _lightDataForAccumulator
        };

        foreach (var geometry in litGeometries)
        {
            var drawableIndex = checked((int)geometry.GetDrawableIndex());

            // Directional light doesn't filter out e.g. deferred lit geometries for shadow focusing
            if (lightType == LightType.Directional)
            {
                var isForwardLit = (GetGeometryRenderFlags(drawableIndex) & GeometryRenderFlag.ForwardLit) != 0;
                if (!isForwardLit)
                    continue;
            }

            var distance = Math.Max(light.GetDistanceTo(geometry), MathDefs.LargeEpsilon);
            var penalty = GetDrawableLightPenalty(distance * lightIntensityPenalty,
                isNegative, lightImportance, lightType);
            ref var lighting = ref _geometryLighting[checked((int)drawableIndex)];
            lighting.AccumulateLight(ctx, geometry, lightImportance, checked((int)lightIndex), penalty);
        }
    }

    private void FinalizeForwardLighting()
    {
        foreach (var drawable in _geometries)
        {
            if (drawable is null)
                continue;
            var drawableIndex = checked((int)drawable.GetDrawableIndex());
            var flags = GetGeometryRenderFlags(drawableIndex);
            if ((flags & GeometryRenderFlag.ForwardLit) != 0)
            {
                ref var lightAccumulator = ref _geometryLighting[checked((int)drawableIndex)];
                lightAccumulator.Cook();
            }
        }
    }

    private void SortLightProcessorsByShadowMapSize()
    {
        _lightProcessorsByShadowMapSize.Clear();
        _lightProcessorsByShadowMapSize.AddRange(_lightProcessors);
        _lightProcessorsByShadowMapSize.Sort(static (lhs, rhs) =>
        {
            if (lhs is null || rhs is null)
                return 0;
            var lhsSize = lhs.ShadowMapSize;
            var rhsSize = rhs.ShadowMapSize;
            var lhsLen = (long)lhsSize.X * lhsSize.X + (long)lhsSize.Y * lhsSize.Y;
            var rhsLen = (long)rhsSize.X * rhsSize.X + (long)rhsSize.Y * rhsSize.Y;
            if (lhsLen != rhsLen)
                return rhsLen.CompareTo(lhsLen);
            return lhs.Light.Id.CompareTo(rhs.Light.Id);
        });
    }

    private void SortLightProcessorsByShadowMapTexture()
    {
        _lightProcessorsByShadowMapTexture.Clear();
        _lightProcessorsByShadowMapTexture.AddRange(_lightProcessors);
        _lightProcessorsByShadowMapTexture.Sort(static (lhs, rhs) =>
        {
            if (lhs is null || rhs is null)
                return 0;
            return Comparer<Texture2D?>.Default.Compare(lhs.ShadowMap.Texture, rhs.ShadowMap.Texture);
        });
    }

    private void CheckMaterialForAuxiliaryRenderSurfaces(Material? material)
    {
        // Skip if already checked or not main viewport
        if (material is null || material.GetAuxViewFrameNumber() == _frameInfo.FrameNumber || _frameInfo.RenderTarget is not null)
            return;

        var textures = material.EnumerateTextures();
        while (textures.MoveNext())
        {
            // Skip if not render targets
            var texture = textures.Current.Value.Texture;
            if (!texture.IsRenderTarget())
                continue;

            // Have to check cube & 2D textures separately
            if (texture is Texture2D texture2D)
            {
                var target = texture2D.GetRenderSurface();
                if (target is not null && target.GetUpdateMode() == RenderSurfaceUpdateMode.UpdateVisible)
                    target.QueueUpdate();
            }
            else if (texture is TextureCube textureCube)
            {
                for (var j = CubeMapFace.PositiveX; j < CubeMapFace.MaxCubeMapFaces; ++j)
                {
                    var target = textureCube.GetRenderSurface(j);
                    if (target is not null && target.GetUpdateMode() == RenderSurfaceUpdateMode.UpdateVisible)
                        target.QueueUpdate();
                }
            }
        }

        // Flag as processed so we can early-out next time we come across this material on the same frame
        material.MarkForAuxView(_frameInfo.FrameNumber);
    }

    private void ProcessVisibleDrawable(Drawable drawable)
    {
        var drawableIndex = drawable.GetDrawableIndex();
        var threadIndex = WorkQueue.GetThreadIndex();

        drawable.UpdateBatches(_frameInfo);

        Interlocked.Exchange(ref _isDrawableUpdated[(int)drawableIndex], 1);

        // Skip if too far
        var maxDistance = drawable.GetDrawDistance();
        if (maxDistance > 0.0f && drawable.GetDistance() > maxDistance)
            return;

        drawable.MarkInView(_frameInfo);

        // For geometries, find zone, clear lights and calculate view space Z range
        if ((drawable.GetDrawableFlags() & DrawableFlags.Geometry) != 0)
        {
            var boundingBox = drawable.GetWorldBoundingBox();
            var zRange = CalculateBoundingBoxZRange(boundingBox);

            // Update zone
            UpdateDrawableZone(boundingBox, drawable);
            UpdateDrawableReflection(boundingBox, drawable);

            // Do not add "infinite" objects like skybox to prevent shadow map focusing behaving erroneously
            if (!zRange.IsValid())
                _geometryZRanges[checked((int)drawableIndex)] = new FloatRange(MathDefs.LargeValue, MathDefs.LargeValue);
            else
            {
                _geometryZRanges[checked((int)drawableIndex)] = zRange;
                _sceneZRangeTemp[checked((int)threadIndex)] |= zRange;
            }

            // Always reset lights in case of material change
            ref var lightAccumulator = ref _geometryLighting[checked((int)drawableIndex)];
            lightAccumulator.ResetLights();

            // Collect batches
            var isForwardLit = false;
            var needAmbient = false;

            var sourceBatches = drawable.GetBatches();
            for (var sourceBatchIndex = 0; sourceBatchIndex < sourceBatches.Count; ++sourceBatchIndex)
            {
                var sourceBatch = sourceBatches[sourceBatchIndex];
                if (sourceBatch.Geometry is null || sourceBatch.Geometry.IsEmpty || sourceBatch.NumWorldTransforms == 0)
                    continue;

                // Find current technique
                var material = sourceBatch.Material ?? _defaultMaterial;
                if (material is null)
                    continue;
                var technique = material.FindTechnique(drawable, _materialQuality);
                if (technique is null)
                    continue;

                // Check for aux views
                CheckMaterialForAuxiliaryRenderSurfaces(sourceBatch.Material);

                // Update scene passes
                foreach (var pass in _passes)
                {
                    var result = pass.AddBatch(threadIndex, drawable, sourceBatchIndex, technique);
                    if (result.ForwardLitAdded)
                        isForwardLit = true;
                    if (result.Added && pass.IsFlagSet(DrawableProcessorPassFlag.HasAmbientLighting))
                        needAmbient = true;
                }
            }

            // Process lighting
            if (needAmbient)
            {
                var giType = drawable.GetGlobalIlluminationType();

                // Reset SH from GI if possible/needed, reset to zero otherwise
                if (_gi is not null && giType >= GlobalIlluminationType.BlendLightProbes)
                {
                    ref var hint = ref drawable.GetMutableLightProbeTetrahedronHint();
                    var samplePosition = boundingBox.Center;
                    lightAccumulator.SphericalHarmonics = _gi.SampleAmbientSH(samplePosition, ref hint);
                }
                else
                    lightAccumulator.SphericalHarmonics = default;

                // Apply ambient from Zone
                ref var cachedZone = ref drawable.GetMutableCachedZone();
                if (cachedZone.Zone is { } zone)
                {
                    if (!zone.IsBackgroundStatic())
                        lightAccumulator.SphericalHarmonics += zone.GetAmbientAndBackgroundLighting();
                    else
                        lightAccumulator.SphericalHarmonics += zone.GetAmbientLighting();

                    lightAccumulator.ReflectionProbes[0] = zone.GetReflectionProbe();
                    lightAccumulator.ReflectionProbes[1] = lightAccumulator.ReflectionProbes[0];
                    lightAccumulator.ReflectionProbesBlendFactor = 0.0f;

                    // Apply reflection probe
                    var reflectionMode = drawable.GetReflectionMode();
                    if (reflectionMode != ReflectionMode.Zone)
                    {
                        ref var reflection = ref drawable.GetMutableCachedReflection();
                        var probe0 = reflection.Probes[0];
                        var probe1 = reflection.Probes[1];

                        if (probe0.Data is not null)
                        {
                            lightAccumulator.ReflectionProbes[0] = probe0.Data;

                            if (reflectionMode >= ReflectionMode.BlendProbes && probe1.Data is not null)
                            {
                                lightAccumulator.ReflectionProbes[1] = probe1.Data;
                                lightAccumulator.ReflectionProbesBlendFactor = probe0.Priority != probe1.Priority
                                    ? 1.0f - probe0.Volume
                                    : probe1.Volume / (probe0.Volume + probe1.Volume);
                            }
                            if (reflectionMode == ReflectionMode.BlendProbesAndZone && probe1.Data is null)
                                lightAccumulator.ReflectionProbesBlendFactor = 1.0f - probe0.Volume;
                        }
                    }
                }
            }

            // Store geometry
            _geometries.PushBack(threadIndex, drawable);

            // Update flags
            var flag = GeometryRenderFlag.VisibleInCullCamera;
            if (needAmbient)
                flag |= GeometryRenderFlag.Lit;
            if (isForwardLit)
                flag |= GeometryRenderFlag.ForwardLit;
            _geometryRenderFlags[checked((int)drawableIndex)] = flag;

            // Queue geometry update
            QueueDrawableGeometryUpdate(threadIndex, drawable);
        }
        else if ((drawable.GetDrawableFlags() & DrawableFlags.Light) != 0)
        {
            var light = (Light)drawable;
            var lightColor = light.GetEffectiveColor();

            // Skip lights with zero brightness or black color, skip baked lights too
            if (!lightColor.Equals(Color.Black) && light.GetLightMaskEffective() != 0)
                _lightsTemp.Insert(light);
        }
    }

    private FloatRange CalculateBoundingBoxZRange(BoundingBox boundingBox)
    {
        var center = boundingBox.Center;
        var edge = boundingBox.Size * 0.5f;

        // Ignore "infinite" objects
        if (edge.LengthSquared >= MathDefs.LargeValue * MathDefs.LargeValue)
            return default;

        var viewCenterZ = _cullCameraZAxis.DotProduct(center) + _cullCameraViewMatrix.M23;
        var viewEdgeZ = _cullCameraZAxisAbs.DotProduct(edge);
        var minZ = viewCenterZ - viewEdgeZ;
        var maxZ = viewCenterZ + viewEdgeZ;

        return new FloatRange(minZ, maxZ);
    }

    /// <summary>
    /// Calculate light penalty for drawable for given absolute light penalty and light settings.
    /// Order of penalties, from lower to higher:
    /// -2:      Important directional lights;
    /// -1:      Important point and spot lights;
    ///  0 .. 2: Automatic lights;
    ///  3 .. 5: Not important lights.
    /// </summary>
    private static float GetDrawableLightPenalty(float intensityPenalty, bool isNegative,
        LightImportance importance, LightType type)
    {
        switch (importance)
        {
            case LightImportance.Important:
                return type == LightType.Directional && !isNegative ? -2 : -1;
            case LightImportance.Auto:
                return intensityPenalty <= 1.0f ? intensityPenalty : 2.0f - 1.0f / intensityPenalty;
            case LightImportance.NotImportant:
                return intensityPenalty <= 1.0f ? 3.0f + intensityPenalty : 5.0f - 1.0f / intensityPenalty;
            default:
                return MathDefs.LargeValue;
        }
    }

    public void PreprocessShadowCasters(List<Drawable> shadowCasters,
        IReadOnlyList<Drawable> candidates, FloatRange frustumSubRange, Light light, Camera shadowCamera)
    {
        shadowCasters.Clear();

        var cullCamera = _frameInfo.Camera;
        if (cullCamera is null)
            return;

        var shadowCameraFrustum = shadowCamera.GetFrustum();
        var worldToLightSpace = shadowCamera.GetView();
        var lightType = light.GetLightType();

        // Convert frustum (or sub-frustum) to shadow camera space
        var splitZRange = lightType != LightType.Directional ? _sceneZRange : _sceneZRange & frustumSubRange;
        var frustum = cullCamera.GetSplitFrustum(splitZRange.First, splitZRange.Second);
        var lightSpaceFrustum = frustum.Transformed(worldToLightSpace);
        var lightSpaceFrustumBoundingBox = new BoundingBox(lightSpaceFrustum);

        // Check for degenerate split frustum: in that case there is no need to get shadow casters
        if (lightSpaceFrustum.Vertices[0] == lightSpaceFrustum.Vertices[4])
            return;

        foreach (var drawable in candidates)
        {
            // For point light, check that this drawable is inside the split shadow camera frustum
            if (lightType == LightType.Point
                && shadowCameraFrustum.IsInsideFast(drawable.GetWorldBoundingBox()) == Intersection.Outside)
                continue;

            // Queue shadow caster if it's visible
            var lightSpaceBoundingBox = drawable.GetWorldBoundingBox().Transformed(worldToLightSpace);
            var isDrawableVisible = (GetGeometryRenderFlags(checked((int)drawable.GetDrawableIndex()))
                & GeometryRenderFlag.VisibleInCullCamera) != 0;
            if (isDrawableVisible
                || IsShadowCasterVisible(lightSpaceBoundingBox, shadowCamera, lightSpaceFrustum, lightSpaceFrustumBoundingBox))
            {
                QueueDrawableUpdate(drawable);
                shadowCasters.Add(drawable);
            }
        }
    }

    public void QueueDrawableUpdate(Drawable drawable)
    {
        var drawableIndex = drawable.GetDrawableIndex();
        if (Interlocked.Exchange(ref _isDrawableUpdated[(int)drawableIndex], 1) != 0)
            return;
        _queuedDrawableUpdates.Insert(drawable);
    }

    public void ProcessShadowCasters()
    {
        var list = new List<Drawable>();
        _queuedDrawableUpdates.CopyTo(list);
        WorkQueue.ForEachParallel(_workQueue, list, (_, drawable) => ProcessQueuedDrawable(drawable));

        _queuedDrawableUpdates.Clear();
    }

    public void ProcessQueuedDrawable(Drawable drawable)
    {
        drawable.UpdateBatches(_frameInfo);
        drawable.MarkInView(_frameInfo);

        var boundingBox = drawable.GetWorldBoundingBox();
        UpdateDrawableZone(boundingBox, drawable);
        QueueDrawableGeometryUpdate(WorkQueue.GetThreadIndex(), drawable);
    }

    public void UpdateDrawableZone(BoundingBox boundingBox, Drawable drawable)
    {
        var drawableCenter = boundingBox.Center;
        ref var cachedZone = ref drawable.GetMutableCachedZone();
        var drawableCacheDistanceSquared = (cachedZone.CachePosition - drawableCenter).LengthSquared;

        // Force update if bounding box is invalid
        var forcedUpdate = !float.IsFinite(drawableCacheDistanceSquared);
        // TODO: Do we want strict compare here too?
        if (forcedUpdate || drawableCacheDistanceSquared >= cachedZone.CacheInvalidationDistanceSquared)
        {
            if (_frameInfo.Octree is not null)
            {
                cachedZone = _frameInfo.Octree.QueryZone(drawableCenter, drawable.GetZoneMask());
                drawable.MarkPipelineStateHashDirty();
            }
        }
    }

    public void UpdateDrawableReflection(BoundingBox boundingBox, Drawable drawable)
    {
        var mode = drawable.GetReflectionMode();
        if (mode == ReflectionMode.Zone)
            return;

        var manager = _frameInfo.ReflectionProbeManager;
        if (manager is null)
            return;

        var drawableCenter = boundingBox.Center;
        ref var cachedReflection = ref drawable.GetMutableCachedReflection();

        if (!manager.HasStaticProbes())
        {
            for (var i = 0; i < 2; ++i)
                cachedReflection.Probes[i].Reset();
        }
        else
        {
            var drawableCacheDistanceSquared = (cachedReflection.CachePosition - drawableCenter).LengthSquared;

            var forcedUpdate = !float.IsFinite(drawableCacheDistanceSquared)
                || cachedReflection.CacheRevision != manager.GetRevision();
            if (forcedUpdate || drawableCacheDistanceSquared > cachedReflection.CacheInvalidationDistanceSquared)
            {
                var staticProbes = new ReflectionProbeReference[2];
                var cacheDistance = cachedReflection.CacheInvalidationDistanceSquared;
                manager.QueryStaticProbes(boundingBox, staticProbes, ref cacheDistance);
                cachedReflection.StaticProbes[0] = staticProbes[0];
                cachedReflection.StaticProbes[1] = staticProbes[1];
                cachedReflection.CacheInvalidationDistanceSquared = cacheDistance;
                cachedReflection.CacheRevision = manager.GetRevision();
                cachedReflection.CachePosition = drawableCenter;
            }

            cachedReflection.Probes[0] = cachedReflection.StaticProbes[0];
            cachedReflection.Probes[1] = cachedReflection.StaticProbes[1];
        }

        if (manager.HasDynamicProbes())
        {
            var probes = new ReflectionProbeReference[2];
            probes[0] = cachedReflection.Probes[0];
            probes[1] = cachedReflection.Probes[1];
            manager.QueryDynamicProbes(boundingBox, probes);
            cachedReflection.Probes[0] = probes[0];
            cachedReflection.Probes[1] = probes[1];
        }

        // Estimate Zone weight and replace 2nd probe if it's less important
        if (cachedReflection.Probes[1].IsValid && mode == ReflectionMode.BlendProbesAndZone)
        {
            var zoneWeight = Math.Max(0.0f, 1.0f - cachedReflection.Probes[0].Volume - cachedReflection.Probes[1].Volume);
            if (zoneWeight > cachedReflection.Probes[1].Volume)
                cachedReflection.Probes[1].Reset();
        }
    }

    public void QueueDrawableGeometryUpdate(int threadIndex, Drawable drawable)
    {
        var updateGeometryType = drawable.GetUpdateGeometryType();
        if (updateGeometryType == UpdateGeometryType.MainThread)
            _nonThreadedGeometryUpdates.PushBack(threadIndex, drawable);
        else if (updateGeometryType == UpdateGeometryType.WorkerThread)
            _threadedGeometryUpdates.PushBack(threadIndex, drawable);
    }

    private static bool IsBoundingBoxShadowInOrthoFrustum(BoundingBox boundingBox,
        Frustum frustum, BoundingBox frustumBoundingBox)
    {
        // Extrude the bounding box up to the far edge of the frustum's light space bounding box
        var extrudedBoundingBox = boundingBox;
        extrudedBoundingBox.Max = new Vector3f(
            extrudedBoundingBox.Max.X,
            extrudedBoundingBox.Max.Y,
            Math.Max(extrudedBoundingBox.Max.Z, frustumBoundingBox.Max.Z));
        return frustum.IsInsideFast(extrudedBoundingBox) != Intersection.Outside;
    }

    private static bool IsBoundingBoxShadowInPerspectiveFrustum(BoundingBox boundingBox,
        Frustum frustum, float extrusionDistance)
    {
        // Extrusion direction depends on the position of the shadow caster
        var center = boundingBox.Center;
        var size = boundingBox.Size;

        // Because of the perspective, the bounding box must also grow when it is extruded to the distance
        var originalDistance = MathDefs.Clamp(center.Z - size.Z * 0.5f, MathDefs.Epsilon, extrusionDistance);
        var sizeFactor = extrusionDistance / originalDistance;

        // Calculate the endpoint box and merge it to the original. Because it's axis-aligned, it will be larger
        // than necessary, so the test will be conservative
        var newCenter = center * sizeFactor;
        var newHalfSize = size * sizeFactor * 0.5f;

        var extrudedBox = new BoundingBox(newCenter - newHalfSize, newCenter + newHalfSize);
        extrudedBox.Merge(boundingBox);

        return frustum.IsInsideFast(extrudedBox) != Intersection.Outside;
    }

    private static bool IsShadowCasterVisible(BoundingBox lightSpaceBoundingBox, Camera shadowCamera,
        Frustum lightSpaceFrustum, BoundingBox lightSpaceFrustumBoundingBox)
    {
        if (shadowCamera.IsOrthographic())
            return IsBoundingBoxShadowInOrthoFrustum(lightSpaceBoundingBox, lightSpaceFrustum, lightSpaceFrustumBoundingBox);
        return IsBoundingBoxShadowInPerspectiveFrustum(lightSpaceBoundingBox, lightSpaceFrustum, shadowCamera.GetFarClip());
    }

    public void OnCollectStatistics(ref RenderPipelineStats stats)
    {
        stats.NumOccluders += _sortedOccluders.Count;
        stats.NumLights += _lights.Count;
        stats.NumGeometries += checked((int)_geometries.Size());
        stats.NumShadowedLights += _numShadowedLights;
    }

    public void ProcessOccluders(IReadOnlyList<Drawable> occluders, float sizeThreshold)
    {
        var cullCamera = _frameInfo.Camera!;
        var halfViewSize = cullCamera.GetHalfViewSize();
        var invOrthoSize = 1.0f / cullCamera.GetOrthoSize();

        foreach (var drawable in occluders)
        {
            drawable.UpdateBatches(_frameInfo);

            // Skip if too far
            var maxDistance = drawable.GetDrawDistance();
            if (maxDistance > 0.0f && drawable.GetDistance() > maxDistance)
                return;

            // Check that occluder is big enough on the screen
            var boundingBox = drawable.GetWorldBoundingBox();
            var drawableSize = boundingBox.Size.Length;
            var relativeSize = 0.0f;
            if (cullCamera.IsOrthographic())
                relativeSize = drawableSize * invOrthoSize;
            else
            {
                // Occluders which are near the camera are more useful then occluders at the end of the camera's draw distance
                var relativeDistance = drawable.GetDistance() / cullCamera.GetFarClip();
                relativeSize = drawableSize * halfViewSize
                    / MathF.Max(MathDefs.Epsilon, drawable.GetDistance() * relativeDistance);

                // Give higher priority to occluders which the camera is inside their AABB
                var cameraPos = cullCamera.Node!.GetWorldPosition();
                if (boundingBox.IsInside(cameraPos) == Intersection.Inside)
                    relativeSize *= drawableSize;
            }

            // Keep occluders larger than threshold with lowest triangle count to size ratio.
            if (relativeSize >= sizeThreshold)
            {
                var density = drawable.GetNumOccluderTriangles() / drawableSize;
                var penalty = density / MathF.Max(MathDefs.Epsilon, relativeSize);
                _sortedOccluders.Add(new SortedOccluder { SortValue = penalty, Drawable = drawable });
            }
        }

        _sortedOccluders.Sort();
    }
}
