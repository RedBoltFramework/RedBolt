namespace RedBolt;

[Flags]
public enum DrawableFlags : byte
{
    Undefined = 0,
    Geometry = 1 << 0,
    Light = 1 << 1,
    Zone = 1 << 2,
    Geometry2D = 1 << 3,
    Any = 0xff
}

/// <summary>Geometry update type.</summary>
public enum UpdateGeometryType
{
    None = 0,
    MainThread,
    WorkerThread
}

public enum GlobalIlluminationType
{
    None,
    UseLightMap,
    BlendLightProbes
}

public enum ReflectionMode
{
    Zone,
    NearestProbe,
    BlendProbes,
    BlendProbesAndZone
}

/// <summary>Rendering frame update parameters.</summary>
public struct FrameInfo
{
    /// <summary>Frame number.</summary>
    public uint FrameNumber;

    /// <summary>Time elapsed since last frame.</summary>
    public float TimeStep;

    /// <summary>Viewport size.</summary>
    public Vector2i ViewSize;

    /// <summary>Viewport rectangle.</summary>
    public IntRect ViewRect;

    /// <summary>Destination viewport.</summary>
    public Viewport? Viewport;

    /// <summary>Destination render surface.</summary>
    public RenderOutputTargetInfo? RenderTarget;

    /// <summary>Scene and pre-fetched Scene components.</summary>
    public Scene? Scene;

    /// <summary>Node to be used as a point-of-view reference, typically the camera's node.</summary>
    public Node? ViewReferenceNode;

    public Camera? Camera;

    /// <summary>Optional list of additional cameras that may be attached, such as eyes, etc.</summary>
    public (Camera? First, Camera? Second) AdditionalCameras;

    public Octree? Octree;

    public ReflectionProbeManager? ReflectionProbeManager;
}

/// <summary>Cached info about current zone.</summary>
public struct CachedDrawableZone
{
    private WeakReference<Zone>? _zone;

    /// <summary>Pointer to Zone.</summary>
    public Zone? Zone
    {
        readonly get => _zone is not null && _zone.TryGetTarget(out var zone) ? zone : null;
        set => _zone = value is not null ? new WeakReference<Zone>(value) : null;
    }

    /// <summary>Node position at the moment of last caching.</summary>
    public Vector3f CachePosition;

    /// <summary>Cache invalidation distance (squared).</summary>
    public float CacheInvalidationDistanceSquared = -1.0f;

    public CachedDrawableZone(Zone? zone, Vector3f cachePosition, float cacheInvalidationDistanceSquared)
    {
        Zone = zone;
        CachePosition = cachePosition;
        CacheInvalidationDistanceSquared = cacheInvalidationDistanceSquared;
    }
}

[System.Runtime.CompilerServices.InlineArray(2)]
public struct ReflectionProbeReferenceArray
{
    private ReflectionProbeReference _element0;

    public readonly int Length => 2;
}

/// <summary>Cached info about current static reflection probe.</summary>
public struct CachedDrawableReflection
{
    /// <summary>Most important reflection probes affecting Drawable.</summary>
    public ReflectionProbeReferenceArray StaticProbes;

    public ReflectionProbeReferenceArray Probes;

    /// <summary>Information for cache invalidation.</summary>
    public uint CacheRevision { get; set; }

    public Vector3f CachePosition { get; set; }

    public float CacheInvalidationDistanceSquared { get; set; }

    public CachedDrawableReflection()
    {
        CacheRevision = 0;
        CachePosition = default;
        CacheInvalidationDistanceSquared = -1.0f;
    }
}

/// <summary>Source data for a 3D geometry draw call.</summary>
public struct SourceBatch : ICloneable, IDisposable, IEquatable<SourceBatch>
{
    private static readonly Matrix3x4[] _identityWorldTransform = [Matrix3x4.Identity];

    /// <summary>Distance from camera.</summary>
    public float Distance;
    /// <summary>Geometry.</summary>
    public Geometry? Geometry;
    /// <summary>Material.</summary>
    public Material? Material;
    /// <summary>World transform(s). For a skinned model, these are the bone transforms.</summary>
    public Matrix3x4[] WorldTransform = _identityWorldTransform;
    /// <summary>Number of world transforms.</summary>
    public uint NumWorldTransforms = 1;
    /// <summary>Per-instance data. If not null, must contain enough data to fill instancing buffer.</summary>
    public object? InstancingData;
    /// <summary>Geometry type.</summary>
    public GeometryType GeometryType = GeometryType.Static;
    /// <summary>Lightmap UV scale and offset.</summary>
    public Vector4f? LightmapScaleOffset;
    /// <summary>Lightmap texture index.</summary>
    public uint LightmapIndex;

