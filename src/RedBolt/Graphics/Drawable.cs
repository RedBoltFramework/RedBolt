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

/// <summary>Cached info about current zone.</summary>
public struct CachedDrawableZone
{
    /// <summary>Pointer to Zone.</summary>
    public Zone? Zone;

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

public abstract class Drawable : Component
{
    public const uint DefaultViewMask = uint.MaxValue;
    public const uint DefaultLightMask = uint.MaxValue;
    public const uint DefaultShadowMask = uint.MaxValue;
    public const uint DefaultZoneMask = uint.MaxValue;
    public const uint PortableLightMask = 0x0f;
    public const int MaxVertexLights = 4;
    public const float AnimationLodBaseScale = 2500.0f;

    protected Drawable(DrawableFlags drawableFlags = DrawableFlags.Undefined)
    {
        DrawableFlags = drawableFlags;
    }

    private BoundingBox _boundingBox;
    private BoundingBox _worldBoundingBox;
    private bool _worldBoundingBoxDirty = true;
    private CachedDrawableZone _cachedZone;
    private bool _zoneDirty;
    private Octant? _octant;
    private uint _drawableIndex = uint.MaxValue;

    public BoundingBox BoundingBox => _boundingBox;

    public BoundingBox WorldBoundingBox => GetWorldBoundingBox();

    public DrawableFlags DrawableFlags { get; }

    public float DrawDistance { get; set; }

    public float ShadowDistance { get; set; }

    public float LodBias { get; set; } = 1.0f;

    public uint ViewMask { get; set; } = DefaultViewMask;

    public uint LightMask { get; set; } = DefaultLightMask;

    public uint ShadowMask { get; set; } = DefaultShadowMask;

    public uint ZoneMask { get; set; } = DefaultZoneMask;

    public bool CastShadows { get; set; }

    public bool Occluder { get; set; } = true;

    public bool Occludee { get; set; } = true;

    public GlobalIlluminationType GlobalIlluminationType { get; set; }

    public ReflectionMode ReflectionMode { get; set; }

    public BoundingBox GetBoundingBox() => _boundingBox;

    public DrawableFlags GetDrawableFlags() => DrawableFlags;

    public float GetDrawDistance() => DrawDistance;

    public float GetShadowDistance() => ShadowDistance;

    public float GetLodBias() => LodBias;

    public uint GetViewMask() => ViewMask;

    public uint GetLightMask() => LightMask;

    public uint GetShadowMask() => ShadowMask;

    public uint GetZoneMask() => ZoneMask;

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

    internal void SetDrawableIndex(uint drawableIndex) => _drawableIndex = drawableIndex;

    /// <summary>
    /// Return mutable cached zone data.
    /// </summary>
    public ref CachedDrawableZone GetMutableCachedZone() => ref _cachedZone;

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
        DrawDistance = Math.Max(distance, 0.0f);
    }

    public void SetShadowDistance(float distance)
    {
        ShadowDistance = Math.Max(distance, 0.0f);
    }

    public void SetLodBias(float bias)
    {
        LodBias = Math.Max(bias, MathDefs.Epsilon);
    }

    public void SetViewMask(uint mask)
    {
        ViewMask = mask;
    }

    public void SetLightMask(uint mask)
    {
        LightMask = mask;
    }

    public void SetShadowMask(uint mask)
    {
        ShadowMask = mask;
    }

    public void SetZoneMask(uint mask)
    {
        ZoneMask = mask;
        _cachedZone.CacheInvalidationDistanceSquared = -1.0f;
        if (IsAttached)
            OnMarkedDirty(Node);
    }

    public void SetCastShadows(bool enable)
    {
        CastShadows = enable;
    }

    public void SetOccluder(bool enable)
    {
        Occluder = enable;
    }

    public void SetOccludee(bool enable)
    {
        Occludee = enable;
    }

    public void SetGlobalIlluminationType(GlobalIlluminationType type)
    {
        GlobalIlluminationType = type;
    }

    public void SetReflectionMode(ReflectionMode mode)
    {
        ReflectionMode = mode;
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
        _worldBoundingBoxDirty = true;
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

    protected virtual void OnWorldBoundingBoxUpdate()
    {
        _worldBoundingBox = IsAttached ? _boundingBox.Transformed(Node.GetWorldTransform()) : _boundingBox;
    }

    protected internal override void OnMarkedDirty(Node node)
    {
        MarkForUpdate();
    }
}
