using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace RedBolt;

[StructLayout(LayoutKind.Sequential)]
public struct Matrix3 : IEquatable<Matrix3>, IApproximateEquatable<Matrix3>
{
    /// <summary>Construct an identity matrix.</summary>
    public Matrix3()
        : this(
            1.0f, 0.0f, 0.0f,
            0.0f, 1.0f, 0.0f,
            0.0f, 0.0f, 1.0f)
    {
    }

    /// <summary>Copy-construct from another matrix.</summary>
    public Matrix3(Matrix3 matrix)
        : this(
            matrix.M00, matrix.M01, matrix.M02,
            matrix.M10, matrix.M11, matrix.M12,
            matrix.M20, matrix.M21, matrix.M22)
    {
    }

    /// <summary>Construct from values.</summary>
    public Matrix3(
        float v00, float v01, float v02,
        float v10, float v11, float v12,
        float v20, float v21, float v22)
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

    /// <summary>Construct from a float array.</summary>
    public Matrix3(ReadOnlySpan<float> data)
    {
        M00 = data[0];
        M01 = data[1];
        M02 = data[2];
        M10 = data[3];
        M11 = data[4];
        M12 = data[5];
        M20 = data[6];
        M21 = data[7];
        M22 = data[8];
    }

    /// <summary>Construct from an angle (in degrees) and axis.</summary>
    public Matrix3(float angle, Vector3f axis)
    {
        this = default;
        FromAngleAxis(angle, axis);
    }

    /// <summary>Test for equality with another matrix without epsilon.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(Matrix3 lhs, Matrix3 rhs) =>
        lhs.M00 == rhs.M00 && lhs.M01 == rhs.M01 && lhs.M02 == rhs.M02 &&
        lhs.M10 == rhs.M10 && lhs.M11 == rhs.M11 && lhs.M12 == rhs.M12 &&
        lhs.M20 == rhs.M20 && lhs.M21 == rhs.M21 && lhs.M22 == rhs.M22;

    /// <summary>Test for inequality with another matrix without epsilon.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(Matrix3 lhs, Matrix3 rhs) => !(lhs == rhs);

