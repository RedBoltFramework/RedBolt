using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace RedBolt;

[StructLayout(LayoutKind.Sequential)]
public struct IntVector2 : IEquatable<IntVector2>
{
    public int X;
    public int Y;

    public IntVector2(int x = 0, int y = 0)
    {
        X = x;
        Y = y;
    }

    public static readonly IntVector2 Zero = new(0, 0);
    public static readonly IntVector2 Left = new(-1, 0);
    public static readonly IntVector2 Right = new(1, 0);
    public static readonly IntVector2 Up = new(0, 1);
    public static readonly IntVector2 Down = new(0, -1);
    public static readonly IntVector2 One = new(1, 1);

    public int[] Data => [X, Y];

    public float Length => MathF.Sqrt(X * X + Y * Y);

    public int this[int index]
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(IntVector2 lhs, IntVector2 rhs) => lhs.X == rhs.X && lhs.Y == rhs.Y;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(IntVector2 lhs, IntVector2 rhs) => !(lhs == rhs);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IntVector2 operator +(IntVector2 lhs, IntVector2 rhs) => new(lhs.X + rhs.X, lhs.Y + rhs.Y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IntVector2 operator -(IntVector2 value) => new(-value.X, -value.Y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IntVector2 operator -(IntVector2 lhs, IntVector2 rhs) => new(lhs.X - rhs.X, lhs.Y - rhs.Y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IntVector2 operator *(IntVector2 lhs, int rhs) => new(lhs.X * rhs, lhs.Y * rhs);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IntVector2 operator *(int lhs, IntVector2 rhs) => rhs * lhs;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IntVector2 operator *(IntVector2 lhs, IntVector2 rhs) => new(lhs.X * rhs.X, lhs.Y * rhs.Y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IntVector2 operator /(IntVector2 lhs, int rhs) => new(lhs.X / rhs, lhs.Y / rhs);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IntVector2 operator /(IntVector2 lhs, IntVector2 rhs) => new(lhs.X / rhs.X, lhs.Y / rhs.Y);

    public static IntVector2 Min(IntVector2 lhs, IntVector2 rhs) => new(Math.Min(lhs.X, rhs.X), Math.Min(lhs.Y, rhs.Y));

    public static IntVector2 Max(IntVector2 lhs, IntVector2 rhs) => new(Math.Max(lhs.X, rhs.X), Math.Max(lhs.Y, rhs.Y));

    public Vector2 ToVector2() => new(X, Y);

    public Vector3 ToVector3() => new(X, Y, 0.0f);

    public IntVector3 ToIntVector3() => new(X, Y, 0);

    public bool Equals(IntVector2 other) => this == other;

    public override bool Equals(object? obj) => obj is IntVector2 other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(X, Y);

    public override string ToString() => string.Format(CultureInfo.InvariantCulture, "{0} {1}", X, Y);
}
