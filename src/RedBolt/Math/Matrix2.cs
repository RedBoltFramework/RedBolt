using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace RedBolt;

[StructLayout(LayoutKind.Sequential)]
public struct Matrix2 : IEquatable<Matrix2>, IApproximateEquatable<Matrix2>
{
    /// <summary>Construct an identity matrix.</summary>
    public Matrix2()
        : this(1.0f, 0.0f, 0.0f, 1.0f)
    {
    }

    /// <summary>Construct from values.</summary>
    public Matrix2(float v00, float v01, float v10, float v11)
    {
        M00 = v00;
        M01 = v01;
        M10 = v10;
        M11 = v11;
    }

    /// <summary>Construct from a float array.</summary>
    public Matrix2(ReadOnlySpan<float> data)
    {
        M00 = data[0];
        M01 = data[1];
        M10 = data[2];
        M11 = data[3];
    }

    /// <summary>Test for equality with another matrix without epsilon.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(Matrix2 lhs, Matrix2 rhs) => lhs.M00 == rhs.M00 && lhs.M01 == rhs.M01 && lhs.M10 == rhs.M10 && lhs.M11 == rhs.M11;

    /// <summary>Test for inequality with another matrix without epsilon.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(Matrix2 lhs, Matrix2 rhs) => !(lhs == rhs);

    /// <summary>Multiply a Vector2.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2f operator *(Matrix2 lhs, Vector2f rhs) => new(
        lhs.M00 * rhs.X + lhs.M01 * rhs.Y,
        lhs.M10 * rhs.X + lhs.M11 * rhs.Y);

    /// <summary>Add a matrix.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Matrix2 operator +(Matrix2 lhs, Matrix2 rhs) => new(
        lhs.M00 + rhs.M00,
        lhs.M01 + rhs.M01,
        lhs.M10 + rhs.M10,
        lhs.M11 + rhs.M11);

    /// <summary>Subtract a matrix.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Matrix2 operator -(Matrix2 lhs, Matrix2 rhs) => new(
        lhs.M00 - rhs.M00,
        lhs.M01 - rhs.M01,
        lhs.M10 - rhs.M10,
        lhs.M11 - rhs.M11);

    /// <summary>Multiply with a scalar.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Matrix2 operator *(Matrix2 lhs, float rhs) => new(
        lhs.M00 * rhs,
        lhs.M01 * rhs,
        lhs.M10 * rhs,
        lhs.M11 * rhs);

    /// <summary>Multiply a matrix.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Matrix2 operator *(Matrix2 lhs, Matrix2 rhs) => new(
        lhs.M00 * rhs.M00 + lhs.M01 * rhs.M10,
        lhs.M00 * rhs.M01 + lhs.M01 * rhs.M11,
        lhs.M10 * rhs.M00 + lhs.M11 * rhs.M10,
        lhs.M10 * rhs.M01 + lhs.M11 * rhs.M11);

    /// <summary>Set scaling elements.</summary>
    public void SetScale(Vector2f scale)
    {
        M00 = scale.X;
        M11 = scale.Y;
    }

    /// <summary>Set uniform scaling elements.</summary>
    public void SetScale(float scale)
    {
        M00 = scale;
        M11 = scale;
    }

    /// <summary>Return the scaling part.</summary>
    public readonly Vector2f Scale => new(
        MathF.Sqrt(M00 * M00 + M10 * M10),
        MathF.Sqrt(M01 * M01 + M11 * M11));

    /// <summary>Return transpose.</summary>
    public readonly Matrix2 Transpose => new(M00, M10, M01, M11);

    /// <summary>Return scaled by a vector.</summary>
    public readonly Matrix2 Scaled(Vector2f scale) => new(
        M00 * scale.X,
        M01 * scale.Y,
        M10 * scale.X,
        M11 * scale.Y);

    /// <summary>Test for equality with another matrix with epsilon.</summary>
    public readonly bool ApproximatelyEquivalent(Matrix2 rhs, float epsilon = MathDefs.Epsilon) =>
        MathDefs.Equals(M00, rhs.M00, epsilon) &&
        MathDefs.Equals(M01, rhs.M01, epsilon) &&
        MathDefs.Equals(M10, rhs.M10, epsilon) &&
        MathDefs.Equals(M11, rhs.M11, epsilon);

    /// <summary>Return inverse.</summary>
    public readonly Matrix2 Inverse
    {
        get
        {
            var determinant = M00 * M11 - M01 * M10;
            var invDeterminant = 1.0f / determinant;
            return new Matrix2(M11, -M01, -M10, M00) * invDeterminant;
        }
    }

    /// <summary>Return float data.</summary>
    public readonly ReadOnlySpan<float> Data => MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(in M00), 4);

    /// <summary>Return whether any element is NaN.</summary>
    public readonly bool IsNaN => float.IsNaN(M00) || float.IsNaN(M01) || float.IsNaN(M10) || float.IsNaN(M11);

    /// <summary>Return whether any element is Inf.</summary>
    public readonly bool IsInf => float.IsInfinity(M00) || float.IsInfinity(M01) || float.IsInfinity(M10) || float.IsInfinity(M11);

    /// <summary>Return as string.</summary>
    public override readonly string ToString() => string.Format(CultureInfo.InvariantCulture, "{0} {1} {2} {3}", M00, M01, M10, M11);

    public float M00;
    public float M01;
    public float M10;
    public float M11;

    /// <summary>Bulk transpose matrices.</summary>
    public static void BulkTranspose(Span<float> destination, ReadOnlySpan<float> source, int count)
    {
        for (var i = 0; i < count; ++i)
        {
            var offset = i * 4;
            destination[offset] = source[offset];
            destination[offset + 1] = source[offset + 2];
            destination[offset + 2] = source[offset + 1];
            destination[offset + 3] = source[offset + 3];
        }
    }

    /// <summary>Zero matrix.</summary>
    public static readonly Matrix2 Zero = new(0.0f, 0.0f, 0.0f, 0.0f);

    /// <summary>Identity matrix.</summary>
    public static readonly Matrix2 Identity = new();

    /// <summary>Multiply a 2x2 matrix with a scalar.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Matrix2 operator *(float lhs, Matrix2 rhs) => rhs * lhs;

    public readonly bool Equals(Matrix2 other) => this == other;
    public override readonly bool Equals(object? obj) => obj is Matrix2 other && Equals(other);
    public override readonly int GetHashCode() => HashCode.Combine(M00, M01, M10, M11);
}
