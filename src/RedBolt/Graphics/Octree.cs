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
    public Vector3f Position { get; init; }

    public Vector3f Normal { get; init; }

    public Vector2f TextureUV { get; init; }

    public float Distance { get; init; }

    public Drawable? Drawable { get; init; }

    public Node? Node { get; init; }

    public uint SubObject { get; init; }
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

public sealed class Octree : Component
{
    public const float DefaultOctreeSize = 1000.0f;
    public const uint DefaultOctreeLevels = 8;

    private readonly HashSet<Drawable> _manualDrawables = [];
    private readonly HashSet<Drawable> _drawableUpdates = [];
    private BoundingBox _worldBoundingBox = new(-DefaultOctreeSize, DefaultOctreeSize);
    private readonly Octant _rootOctant;
    private uint _numLevels = DefaultOctreeLevels;
    private bool _locked;

    public Octree()
    {
        _rootOctant = new Octant(_worldBoundingBox, 0, parent: null, this);

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
    }

    public void RemoveManualDrawable(Drawable? drawable)
    {
        if (drawable is null || _locked)
            return;

        _manualDrawables.Remove(drawable);
        CancelUpdate(drawable);
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

    public RayQueryResult? RaycastSingle(Ray ray, RayQueryLevel level = RayQueryLevel.RayAabb, float maxDistance = float.PositiveInfinity, DrawableFlags drawableFlags = DrawableFlags.Any, uint viewMask = Drawable.DefaultViewMask)
    {
        return Raycast(ray, level, maxDistance, drawableFlags, viewMask).FirstOrDefault();
    }

    public object? QueryZone(Drawable drawable) => null;

    public object? QueryZone(Vector3f drawablePosition, uint zoneMask) => null;

    public object? GetBackgroundZone() => null;

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
