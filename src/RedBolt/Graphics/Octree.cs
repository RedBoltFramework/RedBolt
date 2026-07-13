namespace RedBolt;

public enum RayQueryLevel
{
    RayAabb,
    RayObb,
    RayTriangle,
    RayTriangleUv
}

public sealed class RayQueryResult
{
    public Vector3f Position { get; set; }

    public Vector3f Normal { get; set; }

    public Vector2f TextureUV { get; set; }

    public float Distance { get; set; }

    public Drawable? Drawable { get; set; }

    public Node? Node { get; set; }

    public uint SubObject { get; set; }

    public bool Equals(RayQueryResult? other)
    {
        return other is not null && Position == other.Position && Normal == other.Normal && TextureUV == other.TextureUV &&
            Distance == other.Distance && ReferenceEquals(Drawable, other.Drawable) && ReferenceEquals(Node, other.Node) &&
            SubObject == other.SubObject;
    }

    public override bool Equals(object? obj) => obj is RayQueryResult other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Position, Normal, TextureUV, Distance, Drawable, Node, SubObject);

    public static bool operator !=(RayQueryResult? left, RayQueryResult? right) => !(left?.Equals(right) ?? right is null);

    public static bool operator ==(RayQueryResult? left, RayQueryResult? right) => left?.Equals(right) ?? right is null;
}

public sealed class Octant
{
    internal Octant(BoundingBox box, uint level, Octant? parent, Octree octree)
    {
        WorldBoundingBox = box;
        CullingBox = box;
        Level = level;
        Parent = parent;
        Octree = octree;
    }

    public BoundingBox WorldBoundingBox { get; private set; }

    public BoundingBox CullingBox { get; private set; }

    public uint Level { get; }

    public Octant? Parent { get; }

    public Octree Octree { get; }

    public uint NumDrawables => Octree.GetNumDrawables();

    public bool IsEmpty() => NumDrawables == 0;

    public BoundingBox GetWorldBoundingBox() => WorldBoundingBox;

    public BoundingBox GetCullingBox() => CullingBox;

    public uint GetLevel() => Level;

    public Octant? GetParent() => Parent;

    public Octree GetOctree() => Octree;

    public uint GetNumDrawables() => NumDrawables;

    internal void SetRootSize(BoundingBox box)
    {
        WorldBoundingBox = box;
        CullingBox = box;
    }
}

/// <summary>Acceleration structure for zone search.</summary>
public sealed class ZoneLookupIndex
{
    private sealed class ZoneData
    {
        public BoundingBox BoundingBox;
        public Matrix3x4 InverseWorldTransform;
        public uint ZoneMask;
    }

    private readonly Zone? _defaultZone;
    private readonly List<Zone> _zones = [];
    private readonly List<ZoneData> _zonesData = [];
    private bool _zonesDirty;

    public ZoneLookupIndex()
    {
        var context = Context.Instance;
        var renderer = context.GetSubsystem<Renderer>();
        _defaultZone = renderer?.GetDefaultZone();
    }

    public void AddZone(Zone zone)
    {
        ArgumentNullException.ThrowIfNull(zone);
        System.Diagnostics.Debug.Assert(!_zones.Contains(zone));
        _zones.Add(zone);
        _zonesDirty = true;
    }

    public void UpdateZone(Zone zone)
    {
        ArgumentNullException.ThrowIfNull(zone);
        System.Diagnostics.Debug.Assert(_zones.Contains(zone));
        _zonesDirty = true;
    }

    public void RemoveZone(Zone zone)
    {
        ArgumentNullException.ThrowIfNull(zone);
        var index = _zones.IndexOf(zone);
        System.Diagnostics.Debug.Assert(index >= 0);
        _zones.RemoveAt(index);
    }

