using System.Globalization;
using System.Runtime.InteropServices;

namespace RedBolt;

[StructLayout(LayoutKind.Sequential)]
public struct Matrix3x4 : IEquatable<Matrix3x4>, IApproximateEquatable<Matrix3x4>
{
    public float M00;
    public float M01;
    public float M02;
    public float M03;
    public float M10;
    public float M11;
    public float M12;
    public float M13;
    public float M20;
    public float M21;
    public float M22;
    public float M23;

    public Matrix3x4(
        float v00 = 1.0f, float v01 = 0.0f, float v02 = 0.0f, float v03 = 0.0f,
        float v10 = 0.0f, float v11 = 1.0f, float v12 = 0.0f, float v13 = 0.0f,
        float v20 = 0.0f, float v21 = 0.0f, float v22 = 1.0f, float v23 = 0.0f)
    {
        M00 = v00;
        M01 = v01;
        M02 = v02;
        M03 = v03;
        M10 = v10;
        M11 = v11;
        M12 = v12;
        M13 = v13;
        M20 = v20;
        M21 = v21;
        M22 = v22;
        M23 = v23;
    }

    public Matrix3x4(Matrix3 matrix)
        : this(matrix.M00, matrix.M01, matrix.M02, 0.0f, matrix.M10, matrix.M11, matrix.M12, 0.0f, matrix.M20, matrix.M21, matrix.M22, 0.0f)
    {
    }

    public Matrix3x4(Matrix4 matrix)
        : this(matrix.M00, matrix.M01, matrix.M02, matrix.M03, matrix.M10, matrix.M11, matrix.M12, matrix.M13, matrix.M20, matrix.M21, matrix.M22, matrix.M23)
    {
    }

    public Matrix3x4(Vector3 translation, Quaternion rotation, float scale)
        : this(translation, rotation, new Vector3(scale, scale, scale))
    {
    }

    public Matrix3x4(Vector3 translation, Quaternion rotation, Vector3 scale)
    {
        this = default;
        SetRotation(rotation.RotationMatrix.Scaled(scale));
        SetTranslation(translation);
    }

    public static readonly Matrix3x4 IDENTITY = new();
    public static Matrix3x4 Identity => IDENTITY;

    public Matrix3 Matrix3 => new(M00, M01, M02, M10, M11, M12, M20, M21, M22);
    public Matrix4 Matrix4 => new(M00, M01, M02, M03, M10, M11, M12, M13, M20, M21, M22, M23, 0.0f, 0.0f, 0.0f, 1.0f);
    public Matrix3 RotationMatrix => Matrix3.Scaled(new Vector3(
        1.0f / MathF.Sqrt(M00 * M00 + M10 * M10 + M20 * M20),
        1.0f / MathF.Sqrt(M01 * M01 + M11 * M11 + M21 * M21),
        1.0f / MathF.Sqrt(M02 * M02 + M12 * M12 + M22 * M22)));
    public Quaternion Rotation => new(RotationMatrix);
    public Vector3 Scale => new(
        MathF.Sqrt(M00 * M00 + M10 * M10 + M20 * M20),
        MathF.Sqrt(M01 * M01 + M11 * M11 + M21 * M21),
        MathF.Sqrt(M02 * M02 + M12 * M12 + M22 * M22));
    public Vector3 Translation => new(M03, M13, M23);

    public void SetRotation(Matrix3 rotation)
    {
        M00 = rotation.M00;
        M01 = rotation.M01;
        M02 = rotation.M02;
        M10 = rotation.M10;
        M11 = rotation.M11;
        M12 = rotation.M12;
        M20 = rotation.M20;
        M21 = rotation.M21;
        M22 = rotation.M22;
    }

    public void SetScale(Vector3 scale)
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

    public void SetTranslation(Vector3 translation)
    {
        M03 = translation.X;
        M13 = translation.Y;
        M23 = translation.Z;
    }

