using System.Globalization;
using System.Runtime.InteropServices;

namespace RedBolt;

[StructLayout(LayoutKind.Sequential)]
public struct Rect : IEquatable<Rect>, IApproximateEquatable<Rect>
{
    public Vector2f Min;
    public Vector2f Max;

    public Rect(float minX = float.PositiveInfinity, float minY = float.PositiveInfinity, float maxX = float.NegativeInfinity, float maxY = float.NegativeInfinity)
    {
        Min = new Vector2f(minX, minY);
        Max = new Vector2f(maxX, maxY);
    }

    public Rect(float[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.Length < 4)
            throw new ArgumentException("Expected at least four elements.", nameof(data));

        Min = new Vector2f(data[0], data[1]);
        Max = new Vector2f(data[2], data[3]);
    }

    public Rect(Vector2f min, Vector2f max)
    {
        Min = min;
        Max = max;
    }

    public Rect(Vector4f vector)
    {
        Min = vector.Xy;
        Max = vector.Zw;
    }

    public static readonly Rect Full = new(-1.0f, -1.0f, 1.0f, 1.0f);
    public static readonly Rect Positive = new(0.0f, 0.0f, 1.0f, 1.0f);
    public static readonly Rect Zero = new(0.0f, 0.0f, 0.0f, 0.0f);

    public readonly float[] Data => [Min.X, Min.Y, Max.X, Max.Y];

    public readonly float Bottom => Max.Y;
    public readonly Vector2f Center => (Max + Min) * 0.5f;
    public readonly bool Defined => !float.IsPositiveInfinity(Min.X);
    public readonly Vector2f HalfSize => (Max - Min) * 0.5f;
    public readonly float Left => Min.X;
    public readonly float Right => Max.X;
    public readonly Vector2f Size => Max - Min;
    public readonly float Top => Min.Y;

    public void Define(Rect rect)
    {
        Min = rect.Min;
        Max = rect.Max;
    }

    public void Define(Vector2f min, Vector2f max)
    {
        Min = min;
        Max = max;
    }

    public void Define(Vector2f point)
    {
        Min = point;
        Max = point;
    }

    public void Clear()
    {
        Min = new Vector2f(float.PositiveInfinity, float.PositiveInfinity);
        Max = new Vector2f(float.NegativeInfinity, float.NegativeInfinity);
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

    public readonly Intersection IsInside(Vector2f point)
    {
        return point.X < Min.X || point.Y < Min.Y || point.X > Max.X || point.Y > Max.Y
            ? Intersection.Outside
            : Intersection.Inside;
    }

    public readonly Intersection IsInside(Rect rect)
    {
        if (rect.Max.X < Min.X || rect.Min.X > Max.X || rect.Max.Y < Min.Y || rect.Min.Y > Max.Y)
            return Intersection.Outside;
        if (rect.Min.X < Min.X || rect.Max.X > Max.X || rect.Min.Y < Min.Y || rect.Max.Y > Max.Y)
            return Intersection.Intersects;
        return Intersection.Inside;
    }

    public void Merge(Vector2f point)
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

    public readonly bool Equals(Rect other) => Min.Equals(other.Min) && Max.Equals(other.Max);
    public override bool Equals(object obj) => obj is Rect other && Equals(other);
    public override readonly int GetHashCode() => HashCode.Combine(Min, Max);
    public readonly Vector4f ToVector4() => new(Min.X, Min.Y, Max.X, Max.Y);
    public readonly uint ToHash() => unchecked((uint)(Min.GetHashCode() * 37 + Max.GetHashCode()));
    public override string ToString() => string.Format(CultureInfo.InvariantCulture, "{0} {1} {2} {3}", Min.X, Min.Y, Max.X, Max.Y);

    public static bool operator ==(Rect lhs, Rect rhs) => lhs.Equals(rhs);
    public static bool operator !=(Rect lhs, Rect rhs) => !lhs.Equals(rhs);
    public static Rect operator +(Rect lhs, Rect rhs) => new(lhs.Min + rhs.Min, lhs.Max + rhs.Max);
    public static Rect operator -(Rect lhs, Rect rhs) => new(lhs.Min - rhs.Min, lhs.Max - rhs.Max);
    public static Rect operator *(Rect lhs, float rhs) => new(lhs.Min * rhs, lhs.Max * rhs);
    public static Rect operator /(Rect lhs, float rhs) => new(lhs.Min / rhs, lhs.Max / rhs);
}
