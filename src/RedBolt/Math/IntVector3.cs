using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace RedBolt;

[StructLayout(LayoutKind.Sequential)]
public struct IntVector3 : IEquatable<IntVector3>
{
    public int X;
    public int Y;
    public int Z;

    public IntVector3(int x = 0, int y = 0, int z = 0)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public static readonly IntVector3 Zero = new(0, 0, 0);
    public static readonly IntVector3 Left = new(-1, 0, 0);
    public static readonly IntVector3 Right = new(1, 0, 0);
    public static readonly IntVector3 Up = new(0, 1, 0);
    public static readonly IntVector3 Down = new(0, -1, 0);
    public static readonly IntVector3 Forward = new(0, 0, 1);
    public static readonly IntVector3 Back = new(0, 0, -1);
    public static readonly IntVector3 One = new(1, 1, 1);

    public int[] Data => [X, Y, Z];

    public float Length => MathF.Sqrt(X * X + Y * Y + Z * Z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(IntVector3 lhs, IntVector3 rhs) => lhs.X == rhs.X && lhs.Y == rhs.Y && lhs.Z == rhs.Z;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(IntVector3 lhs, IntVector3 rhs) => !(lhs == rhs);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IntVector3 operator +(IntVector3 lhs, IntVector3 rhs) => new(lhs.X + rhs.X, lhs.Y + rhs.Y, lhs.Z + rhs.Z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IntVector3 operator -(IntVector3 value) => new(-value.X, -value.Y, -value.Z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IntVector3 operator -(IntVector3 lhs, IntVector3 rhs) => new(lhs.X - rhs.X, lhs.Y - rhs.Y, lhs.Z - rhs.Z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IntVector3 operator *(IntVector3 lhs, int rhs) => new(lhs.X * rhs, lhs.Y * rhs, lhs.Z * rhs);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IntVector3 operator *(int lhs, IntVector3 rhs) => rhs * lhs;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IntVector3 operator *(IntVector3 lhs, IntVector3 rhs) => new(lhs.X * rhs.X, lhs.Y * rhs.Y, lhs.Z * rhs.Z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IntVector3 operator /(IntVector3 lhs, int rhs) => new(lhs.X / rhs, lhs.Y / rhs, lhs.Z / rhs);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IntVector3 operator /(IntVector3 lhs, IntVector3 rhs) => new(lhs.X / rhs.X, lhs.Y / rhs.Y, lhs.Z / rhs.Z);

    public static IntVector3 Min(IntVector3 lhs, IntVector3 rhs) => new(Math.Min(lhs.X, rhs.X), Math.Min(lhs.Y, rhs.Y), Math.Min(lhs.Z, rhs.Z));

    public static IntVector3 Max(IntVector3 lhs, IntVector3 rhs) => new(Math.Max(lhs.X, rhs.X), Math.Max(lhs.Y, rhs.Y), Math.Max(lhs.Z, rhs.Z));

    public Vector2 ToVector2() => new(X, Y);

    public IntVector2 ToIntVector2() => new(X, Y);

    public Vector3 ToVector3() => new(X, Y, Z);

    public bool Equals(IntVector3 other) => this == other;

    public override bool Equals(object? obj) => obj is IntVector3 other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(X, Y, Z);

    public override string ToString() => string.Format(CultureInfo.InvariantCulture, "{0} {1} {2}", X, Y, Z);
}