    public Vector3 SignedScale(Matrix3 rotation) => new(
        rotation.M00 * M00 + rotation.M10 * M10 + rotation.M20 * M20,
        rotation.M01 * M01 + rotation.M11 * M11 + rotation.M21 * M21,
        rotation.M02 * M02 + rotation.M12 * M12 + rotation.M22 * M22);

    public bool ApproximatelyEquivalent(Matrix3x4 rhs, float epsilon = MathDefs.Epsilon) =>
        MathDefs.ApproximatelyEquivalent(M00, rhs.M00, epsilon) &&
        MathDefs.ApproximatelyEquivalent(M01, rhs.M01, epsilon) &&
        MathDefs.ApproximatelyEquivalent(M02, rhs.M02, epsilon) &&
        MathDefs.ApproximatelyEquivalent(M03, rhs.M03, epsilon) &&
        MathDefs.ApproximatelyEquivalent(M10, rhs.M10, epsilon) &&
        MathDefs.ApproximatelyEquivalent(M11, rhs.M11, epsilon) &&
        MathDefs.ApproximatelyEquivalent(M12, rhs.M12, epsilon) &&
        MathDefs.ApproximatelyEquivalent(M13, rhs.M13, epsilon) &&
        MathDefs.ApproximatelyEquivalent(M20, rhs.M20, epsilon) &&
        MathDefs.ApproximatelyEquivalent(M21, rhs.M21, epsilon) &&
        MathDefs.ApproximatelyEquivalent(M22, rhs.M22, epsilon) &&
        MathDefs.ApproximatelyEquivalent(M23, rhs.M23, epsilon);

