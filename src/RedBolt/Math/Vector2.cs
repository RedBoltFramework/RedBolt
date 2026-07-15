using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace RedBolt;

[StructLayout(LayoutKind.Sequential)]
public struct BaseRealVector2<T> : IEquatable<BaseRealVector2<T>>, IApproximateEquatable<BaseRealVector2<T>>
    where T : unmanaged, IFloatingPointIeee754<T>
{
    public T X;
    public T Y;

    public BaseRealVector2(T x, T y)
    {
        X = x;
        Y = y;
    }

    public BaseRealVector2(T[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.Length < 2)
            throw new ArgumentException("Expected at least two elements.", nameof(data));

        X = data[0];
        Y = data[1];
    }

    public static readonly BaseRealVector2<T> ZERO = new(T.Zero, T.Zero);
    public static readonly BaseRealVector2<T> LEFT = new(-T.One, T.Zero);
    public static readonly BaseRealVector2<T> RIGHT = new(T.One, T.Zero);
    public static readonly BaseRealVector2<T> UP = new(T.Zero, T.One);
    public static readonly BaseRealVector2<T> DOWN = new(T.Zero, -T.One);
    public static readonly BaseRealVector2<T> ONE = new(T.One, T.One);

    public static BaseRealVector2<T> Zero => ZERO;
    public static BaseRealVector2<T> Left => LEFT;
    public static BaseRealVector2<T> Right => RIGHT;
    public static BaseRealVector2<T> Up => UP;
    public static BaseRealVector2<T> Down => DOWN;
    public static BaseRealVector2<T> One => ONE;

    public T[] Data => [X, Y];
    public bool IsNaN => T.IsNaN(X) || T.IsNaN(Y);
    public bool IsInf => T.IsInfinity(X) || T.IsInfinity(Y);
    public T Length => T.Sqrt(LengthSquared);
    public T LengthSquared => X * X + Y * Y;
    public BaseRealVector2<T> Abs => new(T.Abs(X), T.Abs(Y));

    public BaseRealVector2<T> Normalized
    {
        get
        {
            var lenSquared = LengthSquared;
            if (!MathDefs.Equals(double.CreateChecked(lenSquared), 1.0) && lenSquared > T.Zero)
                return this * (T.One / T.Sqrt(lenSquared));
            return this;
        }
    }

    public BaseRealVector2<T> NormalizedOrDefault(BaseRealVector2<T> defaultValue = default, T? epsilon = null)
    {
        var eps = epsilon ?? T.CreateChecked(MathDefs.LargeEpsilon);
        return LengthSquared < eps * eps ? defaultValue : this * (T.One / Length);
    }

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

    public void Normalize()
    {
        var lenSquared = LengthSquared;
        if (!MathDefs.Equals(double.CreateChecked(lenSquared), 1.0) && lenSquared > T.Zero)
            this *= T.One / T.Sqrt(lenSquared);
    }

    public T DotProduct(BaseRealVector2<T> rhs) => X * rhs.X + Y * rhs.Y;

    public T AbsDotProduct(BaseRealVector2<T> rhs) => T.Abs(X * rhs.X) + T.Abs(Y * rhs.Y);

    public T CrossProduct(BaseRealVector2<T> rhs) => X * rhs.Y - Y * rhs.X;

    public T ProjectOntoAxis(BaseRealVector2<T> axis) => DotProduct(axis.Normalized);

    public T ProjectOntoLineScalar(BaseRealVector2<T> from, BaseRealVector2<T> to, bool clamped = false)
    {
        var direction = to - from;
        var lengthSquared = direction.LengthSquared;
        var factor = (this - from).DotProduct(direction) / lengthSquared;
        return clamped ? T.Clamp(factor, T.Zero, T.One) : factor;
    }

    public BaseRealVector2<T> ProjectOntoLine(BaseRealVector2<T> from, BaseRealVector2<T> to, bool clamped = false) => from.Lerp(to, ProjectOntoLineScalar(from, to, clamped));

    public T DistanceToPoint(BaseRealVector2<T> point) => (this - point).Length;

    public T ScalarCrossProduct(BaseRealVector2<T> rhs) => Y * rhs.X - X * rhs.Y;

    public T Angle(BaseRealVector2<T> rhs) => T.Acos(DotProduct(rhs) / (Length * rhs.Length)) * T.CreateChecked(MathDefs.RadToDeg);

    public T SignedAngle(BaseRealVector2<T> rhs) => Angle(rhs) * (ScalarCrossProduct(rhs) >= T.Zero ? T.One : -T.One);

    public BaseRealVector2<T> Lerp(BaseRealVector2<T> rhs, T t) => this * (T.One - t) + rhs * t;

    public BaseRealVector2<T> ReNormalized(T minLength, T maxLength, BaseRealVector2<T> defaultValue = default, T? epsilon = null)
    {
        var typedEpsilon = epsilon ?? T.CreateChecked(MathDefs.LargeEpsilon);
        var lenSquared = LengthSquared;
        if (lenSquared < typedEpsilon * typedEpsilon)
            return defaultValue;

        var length = T.Sqrt(lenSquared);
        var newLength = T.Clamp(length, minLength, maxLength);
        return this * (newLength / length);
    }

    public BaseRealVector2<T> GetOrthogonalClockwise() => new(Y, -X);

    public BaseRealVector2<T> GetOrthogonalCounterClockwise() => new(-Y, X);

    public Vector2i ToIntVector2() => new(int.CreateChecked(X), int.CreateChecked(Y));

    public Vector3i ToIntVector3(int z = 0) => new(int.CreateChecked(X), int.CreateChecked(Y), z);

    public BaseRealVector3<T> ToVector3(T z = default) => new(X, Y, z);

    public Vector4f ToVector4(float z = 0.0f, float w = 0.0f) => new(float.CreateChecked(X), float.CreateChecked(Y), z, w);

    public bool ApproximatelyEquivalent(BaseRealVector2<T> rhs, float epsilon = MathDefs.Epsilon)
    {
        var typedEpsilon = T.CreateChecked(epsilon);
        return T.Abs(X - rhs.X) <= typedEpsilon && T.Abs(Y - rhs.Y) <= typedEpsilon;
    }

    public bool Equals(BaseRealVector2<T> other) => this == other;

    public override bool Equals(object? obj) => obj is BaseRealVector2<T> other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(X, Y);

    public uint ToHash() => unchecked((uint)(X.GetHashCode() * 37 + Y.GetHashCode()));

    public override string ToString() => string.Format(CultureInfo.InvariantCulture, "{0} {1}", X, Y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(BaseRealVector2<T> lhs, BaseRealVector2<T> rhs) => lhs.X == rhs.X && lhs.Y == rhs.Y;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(BaseRealVector2<T> lhs, BaseRealVector2<T> rhs) => !(lhs == rhs);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BaseRealVector2<T> operator +(BaseRealVector2<T> lhs, BaseRealVector2<T> rhs) => new(lhs.X + rhs.X, lhs.Y + rhs.Y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BaseRealVector2<T> operator -(BaseRealVector2<T> value) => new(-value.X, -value.Y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BaseRealVector2<T> operator -(BaseRealVector2<T> lhs, BaseRealVector2<T> rhs) => new(lhs.X - rhs.X, lhs.Y - rhs.Y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BaseRealVector2<T> operator *(BaseRealVector2<T> lhs, T rhs) => new(lhs.X * rhs, lhs.Y * rhs);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BaseRealVector2<T> operator *(T lhs, BaseRealVector2<T> rhs) => rhs * lhs;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BaseRealVector2<T> operator *(BaseRealVector2<T> lhs, BaseRealVector2<T> rhs) => new(lhs.X * rhs.X, lhs.Y * rhs.Y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BaseRealVector2<T> operator /(BaseRealVector2<T> lhs, T rhs) => new(lhs.X / rhs, lhs.Y / rhs);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BaseRealVector2<T> operator /(BaseRealVector2<T> lhs, BaseRealVector2<T> rhs) => new(lhs.X / rhs.X, lhs.Y / rhs.Y);

    public static BaseRealVector2<T> Lerp(BaseRealVector2<T> lhs, BaseRealVector2<T> rhs, BaseRealVector2<T> t) => lhs + (rhs - lhs) * t;

    public static BaseRealVector2<T> Min(BaseRealVector2<T> lhs, BaseRealVector2<T> rhs) => new(T.Min(lhs.X, rhs.X), T.Min(lhs.Y, rhs.Y));

    public static BaseRealVector2<T> Max(BaseRealVector2<T> lhs, BaseRealVector2<T> rhs) => new(T.Max(lhs.X, rhs.X), T.Max(lhs.Y, rhs.Y));

    public static BaseRealVector2<T> Floor(BaseRealVector2<T> vec) => new(T.Floor(vec.X), T.Floor(vec.Y));

    public static BaseRealVector2<T> Round(BaseRealVector2<T> vec) => new(T.Round(vec.X), T.Round(vec.Y));

    public static BaseRealVector2<T> Ceil(BaseRealVector2<T> vec) => new(T.Ceiling(vec.X), T.Ceiling(vec.Y));

    public static BaseRealVector2<T> Sqrt(BaseRealVector2<T> vec) => new(T.Sqrt(vec.X), T.Sqrt(vec.Y));

    public static T StableRandom(BaseRealVector2<T> seed)
    {
        var value = T.Sin(seed.DotProduct(new(T.CreateChecked(12.9898), T.CreateChecked(78.233))) * T.CreateChecked(MathDefs.RadToDeg)) * T.CreateChecked(43758.5453);
        return value - T.Floor(value);
    }

    public static T StableRandom(T seed) => StableRandom(new BaseRealVector2<T>(seed, seed));

    public static Vector2i FloorToInt(BaseRealVector2<T> vec) => new(int.CreateChecked(T.Floor(vec.X)), int.CreateChecked(T.Floor(vec.Y)));

    public static Vector2i RoundToInt(BaseRealVector2<T> vec) => new(int.CreateChecked(T.Round(vec.X)), int.CreateChecked(T.Round(vec.Y)));

    public static Vector2i CeilToInt(BaseRealVector2<T> vec) => new(int.CreateChecked(T.Ceiling(vec.X)), int.CreateChecked(T.Ceiling(vec.Y)));
}
