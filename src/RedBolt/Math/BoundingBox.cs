using System.Runtime.InteropServices;

namespace RedBolt;

[StructLayout(LayoutKind.Sequential)]
public struct BoundingBox : IEquatable<BoundingBox>, IApproximateEquatable<BoundingBox>
{
    public Vector3 Min;
    private float dummyMin;
    public Vector3 Max;
    private float dummyMax;

    public BoundingBox(BoundingBox box)
    {
        Min = box.Min;
        Max = box.Max;
        dummyMin = 0.0f;
        dummyMax = 0.0f;
    }

    public BoundingBox(Rect rect)
    {
        Min = new Vector3(rect.Min, 0.0f);
        Max = new Vector3(rect.Max, 0.0f);
        dummyMin = 0.0f;
        dummyMax = 0.0f;
    }

    public BoundingBox(Vector3 min, Vector3 max)
    {
        Min = min;
        Max = max;
        dummyMin = 0.0f;
        dummyMax = 0.0f;
    }

    public BoundingBox(float min, float max)
    {
        Min = new Vector3(min, min, min);
        Max = new Vector3(max, max, max);
        dummyMin = 0.0f;
        dummyMax = 0.0f;
    }

    public BoundingBox(IReadOnlyList<Vector3> vertices)
    {
        Min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        Max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
        dummyMin = 0.0f;
        dummyMax = 0.0f;
        Define(vertices);
    }

    public BoundingBox(Sphere sphere)
    {
        Min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        Max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
        dummyMin = 0.0f;
        dummyMax = 0.0f;
        Define(sphere);
    }

    public Vector3 Center => (Min + Max) * 0.5f;
    public bool Defined => Min.X != float.PositiveInfinity;
    public Vector3 HalfSize => (Max - Min) * 0.5f;
    public Vector3 Size => Max - Min;

    public void Clear()
    {
        Min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        Max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
    }

    public void Define(BoundingBox box) => Define(box.Min, box.Max);
    public void Define(Rect rect) => Define(new Vector3(rect.Min, 0.0f), new Vector3(rect.Max, 0.0f));
    public void Define(float min, float max) => Define(new Vector3(min, min, min), new Vector3(max, max, max));
    public void Define(Vector3 point) => Min = Max = point;
    public void Define(Vector3 min, Vector3 max)
    {
        Min = min;
        Max = max;
    }

    public void Define(IReadOnlyList<Vector3> vertices)
    {
        Clear();
        Merge(vertices);
    }

    public void Define(Sphere sphere)
    {
        var offset = new Vector3(sphere.Radius, sphere.Radius, sphere.Radius);
        Define(sphere.Center - offset, sphere.Center + offset);
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

    public Intersection IsInside(Vector3 point)
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

    public void Merge(Vector3 point)
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

    public void Merge(IReadOnlyList<Vector3> vertices)
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
        var offset = new Vector3(sphere.Radius, sphere.Radius, sphere.Radius);
        Merge(sphere.Center - offset);
        Merge(sphere.Center + offset);
    }

    public Rect Projected(Matrix3x4 projection)
    {
        var rect = new Rect();
        rect.Merge((projection * Min).ToVector2());
        rect.Merge((projection * new Vector3(Max.X, Min.Y, Min.Z)).ToVector2());
        rect.Merge((projection * new Vector3(Min.X, Max.Y, Min.Z)).ToVector2());
        rect.Merge((projection * new Vector3(Max.X, Max.Y, Min.Z)).ToVector2());
        rect.Merge((projection * new Vector3(Min.X, Min.Y, Max.Z)).ToVector2());
        rect.Merge((projection * new Vector3(Max.X, Min.Y, Max.Z)).ToVector2());
        rect.Merge((projection * new Vector3(Min.X, Max.Y, Max.Z)).ToVector2());
        rect.Merge((projection * Max).ToVector2());
        return rect;
    }

    public void Transform(Matrix3 transform) => this = Transformed(transform);
    public void Transform(Matrix3x4 transform) => this = Transformed(transform);

    public float DistanceToPoint(Vector3 point)
    {
        var offset = Center - point;
        return Vector3.Max(Vector3.Zero, offset.Abs - HalfSize).Length;
    }

    public BoundingBox Transformed(Matrix3 transform)
    {
        return Transformed(new Matrix3x4(transform));
    }

    public BoundingBox Transformed(Matrix3x4 transform)
    {
        var newCenter = transform * Center;
        var oldEdge = HalfSize;
        var newEdge = new Vector3(
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