    /// <summary>Construct with defaults.</summary>
    public SourceBatch()
    {
    }

    /// <summary>Copy-construct.</summary>
    public object Clone() => this;

    /// <summary>Destruct.</summary>
    public void Dispose()
    {
    }

    /// <summary>Equality comparison operator.</summary>
    public bool Equals(SourceBatch other)
    {
        return Distance == other.Distance && ReferenceEquals(Geometry, other.Geometry) &&
            ReferenceEquals(Material, other.Material) && ReferenceEquals(WorldTransform, other.WorldTransform) &&
            NumWorldTransforms == other.NumWorldTransforms && ReferenceEquals(InstancingData, other.InstancingData) &&
            GeometryType == other.GeometryType;
    }

    /// <summary>Inequality comparison operator.</summary>
    public static bool operator !=(SourceBatch left, SourceBatch right) => !left.Equals(right);

    /// <summary>Equality comparison operator.</summary>
    public static bool operator ==(SourceBatch left, SourceBatch right) => left.Equals(right);

    public override bool Equals(object? obj) => obj is SourceBatch other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Distance, Geometry, Material, WorldTransform,
        NumWorldTransforms, InstancingData, GeometryType);
}

/// <summary>Base class for visible components.</summary>
/// <remarks>
/// </remarks>
public abstract class Drawable : Component
{
    private sealed class DrawablePipelineStateTracker : PipelineStateTracker
    {
        private readonly Drawable _owner;

        public DrawablePipelineStateTracker(Drawable owner)
        {
            _owner = owner;
        }

        public PipelineStateSubscription CreateDependencyFor(PipelineStateTracker? dependency)
        {
            return CreateDependency(dependency);
        }

        protected override uint RecalculatePipelineStateHash()
        {
            return _owner.RecalculatePipelineStateHash();
        }
    }

    public const uint DefaultViewMask = uint.MaxValue;
    public const uint DefaultLightMask = uint.MaxValue;
    public const uint DefaultShadowMask = uint.MaxValue;
    public const uint DefaultZoneMask = uint.MaxValue;
    public const uint PortableLightMask = 0x0f;
    public const int MaxVertexLights = 4;
    public const float AnimationLodBaseScale = 2500.0f;

    protected Drawable(DrawableFlags drawableFlags = DrawableFlags.Undefined)
    {
        _boundingBox = new BoundingBox(0.0f, 0.0f);
        DrawableFlags = drawableFlags;
        _pipelineStateTracker = new DrawablePipelineStateTracker(this);
    }

    public override void Dispose()
    {
        RemoveFromOctree();
        _numViewCameras = 0;
        _viewCameras.Clear();
        _cachedReflection = new CachedDrawableReflection();
        _batches.Clear();
        _pipelineStateTracker.Dispose();
        base.Dispose();
    }

    protected BoundingBox _worldBoundingBox = new();
    protected BoundingBox _boundingBox;
    protected bool _worldBoundingBoxDirty = true;
    private CachedDrawableZone _cachedZone = new();
    private bool _zoneDirty;
    private Octant? _octant;
    internal bool _updateQueued;
    private uint _drawableIndex = uint.MaxValue;
    private CachedDrawableReflection _cachedReflection = new();
    private uint _lightProbeTetrahedronHint = uint.MaxValue;
    protected readonly System.Collections.Generic.List<SourceBatch> _batches = [];
    protected readonly Matrix3x4[] _worldTransform = [Matrix3x4.Identity];
    protected uint _viewFrameNumber;
    private float _distance;
    private float _lodDistance;
    private float _drawDistance;
    private float _shadowDistance;
    private float _sortValue;
    private float _lodBias = 1.0f;
    private uint _viewMask = DefaultViewMask;
    private uint _lightMask = DefaultLightMask;
    private uint _shadowMask = DefaultShadowMask;
    private uint _zoneMask = DefaultZoneMask;
    private bool _castShadows;
    private bool _occluder;
    private bool _occludee = true;
    private GlobalIlluminationType _globalIlluminationType;
    private ReflectionMode _reflectionMode = ReflectionMode.BlendProbesAndZone;
    protected readonly System.Collections.Generic.List<WeakReference<Camera>?> _viewCameras = [];
    private int _numViewCameras;
    private readonly DrawablePipelineStateTracker _pipelineStateTracker;

