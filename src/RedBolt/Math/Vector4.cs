using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace RedBolt;

[StructLayout(LayoutKind.Sequential)]
public struct Vector4f : IEquatable<Vector4f>, IApproximateEquatable<Vector4f>
{
    public float X;
    public float Y;
    public float Z;
    public float W;

    public Vector4f(float x = 0.0f, float y = 0.0f, float z = 0.0f, float w = 0.0f)
    {
        X = x;
        Y = y;
        Z = z;
        W = w;
    }

    public Vector4f(Vector2f lhs, Vector2f rhs)
    {
        X = lhs.X;
        Y = lhs.Y;
        Z = rhs.X;
        W = rhs.Y;
    }

    public Vector4f(Vector3f vector, float w)
    {
        X = vector.X;
        Y = vector.Y;
        Z = vector.Z;
        W = w;
    }

    public Vector4f(float[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.Length < 4)
            throw new ArgumentException("Expected at least four elements.", nameof(data));

        X = data[0];
        Y = data[1];
        Z = data[2];
        W = data[3];
    }

    public static readonly Vector4f ZERO = new(0.0f, 0.0f, 0.0f, 0.0f);
    public static readonly Vector4f ONE = new(1.0f, 1.0f, 1.0f, 1.0f);

    public static Vector4f Zero => ZERO;
    public static Vector4f One => ONE;

    // C++: not present in C++ Vector4
    public readonly Vector2f Xy => new(X, Y);
    // C++: not present in C++ Vector4
    public readonly Vector2f Zw => new(Z, W);
    public readonly float[] Data => [X, Y, Z, W];
    public readonly bool IsNaN => float.IsNaN(X) || float.IsNaN(Y) || float.IsNaN(Z) || float.IsNaN(W);
    public readonly bool IsInf => float.IsInfinity(X) || float.IsInfinity(Y) || float.IsInfinity(Z) || float.IsInfinity(W);
    public readonly Vector4f Abs => new(Math.Abs(X), Math.Abs(Y), Math.Abs(Z), Math.Abs(W));
    public readonly float Length => MathF.Sqrt(LengthSquared);
    public readonly float LengthSquared => X * X + Y * Y + Z * Z + W * W;

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

    public readonly float DotProduct(Vector4f rhs) => X * rhs.X + Y * rhs.Y + Z * rhs.Z + W * rhs.W;
    public readonly float AbsDotProduct(Vector4f rhs) => Math.Abs(X * rhs.X) + Math.Abs(Y * rhs.Y) + Math.Abs(Z * rhs.Z) + Math.Abs(W * rhs.W);
    public readonly float ProjectOntoAxis(Vector3f axis) => DotProduct(axis.Normalized.ToVector4());
    public readonly Vector4f Lerp(Vector4f rhs, float t) => this * (1.0f - t) + rhs * t;

    // /// Per-component ceil of 4-vector.
    // inline Vector4 VectorCeil(const Vector4& vec) { return Vector4(Ceil(vec.x_), Ceil(vec.y_), Ceil(vec.z_), Ceil(vec.w_)); }
    public readonly Vector2f ToVector2() => new(X, Y);
    public readonly IntVector2 ToIntVector2() => new((int)X, (int)Y);
    public readonly Vector3f ToVector3() => new(X, Y, Z);
    public readonly IntVector3 ToIntVector3() => new((int)X, (int)Y, (int)Z);

    public readonly bool ApproximatelyEquivalent(Vector4f rhs, float epsilon = MathDefs.Epsilon) =>
        MathDefs.ApproximatelyEquivalent(X, rhs.X, epsilon) &&
        MathDefs.ApproximatelyEquivalent(Y, rhs.Y, epsilon) &&
        MathDefs.ApproximatelyEquivalent(Z, rhs.Z, epsilon) &&
        MathDefs.ApproximatelyEquivalent(W, rhs.W, epsilon);

    public readonly bool Equals(Vector4f other) => this == other;
    public override bool Equals(object? obj) => obj is Vector4f other && Equals(other);
    public override readonly int GetHashCode() => HashCode.Combine(X, Y, Z, W);
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

    public override string ToString() => string.Format(CultureInfo.InvariantCulture, "{0} {1} {2} {3}", X, Y, Z, W);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(Vector4f lhs, Vector4f rhs) => lhs.X == rhs.X && lhs.Y == rhs.Y && lhs.Z == rhs.Z && lhs.W == rhs.W;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(Vector4f lhs, Vector4f rhs) => !(lhs == rhs);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4f operator +(Vector4f lhs, Vector4f rhs) => new(lhs.X + rhs.X, lhs.Y + rhs.Y, lhs.Z + rhs.Z, lhs.W + rhs.W);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4f operator -(Vector4f value) => new(-value.X, -value.Y, -value.Z, -value.W);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4f operator -(Vector4f lhs, Vector4f rhs) => new(lhs.X - rhs.X, lhs.Y - rhs.Y, lhs.Z - rhs.Z, lhs.W - rhs.W);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4f operator *(Vector4f lhs, float rhs) => new(lhs.X * rhs, lhs.Y * rhs, lhs.Z * rhs, lhs.W * rhs);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4f operator *(float lhs, Vector4f rhs) => rhs * lhs;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4f operator *(Vector4f lhs, Vector4f rhs) => new(lhs.X * rhs.X, lhs.Y * rhs.Y, lhs.Z * rhs.Z, lhs.W * rhs.W);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4f operator /(Vector4f lhs, float rhs) => new(lhs.X / rhs, lhs.Y / rhs, lhs.Z / rhs, lhs.W / rhs);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4f operator /(Vector4f lhs, Vector4f rhs) => new(lhs.X / rhs.X, lhs.Y / rhs.Y, lhs.Z / rhs.Z, lhs.W / rhs.W);

    // C++: not present in C++ Vector4 — C++ only has vector * scalar, not vector + scalar or vector - scalar
    public static Vector4f operator +(Vector4f lhs, float rhs) => new(lhs.X + rhs, lhs.Y + rhs, lhs.Z + rhs, lhs.W + rhs);
    // C++: not present in C++ Vector4
    public static Vector4f operator -(Vector4f lhs, float rhs) => new(lhs.X - rhs, lhs.Y - rhs, lhs.Z - rhs, lhs.W - rhs);
}
