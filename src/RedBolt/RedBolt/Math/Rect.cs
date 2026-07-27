using System.Globalization;
using System.Runtime.InteropServices;

namespace RedBolt.Math;

/// <summary>Two-dimensional bounding rectangle.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct Rect : ICloneable, IEquatable<Rect>, IApproximateEquatable<Rect>
{
    /// <summary>Construct an undefined rect.</summary>
    public Rect()
    {
        Min = new Vector2f(float.PositiveInfinity, float.PositiveInfinity);
        Max = new Vector2f(float.NegativeInfinity, float.NegativeInfinity);
    }

    /// <summary>Construct from minimum and maximum vectors.</summary>
    public Rect(Vector2f min, Vector2f max)
    {
        Min = min;
        Max = max;
    }

    /// <summary>Construct from coordinates.</summary>
    public Rect(float left, float top, float right, float bottom)
    {
        Min = new Vector2f(left, top);
        Max = new Vector2f(right, bottom);
    }

    /// <summary>Construct from a Vector4.</summary>
    public Rect(Vector4f vector)
    {
        Min = new Vector2f(vector.X, vector.Y);
        Max = new Vector2f(vector.Z, vector.W);
    }

    /// <summary>Construct from a float array.</summary>
    public Rect(ReadOnlySpan<float> data)
    {
        Min = new Vector2f(data[0], data[1]);
        Max = new Vector2f(data[2], data[3]);
    }

    /// <summary>Copy-construct from another rect.</summary>
    public Rect(Rect rect)
    {
        Min = rect.Min;
        Max = rect.Max;
    }

    public readonly Rect Clone() => new(this);

    readonly object ICloneable.Clone() => Clone();

    public readonly bool Equals(Rect other) => Min == other.Min && Max == other.Max;

    public override readonly bool Equals(object? obj) => obj is Rect other && Equals(other);

    public override readonly int GetHashCode() => HashCode.Combine(Min, Max);

    /// <summary>Test for equality with another rect.</summary>
    public static bool operator ==(Rect lhs, Rect rhs) => lhs.Min == rhs.Min && lhs.Max == rhs.Max;

    /// <summary>Test for inequality with another rect.</summary>
    public static bool operator !=(Rect lhs, Rect rhs) => lhs.Min != rhs.Min || lhs.Max != rhs.Max;

    /// <summary>Divide by scalar.</summary>
    public static Rect operator /(Rect lhs, float value) => new(lhs.Min / value, lhs.Max / value);

    /// <summary>Multiply by scalar.</summary>
    public static Rect operator *(Rect lhs, float value) => new(lhs.Min * value, lhs.Max * value);

    /// <summary>Add another rect.</summary>
    public static Rect operator +(Rect lhs, Rect rhs) => new(lhs.Min + rhs.Min, lhs.Max + rhs.Max);

    /// <summary>Subtract another rect.</summary>
    public static Rect operator -(Rect lhs, Rect rhs) => new(lhs.Min - rhs.Min, lhs.Max - rhs.Max);

    /// <summary>Define from another rect.</summary>
    public void Define(Rect rect)
    {
        Min = rect.Min;
        Max = rect.Max;
    }

    /// <summary>Define from minimum and maximum vectors.</summary>
    public void Define(Vector2f min, Vector2f max)
    {
        Min = min;
        Max = max;
    }

    /// <summary>Define from a point.</summary>
    public void Define(Vector2f point)
    {
        Min = Max = point;
    }

    /// <summary>Merge a point.</summary>
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

    /// <summary>Merge a rect.</summary>
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

    /// <summary>Clear to undefined state.</summary>
    public void Clear()
    {
        Min = new Vector2f(float.PositiveInfinity, float.PositiveInfinity);
        Max = new Vector2f(float.NegativeInfinity, float.NegativeInfinity);
    }

    /// <summary>Clip with another rect.</summary>
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
        {
            Min = new Vector2f(float.PositiveInfinity, float.PositiveInfinity);
            Max = new Vector2f(float.NegativeInfinity, float.NegativeInfinity);
        }
    }

    /// <summary>Return true if this rect is defined via a previous call to Define() or Merge().</summary>
    public readonly bool Defined => Min.X != float.PositiveInfinity;

    /// <summary>Return center.</summary>
    public readonly Vector2f Center => (Max + Min) * 0.5f;

    /// <summary>Return size.</summary>
    public readonly Vector2f Size => Max - Min;

    /// <summary>Return half-size.</summary>
    public readonly Vector2f HalfSize => (Max - Min) * 0.5f;

    /// <summary>Test for equality with another rect with epsilon.</summary>
    public readonly bool ApproximatelyEquivalent(Rect rhs, float epsilon = MathDefs.Epsilon) =>
        Min.ApproximatelyEquivalent(rhs.Min, epsilon) && Max.ApproximatelyEquivalent(rhs.Max, epsilon);

    /// <summary>Test whether a point is inside.</summary>
    public readonly Intersection IsInside(Vector2f point)
    {
        if (point.X < Min.X || point.Y < Min.Y || point.X > Max.X || point.Y > Max.Y)
            return Intersection.Outside;
        else
            return Intersection.Inside;
    }

    /// <summary>Test if another rect is inside, outside or intersects.</summary>
    public readonly Intersection IsInside(Rect rect)
    {
        if (rect.Max.X < Min.X || rect.Min.X > Max.X || rect.Max.Y < Min.Y || rect.Min.Y > Max.Y)
            return Intersection.Outside;
        else if (rect.Min.X < Min.X || rect.Max.X > Max.X || rect.Min.Y < Min.Y || rect.Max.Y > Max.Y)
            return Intersection.Intersects;
        else
            return Intersection.Inside;
    }

    /// <summary>Return float data.</summary>
    public ReadOnlySpan<float> Data() => MemoryMarshal.CreateReadOnlySpan(ref Min.X, 4);

    /// <summary>Return as a vector.</summary>
    public readonly Vector4f ToVector4() => new(Min.X, Min.Y, Max.X, Max.Y);

    /// <summary>Return as string.</summary>
    public override readonly string ToString() => string.Format(
        CultureInfo.InvariantCulture, "{0:g} {1:g} {2:g} {3:g}", Min.X, Min.Y, Max.X, Max.Y);

    /// <summary>Return hash value for HashSet &amp; HashMap.</summary>
    public readonly uint ToHash() => unchecked(Min.ToHash() * 37 + Max.ToHash());

    /// <summary>Return left coordinate.</summary>
    public readonly float Left => Min.X;

    /// <summary>Return top coordinate.</summary>
    public readonly float Top => Min.Y;

    /// <summary>Return right coordinate.</summary>
    public readonly float Right => Max.X;

    /// <summary>Return bottom coordinate.</summary>
    public readonly float Bottom => Max.Y;

    /// <summary>Minimum vector.</summary>
    public Vector2f Min;

    /// <summary>Maximum vector.</summary>
    public Vector2f Max;

    /// <summary>Rect in the range (-1, -1) - (1, 1).</summary>
    public static readonly Rect Full = new(-1.0f, -1.0f, 1.0f, 1.0f);

    /// <summary>Rect in the range (0, 0) - (1, 1).</summary>
    public static readonly Rect Positive = new(0.0f, 0.0f, 1.0f, 1.0f);

    /// <summary>Zero-sized rect.</summary>
    public static readonly Rect Zero = new(0.0f, 0.0f, 0.0f, 0.0f);
}
