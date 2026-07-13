namespace RedBolt;

/// <summary>Base class for octree queries.</summary>
public abstract class OctreeQuery : IDisposable
{
    /// <summary>Construct with query parameters.</summary>
    public OctreeQuery(List<Drawable> result, DrawableFlags drawableFlags, uint viewMask)
    {
        Result = result;
        DrawableFlags = drawableFlags;
        ViewMask = viewMask;
    }

    /// <summary>Result vector reference.</summary>
    public List<Drawable> Result;
    /// <summary>Drawable flags to include.</summary>
    public DrawableFlags DrawableFlags;
    /// <summary>Drawable layers to include.</summary>
    public uint ViewMask;

    /// <summary>Intersection test for an octant.</summary>
    public abstract Intersection TestOctant(BoundingBox box, bool inside);
    /// <summary>Intersection test for drawables.</summary>
    public abstract void TestDrawables(IReadOnlyList<Drawable> drawables, bool inside);

    /// <summary>Destruct.</summary>
    public virtual void Dispose()
    {
    }

}

/// <summary>Point octree query.</summary>
public sealed class PointOctreeQuery : OctreeQuery
{
    /// <summary>Construct with point and query parameters.</summary>
    public PointOctreeQuery(List<Drawable> result, Vector3f point,
        DrawableFlags drawableFlags = DrawableFlags.Any, uint viewMask = Drawable.DefaultViewMask)
        : base(result, drawableFlags, viewMask)
    {
        Point = point;
    }

    /// <summary>Point.</summary>
    public Vector3f Point;

    /// <summary>Intersection test for an octant.</summary>
    public override Intersection TestOctant(BoundingBox box, bool inside)
    {
        return inside ? Intersection.Inside : box.IsInside(Point);
    }

    /// <summary>Intersection test for drawables.</summary>
    public override void TestDrawables(IReadOnlyList<Drawable> drawables, bool inside)
    {
        for (var i = 0; i < drawables.Count; ++i)
        {
            var drawable = drawables[i];
            if ((drawable.GetDrawableFlags() & DrawableFlags) != DrawableFlags.Undefined &&
                (drawable.GetViewMask() & ViewMask) != 0 &&
                (inside || drawable.GetWorldBoundingBox().IsInside(Point) != Intersection.Outside))
                Result.Add(drawable);
        }
    }
}

/// <summary>Sphere octree query.</summary>
public sealed class SphereOctreeQuery : OctreeQuery
{
    /// <summary>Construct with sphere and query parameters.</summary>
    public SphereOctreeQuery(List<Drawable> result, Sphere sphere,
        DrawableFlags drawableFlags = DrawableFlags.Any, uint viewMask = Drawable.DefaultViewMask)
        : base(result, drawableFlags, viewMask)
    {
        Sphere = sphere;
    }

    /// <summary>Sphere.</summary>
    public Sphere Sphere;

    /// <summary>Intersection test for an octant.</summary>
    public override Intersection TestOctant(BoundingBox box, bool inside)
    {
        return inside ? Intersection.Inside : Sphere.IsInside(box);
    }

    /// <summary>Intersection test for drawables.</summary>
    public override void TestDrawables(IReadOnlyList<Drawable> drawables, bool inside)
    {
        for (var i = 0; i < drawables.Count; ++i)
        {
            var drawable = drawables[i];
            if ((drawable.GetDrawableFlags() & DrawableFlags) != DrawableFlags.Undefined &&
                (drawable.GetViewMask() & ViewMask) != 0 &&
                (inside || Sphere.IsInsideFast(drawable.GetWorldBoundingBox()) != Intersection.Outside))
                Result.Add(drawable);
        }
    }
}

/// <summary>Bounding box octree query.</summary>
public sealed class BoxOctreeQuery : OctreeQuery
{
    /// <summary>Construct with bounding box and query parameters.</summary>
    public BoxOctreeQuery(List<Drawable> result, BoundingBox box,
        DrawableFlags drawableFlags = DrawableFlags.Any, uint viewMask = Drawable.DefaultViewMask)
        : base(result, drawableFlags, viewMask)
    {
        Box = box;
    }

    /// <summary>Bounding box.</summary>
    public BoundingBox Box;

    /// <summary>Intersection test for an octant.</summary>
    public override Intersection TestOctant(BoundingBox box, bool inside)
    {
        return inside ? Intersection.Inside : Box.IsInside(box);
    }

    /// <summary>Intersection test for drawables.</summary>
    public override void TestDrawables(IReadOnlyList<Drawable> drawables, bool inside)
    {
        for (var i = 0; i < drawables.Count; ++i)
        {
            var drawable = drawables[i];
            if ((drawable.GetDrawableFlags() & DrawableFlags) != DrawableFlags.Undefined &&
                (drawable.GetViewMask() & ViewMask) != 0 &&
                (inside || Box.IsInsideFast(drawable.GetWorldBoundingBox()) != Intersection.Outside))
                Result.Add(drawable);
        }
    }
}

