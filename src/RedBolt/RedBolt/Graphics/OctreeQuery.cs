namespace RedBolt.Graphics;

using RedBolt.Math;
using RedBolt.Scene;

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

    /// <summary>Destruct.</summary>
    public virtual void Dispose()
    {
    }

    /// <summary>Intersection test for an octant.</summary>
    public abstract Intersection TestOctant(in BoundingBox box, bool inside);

    /// <summary>Intersection test for drawables.</summary>
    public abstract void TestDrawables(IReadOnlyList<Drawable> drawables, bool inside);

    /// <summary>Result vector reference.</summary>
    public List<Drawable> Result;

    /// <summary>Drawable flags to include.</summary>
    public DrawableFlags DrawableFlags;

    /// <summary>Drawable layers to include.</summary>
    public uint ViewMask;
}

/// <summary>Point octree query.</summary>
public class PointOctreeQuery : OctreeQuery
{
    /// <summary>Construct with point and query parameters.</summary>
    public PointOctreeQuery(List<Drawable> result, in Vector3f point,
        DrawableFlags drawableFlags = DrawableFlags.Any, uint viewMask = Drawable.DefaultViewMask)
        : base(result, drawableFlags, viewMask)
    {
        Point = point;
    }

    /// <summary>Intersection test for an octant.</summary>
    public override Intersection TestOctant(in BoundingBox box, bool inside)
    {
        if (inside)
            return Intersection.Inside;
        else
            return box.IsInside(Point);
    }

    /// <summary>Intersection test for drawables.</summary>
    public override void TestDrawables(IReadOnlyList<Drawable> drawables, bool inside)
    {
        for (var i = 0; i < drawables.Count; ++i)
        {
            var drawable = drawables[i];

            if ((drawable.GetDrawableFlags() & DrawableFlags) != 0 && (drawable.GetViewMask() & ViewMask) != 0)
            {
                if (inside || drawable.GetWorldBoundingBox().IsInside(Point) != Intersection.Outside)
                    Result.Add(drawable);
            }
        }
    }

    /// <summary>Point.</summary>
    public Vector3f Point;
}

/// <summary>Sphere octree query.</summary>
public class SphereOctreeQuery : OctreeQuery
{
    /// <summary>Construct with sphere and query parameters.</summary>
    public SphereOctreeQuery(List<Drawable> result, in Sphere sphere,
        DrawableFlags drawableFlags = DrawableFlags.Any, uint viewMask = Drawable.DefaultViewMask)
        : base(result, drawableFlags, viewMask)
    {
        Sphere = sphere;
    }

    /// <summary>Intersection test for an octant.</summary>
    public override Intersection TestOctant(in BoundingBox box, bool inside)
    {
        if (inside)
            return Intersection.Inside;
        else
            return Sphere.IsInside(box);
    }

    /// <summary>Intersection test for drawables.</summary>
    public override void TestDrawables(IReadOnlyList<Drawable> drawables, bool inside)
    {
        for (var i = 0; i < drawables.Count; ++i)
        {
            var drawable = drawables[i];

            if ((drawable.GetDrawableFlags() & DrawableFlags) != 0 && (drawable.GetViewMask() & ViewMask) != 0)
            {
                if (inside || Sphere.IsInsideFast(drawable.GetWorldBoundingBox()) != Intersection.Outside)
                    Result.Add(drawable);
            }
        }
    }

    /// <summary>Sphere.</summary>
    public Sphere Sphere;
}

/// <summary>Bounding box octree query.</summary>
public class BoxOctreeQuery : OctreeQuery
{
    /// <summary>Construct with bounding box and query parameters.</summary>
    public BoxOctreeQuery(List<Drawable> result, in BoundingBox box,
        DrawableFlags drawableFlags = DrawableFlags.Any, uint viewMask = Drawable.DefaultViewMask)
        : base(result, drawableFlags, viewMask)
    {
        Box = box;
    }

    /// <summary>Intersection test for an octant.</summary>
    public override Intersection TestOctant(in BoundingBox box, bool inside)
    {
        if (inside)
            return Intersection.Inside;
        else
            return Box.IsInside(box);
    }

    /// <summary>Intersection test for drawables.</summary>
    public override void TestDrawables(IReadOnlyList<Drawable> drawables, bool inside)
    {
        for (var i = 0; i < drawables.Count; ++i)
        {
            var drawable = drawables[i];

            if ((drawable.GetDrawableFlags() & DrawableFlags) != 0 && (drawable.GetViewMask() & ViewMask) != 0)
            {
                if (inside || Box.IsInsideFast(drawable.GetWorldBoundingBox()) != Intersection.Outside)
                    Result.Add(drawable);
            }
        }
    }

    /// <summary>Bounding box.</summary>
    public BoundingBox Box;
}

/// <summary>Frustum octree query.</summary>
public class FrustumOctreeQuery : OctreeQuery
{
    /// <summary>Construct with frustum and query parameters.</summary>
    public FrustumOctreeQuery(List<Drawable> result, Frustum frustum,
        DrawableFlags drawableFlags = DrawableFlags.Any, uint viewMask = Drawable.DefaultViewMask)
        : base(result, drawableFlags, viewMask)
    {
        Frustum = new Frustum(frustum);
    }

