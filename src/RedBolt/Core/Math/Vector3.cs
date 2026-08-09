// Copyright (c) 2008-2022 the Urho3D project.
// Copyright (c) 2022-2026 the rbfx project.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Math;

using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

/// <summary>Three-dimensional vector with real-number values.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct BaseRealVector3<T> : IEquatable<BaseRealVector3<T>>
    where T : unmanaged, IFloatingPointIeee754<T>
{
    /// <summary>Construct a zero vector.</summary>
    public BaseRealVector3()
    {
        X = T.Zero;
        Y = T.Zero;
        Z = T.Zero;
    }

    /// <summary>Construct from coordinates.</summary>
    public BaseRealVector3(T x, T y, T z = default)
    {
        X = x;
        Y = y;
        Z = z;
    }

    /// <summary>Construct from a scalar array.</summary>
    public BaseRealVector3(ReadOnlySpan<T> data)
    {
        X = data[0];
        Y = data[1];
        Z = data[2];
    }

    /// <summary>Construct from 2D vector in X0Z plane.</summary>
    public static BaseRealVector3<T> FromXZ(in BaseRealVector2<T> vector, T y = default) =>
        new(vector.X, y, vector.Y);

    /// <summary>Convert to tuple.</summary>
    public readonly (T X, T Y, T Z) Tie() => (X, Y, Z);

    /// <summary>Test for equality with another vector without epsilon.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(BaseRealVector3<T> lhs, BaseRealVector3<T> rhs) =>
        lhs.X == rhs.X && lhs.Y == rhs.Y && lhs.Z == rhs.Z;

    /// <summary>Test for inequality with another vector without epsilon.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(BaseRealVector3<T> lhs, BaseRealVector3<T> rhs) => !(lhs == rhs);

    /// <summary>Add a vector.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BaseRealVector3<T> operator +(BaseRealVector3<T> lhs, BaseRealVector3<T> rhs) =>
        new(lhs.X + rhs.X, lhs.Y + rhs.Y, lhs.Z + rhs.Z);

    /// <summary>Return negation.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BaseRealVector3<T> operator -(BaseRealVector3<T> value) => new(-value.X, -value.Y, -value.Z);

    /// <summary>Subtract a vector.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BaseRealVector3<T> operator -(BaseRealVector3<T> lhs, BaseRealVector3<T> rhs) =>
        new(lhs.X - rhs.X, lhs.Y - rhs.Y, lhs.Z - rhs.Z);

    /// <summary>Multiply with a scalar.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BaseRealVector3<T> operator *(BaseRealVector3<T> lhs, T rhs) =>
        new(lhs.X * rhs, lhs.Y * rhs, lhs.Z * rhs);

    /// <summary>Multiply with a vector.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BaseRealVector3<T> operator *(BaseRealVector3<T> lhs, BaseRealVector3<T> rhs) =>
        new(lhs.X * rhs.X, lhs.Y * rhs.Y, lhs.Z * rhs.Z);

    /// <summary>Divide by a scalar.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BaseRealVector3<T> operator /(BaseRealVector3<T> lhs, T rhs) =>
        new(lhs.X / rhs, lhs.Y / rhs, lhs.Z / rhs);

    /// <summary>Divide by a vector.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BaseRealVector3<T> operator /(BaseRealVector3<T> lhs, BaseRealVector3<T> rhs) =>
        new(lhs.X / rhs.X, lhs.Y / rhs.Y, lhs.Z / rhs.Z);

    /// <summary>Normalize to unit length.</summary>
    public void Normalize()
    {
        var lenSquared = LengthSquared;
        if (!MathDefs.Equals(double.CreateChecked(lenSquared), 1.0) && lenSquared > T.Zero)
        {
            var invLen = T.One / T.Sqrt(lenSquared);
            X *= invLen;
            Y *= invLen;
            Z *= invLen;
        }
    }

    /// <summary>Return length.</summary>
    public readonly T Length => T.Sqrt(X * X + Y * Y + Z * Z);

    /// <summary>Return squared length.</summary>
    public readonly T LengthSquared => X * X + Y * Y + Z * Z;

    /// <summary>Calculate dot product.</summary>
    public readonly T DotProduct(in BaseRealVector3<T> rhs) => X * rhs.X + Y * rhs.Y + Z * rhs.Z;

    /// <summary>Calculate absolute dot product.</summary>
    public readonly T AbsDotProduct(in BaseRealVector3<T> rhs) =>
        T.Abs(X * rhs.X) + T.Abs(Y * rhs.Y) + T.Abs(Z * rhs.Z);

    /// <summary>Project direction vector onto axis.</summary>
    public readonly T ProjectOntoAxis(in BaseRealVector3<T> axis) => DotProduct(axis.Normalized);

    /// <summary>Project position vector onto plane with given origin and normal.</summary>
    public readonly BaseRealVector3<T> ProjectOntoPlane(in BaseRealVector3<T> origin, in BaseRealVector3<T> normal)
    {
        var delta = this - origin;
        return this - normal.Normalized * delta.ProjectOntoAxis(normal);
    }

    /// <summary>Project position vector onto line segment. Returns interpolation factor between line points.</summary>
    public readonly T ProjectOntoLineScalar(in BaseRealVector3<T> from, in BaseRealVector3<T> to,
        bool clamped = false)
    {
        var direction = to - from;
        var lengthSquared = direction.LengthSquared;
        var factor = (this - from).DotProduct(direction) / lengthSquared;
        return clamped ? T.Clamp(factor, T.Zero, T.One) : factor;
    }

    /// <summary>Project position vector onto line segment. Returns new position.</summary>
    public readonly BaseRealVector3<T> ProjectOntoLine(in BaseRealVector3<T> from, in BaseRealVector3<T> to,
        bool clamped = false) => from.Lerp(to, ProjectOntoLineScalar(from, to, clamped));

    /// <summary>Calculate distance to another position vector.</summary>
    public readonly T DistanceToPoint(in BaseRealVector3<T> point) => (this - point).Length;

    /// <summary>Calculate distance to the plane with given origin and normal.</summary>
    public readonly T DistanceToPlane(in BaseRealVector3<T> origin, in BaseRealVector3<T> normal) =>
        (this - origin).ProjectOntoAxis(normal);

    /// <summary>Make vector orthogonal to the axis.</summary>
    public readonly BaseRealVector3<T> Orthogonalize(in BaseRealVector3<T> axis) =>
        axis.CrossProduct(this).CrossProduct(axis).Normalized;

    /// <summary>Calculate cross product.</summary>
    public readonly BaseRealVector3<T> CrossProduct(in BaseRealVector3<T> rhs) => new(
        Y * rhs.Z - Z * rhs.Y,
        Z * rhs.X - X * rhs.Z,
        X * rhs.Y - Y * rhs.X);

    /// <summary>Return absolute vector.</summary>
    public readonly BaseRealVector3<T> Abs => new(T.Abs(X), T.Abs(Y), T.Abs(Z));

    /// <summary>Linear interpolation with another vector.</summary>
    public readonly BaseRealVector3<T> Lerp(in BaseRealVector3<T> rhs, T t) => this * (T.One - t) + rhs * t;

    /// <summary>Test for equality with another vector with epsilon.</summary>
    public readonly bool ApproximatelyEquivalent(in BaseRealVector3<T> rhs, float epsilon = MathDefs.Epsilon)
    {
        var typedEpsilon = T.CreateChecked(epsilon);
        return T.Abs(X - rhs.X) <= typedEpsilon && T.Abs(Y - rhs.Y) <= typedEpsilon &&
            T.Abs(Z - rhs.Z) <= typedEpsilon;
    }

    /// <summary>Returns the angle between this vector and another vector in degrees, from 0 to 180.</summary>
    public readonly T Angle(in BaseRealVector3<T> rhs) =>
        T.Acos(DotProduct(rhs) / (Length * rhs.Length)) * T.CreateChecked(MathDefs.RadToDeg);

    /// <summary>Returns the signed angle between this vector and another vector in degrees, from -180 to 180.</summary>
    /// <remarks>Axis is used to determine the sign of the angle.
    /// If axis is orthogonal to both vectors, it is guaranteed that quaternion rotation with this axis and angle
    /// will result in transition from first vector to the second one.</remarks>
    public readonly T SignedAngle(in BaseRealVector3<T> rhs, in BaseRealVector3<T> axis)
    {
        var angle = Angle(rhs);
        var sign = CrossProduct(rhs).DotProduct(axis) < T.Zero ? -T.One : T.One;
        return angle * sign;
    }

    /// <summary>Return whether any component is NaN.</summary>
    public readonly bool IsNaN => T.IsNaN(X) || T.IsNaN(Y) || T.IsNaN(Z);

    /// <summary>Return whether any component is Inf.</summary>
    public readonly bool IsInf => T.IsInfinity(X) || T.IsInfinity(Y) || T.IsInfinity(Z);

    /// <summary>Return normalized to unit length.</summary>
    public readonly BaseRealVector3<T> Normalized
    {
        get
        {
            var lenSquared = LengthSquared;
            if (!MathDefs.Equals(double.CreateChecked(lenSquared), 1.0) && lenSquared > T.Zero)
            {
                var invLen = T.One / T.Sqrt(lenSquared);
                return this * invLen;
            }
            return this;
        }
    }

    /// <summary>Return normalized to unit length or zero if length is too small.</summary>
    public readonly BaseRealVector3<T> NormalizedOrDefault(BaseRealVector3<T> defaultValue = default,
        T? epsilon = null)
    {
        var eps = epsilon ?? T.CreateChecked(MathDefs.LargeEpsilon);
        var lenSquared = LengthSquared;
        if (lenSquared < eps * eps)
            return defaultValue;
        return this / T.Sqrt(lenSquared);
    }

    /// <summary>Return normalized vector with length in given range.</summary>
    public readonly BaseRealVector3<T> ReNormalized(T minLength, T maxLength,
        BaseRealVector3<T> defaultValue = default, T? epsilon = null)
    {
        var eps = epsilon ?? T.CreateChecked(MathDefs.LargeEpsilon);
        var lenSquared = LengthSquared;
        if (lenSquared < eps * eps)
            return defaultValue;
        var length = T.Sqrt(lenSquared);
        var newLength = T.Clamp(length, minLength, maxLength);
        return this * (newLength / length);
    }

    /// <summary>Return data.</summary>
    public ReadOnlySpan<T> Data() => MemoryMarshal.CreateReadOnlySpan(ref X, 3);

    /// <summary>Return mutable data.</summary>
    public Span<T> MutableData() => MemoryMarshal.CreateSpan(ref X, 3);

    /// <summary>Return as string.</summary>
    public readonly override string ToString() =>
        string.Format(CultureInfo.InvariantCulture, "{0:g} {1:g} {2:g}", X, Y, Z);

    /// <summary>Return hash value for HashSet &amp; HashMap.</summary>
    public readonly uint ToHash()
    {
        uint hash = 0;
        uint componentHash;
        if (Unsafe.SizeOf<T>() == sizeof(float))
            componentHash = Unsafe.BitCast<T, uint>(X);
        else
        {
            var value = Unsafe.BitCast<T, ulong>(X);
            var lowValue = unchecked((uint)value);
            var highValue = unchecked((uint)(value >> 32));
            componentHash = lowValue;
            if (highValue != 0)
                componentHash ^= unchecked(highValue + 0x9e3779b9u + (componentHash << 6) + (componentHash >> 2));
        }
        hash ^= unchecked(componentHash + 0x9e3779b9u + (hash << 6) + (hash >> 2));

        if (Unsafe.SizeOf<T>() == sizeof(float))
            componentHash = Unsafe.BitCast<T, uint>(Y);
        else
        {
            var value = Unsafe.BitCast<T, ulong>(Y);
            var lowValue = unchecked((uint)value);
            var highValue = unchecked((uint)(value >> 32));
            componentHash = lowValue;
            if (highValue != 0)
                componentHash ^= unchecked(highValue + 0x9e3779b9u + (componentHash << 6) + (componentHash >> 2));
        }
        hash ^= unchecked(componentHash + 0x9e3779b9u + (hash << 6) + (hash >> 2));

        if (Unsafe.SizeOf<T>() == sizeof(float))
            componentHash = Unsafe.BitCast<T, uint>(Z);
        else
        {
            var value = Unsafe.BitCast<T, ulong>(Z);
            var lowValue = unchecked((uint)value);
            var highValue = unchecked((uint)(value >> 32));
            componentHash = lowValue;
            if (highValue != 0)
                componentHash ^= unchecked(highValue + 0x9e3779b9u + (componentHash << 6) + (componentHash >> 2));
        }
        hash ^= unchecked(componentHash + 0x9e3779b9u + (hash << 6) + (hash >> 2));
        return hash;
    }

    // C# has no equivalent of the C++ associated ScalarType template parameter.
    // template <class OtherVectorType> constexpr OtherVectorType Cast(typename OtherVectorType::ScalarType w = {}) const
    // template <class OtherVectorType> constexpr OtherVectorType CastXZ(typename OtherVectorType::ScalarType y = {}, typename OtherVectorType::ScalarType w = {}) const

    /// <summary>Return IntVector2 vector.</summary>
    public readonly Vector2i ToIntVector2() => new(int.CreateTruncating(X), int.CreateTruncating(Y));

    /// <summary>Return Vector2 vector.</summary>
    public readonly Vector2f ToVector2() => new(float.CreateChecked(X), float.CreateChecked(Y));

    /// <summary>Return IntVector3 vector.</summary>
    public readonly Vector3i ToIntVector3() =>
        new(int.CreateTruncating(X), int.CreateTruncating(Y), int.CreateTruncating(Z));

    /// <summary>Return Vector4 vector.</summary>
    public readonly Vector4f ToVector4(float w = 0.0f) =>
        new(float.CreateChecked(X), float.CreateChecked(Y), float.CreateChecked(Z), w);

    /// <summary>Return Vector2 from X and Z components.</summary>
    public readonly BaseRealVector2<T> ToXZ() => new(X, Z);

    public readonly bool Equals(BaseRealVector3<T> other) => this == other;

    public readonly override bool Equals(object? obj) => obj is BaseRealVector3<T> other && Equals(other);

    public readonly override int GetHashCode() => unchecked((int)ToHash());

    /// <summary>X coordinate.</summary>
    public T X;

    /// <summary>Y coordinate.</summary>
    public T Y;

    /// <summary>Z coordinate.</summary>
    public T Z;

    /// <summary>Zero vector.</summary>
    public static readonly BaseRealVector3<T> Zero = new(T.Zero, T.Zero, T.Zero);
    /// <summary>(-1,0,0) vector.</summary>
    public static readonly BaseRealVector3<T> Left = new(-T.One, T.Zero, T.Zero);
    /// <summary>(1,0,0) vector.</summary>
    public static readonly BaseRealVector3<T> Right = new(T.One, T.Zero, T.Zero);
    /// <summary>(0,1,0) vector.</summary>
    public static readonly BaseRealVector3<T> Up = new(T.Zero, T.One, T.Zero);
    /// <summary>(0,-1,0) vector.</summary>
    public static readonly BaseRealVector3<T> Down = new(T.Zero, -T.One, T.Zero);
    /// <summary>(0,0,1) vector.</summary>
    public static readonly BaseRealVector3<T> Forward = new(T.Zero, T.Zero, T.One);
    /// <summary>(0,0,-1) vector.</summary>
    public static readonly BaseRealVector3<T> Back = new(T.Zero, T.Zero, -T.One);
    /// <summary>(1,1,1) vector.</summary>
    public static readonly BaseRealVector3<T> One = new(T.One, T.One, T.One);

    /// <summary>Multiply BaseVector3 with a scalar.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BaseRealVector3<T> operator *(T lhs, BaseRealVector3<T> rhs) => rhs * lhs;

    /// <summary>Per-component linear interpolation between two 3-vectors.</summary>
    public static BaseRealVector3<T> Lerp(in BaseRealVector3<T> lhs, in BaseRealVector3<T> rhs,
        in BaseRealVector3<T> t) => lhs + (rhs - lhs) * t;

    /// <summary>Per-component min of two 3-vectors.</summary>
    public static BaseRealVector3<T> Min(in BaseRealVector3<T> lhs, in BaseRealVector3<T> rhs) =>
        new(T.Min(lhs.X, rhs.X), T.Min(lhs.Y, rhs.Y), T.Min(lhs.Z, rhs.Z));

    /// <summary>Per-component max of two 3-vectors.</summary>
    public static BaseRealVector3<T> Max(in BaseRealVector3<T> lhs, in BaseRealVector3<T> rhs) =>
        new(T.Max(lhs.X, rhs.X), T.Max(lhs.Y, rhs.Y), T.Max(lhs.Z, rhs.Z));

    /// <summary>Per-component floor of 3-vector.</summary>
    public static BaseRealVector3<T> Floor(in BaseRealVector3<T> vec) =>
        new(T.Floor(vec.X), T.Floor(vec.Y), T.Floor(vec.Z));

    /// <summary>Per-component round of 3-vector.</summary>
    public static BaseRealVector3<T> Round(in BaseRealVector3<T> vec) =>
        new(T.Round(vec.X), T.Round(vec.Y), T.Round(vec.Z));

    /// <summary>Per-component ceil of 3-vector.</summary>
    public static BaseRealVector3<T> Ceil(in BaseRealVector3<T> vec) =>
        new(T.Ceiling(vec.X), T.Ceiling(vec.Y), T.Ceiling(vec.Z));

    /// <summary>Per-component absolute value of 3-vector.</summary>
    public static BaseRealVector3<T> VectorAbs(in BaseRealVector3<T> vec) =>
        new(T.Abs(vec.X), T.Abs(vec.Y), T.Abs(vec.Z));

    /// <summary>Per-component square root of 3-vector.</summary>
    public static BaseRealVector3<T> Sqrt(in BaseRealVector3<T> vec) =>
        new(T.Sqrt(vec.X), T.Sqrt(vec.Y), T.Sqrt(vec.Z));

    /// <summary>Per-component floor of 3-vector. Returns BaseIntegerVector3.</summary>
    public static Vector3i FloorToInt(in BaseRealVector3<T> vec) => new(
        int.CreateTruncating(T.Floor(vec.X)),
        int.CreateTruncating(T.Floor(vec.Y)),
        int.CreateTruncating(T.Floor(vec.Z)));

    /// <summary>Per-component round of 3-vector. Returns BaseIntegerVector3.</summary>
    public static Vector3i RoundToInt(in BaseRealVector3<T> vec) => new(
        int.CreateTruncating(T.Round(vec.X)),
        int.CreateTruncating(T.Round(vec.Y)),
        int.CreateTruncating(T.Round(vec.Z)));

    /// <summary>Per-component ceil of 3-vector. Returns BaseIntegerVector3.</summary>
    public static Vector3i CeilToInt(in BaseRealVector3<T> vec) => new(
        int.CreateTruncating(T.Ceiling(vec.X)),
        int.CreateTruncating(T.Ceiling(vec.Y)),
        int.CreateTruncating(T.Ceiling(vec.Z)));

    /// <summary>Return a random value from [0, 1) from 3-vector seed.</summary>
    public static T StableRandom(in BaseRealVector3<T> seed) =>
        BaseRealVector2<T>.StableRandom(new BaseRealVector2<T>(
            BaseRealVector2<T>.StableRandom(new BaseRealVector2<T>(seed.X, seed.Y)), seed.Z));
}
