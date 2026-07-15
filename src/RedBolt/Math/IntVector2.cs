using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace RedBolt;

[StructLayout(LayoutKind.Sequential)]
public struct BaseIntVector2<T> : IEquatable<BaseIntVector2<T>>
    where T : unmanaged, IBinaryInteger<T>, ISignedNumber<T>
{
    public T X;
    public T Y;

    public BaseIntVector2(T x = default, T y = default)
    {
        X = x;
        Y = y;
    }

    public BaseIntVector2(T[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.Length < 2)
            throw new ArgumentException("Expected at least two elements.", nameof(data));

        X = data[0];
        Y = data[1];
    }

    public static readonly BaseIntVector2<T> Zero = new();
    public static readonly BaseIntVector2<T> Left = new(-T.One, T.Zero);
    public static readonly BaseIntVector2<T> Right = new(T.One, T.Zero);
    public static readonly BaseIntVector2<T> Up = new(T.Zero, T.One);
    public static readonly BaseIntVector2<T> Down = new(T.Zero, -T.One);
    public static readonly BaseIntVector2<T> One = new(T.One, T.One);

    public T[] Data => [X, Y];

    public float Length => MathF.Sqrt(float.CreateChecked(X * X + Y * Y));

    public T this[int index]
    {
        get => index switch
        {
            0 => X,
            1 => Y,
            _ => throw new IndexOutOfRangeException()
        };
        set
        {
            switch (index)
            {
                case 0:
                    X = value;
                    break;
                case 1:
                    Y = value;
                    break;
                default:
                    throw new IndexOutOfRangeException();
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(BaseIntVector2<T> lhs, BaseIntVector2<T> rhs) => lhs.X == rhs.X && lhs.Y == rhs.Y;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(BaseIntVector2<T> lhs, BaseIntVector2<T> rhs) => !(lhs == rhs);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(BaseIntVector2<T> lhs, BaseIntVector2<T> rhs) => lhs.X < rhs.X || lhs.X == rhs.X && lhs.Y < rhs.Y;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(BaseIntVector2<T> lhs, BaseIntVector2<T> rhs) => rhs < lhs;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(BaseIntVector2<T> lhs, BaseIntVector2<T> rhs) => !(rhs < lhs);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(BaseIntVector2<T> lhs, BaseIntVector2<T> rhs) => !(lhs < rhs);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BaseIntVector2<T> operator +(BaseIntVector2<T> lhs, BaseIntVector2<T> rhs) => new(lhs.X + rhs.X, lhs.Y + rhs.Y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BaseIntVector2<T> operator -(BaseIntVector2<T> value) => new(-value.X, -value.Y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BaseIntVector2<T> operator -(BaseIntVector2<T> lhs, BaseIntVector2<T> rhs) => new(lhs.X - rhs.X, lhs.Y - rhs.Y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BaseIntVector2<T> operator *(BaseIntVector2<T> lhs, T rhs) => new(lhs.X * rhs, lhs.Y * rhs);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BaseIntVector2<T> operator *(T lhs, BaseIntVector2<T> rhs) => rhs * lhs;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BaseIntVector2<T> operator *(BaseIntVector2<T> lhs, BaseIntVector2<T> rhs) => new(lhs.X * rhs.X, lhs.Y * rhs.Y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BaseIntVector2<T> operator /(BaseIntVector2<T> lhs, T rhs) => new(lhs.X / rhs, lhs.Y / rhs);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BaseIntVector2<T> operator /(BaseIntVector2<T> lhs, BaseIntVector2<T> rhs) => new(lhs.X / rhs.X, lhs.Y / rhs.Y);

    public static BaseIntVector2<T> Min(BaseIntVector2<T> lhs, BaseIntVector2<T> rhs) => new(lhs.X < rhs.X ? lhs.X : rhs.X, lhs.Y < rhs.Y ? lhs.Y : rhs.Y);

    public static BaseIntVector2<T> Max(BaseIntVector2<T> lhs, BaseIntVector2<T> rhs) => new(lhs.X > rhs.X ? lhs.X : rhs.X, lhs.Y > rhs.Y ? lhs.Y : rhs.Y);

    public static BaseIntVector2<T> Abs(BaseIntVector2<T> vec) => new(T.Abs(vec.X), T.Abs(vec.Y));

    // TODO: port - C++ code:
    // constexpr const T* Data() const { return &x_; }
    // constexpr T* MutableData() { return &x_; }
    // TODO: port - C++ code:
    // template <class OtherVectorType> constexpr OtherVectorType Cast(typename OtherVectorType::ScalarType z = {}, typename OtherVectorType::ScalarType w = {}) const
    // template <class OtherVectorType> constexpr OtherVectorType CastXZ(typename OtherVectorType::ScalarType z = {}, typename OtherVectorType::ScalarType w = {}) const
    // Vector4 ToVector4(float z = 0.0f, float w = 0.0f) const;

    public Vector2f ToVector2() => new(float.CreateChecked(X), float.CreateChecked(Y));

    public Vector3f ToVector3() => new(float.CreateChecked(X), float.CreateChecked(Y), 0.0f);

    public BaseIntVector3<T> ToIntVector3() => new(X, Y, T.Zero);

    public bool Equals(BaseIntVector2<T> other) => this == other;

    public override bool Equals(object? obj) => obj is BaseIntVector2<T> other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(X, Y);

    public uint ToHash() => unchecked((uint)(X.GetHashCode() * 37 + Y.GetHashCode()));

    public override string ToString() => string.Format(CultureInfo.InvariantCulture, "{0} {1}", X, Y);
}
