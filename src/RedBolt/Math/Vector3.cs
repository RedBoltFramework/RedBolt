using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace RedBolt;

[StructLayout(LayoutKind.Sequential)]
public struct Vector3 : IEquatable<Vector3>, IApproximateEquatable<Vector3>
{
    public float X;
    public float Y;
    public float Z;

    public Vector3(float x = 0.0f, float y = 0.0f, float z = 0.0f)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public Vector3(Vector2 vector, float z = 0.0f)
    {
        X = vector.X;
        Y = vector.Y;
        Z = z;
    }

    public Vector3(IntVector3 vector)
    {
        X = vector.X;
        Y = vector.Y;
        Z = vector.Z;
    }

    public static readonly Vector3 ZERO = new(0.0f, 0.0f, 0.0f);
    public static readonly Vector3 LEFT = new(-1.0f, 0.0f, 0.0f);
    public static readonly Vector3 RIGHT = new(1.0f, 0.0f, 0.0f);
    public static readonly Vector3 UP = new(0.0f, 1.0f, 0.0f);
    public static readonly Vector3 DOWN = new(0.0f, -1.0f, 0.0f);
    public static readonly Vector3 FORWARD = new(0.0f, 0.0f, 1.0f);
    public static readonly Vector3 BACK = new(0.0f, 0.0f, -1.0f);
    public static readonly Vector3 ONE = new(1.0f, 1.0f, 1.0f);

    public static Vector3 Zero => ZERO;
    public static Vector3 Left => LEFT;
    public static Vector3 Right => RIGHT;
    public static Vector3 Up => UP;
    public static Vector3 Down => DOWN;
    public static Vector3 Forward => FORWARD;
    public static Vector3 Back => BACK;
    public static Vector3 One => ONE;

    public float[] Data => [X, Y, Z];
    public bool IsNaN => float.IsNaN(X) || float.IsNaN(Y) || float.IsNaN(Z);
    public float Length => MathF.Sqrt(LengthSquared);
    public float LengthSquared => X * X + Y * Y + Z * Z;
    public Vector3 Abs => new(Math.Abs(X), Math.Abs(Y), Math.Abs(Z));

    public Vector3 Normalized
    {
        get
        {
            var lenSquared = LengthSquared;
            if (!MathDefs.Equals(lenSquared, 1.0f) && lenSquared > 0.0f)
                return this * (1.0f / MathF.Sqrt(lenSquared));
            return this;
        }
    }

    public Vector3 NormalizedOrDefault => LengthSquared < MathDefs.LargeEpsilon * MathDefs.LargeEpsilon
        ? Zero
        : this * (1.0f / Length);