    public void Commit()
    {
        if (_zonesDirty)
        {
            _zonesDirty = false;

            // Sort zones by priority from high to low
            _zones.Sort(static (lhs, rhs) => rhs.GetPriority().CompareTo(lhs.GetPriority()));

            // Update cached data
            _zonesData.Clear();
            foreach (var zone in _zones)
            {
                _zonesData.Add(new ZoneData
                {
                    ZoneMask = zone.GetZoneMask(),
                    BoundingBox = zone.GetBoundingBox(),
                    InverseWorldTransform = zone.GetInverseWorldTransform()
                });
            }
        }

        // TODO: port: for (Zone* zone : zones_)
        // TODO: port:     zone->UpdateCachedData();
        // TODO: port: if (defaultZone_)
        // TODO: port:     defaultZone_->UpdateCachedData();
    }

    public CachedDrawableZone QueryZone(Vector3f position, uint zoneMask)
    {
        var minDistanceToOtherZone = MathDefs.LargeValue;
        var distanceToBestZoneBorder = MathDefs.LargeValue;
        Zone? bestZone = null;

        for (var i = 0; i < _zonesData.Count; ++i)
        {
            var data = _zonesData[i];
            if ((data.ZoneMask & zoneMask) == 0)
                continue;

            var localPosition = data.InverseWorldTransform * position;
            var signedDistance = data.BoundingBox.SignedDistanceToPoint(localPosition);

            if (signedDistance > 0.0f)
            {
                // Zone cannot affect point, keep distance for cache
                minDistanceToOtherZone = MathF.Min(minDistanceToOtherZone, signedDistance);
            }
            else if (bestZone is null)
            {
                // Zone may affect point
                bestZone = _zones[i];
                distanceToBestZoneBorder = -signedDistance;
            }
        }

        var cacheInvalidationDistance = MathF.Min(minDistanceToOtherZone, distanceToBestZoneBorder);
        return new CachedDrawableZone(bestZone ?? _defaultZone, position, cacheInvalidationDistance * cacheInvalidationDistance);
    }

    public Zone? GetBackgroundZone()
    {
        return _zones.Count > 0 && _zones[^1].GetPriority() <= 0 ? _zones[^1] : _defaultZone;
    }
}

public sealed class Octree : Component
{
    public const float DefaultOctreeSize = 1000.0f;
    public const uint DefaultOctreeLevels = 8;

    private readonly HashSet<Drawable> _manualDrawables = [];
    private readonly HashSet<Drawable> _drawableUpdates = [];
    private readonly ZoneLookupIndex _zones;
    private BoundingBox _worldBoundingBox = new(-DefaultOctreeSize, DefaultOctreeSize);
    private readonly Octant _rootOctant;
    private uint _numLevels = DefaultOctreeLevels;
    private bool _locked;

    public Octree()
    {
        _rootOctant = new Octant(_worldBoundingBox, 0, parent: null, this);
        _zones = new ZoneLookupIndex();

        RegisterAttribute(
            "Bounding Box Min",
            typeof(Vector3f),
            self => ((Octree)self)._worldBoundingBox.Min,
            (self, value) => ((Octree)self).SetSize(new BoundingBox(value is Vector3f min ? min : -Vector3f.One * DefaultOctreeSize, ((Octree)self)._worldBoundingBox.Max), ((Octree)self)._numLevels),
            -Vector3f.One * DefaultOctreeSize);
        RegisterAttribute(
            "Bounding Box Max",
            typeof(Vector3f),
            self => ((Octree)self)._worldBoundingBox.Max,
            (self, value) => ((Octree)self).SetSize(new BoundingBox(((Octree)self)._worldBoundingBox.Min, value is Vector3f max ? max : Vector3f.One * DefaultOctreeSize), ((Octree)self)._numLevels),
            Vector3f.One * DefaultOctreeSize);
        RegisterAttribute(
            "Number of Levels",
            typeof(uint),
            self => ((Octree)self)._numLevels,
            (self, value) => ((Octree)self).SetSize(((Octree)self)._worldBoundingBox, value is uint levels ? levels : DefaultOctreeLevels),
            DefaultOctreeLevels);
    }

