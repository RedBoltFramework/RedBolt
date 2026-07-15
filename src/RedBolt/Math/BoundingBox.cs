using System.Runtime.InteropServices;

namespace RedBolt;

[StructLayout(LayoutKind.Sequential)]
public struct BoundingBox : IEquatable<BoundingBox>, IApproximateEquatable<BoundingBox>
{
    public Vector3f Min;
    private float _dummyMin;
    public Vector3f Max;
    private float _dummyMax;

    public BoundingBox(BoundingBox box)
    {
        Min = box.Min;
        Max = box.Max;
        _dummyMin = 0.0f;
        _dummyMax = 0.0f;
    }

    public BoundingBox(Rect rect)
    {
        Min = new Vector3f(rect.Min.X, rect.Min.Y, 0.0f);
        Max = new Vector3f(rect.Max.X, rect.Max.Y, 0.0f);
        _dummyMin = 0.0f;
        _dummyMax = 0.0f;
    }

    public BoundingBox(Vector3f min, Vector3f max)
    {
        Min = min;
        Max = max;
        _dummyMin = 0.0f;
        _dummyMax = 0.0f;
    }

    public BoundingBox(float min, float max)
    {
        Min = new Vector3f(min, min, min);
        Max = new Vector3f(max, max, max);
        _dummyMin = 0.0f;
        _dummyMax = 0.0f;
    }

