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

/// <summary>Octree component. Should be added only to the root scene node.</summary>
public sealed class Octant
{
    private readonly Octant?[] _children = new Octant?[8];
    private readonly List<Drawable> _drawables = [];
    private Vector3f _center;
    private Vector3f _halfSize;
    private uint _numDrawables;

    internal Octant(BoundingBox box, uint level, Octant? parent, Octree octree)
    {
        WorldBoundingBox = box;
        CullingBox = box;
        Level = level;
        Parent = parent;
        Octree = octree;
        Initialize(box);
    }

    public BoundingBox WorldBoundingBox { get; private set; }

    public BoundingBox CullingBox { get; private set; }

    public uint Level { get; }

    public Octant? Parent { get; }

    public Octree Octree { get; }

    public uint NumDrawables => _numDrawables;

    public bool IsEmpty() => NumDrawables == 0;

    public BoundingBox GetWorldBoundingBox() => WorldBoundingBox;

    public BoundingBox GetCullingBox() => CullingBox;

    public uint GetLevel() => Level;

    public Octant? GetParent() => Parent;

    public Octree GetOctree() => Octree;

    public uint GetNumDrawables() => NumDrawables;

    internal void DrawDebugGeometry(DebugRenderer? debug, bool depthTest)
    {
        if (debug is null || !debug.IsInside(WorldBoundingBox))
            return;

        debug.AddBoundingBox(WorldBoundingBox, new Color(0.25f, 0.25f, 0.25f), depthTest);

        foreach (var child in _children)
            child?.DrawDebugGeometry(debug, depthTest);
    }

    public Octant GetOrCreateChild(uint index)
    {
        if (index >= _children.Length)
            throw new ArgumentOutOfRangeException(nameof(index));
        if (_children[index] is not null)
            return _children[index]!;

        var newMin = WorldBoundingBox.Min;
        var newMax = WorldBoundingBox.Max;
        var oldCenter = WorldBoundingBox.Center;

        if ((index & 1u) != 0)
            newMin.X = oldCenter.X;
        else
            newMax.X = oldCenter.X;
        if ((index & 2u) != 0)
            newMin.Y = oldCenter.Y;
        else
            newMax.Y = oldCenter.Y;
        if ((index & 4u) != 0)
            newMin.Z = oldCenter.Z;
        else
            newMax.Z = oldCenter.Z;

        return _children[index] = new Octant(new BoundingBox(newMin, newMax), Level + 1, this, Octree);
    }

    public void DeleteChild(uint index)
    {
        if (index >= _children.Length)
            throw new ArgumentOutOfRangeException(nameof(index));
        _children[index] = null;
    }

    public void AddDrawable(Drawable drawable)
    {
        ArgumentNullException.ThrowIfNull(drawable);
        if (_drawables.Contains(drawable))
            return;
        drawable.SetOctant(this);
        _drawables.Add(drawable);
        IncDrawableCount();
    }

    public void RemoveDrawable(Drawable drawable, bool resetOctant = true)
    {
        ArgumentNullException.ThrowIfNull(drawable);
        if (!_drawables.Remove(drawable))
            return;
        if (resetOctant)
            drawable.SetOctant(null);
        DecDrawableCount();
    }

    public bool CheckDrawableFit(BoundingBox box)
    {
        var boxSize = box.Size;
        if (Level >= Octree.GetNumLevels() || boxSize.X >= _halfSize.X || boxSize.Y >= _halfSize.Y || boxSize.Z >= _halfSize.Z)
            return true;

        if (box.Min.X <= WorldBoundingBox.Min.X - 0.5f * _halfSize.X
            || box.Max.X >= WorldBoundingBox.Max.X + 0.5f * _halfSize.X
            || box.Min.Y <= WorldBoundingBox.Min.Y - 0.5f * _halfSize.Y
            || box.Max.Y >= WorldBoundingBox.Max.Y + 0.5f * _halfSize.Y
            || box.Min.Z <= WorldBoundingBox.Min.Z - 0.5f * _halfSize.Z
            || box.Max.Z >= WorldBoundingBox.Max.Z + 0.5f * _halfSize.Z)
            return true;

        return false;
    }

    public void InsertDrawable(Drawable drawable)
    {
        ArgumentNullException.ThrowIfNull(drawable);
        var box = drawable.GetWorldBoundingBox();
        var insertHere = ReferenceEquals(this, Octree.GetRootOctant())
            ? (!drawable.IsOccludee() || CullingBox.IsInside(box) != Intersection.Inside || CheckDrawableFit(box))
            : CheckDrawableFit(box);

        if (insertHere)
        {
            var oldOctant = drawable.GetOctant();
            if (!ReferenceEquals(oldOctant, this))
            {
                AddDrawable(drawable);
                oldOctant?.RemoveDrawable(drawable, resetOctant: false);
            }
            return;
        }

        var boxCenter = box.Center;
        var x = boxCenter.X < _center.X ? 0u : 1u;
        var y = boxCenter.Y < _center.Y ? 0u : 2u;
        var z = boxCenter.Z < _center.Z ? 0u : 4u;
        GetOrCreateChild(x + y + z).InsertDrawable(drawable);
    }