    /// <summary>Multiply a Vector3.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3f operator *(Matrix3 lhs, Vector3f rhs) => new(
        lhs.M00 * rhs.X + lhs.M01 * rhs.Y + lhs.M02 * rhs.Z,
        lhs.M10 * rhs.X + lhs.M11 * rhs.Y + lhs.M12 * rhs.Z,
        lhs.M20 * rhs.X + lhs.M21 * rhs.Y + lhs.M22 * rhs.Z);

    /// <summary>Add a matrix.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Matrix3 operator +(Matrix3 lhs, Matrix3 rhs) => new(
        lhs.M00 + rhs.M00, lhs.M01 + rhs.M01, lhs.M02 + rhs.M02,
        lhs.M10 + rhs.M10, lhs.M11 + rhs.M11, lhs.M12 + rhs.M12,
        lhs.M20 + rhs.M20, lhs.M21 + rhs.M21, lhs.M22 + rhs.M22);

    /// <summary>Subtract a matrix.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Matrix3 operator -(Matrix3 lhs, Matrix3 rhs) => new(
        lhs.M00 - rhs.M00, lhs.M01 - rhs.M01, lhs.M02 - rhs.M02,
        lhs.M10 - rhs.M10, lhs.M11 - rhs.M11, lhs.M12 - rhs.M12,
        lhs.M20 - rhs.M20, lhs.M21 - rhs.M21, lhs.M22 - rhs.M22);

    /// <summary>Multiply with a scalar.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Matrix3 operator *(Matrix3 lhs, float rhs) => new(
        lhs.M00 * rhs, lhs.M01 * rhs, lhs.M02 * rhs,
        lhs.M10 * rhs, lhs.M11 * rhs, lhs.M12 * rhs,
        lhs.M20 * rhs, lhs.M21 * rhs, lhs.M22 * rhs);

    /// <summary>Multiply a matrix.</summary>
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

    /// <summary>Define from an angle (in degrees) and axis.</summary>
    public void FromAngleAxis(float angle, Vector3f axis)
    {
        var normAxis = axis.Normalized;
        MathDefs.SinCos(angle, out var sinAngle, out var cosAngle);
        var oneMinusCosAngle = 1.0f - cosAngle;

        M00 = cosAngle + normAxis.X * normAxis.X * oneMinusCosAngle;
        M10 = normAxis.Y * normAxis.X * oneMinusCosAngle + normAxis.Z * sinAngle;
        M20 = normAxis.Z * normAxis.X * oneMinusCosAngle - normAxis.Y * sinAngle;

        M01 = normAxis.X * normAxis.Y * oneMinusCosAngle - normAxis.Z * sinAngle;
        M11 = cosAngle + normAxis.Y * normAxis.Y * oneMinusCosAngle;
        M21 = normAxis.Z * normAxis.Y * oneMinusCosAngle + normAxis.X * sinAngle;

        M02 = normAxis.X * normAxis.Z * oneMinusCosAngle + normAxis.Y * sinAngle;
        M12 = normAxis.Y * normAxis.Z * oneMinusCosAngle - normAxis.X * sinAngle;
        M22 = cosAngle + normAxis.Z * normAxis.Z * oneMinusCosAngle;
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
    public readonly Matrix3 Transpose => new(
        M00, M10, M20,
        M01, M11, M21,
        M02, M12, M22);

    /// <summary>Return scaled by a vector.</summary>
    public readonly Matrix3 Scaled(Vector3f scale) => new(
        M00 * scale.X, M01 * scale.Y, M02 * scale.Z,
        M10 * scale.X, M11 * scale.Y, M12 * scale.Z,
        M20 * scale.X, M21 * scale.Y, M22 * scale.Z);

    /// <summary>Test for equality with another matrix with epsilon.</summary>
    public readonly bool ApproximatelyEquivalent(Matrix3 rhs, float epsilon = MathDefs.Epsilon) =>
        MathDefs.Equals(M00, rhs.M00, epsilon) &&
        MathDefs.Equals(M01, rhs.M01, epsilon) &&
        MathDefs.Equals(M02, rhs.M02, epsilon) &&
        MathDefs.Equals(M10, rhs.M10, epsilon) &&
        MathDefs.Equals(M11, rhs.M11, epsilon) &&
        MathDefs.Equals(M12, rhs.M12, epsilon) &&
        MathDefs.Equals(M20, rhs.M20, epsilon) &&
        MathDefs.Equals(M21, rhs.M21, epsilon) &&
        MathDefs.Equals(M22, rhs.M22, epsilon);

    /// <summary>Return determinant.</summary>
    public readonly float Determinant =>
        M00 * M11 * M22 +
        M10 * M21 * M02 +
        M20 * M01 * M12 -
        M20 * M11 * M02 -
        M10 * M01 * M22 -
        M00 * M21 * M12;

    /// <summary>Return inverse.</summary>
    public readonly Matrix3 Inverse
    {
        get
        {
            var invDet = 1.0f / Determinant;
            return new Matrix3(
                (M11 * M22 - M21 * M12) * invDet,
                -(M01 * M22 - M21 * M02) * invDet,
                (M01 * M12 - M11 * M02) * invDet,
                -(M10 * M22 - M20 * M12) * invDet,
                (M00 * M22 - M20 * M02) * invDet,
                -(M00 * M12 - M10 * M02) * invDet,
                (M10 * M21 - M20 * M11) * invDet,
                -(M00 * M21 - M20 * M01) * invDet,
                (M00 * M11 - M10 * M01) * invDet);
        }
    }

    /// <summary>Return float data.</summary>
    public readonly ReadOnlySpan<float> Data => MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(in M00), 9);

    /// <summary>Return matrix element.</summary>
    public readonly float Element(int i, int j) => Data[i * 3 + j];

    /// <summary>Return matrix row.</summary>
    public readonly Vector3f Row(int i) => new(Element(i, 0), Element(i, 1), Element(i, 2));

    /// <summary>Return matrix column.</summary>
    public readonly Vector3f Column(int j) => new(Element(0, j), Element(1, j), Element(2, j));

    /// <summary>Return whether any element is NaN.</summary>
    public readonly bool IsNaN =>
        float.IsNaN(M00) || float.IsNaN(M01) || float.IsNaN(M02) ||
        float.IsNaN(M10) || float.IsNaN(M11) || float.IsNaN(M12) ||
        float.IsNaN(M20) || float.IsNaN(M21) || float.IsNaN(M22);

    /// <summary>Return whether any element is Inf.</summary>
    public readonly bool IsInf =>
        float.IsInfinity(M00) || float.IsInfinity(M01) || float.IsInfinity(M02) ||
        float.IsInfinity(M10) || float.IsInfinity(M11) || float.IsInfinity(M12) ||
        float.IsInfinity(M20) || float.IsInfinity(M21) || float.IsInfinity(M22);

    /// <summary>Return as string.</summary>
    public override readonly string ToString() => string.Format(
        CultureInfo.InvariantCulture,
        "{0} {1} {2} {3} {4} {5} {6} {7} {8}",
        M00, M01, M02, M10, M11, M12, M20, M21, M22);

    /// <summary>Return hash value for HashSet &amp; HashMap.</summary>
    public readonly uint ToHash()
    {
        var hash = 37u;
        for (var i = 0; i < 3 * 3; ++i)
            hash = unchecked(37u * hash + (uint)BitConverter.SingleToInt32Bits(Data[i]));
        return hash;
    }

    public float M00;
    public float M01;
    public float M02;
    public float M10;
    public float M11;
    public float M12;
    public float M20;
    public float M21;
    public float M22;

    /// <summary>Bulk transpose matrices.</summary>
    public static void BulkTranspose(Span<float> destination, ReadOnlySpan<float> source, int count)
    {
        for (var i = 0; i < count; ++i)
        {
            var offset = i * 9;
            destination[offset] = source[offset];
            destination[offset + 1] = source[offset + 3];
            destination[offset + 2] = source[offset + 6];
            destination[offset + 3] = source[offset + 1];
            destination[offset + 4] = source[offset + 4];
            destination[offset + 5] = source[offset + 7];
            destination[offset + 6] = source[offset + 2];
            destination[offset + 7] = source[offset + 5];
            destination[offset + 8] = source[offset + 8];
        }
    }

    /// <summary>Zero matrix.</summary>
    public static readonly Matrix3 Zero = new(
        0.0f, 0.0f, 0.0f,
        0.0f, 0.0f, 0.0f,
        0.0f, 0.0f, 0.0f);

    /// <summary>Identity matrix.</summary>
    public static readonly Matrix3 Identity = new();

    /// <summary>Multiply a 3x3 matrix with a scalar.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Matrix3 operator *(float lhs, Matrix3 rhs) => rhs * lhs;

    public readonly bool Equals(Matrix3 other) => this == other;
    public override readonly bool Equals(object? obj) => obj is Matrix3 other && Equals(other);
    public override readonly int GetHashCode() => unchecked((int)ToHash());
}