    public BoundingBox BoundingBox => _boundingBox;

    public BoundingBox WorldBoundingBox => GetWorldBoundingBox();

    public DrawableFlags DrawableFlags { get; }

    public float DrawDistance
    {
        get => _drawDistance;
        set => SetDrawDistance(value);
    }

    public float ShadowDistance
    {
        get => _shadowDistance;
        set => SetShadowDistance(value);
    }

    public float LodBias
    {
        get => _lodBias;
        set => SetLodBias(value);
    }

    public uint ViewMask
    {
        get => _viewMask;
        set => SetViewMask(value);
    }

    public uint LightMask
    {
        get => _lightMask;
        set => SetLightMask(value);
    }

    public uint ShadowMask
    {
        get => _shadowMask;
        set => SetShadowMask(value);
    }

    public uint ZoneMask
    {
        get => _zoneMask;
        set => SetZoneMask(value);
    }

    public bool CastShadows
    {
        get => _castShadows;
        set => SetCastShadows(value);
    }

    public bool Occluder
    {
        get => _occluder;
        set => SetOccluder(value);
    }

    public bool Occludee
    {
        get => _occludee;
        set => SetOccludee(value);
    }

    public GlobalIlluminationType GlobalIlluminationType
    {
        get => _globalIlluminationType;
        set => SetGlobalIlluminationType(value);
    }

    public ReflectionMode ReflectionMode
    {
        get => _reflectionMode;
        set => SetReflectionMode(value);
    }

    public BoundingBox GetBoundingBox() => _boundingBox;

    public DrawableFlags GetDrawableFlags() => DrawableFlags;

    public float GetDrawDistance() => DrawDistance;

    public float GetShadowDistance() => ShadowDistance;

    public float GetDistance() => _distance;

    public float GetLodDistance() => _lodDistance;

    public float GetSortValue() => _sortValue;

    public float GetLodBias() => LodBias;

    public uint GetViewMask() => ViewMask;

    public uint GetLightMask() => LightMask;

    public uint GetShadowMask() => ShadowMask;

    public uint GetZoneMask() => ZoneMask;

    public uint GetPipelineStateHash() => _pipelineStateTracker.GetPipelineStateHash();

    public void MarkPipelineStateHashDirty() => _pipelineStateTracker.MarkPipelineStateHashDirty();

    /// <summary>
    /// Return current zone.
    /// </summary>
    public Zone? GetZone() => _cachedZone.Zone;

    /// <summary>
    /// Return whether current zone is inconclusive or dirty due to the drawable moving.
    /// </summary>
    public bool IsZoneDirty() => _zoneDirty;

    /// <summary>
    /// Return octree octant.
    /// </summary>
    public Octant? GetOctant() => _octant;

    internal void SetOctant(Octant? octant) => _octant = octant;

    /// <summary>
    /// Return index in octree.
    /// </summary>
    public uint GetDrawableIndex() => _drawableIndex;

    /// <summary>
    /// Return whether the drawable is added to Octree.
    /// </summary>
    public bool IsInOctree() => _drawableIndex != uint.MaxValue;

    internal void SetDrawableIndex(uint drawableIndex) => _drawableIndex = drawableIndex;

    /// <summary>
    /// Return mutable cached zone data.
    /// </summary>
    public ref CachedDrawableZone GetMutableCachedZone() => ref _cachedZone;

    /// <summary>
    /// Return mutable cached reflection data.
    /// </summary>
    public ref CachedDrawableReflection GetMutableCachedReflection() => ref _cachedReflection;

    /// <summary>
    /// Return mutable light probe tetrahedron hint.
    /// </summary>
    public ref uint GetMutableLightProbeTetrahedronHint() => ref _lightProbeTetrahedronHint;

    /// <summary>
    /// Return combined light masks of Drawable and its currently cached Zone.
    /// </summary>
    public uint GetLightMaskInZone()
    {
        var zoneLightMask = _cachedZone.Zone?.GetLightMask() ?? uint.MaxValue;
        return zoneLightMask & LightMask;
    }

