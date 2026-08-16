// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Math;

using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

/// <summary>3x4 matrix for scene node transform calculations.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct Matrix3x4 : IEquatable<Matrix3x4>, IApproximateEquatable<Matrix3x4>
{
    /// <summary>Construct an identity matrix.</summary>
    public Matrix3x4()
        : this(
            1.0f, 0.0f, 0.0f, 0.0f,
            0.0f, 1.0f, 0.0f, 0.0f,
            0.0f, 0.0f, 1.0f, 0.0f)
    {
    }

    /// <summary>Copy-construct from another matrix.</summary>
    public Matrix3x4(Matrix3x4 matrix)
        : this(
            matrix.M00, matrix.M01, matrix.M02, matrix.M03,
            matrix.M10, matrix.M11, matrix.M12, matrix.M13,
            matrix.M20, matrix.M21, matrix.M22, matrix.M23)
    {
    }

    /// <summary>Copy-construct from a 3x3 matrix and set the extra elements to identity.</summary>
    public Matrix3x4(Matrix3 matrix)
        : this(
            matrix.M00, matrix.M01, matrix.M02, 0.0f,
            matrix.M10, matrix.M11, matrix.M12, 0.0f,
            matrix.M20, matrix.M21, matrix.M22, 0.0f)
    {
    }

    /// <summary>Copy-construct from a 4x4 matrix which is assumed to contain no projection.</summary>
    public Matrix3x4(Matrix4 matrix)
        : this(
            matrix.M00, matrix.M01, matrix.M02, matrix.M03,
            matrix.M10, matrix.M11, matrix.M12, matrix.M13,
            matrix.M20, matrix.M21, matrix.M22, matrix.M23)
    {
    }

    /// <summary>Construct from values.</summary>
    public Matrix3x4(
        float v00, float v01, float v02, float v03,
        float v10, float v11, float v12, float v13,
        float v20, float v21, float v22, float v23)
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

    /// <summary>Construct from a float array.</summary>
    public Matrix3x4(ReadOnlySpan<float> data)
    {
        M00 = data[0];
        M01 = data[1];
        M02 = data[2];
        M03 = data[3];
        M10 = data[4];
        M11 = data[5];
        M12 = data[6];
        M13 = data[7];
        M20 = data[8];
        M21 = data[9];
        M22 = data[10];
        M23 = data[11];
    }

    /// <summary>Construct from translation, rotation and uniform scale.</summary>
    public Matrix3x4(Vector3f translation, Quaternion rotation, float scale)
    {
        this = default;
        SetRotation(rotation.RotationMatrix * scale);
        SetTranslation(translation);
    }

    /// <summary>Construct from translation, rotation and nonuniform scale.</summary>
    public Matrix3x4(Vector3f translation, Quaternion rotation, Vector3f scale)
    {
        this = default;
        SetRotation(rotation.RotationMatrix.Scaled(scale));
        SetTranslation(translation);
    }

    /// <summary>Construct from translation vector.</summary>
    public static Matrix3x4 FromTranslation(Vector3f translation)
    {
        var matrix = new Matrix3x4();
        matrix.SetTranslation(translation);
        return matrix;
    }

    /// <summary>Construct from rotation as quaternion.</summary>
    public static Matrix3x4 FromRotation(Quaternion rotation)
    {
        var matrix = new Matrix3x4();
        matrix.SetRotation(rotation.RotationMatrix);
        return matrix;
    }

    /// <summary>Constuct from scalar scale.</summary>
    public static Matrix3x4 FromScale(float scale)
    {
        var matrix = new Matrix3x4();
        matrix.SetScale(scale);
        return matrix;
    }

    /// <summary>Constuct from vector scale.</summary>
    public static Matrix3x4 FromScale(Vector3f scale)
    {
        var matrix = new Matrix3x4();
        matrix.SetScale(scale);
        return matrix;
    }

    /// <summary>Test for equality with another matrix without epsilon.</summary>
    public static bool operator ==(Matrix3x4 lhs, Matrix3x4 rhs)
    {
        var leftData = lhs.Data;
        var rightData = rhs.Data;
        for (var i = 0; i < 12; ++i)
        {
            if (leftData[i] != rightData[i])
                return false;
        }
        return true;
    }

    /// <summary>Test for inequality with another matrix without epsilon.</summary>
    public static bool operator !=(Matrix3x4 lhs, Matrix3x4 rhs) => !(lhs == rhs);

    /// <summary>Multiply a Vector3 which is assumed to represent position.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3f operator *(Matrix3x4 lhs, Vector3f rhs) => new(
        lhs.M00 * rhs.X + lhs.M01 * rhs.Y + lhs.M02 * rhs.Z + lhs.M03,
        lhs.M10 * rhs.X + lhs.M11 * rhs.Y + lhs.M12 * rhs.Z + lhs.M13,
        lhs.M20 * rhs.X + lhs.M21 * rhs.Y + lhs.M22 * rhs.Z + lhs.M23);

    /// <summary>Multiply a Vector4.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3f operator *(Matrix3x4 lhs, Vector4f rhs) => new(
        lhs.M00 * rhs.X + lhs.M01 * rhs.Y + lhs.M02 * rhs.Z + lhs.M03 * rhs.W,
        lhs.M10 * rhs.X + lhs.M11 * rhs.Y + lhs.M12 * rhs.Z + lhs.M13 * rhs.W,
        lhs.M20 * rhs.X + lhs.M21 * rhs.Y + lhs.M22 * rhs.Z + lhs.M23 * rhs.W);

    /// <summary>Add a matrix.</summary>
    public static Matrix3x4 operator +(Matrix3x4 lhs, Matrix3x4 rhs) => new(
        lhs.M00 + rhs.M00, lhs.M01 + rhs.M01, lhs.M02 + rhs.M02, lhs.M03 + rhs.M03,
        lhs.M10 + rhs.M10, lhs.M11 + rhs.M11, lhs.M12 + rhs.M12, lhs.M13 + rhs.M13,
        lhs.M20 + rhs.M20, lhs.M21 + rhs.M21, lhs.M22 + rhs.M22, lhs.M23 + rhs.M23);

    /// <summary>Subtract a matrix.</summary>
    public static Matrix3x4 operator -(Matrix3x4 lhs, Matrix3x4 rhs) => new(
        lhs.M00 - rhs.M00, lhs.M01 - rhs.M01, lhs.M02 - rhs.M02, lhs.M03 - rhs.M03,
        lhs.M10 - rhs.M10, lhs.M11 - rhs.M11, lhs.M12 - rhs.M12, lhs.M13 - rhs.M13,
        lhs.M20 - rhs.M20, lhs.M21 - rhs.M21, lhs.M22 - rhs.M22, lhs.M23 - rhs.M23);

    /// <summary>Multiply with a scalar.</summary>
    public static Matrix3x4 operator *(Matrix3x4 lhs, float rhs) => new(
        lhs.M00 * rhs, lhs.M01 * rhs, lhs.M02 * rhs, lhs.M03 * rhs,
        lhs.M10 * rhs, lhs.M11 * rhs, lhs.M12 * rhs, lhs.M13 * rhs,
        lhs.M20 * rhs, lhs.M21 * rhs, lhs.M22 * rhs, lhs.M23 * rhs);

    /// <summary>Multiply a matrix.</summary>
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

    /// <summary>Multiply a 4x4 matrix.</summary>
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
        rhs.M30, rhs.M31, rhs.M32, rhs.M33);

    /// <summary>Set translation elements.</summary>
    public void SetTranslation(Vector3f translation)
    {
        M03 = translation.X;
        M13 = translation.Y;
        M23 = translation.Z;
    }

    /// <summary>Set rotation elements from a 3x3 matrix.</summary>
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

    /// <summary>Set scaling elements.</summary>
    public void SetScale(Vector3f scale)
    {
        M00 = scale.X;
        M11 = scale.Y;
        M22 = scale.Z;
    }

    /// <summary>Set uniform scaling elements.</summary>
    public void SetScale(float scale)
    {
        M00 = scale;
        M11 = scale;
        M22 = scale;
    }

    /// <summary>Return the combined rotation and scaling matrix.</summary>
    public readonly Matrix3 Matrix3 => new(
        M00, M01, M02,
        M10, M11, M12,
        M20, M21, M22);

    /// <summary>Convert to a 4x4 matrix by filling in an identity last row.</summary>
    public readonly Matrix4 Matrix4 => new(
        M00, M01, M02, M03,
        M10, M11, M12, M13,
        M20, M21, M22, M23,
        0.0f, 0.0f, 0.0f, 1.0f);

    /// <summary>Return the rotation matrix with scaling removed.</summary>
    public readonly Matrix3 RotationMatrix
    {
        get
        {
            var invScale = new Vector3f(
                1.0f / MathF.Sqrt(M00 * M00 + M10 * M10 + M20 * M20),
                1.0f / MathF.Sqrt(M01 * M01 + M11 * M11 + M21 * M21),
                1.0f / MathF.Sqrt(M02 * M02 + M12 * M12 + M22 * M22));
            if (Determinant < 0.0f)
                invScale.X = -invScale.X;
            return Matrix3.Scaled(invScale);
        }
    }

    /// <summary>Return the translation part.</summary>
    public readonly Vector3f Translation => new(M03, M13, M23);

    /// <summary>Return the rotation part.</summary>
    public readonly Quaternion Rotation => new(RotationMatrix);

    /// <summary>Return the scaling part.</summary>
    public readonly Vector3f Scale
    {
        get
        {
            var determinantSign = Determinant < 0.0f ? -1.0f : 1.0f;
            return new Vector3f(
                MathF.Sqrt(M00 * M00 + M10 * M10 + M20 * M20) * determinantSign,
                MathF.Sqrt(M01 * M01 + M11 * M11 + M21 * M21),
                MathF.Sqrt(M02 * M02 + M12 * M12 + M22 * M22));
        }
    }

    /// <summary>Return the scaling part with the sign. Reference rotation matrix is required to avoid ambiguity.</summary>
    public readonly Vector3f SignedScale(Matrix3 rotation) => new(
        rotation.M00 * M00 + rotation.M10 * M10 + rotation.M20 * M20,
        rotation.M01 * M01 + rotation.M11 * M11 + rotation.M21 * M21,
        rotation.M02 * M02 + rotation.M12 * M12 + rotation.M22 * M22);

    /// <summary>Test for equality with another matrix with epsilon.</summary>
    public readonly bool ApproximatelyEquivalent(Matrix3x4 rhs, float epsilon = MathDefs.Epsilon)
    {
        var leftData = Data;
        var rightData = rhs.Data;
        for (var i = 0; i < 12; ++i)
        {
            if (!MathDefs.Equals(leftData[i], rightData[i], epsilon))
                return false;
        }
        return true;
    }

    /// <summary>Return decomposition to translation, rotation and scale.</summary>
    public readonly void Decompose(out Vector3f translation, out Quaternion rotation, out Vector3f scale)
    {
        translation.X = M03;
        translation.Y = M13;
        translation.Z = M23;

        scale.X = MathDefs.SnapTo(MathF.Sqrt(M00 * M00 + M10 * M10 + M20 * M20), 1.0f, MathDefs.Epsilon);
        scale.Y = MathDefs.SnapTo(MathF.Sqrt(M01 * M01 + M11 * M11 + M21 * M21), 1.0f, MathDefs.Epsilon);
        scale.Z = MathDefs.SnapTo(MathF.Sqrt(M02 * M02 + M12 * M12 + M22 * M22), 1.0f, MathDefs.Epsilon);

        var rotationScaleMatrix = Matrix3;
        if (rotationScaleMatrix.Determinant < 0.0f)
            scale.X = -scale.X;

        var invScale = new Vector3f(1.0f / scale.X, 1.0f / scale.Y, 1.0f / scale.Z);
        rotation = new Quaternion(rotationScaleMatrix.Scaled(invScale));
    }

    /// <summary>Return determinant.</summary>
    public readonly float Determinant =>
        M00 * M11 * M22 +
        M10 * M21 * M02 +
        M20 * M01 * M12 -
        M20 * M11 * M02 -
        M10 * M01 * M22 -
        M00 * M21 * M12;

    /// <summary>Return inverse.</summary>
    public readonly Matrix3x4 Inverse
    {
        get
        {
            var invDet = 1.0f / Determinant;
            var result = new Matrix3x4();

            result.M00 = (M11 * M22 - M21 * M12) * invDet;
            result.M01 = -(M01 * M22 - M21 * M02) * invDet;
            result.M02 = (M01 * M12 - M11 * M02) * invDet;
            result.M03 = -(M03 * result.M00 + M13 * result.M01 + M23 * result.M02);
            result.M10 = -(M10 * M22 - M20 * M12) * invDet;
            result.M11 = (M00 * M22 - M20 * M02) * invDet;
            result.M12 = -(M00 * M12 - M10 * M02) * invDet;
            result.M13 = -(M03 * result.M10 + M13 * result.M11 + M23 * result.M12);
            result.M20 = (M10 * M21 - M20 * M11) * invDet;
            result.M21 = -(M00 * M21 - M20 * M01) * invDet;
            result.M22 = (M00 * M11 - M10 * M01) * invDet;
            result.M23 = -(M03 * result.M20 + M13 * result.M21 + M23 * result.M22);

            return result;
        }
    }

    /// <summary>Return float data.</summary>
    public readonly ReadOnlySpan<float> Data => MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(in M00), 12);

    /// <summary>Return matrix element.</summary>
    public readonly float Element(int i, int j) => Data[i * 4 + j];

    /// <summary>Return matrix row.</summary>
    public readonly Vector4f Row(int i) => new(Element(i, 0), Element(i, 1), Element(i, 2), Element(i, 3));

    /// <summary>Return matrix column.</summary>
    public readonly Vector3f Column(int j) => new(Element(0, j), Element(1, j), Element(2, j));

    /// <summary>Return whether any element is NaN.</summary>
    public readonly bool IsNaN
    {
        get
        {
            var data = Data;
            for (var i = 0; i < 12; ++i)
            {
                if (float.IsNaN(data[i]))
                    return true;
            }
            return false;
        }
    }

    /// <summary>Return whether any element is Inf.</summary>
    public readonly bool IsInf
    {
        get
        {
            var data = Data;
            for (var i = 0; i < 12; ++i)
            {
                if (float.IsInfinity(data[i]))
                    return true;
            }
            return false;
        }
    }

    /// <summary>Return as string.</summary>
    public override readonly string ToString() => string.Format(
        CultureInfo.InvariantCulture,
        "{0} {1} {2} {3} {4} {5} {6} {7} {8} {9} {10} {11}",
        M00, M01, M02, M03, M10, M11, M12, M13, M20, M21, M22, M23);

    /// <summary>Return hash value for HashSet &amp; HashMap.</summary>
    public readonly uint ToHash()
    {
        var hash = 37u;
        for (var i = 0; i < 3 * 4; ++i)
            hash = unchecked(37u * hash + (uint)BitConverter.SingleToInt32Bits(Data[i]));
        return hash;
    }

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

    /// <summary>Zero matrix.</summary>
    public static readonly Matrix3x4 Zero = new(
        0.0f, 0.0f, 0.0f, 0.0f,
        0.0f, 0.0f, 0.0f, 0.0f,
        0.0f, 0.0f, 0.0f, 0.0f);

    /// <summary>Identity matrix.</summary>
    public static readonly Matrix3x4 Identity = new();

    /// <summary>Multiply a 3x4 matrix with a scalar.</summary>
    public static Matrix3x4 operator *(float lhs, Matrix3x4 rhs) => rhs * lhs;

    public readonly bool Equals(Matrix3x4 other) => this == other;
    public override readonly bool Equals(object? obj) => obj is Matrix3x4 other && Equals(other);
    public override readonly int GetHashCode() => unchecked((int)ToHash());
}
