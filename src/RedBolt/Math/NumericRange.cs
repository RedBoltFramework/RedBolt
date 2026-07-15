using System.Numerics;

namespace RedBolt;

/// <summary>
/// Numerical range (pair of min and max value). Invalid if flipped.
/// </summary>
public struct NumericRange<T> : IEquatable<NumericRange<T>>
    where T : unmanaged, INumber<T>, IMinMaxValue<T>
{
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

    // C++: NumericRange<T>& operator|=(const NumericRange& rhs)
    // {
    //     this->first = ea::min(this->first, rhs.first);
    //     this->second = ea::max(this->second, rhs.second);
    //     return *this;
    // }
    public void Accumulate(NumericRange<T> rhs)
    {
        First = T.Min(First, rhs.First);
        Second = T.Max(Second, rhs.Second);
    }

    // C++: NumericRange<T>& operator&=(const NumericRange& rhs)
    // {
    //     this->first = ea::max(this->first, rhs.first);
    //     this->second = ea::min(this->second, rhs.second);
    //     return *this;
    // }
    public void Trim(NumericRange<T> rhs)
    {
        First = T.Max(First, rhs.First);
        Second = T.Min(Second, rhs.Second);
    }

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

    /// <summary>
    /// Minimum value.
    /// </summary>
    public T First;

    /// <summary>
    /// Maximum value.
    /// </summary>
    public T Second;
}