    public BoundingBox(IReadOnlyList<Vector3f> vertices)
    {
        Min = new Vector3f(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        Max = new Vector3f(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
        _dummyMin = 0.0f;
        _dummyMax = 0.0f;
        Define(vertices);
    }

    public BoundingBox(Sphere sphere)
    {
        Min = new Vector3f(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        Max = new Vector3f(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
        _dummyMin = 0.0f;
        _dummyMax = 0.0f;
        Define(sphere);
    }

    public BoundingBox(Polyhedron polyhedron)
    {
        Min = new Vector3f(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        Max = new Vector3f(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
        _dummyMin = 0.0f;
        _dummyMax = 0.0f;
        Define(polyhedron);
    }

    public BoundingBox(Frustum frustum)
    {
        Min = new Vector3f(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        Max = new Vector3f(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
        _dummyMin = 0.0f;
        _dummyMax = 0.0f;
        Define(frustum);
    }

    public Vector3f Center => (Min + Max) * 0.5f;
    public bool Defined => Min.X != float.PositiveInfinity;
    public Vector3f HalfSize => (Max - Min) * 0.5f;
    public Vector3f Size => Max - Min;
    public float Volume => Size.X * Size.Y * Size.Z;

    public void Clear()
    {
        Min = new Vector3f(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        Max = new Vector3f(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
    }

    public void Define(BoundingBox box) => Define(box.Min, box.Max);
    public void Define(Rect rect) => Define(new Vector3f(rect.Min.X, rect.Min.Y, 0.0f), new Vector3f(rect.Max.X, rect.Max.Y, 0.0f));
    public void Define(float min, float max) => Define(new Vector3f(min, min, min), new Vector3f(max, max, max));
    public void Define(Vector3f point) => Min = Max = point;
    public void Define(Vector3f min, Vector3f max)
    {
        Min = min;
        Max = max;
    }

    public void Define(IReadOnlyList<Vector3f> vertices)
    {
        Clear();
        Merge(vertices);
    }

    public void Define(Sphere sphere)
    {
        var offset = new Vector3f(sphere.Radius, sphere.Radius, sphere.Radius);
        Define(sphere.Center - offset, sphere.Center + offset);
    }

    public void Define(Frustum frustum)
    {
        Clear();
        Merge(frustum);
    }

    public void Define(Polyhedron polyhedron)
    {
        Clear();
        Merge(polyhedron);
    }

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

    public Intersection IsInside(Vector3f point)
    {
        return point.X < Min.X || point.X > Max.X || point.Y < Min.Y || point.Y > Max.Y || point.Z < Min.Z || point.Z > Max.Z
            ? Intersection.Outside
            : Intersection.Inside;
    }

    public Intersection IsInside(BoundingBox box)
    {
        if (box.Max.X < Min.X || box.Min.X > Max.X || box.Max.Y < Min.Y || box.Min.Y > Max.Y || box.Max.Z < Min.Z || box.Min.Z > Max.Z)
            return Intersection.Outside;
        if (box.Min.X < Min.X || box.Max.X > Max.X || box.Min.Y < Min.Y || box.Max.Y > Max.Y || box.Min.Z < Min.Z || box.Max.Z > Max.Z)
            return Intersection.Intersects;
        return Intersection.Inside;
    }

    public Intersection IsInside(Sphere sphere)
    {
        var distSquared = 0.0f;
        var center = sphere.Center;

        if (center.X < Min.X)
            distSquared += (center.X - Min.X) * (center.X - Min.X);
        else if (center.X > Max.X)
            distSquared += (center.X - Max.X) * (center.X - Max.X);
        if (center.Y < Min.Y)
            distSquared += (center.Y - Min.Y) * (center.Y - Min.Y);
        else if (center.Y > Max.Y)
            distSquared += (center.Y - Max.Y) * (center.Y - Max.Y);
        if (center.Z < Min.Z)
            distSquared += (center.Z - Min.Z) * (center.Z - Min.Z);
        else if (center.Z > Max.Z)
            distSquared += (center.Z - Max.Z) * (center.Z - Max.Z);

        if (distSquared >= sphere.Radius * sphere.Radius)
            return Intersection.Outside;

        return center.X - sphere.Radius >= Min.X && center.X + sphere.Radius <= Max.X &&
            center.Y - sphere.Radius >= Min.Y && center.Y + sphere.Radius <= Max.Y &&
            center.Z - sphere.Radius >= Min.Z && center.Z + sphere.Radius <= Max.Z
            ? Intersection.Inside
            : Intersection.Intersects;
    }

    public Intersection IsInsideFast(BoundingBox box)
    {
        return box.Max.X < Min.X || box.Min.X > Max.X || box.Max.Y < Min.Y || box.Min.Y > Max.Y || box.Max.Z < Min.Z || box.Min.Z > Max.Z
            ? Intersection.Outside
            : Intersection.Inside;
    }

    public Intersection IsInsideFast(Sphere sphere) => IsInside(sphere) == Intersection.Outside ? Intersection.Outside : Intersection.Inside;

    public void Merge(Vector3f point)
    {
        if (point.X < Min.X)
            Min.X = point.X;
        if (point.X > Max.X)
            Max.X = point.X;
        if (point.Y < Min.Y)
            Min.Y = point.Y;
        if (point.Y > Max.Y)
            Max.Y = point.Y;
        if (point.Z < Min.Z)
            Min.Z = point.Z;
        if (point.Z > Max.Z)
            Max.Z = point.Z;
    }

    public void Merge(IReadOnlyList<Vector3f> vertices)
    {
        foreach (var vertex in vertices)
            Merge(vertex);
    }

    public void Merge(BoundingBox box)
    {
        if (!box.Defined)
            return;
        Merge(box.Min);
        Merge(box.Max);
    }

    public void Merge(Sphere sphere)
    {
        var offset = new Vector3f(sphere.Radius, sphere.Radius, sphere.Radius);
        Merge(sphere.Center - offset);
        Merge(sphere.Center + offset);
    }

    public void Merge(Frustum frustum) => Merge(frustum.Vertices);

    public void Merge(Polyhedron polyhedron)
    {
        foreach (var face in polyhedron.Faces)
            Merge(face);
    }

    public BoundingBox Merged(Vector3f point) { var result = this; result.Merge(point); return result; }
    public BoundingBox Merged(BoundingBox box) { var result = this; result.Merge(box); return result; }
    public BoundingBox Merged(Sphere sphere) { var result = this; result.Merge(sphere); return result; }
    public BoundingBox Clipped(BoundingBox box) { var result = this; result.Clip(box); return result; }
    public BoundingBox Padded(Vector3f minPadding, Vector3f maxPadding) => new(Min - minPadding, Max + maxPadding);
    public BoundingBox Padded(Vector3f padding) => Padded(padding, padding);

    public Rect Projected(Matrix3x4 projection)
    {
        var rect = new Rect();
        rect.Merge((projection * Min).ToVector2());
        rect.Merge((projection * new Vector3f(Max.X, Min.Y, Min.Z)).ToVector2());
        rect.Merge((projection * new Vector3f(Min.X, Max.Y, Min.Z)).ToVector2());
        rect.Merge((projection * new Vector3f(Max.X, Max.Y, Min.Z)).ToVector2());
        rect.Merge((projection * new Vector3f(Min.X, Min.Y, Max.Z)).ToVector2());
        rect.Merge((projection * new Vector3f(Max.X, Min.Y, Max.Z)).ToVector2());
        rect.Merge((projection * new Vector3f(Min.X, Max.Y, Max.Z)).ToVector2());
        rect.Merge((projection * Max).ToVector2());
        return rect;
    }

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

    public void Transform(Matrix3 transform) => this = Transformed(transform);
    public void Transform(Matrix3x4 transform) => this = Transformed(transform);

    public float DistanceToPoint(Vector3f point)
    {
        var offset = Center - point;
        return Vector3f.Max(Vector3f.Zero, offset.Abs - HalfSize).Length;
    }

    public float SignedDistanceToPoint(Vector3f point)
    {
        var delta = (Center - point).Abs - HalfSize;
        var outerDistance = Vector3f.Max(Vector3f.Zero, delta).Length;
        var innerDistance = Math.Max(delta.X, Math.Max(delta.Y, delta.Z));
        return innerDistance < 0.0f ? innerDistance : outerDistance;
    }

    public float DistanceToBoundingBox(BoundingBox box)
    {
        var absoluteOffset = (Center - box.Center).Abs;
        var delta = absoluteOffset - Vector3f.Max(HalfSize, box.HalfSize) - Vector3f.Min(HalfSize, box.HalfSize);
        return Vector3f.Max(Vector3f.Zero, delta).Length;
    }

    public float SignedDistanceToBoundingBox(BoundingBox box)
    {
        var absoluteOffset = (Center - box.Center).Abs;
        var maxHalfSize = Vector3f.Max(HalfSize, box.HalfSize);
        var minHalfSize = Vector3f.Min(HalfSize, box.HalfSize);
        var outerDelta = absoluteOffset - maxHalfSize - minHalfSize;
        var outerDistance = Vector3f.Max(Vector3f.Zero, outerDelta).Length;
        var innerDelta = maxHalfSize - absoluteOffset - minHalfSize;
        var innerDistance = -Math.Min(innerDelta.X, Math.Min(innerDelta.Y, innerDelta.Z));
        return innerDistance < 0.0f ? innerDistance : outerDistance;
    }

    public BoundingBox Transformed(Matrix3 transform)
    {
        return Transformed(new Matrix3x4(transform));
    }

    public BoundingBox Transformed(Matrix3x4 transform)
    {
        var newCenter = transform * Center;
        var oldEdge = HalfSize;
        var newEdge = new Vector3f(
            Math.Abs(transform.M00) * oldEdge.X + Math.Abs(transform.M01) * oldEdge.Y + Math.Abs(transform.M02) * oldEdge.Z,
            Math.Abs(transform.M10) * oldEdge.X + Math.Abs(transform.M11) * oldEdge.Y + Math.Abs(transform.M12) * oldEdge.Z,
            Math.Abs(transform.M20) * oldEdge.X + Math.Abs(transform.M21) * oldEdge.Y + Math.Abs(transform.M22) * oldEdge.Z);
        return new BoundingBox(newCenter - newEdge, newCenter + newEdge);
    }

    public bool ApproximatelyEquivalent(BoundingBox rhs, float epsilon = MathDefs.Epsilon) =>
        Min.ApproximatelyEquivalent(rhs.Min, epsilon) && Max.ApproximatelyEquivalent(rhs.Max, epsilon);

    public bool Equals(BoundingBox other) => Min.Equals(other.Min) && Max.Equals(other.Max);
    public override bool Equals(object? obj) => obj is BoundingBox other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Min, Max);
    public override string ToString() => $"{Min} - {Max}";
}
