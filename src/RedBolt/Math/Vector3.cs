using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace RedBolt;

[StructLayout(LayoutKind.Sequential)]
public struct BaseRealVector3<T> : IEquatable<BaseRealVector3<T>>, IApproximateEquatable<BaseRealVector3<T>>
    where T : unmanaged, IFloatingPointIeee754<T>
{
    public T X;
    public T Y;
    public T Z;

    public BaseRealVector3(T x, T y, T z = default)
    {
        X = x;
        Y = y;
        Z = z;
    }

    // C++: uses static FromXZ(const BaseVector2<T>&, T) instead of a constructor
    public BaseRealVector3(BaseRealVector2<T> vector, T z = default)
    {
        X = vector.X;
        Y = vector.Y;
        Z = z;
    }

    // C++: no constructor from BaseIntegerVector3 — conversion is via Cast<Vector3>() on the integer vector
    public BaseRealVector3(Vector3i vector)
    {
        X = T.CreateChecked(vector.X);
        Y = T.CreateChecked(vector.Y);
        Z = T.CreateChecked(vector.Z);
    }

    public BaseRealVector3(ReadOnlySpan<T> data)
    {
        if (data.Length < 3)
            throw new ArgumentException("Expected at least three elements.", nameof(data));

        X = data[0];
        Y = data[1];
        Z = data[2];
    }

    public static readonly BaseRealVector3<T> ZERO = new(T.Zero, T.Zero, T.Zero);
    public static readonly BaseRealVector3<T> LEFT = new(-T.One, T.Zero, T.Zero);
    public static readonly BaseRealVector3<T> RIGHT = new(T.One, T.Zero, T.Zero);
    public static readonly BaseRealVector3<T> UP = new(T.Zero, T.One, T.Zero);
    public static readonly BaseRealVector3<T> DOWN = new(T.Zero, -T.One, T.Zero);
    public static readonly BaseRealVector3<T> FORWARD = new(T.Zero, T.Zero, T.One);
    public static readonly BaseRealVector3<T> BACK = new(T.Zero, T.Zero, -T.One);
    public static readonly BaseRealVector3<T> ONE = new(T.One, T.One, T.One);

    public static BaseRealVector3<T> Zero => ZERO;
    public static BaseRealVector3<T> Left => LEFT;
    public static BaseRealVector3<T> Right => RIGHT;
    public static BaseRealVector3<T> Up => UP;
    public static BaseRealVector3<T> Down => DOWN;
    public static BaseRealVector3<T> Forward => FORWARD;
    public static BaseRealVector3<T> Back => BACK;
    public static BaseRealVector3<T> One => ONE;

    public T[] Data => [X, Y, Z];
    public bool IsNaN => T.IsNaN(X) || T.IsNaN(Y) || T.IsNaN(Z);
    public bool IsInf => T.IsInfinity(X) || T.IsInfinity(Y) || T.IsInfinity(Z);
    public T Length => T.Sqrt(LengthSquared);
    public T LengthSquared => X * X + Y * Y + Z * Z;
    public BaseRealVector3<T> Abs => new(T.Abs(X), T.Abs(Y), T.Abs(Z));

    public BaseRealVector3<T> Normalized
    {
        get
        {
            var lenSquared = LengthSquared;
            if (!MathDefs.Equals(double.CreateChecked(lenSquared), 1.0) && lenSquared > T.Zero)
                return this * (T.One / T.Sqrt(lenSquared));
            return this;
        }
    }

    public BaseRealVector3<T> NormalizedOrDefault(BaseRealVector3<T> defaultValue = default, T? epsilon = null)
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
            2 => Z,
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
                case 2:
                    Z = value;
                    break;
                default:
                    throw new IndexOutOfRangeException();
            }
        }
    }

    public static BaseRealVector3<T> FromXZ(BaseRealVector2<T> vector, T y = default) => new(vector.X, y, vector.Y);

    public void Normalize()
    {
        var lenSquared = LengthSquared;
        if (!MathDefs.Equals(double.CreateChecked(lenSquared), 1.0) && lenSquared > T.Zero)
            this *= T.One / T.Sqrt(lenSquared);
    }

    public T DotProduct(BaseRealVector3<T> rhs) => X * rhs.X + Y * rhs.Y + Z * rhs.Z;

    public T AbsDotProduct(BaseRealVector3<T> rhs) => T.Abs(X * rhs.X) + T.Abs(Y * rhs.Y) + T.Abs(Z * rhs.Z);

    public T ProjectOntoAxis(BaseRealVector3<T> axis) => DotProduct(axis.Normalized);

    public BaseRealVector3<T> ProjectOntoPlane(BaseRealVector3<T> origin, BaseRealVector3<T> normal)
    {
        var delta = this - origin;
        return this - normal.Normalized * delta.ProjectOntoAxis(normal);
    }

    public T ProjectOntoLineScalar(BaseRealVector3<T> from, BaseRealVector3<T> to, bool clamped = false)
    {
        var direction = to - from;
        var lengthSquared = direction.LengthSquared;
        var factor = (this - from).DotProduct(direction) / lengthSquared;
        return clamped ? T.Clamp(factor, T.Zero, T.One) : factor;
    }

    public BaseRealVector3<T> ProjectOntoLine(BaseRealVector3<T> from, BaseRealVector3<T> to, bool clamped = false)
        => from.Lerp(to, ProjectOntoLineScalar(from, to, clamped));

    public T DistanceToPoint(BaseRealVector3<T> point) => (this - point).Length;

    public T DistanceToPlane(BaseRealVector3<T> origin, BaseRealVector3<T> normal) => (this - origin).ProjectOntoAxis(normal);

    public BaseRealVector3<T> Orthogonalize(BaseRealVector3<T> axis) => axis.CrossProduct(this).CrossProduct(axis).Normalized;

    public BaseRealVector3<T> CrossProduct(BaseRealVector3<T> rhs) => new(
        Y * rhs.Z - Z * rhs.Y,
        Z * rhs.X - X * rhs.Z,
        X * rhs.Y - Y * rhs.X);

    public BaseRealVector3<T> Lerp(BaseRealVector3<T> rhs, T t) => this * (T.One - t) + rhs * t;

    // C++: returns radians: return Urho3D::Acos(DotProduct(rhs) / (Length() * rhs.Length()));
    public T Angle(BaseRealVector3<T> rhs) => T.Acos(DotProduct(rhs) / (Length * rhs.Length)) * T.CreateChecked(MathDefs.RadToDeg);

    // C++: returns radians (signed angle in radians)
    public T SignedAngle(BaseRealVector3<T> rhs, BaseRealVector3<T> axis)
    {
        var angle = Angle(rhs);
        var sign = CrossProduct(rhs).DotProduct(axis) < T.Zero ? -T.One : T.One;
        return angle * sign;
    }

    public BaseRealVector2<T> ToVector2() => new(X, Y);

    public Vector2i ToIntVector2() => new(int.CreateChecked(X), int.CreateChecked(Y));

    public Vector3i ToIntVector3() => new(int.CreateChecked(X), int.CreateChecked(Y), int.CreateChecked(Z));

    public BaseRealVector2<T> ToXZ() => new(X, Z);

    public Vector4f ToVector4(float w = 0.0f) => new(float.CreateChecked(X), float.CreateChecked(Y), float.CreateChecked(Z), w);

    public BaseRealVector3<T> ReNormalized(T minLength, T maxLength, BaseRealVector3<T> defaultValue = default, T? epsilon = null)
    {
        var typedEpsilon = epsilon ?? T.CreateChecked(MathDefs.LargeEpsilon);
        var lenSquared = LengthSquared;
        if (lenSquared < typedEpsilon * typedEpsilon)
            return defaultValue;

        var length = T.Sqrt(lenSquared);
        var newLength = T.Clamp(length, minLength, maxLength);
        return this * (newLength / length);
    }

    public bool ApproximatelyEquivalent(BaseRealVector3<T> rhs, float epsilon = MathDefs.Epsilon)
    {
        var typedEpsilon = T.CreateChecked(epsilon);
        return T.Abs(X - rhs.X) <= typedEpsilon && T.Abs(Y - rhs.Y) <= typedEpsilon && T.Abs(Z - rhs.Z) <= typedEpsilon;
    }

    public bool Equals(BaseRealVector3<T> other) => this == other;

    public override bool Equals(object? obj) => obj is BaseRealVector3<T> other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(X, Y, Z);

    public override string ToString() => string.Format(CultureInfo.InvariantCulture, "{0} {1} {2}", X, Y, Z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(BaseRealVector3<T> lhs, BaseRealVector3<T> rhs) => lhs.X == rhs.X && lhs.Y == rhs.Y && lhs.Z == rhs.Z;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(BaseRealVector3<T> lhs, BaseRealVector3<T> rhs) => !(lhs == rhs);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BaseRealVector3<T> operator +(BaseRealVector3<T> lhs, BaseRealVector3<T> rhs) => new(lhs.X + rhs.X, lhs.Y + rhs.Y, lhs.Z + rhs.Z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BaseRealVector3<T> operator -(BaseRealVector3<T> value) => new(-value.X, -value.Y, -value.Z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BaseRealVector3<T> operator -(BaseRealVector3<T> lhs, BaseRealVector3<T> rhs) => new(lhs.X - rhs.X, lhs.Y - rhs.Y, lhs.Z - rhs.Z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BaseRealVector3<T> operator *(BaseRealVector3<T> lhs, T rhs) => new(lhs.X * rhs, lhs.Y * rhs, lhs.Z * rhs);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BaseRealVector3<T> operator *(T lhs, BaseRealVector3<T> rhs) => rhs * lhs;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BaseRealVector3<T> operator *(BaseRealVector3<T> lhs, BaseRealVector3<T> rhs) => new(lhs.X * rhs.X, lhs.Y * rhs.Y, lhs.Z * rhs.Z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BaseRealVector3<T> operator /(BaseRealVector3<T> lhs, T rhs) => new(lhs.X / rhs, lhs.Y / rhs, lhs.Z / rhs);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BaseRealVector3<T> operator /(BaseRealVector3<T> lhs, BaseRealVector3<T> rhs) => new(lhs.X / rhs.X, lhs.Y / rhs.Y, lhs.Z / rhs.Z);

    public static BaseRealVector3<T> Lerp(BaseRealVector3<T> lhs, BaseRealVector3<T> rhs, BaseRealVector3<T> t) => lhs + (rhs - lhs) * t;

    public static BaseRealVector3<T> Min(BaseRealVector3<T> lhs, BaseRealVector3<T> rhs) => new(T.Min(lhs.X, rhs.X), T.Min(lhs.Y, rhs.Y), T.Min(lhs.Z, rhs.Z));

    public static BaseRealVector3<T> Max(BaseRealVector3<T> lhs, BaseRealVector3<T> rhs) => new(T.Max(lhs.X, rhs.X), T.Max(lhs.Y, rhs.Y), T.Max(lhs.Z, rhs.Z));

    public static BaseRealVector3<T> Floor(BaseRealVector3<T> vec) => new(T.Floor(vec.X), T.Floor(vec.Y), T.Floor(vec.Z));

    public static BaseRealVector3<T> Round(BaseRealVector3<T> vec) => new(T.Round(vec.X), T.Round(vec.Y), T.Round(vec.Z));

    public static BaseRealVector3<T> Ceil(BaseRealVector3<T> vec) => new(T.Ceiling(vec.X), T.Ceiling(vec.Y), T.Ceiling(vec.Z));

    public static BaseRealVector3<T> Sqrt(BaseRealVector3<T> vec) => new(T.Sqrt(vec.X), T.Sqrt(vec.Y), T.Sqrt(vec.Z));

    public static T StableRandom(BaseRealVector3<T> seed) =>
        BaseRealVector2<T>.StableRandom(new BaseRealVector2<T>(
            BaseRealVector2<T>.StableRandom(new BaseRealVector2<T>(seed.X, seed.Y)), seed.Z));

    public static Vector3i FloorToInt(BaseRealVector3<T> vec) => new(int.CreateChecked(T.Floor(vec.X)), int.CreateChecked(T.Floor(vec.Y)), int.CreateChecked(T.Floor(vec.Z)));

    public static Vector3i RoundToInt(BaseRealVector3<T> vec) => new(int.CreateChecked(T.Round(vec.X)), int.CreateChecked(T.Round(vec.Y)), int.CreateChecked(T.Round(vec.Z)));

    public static Vector3i CeilToInt(BaseRealVector3<T> vec) => new(int.CreateChecked(T.Ceiling(vec.X)), int.CreateChecked(T.Ceiling(vec.Y)), int.CreateChecked(T.Ceiling(vec.Z)));
}
