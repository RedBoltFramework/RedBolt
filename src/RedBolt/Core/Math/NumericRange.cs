namespace RedBolt.Math;

using System.Numerics;
using System.Runtime.InteropServices;

/// <summary>
/// Numerical range (pair of min and max value). Invalid if flipped.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct NumericRange<T> : IEquatable<NumericRange<T>>
    where T : unmanaged, INumber<T>, IMinMaxValue<T>
{
    public T First;
    public T Second;

    /// <summary>
    /// Construct invalid.
    /// </summary>
    public NumericRange()
    {
        First = T.MaxValue;
        Second = T.MinValue;
    }

    /// <summary>
    /// Construct valid.
    /// </summary>
    public NumericRange(T minValue, T maxValue)
    {
        First = minValue;
        Second = maxValue;
    }

    /// <summary>
    /// Return whether the range is valid.
    /// </summary>
    public readonly bool IsValid() => First <= Second;

    /// <summary>
    /// Return whether the range intersects another.
    /// </summary>
    public readonly bool Intersect(NumericRange<T> rhs) => First <= rhs.Second && rhs.First <= Second;

    /// <summary>
    /// Return whether the range contains a value (including borders).
    /// </summary>
    public readonly bool ContainsInclusive(T value) => First <= value && value <= Second;

    /// <summary>
    /// Return whether the range contains a value (excluding borders).
    /// </summary>
    public readonly bool ContainsExclusive(T value) => First < value && value < Second;

    /// <summary>
    /// Accumulate range.
    /// </summary>
    public static NumericRange<T> operator |(NumericRange<T> lhs, NumericRange<T> rhs)
    {
        lhs.First = T.Min(lhs.First, rhs.First);
        lhs.Second = T.Max(lhs.Second, rhs.Second);
        return lhs;
    }

    /// <summary>
    /// Trim range.
    /// </summary>
    public static NumericRange<T> operator &(NumericRange<T> lhs, NumericRange<T> rhs)
    {
        lhs.First = T.Max(lhs.First, rhs.First);
        lhs.Second = T.Min(lhs.Second, rhs.Second);
        return lhs;
    }

    public readonly bool Equals(NumericRange<T> other) => First == other.First && Second == other.Second;

    public override readonly bool Equals(object? obj) => obj is NumericRange<T> other && Equals(other);

    public override readonly int GetHashCode() => HashCode.Combine(First, Second);

    public static bool operator ==(NumericRange<T> lhs, NumericRange<T> rhs) => lhs.Equals(rhs);

    public static bool operator !=(NumericRange<T> lhs, NumericRange<T> rhs) => !lhs.Equals(rhs);

}
