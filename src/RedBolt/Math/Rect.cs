using System.Globalization;
using System.Runtime.InteropServices;

namespace RedBolt;

[StructLayout(LayoutKind.Sequential)]
public struct Rect : IEquatable<Rect>, IApproximateEquatable<Rect>
{
    public Vector2 Min;
    public Vector2 Max;

    public Rect(float minX = float.PositiveInfinity, float minY = float.PositiveInfinity, float maxX = float.NegativeInfinity, float maxY = float.NegativeInfinity)
    {
        Min = new Vector2(minX, minY);
        Max = new Vector2(maxX, maxY);
    }

    public Rect(Vector2 min, Vector2 max)
    {
        Min = min;
        Max = max;
    }

    public Rect(Vector4 vector)
    {
        Min = vector.Xy;
        Max = vector.Zw;
    }

    public static readonly Rect Full = new(-1.0f, -1.0f, 1.0f, 1.0f);
    public static readonly Rect Positive = new(0.0f, 0.0f, 1.0f, 1.0f);
    public static readonly Rect Zero = new(0.0f, 0.0f, 0.0f, 0.0f);

    public float Bottom => Max.Y;
    public Vector2 Center => (Max + Min) * 0.5f;
    public bool Defined => !float.IsPositiveInfinity(Min.X);
    public Vector2 HalfSize => (Max - Min) * 0.5f;
    public float Left => Min.X;
    public float Right => Max.X;
    public Vector2 Size => Max - Min;
    public float Top => Min.Y;

    public void Clear()
    {
        Min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        Max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
    }

    public void Clip(Rect rect)
    {
        if (rect.Min.X > Min.X)
            Min.X = rect.Min.X;
        if (rect.Max.X < Max.X)
            Max.X = rect.Max.X;
        if (rect.Min.Y > Min.Y)
            Min.Y = rect.Min.Y;
        if (rect.Max.Y < Max.Y)
            Max.Y = rect.Max.Y;

        if (Min.X > Max.X || Min.Y > Max.Y)
            Clear();
    }

    public Intersection IsInside(Vector2 point)
    {
        return point.X < Min.X || point.Y < Min.Y || point.X > Max.X || point.Y > Max.Y
            ? Intersection.Outside
            : Intersection.Inside;
    }

    public Intersection IsInside(Rect rect)
    {
        if (rect.Max.X < Min.X || rect.Min.X > Max.X || rect.Max.Y < Min.Y || rect.Min.Y > Max.Y)
            return Intersection.Outside;
        if (rect.Min.X < Min.X || rect.Max.X > Max.X || rect.Min.Y < Min.Y || rect.Max.Y > Max.Y)
            return Intersection.Intersects;
        return Intersection.Inside;
    }

    public void Merge(Vector2 point)
    {
        if (point.X < Min.X)
            Min.X = point.X;
        if (point.X > Max.X)
            Max.X = point.X;
        if (point.Y < Min.Y)
            Min.Y = point.Y;
        if (point.Y > Max.Y)
            Max.Y = point.Y;
    }

    public void Merge(Rect rect)
    {
        if (rect.Min.X < Min.X)
            Min.X = rect.Min.X;
        if (rect.Min.Y < Min.Y)
            Min.Y = rect.Min.Y;
        if (rect.Max.X > Max.X)
            Max.X = rect.Max.X;
        if (rect.Max.Y > Max.Y)
            Max.Y = rect.Max.Y;
    }

    public bool ApproximatelyEquivalent(Rect rhs, float epsilon = MathDefs.Epsilon) =>
        Min.ApproximatelyEquivalent(rhs.Min, epsilon) && Max.ApproximatelyEquivalent(rhs.Max, epsilon);

    public bool Equals(Rect other) => Min.Equals(other.Min) && Max.Equals(other.Max);
    public override bool Equals(object? obj) => obj is Rect other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Min, Max);
    public override string ToString() => string.Format(CultureInfo.InvariantCulture, "{0} {1} {2} {3}", Min.X, Min.Y, Max.X, Max.Y);

    public static bool operator ==(Rect lhs, Rect rhs) => lhs.Equals(rhs);
    public static bool operator !=(Rect lhs, Rect rhs) => !lhs.Equals(rhs);
    public static Rect operator +(Rect lhs, Rect rhs) => new(lhs.Min + rhs.Min, lhs.Max + rhs.Max);
    public static Rect operator -(Rect lhs, Rect rhs) => new(lhs.Min - rhs.Min, lhs.Max - rhs.Max);
    public static Rect operator *(Rect lhs, Rect rhs) => new(lhs.Min * rhs.Min, lhs.Max * rhs.Max);
    public static Rect operator /(Rect lhs, Rect rhs) => new(lhs.Min / rhs.Min, lhs.Max / rhs.Max);
}