    public bool Equals(Matrix3x4 other) => this == other;
    public override bool Equals(object? obj) => obj is Matrix3x4 other && Equals(other);
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(M00);
        hash.Add(M01);
        hash.Add(M02);
        hash.Add(M03);
        hash.Add(M10);
        hash.Add(M11);
        hash.Add(M12);
        hash.Add(M13);
        hash.Add(M20);
        hash.Add(M21);
        hash.Add(M22);
        hash.Add(M23);
        return hash.ToHashCode();
    }
    public override string ToString() => string.Format(CultureInfo.InvariantCulture, "{0} {1} {2} {3} {4} {5} {6} {7} {8} {9} {10} {11}", M00, M01, M02, M03, M10, M11, M12, M13, M20, M21, M22, M23);

    public static bool operator ==(Matrix3x4 lhs, Matrix3x4 rhs) =>
        lhs.M00 == rhs.M00 && lhs.M01 == rhs.M01 && lhs.M02 == rhs.M02 && lhs.M03 == rhs.M03 &&
        lhs.M10 == rhs.M10 && lhs.M11 == rhs.M11 && lhs.M12 == rhs.M12 && lhs.M13 == rhs.M13 &&
        lhs.M20 == rhs.M20 && lhs.M21 == rhs.M21 && lhs.M22 == rhs.M22 && lhs.M23 == rhs.M23;

    public static bool operator !=(Matrix3x4 lhs, Matrix3x4 rhs) => !(lhs == rhs);

    public static Vector3 operator *(Matrix3x4 lhs, Vector3 rhs) => new(
        lhs.M00 * rhs.X + lhs.M01 * rhs.Y + lhs.M02 * rhs.Z + lhs.M03,
        lhs.M10 * rhs.X + lhs.M11 * rhs.Y + lhs.M12 * rhs.Z + lhs.M13,
        lhs.M20 * rhs.X + lhs.M21 * rhs.Y + lhs.M22 * rhs.Z + lhs.M23);

    public static Vector3 operator *(Matrix3x4 lhs, Vector4 rhs) => new(
        lhs.M00 * rhs.X + lhs.M01 * rhs.Y + lhs.M02 * rhs.Z + lhs.M03 * rhs.W,
        lhs.M10 * rhs.X + lhs.M11 * rhs.Y + lhs.M12 * rhs.Z + lhs.M13 * rhs.W,
        lhs.M20 * rhs.X + lhs.M21 * rhs.Y + lhs.M22 * rhs.Z + lhs.M23 * rhs.W);

    public static Matrix3x4 operator *(Matrix3x4 lhs, Matrix3x4 rhs) => new(
        lhs.M00 * rhs.M00 + lhs.M01 * rhs.M10 + lhs.M02 * rhs.M20,
        lhs.M00 * rhs.M01 + lhs.M01 * rhs.M11 + lhs.M02 * rhs.M21,
        lhs.M00 * rhs.M02 + lhs.M01 * rhs.M12 + lhs.M02 * rhs.M22,
        lhs.M00 * rhs.M03 + lhs.M01 * rhs.M13 + lhs.M02 * rhs.M23 + lhs.M03,
        lhs.M10 * rhs.M00 + lhs.M11 * rhs.M10 + lhs.M12 * rhs.M20,
        lhs.M10 * rhs.M01 + lhs.M11 * rhs.M11 + lhs.M12 * rhs.M21,
        lhs.M10 * rhs.M02 + lhs.M11 * rhs.M12 + lhs.M12 * rhs.M22,
        lhs.M10 * rhs.M03 + lhs.M11 * rhs.M13 + lhs.M12 * rhs.M23 + lhs.M13,
        lhs.M20 * rhs.M00 + lhs.M21 * rhs.M10 + lhs.M22 * rhs.M20,
        lhs.M20 * rhs.M01 + lhs.M21 * rhs.M11 + lhs.M22 * rhs.M21,
        lhs.M20 * rhs.M02 + lhs.M21 * rhs.M12 + lhs.M22 * rhs.M22,
        lhs.M20 * rhs.M03 + lhs.M21 * rhs.M13 + lhs.M22 * rhs.M23 + lhs.M23);

    public static Matrix4 operator *(Matrix3x4 lhs, Matrix4 rhs) => new(
        lhs.M00 * rhs.M00 + lhs.M01 * rhs.M10 + lhs.M02 * rhs.M20 + lhs.M03 * rhs.M30,
        lhs.M00 * rhs.M01 + lhs.M01 * rhs.M11 + lhs.M02 * rhs.M21 + lhs.M03 * rhs.M31,
        lhs.M00 * rhs.M02 + lhs.M01 * rhs.M12 + lhs.M02 * rhs.M22 + lhs.M03 * rhs.M32,
        lhs.M00 * rhs.M03 + lhs.M01 * rhs.M13 + lhs.M02 * rhs.M23 + lhs.M03 * rhs.M33,
        lhs.M10 * rhs.M00 + lhs.M11 * rhs.M10 + lhs.M12 * rhs.M20 + lhs.M13 * rhs.M30,
        lhs.M10 * rhs.M01 + lhs.M11 * rhs.M11 + lhs.M12 * rhs.M21 + lhs.M13 * rhs.M31,
        lhs.M10 * rhs.M02 + lhs.M11 * rhs.M12 + lhs.M12 * rhs.M22 + lhs.M13 * rhs.M32,
        lhs.M10 * rhs.M03 + lhs.M11 * rhs.M13 + lhs.M12 * rhs.M23 + lhs.M13 * rhs.M33,
        lhs.M20 * rhs.M00 + lhs.M21 * rhs.M10 + lhs.M22 * rhs.M20 + lhs.M23 * rhs.M30,
        lhs.M20 * rhs.M01 + lhs.M21 * rhs.M11 + lhs.M22 * rhs.M21 + lhs.M23 * rhs.M31,
        lhs.M20 * rhs.M02 + lhs.M21 * rhs.M12 + lhs.M22 * rhs.M22 + lhs.M23 * rhs.M32,
        lhs.M20 * rhs.M03 + lhs.M21 * rhs.M13 + lhs.M22 * rhs.M23 + lhs.M23 * rhs.M33,
        rhs.M30,
        rhs.M31,
        rhs.M32,
        rhs.M33);
}
