using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace RedBolt;

[StructLayout(LayoutKind.Sequential)]
public struct Matrix4 : IEquatable<Matrix4>, IApproximateEquatable<Matrix4>
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
    public float M30;
    public float M31;
    public float M32;
    public float M33;

    public Matrix4(Matrix3 matrix)
    {
        M00 = matrix.M00;
        M01 = matrix.M01;
        M02 = matrix.M02;
        M03 = 0.0f;
        M10 = matrix.M10;
        M11 = matrix.M11;
        M12 = matrix.M12;
        M13 = 0.0f;
        M20 = matrix.M20;
        M21 = matrix.M21;
        M22 = matrix.M22;
        M23 = 0.0f;
        M30 = 0.0f;
        M31 = 0.0f;
        M32 = 0.0f;
        M33 = 1.0f;
    }

    public Matrix4(
        float v00 = 1.0f, float v01 = 0.0f, float v02 = 0.0f, float v03 = 0.0f,
        float v10 = 0.0f, float v11 = 1.0f, float v12 = 0.0f, float v13 = 0.0f,
        float v20 = 0.0f, float v21 = 0.0f, float v22 = 1.0f, float v23 = 0.0f,
        float v30 = 0.0f, float v31 = 0.0f, float v32 = 0.0f, float v33 = 1.0f)
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

    public Matrix4(IReadOnlyList<float> data)
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

    public static readonly Matrix4 ZERO = new(
        0.0f, 0.0f, 0.0f, 0.0f,
        0.0f, 0.0f, 0.0f, 0.0f,
        0.0f, 0.0f, 0.0f, 0.0f,
        0.0f, 0.0f, 0.0f, 0.0f);

    public static readonly Matrix4 IDENTITY = new(
        1.0f, 0.0f, 0.0f, 0.0f,
        0.0f, 1.0f, 0.0f, 0.0f,
        0.0f, 0.0f, 1.0f, 0.0f,
        0.0f, 0.0f, 0.0f, 1.0f);

    public static Matrix4 Zero => ZERO;
    public static Matrix4 Identity => IDENTITY;

    public Vector3f Translation
    {
        get => new(M03, M13, M23);
        set
        {
            M03 = value.X;
            M13 = value.Y;
            M23 = value.Z;
        }
    }

    public Matrix3 Matrix3 => new(M00, M01, M02, M10, M11, M12, M20, M21, M22);

    public Matrix3 RotationMatrix
    {
        get
        {
            var invScale = new Vector3f(
                1.0f / MathF.Sqrt(M00 * M00 + M10 * M10 + M20 * M20),
                1.0f / MathF.Sqrt(M01 * M01 + M11 * M11 + M21 * M21),
                1.0f / MathF.Sqrt(M02 * M02 + M12 * M12 + M22 * M22));
            return this.Matrix3.Scaled(invScale);
        }
        set
        {
            M00 = value.M00;
            M01 = value.M01;
            M02 = value.M02;
            M10 = value.M10;
            M11 = value.M11;
            M12 = value.M12;
            M20 = value.M20;
            M21 = value.M21;
            M22 = value.M22;
        }
    }

    public Quaternion Rotation => new(RotationMatrix);

    public Vector3f Scale
    {
        get => new(
            MathF.Sqrt(M00 * M00 + M10 * M10 + M20 * M20),
            MathF.Sqrt(M01 * M01 + M11 * M11 + M21 * M21),
            MathF.Sqrt(M02 * M02 + M12 * M12 + M22 * M22));
        set
        {
            M00 = value.X;
            M11 = value.Y;
            M22 = value.Z;
        }
    }

    public Matrix4 Transposed => new(
        M00, M10, M20, M30,
        M01, M11, M21, M31,
        M02, M12, M22, M32,
        M03, M13, M23, M33);

    public Matrix4 Inverted
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

    public float[] Data =>
    [
        M00, M01, M02, M03,
        M10, M11, M12, M13,
        M20, M21, M22, M23,
        M30, M31, M32, M33
    ];
    public float Determinant
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
    public Matrix4 Inverse => Inverted;
    public bool IsNaN => Data.Any(float.IsNaN);
    public bool IsInf => Data.Any(float.IsInfinity);

    public float this[int i, int j]
    {
        get => (i, j) switch
        {
            (0, 0) => M00,
            (0, 1) => M01,
            (0, 2) => M02,
            (0, 3) => M03,
            (1, 0) => M10,
            (1, 1) => M11,
            (1, 2) => M12,
            (1, 3) => M13,
            (2, 0) => M20,
            (2, 1) => M21,
            (2, 2) => M22,
            (2, 3) => M23,
            (3, 0) => M30,
            (3, 1) => M31,
            (3, 2) => M32,
            (3, 3) => M33,
            _ => throw new IndexOutOfRangeException()
        };
        set
        {
            switch (i, j)
            {
                case (0, 0): M00 = value; break;
                case (0, 1): M01 = value; break;
                case (0, 2): M02 = value; break;
                case (0, 3): M03 = value; break;
                case (1, 0): M10 = value; break;
                case (1, 1): M11 = value; break;
                case (1, 2): M12 = value; break;
                case (1, 3): M13 = value; break;
                case (2, 0): M20 = value; break;
                case (2, 1): M21 = value; break;
                case (2, 2): M22 = value; break;
                case (2, 3): M23 = value; break;
                case (3, 0): M30 = value; break;
                case (3, 1): M31 = value; break;
                case (3, 2): M32 = value; break;
                case (3, 3): M33 = value; break;
                default: throw new IndexOutOfRangeException();
            }
        }
    }

    public void SetScale(float scale)
    {
        M00 = scale;
        M11 = scale;
        M22 = scale;
    }

    public void SetScale(Vector3f scale)
    {
        M00 = scale.X;
        M11 = scale.Y;
        M22 = scale.Z;
    }

    public void SetRotation(Matrix3 rotation)
    {
        M00 = rotation.M00; M01 = rotation.M01; M02 = rotation.M02;
        M10 = rotation.M10; M11 = rotation.M11; M12 = rotation.M12;
        M20 = rotation.M20; M21 = rotation.M21; M22 = rotation.M22;
    }

    public Vector3f SignedScale(Matrix3 rotation) => new(
        rotation.M00 * M00 + rotation.M10 * M10 + rotation.M20 * M20,
        rotation.M01 * M01 + rotation.M11 * M11 + rotation.M21 * M21,
        rotation.M02 * M02 + rotation.M12 * M12 + rotation.M22 * M22);

    public void Decompose(out Vector3f translation, out Quaternion rotation, out Vector3f scale)
    {
        translation = Translation;
        scale = Scale;
        var invScale = new Vector3f(1.0f / scale.X, 1.0f / scale.Y, 1.0f / scale.Z);
        rotation = new Quaternion(this.Matrix3.Scaled(invScale));
    }

    public Vector4f Row(int i) => new(this[i, 0], this[i, 1], this[i, 2], this[i, 3]);
    public Vector4f Column(int j) => new(this[0, j], this[1, j], this[2, j], this[3, j]);

    public float Element(uint row, uint column) => this[checked((int)row), checked((int)column)];

    public uint ToHash()
    {
        var hash = 37u;
        foreach (var value in Data)
            hash = unchecked(37u * hash + (uint)BitConverter.SingleToInt32Bits(value));
        return hash;
    }

    public static void BulkTranspose(float[] destination, float[] source, uint count)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(source);
        if (destination.Length < count * 16 || source.Length < count * 16)
            throw new ArgumentException("Source and destination must contain count matrices.");
        for (var matrix = 0; matrix < count; ++matrix)
        {
            var offset = (int)matrix * 16;
            for (var row = 0; row < 4; ++row)
                for (var column = 0; column < 4; ++column)
                    destination[offset + row * 4 + column] = source[offset + column * 4 + row];
        }
    }

    public bool ApproximatelyEquivalent(Matrix4 rhs, float epsilon = MathDefs.Epsilon) =>
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
        MathDefs.ApproximatelyEquivalent(M23, rhs.M23, epsilon) &&
        MathDefs.ApproximatelyEquivalent(M30, rhs.M30, epsilon) &&
        MathDefs.ApproximatelyEquivalent(M31, rhs.M31, epsilon) &&
        MathDefs.ApproximatelyEquivalent(M32, rhs.M32, epsilon) &&
        MathDefs.ApproximatelyEquivalent(M33, rhs.M33, epsilon);

    public bool Equals(Matrix4 other) => this == other;
    public override bool Equals(object? obj) => obj is Matrix4 other && Equals(other);
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
        hash.Add(M30);
        hash.Add(M31);
        hash.Add(M32);
        hash.Add(M33);
        return hash.ToHashCode();
    }

    public override string ToString() => string.Format(CultureInfo.InvariantCulture, "{0} {1} {2} {3} {4} {5} {6} {7} {8} {9} {10} {11} {12} {13} {14} {15}", M00, M01, M02, M03, M10, M11, M12, M13, M20, M21, M22, M23, M30, M31, M32, M33);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(Matrix4 lhs, Matrix4 rhs) =>
        lhs.M00 == rhs.M00 && lhs.M01 == rhs.M01 && lhs.M02 == rhs.M02 && lhs.M03 == rhs.M03 &&
        lhs.M10 == rhs.M10 && lhs.M11 == rhs.M11 && lhs.M12 == rhs.M12 && lhs.M13 == rhs.M13 &&
        lhs.M20 == rhs.M20 && lhs.M21 == rhs.M21 && lhs.M22 == rhs.M22 && lhs.M23 == rhs.M23 &&
        lhs.M30 == rhs.M30 && lhs.M31 == rhs.M31 && lhs.M32 == rhs.M32 && lhs.M33 == rhs.M33;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(Matrix4 lhs, Matrix4 rhs) => !(lhs == rhs);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3f operator *(Matrix4 lhs, Vector3f rhs)
    {
        var invW = 1.0f / (lhs.M30 * rhs.X + lhs.M31 * rhs.Y + lhs.M32 * rhs.Z + lhs.M33);
        return new Vector3f(
            (lhs.M00 * rhs.X + lhs.M01 * rhs.Y + lhs.M02 * rhs.Z + lhs.M03) * invW,
            (lhs.M10 * rhs.X + lhs.M11 * rhs.Y + lhs.M12 * rhs.Z + lhs.M13) * invW,
            (lhs.M20 * rhs.X + lhs.M21 * rhs.Y + lhs.M22 * rhs.Z + lhs.M23) * invW);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4f operator *(Matrix4 lhs, Vector4f rhs) => new(
        lhs.M00 * rhs.X + lhs.M01 * rhs.Y + lhs.M02 * rhs.Z + lhs.M03 * rhs.W,
        lhs.M10 * rhs.X + lhs.M11 * rhs.Y + lhs.M12 * rhs.Z + lhs.M13 * rhs.W,
        lhs.M20 * rhs.X + lhs.M21 * rhs.Y + lhs.M22 * rhs.Z + lhs.M23 * rhs.W,
        lhs.M30 * rhs.X + lhs.M31 * rhs.Y + lhs.M32 * rhs.Z + lhs.M33 * rhs.W);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Matrix4 operator +(Matrix4 lhs, Matrix4 rhs) => new(
        lhs.M00 + rhs.M00, lhs.M01 + rhs.M01, lhs.M02 + rhs.M02, lhs.M03 + rhs.M03,
        lhs.M10 + rhs.M10, lhs.M11 + rhs.M11, lhs.M12 + rhs.M12, lhs.M13 + rhs.M13,
        lhs.M20 + rhs.M20, lhs.M21 + rhs.M21, lhs.M22 + rhs.M22, lhs.M23 + rhs.M23,
        lhs.M30 + rhs.M30, lhs.M31 + rhs.M31, lhs.M32 + rhs.M32, lhs.M33 + rhs.M33);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Matrix4 operator -(Matrix4 lhs, Matrix4 rhs) => new(
        lhs.M00 - rhs.M00, lhs.M01 - rhs.M01, lhs.M02 - rhs.M02, lhs.M03 - rhs.M03,
        lhs.M10 - rhs.M10, lhs.M11 - rhs.M11, lhs.M12 - rhs.M12, lhs.M13 - rhs.M13,
        lhs.M20 - rhs.M20, lhs.M21 - rhs.M21, lhs.M22 - rhs.M22, lhs.M23 - rhs.M23,
        lhs.M30 - rhs.M30, lhs.M31 - rhs.M31, lhs.M32 - rhs.M32, lhs.M33 - rhs.M33);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Matrix4 operator *(Matrix4 lhs, float rhs) => new(
        lhs.M00 * rhs, lhs.M01 * rhs, lhs.M02 * rhs, lhs.M03 * rhs,
        lhs.M10 * rhs, lhs.M11 * rhs, lhs.M12 * rhs, lhs.M13 * rhs,
        lhs.M20 * rhs, lhs.M21 * rhs, lhs.M22 * rhs, lhs.M23 * rhs,
        lhs.M30 * rhs, lhs.M31 * rhs, lhs.M32 * rhs, lhs.M33 * rhs);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Matrix4 operator *(float lhs, Matrix4 rhs) => rhs * lhs;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
}