/// <summary>Frustum octree query.</summary>
public sealed class FrustumOctreeQuery : OctreeQuery
{
    /// <summary>Construct with frustum and query parameters.</summary>
    public FrustumOctreeQuery(List<Drawable> result, Frustum frustum,
        DrawableFlags drawableFlags = DrawableFlags.Any, uint viewMask = Drawable.DefaultViewMask)
        : base(result, drawableFlags, viewMask)
    {
        Frustum = frustum;
    }

    /// <summary>Frustum.</summary>
    public Frustum Frustum;

    /// <summary>Intersection test for an octant.</summary>
    public override Intersection TestOctant(BoundingBox box, bool inside)
    {
        return inside ? Intersection.Inside : Frustum.IsInside(box);
    }

    /// <summary>Intersection test for drawables.</summary>
    public override void TestDrawables(IReadOnlyList<Drawable> drawables, bool inside)
    {
        for (var i = 0; i < drawables.Count; ++i)
        {
            var drawable = drawables[i];
            if ((drawable.GetDrawableFlags() & DrawableFlags) != DrawableFlags.Undefined &&
                (drawable.GetViewMask() & ViewMask) != 0 &&
                (inside || Frustum.IsInsideFast(drawable.GetWorldBoundingBox()) != Intersection.Outside))
                Result.Add(drawable);
        }
    }
}

/// <summary>General octree query result. Used for Lua bindings only.</summary>
public struct OctreeQueryResult : IEquatable<OctreeQueryResult>
{
    /// <summary>Drawable.</summary>
    public Drawable? Drawable;
    /// <summary>Scene node.</summary>
    public Node? Node;

    /// <summary>Test for inequality, added to prevent GCC from complaining.</summary>
    public bool Equals(OctreeQueryResult other) => ReferenceEquals(Drawable, other.Drawable) && ReferenceEquals(Node, other.Node);

    public override bool Equals(object? obj) => obj is OctreeQueryResult other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Drawable, Node);

    public static bool operator !=(OctreeQueryResult left, OctreeQueryResult right) => !left.Equals(right);

    public static bool operator ==(OctreeQueryResult left, OctreeQueryResult right) => left.Equals(right);
}

/// <summary>Raycast query.</summary>
public sealed class RayOctreeQuery : IDisposable
{
    private readonly List<RayQueryResult> _resultStorage = [];

    /// <summary>Construct with ray and query parameters.</summary>
    public RayOctreeQuery(List<RayQueryResult> result, Ray ray, RayQueryLevel level = RayQueryLevel.RayTriangle,
        float maxDistance = float.PositiveInfinity, DrawableFlags drawableFlags = DrawableFlags.Any,
        uint viewMask = Drawable.DefaultViewMask)
    {
        Result = result;
        Ray = ray;
        DrawableFlags = drawableFlags;
        ViewMask = viewMask;
        MaxDistance = maxDistance;
        Level = level;
    }

    /// <summary>Construct with ray and query parameters and use builtin storage for results.</summary>
    public RayOctreeQuery(Ray ray, RayQueryLevel level = RayQueryLevel.RayTriangle,
        float maxDistance = float.PositiveInfinity, DrawableFlags drawableFlags = DrawableFlags.Any,
        uint viewMask = Drawable.DefaultViewMask)
    {
        Result = _resultStorage;
        Ray = ray;
        DrawableFlags = drawableFlags;
        ViewMask = viewMask;
        MaxDistance = maxDistance;
        Level = level;
    }

    /// <summary>Result vector reference.</summary>
    public List<RayQueryResult> Result;
    /// <summary>Ray.</summary>
    public Ray Ray;
    /// <summary>Drawable flags to include.</summary>
    public DrawableFlags DrawableFlags;
    /// <summary>Drawable layers to include.</summary>
    public uint ViewMask;
    /// <summary>Maximum ray distance.</summary>
    public float MaxDistance;
    /// <summary>Raycast detail level.</summary>
    public RayQueryLevel Level;

    /// <summary>Destruct.</summary>
    public void Dispose()
    {
    }
}

/// <summary>All-content octree query.</summary>
public sealed class AllContentOctreeQuery : OctreeQuery
{
    /// <summary>Construct.</summary>
    public AllContentOctreeQuery(List<Drawable> result, DrawableFlags drawableFlags, uint viewMask)
        : base(result, drawableFlags, viewMask)
    {
    }

    /// <summary>Intersection test for an octant.</summary>
    public override Intersection TestOctant(BoundingBox box, bool inside) => Intersection.Inside;

    /// <summary>Intersection test for drawables.</summary>
    public override void TestDrawables(IReadOnlyList<Drawable> drawables, bool inside)
    {
        for (var i = 0; i < drawables.Count; ++i)
        {
            var drawable = drawables[i];
            if ((drawable.GetDrawableFlags() & DrawableFlags) != DrawableFlags.Undefined &&
                (drawable.GetViewMask() & ViewMask) != 0)
                Result.Add(drawable);
        }
    }
}