    /// <summary>
    /// Return combined shadow masks of Drawable and its currently cached Zone.
    /// </summary>
    public uint GetShadowMaskInZone()
    {
        var zoneShadowMask = _cachedZone.Zone?.GetShadowMask() ?? uint.MaxValue;
        return zoneShadowMask & ShadowMask;
    }

    public bool GetCastShadows() => CastShadows;

    public bool IsOccluder() => Occluder;

    public bool IsOccludee() => Occludee;

    public GlobalIlluminationType GetGlobalIlluminationType() => GlobalIlluminationType;

    public ReflectionMode GetReflectionMode() => ReflectionMode;

    /// <summary>
    /// Return whether is in view this frame from any viewport camera. Excludes shadow map cameras.
    /// </summary>
    public bool IsInView()
    {
        var renderer = GetSubsystem<Renderer>();
        return renderer is not null && _viewFrameNumber == renderer.GetFrameInfo().FrameNumber && _numViewCameras > 0;
    }

    /// <summary>
    /// Return whether is in view of a specific camera this frame. Pass in a null camera to allow any camera, including shadow map cameras.
    /// </summary>
    public bool IsInView(Camera? camera)
    {
        var renderer = GetSubsystem<Renderer>();
        return renderer is not null && _viewFrameNumber == renderer.GetFrameInfo().FrameNumber &&
            (camera is null || ContainsViewCamera(camera));
    }

    /// <summary>
    /// Return draw call source data.
    /// </summary>
    public IReadOnlyList<SourceBatch> GetBatches() => _batches;

    /// <summary>Process raycast with custom transform.</summary>
    public void ProcessCustomRayQuery(RayOctreeQuery query, BoundingBox worldBoundingBox, List<RayQueryResult> results)
    {
        var distanceAndNormal = query.Ray.HitDistanceAndNormal(worldBoundingBox);
        if (distanceAndNormal.Distance < query.MaxDistance)
        {
            var result = new RayQueryResult
            {
                Position = query.Ray.Origin + distanceAndNormal.Distance * query.Ray.Direction,
                // For the world bounding box the normal is in world space.
                Normal = distanceAndNormal.Normal,
                Distance = distanceAndNormal.Distance,
                Drawable = this,
                Node = Node,
                SubObject = uint.MaxValue
            };
            results.Add(result);
        }
    }

    /// <summary>Process octree raycast. May be called from a worker thread.</summary>
    public virtual void ProcessRayQuery(RayOctreeQuery query, List<RayQueryResult> results)
    {
        ProcessCustomRayQuery(query, GetWorldBoundingBox(), results);
    }

    /// <summary>Update before octree reinsertion. May be called from a worker thread.</summary>
    public virtual void Update(FrameInfo frame)
    {
    }

    public virtual void UpdateBatches(FrameInfo frame)
    {
        var worldBoundingBox = GetWorldBoundingBox();
        _worldTransform[0] = Node.GetWorldTransform();
        _distance = frame.Camera!.GetDistance(worldBoundingBox.Center);

        for (var i = 0; i < _batches.Count; ++i)
        {
            var batch = _batches[i];
            batch.Distance = _distance;
            batch.WorldTransform = _worldTransform;
            _batches[i] = batch;
        }

        var scale = (worldBoundingBox.Size.X + worldBoundingBox.Size.Y + worldBoundingBox.Size.Z) / 3.0f;
        var newLodDistance = frame.Camera.GetLodDistance(_distance, scale, LodBias);
        if (newLodDistance != _lodDistance)
            _lodDistance = newLodDistance;
    }

    /// <summary>Batch update from the main thread.</summary>
    public virtual void UpdateBatchesDelayed(FrameInfo frame)
    {
    }

    /// <summary>Prepare geometry for rendering.</summary>
    public virtual void UpdateGeometry(FrameInfo frame)
    {
    }

    /// <summary>Return whether a geometry update is necessary, and if it can happen in a worker thread.</summary>
    public virtual UpdateGeometryType GetUpdateGeometryType() => UpdateGeometryType.None;

    /// <summary>Return the geometry for a specific LOD level.</summary>
    public virtual Geometry? GetLodGeometry(uint batchIndex, uint level)
    {
        // By default return the visible batch geometry.
        return batchIndex < _batches.Count ? _batches[(int)batchIndex].Geometry : null;
    }

