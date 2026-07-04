using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace RedBolt;

[StructLayout(LayoutKind.Sequential)]
public struct Vector2 : IEquatable<Vector2>, IApproximateEquatable<Vector2>
{
    public float X;
    public float Y;

    public Vector2(float x, float y)
    {
        X = x;
        Y = y;
    }

    public Vector2(IntVector2 vector)
    {
        X = vector.X;
        Y = vector.Y;
    }

    public static readonly Vector2 ZERO = new(0.0f, 0.0f);
    public static readonly Vector2 LEFT = new(-1.0f, 0.0f);
    public static readonly Vector2 RIGHT = new(1.0f, 0.0f);
    public static readonly Vector2 UP = new(0.0f, 1.0f);
    public static readonly Vector2 DOWN = new(0.0f, -1.0f);
    public static readonly Vector2 ONE = new(1.0f, 1.0f);

    public static Vector2 Zero => ZERO;
    public static Vector2 Left => LEFT;
    public static Vector2 Right => RIGHT;
    public static Vector2 Up => UP;
    public static Vector2 Down => DOWN;
    public static Vector2 One => ONE;

    public float[] Data => [X, Y];
    public bool IsNaN => float.IsNaN(X) || float.IsNaN(Y);
    public float Length => MathF.Sqrt(LengthSquared);
    public float LengthSquared => X * X + Y * Y;
    public Vector2 Abs => new(Math.Abs(X), Math.Abs(Y));

    public Vector2 Normalized
    {
        get
        {
            var lenSquared = LengthSquared;
            if (!MathDefs.Equals(lenSquared, 1.0f) && lenSquared > 0.0f)
                return this * (1.0f / MathF.Sqrt(lenSquared));
            return this;
        }
    }

    public Vector2 NormalizedOrDefault => LengthSquared < MathDefs.LargeEpsilon * MathDefs.LargeEpsilon
        ? Zero
        : this * (1.0f / Length);

    public float this[int index]
    {
        get => index switch
        {
            0 => X,
            1 => Y,
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
                default:
                    throw new IndexOutOfRangeException();
            }
        }
    }

    public void Normalize()
    {
        var lenSquared = LengthSquared;
        if (!MathDefs.Equals(lenSquared, 1.0f) && lenSquared > 0.0f)
            this *= 1.0f / MathF.Sqrt(lenSquared);
    }

    public float DotProduct(Vector2 rhs) => X * rhs.X + Y * rhs.Y;

    public float AbsDotProduct(Vector2 rhs) => Math.Abs(X * rhs.X) + Math.Abs(Y * rhs.Y);

    public float ProjectOntoAxis(Vector2 axis) => DotProduct(axis.Normalized);

    public float Angle(Vector2 rhs) => MathF.Acos(DotProduct(rhs) / (Length * rhs.Length)) * MathDefs.RadToDeg;

    public Vector2 Lerp(Vector2 rhs, float t) => this * (1.0f - t) + rhs * t;

    public bool ApproximatelyEquivalent(Vector2 rhs, float epsilon = MathDefs.Epsilon) =>
        MathDefs.ApproximatelyEquivalent(X, rhs.X, epsilon) &&
        MathDefs.ApproximatelyEquivalent(Y, rhs.Y, epsilon);

    public bool Equals(Vector2 other) => this == other;

    public override bool Equals(object? obj) => obj is Vector2 other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(X, Y);

    public override string ToString() => string.Format(CultureInfo.InvariantCulture, "{0} {1}", X, Y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(Vector2 lhs, Vector2 rhs) => lhs.X == rhs.X && lhs.Y == rhs.Y;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(Vector2 lhs, Vector2 rhs) => !(lhs == rhs);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2 operator +(Vector2 lhs, Vector2 rhs) => new(lhs.X + rhs.X, lhs.Y + rhs.Y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2 operator -(Vector2 value) => new(-value.X, -value.Y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2 operator -(Vector2 lhs, Vector2 rhs) => new(lhs.X - rhs.X, lhs.Y - rhs.Y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2 operator *(Vector2 lhs, float rhs) => new(lhs.X * rhs, lhs.Y * rhs);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2 operator *(float lhs, Vector2 rhs) => rhs * lhs;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2 operator *(Vector2 lhs, Vector2 rhs) => new(lhs.X * rhs.X, lhs.Y * rhs.Y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2 operator /(Vector2 lhs, float rhs) => new(lhs.X / rhs, lhs.Y / rhs);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2 operator /(Vector2 lhs, Vector2 rhs) => new(lhs.X / rhs.X, lhs.Y / rhs.Y);

    public static Vector2 Min(Vector2 lhs, Vector2 rhs) => new(Math.Min(lhs.X, rhs.X), Math.Min(lhs.Y, rhs.Y));
    public static Vector2 Max(Vector2 lhs, Vector2 rhs) => new(Math.Max(lhs.X, rhs.X), Math.Max(lhs.Y, rhs.Y));
    public static Vector2 Floor(Vector2 vec) => new(MathF.Floor(vec.X), MathF.Floor(vec.Y));
    public static Vector2 Round(Vector2 vec) => new(MathF.Round(vec.X), MathF.Round(vec.Y));
    public static Vector2 Ceil(Vector2 vec) => new(MathF.Ceiling(vec.X), MathF.Ceiling(vec.Y));
    public static IntVector2 FloorToInt(Vector2 vec) => new((int)MathF.Floor(vec.X), (int)MathF.Floor(vec.Y));
    public static IntVector2 RoundToInt(Vector2 vec) => new((int)MathF.Round(vec.X), (int)MathF.Round(vec.Y));
    public static IntVector2 CeilToInt(Vector2 vec) => new((int)MathF.Ceiling(vec.X), (int)MathF.Ceiling(vec.Y));
}
