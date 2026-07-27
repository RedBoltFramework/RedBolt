// Copyright (c) 2008-2022 the Urho3D project.
// Copyright (c) 2022-2026 the rbfx project.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

using System.Globalization;
using SystemMath = global::System.Math;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace RedBolt.Math;

/// <summary>Four-dimensional vector.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct Vector4f : ICloneable, IEquatable<Vector4f>
{
    /// <summary>Construct a zero vector.</summary>
    public Vector4f()
    {
        X = 0.0f;
        Y = 0.0f;
        Z = 0.0f;
        W = 0.0f;
    }

    /// <summary>Copy-construct from another vector.</summary>
    public Vector4f(Vector4f vector)
    {
        X = vector.X;
        Y = vector.Y;
        Z = vector.Z;
        W = vector.W;
    }

    public readonly Vector4f Clone() => new(this);

    readonly object ICloneable.Clone() => Clone();

    /// <summary>Construct from 3-dimensional vector and a scalar.</summary>
    public Vector4f(in Vector3f v1, float w)
    {
        X = v1.X;
        Y = v1.Y;
        Z = v1.Z;
        W = w;
    }

    /// <summary>Construct from two 2-dimensional vectors.</summary>
    public Vector4f(in Vector2f v1, in Vector2f v2)
    {
        X = v1.X;
        Y = v1.Y;
        Z = v2.X;
        W = v2.Y;
    }

    /// <summary>Construct from coordinates.</summary>
    public Vector4f(float x, float y, float z, float w)
    {
        X = x;
        Y = y;
        Z = z;
        W = w;
    }

    /// <summary>Construct from a float array.</summary>
    public Vector4f(ReadOnlySpan<float> data)
    {
        X = data[0];
        Y = data[1];
        Z = data[2];
        W = data[3];
    }

    /// <summary>Test for equality with another vector without epsilon.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(Vector4f lhs, Vector4f rhs) =>
        lhs.X == rhs.X && lhs.Y == rhs.Y && lhs.Z == rhs.Z && lhs.W == rhs.W;

    /// <summary>Test for inequality with another vector without epsilon.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(Vector4f lhs, Vector4f rhs) =>
        lhs.X != rhs.X || lhs.Y != rhs.Y || lhs.Z != rhs.Z || lhs.W != rhs.W;

    /// <summary>Add a vector.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4f operator +(Vector4f lhs, Vector4f rhs) =>
        new(lhs.X + rhs.X, lhs.Y + rhs.Y, lhs.Z + rhs.Z, lhs.W + rhs.W);

    /// <summary>Return negation.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4f operator -(Vector4f value) => new(-value.X, -value.Y, -value.Z, -value.W);

    /// <summary>Subtract a vector.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4f operator -(Vector4f lhs, Vector4f rhs) =>
        new(lhs.X - rhs.X, lhs.Y - rhs.Y, lhs.Z - rhs.Z, lhs.W - rhs.W);

    /// <summary>Multiply with a scalar.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4f operator *(Vector4f lhs, float rhs) =>
        new(lhs.X * rhs, lhs.Y * rhs, lhs.Z * rhs, lhs.W * rhs);

    /// <summary>Multiply with a vector.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4f operator *(Vector4f lhs, Vector4f rhs) =>
        new(lhs.X * rhs.X, lhs.Y * rhs.Y, lhs.Z * rhs.Z, lhs.W * rhs.W);

    /// <summary>Divide by a scalar.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4f operator /(Vector4f lhs, float rhs) =>
        new(lhs.X / rhs, lhs.Y / rhs, lhs.Z / rhs, lhs.W / rhs);

    /// <summary>Divide by a vector.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4f operator /(Vector4f lhs, Vector4f rhs) =>
        new(lhs.X / rhs.X, lhs.Y / rhs.Y, lhs.Z / rhs.Z, lhs.W / rhs.W);

    /// <summary>Return const value by index.</summary>
    /// <summary>Return mutable value by index.</summary>
    public float this[int index]
    {
        readonly get => index switch
        {
            0 => X,
            1 => Y,
            2 => Z,
            3 => W,
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
                case 3:
                    W = value;
                    break;
                default:
                    throw new IndexOutOfRangeException();
            }
        }
    }

    /// <summary>Return length.</summary>
    public readonly float Length => MathF.Sqrt(X * X + Y * Y + Z * Z + W * W);

    /// <summary>Return squared length.</summary>
    public readonly float LengthSquared => X * X + Y * Y + Z * Z + W * W;

    /// <summary>Calculate dot product.</summary>
    public readonly float DotProduct(in Vector4f rhs) => X * rhs.X + Y * rhs.Y + Z * rhs.Z + W * rhs.W;

    /// <summary>Calculate absolute dot product.</summary>
    public readonly float AbsDotProduct(in Vector4f rhs) =>
        SystemMath.Abs(X * rhs.X) + SystemMath.Abs(Y * rhs.Y) + SystemMath.Abs(Z * rhs.Z) + SystemMath.Abs(W * rhs.W);

    /// <summary>Project vector onto axis.</summary>
    public readonly float ProjectOntoAxis(in Vector3f axis) => DotProduct(axis.Normalized.ToVector4());

    /// <summary>Return absolute vector.</summary>
    public readonly Vector4f Abs => new(SystemMath.Abs(X), SystemMath.Abs(Y), SystemMath.Abs(Z), SystemMath.Abs(W));

    /// <summary>Linear interpolation with another vector.</summary>
    public readonly Vector4f Lerp(in Vector4f rhs, float t) => this * (1.0f - t) + rhs * t;

    /// <summary>Test for equality with another vector with epsilon.</summary>
    public readonly bool ApproximatelyEquivalent(in Vector4f rhs, float epsilon = MathDefs.Epsilon) =>
        MathDefs.Equals(X, rhs.X, epsilon) &&
        MathDefs.Equals(Y, rhs.Y, epsilon) &&
        MathDefs.Equals(Z, rhs.Z, epsilon) &&
        MathDefs.Equals(W, rhs.W, epsilon);

    /// <summary>Return whether any component is NaN.</summary>
    public readonly bool IsNaN => float.IsNaN(X) || float.IsNaN(Y) || float.IsNaN(Z) || float.IsNaN(W);

    /// <summary>Return whether any component is Inf.</summary>
    public readonly bool IsInf => float.IsInfinity(X) || float.IsInfinity(Y) || float.IsInfinity(Z) || float.IsInfinity(W);

    /// <summary>Return IntVector2 vector (z, w components are ignored).</summary>
    public readonly Vector2i ToIntVector2() => new(int.CreateTruncating(X), int.CreateTruncating(Y));

    /// <summary>Return Vector2 vector (z, w components are ignored).</summary>
    public readonly Vector2f ToVector2() => new(X, Y);

    /// <summary>Return IntVector3 vector (w component is ignored).</summary>
    public readonly Vector3i ToIntVector3() =>
        new(int.CreateTruncating(X), int.CreateTruncating(Y), int.CreateTruncating(Z));

    /// <summary>Return Vector3 vector (w component is ignored).</summary>
    public readonly Vector3f ToVector3() => new(X, Y, Z);

    /// <summary>Return float data.</summary>
    public ReadOnlySpan<float> Data() => MemoryMarshal.CreateReadOnlySpan(ref X, 4);

    /// <summary>Return as string.</summary>
    public readonly override string ToString() =>
        string.Format(CultureInfo.InvariantCulture, "{0:g} {1:g} {2:g} {3:g}", X, Y, Z, W);

    /// <summary>Return hash value for HashSet &amp; HashMap.</summary>
    public readonly uint ToHash()
    {
        unchecked
        {
            uint hash = 37;
            hash = 37 * hash + (uint)BitConverter.SingleToInt32Bits(X);
            hash = 37 * hash + (uint)BitConverter.SingleToInt32Bits(Y);
            hash = 37 * hash + (uint)BitConverter.SingleToInt32Bits(Z);
            hash = 37 * hash + (uint)BitConverter.SingleToInt32Bits(W);
            return hash;
        }
    }

    public readonly bool Equals(Vector4f other) => this == other;

    public readonly override bool Equals(object? obj) => obj is Vector4f other && Equals(other);

    public readonly override int GetHashCode() => unchecked((int)ToHash());

    /// <summary>X coordinate.</summary>
    public float X;

    /// <summary>Y coordinate.</summary>
    public float Y;

    /// <summary>Z coordinate.</summary>
    public float Z;

    /// <summary>W coordinate.</summary>
    public float W;

    /// <summary>Zero vector.</summary>
    public static readonly Vector4f Zero;

    /// <summary>(1,1,1,1) vector.</summary>
    public static readonly Vector4f One = new(1.0f, 1.0f, 1.0f, 1.0f);

    /// <summary>Multiply Vector4 with a scalar.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4f operator *(float lhs, Vector4f rhs) => rhs * lhs;
}