    /// <summary>Get Geometry pointer if the source one is not null or empty.</summary>
    protected static Geometry? GetGeometryIfNotEmpty(Geometry? geometry)
    {
        return geometry is not null && geometry.GetEffectiveIndexCount() > 0 ? geometry : null;
    }

    /// <summary>Return number of occlusion geometry triangles.</summary>
    public virtual uint GetNumOccluderTriangles() => 0;

    /// <summary>Draw to occlusion buffer. Return true if did not run out of triangles.</summary>
    public virtual bool DrawOcclusion(OcclusionBuffer buffer) => true;

    public bool IsInView(FrameInfo frame, bool anyCamera = false)
    {
        return _viewFrameNumber == frame.FrameNumber &&
            (anyCamera || frame.AdditionalCameras.First is { } camera && ContainsViewCamera(camera));
    }

    public BoundingBox GetWorldBoundingBox()
    {
        if (_worldBoundingBoxDirty)
        {
            OnWorldBoundingBoxUpdate();
            _worldBoundingBoxDirty = false;
        }

        return _worldBoundingBox;
    }

    public void SetDrawDistance(float distance)
    {
        _drawDistance = distance;
    }

    public void SetShadowDistance(float distance)
    {
        _shadowDistance = distance;
    }

    public void SetSortValue(float value)
    {
        _sortValue = value;
    }

    protected void SetDistance(float distance)
    {
        _distance = distance;
    }

    protected void SetLodDistance(float distance)
    {
        _lodDistance = distance;
    }

    public void MarkInView(FrameInfo frame)
    {
        if (frame.FrameNumber != _viewFrameNumber)
        {
            _viewFrameNumber = frame.FrameNumber;
            _numViewCameras = 0;
        }

        SetViewCamera(_numViewCameras++, frame.AdditionalCameras.First);
    }

    public void MarkInView(uint frameNumber)
    {
        if (frameNumber != _viewFrameNumber)
        {
            _viewFrameNumber = frameNumber;
            _numViewCameras = 0;
        }
    }

    public void SetLodBias(float bias)
    {
        _lodBias = Math.Max(bias, MathDefs.Epsilon);
    }

    public void SetViewMask(uint mask)
    {
        _viewMask = mask;
    }

    public void SetLightMask(uint mask)
    {
        _lightMask = mask;
        MarkPipelineStateHashDirty();
    }

    public void SetShadowMask(uint mask)
    {
        _shadowMask = mask;
    }

    public void SetZoneMask(uint mask)
    {
        _zoneMask = mask;
        _cachedZone.CacheInvalidationDistanceSquared = -1.0f;
        if (Node is not null)
            OnMarkedDirty(Node);
        else
        {
            _worldBoundingBoxDirty = true;
            MarkForUpdate();
            _zoneDirty = true;
        }
    }

    public void SetCastShadows(bool enable)
    {
        _castShadows = enable;
    }

    public void SetOccluder(bool enable)
    {
        _occluder = enable;
    }

    public void SetOccludee(bool enable)
    {
        if (enable != _occludee)
        {
            _occludee = enable;
            // Reinsert to octree to make sure octant occlusion does not erroneously hide this drawable
            if (_octant is not null && !_updateQueued)
                _octant.GetOctree()!.QueueUpdate(this);
        }
    }

    public void SetGlobalIlluminationType(GlobalIlluminationType type)
    {
        _globalIlluminationType = type;
        MarkPipelineStateHashDirty();
    }

    public void SetReflectionMode(ReflectionMode mode)
    {
        _reflectionMode = mode;
        MarkPipelineStateHashDirty();
    }

    /// <summary>
    /// Set new zone.
    /// </summary>
    public void SetZone(Zone? zone, bool temporary = false)
    {
        if (zone is not null)
            _cachedZone.Zone = zone;
        else
            _cachedZone = new CachedDrawableZone();

        // If the zone assignment was temporary (inconclusive) set the dirty flag so that it will be re-evaluated on the next frame.
        _zoneDirty = temporary;
    }

    public void MarkForUpdate()
    {
        if (!_updateQueued && _octant is not null)
            _octant.GetOctree()!.QueueUpdate(this);
    }

