using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace RedBolt;

[StructLayout(LayoutKind.Sequential)]
public struct Vector4 : IEquatable<Vector4>, IApproximateEquatable<Vector4>
{
    public float X;
    public float Y;
    public float Z;
    public float W;

    public Vector4(float x = 0.0f, float y = 0.0f, float z = 0.0f, float w = 0.0f)
    {
        X = x;
        Y = y;
        Z = z;
        W = w;
    }

    public Vector4(Vector3 vector, float w)
    {
        X = vector.X;
        Y = vector.Y;
        Z = vector.Z;
        W = w;
    }

    public static readonly Vector4 ZERO = new(0.0f, 0.0f, 0.0f, 0.0f);
    public static readonly Vector4 ONE = new(1.0f, 1.0f, 1.0f, 1.0f);

    public static Vector4 Zero => ZERO;
    public static Vector4 One => ONE;

    public Vector2 Xy => new(X, Y);
    public Vector2 Zw => new(Z, W);
    public float[] Data => [X, Y, Z, W];
    public bool IsNaN => float.IsNaN(X) || float.IsNaN(Y) || float.IsNaN(Z) || float.IsNaN(W);
    public Vector4 Abs => new(Math.Abs(X), Math.Abs(Y), Math.Abs(Z), Math.Abs(W));

    public float DotProduct(Vector4 rhs) => X * rhs.X + Y * rhs.Y + Z * rhs.Z + W * rhs.W;
    public float AbsDotProduct(Vector4 rhs) => Math.Abs(X * rhs.X) + Math.Abs(Y * rhs.Y) + Math.Abs(Z * rhs.Z) + Math.Abs(W * rhs.W);
    public Vector4 Lerp(Vector4 rhs, float t) => this * (1.0f - t) + rhs * t;

    public bool ApproximatelyEquivalent(Vector4 rhs, float epsilon = MathDefs.Epsilon) =>
        MathDefs.ApproximatelyEquivalent(X, rhs.X, epsilon) &&
        MathDefs.ApproximatelyEquivalent(Y, rhs.Y, epsilon) &&
        MathDefs.ApproximatelyEquivalent(Z, rhs.Z, epsilon) &&
        MathDefs.ApproximatelyEquivalent(W, rhs.W, epsilon);

    public bool Equals(Vector4 other) => this == other;
    public override bool Equals(object? obj) => obj is Vector4 other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(X, Y, Z, W);
    public override string ToString() => string.Format(CultureInfo.InvariantCulture, "{0} {1} {2} {3}", X, Y, Z, W);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(Vector4 lhs, Vector4 rhs) => lhs.X == rhs.X && lhs.Y == rhs.Y && lhs.Z == rhs.Z && lhs.W == rhs.W;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(Vector4 lhs, Vector4 rhs) => !(lhs == rhs);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4 operator +(Vector4 lhs, Vector4 rhs) => new(lhs.X + rhs.X, lhs.Y + rhs.Y, lhs.Z + rhs.Z, lhs.W + rhs.W);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4 operator -(Vector4 value) => new(-value.X, -value.Y, -value.Z, -value.W);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4 operator -(Vector4 lhs, Vector4 rhs) => new(lhs.X - rhs.X, lhs.Y - rhs.Y, lhs.Z - rhs.Z, lhs.W - rhs.W);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4 operator *(Vector4 lhs, float rhs) => new(lhs.X * rhs, lhs.Y * rhs, lhs.Z * rhs, lhs.W * rhs);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4 operator *(float lhs, Vector4 rhs) => rhs * lhs;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4 operator *(Vector4 lhs, Vector4 rhs) => new(lhs.X * rhs.X, lhs.Y * rhs.Y, lhs.Z * rhs.Z, lhs.W * rhs.W);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4 operator /(Vector4 lhs, float rhs) => new(lhs.X / rhs, lhs.Y / rhs, lhs.Z / rhs, lhs.W / rhs);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4 operator /(Vector4 lhs, Vector4 rhs) => new(lhs.X / rhs.X, lhs.Y / rhs.Y, lhs.Z / rhs.Z, lhs.W / rhs.W);
}