    /// <summary>Intersection test for an octant.</summary>
    public override Intersection TestOctant(in BoundingBox box, bool inside)
    {
        if (inside)
            return Intersection.Inside;
        else
            return Frustum.IsInside(box);
    }

    /// <summary>Intersection test for drawables.</summary>
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

    /// <summary>Frustum.</summary>
    public Frustum Frustum;
}

/// <summary>General octree query result. Used for Lua bindings only.</summary>
public struct OctreeQueryResult : IEquatable<OctreeQueryResult>
{
    /// <summary>Construct with defaults.</summary>
    public OctreeQueryResult()
    {
        Drawable = null;
        Node = null;
    }

    /// <summary>Test for inequality, added to prevent GCC from complaining.</summary>
    public readonly bool Equals(OctreeQueryResult other) =>
        ReferenceEquals(Drawable, other.Drawable) && ReferenceEquals(Node, other.Node);

    public override readonly bool Equals(object? obj) => obj is OctreeQueryResult other && Equals(other);

    public override readonly int GetHashCode() => HashCode.Combine(Drawable, Node);

    public static bool operator !=(OctreeQueryResult left, OctreeQueryResult right) => !left.Equals(right);

    public static bool operator ==(OctreeQueryResult left, OctreeQueryResult right) => left.Equals(right);

    /// <summary>Drawable.</summary>
    public Drawable? Drawable;

    /// <summary>Scene node.</summary>
    public Node? Node;
}

/// <summary>Graphics raycast detail level.</summary>
public enum RayQueryLevel
{
    RayAabb = 0,
    RayObb,
    RayTriangle,
    RayTriangleUv
}

/// <summary>Raycast result.</summary>
public struct RayQueryResult : IEquatable<RayQueryResult>
{
    /// <summary>Construct with defaults.</summary>
    public RayQueryResult()
    {
        Position = default;
        Normal = default;
        TextureUV = default;
        Distance = 0.0f;
        Drawable = null;
        Node = null;
        SubObject = 0;
    }

    /// <summary>Test for inequality, added to prevent GCC from complaining.</summary>
    public readonly bool Equals(RayQueryResult other)
    {
        return Position == other.Position && Normal == other.Normal && TextureUV == other.TextureUV &&
            Distance == other.Distance && ReferenceEquals(Drawable, other.Drawable) && ReferenceEquals(Node, other.Node) &&
            SubObject == other.SubObject;
    }

    public override readonly bool Equals(object? obj) => obj is RayQueryResult other && Equals(other);

    public override readonly int GetHashCode() => HashCode.Combine(Position, Normal, TextureUV, Distance, Drawable, Node, SubObject);

    public static bool operator !=(RayQueryResult left, RayQueryResult right) => !left.Equals(right);

    public static bool operator ==(RayQueryResult left, RayQueryResult right) => left.Equals(right);

    /// <summary>Hit position in world space.</summary>
    public Vector3f Position;

    /// <summary>Hit normal in world space. Negation of ray direction if per-triangle data not available.</summary>
    public Vector3f Normal;

    /// <summary>Hit texture position.</summary>
    public Vector2f TextureUV;

    /// <summary>Distance from ray origin.</summary>
    public float Distance;

    /// <summary>Drawable.</summary>
    public Drawable? Drawable;

    /// <summary>Scene node.</summary>
    public Node? Node;

    /// <summary>Drawable specific subobject if applicable.</summary>
    public uint SubObject;
}

/// <summary>Raycast octree query.</summary>
public class RayOctreeQuery : IDisposable
{
    /// <summary>Construct with ray and query parameters.</summary>
    public RayOctreeQuery(List<RayQueryResult> result, in Ray ray, RayQueryLevel level = RayQueryLevel.RayTriangle,
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
    public RayOctreeQuery(in Ray ray, RayQueryLevel level = RayQueryLevel.RayTriangle,
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

    /// <summary>Destruct.</summary>
    public void Dispose()
    {
        _resultStorage.Clear();
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

    /// <summary>Builtin result storage.</summary>
    private readonly List<RayQueryResult> _resultStorage = [];
}

/// <summary>All-content octree query.</summary>
public class AllContentOctreeQuery : OctreeQuery
{
    /// <summary>Construct.</summary>
    public AllContentOctreeQuery(List<Drawable> result, DrawableFlags drawableFlags, uint viewMask)
        : base(result, drawableFlags, viewMask)
    {
    }

    /// <summary>Intersection test for an octant.</summary>
    public override Intersection TestOctant(in BoundingBox box, bool inside)
    {
        return Intersection.Inside;
    }

    /// <summary>Intersection test for drawables.</summary>
    public override void TestDrawables(IReadOnlyList<Drawable> drawables, bool inside)
    {
        for (var i = 0; i < drawables.Count; ++i)
        {
            var drawable = drawables[i];

            if ((drawable.GetDrawableFlags() & DrawableFlags) != 0 && (drawable.GetViewMask() & ViewMask) != 0)
                Result.Add(drawable);
        }
    }
}
