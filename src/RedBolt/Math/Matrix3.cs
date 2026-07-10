using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace RedBolt;

[StructLayout(LayoutKind.Sequential)]
public struct Matrix3 : IEquatable<Matrix3>, IApproximateEquatable<Matrix3>
{
    public float M00;
    public float M01;
    public float M02;
    public float M10;
    public float M11;
    public float M12;
    public float M20;
    public float M21;
    public float M22;

    public Matrix3(
        float v00 = 1.0f, float v01 = 0.0f, float v02 = 0.0f,
        float v10 = 0.0f, float v11 = 1.0f, float v12 = 0.0f,
        float v20 = 0.0f, float v21 = 0.0f, float v22 = 1.0f)
    {
        M00 = v00;
        M01 = v01;
        M02 = v02;
        M10 = v10;
        M11 = v11;
        M12 = v12;
        M20 = v20;
        M21 = v21;
        M22 = v22;
    }

    public Matrix3(float angle, Vector3f axis)
        : this(Quaternion.FromAngleAxis(angle, axis).RotationMatrix)
    {
    }

    public Matrix3(Matrix3 other)
        : this(other.M00, other.M01, other.M02, other.M10, other.M11, other.M12, other.M20, other.M21, other.M22)
    {
    }

    public static readonly Matrix3 ZERO = new(
        0.0f, 0.0f, 0.0f,
        0.0f, 0.0f, 0.0f,
        0.0f, 0.0f, 0.0f);
    public static readonly Matrix3 IDENTITY = new();

    public static Matrix3 Zero => ZERO;
    public static Matrix3 Identity => IDENTITY;
    public Vector3f Scale => new(
        MathF.Sqrt(M00 * M00 + M10 * M10 + M20 * M20),
        MathF.Sqrt(M01 * M01 + M11 * M11 + M21 * M21),
        MathF.Sqrt(M02 * M02 + M12 * M12 + M22 * M22));
    public Matrix3 Transposed => new(M00, M10, M20, M01, M11, M21, M02, M12, M22);

    public void SetScale(Vector3f scale)
    {
        M00 = scale.X;
        M11 = scale.Y;
        M22 = scale.Z;
    }

    public void SetScale(float scale)
    {
        M00 = scale;
        M11 = scale;
        M22 = scale;
    }

    public Vector3f SignedScale(Matrix3 rotation) => new(
        rotation.M00 * M00 + rotation.M10 * M10 + rotation.M20 * M20,
        rotation.M01 * M01 + rotation.M11 * M11 + rotation.M21 * M21,
        rotation.M02 * M02 + rotation.M12 * M12 + rotation.M22 * M22);

    public Matrix3 Scaled(Vector3f scale) => new(
        M00 * scale.X, M01 * scale.Y, M02 * scale.Z,
        M10 * scale.X, M11 * scale.Y, M12 * scale.Z,
        M20 * scale.X, M21 * scale.Y, M22 * scale.Z);

    public bool ApproximatelyEquivalent(Matrix3 rhs, float epsilon = MathDefs.Epsilon) =>
        MathDefs.ApproximatelyEquivalent(M00, rhs.M00, epsilon) &&
        MathDefs.ApproximatelyEquivalent(M01, rhs.M01, epsilon) &&
        MathDefs.ApproximatelyEquivalent(M02, rhs.M02, epsilon) &&
        MathDefs.ApproximatelyEquivalent(M10, rhs.M10, epsilon) &&
        MathDefs.ApproximatelyEquivalent(M11, rhs.M11, epsilon) &&
        MathDefs.ApproximatelyEquivalent(M12, rhs.M12, epsilon) &&
        MathDefs.ApproximatelyEquivalent(M20, rhs.M20, epsilon) &&
        MathDefs.ApproximatelyEquivalent(M21, rhs.M21, epsilon) &&
        MathDefs.ApproximatelyEquivalent(M22, rhs.M22, epsilon);

