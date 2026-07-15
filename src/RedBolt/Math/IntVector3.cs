using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace RedBolt;

[StructLayout(LayoutKind.Sequential)]
public struct BaseIntVector3<T> : IEquatable<BaseIntVector3<T>>
    where T : unmanaged, IBinaryInteger<T>, ISignedNumber<T>
{
    public T X;
    public T Y;
    public T Z;

    public BaseIntVector3(T x = default, T y = default, T z = default)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public BaseIntVector3(T[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.Length < 3)
            throw new ArgumentException("At least three components are required.", nameof(data));
        X = data[0];
        Y = data[1];
        Z = data[2];
    }

    public static readonly BaseIntVector3<T> Zero = new();
    public static readonly BaseIntVector3<T> Left = new(-T.One, T.Zero, T.Zero);
    public static readonly BaseIntVector3<T> Right = new(T.One, T.Zero, T.Zero);
    public static readonly BaseIntVector3<T> Up = new(T.Zero, T.One, T.Zero);
    public static readonly BaseIntVector3<T> Down = new(T.Zero, -T.One, T.Zero);
    public static readonly BaseIntVector3<T> Forward = new(T.Zero, T.Zero, T.One);
    public static readonly BaseIntVector3<T> Back = new(T.Zero, T.Zero, -T.One);
    public static readonly BaseIntVector3<T> One = new(T.One, T.One, T.One);

    public T[] Data => [X, Y, Z];

    public float Length => MathF.Sqrt(float.CreateChecked(X * X + Y * Y + Z * Z));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(BaseIntVector3<T> lhs, BaseIntVector3<T> rhs) => lhs.X == rhs.X && lhs.Y == rhs.Y && lhs.Z == rhs.Z;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(BaseIntVector3<T> lhs, BaseIntVector3<T> rhs) => !(lhs == rhs);

    public static bool operator <(BaseIntVector3<T> lhs, BaseIntVector3<T> rhs) =>
        lhs.X < rhs.X || lhs.X == rhs.X && (lhs.Y < rhs.Y || lhs.Y == rhs.Y && lhs.Z < rhs.Z);

    public static bool operator >(BaseIntVector3<T> lhs, BaseIntVector3<T> rhs) => rhs < lhs;

    public static bool operator <=(BaseIntVector3<T> lhs, BaseIntVector3<T> rhs) => !(rhs < lhs);

    public static bool operator >=(BaseIntVector3<T> lhs, BaseIntVector3<T> rhs) => !(lhs < rhs);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BaseIntVector3<T> operator +(BaseIntVector3<T> lhs, BaseIntVector3<T> rhs) => new(lhs.X + rhs.X, lhs.Y + rhs.Y, lhs.Z + rhs.Z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BaseIntVector3<T> operator -(BaseIntVector3<T> value) => new(-value.X, -value.Y, -value.Z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BaseIntVector3<T> operator -(BaseIntVector3<T> lhs, BaseIntVector3<T> rhs) => new(lhs.X - rhs.X, lhs.Y - rhs.Y, lhs.Z - rhs.Z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BaseIntVector3<T> operator *(BaseIntVector3<T> lhs, T rhs) => new(lhs.X * rhs, lhs.Y * rhs, lhs.Z * rhs);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BaseIntVector3<T> operator *(T lhs, BaseIntVector3<T> rhs) => rhs * lhs;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BaseIntVector3<T> operator *(BaseIntVector3<T> lhs, BaseIntVector3<T> rhs) => new(lhs.X * rhs.X, lhs.Y * rhs.Y, lhs.Z * rhs.Z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BaseIntVector3<T> operator /(BaseIntVector3<T> lhs, T rhs) => new(lhs.X / rhs, lhs.Y / rhs, lhs.Z / rhs);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BaseIntVector3<T> operator /(BaseIntVector3<T> lhs, BaseIntVector3<T> rhs) => new(lhs.X / rhs.X, lhs.Y / rhs.Y, lhs.Z / rhs.Z);

    public static BaseIntVector3<T> Min(BaseIntVector3<T> lhs, BaseIntVector3<T> rhs) => new(
        lhs.X < rhs.X ? lhs.X : rhs.X,
        lhs.Y < rhs.Y ? lhs.Y : rhs.Y,
        lhs.Z < rhs.Z ? lhs.Z : rhs.Z);

    public static BaseIntVector3<T> Max(BaseIntVector3<T> lhs, BaseIntVector3<T> rhs) => new(
        lhs.X > rhs.X ? lhs.X : rhs.X,
        lhs.Y > rhs.Y ? lhs.Y : rhs.Y,
        lhs.Z > rhs.Z ? lhs.Z : rhs.Z);

    public static BaseIntVector3<T> Abs(BaseIntVector3<T> vec) => new(T.Abs(vec.X), T.Abs(vec.Y), T.Abs(vec.Z));

    // TODO: port - C++ compound assignment operators:
    // constexpr BaseIntegerVector3& operator+=(const BaseIntegerVector3& rhs) { x_ += rhs.x_; y_ += rhs.y_; z_ += rhs.z_; return *this; }
    // constexpr BaseIntegerVector3& operator-=(const BaseIntegerVector3& rhs) { x_ -= rhs.x_; y_ -= rhs.y_; z_ -= rhs.z_; return *this; }
    // constexpr BaseIntegerVector3& operator*=(T rhs) { x_ *= rhs; y_ *= rhs; z_ *= rhs; return *this; }
    // constexpr BaseIntegerVector3& operator*=(const BaseIntegerVector3& rhs) { x_ *= rhs.x_; y_ *= rhs.y_; z_ *= rhs.z_; return *this; }
    // constexpr BaseIntegerVector3& operator/=(T rhs) { x_ /= rhs; y_ /= rhs; z_ /= rhs; return *this; }
    // constexpr BaseIntegerVector3& operator/=(const BaseIntegerVector3& rhs) { x_ /= rhs.x_; y_ /= rhs.y_; z_ /= rhs.z_; return *this; }
    // TODO: port - C++ code:
    // constexpr const T* Data() const { return &x_; }
    // constexpr T* MutableData() { return &x_; }
    // TODO: port - C++ code:
    // template <class OtherVectorType> constexpr OtherVectorType Cast(typename OtherVectorType::ScalarType w = {}) const
    // template <class OtherVectorType> constexpr OtherVectorType CastXZ(typename OtherVectorType::ScalarType w = {}) const

    public Vector2f ToVector2() => new(float.CreateChecked(X), float.CreateChecked(Y));

    public BaseIntVector2<T> ToIntVector2() => new(X, Y);

    public Vector3f ToVector3() => new(float.CreateChecked(X), float.CreateChecked(Y), float.CreateChecked(Z));

    public Vector4f ToVector4(float w = 0.0f) => new(float.CreateChecked(X), float.CreateChecked(Y), float.CreateChecked(Z), w);

    public bool Equals(BaseIntVector3<T> other) => this == other;

    public override bool Equals(object? obj) => obj is BaseIntVector3<T> other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(X, Y, Z);

    public uint ToHash() => unchecked((uint)((X.GetHashCode() * 37 + Y.GetHashCode()) * 37 + Z.GetHashCode()));

    public override string ToString() => string.Format(CultureInfo.InvariantCulture, "{0} {1} {2}", X, Y, Z);
}
