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
    public Vector3 Position { get; init; }

    public Vector3 Normal { get; init; }

    public Vector2 TextureUV { get; init; }

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

    private readonly HashSet<Drawable> manualDrawables = [];
    private readonly HashSet<Drawable> drawableUpdates = [];
    private BoundingBox worldBoundingBox = new(-DefaultOctreeSize, DefaultOctreeSize);
    private readonly Octant rootOctant;
    private uint numLevels = DefaultOctreeLevels;
    private bool locked;

    public Octree()
    {
        rootOctant = new Octant(worldBoundingBox, 0, parent: null, this);

        RegisterAttribute(
            "Bounding Box Min",
            typeof(Vector3),
            self => ((Octree)self).worldBoundingBox.Min,
            (self, value) => ((Octree)self).SetSize(new BoundingBox(value is Vector3 min ? min : -Vector3.One * DefaultOctreeSize, ((Octree)self).worldBoundingBox.Max), ((Octree)self).numLevels),
            -Vector3.One * DefaultOctreeSize);
        RegisterAttribute(
            "Bounding Box Max",
            typeof(Vector3),
            self => ((Octree)self).worldBoundingBox.Max,
            (self, value) => ((Octree)self).SetSize(new BoundingBox(((Octree)self).worldBoundingBox.Min, value is Vector3 max ? max : Vector3.One * DefaultOctreeSize), ((Octree)self).numLevels),
            Vector3.One * DefaultOctreeSize);
        RegisterAttribute(
            "Number of Levels",
            typeof(uint),
            self => ((Octree)self).numLevels,
            (self, value) => ((Octree)self).SetSize(((Octree)self).worldBoundingBox, value is uint levels ? levels : DefaultOctreeLevels),
            DefaultOctreeLevels);
    }

    public BoundingBox WorldBoundingBox => worldBoundingBox;

    public uint NumLevels => numLevels;

    public bool Locked => locked;

    public Octant GetRootOctant() => rootOctant;

    public uint GetNumLevels() => numLevels;

    public BoundingBox GetWorldBoundingBox() => worldBoundingBox;

    public IReadOnlyList<Drawable> GetAllDrawables() => EnumerateDrawables().ToArray();

    public uint GetNumDrawables() => (uint)GetAllDrawables().Count;

    public void SetSize(BoundingBox box, uint numLevels)
    {
        worldBoundingBox = box;
        this.numLevels = Math.Max(numLevels, 1u);
        rootOctant.SetRootSize(box);
    }

    public void SetLocked(bool locked)
    {
        this.locked = locked;
    }

    public bool GetLocked() => locked;

    public void AddManualDrawable(Drawable? drawable)
    {
        if (drawable is null || locked)
            return;

        manualDrawables.Add(drawable);
    }

    public void RemoveManualDrawable(Drawable? drawable)
    {
        if (drawable is null || locked)
            return;

        manualDrawables.Remove(drawable);
        CancelUpdate(drawable);
    }

    public void AddDrawable(Drawable? drawable) => AddManualDrawable(drawable);

    public void RemoveDrawable(Drawable? drawable) => RemoveManualDrawable(drawable);

    public void QueueUpdate(Drawable? drawable)
    {
        if (drawable is not null)
            drawableUpdates.Add(drawable);
    }

    public void CancelUpdate(Drawable? drawable)
    {
        if (drawable is not null)
            drawableUpdates.Remove(drawable);
    }

    public void Update(float timeStep = 0.0f)
    {
        foreach (var drawable in drawableUpdates)
            drawable.MarkForUpdate();
        drawableUpdates.Clear();
    }

    public IReadOnlyList<Drawable> GetDrawables(DrawableFlags drawableFlags = DrawableFlags.Any, uint viewMask = Drawable.DefaultViewMask)
    {
        return EnumerateDrawables()
            .Where(drawable => Matches(drawable, drawableFlags, viewMask))
            .ToArray();
    }

    public IReadOnlyList<Drawable> GetDrawables(Vector3 point, DrawableFlags drawableFlags = DrawableFlags.Any, uint viewMask = Drawable.DefaultViewMask)
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

    public object? QueryZone(Vector3 drawablePosition, uint zoneMask) => null;

    public object? GetBackgroundZone() => null;

    public void DrawDebugGeometry(bool depthTest)
    {
    }

    protected internal override void OnSceneSet(Scene? previousScene, Scene? scene)
    {
        if (scene is null)
        {
            manualDrawables.Clear();
            drawableUpdates.Clear();
        }
    }

    private IEnumerable<Drawable> EnumerateDrawables()
    {
        var result = new HashSet<Drawable>(manualDrawables);
        if (IsAttached)
        {
            foreach (var drawable in Scene.GetAllComponents().Values.OfType<Drawable>())
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
            TextureUV = Vector2.Zero,
            SubObject = 0
        };
    }
}