    public bool Equals(Matrix3 other) => this == other;
    public override bool Equals(object? obj) => obj is Matrix3 other && Equals(other);
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(M00);
        hash.Add(M01);
        hash.Add(M02);
        hash.Add(M10);
        hash.Add(M11);
        hash.Add(M12);
        hash.Add(M20);
        hash.Add(M21);
        hash.Add(M22);
        return hash.ToHashCode();
    }
    public override string ToString() => string.Format(CultureInfo.InvariantCulture, "{0} {1} {2} {3} {4} {5} {6} {7} {8}", M00, M01, M02, M10, M11, M12, M20, M21, M22);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(Matrix3 lhs, Matrix3 rhs) =>
        lhs.M00 == rhs.M00 && lhs.M01 == rhs.M01 && lhs.M02 == rhs.M02 &&
        lhs.M10 == rhs.M10 && lhs.M11 == rhs.M11 && lhs.M12 == rhs.M12 &&
        lhs.M20 == rhs.M20 && lhs.M21 == rhs.M21 && lhs.M22 == rhs.M22;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(Matrix3 lhs, Matrix3 rhs) => !(lhs == rhs);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3f operator *(Matrix3 lhs, Vector3f rhs) => new(
        lhs.M00 * rhs.X + lhs.M01 * rhs.Y + lhs.M02 * rhs.Z,
        lhs.M10 * rhs.X + lhs.M11 * rhs.Y + lhs.M12 * rhs.Z,
        lhs.M20 * rhs.X + lhs.M21 * rhs.Y + lhs.M22 * rhs.Z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Matrix3 operator +(Matrix3 lhs, Matrix3 rhs) => new(
        lhs.M00 + rhs.M00, lhs.M01 + rhs.M01, lhs.M02 + rhs.M02,
        lhs.M10 + rhs.M10, lhs.M11 + rhs.M11, lhs.M12 + rhs.M12,
        lhs.M20 + rhs.M20, lhs.M21 + rhs.M21, lhs.M22 + rhs.M22);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Matrix3 operator -(Matrix3 lhs, Matrix3 rhs) => new(
        lhs.M00 - rhs.M00, lhs.M01 - rhs.M01, lhs.M02 - rhs.M02,
        lhs.M10 - rhs.M10, lhs.M11 - rhs.M11, lhs.M12 - rhs.M12,
        lhs.M20 - rhs.M20, lhs.M21 - rhs.M21, lhs.M22 - rhs.M22);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Matrix3 operator *(Matrix3 lhs, float rhs) => new(
        lhs.M00 * rhs, lhs.M01 * rhs, lhs.M02 * rhs,
        lhs.M10 * rhs, lhs.M11 * rhs, lhs.M12 * rhs,
        lhs.M20 * rhs, lhs.M21 * rhs, lhs.M22 * rhs);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Matrix3 operator *(float lhs, Matrix3 rhs) => rhs * lhs;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Matrix3 operator *(Matrix3 lhs, Matrix3 rhs) => new(
        lhs.M00 * rhs.M00 + lhs.M01 * rhs.M10 + lhs.M02 * rhs.M20,
        lhs.M00 * rhs.M01 + lhs.M01 * rhs.M11 + lhs.M02 * rhs.M21,
        lhs.M00 * rhs.M02 + lhs.M01 * rhs.M12 + lhs.M02 * rhs.M22,
        lhs.M10 * rhs.M00 + lhs.M11 * rhs.M10 + lhs.M12 * rhs.M20,
        lhs.M10 * rhs.M01 + lhs.M11 * rhs.M11 + lhs.M12 * rhs.M21,
        lhs.M10 * rhs.M02 + lhs.M11 * rhs.M12 + lhs.M12 * rhs.M22,
        lhs.M20 * rhs.M00 + lhs.M21 * rhs.M10 + lhs.M22 * rhs.M20,
        lhs.M20 * rhs.M01 + lhs.M21 * rhs.M11 + lhs.M22 * rhs.M21,
        lhs.M20 * rhs.M02 + lhs.M21 * rhs.M12 + lhs.M22 * rhs.M22);
}