    public BoundingBox WorldBoundingBox => _worldBoundingBox;

    public uint NumLevels => _numLevels;

    public bool Locked => _locked;

    public Octant GetRootOctant() => _rootOctant;

    public uint GetNumLevels() => _numLevels;

    public BoundingBox GetWorldBoundingBox() => _worldBoundingBox;

    public IReadOnlyList<Drawable> GetAllDrawables() => EnumerateDrawables().ToArray();

    public uint GetNumDrawables() => (uint)GetAllDrawables().Count;

    public void SetSize(BoundingBox box, uint numLevels)
    {
        _worldBoundingBox = box;
        this._numLevels = Math.Max(numLevels, 1u);
        _rootOctant.SetRootSize(box);
    }

    public void SetLocked(bool locked)
    {
        this._locked = locked;
    }

    public bool GetLocked() => _locked;

    public void AddManualDrawable(Drawable? drawable)
    {
        if (drawable is null || _locked)
            return;

        _manualDrawables.Add(drawable);
        if (drawable is Zone zone)
            _zones.AddZone(zone);
    }

    public void RemoveManualDrawable(Drawable? drawable)
    {
        if (drawable is null || _locked)
            return;

        _manualDrawables.Remove(drawable);
        CancelUpdate(drawable);
        if (drawable is Zone zone)
            _zones.RemoveZone(zone);
    }

    public void AddDrawable(Drawable? drawable) => AddManualDrawable(drawable);

    public void RemoveDrawable(Drawable? drawable) => RemoveManualDrawable(drawable);

    public void QueueUpdate(Drawable? drawable)
    {
        if (drawable is not null)
            _drawableUpdates.Add(drawable);
    }

    public void CancelUpdate(Drawable? drawable)
    {
        if (drawable is not null)
            _drawableUpdates.Remove(drawable);
    }

    public void Update(float timeStep = 0.0f)
    {
        foreach (var drawable in _drawableUpdates)
            drawable.MarkForUpdate();
        _drawableUpdates.Clear();
    }

    public IReadOnlyList<Drawable> GetDrawables(DrawableFlags drawableFlags = DrawableFlags.Any, uint viewMask = Drawable.DefaultViewMask)
    {
        return EnumerateDrawables()
            .Where(drawable => Matches(drawable, drawableFlags, viewMask))
            .ToArray();
    }

    public void GetDrawables(OctreeQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        query.Result.Clear();
        var intersection = query.TestOctant(_worldBoundingBox, inside: false);
        if (intersection != Intersection.Outside)
            query.TestDrawables(GetAllDrawables(), intersection == Intersection.Inside);
    }

    public IReadOnlyList<Drawable> GetDrawables(Vector3f point, DrawableFlags drawableFlags = DrawableFlags.Any, uint viewMask = Drawable.DefaultViewMask)
    {
        return GetDrawables(drawableFlags, viewMask)
            .Where(drawable => drawable.GetWorldBoundingBox().IsInside(point) != Intersection.Outside)
            .ToArray();
    }

    public IReadOnlyList<Drawable> GetDrawables(BoundingBox box, DrawableFlags drawableFlags = DrawableFlags.Any, uint viewMask = Drawable.DefaultViewMask)
    {
        return GetDrawables(drawableFlags, viewMask)
            .Where(drawable => drawable.GetWorldBoundingBox().IsInside(box) != Intersection.Outside)
            .ToArray();
    }

    public IReadOnlyList<Drawable> GetDrawables(Sphere sphere, DrawableFlags drawableFlags = DrawableFlags.Any, uint viewMask = Drawable.DefaultViewMask)
    {
        return GetDrawables(drawableFlags, viewMask)
            .Where(drawable => drawable.GetWorldBoundingBox().IsInside(sphere) != Intersection.Outside)
            .ToArray();
    }

