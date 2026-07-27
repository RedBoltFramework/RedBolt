using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace RedBolt.Math;
using Math = global::System.Math;


/// <summary>Two-dimensional vector with integer values.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct BaseIntegerVector2<T> : IEquatable<BaseIntegerVector2<T>>
    where T : unmanaged, IBinaryInteger<T>
{
    /// <summary>Construct a zero vector.</summary>
    public BaseIntegerVector2()
    {
        X = T.Zero;
        Y = T.Zero;
    }

    /// <summary>Construct from coordinates.</summary>
    public BaseIntegerVector2(T x, T y)
    {
        X = x;
        Y = y;
    }

    /// <summary>Construct from an array.</summary>
    public BaseIntegerVector2(ReadOnlySpan<T> data)
    {
        X = data[0];
        Y = data[1];
    }

    /// <summary>Convert to tuple.</summary>
    public readonly (T X, T Y) Tie() => (X, Y);

    /// <summary>Test for equality with another vector.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(BaseIntegerVector2<T> lhs, BaseIntegerVector2<T> rhs) => lhs.X == rhs.X && lhs.Y == rhs.Y;

    /// <summary>Test for inequality with another vector.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(BaseIntegerVector2<T> lhs, BaseIntegerVector2<T> rhs) => !(lhs == rhs);

    /// <summary>Lexicographic comparison for sorting.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(BaseIntegerVector2<T> lhs, BaseIntegerVector2<T> rhs) => lhs.X < rhs.X || lhs.X == rhs.X && lhs.Y < rhs.Y;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(BaseIntegerVector2<T> lhs, BaseIntegerVector2<T> rhs) => rhs < lhs;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(BaseIntegerVector2<T> lhs, BaseIntegerVector2<T> rhs) => !(rhs < lhs);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(BaseIntegerVector2<T> lhs, BaseIntegerVector2<T> rhs) => !(lhs < rhs);

    /// <summary>Add a vector.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BaseIntegerVector2<T> operator +(BaseIntegerVector2<T> lhs, BaseIntegerVector2<T> rhs) => new(lhs.X + rhs.X, lhs.Y + rhs.Y);

    /// <summary>Return negation.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BaseIntegerVector2<T> operator -(BaseIntegerVector2<T> value) => new(T.Zero - value.X, T.Zero - value.Y);

    /// <summary>Subtract a vector.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BaseIntegerVector2<T> operator -(BaseIntegerVector2<T> lhs, BaseIntegerVector2<T> rhs) => new(lhs.X - rhs.X, lhs.Y - rhs.Y);

    /// <summary>Multiply with a scalar.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BaseIntegerVector2<T> operator *(BaseIntegerVector2<T> lhs, T rhs) => new(lhs.X * rhs, lhs.Y * rhs);

    /// <summary>Multiply BaseIntegerVector2 with a scalar.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BaseIntegerVector2<T> operator *(T lhs, BaseIntegerVector2<T> rhs) => rhs * lhs;

    /// <summary>Multiply with a vector.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BaseIntegerVector2<T> operator *(BaseIntegerVector2<T> lhs, BaseIntegerVector2<T> rhs) => new(lhs.X * rhs.X, lhs.Y * rhs.Y);

    /// <summary>Divide by a scalar.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BaseIntegerVector2<T> operator /(BaseIntegerVector2<T> lhs, T rhs) => new(lhs.X / rhs, lhs.Y / rhs);

    /// <summary>Divide by a vector.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BaseIntegerVector2<T> operator /(BaseIntegerVector2<T> lhs, BaseIntegerVector2<T> rhs) => new(lhs.X / rhs.X, lhs.Y / rhs.Y);

    /// <summary>Return integer data.</summary>
    public ReadOnlySpan<T> Data() => MemoryMarshal.CreateReadOnlySpan(ref X, 2);

    /// <summary>Return integer data.</summary>
    public Span<T> MutableData() => MemoryMarshal.CreateSpan(ref X, 2);

    // C# has no equivalent of the C++ associated ScalarType template parameter.
    // template <class OtherVectorType> constexpr OtherVectorType Cast(typename OtherVectorType::ScalarType z = {}, typename OtherVectorType::ScalarType w = {}) const
    // template <class OtherVectorType> constexpr OtherVectorType CastXZ(typename OtherVectorType::ScalarType z = {}, typename OtherVectorType::ScalarType w = {}) const

    /// <summary>Return Vector2 vector.</summary>
    public Vector2f ToVector2() => new(float.CreateChecked(X), float.CreateChecked(Y));

    /// <summary>Return IntVector3 vector.</summary>
    public Vector3i ToIntVector3(int z = 0) => new(int.CreateChecked(X), int.CreateChecked(Y), z);

    /// <summary>Return Vector3 vector.</summary>
    public Vector3f ToVector3(float z = 0.0f) => new(float.CreateChecked(X), float.CreateChecked(Y), z);

    /// <summary>Return Vector4 vector.</summary>
    public Vector4f ToVector4(float z = 0.0f, float w = 0.0f) => new(float.CreateChecked(X), float.CreateChecked(Y), z, w);

    /// <summary>Return length.</summary>
    public T Length => T.CreateTruncating(Math.Sqrt(double.CreateChecked(X * X + Y * Y)));

    public bool Equals(BaseIntegerVector2<T> other) => this == other;

    public override bool Equals(object? obj) => obj is BaseIntegerVector2<T> other && Equals(other);

    public override int GetHashCode() => unchecked((int)ToHash());

    /// <summary>Return as string.</summary>
    public override string ToString() => Unsafe.SizeOf<T>() <= 4
        ? string.Format(CultureInfo.InvariantCulture, "{0} {1}", X, Y)
        : "? ? ?";

    /// <summary>Return hash value for HashSet &amp; HashMap.</summary>
    public uint ToHash()
    {
        uint hash = 0;
        hash ^= unchecked((uint)X.GetHashCode() + 0x9e3779b9u + (hash << 6) + (hash >> 2));
        hash ^= unchecked((uint)Y.GetHashCode() + 0x9e3779b9u + (hash << 6) + (hash >> 2));
        return hash;
    }

    /// <summary>X coordinate.</summary>
    public T X;

    /// <summary>Y coordinate.</summary>
    public T Y;

    /// <summary>Zero vector.</summary>
    public static readonly BaseIntegerVector2<T> Zero = new();
    /// <summary>(-1,0) vector.</summary>
    public static readonly BaseIntegerVector2<T> Left = new(T.Zero - T.One, T.Zero);
    /// <summary>(1,0) vector.</summary>
    public static readonly BaseIntegerVector2<T> Right = new(T.One, T.Zero);
    /// <summary>(0,1) vector.</summary>
    public static readonly BaseIntegerVector2<T> Up = new(T.Zero, T.One);
    /// <summary>(0,-1) vector.</summary>
    public static readonly BaseIntegerVector2<T> Down = new(T.Zero, T.Zero - T.One);
    /// <summary>(1,1) vector.</summary>
    public static readonly BaseIntegerVector2<T> One = new(T.One, T.One);

    /// <summary>Per-component min of two integer 2-vectors.</summary>
    public static BaseIntegerVector2<T> Min(BaseIntegerVector2<T> lhs, BaseIntegerVector2<T> rhs) => new(lhs.X < rhs.X ? lhs.X : rhs.X, lhs.Y < rhs.Y ? lhs.Y : rhs.Y);

    /// <summary>Per-component max of two integer 2-vectors.</summary>
    public static BaseIntegerVector2<T> Max(BaseIntegerVector2<T> lhs, BaseIntegerVector2<T> rhs) => new(lhs.X > rhs.X ? lhs.X : rhs.X, lhs.Y > rhs.Y ? lhs.Y : rhs.Y);

    /// <summary>Per-component absolute value of integer 2-vector.</summary>
    public static BaseIntegerVector2<T> Abs(BaseIntegerVector2<T> vec) => new(T.Abs(vec.X), T.Abs(vec.Y));
}