    internal void SetRootSize(BoundingBox box)
    {
        for (uint i = 0; i < _children.Length; ++i)
            DeleteChild(i);
        WorldBoundingBox = box;
        Initialize(box);
        _numDrawables = (uint)_drawables.Count;
    }

    private void Initialize(BoundingBox box)
    {
        WorldBoundingBox = box;
        _center = box.Center;
        _halfSize = box.Size * 0.5f;
        CullingBox = new BoundingBox(WorldBoundingBox.Min - _halfSize, WorldBoundingBox.Max + _halfSize);
    }

    private void IncDrawableCount()
    {
        ++_numDrawables;
        Parent?.IncDrawableCount();
    }

    private void DecDrawableCount()
    {
        var parent = Parent;
        --_numDrawables;
        if (_numDrawables == 0 && parent is not null)
        {
            var index = (uint)((_center.X >= parent._center.X ? 1 : 0)
                | (_center.Y >= parent._center.Y ? 2 : 0)
                | (_center.Z >= parent._center.Z ? 4 : 0));
            parent.DeleteChild(index);
        }
        parent?.DecDrawableCount();
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

        foreach (var zone in _zones)
            zone.UpdateCachedData();
        _defaultZone?.UpdateCachedData();
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
    private readonly List<Drawable> _drawables = [];
    private readonly HashSet<Drawable> _drawableUpdates = [];
    private readonly Dictionary<Node, Transform> _pendingNodeTransforms = [];
    private readonly ZoneLookupIndex _zones;
    private BoundingBox _worldBoundingBox = new(-DefaultOctreeSize, DefaultOctreeSize);
    private readonly Octant _rootOctant;
    private uint _numLevels = DefaultOctreeLevels;
    private bool _locked;

    public Octree()
    {
        _rootOctant = new Octant(_worldBoundingBox, 0, parent: null, this);
        _zones = new ZoneLookupIndex();

        // If the engine is running headless, subscribe to RenderUpdate events for manually updating the octree
        // to allow raycasts and animation update.
        if (Context.Instance.GetSubsystem<Graphics>() is null)
            SubscribeToEvent<CoreEvents.RenderUpdateEvent>(HandleRenderUpdate);

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

    public IReadOnlyList<Drawable> GetAllDrawables() => _drawables;

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
        if (drawable is null || drawable.GetOctant() is not null)
            return;

        AddDrawable(drawable);
        if (drawable.GetOctant() is { } octant && ReferenceEquals(octant.GetOctree(), this))
            _manualDrawables.Add(drawable);
    }

    public void RemoveManualDrawable(Drawable? drawable)
    {
        if (drawable is null)
            return;

        CancelUpdate(drawable);
        var octant = drawable.GetOctant();
        if (octant is not null && ReferenceEquals(octant.Octree, this))
            RemoveDrawable(drawable, octant);
    }

    public void AddDrawable(Drawable? drawable)
    {
        if (drawable is null || _locked || drawable.GetDrawableIndex() != uint.MaxValue)
            return;

        var index = (uint)_drawables.Count;
        _drawables.Add(drawable);
        drawable.SetDrawableIndex(index);
        _rootOctant.InsertDrawable(drawable);

        if (drawable is Zone zone)
        {
            _zones.AddZone(zone);
            zone.ClearDrawablesZone();
        }
    }

    public void RemoveDrawable(Drawable? drawable)
    {
        if (drawable is null)
            return;

        CancelUpdate(drawable);
        var octant = drawable.GetOctant();
        if (octant is not null && ReferenceEquals(octant.GetOctree(), this))
            RemoveDrawable(drawable, octant);
    }

    public void QueueUpdate(Drawable? drawable)
    {
        if (drawable is not null)
            _drawableUpdates.Add(drawable);
    }

    public void QueueNodeTransformUpdate(Node node, Transform transform)
    {
        _pendingNodeTransforms[node] = transform;
    }

    public void CancelUpdate(Drawable? drawable)
    {
        if (drawable is not null)
            _drawableUpdates.Remove(drawable);
    }

    public void MarkZoneDirty(Zone zone)
    {
        ArgumentNullException.ThrowIfNull(zone);
        _zones.UpdateZone(zone);
    }

    public void Update(float timeStep = 0.0f)
    {
        foreach (var drawable in _drawableUpdates)
        {
            drawable.MarkForUpdate();
            var octant = drawable.GetOctant();
            if (octant is null || !ReferenceEquals(octant.GetOctree(), this))
                continue;

            var box = drawable.GetWorldBoundingBox();
            if (drawable.IsOccludee()
                && octant.GetCullingBox().IsInside(box) == Intersection.Inside
                && octant.CheckDrawableFit(box))
                continue;

            _rootOctant.InsertDrawable(drawable);
        }
        _drawableUpdates.Clear();

        foreach (var pair in _pendingNodeTransforms)
        {
            var transform = pair.Value;
            pair.Key.SetTransform(transform.Position, transform.Rotation, transform.Scale);
        }
        _pendingNodeTransforms.Clear();

        if (Scene is { } scene)
        {
            var eventData = new SceneEvents.SceneDrawableUpdateFinishedEvent
            {
                Scene = scene,
                TimeStep = timeStep
            };
            Context.Instance.SendEvent(ref eventData);
        }

        _zones.Commit();
    }

    public void RemoveDrawable(Drawable drawable, Octant octant)
    {
        ArgumentNullException.ThrowIfNull(drawable);
        ArgumentNullException.ThrowIfNull(octant);
        octant.RemoveDrawable(drawable);
        if (drawable is Zone zone)
            _zones.RemoveZone(zone);

        var index = drawable.GetDrawableIndex();
        if (index < _drawables.Count && ReferenceEquals(_drawables[(int)index], drawable))
        {
            var lastIndex = _drawables.Count - 1;
            var lastDrawable = _drawables[lastIndex];
            _drawables.RemoveAt(lastIndex);
            if (index < lastIndex)
            {
                _drawables[(int)index] = lastDrawable;
                lastDrawable.SetDrawableIndex(index);
            }
        }

        _manualDrawables.Remove(drawable);
        drawable.SetDrawableIndex(uint.MaxValue);
        drawable.GetMutableCachedZone() = new CachedDrawableZone();
    }

    public IReadOnlyList<Drawable> GetDrawables(DrawableFlags drawableFlags = DrawableFlags.Any, uint viewMask = Drawable.DefaultViewMask)
    {
        return _drawables
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
        var debug = GetComponent<DebugRenderer>();
        _rootOctant.DrawDebugGeometry(debug, depthTest);
    }

    protected internal override void OnSceneSet(Scene? previousScene, Scene? scene)
    {
        base.OnSceneSet(previousScene, scene);

        if (previousScene is not null)
        {
            UnsubscribeFromEvent<SceneEvents.WorldOriginUpdateEvent>(previousScene);
            UnsubscribeFromEvent<SceneEvents.WorldOriginPostUpdateEvent>(previousScene);
        }

        if (scene is not null)
        {
            SubscribeToEvent<SceneEvents.WorldOriginUpdateEvent>(scene, HandleWorldOriginUpdate);
            SubscribeToEvent<SceneEvents.WorldOriginPostUpdateEvent>(scene, HandleWorldOriginPostUpdate);
        }

        if (scene is null)
        {
            _manualDrawables.Clear();
            _drawableUpdates.Clear();
        }
    }

    private void HandleWorldOriginUpdate(ref SceneEvents.WorldOriginUpdateEvent eventData)
    {
        foreach (var drawable in _drawables)
            drawable.UpdateWorldOrigin(eventData.OldOrigin, eventData.NewOrigin, eventData.Delta);
    }

    private void HandleWorldOriginPostUpdate(ref SceneEvents.WorldOriginPostUpdateEvent eventData)
    {
        foreach (var drawable in _drawables)
            drawable.PostUpdateWorldOrigin(eventData.OldOrigin, eventData.NewOrigin, eventData.Delta);
    }

    private void HandleRenderUpdate(ref CoreEvents.RenderUpdateEvent eventData)
    {
        var scene = Scene;
        if (scene is null || !scene.IsUpdateEnabled())
            return;

        Update(eventData.TimeStep);
    }

    private static bool Matches(Drawable drawable, DrawableFlags drawableFlags, uint viewMask)
    {
        return (drawable.GetDrawableFlags() & drawableFlags) != 0 && (drawable.GetViewMask() & viewMask) != 0;
    }

    private static IEnumerable<RayQueryResult> CreateRayQueryResults(Ray ray, RayQueryLevel level, Drawable drawable, float maxDistance)
    {
        if (drawable is Skybox)
            yield break;

        if (drawable is Light light)
        {
            var results = new List<RayQueryResult>();
            light.ProcessRayQuery(new RayOctreeQuery(results, ray, level, maxDistance));
            foreach (var result in results)
                yield return result;
            yield break;
        }

        if (drawable is CustomGeometry customGeometry)
        {
            foreach (var result in customGeometry.CreateRayQueryResults(ray, level, maxDistance))
                yield return result;
            yield break;
        }

        if (drawable is StaticModelGroup group)
        {
            var results = new List<RayQueryResult>();
            group.ProcessRayQuery(new RayOctreeQuery(results, ray, level, maxDistance));
            foreach (var result in results)
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
