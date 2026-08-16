// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Math;

using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

/// <summary>Two-dimensional vector with real-number values.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct BaseRealVector2<T> : IEquatable<BaseRealVector2<T>>
    where T : unmanaged, IFloatingPointIeee754<T>
{
    /// <summary>Construct a zero vector.</summary>
    public BaseRealVector2()
    {
        X = T.Zero;
        Y = T.Zero;
    }

    /// <summary>Construct from coordinates.</summary>
    public BaseRealVector2(T x, T y)
    {
        X = x;
        Y = y;
    }

    /// <summary>Construct from a scalar array.</summary>
    public BaseRealVector2(ReadOnlySpan<T> data)
    {
        X = data[0];
        Y = data[1];
    }

    /// <summary>Convert to tuple.</summary>
    public readonly (T X, T Y) Tie() => (X, Y);

    /// <summary>Test for equality with another vector without epsilon.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(BaseRealVector2<T> lhs, BaseRealVector2<T> rhs) => lhs.X == rhs.X && lhs.Y == rhs.Y;

    /// <summary>Test for inequality with another vector without epsilon.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(BaseRealVector2<T> lhs, BaseRealVector2<T> rhs) => !(lhs == rhs);

    /// <summary>Add a vector.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BaseRealVector2<T> operator +(BaseRealVector2<T> lhs, BaseRealVector2<T> rhs) => new(lhs.X + rhs.X, lhs.Y + rhs.Y);

    /// <summary>Return negation.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BaseRealVector2<T> operator -(BaseRealVector2<T> value) => new(-value.X, -value.Y);

    /// <summary>Subtract a vector.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BaseRealVector2<T> operator -(BaseRealVector2<T> lhs, BaseRealVector2<T> rhs) => new(lhs.X - rhs.X, lhs.Y - rhs.Y);

    /// <summary>Multiply with a scalar.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BaseRealVector2<T> operator *(BaseRealVector2<T> lhs, T rhs) => new(lhs.X * rhs, lhs.Y * rhs);

    /// <summary>Multiply with a vector.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BaseRealVector2<T> operator *(BaseRealVector2<T> lhs, BaseRealVector2<T> rhs) => new(lhs.X * rhs.X, lhs.Y * rhs.Y);

    /// <summary>Divide by a scalar.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BaseRealVector2<T> operator /(BaseRealVector2<T> lhs, T rhs) => new(lhs.X / rhs, lhs.Y / rhs);

    /// <summary>Divide by a vector.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BaseRealVector2<T> operator /(BaseRealVector2<T> lhs, BaseRealVector2<T> rhs) => new(lhs.X / rhs.X, lhs.Y / rhs.Y);

    /// <summary>Normalize to unit length.</summary>
    public void Normalize()
    {
        var lenSquared = LengthSquared;
        if (!MathDefs.Equals(double.CreateChecked(lenSquared), 1.0) && lenSquared > T.Zero)
        {
            var invLen = T.One / T.Sqrt(lenSquared);
            X *= invLen;
            Y *= invLen;
        }
    }

    /// <summary>Return length.</summary>
    public readonly T Length => T.Sqrt(X * X + Y * Y);

    /// <summary>Return squared length.</summary>
    public readonly T LengthSquared => X * X + Y * Y;

    /// <summary>Calculate dot product.</summary>
    public readonly T DotProduct(in BaseRealVector2<T> rhs) => X * rhs.X + Y * rhs.Y;

    /// <summary>Calculate absolute dot product.</summary>
    public readonly T AbsDotProduct(in BaseRealVector2<T> rhs) => T.Abs(X * rhs.X) + T.Abs(Y * rhs.Y);

    /// <summary>Calculate "cross product" aka z component of cross product of (x1, y1, 0) and (x2, y2, 0).</summary>
    public readonly T CrossProduct(in BaseRealVector2<T> rhs) => X * rhs.Y - Y * rhs.X;

    /// <summary>Project vector onto axis.</summary>
    public readonly T ProjectOntoAxis(in BaseRealVector2<T> axis) => DotProduct(axis.Normalized);

    /// <summary>Project position vector onto line segment. Returns interpolation factor between line points.</summary>
    public readonly T ProjectOntoLineScalar(in BaseRealVector2<T> from, in BaseRealVector2<T> to, bool clamped = false)
    {
        var direction = to - from;
        var lengthSquared = direction.LengthSquared;
        var factor = (this - from).DotProduct(direction) / lengthSquared;
        return clamped ? T.Clamp(factor, T.Zero, T.One) : factor;
    }

    /// <summary>Project position vector onto line segment. Returns new position.</summary>
    public readonly BaseRealVector2<T> ProjectOntoLine(in BaseRealVector2<T> from, in BaseRealVector2<T> to, bool clamped = false) =>
        from.Lerp(to, ProjectOntoLineScalar(from, to, clamped));

    /// <summary>Calculate distance to another position vector.</summary>
    public readonly T DistanceToPoint(in BaseRealVector2<T> point) => (this - point).Length;

    /// <summary>Return scalar cross product of 2D vectors.</summary>
    public readonly T ScalarCrossProduct(in BaseRealVector2<T> rhs) => Y * rhs.X - X * rhs.Y;

    /// <summary>Returns the angle between this vector and another vector in degrees.</summary>
    public readonly T Angle(in BaseRealVector2<T> rhs) =>
        T.Acos(DotProduct(rhs) / (Length * rhs.Length)) * T.CreateChecked(MathDefs.RadToDeg);

    /// <summary>Returns signed angle between this vector and another vector in degrees. Clockwise direction is positive.</summary>
    public readonly T SignedAngle(in BaseRealVector2<T> rhs) =>
        Angle(rhs) * (ScalarCrossProduct(rhs) >= T.Zero ? T.One : -T.One);

    /// <summary>Return absolute vector.</summary>
    public readonly BaseRealVector2<T> Abs => new(T.Abs(X), T.Abs(Y));

    /// <summary>Linear interpolation with another vector.</summary>
    public readonly BaseRealVector2<T> Lerp(in BaseRealVector2<T> rhs, T t) => this * (T.One - t) + rhs * t;

    /// <summary>Test for equality with another vector with epsilon.</summary>
    public readonly bool ApproximatelyEquivalent(in BaseRealVector2<T> rhs, float epsilon = MathDefs.Epsilon)
    {
        var typedEpsilon = T.CreateChecked(epsilon);
        return T.Abs(X - rhs.X) <= typedEpsilon && T.Abs(Y - rhs.Y) <= typedEpsilon;
    }

    /// <summary>Return whether any component is NaN.</summary>
    public readonly bool IsNaN => T.IsNaN(X) || T.IsNaN(Y);

    /// <summary>Return whether any component is Inf.</summary>
    public readonly bool IsInf => T.IsInfinity(X) || T.IsInfinity(Y);

    /// <summary>Return normalized to unit length.</summary>
    public readonly BaseRealVector2<T> Normalized
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
    public readonly BaseRealVector2<T> NormalizedOrDefault(BaseRealVector2<T> defaultValue = default, T? epsilon = null)
    {
        var eps = epsilon ?? T.CreateChecked(MathDefs.LargeEpsilon);
        var lenSquared = LengthSquared;
        if (lenSquared < eps * eps)
            return defaultValue;
        return this / T.Sqrt(lenSquared);
    }

    /// <summary>Return normalized vector with length in given range.</summary>
    public readonly BaseRealVector2<T> ReNormalized(T minLength, T maxLength,
        BaseRealVector2<T> defaultValue = default, T? epsilon = null)
    {
        var eps = epsilon ?? T.CreateChecked(MathDefs.LargeEpsilon);
        var lenSquared = LengthSquared;
        if (lenSquared < eps * eps)
            return defaultValue;
        var length = T.Sqrt(lenSquared);
        var newLength = T.Clamp(length, minLength, maxLength);
        return this * (newLength / length);
    }

    /// <summary>Return orthogonal vector (clockwise).</summary>
    public readonly BaseRealVector2<T> GetOrthogonalClockwise() => new(Y, -X);

    /// <summary>Return orthogonal vector (counter-clockwise).</summary>
    public readonly BaseRealVector2<T> GetOrthogonalCounterClockwise() => new(-Y, X);

    /// <summary>Return data.</summary>
    public ReadOnlySpan<T> Data() => MemoryMarshal.CreateReadOnlySpan(ref X, 2);

    /// <summary>Return mutable data.</summary>
    public Span<T> MutableData() => MemoryMarshal.CreateSpan(ref X, 2);

    /// <summary>Return as string.</summary>
    public readonly override string ToString() => string.Format(CultureInfo.InvariantCulture, "{0:g} {1:g}", X, Y);

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
        return hash;
    }

    // C# has no equivalent of the C++ associated ScalarType template parameter.
    // template <class OtherVectorType> constexpr OtherVectorType Cast(typename OtherVectorType::ScalarType z = {}, typename OtherVectorType::ScalarType w = {}) const
    // template <class OtherVectorType> constexpr OtherVectorType CastXZ(typename OtherVectorType::ScalarType z = {}, typename OtherVectorType::ScalarType w = {}) const

    /// <summary>Return IntVector2 vector.</summary>
    public readonly Vector2i ToIntVector2() => new(int.CreateTruncating(X), int.CreateTruncating(Y));

    /// <summary>Return IntVector3 vector.</summary>
    public readonly Vector3i ToIntVector3(int z = 0) => new(int.CreateTruncating(X), int.CreateTruncating(Y), z);

    /// <summary>Return Vector3 vector.</summary>
    public readonly Vector3f ToVector3(float z = 0.0f) => new(float.CreateChecked(X), float.CreateChecked(Y), z);

    /// <summary>Return Vector4 vector.</summary>
    public readonly Vector4f ToVector4(float z = 0.0f, float w = 0.0f) => new(float.CreateChecked(X), float.CreateChecked(Y), z, w);

    public readonly bool Equals(BaseRealVector2<T> other) => this == other;

    public readonly override bool Equals(object? obj) => obj is BaseRealVector2<T> other && Equals(other);

    public readonly override int GetHashCode() => unchecked((int)ToHash());

    /// <summary>X coordinate.</summary>
    public T X;

    /// <summary>Y coordinate.</summary>
    public T Y;

    /// <summary>Zero vector.</summary>
    public static readonly BaseRealVector2<T> Zero = new(T.Zero, T.Zero);
    /// <summary>(-1,0) vector.</summary>
    public static readonly BaseRealVector2<T> Left = new(-T.One, T.Zero);
    /// <summary>(1,0) vector.</summary>
    public static readonly BaseRealVector2<T> Right = new(T.One, T.Zero);
    /// <summary>(0,1) vector.</summary>
    public static readonly BaseRealVector2<T> Up = new(T.Zero, T.One);
    /// <summary>(0,-1) vector.</summary>
    public static readonly BaseRealVector2<T> Down = new(T.Zero, -T.One);
    /// <summary>(1,1) vector.</summary>
    public static readonly BaseRealVector2<T> One = new(T.One, T.One);

    /// <summary>Multiply BaseVector2 with a scalar.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BaseRealVector2<T> operator *(T lhs, BaseRealVector2<T> rhs) => rhs * lhs;

    /// <summary>Per-component linear interpolation between two 2-vectors.</summary>
    public static BaseRealVector2<T> Lerp(in BaseRealVector2<T> lhs, in BaseRealVector2<T> rhs,
        in BaseRealVector2<T> t) => lhs + (rhs - lhs) * t;

    /// <summary>Per-component min of two 2-vectors.</summary>
    public static BaseRealVector2<T> Min(in BaseRealVector2<T> lhs, in BaseRealVector2<T> rhs) =>
        new(T.Min(lhs.X, rhs.X), T.Min(lhs.Y, rhs.Y));

    /// <summary>Per-component max of two 2-vectors.</summary>
    public static BaseRealVector2<T> Max(in BaseRealVector2<T> lhs, in BaseRealVector2<T> rhs) =>
        new(T.Max(lhs.X, rhs.X), T.Max(lhs.Y, rhs.Y));

    /// <summary>Per-component floor of 2-vector.</summary>
    public static BaseRealVector2<T> Floor(in BaseRealVector2<T> vec) => new(T.Floor(vec.X), T.Floor(vec.Y));

    /// <summary>Per-component round of 2-vector.</summary>
    public static BaseRealVector2<T> Round(in BaseRealVector2<T> vec) => new(T.Round(vec.X), T.Round(vec.Y));

    /// <summary>Per-component ceil of 2-vector.</summary>
    public static BaseRealVector2<T> Ceil(in BaseRealVector2<T> vec) => new(T.Ceiling(vec.X), T.Ceiling(vec.Y));

    /// <summary>Per-component absolute value of 2-vector.</summary>
    public static BaseRealVector2<T> VectorAbs(in BaseRealVector2<T> vec) => new(T.Abs(vec.X), T.Abs(vec.Y));

    /// <summary>Per-component square root of 2-vector.</summary>
    public static BaseRealVector2<T> Sqrt(in BaseRealVector2<T> vec) => new(T.Sqrt(vec.X), T.Sqrt(vec.Y));

    /// <summary>Per-component floor of 2-vector. Returns BaseIntegerVector2.</summary>
    public static Vector2i FloorToInt(in BaseRealVector2<T> vec) =>
        new(int.CreateTruncating(T.Floor(vec.X)), int.CreateTruncating(T.Floor(vec.Y)));

    /// <summary>Per-component round of 2-vector. Returns BaseIntegerVector2.</summary>
    public static Vector2i RoundToInt(in BaseRealVector2<T> vec) =>
        new(int.CreateTruncating(T.Round(vec.X)), int.CreateTruncating(T.Round(vec.Y)));

    /// <summary>Per-component ceil of 2-vector. Returns BaseIntegerVector2.</summary>
    public static Vector2i CeilToInt(in BaseRealVector2<T> vec) =>
        new(int.CreateTruncating(T.Ceiling(vec.X)), int.CreateTruncating(T.Ceiling(vec.Y)));

    /// <summary>Return a random value from [0, 1) from 2-vector seed.</summary>
    /// <remarks>http://stackoverflow.com/questions/12964279/whats-the-origin-of-this-glsl-rand-one-liner</remarks>
    public static T StableRandom(in BaseRealVector2<T> seed)
    {
        var value = T.Sin(seed.DotProduct(new(T.CreateChecked(12.9898f), T.CreateChecked(78.233f)))
            * T.CreateChecked(MathDefs.RadToDeg)) * T.CreateChecked(43758.5453f);
        return value - T.Floor(value);
    }

    /// <summary>Return a random value from [0, 1) from scalar seed.</summary>
    public static T StableRandom(T seed) => StableRandom(new BaseRealVector2<T>(seed, seed));
}