    public IReadOnlyList<RayQueryResult> Raycast(Ray ray, RayQueryLevel level = RayQueryLevel.RayAabb, float maxDistance = float.PositiveInfinity, DrawableFlags drawableFlags = DrawableFlags.Any, uint viewMask = Drawable.DefaultViewMask)
    {
        return GetDrawables(drawableFlags, viewMask)
            .SelectMany(drawable => CreateRayQueryResults(ray, level, drawable, maxDistance))
            .OrderBy(static result => result.Distance)
            .ToArray();
    }

    public void Raycast(RayOctreeQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        query.Result.Clear();
        foreach (var drawable in GetDrawables(query.DrawableFlags, query.ViewMask))
        {
            foreach (var result in CreateRayQueryResults(query.Ray, query.Level, drawable, query.MaxDistance))
                query.Result.Add(result);
        }

        query.Result.Sort(static (left, right) => left.Distance.CompareTo(right.Distance));
    }

    public RayQueryResult? RaycastSingle(Ray ray, RayQueryLevel level = RayQueryLevel.RayAabb, float maxDistance = float.PositiveInfinity, DrawableFlags drawableFlags = DrawableFlags.Any, uint viewMask = Drawable.DefaultViewMask)
    {
        var results = Raycast(ray, level, maxDistance, drawableFlags, viewMask);
        return results.Count > 0 ? results[0] : null;
    }

    public void RaycastSingle(RayOctreeQuery query)
    {
        Raycast(query);
        while (query.Result.Count > 1)
            query.Result.RemoveAt(query.Result.Count - 1);
    }

    public CachedDrawableZone QueryZone(Drawable drawable)
    {
        ArgumentNullException.ThrowIfNull(drawable);
        return QueryZone(drawable.GetWorldBoundingBox().Center, drawable.GetZoneMask());
    }

    public CachedDrawableZone QueryZone(Vector3f drawablePosition, uint zoneMask)
    {
        return _zones.QueryZone(drawablePosition, zoneMask);
    }

    public Zone? GetBackgroundZone() => _zones.GetBackgroundZone();

    public void DrawDebugGeometry(bool depthTest)
    {
    }

    protected internal override void OnSceneSet(Scene? previousScene, Scene? scene)
    {
        if (scene is null)
        {
            _manualDrawables.Clear();
            _drawableUpdates.Clear();
        }
    }

    private IEnumerable<Drawable> EnumerateDrawables()
    {
        var result = new HashSet<Drawable>(_manualDrawables);
        if (Scene is { } scene)
        {
            foreach (var drawable in scene.GetAllComponents().Values.OfType<Drawable>())
                result.Add(drawable);
        }

        return result;
    }

    private static bool Matches(Drawable drawable, DrawableFlags drawableFlags, uint viewMask)
    {
        return (drawable.GetDrawableFlags() & drawableFlags) != 0 && (drawable.GetViewMask() & viewMask) != 0;
    }

    private static IEnumerable<RayQueryResult> CreateRayQueryResults(Ray ray, RayQueryLevel level, Drawable drawable, float maxDistance)
    {
        if (drawable is Skybox)
            yield break;

        if (drawable is CustomGeometry customGeometry)
        {
            foreach (var result in customGeometry.CreateRayQueryResults(ray, level, maxDistance))
                yield return result;
            yield break;
        }

        if (drawable is StaticModelGroup group)
        {
            foreach (var result in group.CreateRayQueryResults(ray, maxDistance))
                yield return result;
            yield break;
        }

        var distance = ray.HitDistance(drawable.GetWorldBoundingBox());
        if (float.IsPositiveInfinity(distance) || distance > maxDistance)
            yield break;

        yield return new RayQueryResult
        {
            Drawable = drawable,
            Node = drawable.IsAttached ? drawable.Node : null,
            Distance = distance,
            Position = ray.Origin + ray.Direction * distance,
            Normal = -ray.Direction,
            TextureUV = Vector2f.Zero,
            SubObject = 0
        };
    }
}
