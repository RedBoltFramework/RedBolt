using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace RedBolt.Math;
using Math = global::System.Math;


/// <summary>Three-dimensional vector with integer values.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct BaseIntegerVector3<T> : IEquatable<BaseIntegerVector3<T>>
    where T : unmanaged, IBinaryInteger<T>
{
    /// <summary>Construct a zero vector.</summary>
    public BaseIntegerVector3()
    {
        X = T.Zero;
        Y = T.Zero;
        Z = T.Zero;
    }

    /// <summary>Construct from coordinates.</summary>
    public BaseIntegerVector3(T x, T y, T z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    /// <summary>Construct from an array.</summary>
    public BaseIntegerVector3(ReadOnlySpan<T> data)
    {
        X = data[0];
        Y = data[1];
        Z = data[2];
    }

    /// <summary>Convert to tuple.</summary>
    public readonly (T X, T Y, T Z) Tie() => (X, Y, Z);

    /// <summary>Test for equality with another vector.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(BaseIntegerVector3<T> lhs, BaseIntegerVector3<T> rhs) => lhs.X == rhs.X && lhs.Y == rhs.Y && lhs.Z == rhs.Z;

    /// <summary>Test for inequality with another vector.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(BaseIntegerVector3<T> lhs, BaseIntegerVector3<T> rhs) => !(lhs == rhs);

    /// <summary>Lexicographic comparison for sorting.</summary>
    public static bool operator <(BaseIntegerVector3<T> lhs, BaseIntegerVector3<T> rhs) =>
        lhs.X < rhs.X || lhs.X == rhs.X && (lhs.Y < rhs.Y || lhs.Y == rhs.Y && lhs.Z < rhs.Z);

    public static bool operator >(BaseIntegerVector3<T> lhs, BaseIntegerVector3<T> rhs) => rhs < lhs;

    public static bool operator <=(BaseIntegerVector3<T> lhs, BaseIntegerVector3<T> rhs) => !(rhs < lhs);

    public static bool operator >=(BaseIntegerVector3<T> lhs, BaseIntegerVector3<T> rhs) => !(lhs < rhs);

    /// <summary>Add a vector.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BaseIntegerVector3<T> operator +(BaseIntegerVector3<T> lhs, BaseIntegerVector3<T> rhs) => new(lhs.X + rhs.X, lhs.Y + rhs.Y, lhs.Z + rhs.Z);

    /// <summary>Return negation.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BaseIntegerVector3<T> operator -(BaseIntegerVector3<T> value) =>
        new(T.Zero - value.X, T.Zero - value.Y, T.Zero - value.Z);

    /// <summary>Subtract a vector.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BaseIntegerVector3<T> operator -(BaseIntegerVector3<T> lhs, BaseIntegerVector3<T> rhs) => new(lhs.X - rhs.X, lhs.Y - rhs.Y, lhs.Z - rhs.Z);

    /// <summary>Multiply with a scalar.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BaseIntegerVector3<T> operator *(BaseIntegerVector3<T> lhs, T rhs) => new(lhs.X * rhs, lhs.Y * rhs, lhs.Z * rhs);

    /// <summary>Multiply BaseIntegerVector3 with a scalar.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BaseIntegerVector3<T> operator *(T lhs, BaseIntegerVector3<T> rhs) => rhs * lhs;

    /// <summary>Multiply with a vector.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BaseIntegerVector3<T> operator *(BaseIntegerVector3<T> lhs, BaseIntegerVector3<T> rhs) => new(lhs.X * rhs.X, lhs.Y * rhs.Y, lhs.Z * rhs.Z);

    /// <summary>Divide by a scalar.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BaseIntegerVector3<T> operator /(BaseIntegerVector3<T> lhs, T rhs) => new(lhs.X / rhs, lhs.Y / rhs, lhs.Z / rhs);

    /// <summary>Divide by a vector.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BaseIntegerVector3<T> operator /(BaseIntegerVector3<T> lhs, BaseIntegerVector3<T> rhs) => new(lhs.X / rhs.X, lhs.Y / rhs.Y, lhs.Z / rhs.Z);

    /// <summary>Return integer data.</summary>
    public ReadOnlySpan<T> Data() => MemoryMarshal.CreateReadOnlySpan(ref X, 3);

    /// <summary>Return integer data.</summary>
    public Span<T> MutableData() => MemoryMarshal.CreateSpan(ref X, 3);

    // C# has no equivalent of the C++ associated ScalarType template parameter.
    // template <class OtherVectorType> constexpr OtherVectorType Cast(typename OtherVectorType::ScalarType w = {}) const
    // template <class OtherVectorType> constexpr OtherVectorType CastXZ(typename OtherVectorType::ScalarType w = {}) const

    /// <summary>Return Vector2 vector (z component is ignored).</summary>
    public Vector2f ToVector2() => new(float.CreateChecked(X), float.CreateChecked(Y));

    /// <summary>Return IntVector2 vector (z component is ignored).</summary>
    public Vector2i ToIntVector2() => new(int.CreateChecked(X), int.CreateChecked(Y));

    /// <summary>Return Vector3 vector.</summary>
    public Vector3f ToVector3() => new(float.CreateChecked(X), float.CreateChecked(Y), float.CreateChecked(Z));

    /// <summary>Return Vector4 vector.</summary>
    public Vector4f ToVector4(float w = 0.0f) => new(float.CreateChecked(X), float.CreateChecked(Y), float.CreateChecked(Z), w);

    /// <summary>Return length.</summary>
    public T Length => T.CreateTruncating(Math.Sqrt(double.CreateChecked(X * X + Y * Y + Z * Z)));

    public bool Equals(BaseIntegerVector3<T> other) => this == other;

    public override bool Equals(object? obj) => obj is BaseIntegerVector3<T> other && Equals(other);

    public override int GetHashCode() => unchecked((int)ToHash());

    /// <summary>Return as string.</summary>
    public override string ToString() => Unsafe.SizeOf<T>() <= 4
        ? string.Format(CultureInfo.InvariantCulture, "{0} {1} {2}", X, Y, Z)
        : "? ? ?";

    /// <summary>Return hash value for HashSet &amp; HashMap.</summary>
    public uint ToHash()
    {
        uint hash = 0;
        hash ^= unchecked((uint)X.GetHashCode() + 0x9e3779b9u + (hash << 6) + (hash >> 2));
        hash ^= unchecked((uint)Y.GetHashCode() + 0x9e3779b9u + (hash << 6) + (hash >> 2));
        hash ^= unchecked((uint)Z.GetHashCode() + 0x9e3779b9u + (hash << 6) + (hash >> 2));
        return hash;
    }

    /// <summary>X coordinate.</summary>
    public T X;

    /// <summary>Y coordinate.</summary>
    public T Y;

    /// <summary>Z coordinate.</summary>
    public T Z;

    /// <summary>Zero vector.</summary>
    public static readonly BaseIntegerVector3<T> Zero = new();
    /// <summary>(-1,0,0) vector.</summary>
    public static readonly BaseIntegerVector3<T> Left = new(T.Zero - T.One, T.Zero, T.Zero);
    /// <summary>(1,0,0) vector.</summary>
    public static readonly BaseIntegerVector3<T> Right = new(T.One, T.Zero, T.Zero);
    /// <summary>(0,1,0) vector.</summary>
    public static readonly BaseIntegerVector3<T> Up = new(T.Zero, T.One, T.Zero);
    /// <summary>(0,-1,0) vector.</summary>
    public static readonly BaseIntegerVector3<T> Down = new(T.Zero, T.Zero - T.One, T.Zero);
    /// <summary>(0,0,1) vector.</summary>
    public static readonly BaseIntegerVector3<T> Forward = new(T.Zero, T.Zero, T.One);
    /// <summary>(0,0,-1) vector.</summary>
    public static readonly BaseIntegerVector3<T> Back = new(T.Zero, T.Zero, T.Zero - T.One);
    /// <summary>(1,1,1) vector.</summary>
    public static readonly BaseIntegerVector3<T> One = new(T.One, T.One, T.One);

    /// <summary>Per-component min of two integer 3-vectors.</summary>
    public static BaseIntegerVector3<T> Min(BaseIntegerVector3<T> lhs, BaseIntegerVector3<T> rhs) => new(
        lhs.X < rhs.X ? lhs.X : rhs.X,
        lhs.Y < rhs.Y ? lhs.Y : rhs.Y,
        lhs.Z < rhs.Z ? lhs.Z : rhs.Z);

    /// <summary>Per-component max of two integer 3-vectors.</summary>
    public static BaseIntegerVector3<T> Max(BaseIntegerVector3<T> lhs, BaseIntegerVector3<T> rhs) => new(
        lhs.X > rhs.X ? lhs.X : rhs.X,
        lhs.Y > rhs.Y ? lhs.Y : rhs.Y,
        lhs.Z > rhs.Z ? lhs.Z : rhs.Z);

    /// <summary>Per-component absolute value of integer 3-vector.</summary>
    public static BaseIntegerVector3<T> Abs(BaseIntegerVector3<T> vec) => new(T.Abs(vec.X), T.Abs(vec.Y), T.Abs(vec.Z));
}