    public float this[int index]
    {
        get => index switch
        {
            0 => X,
            1 => Y,
            2 => Z,
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
                default:
                    throw new IndexOutOfRangeException();
            }
        }
    }

    public static Vector3 FromXZ(Vector2 vector, float y = 0.0f) => new(vector.X, y, vector.Y);

    public void Normalize()
    {
        var lenSquared = LengthSquared;
        if (!MathDefs.Equals(lenSquared, 1.0f) && lenSquared > 0.0f)
            this *= 1.0f / MathF.Sqrt(lenSquared);
    }

    public float DotProduct(Vector3 rhs) => X * rhs.X + Y * rhs.Y + Z * rhs.Z;

    public float AbsDotProduct(Vector3 rhs) => Math.Abs(X * rhs.X) + Math.Abs(Y * rhs.Y) + Math.Abs(Z * rhs.Z);

    public float ProjectOntoAxis(Vector3 axis) => DotProduct(axis.Normalized);

    public Vector3 ProjectOntoPlane(Vector3 origin, Vector3 normal)
    {
        var delta = this - origin;
        return this - normal.Normalized * delta.ProjectOntoAxis(normal);
    }

    public Vector3 ProjectOntoLine(Vector3 from, Vector3 to, bool clamped = false)
    {
        var direction = to - from;
        var factor = (this - from).DotProduct(direction) / direction.LengthSquared;
        if (clamped)
            factor = MathDefs.Clamp(factor, 0.0f, 1.0f);
        return from + direction * factor;
    }

    public float DistanceToPoint(Vector3 point) => (this - point).Length;

    public float DistanceToPlane(Vector3 origin, Vector3 normal) => (this - origin).ProjectOntoAxis(normal);

    public Vector3 Orthogonalize(Vector3 axis) => axis.CrossProduct(this).CrossProduct(axis).Normalized;

    public Vector3 CrossProduct(Vector3 rhs) => new(
        Y * rhs.Z - Z * rhs.Y,
        Z * rhs.X - X * rhs.Z,
        X * rhs.Y - Y * rhs.X);

    public Vector3 Lerp(Vector3 rhs, float t) => this * (1.0f - t) + rhs * t;

    public float Angle(Vector3 rhs) => MathDefs.Acos(DotProduct(rhs) / (Length * rhs.Length));

    public Vector2 ToVector2() => new(X, Y);

    public IntVector2 ToIntVector2() => new((int)X, (int)Y);

    public IntVector3 ToIntVector3() => new((int)X, (int)Y, (int)Z);

    public Vector2 ToXZ() => new(X, Z);

    public Vector4 ToVector4(float w) => new(this, w);

    public bool ApproximatelyEquivalent(Vector3 rhs, float epsilon = MathDefs.Epsilon) =>
        MathDefs.ApproximatelyEquivalent(X, rhs.X, epsilon) &&
        MathDefs.ApproximatelyEquivalent(Y, rhs.Y, epsilon) &&
        MathDefs.ApproximatelyEquivalent(Z, rhs.Z, epsilon);

    public bool Equals(Vector3 other) => this == other;

    public override bool Equals(object? obj) => obj is Vector3 other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(X, Y, Z);

    public override string ToString() => string.Format(CultureInfo.InvariantCulture, "{0} {1} {2}", X, Y, Z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(Vector3 lhs, Vector3 rhs) => lhs.X == rhs.X && lhs.Y == rhs.Y && lhs.Z == rhs.Z;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(Vector3 lhs, Vector3 rhs) => !(lhs == rhs);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 operator +(Vector3 lhs, Vector3 rhs) => new(lhs.X + rhs.X, lhs.Y + rhs.Y, lhs.Z + rhs.Z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 operator -(Vector3 value) => new(-value.X, -value.Y, -value.Z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 operator -(Vector3 lhs, Vector3 rhs) => new(lhs.X - rhs.X, lhs.Y - rhs.Y, lhs.Z - rhs.Z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 operator *(Vector3 lhs, float rhs) => new(lhs.X * rhs, lhs.Y * rhs, lhs.Z * rhs);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 operator *(float lhs, Vector3 rhs) => rhs * lhs;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 operator *(Vector3 lhs, Vector3 rhs) => new(lhs.X * rhs.X, lhs.Y * rhs.Y, lhs.Z * rhs.Z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 operator /(Vector3 lhs, float rhs) => new(lhs.X / rhs, lhs.Y / rhs, lhs.Z / rhs);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 operator /(Vector3 lhs, Vector3 rhs) => new(lhs.X / rhs.X, lhs.Y / rhs.Y, lhs.Z / rhs.Z);

    public static Vector3 Lerp(Vector3 lhs, Vector3 rhs, Vector3 t) => lhs + (rhs - lhs) * t;
    public static Vector3 Min(Vector3 lhs, Vector3 rhs) => new(Math.Min(lhs.X, rhs.X), Math.Min(lhs.Y, rhs.Y), Math.Min(lhs.Z, rhs.Z));
    public static Vector3 Max(Vector3 lhs, Vector3 rhs) => new(Math.Max(lhs.X, rhs.X), Math.Max(lhs.Y, rhs.Y), Math.Max(lhs.Z, rhs.Z));
    public static Vector3 Floor(Vector3 vec) => new(MathF.Floor(vec.X), MathF.Floor(vec.Y), MathF.Floor(vec.Z));
    public static Vector3 Round(Vector3 vec) => new(MathF.Round(vec.X), MathF.Round(vec.Y), MathF.Round(vec.Z));
    public static Vector3 Ceil(Vector3 vec) => new(MathF.Ceiling(vec.X), MathF.Ceiling(vec.Y), MathF.Ceiling(vec.Z));
    public static IntVector3 FloorToInt(Vector3 vec) => new((int)MathF.Floor(vec.X), (int)MathF.Floor(vec.Y), (int)MathF.Floor(vec.Z));
    public static IntVector3 RoundToInt(Vector3 vec) => new((int)MathF.Round(vec.X), (int)MathF.Round(vec.Y), (int)MathF.Round(vec.Z));
    public static IntVector3 CeilToInt(Vector3 vec) => new((int)MathF.Ceiling(vec.X), (int)MathF.Ceiling(vec.Y), (int)MathF.Ceiling(vec.Z));
}
