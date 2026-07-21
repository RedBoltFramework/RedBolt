using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace RedBolt;

/// <summary>4x4 matrix for arbitrary linear transforms including projection.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct Matrix4 : IEquatable<Matrix4>, IApproximateEquatable<Matrix4>
{
    /// <summary>Construct an identity matrix.</summary>
    public Matrix4()
        : this(
            1.0f, 0.0f, 0.0f, 0.0f,
            0.0f, 1.0f, 0.0f, 0.0f,
            0.0f, 0.0f, 1.0f, 0.0f,
            0.0f, 0.0f, 0.0f, 1.0f)
    {
    }

    /// <summary>Copy-construct from another matrix.</summary>
    public Matrix4(Matrix4 matrix)
        : this(
            matrix.M00, matrix.M01, matrix.M02, matrix.M03,
            matrix.M10, matrix.M11, matrix.M12, matrix.M13,
            matrix.M20, matrix.M21, matrix.M22, matrix.M23,
            matrix.M30, matrix.M31, matrix.M32, matrix.M33)
    {
    }

    /// <summary>Copy-construct from a 3x3 matrix and set the extra elements to identity.</summary>
    public Matrix4(Matrix3 matrix)
        : this(
            matrix.M00, matrix.M01, matrix.M02, 0.0f,
            matrix.M10, matrix.M11, matrix.M12, 0.0f,
            matrix.M20, matrix.M21, matrix.M22, 0.0f,
            0.0f, 0.0f, 0.0f, 1.0f)
    {
    }

    /// <summary>Construct from values.</summary>
    public Matrix4(
        float v00, float v01, float v02, float v03,
        float v10, float v11, float v12, float v13,
        float v20, float v21, float v22, float v23,
        float v30, float v31, float v32, float v33)
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
        M30 = v30;
        M31 = v31;
        M32 = v32;
        M33 = v33;
    }

    /// <summary>Construct from a float array.</summary>
    public Matrix4(ReadOnlySpan<float> data)
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
        M30 = data[12];
        M31 = data[13];
        M32 = data[14];
        M33 = data[15];
    }

    /// <summary>Test for equality with another matrix without epsilon.</summary>
    public static bool operator ==(Matrix4 lhs, Matrix4 rhs)
    {
        var leftData = lhs.Data;
        var rightData = rhs.Data;
        for (var i = 0; i < 16; ++i)
        {
            if (leftData[i] != rightData[i])
                return false;
        }
        return true;
    }

    /// <summary>Test for inequality with another matrix without epsilon.</summary>
    public static bool operator !=(Matrix4 lhs, Matrix4 rhs) => !(lhs == rhs);

    /// <summary>Multiply a Vector3 which is assumed to represent position.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3f operator *(Matrix4 lhs, Vector3f rhs)
    {
        var invW = 1.0f / (lhs.M30 * rhs.X + lhs.M31 * rhs.Y + lhs.M32 * rhs.Z + lhs.M33);
        return new Vector3f(
            (lhs.M00 * rhs.X + lhs.M01 * rhs.Y + lhs.M02 * rhs.Z + lhs.M03) * invW,
            (lhs.M10 * rhs.X + lhs.M11 * rhs.Y + lhs.M12 * rhs.Z + lhs.M13) * invW,
            (lhs.M20 * rhs.X + lhs.M21 * rhs.Y + lhs.M22 * rhs.Z + lhs.M23) * invW);
    }

    /// <summary>Multiply a Vector4.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4f operator *(Matrix4 lhs, Vector4f rhs) => new(
        lhs.M00 * rhs.X + lhs.M01 * rhs.Y + lhs.M02 * rhs.Z + lhs.M03 * rhs.W,
        lhs.M10 * rhs.X + lhs.M11 * rhs.Y + lhs.M12 * rhs.Z + lhs.M13 * rhs.W,
        lhs.M20 * rhs.X + lhs.M21 * rhs.Y + lhs.M22 * rhs.Z + lhs.M23 * rhs.W,
        lhs.M30 * rhs.X + lhs.M31 * rhs.Y + lhs.M32 * rhs.Z + lhs.M33 * rhs.W);

    /// <summary>Add a matrix.</summary>
    public static Matrix4 operator +(Matrix4 lhs, Matrix4 rhs) => new(
        lhs.M00 + rhs.M00, lhs.M01 + rhs.M01, lhs.M02 + rhs.M02, lhs.M03 + rhs.M03,
        lhs.M10 + rhs.M10, lhs.M11 + rhs.M11, lhs.M12 + rhs.M12, lhs.M13 + rhs.M13,
        lhs.M20 + rhs.M20, lhs.M21 + rhs.M21, lhs.M22 + rhs.M22, lhs.M23 + rhs.M23,
        lhs.M30 + rhs.M30, lhs.M31 + rhs.M31, lhs.M32 + rhs.M32, lhs.M33 + rhs.M33);

    /// <summary>Subtract a matrix.</summary>
    public static Matrix4 operator -(Matrix4 lhs, Matrix4 rhs) => new(
        lhs.M00 - rhs.M00, lhs.M01 - rhs.M01, lhs.M02 - rhs.M02, lhs.M03 - rhs.M03,
        lhs.M10 - rhs.M10, lhs.M11 - rhs.M11, lhs.M12 - rhs.M12, lhs.M13 - rhs.M13,
        lhs.M20 - rhs.M20, lhs.M21 - rhs.M21, lhs.M22 - rhs.M22, lhs.M23 - rhs.M23,
        lhs.M30 - rhs.M30, lhs.M31 - rhs.M31, lhs.M32 - rhs.M32, lhs.M33 - rhs.M33);

    /// <summary>Multiply with a scalar.</summary>
    public static Matrix4 operator *(Matrix4 lhs, float rhs) => new(
        lhs.M00 * rhs, lhs.M01 * rhs, lhs.M02 * rhs, lhs.M03 * rhs,
        lhs.M10 * rhs, lhs.M11 * rhs, lhs.M12 * rhs, lhs.M13 * rhs,
        lhs.M20 * rhs, lhs.M21 * rhs, lhs.M22 * rhs, lhs.M23 * rhs,
        lhs.M30 * rhs, lhs.M31 * rhs, lhs.M32 * rhs, lhs.M33 * rhs);

    /// <summary>Multiply a matrix.</summary>
    public static Matrix4 operator *(Matrix4 lhs, Matrix4 rhs) => new(
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
        lhs.M30 * rhs.M00 + lhs.M31 * rhs.M10 + lhs.M32 * rhs.M20 + lhs.M33 * rhs.M30,
        lhs.M30 * rhs.M01 + lhs.M31 * rhs.M11 + lhs.M32 * rhs.M21 + lhs.M33 * rhs.M31,
        lhs.M30 * rhs.M02 + lhs.M31 * rhs.M12 + lhs.M32 * rhs.M22 + lhs.M33 * rhs.M32,
        lhs.M30 * rhs.M03 + lhs.M31 * rhs.M13 + lhs.M32 * rhs.M23 + lhs.M33 * rhs.M33);

    /// <summary>Multiply with a 3x4 matrix.</summary>
    public static Matrix4 operator *(Matrix4 lhs, Matrix3x4 rhs) => new(
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
        lhs.M20 * rhs.M03 + lhs.M21 * rhs.M13 + lhs.M22 * rhs.M23 + lhs.M23,
        lhs.M30 * rhs.M00 + lhs.M31 * rhs.M10 + lhs.M32 * rhs.M20,
        lhs.M30 * rhs.M01 + lhs.M31 * rhs.M11 + lhs.M32 * rhs.M21,
        lhs.M30 * rhs.M02 + lhs.M31 * rhs.M12 + lhs.M32 * rhs.M22,
        lhs.M30 * rhs.M03 + lhs.M31 * rhs.M13 + lhs.M32 * rhs.M23 + lhs.M33);

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

    /// <summary>Return the rotation matrix with scaling removed.</summary>
    public readonly Matrix3 RotationMatrix
    {
        get
        {
            var invScale = new Vector3f(
                1.0f / MathF.Sqrt(M00 * M00 + M10 * M10 + M20 * M20),
                1.0f / MathF.Sqrt(M01 * M01 + M11 * M11 + M21 * M21),
                1.0f / MathF.Sqrt(M02 * M02 + M12 * M12 + M22 * M22));
            return Matrix3.Scaled(invScale);
        }
    }

    /// <summary>Return the translation part.</summary>
    public readonly Vector3f Translation => new(M03, M13, M23);

    /// <summary>Return the rotation part.</summary>
    public readonly Quaternion Rotation => new(RotationMatrix);

    /// <summary>Return the scaling part.</summary>
    public readonly Vector3f Scale => new(
        MathF.Sqrt(M00 * M00 + M10 * M10 + M20 * M20),
        MathF.Sqrt(M01 * M01 + M11 * M11 + M21 * M21),
        MathF.Sqrt(M02 * M02 + M12 * M12 + M22 * M22));

    /// <summary>Return the scaling part with the sign. Reference rotation matrix is required to avoid ambiguity.</summary>
    public readonly Vector3f SignedScale(Matrix3 rotation) => new(
        rotation.M00 * M00 + rotation.M10 * M10 + rotation.M20 * M20,
        rotation.M01 * M01 + rotation.M11 * M11 + rotation.M21 * M21,
        rotation.M02 * M02 + rotation.M12 * M12 + rotation.M22 * M22);

    /// <summary>Return transposed.</summary>
    public readonly Matrix4 Transpose => new(
        M00, M10, M20, M30,
        M01, M11, M21, M31,
        M02, M12, M22, M32,
        M03, M13, M23, M33);

    /// <summary>Test for equality with another matrix with epsilon.</summary>
    public readonly bool ApproximatelyEquivalent(Matrix4 rhs, float epsilon = MathDefs.Epsilon)
    {
        var leftData = Data;
        var rightData = rhs.Data;
        for (var i = 0; i < 16; ++i)
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
    public readonly float Determinant
    {
        get
        {
            var v0 = M20 * M31 - M21 * M30;
            var v1 = M20 * M32 - M22 * M30;
            var v2 = M20 * M33 - M23 * M30;
            var v3 = M21 * M32 - M22 * M31;
            var v4 = M21 * M33 - M23 * M31;
            var v5 = M22 * M33 - M23 * M32;

            var i00 = v5 * M11 - v4 * M12 + v3 * M13;
            var i10 = -(v5 * M10 - v2 * M12 + v1 * M13);
            var i20 = v4 * M10 - v2 * M11 + v0 * M13;
            var i30 = -(v3 * M10 - v1 * M11 + v0 * M12);

            return i00 * M00 + i10 * M01 + i20 * M02 + i30 * M03;
        }
    }

    /// <summary>Return inverse.</summary>
    public readonly Matrix4 Inverse
    {
        get
        {
            var v0 = M20 * M31 - M21 * M30;
            var v1 = M20 * M32 - M22 * M30;
            var v2 = M20 * M33 - M23 * M30;
            var v3 = M21 * M32 - M22 * M31;
            var v4 = M21 * M33 - M23 * M31;
            var v5 = M22 * M33 - M23 * M32;

            var i00 = v5 * M11 - v4 * M12 + v3 * M13;
            var i10 = -(v5 * M10 - v2 * M12 + v1 * M13);
            var i20 = v4 * M10 - v2 * M11 + v0 * M13;
            var i30 = -(v3 * M10 - v1 * M11 + v0 * M12);

            var invDet = 1.0f / (i00 * M00 + i10 * M01 + i20 * M02 + i30 * M03);

            i00 *= invDet;
            i10 *= invDet;
            i20 *= invDet;
            i30 *= invDet;

            var i01 = -(v5 * M01 - v4 * M02 + v3 * M03) * invDet;
            var i11 = (v5 * M00 - v2 * M02 + v1 * M03) * invDet;
            var i21 = -(v4 * M00 - v2 * M01 + v0 * M03) * invDet;
            var i31 = (v3 * M00 - v1 * M01 + v0 * M02) * invDet;

            v0 = M10 * M31 - M11 * M30;
            v1 = M10 * M32 - M12 * M30;
            v2 = M10 * M33 - M13 * M30;
            v3 = M11 * M32 - M12 * M31;
            v4 = M11 * M33 - M13 * M31;
            v5 = M12 * M33 - M13 * M32;

            var i02 = (v5 * M01 - v4 * M02 + v3 * M03) * invDet;
            var i12 = -(v5 * M00 - v2 * M02 + v1 * M03) * invDet;
            var i22 = (v4 * M00 - v2 * M01 + v0 * M03) * invDet;
            var i32 = -(v3 * M00 - v1 * M01 + v0 * M02) * invDet;

            v0 = M21 * M10 - M20 * M11;
            v1 = M22 * M10 - M20 * M12;
            v2 = M23 * M10 - M20 * M13;
            v3 = M22 * M11 - M21 * M12;
            v4 = M23 * M11 - M21 * M13;
            v5 = M23 * M12 - M22 * M13;

            var i03 = -(v5 * M01 - v4 * M02 + v3 * M03) * invDet;
            var i13 = (v5 * M00 - v2 * M02 + v1 * M03) * invDet;
            var i23 = -(v4 * M00 - v2 * M01 + v0 * M03) * invDet;
            var i33 = (v3 * M00 - v1 * M01 + v0 * M02) * invDet;

            return new Matrix4(
                i00, i01, i02, i03,
                i10, i11, i12, i13,
                i20, i21, i22, i23,
                i30, i31, i32, i33);
        }
    }

    /// <summary>Return float data.</summary>
    public readonly ReadOnlySpan<float> Data => MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(in M00), 16);

    /// <summary>Return matrix element.</summary>
    public readonly float Element(int i, int j) => Data[i * 4 + j];

    /// <summary>Return matrix row.</summary>
    public readonly Vector4f Row(int i) => new(Element(i, 0), Element(i, 1), Element(i, 2), Element(i, 3));

    /// <summary>Return matrix column.</summary>
    public readonly Vector4f Column(int j) => new(Element(0, j), Element(1, j), Element(2, j), Element(3, j));

    /// <summary>Return whether any element is NaN.</summary>
    public readonly bool IsNaN
    {
        get
        {
            var data = Data;
            for (var i = 0; i < 16; ++i)
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
            for (var i = 0; i < 16; ++i)
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
        "{0} {1} {2} {3} {4} {5} {6} {7} {8} {9} {10} {11} {12} {13} {14} {15}",
        M00, M01, M02, M03, M10, M11, M12, M13, M20, M21, M22, M23, M30, M31, M32, M33);

    /// <summary>Return hash value for HashSet &amp; HashMap.</summary>
    public readonly uint ToHash()
    {
        var hash = 37u;
        for (var i = 0; i < 4 * 4; ++i)
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
    public float M30;
    public float M31;
    public float M32;
    public float M33;

    /// <summary>Bulk transpose matrices.</summary>
    public static void BulkTranspose(Span<float> destination, ReadOnlySpan<float> source, int count)
    {
        for (var i = 0; i < count; ++i)
        {
            var offset = i * 16;
            destination[offset] = source[offset];
            destination[offset + 1] = source[offset + 4];
            destination[offset + 2] = source[offset + 8];
            destination[offset + 3] = source[offset + 12];
            destination[offset + 4] = source[offset + 1];
            destination[offset + 5] = source[offset + 5];
            destination[offset + 6] = source[offset + 9];
            destination[offset + 7] = source[offset + 13];
            destination[offset + 8] = source[offset + 2];
            destination[offset + 9] = source[offset + 6];
            destination[offset + 10] = source[offset + 10];
            destination[offset + 11] = source[offset + 14];
            destination[offset + 12] = source[offset + 3];
            destination[offset + 13] = source[offset + 7];
            destination[offset + 14] = source[offset + 11];
            destination[offset + 15] = source[offset + 15];
        }
    }

    /// <summary>Zero matrix.</summary>
    public static readonly Matrix4 Zero = new(
        0.0f, 0.0f, 0.0f, 0.0f,
        0.0f, 0.0f, 0.0f, 0.0f,
        0.0f, 0.0f, 0.0f, 0.0f,
        0.0f, 0.0f, 0.0f, 0.0f);

    /// <summary>Identity matrix.</summary>
    public static readonly Matrix4 Identity = new();

    /// <summary>Multiply a 4x4 matrix with a scalar.</summary>
    public static Matrix4 operator *(float lhs, Matrix4 rhs) => rhs * lhs;

    public readonly bool Equals(Matrix4 other) => this == other;
    public override readonly bool Equals(object? obj) => obj is Matrix4 other && Equals(other);
    public override readonly int GetHashCode() => unchecked((int)ToHash());
}