    /// <summary>Request UpdateBatchesDelayed call from the main thread.</summary>
    protected void RequestUpdateBatchesDelayed(FrameInfo frame)
    {
        var workQueue = GetSubsystem<WorkQueue>();
        workQueue?.PostTaskForMainThread(() => UpdateBatchesDelayed(frame), TaskPriority.Immediate);
    }

    public override void OnSetEnabled()
    {
        var enabled = IsEnabledEffective;
        if (enabled && _octant is null)
            AddToOctree();
        else if (!enabled && _octant is not null)
            RemoveFromOctree();
    }

    protected override void OnNodeSet(Node? previousNode, Node? currentNode)
    {
        currentNode?.AddListener(this);
    }

    protected internal override void OnSceneSet(Scene? previousScene, Scene? scene)
    {
        if (scene is not null)
            AddToOctree();
        else
            RemoveFromOctree();
    }

    protected void SetLocalBoundingBox(BoundingBox box)
    {
        _boundingBox = box;
        MarkForUpdate();
    }

    protected void SetWorldBoundingBox(BoundingBox box)
    {
        _worldBoundingBox = box;
    }

    protected abstract void OnWorldBoundingBoxUpdate();

    protected internal override void OnMarkedDirty(Node node)
    {
        _worldBoundingBoxDirty = true;
        MarkForUpdate();

        // Mark zone assignment dirty when transform changes
        if (ReferenceEquals(node, Node))
            _zoneDirty = true;
    }

    /// <summary>
    /// Called on world origin update.
    /// </summary>
    public virtual void UpdateWorldOrigin(IntVector3 oldOrigin, IntVector3 newOrigin, IntVector3 delta)
    {
    }

    /// <summary>
    /// Called on world origin post-update.
    /// </summary>
    public virtual void PostUpdateWorldOrigin(IntVector3 oldOrigin, IntVector3 newOrigin, IntVector3 delta)
    {
    }

    public override void DrawDebugGeometry(DebugRenderer? debug, bool depthTest)
    {
        if (debug is not null && IsEnabledEffective)
            debug.AddBoundingBox(GetWorldBoundingBox(), new Color(0.0f, 1.0f, 0.0f, 0.46666667f), depthTest);
    }

    protected virtual void OnRemoveFromOctree()
    {
    }

    protected void AddToOctree()
    {
        if (!IsEnabledEffective)
            return;

        var scene = Scene;
        if (scene is not null)
        {
            var octrees = new System.Collections.Generic.List<Octree>(1);
            scene.GetComponents(octrees);
            var octree = octrees.Count > 0 ? octrees[0] : null;
            if (octree is not null)
                octree.AddDrawable(this);
            else
                Microsoft.Extensions.Logging.LoggerExtensions.LogError(
                    Log.GetLogger(), "No Octree component in scene, drawable will not render.");
        }
    }

    protected void RemoveFromOctree()
    {
        if (_octant is null)
            return;

        var octree = _octant.GetOctree()!;
        if (_updateQueued)
            octree.CancelUpdate(this);
        OnRemoveFromOctree();
        octree.RemoveDrawable(this, _octant);
    }

    private bool ContainsViewCamera(Camera camera)
    {
        for (var i = 0; i < _numViewCameras; ++i)
        {
            if (_viewCameras[i] is { } weakCamera && weakCamera.TryGetTarget(out var viewCamera) &&
                ReferenceEquals(viewCamera, camera))
                return true;
        }
        return false;
    }

    private void SetViewCamera(int index, Camera? camera)
    {
        if (index == _viewCameras.Count)
            _viewCameras.Add(camera is not null ? new WeakReference<Camera>(camera) : null);
        else if (camera is null)
            _viewCameras[index] = null;
        else if (_viewCameras[index] is { } weakCamera)
            weakCamera.SetTarget(camera);
        else
            _viewCameras[index] = new WeakReference<Camera>(camera);
    }

    protected PipelineStateSubscription CreateDependency(PipelineStateTracker? dependency)
    {
        return _pipelineStateTracker.CreateDependencyFor(dependency);
    }

    private uint RecalculatePipelineStateHash()
    {
        var hash = 0u;
        CombineHash(ref hash, (uint)GlobalIlluminationType);
        CombineHash(ref hash, ReflectionMode >= ReflectionMode.BlendProbes ? 1u : 0u);
        return hash;
    }

    private static void CombineHash(ref uint result, uint hash)
    {
        result ^= hash + 0x9e3779b9u + (result << 6) + (result >> 2);
    }
}
