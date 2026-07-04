using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace RedBolt;

[StructLayout(LayoutKind.Sequential)]
public struct Matrix2 : IEquatable<Matrix2>, IApproximateEquatable<Matrix2>
{
    public float M00;
    public float M01;
    public float M10;
    public float M11;

    public Matrix2(float v00 = 1.0f, float v01 = 0.0f, float v10 = 0.0f, float v11 = 1.0f)
    {
        M00 = v00;
        M01 = v01;
        M10 = v10;
        M11 = v11;
    }

    public Matrix2(IReadOnlyList<float> data)
    {
        M00 = data[0];
        M01 = data[1];
        M10 = data[2];
        M11 = data[3];
    }

    public static readonly Matrix2 ZERO = new(0.0f, 0.0f, 0.0f, 0.0f);
    public static readonly Matrix2 IDENTITY = new(1.0f, 0.0f, 0.0f, 1.0f);

    public static Matrix2 Zero => ZERO;
    public static Matrix2 Identity => IDENTITY;

    public Vector2 Scale => new(
        MathF.Sqrt(M00 * M00 + M10 * M10),
        MathF.Sqrt(M01 * M01 + M11 * M11));

    public Matrix2 Transposed => new(M00, M10, M01, M11);

    public Matrix2 Inverse
    {
        get
        {
            var determinant = M00 * M11 - M01 * M10;
            var invDeterminant = 1.0f / determinant;
            return new Matrix2(M11, -M01, -M10, M00) * invDeterminant;
        }
    }

    public float[] Data => [M00, M01, M10, M11];

    public void SetScale(Vector2 scale)
    {
        M00 = scale.X;
        M11 = scale.Y;
    }

    public void SetScale(float scale)
    {
        M00 = scale;
        M11 = scale;
    }

    public Matrix2 Scaled(Vector2 scale) => new(
        M00 * scale.X,
        M01 * scale.Y,
        M10 * scale.X,
        M11 * scale.Y);

    public bool ApproximatelyEquivalent(Matrix2 rhs, float epsilon = MathDefs.Epsilon) =>
        MathDefs.ApproximatelyEquivalent(M00, rhs.M00, epsilon) &&
        MathDefs.ApproximatelyEquivalent(M01, rhs.M01, epsilon) &&
        MathDefs.ApproximatelyEquivalent(M10, rhs.M10, epsilon) &&
        MathDefs.ApproximatelyEquivalent(M11, rhs.M11, epsilon);

    public bool Equals(Matrix2 other) => this == other;
    public override bool Equals(object? obj) => obj is Matrix2 other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(M00, M01, M10, M11);
    public override string ToString() => string.Format(CultureInfo.InvariantCulture, "{0} {1} {2} {3}", M00, M01, M10, M11);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(Matrix2 lhs, Matrix2 rhs) => lhs.M00 == rhs.M00 && lhs.M01 == rhs.M01 && lhs.M10 == rhs.M10 && lhs.M11 == rhs.M11;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(Matrix2 lhs, Matrix2 rhs) => !(lhs == rhs);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2 operator *(Matrix2 lhs, Vector2 rhs) => new(
        lhs.M00 * rhs.X + lhs.M01 * rhs.Y,
        lhs.M10 * rhs.X + lhs.M11 * rhs.Y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Matrix2 operator +(Matrix2 lhs, Matrix2 rhs) => new(
        lhs.M00 + rhs.M00,
        lhs.M01 + rhs.M01,
        lhs.M10 + rhs.M10,
        lhs.M11 + rhs.M11);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Matrix2 operator -(Matrix2 lhs, Matrix2 rhs) => new(
        lhs.M00 - rhs.M00,
        lhs.M01 - rhs.M01,
        lhs.M10 - rhs.M10,
        lhs.M11 - rhs.M11);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Matrix2 operator *(Matrix2 lhs, float rhs) => new(
        lhs.M00 * rhs,
        lhs.M01 * rhs,
        lhs.M10 * rhs,
        lhs.M11 * rhs);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Matrix2 operator *(float lhs, Matrix2 rhs) => rhs * lhs;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Matrix2 operator *(Matrix2 lhs, Matrix2 rhs) => new(
        lhs.M00 * rhs.M00 + lhs.M01 * rhs.M10,
        lhs.M00 * rhs.M01 + lhs.M01 * rhs.M11,
        lhs.M10 * rhs.M00 + lhs.M11 * rhs.M10,
        lhs.M10 * rhs.M01 + lhs.M11 * rhs.M11);
}
