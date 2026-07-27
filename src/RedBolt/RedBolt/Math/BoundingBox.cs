using System.Runtime.InteropServices;

namespace RedBolt;

/// <summary>Three-dimensional axis-aligned bounding box.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct BoundingBox : IEquatable<BoundingBox>
{
    /// <summary>Construct with zero size.</summary>
    public BoundingBox()
    {
        Min = new Vector3f(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        _dummyMin = 0.0f;
        Max = new Vector3f(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
        _dummyMax = 0.0f;
    }

    /// <summary>Copy-construct from another bounding box.</summary>
    public BoundingBox(BoundingBox box)
    {
        Min = box.Min;
        _dummyMin = 0.0f;
        Max = box.Max;
        _dummyMax = 0.0f;
    }

    /// <summary>Construct from a rect, with the Z dimension left zero.</summary>
    public BoundingBox(Rect rect)
    {
        Min = new Vector3f(rect.Min.X, rect.Min.Y, 0.0f);
        _dummyMin = 0.0f;
        Max = new Vector3f(rect.Max.X, rect.Max.Y, 0.0f);
        _dummyMax = 0.0f;
    }

    /// <summary>Construct from minimum and maximum vectors.</summary>
    public BoundingBox(Vector3f min, Vector3f max)
    {
        Min = min;
        _dummyMin = 0.0f;
        Max = max;
        _dummyMax = 0.0f;
    }

    /// <summary>Construct from minimum and maximum floats (all dimensions same).</summary>
    public BoundingBox(float min, float max)
    {
        Min = new Vector3f(min, min, min);
        _dummyMin = 0.0f;
        Max = new Vector3f(max, max, max);
        _dummyMax = 0.0f;
    }

    /// <summary>Construct from an array of vertices.</summary>
    public BoundingBox(IReadOnlyList<Vector3f> vertices)
        : this()
    {
        Define(vertices);
    }

    /// <summary>Construct from a frustum.</summary>
    public BoundingBox(Frustum frustum)
        : this()
    {
        Define(frustum);
    }

    /// <summary>Construct from a polyhedron.</summary>
    public BoundingBox(Polyhedron polyhedron)
        : this()
    {
        Define(polyhedron);
    }

    /// <summary>Construct from a sphere.</summary>
    public BoundingBox(Sphere sphere)
        : this()
    {
        Define(sphere);
    }

    /// <summary>Test for equality with another bounding box.</summary>
    public static bool operator ==(BoundingBox lhs, BoundingBox rhs) => lhs.Min == rhs.Min && lhs.Max == rhs.Max;

    /// <summary>Test for inequality with another bounding box.</summary>
    public static bool operator !=(BoundingBox lhs, BoundingBox rhs) => lhs.Min != rhs.Min || lhs.Max != rhs.Max;

    /// <summary>Define from another bounding box.</summary>
    public void Define(BoundingBox box)
    {
        Define(box.Min, box.Max);
    }

    /// <summary>Define from a Rect.</summary>
    public void Define(Rect rect)
    {
        Define(new Vector3f(rect.Min.X, rect.Min.Y, 0.0f), new Vector3f(rect.Max.X, rect.Max.Y, 0.0f));
    }

    /// <summary>Define from minimum and maximum vectors.</summary>
    public void Define(Vector3f min, Vector3f max)
    {
        Min = min;
        Max = max;
    }

    /// <summary>Define from minimum and maximum floats (all dimensions same).</summary>
    public void Define(float min, float max)
    {
        Min = new Vector3f(min, min, min);
        Max = new Vector3f(max, max, max);
    }

    /// <summary>Define from a point.</summary>
    public void Define(Vector3f point)
    {
        Min = Max = point;
    }

    /// <summary>Define from an array of vertices.</summary>
    public void Define(IReadOnlyList<Vector3f> vertices)
    {
        Clear();
        if (vertices.Count == 0)
            return;
        Merge(vertices);
    }

    /// <summary>Define from a frustum.</summary>
    public void Define(Frustum frustum)
    {
        Clear();
        Define(frustum.Vertices);
    }

    /// <summary>Define from a polyhedron.</summary>
    public void Define(Polyhedron polyhedron)
    {
        Clear();
        Merge(polyhedron);
    }

    /// <summary>Define from a sphere.</summary>
    public void Define(Sphere sphere)
    {
        var center = sphere.Center;
        var radius = sphere.Radius;
        Min = center + new Vector3f(-radius, -radius, -radius);
        Max = center + new Vector3f(radius, radius, radius);
    }

    /// <summary>Merge a point.</summary>
    public void Merge(Vector3f point)
    {
        if (point.X < Min.X)
            Min.X = point.X;
        if (point.Y < Min.Y)
            Min.Y = point.Y;
        if (point.Z < Min.Z)
            Min.Z = point.Z;
        if (point.X > Max.X)
            Max.X = point.X;
        if (point.Y > Max.Y)
            Max.Y = point.Y;
        if (point.Z > Max.Z)
            Max.Z = point.Z;
    }

    /// <summary>Merge another bounding box.</summary>
    public void Merge(BoundingBox box)
    {
        if (box.Min.X < Min.X)
            Min.X = box.Min.X;
        if (box.Min.Y < Min.Y)
            Min.Y = box.Min.Y;
        if (box.Min.Z < Min.Z)
            Min.Z = box.Min.Z;
        if (box.Max.X > Max.X)
            Max.X = box.Max.X;
        if (box.Max.Y > Max.Y)
            Max.Y = box.Max.Y;
        if (box.Max.Z > Max.Z)
            Max.Z = box.Max.Z;
    }

    /// <summary>Merge an array of vertices.</summary>
    public void Merge(IReadOnlyList<Vector3f> vertices)
    {
        for (var i = 0; i < vertices.Count; ++i)
            Merge(vertices[i]);
    }

    /// <summary>Merge a frustum.</summary>
    public void Merge(Frustum frustum)
    {
        Merge(frustum.Vertices);
    }

    /// <summary>Merge a polyhedron.</summary>
    public void Merge(Polyhedron polyhedron)
    {
        foreach (var face in polyhedron.Faces)
        {
            if (face.Count != 0)
                Merge(face);
        }
    }

    /// <summary>Merge a sphere.</summary>
    public void Merge(Sphere sphere)
    {
        var center = sphere.Center;
        var radius = sphere.Radius;
        Merge(center + new Vector3f(radius, radius, radius));
        Merge(center + new Vector3f(-radius, -radius, -radius));
    }

    /// <summary>Clip with another bounding box. The box can become degenerate (undefined) as a result.</summary>
    public void Clip(BoundingBox box)
    {
        if (box.Min.X > Min.X)
            Min.X = box.Min.X;
        if (box.Max.X < Max.X)
            Max.X = box.Max.X;
        if (box.Min.Y > Min.Y)
            Min.Y = box.Min.Y;
        if (box.Max.Y < Max.Y)
            Max.Y = box.Max.Y;
        if (box.Min.Z > Min.Z)
            Min.Z = box.Min.Z;
        if (box.Max.Z < Max.Z)
            Max.Z = box.Max.Z;

        if (Min.X > Max.X || Min.Y > Max.Y || Min.Z > Max.Z)
            Clear();
    }

    /// <summary>Transform with a 3x3 matrix.</summary>
    public void Transform(Matrix3 transform)
    {
        this = Transformed(transform);
    }

    /// <summary>Transform with a 3x4 matrix.</summary>
    public void Transform(Matrix3x4 transform)
    {
        this = Transformed(transform);
    }

    /// <summary>Clear to undefined state.</summary>
    public void Clear()
    {
        Min = new Vector3f(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        Max = new Vector3f(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
    }

    /// <summary>Return this bounding box merged with a point.</summary>
    public BoundingBox Merged(Vector3f point)
    {
        var copy = this;
        copy.Merge(point);
        return copy;
    }

    /// <summary>Return this bounding box merged with another bounding box.</summary>
    public BoundingBox Merged(BoundingBox box)
    {
        var copy = this;
        copy.Merge(box);
        return copy;
    }

    /// <summary>Return this bounding box merged with an array of vertices.</summary>
    public BoundingBox Merged(IReadOnlyList<Vector3f> vertices)
    {
        var copy = this;
        copy.Merge(vertices);
        return copy;
    }

    /// <summary>Return this bounding box merged with a frustum.</summary>
    public BoundingBox Merged(Frustum frustum)
    {
        var copy = this;
        copy.Merge(frustum);
        return copy;
    }

    /// <summary>Return this bounding box merged with a polyhedron.</summary>
    public BoundingBox Merged(Polyhedron polyhedron)
    {
        var copy = this;
        copy.Merge(polyhedron);
        return copy;
    }

    /// <summary>Return this bounding box merged with a sphere.</summary>
    public BoundingBox Merged(Sphere sphere)
    {
        var copy = this;
        copy.Merge(sphere);
        return copy;
    }

    /// <summary>Return this bounding box clipped by another box.</summary>
    public BoundingBox Clipped(BoundingBox box)
    {
        var copy = this;
        copy.Clip(box);
        return copy;
    }

    /// <summary>Return this bounding box expanded by given value.</summary>
    public BoundingBox Padded(Vector3f minPadding, Vector3f maxPadding)
    {
        if (!Defined)
            return new BoundingBox();

        var copy = this;
        copy.Min -= minPadding;
        copy.Max += maxPadding;

        if (copy.Min.X > copy.Max.X || copy.Min.Y > copy.Max.Y || copy.Min.Z > copy.Max.Z)
            return new BoundingBox();

        return copy;
    }

    /// <summary>Return this bounding box expanded by given value, same for min and max.</summary>
    public BoundingBox Padded(Vector3f padding)
    {
        return Padded(padding, padding);
    }

    /// <summary>Return true if this bounding box is defined via a previous call to Define() or Merge().</summary>
    public bool Defined => Min.X != float.PositiveInfinity;

    /// <summary>Return center.</summary>
    public Vector3f Center => (Max + Min) * 0.5f;

    /// <summary>Return size.</summary>
    public Vector3f Size => Max - Min;

    /// <summary>Return half-size.</summary>
    public Vector3f HalfSize => (Max - Min) * 0.5f;

    /// <summary>Return volume.</summary>
    public float Volume
    {
        get
        {
            var size = Size;
            return size.X * size.Y * size.Z;
        }
    }

    /// <summary>Return transformed by a 3x3 matrix.</summary>
    public BoundingBox Transformed(Matrix3 transform)
    {
        return Transformed(new Matrix3x4(transform));
    }

    /// <summary>Return transformed by a 3x4 matrix.</summary>
    public BoundingBox Transformed(Matrix3x4 transform)
    {
        var newCenter = transform * Center;
        var oldEdge = Size * 0.5f;
        var newEdge = new Vector3f(
            Math.Abs(transform.M00) * oldEdge.X + Math.Abs(transform.M01) * oldEdge.Y + Math.Abs(transform.M02) * oldEdge.Z,
            Math.Abs(transform.M10) * oldEdge.X + Math.Abs(transform.M11) * oldEdge.Y + Math.Abs(transform.M12) * oldEdge.Z,
            Math.Abs(transform.M20) * oldEdge.X + Math.Abs(transform.M21) * oldEdge.Y + Math.Abs(transform.M22) * oldEdge.Z);
        return new BoundingBox(newCenter - newEdge, newCenter + newEdge);
    }

    /// <summary>Return projected by a 4x4 projection matrix.</summary>
    public Rect Projected(Matrix4 projection)
    {
        var projMin = Min;
        var projMax = Max;
        if (projMin.Z < MathDefs.MinNearClip)
            projMin.Z = MathDefs.MinNearClip;
        if (projMax.Z < MathDefs.MinNearClip)
            projMax.Z = MathDefs.MinNearClip;

        var rect = new Rect();
        rect.Merge((projection * projMin).ToVector2());
        rect.Merge((projection * new Vector3f(projMax.X, projMin.Y, projMin.Z)).ToVector2());
        rect.Merge((projection * new Vector3f(projMin.X, projMax.Y, projMin.Z)).ToVector2());
        rect.Merge((projection * new Vector3f(projMax.X, projMax.Y, projMin.Z)).ToVector2());
        rect.Merge((projection * new Vector3f(projMin.X, projMin.Y, projMax.Z)).ToVector2());
        rect.Merge((projection * new Vector3f(projMax.X, projMin.Y, projMax.Z)).ToVector2());
        rect.Merge((projection * new Vector3f(projMin.X, projMax.Y, projMax.Z)).ToVector2());
        rect.Merge((projection * projMax).ToVector2());
        return rect;
    }

    /// <summary>Return distance to point.</summary>
    public float DistanceToPoint(Vector3f point)
    {
        var absOffset = (Center - point).Abs;
        return Vector3f.Max(Vector3f.Zero, absOffset - HalfSize).Length;
    }

    /// <summary>Return signed distance to point. Negative if point is inside bounding box.</summary>
    public float SignedDistanceToPoint(Vector3f point)
    {
        var absOffset = (Center - point).Abs;
        var delta = absOffset - HalfSize;
        var outerDistance = Vector3f.Max(Vector3f.Zero, delta).Length;
        var innerDistance = Math.Max(delta.X, Math.Max(delta.Y, delta.Z));
        return innerDistance < 0.0f ? innerDistance : outerDistance;
    }

    /// <summary>Return distance to another bounding box.</summary>
    public float DistanceToBoundingBox(BoundingBox box)
    {
        var absOffset = (Center - box.Center).Abs;
        var maxHalfSize = Vector3f.Max(HalfSize, box.HalfSize);
        var minHalfSize = Vector3f.Min(HalfSize, box.HalfSize);
        var delta = absOffset - maxHalfSize - minHalfSize;
        return Vector3f.Max(Vector3f.Zero, delta).Length;
    }

    /// <summary>Return signed distance to another bounding box. Negative if inside.</summary>
    public float SignedDistanceToBoundingBox(BoundingBox box)
    {
        var absOffset = (Center - box.Center).Abs;
        var maxHalfSize = Vector3f.Max(HalfSize, box.HalfSize);
        var minHalfSize = Vector3f.Min(HalfSize, box.HalfSize);
        var outerDelta = absOffset - maxHalfSize - minHalfSize;
        var outerDistance = Vector3f.Max(Vector3f.Zero, outerDelta).Length;
        var innerDelta = maxHalfSize - absOffset - minHalfSize;
        var innerDistance = -Math.Min(innerDelta.X, Math.Min(innerDelta.Y, innerDelta.Z));
        return innerDistance < 0.0f ? innerDistance : outerDistance;
    }

    /// <summary>Test if a point is inside.</summary>
    public Intersection IsInside(Vector3f point)
    {
        return point.X < Min.X || point.X > Max.X || point.Y < Min.Y || point.Y > Max.Y
            || point.Z < Min.Z || point.Z > Max.Z
            ? Intersection.Outside
            : Intersection.Inside;
    }

    /// <summary>Test if another bounding box is inside, outside or intersects.</summary>
    public Intersection IsInside(BoundingBox box)
    {
        if (box.Max.X < Min.X || box.Min.X > Max.X || box.Max.Y < Min.Y || box.Min.Y > Max.Y
            || box.Max.Z < Min.Z || box.Min.Z > Max.Z)
            return Intersection.Outside;
        if (box.Min.X < Min.X || box.Max.X > Max.X || box.Min.Y < Min.Y || box.Max.Y > Max.Y
            || box.Min.Z < Min.Z || box.Max.Z > Max.Z)
            return Intersection.Intersects;
        return Intersection.Inside;
    }

    /// <summary>Test if another bounding box is (partially) inside or outside.</summary>
    public Intersection IsInsideFast(BoundingBox box)
    {
        return box.Max.X < Min.X || box.Min.X > Max.X || box.Max.Y < Min.Y || box.Min.Y > Max.Y
            || box.Max.Z < Min.Z || box.Min.Z > Max.Z
            ? Intersection.Outside
            : Intersection.Inside;
    }

    /// <summary>Test if a sphere is inside, outside or intersects.</summary>
    public Intersection IsInside(Sphere sphere)
    {
        var distSquared = 0.0f;
        float temp;
        var center = sphere.Center;

        if (center.X < Min.X)
        {
            temp = center.X - Min.X;
            distSquared += temp * temp;
        }
        else if (center.X > Max.X)
        {
            temp = center.X - Max.X;
            distSquared += temp * temp;
        }
        if (center.Y < Min.Y)
        {
            temp = center.Y - Min.Y;
            distSquared += temp * temp;
        }
        else if (center.Y > Max.Y)
        {
            temp = center.Y - Max.Y;
            distSquared += temp * temp;
        }
        if (center.Z < Min.Z)
        {
            temp = center.Z - Min.Z;
            distSquared += temp * temp;
        }
        else if (center.Z > Max.Z)
        {
            temp = center.Z - Max.Z;
            distSquared += temp * temp;
        }

        var radius = sphere.Radius;
        if (distSquared > radius * radius)
            return Intersection.Outside;
        if (center.X - radius < Min.X || center.X + radius > Max.X || center.Y - radius < Min.Y
            || center.Y + radius > Max.Y || center.Z - radius < Min.Z || center.Z + radius > Max.Z)
            return Intersection.Intersects;
        return Intersection.Inside;
    }

    /// <summary>Test if a sphere is (partially) inside or outside.</summary>
    public Intersection IsInsideFast(Sphere sphere)
    {
        var distSquared = 0.0f;
        float temp;
        var center = sphere.Center;

        if (center.X < Min.X)
        {
            temp = center.X - Min.X;
            distSquared += temp * temp;
        }
        else if (center.X > Max.X)
        {
            temp = center.X - Max.X;
            distSquared += temp * temp;
        }
        if (center.Y < Min.Y)
        {
            temp = center.Y - Min.Y;
            distSquared += temp * temp;
        }
        else if (center.Y > Max.Y)
        {
            temp = center.Y - Max.Y;
            distSquared += temp * temp;
        }
        if (center.Z < Min.Z)
        {
            temp = center.Z - Min.Z;
            distSquared += temp * temp;
        }
        else if (center.Z > Max.Z)
        {
            temp = center.Z - Max.Z;
            distSquared += temp * temp;
        }

        var radius = sphere.Radius;
        return distSquared >= radius * radius ? Intersection.Outside : Intersection.Inside;
    }

    /// <summary>Return as string.</summary>
    public override string ToString() => $"{Min} - {Max}";

    public bool Equals(BoundingBox other) => this == other;

    public override bool Equals(object? obj) => obj is BoundingBox other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Min, Max);

    /// <summary>Minimum vector.</summary>
    public Vector3f Min;

    private float _dummyMin; // This is never used, but exists to pad the Min value to four floats.

    /// <summary>Maximum vector.</summary>
    public Vector3f Max;

    private float _dummyMax; // This is never used, but exists to pad the Max value to four floats.
}
