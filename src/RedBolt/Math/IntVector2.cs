namespace RedBolt;

public readonly record struct IntVector2(int X, int Y)
{
    public static IntVector2 Down { get; } = new(0, -1);
    public static IntVector2 Left { get; } = new(-1, 0);
    public static IntVector2 One { get; } = new(1, 1);
    public static IntVector2 Right { get; } = new(1, 0);
    public static IntVector2 Up { get; } = new(0, 1);
    public static IntVector2 Zero { get; } = new(0, 0);

    public static IntVector2 operator +(IntVector2 lhs, IntVector2 rhs) => new(lhs.X + rhs.X, lhs.Y + rhs.Y);

    public static IntVector2 operator -(IntVector2 lhs, IntVector2 rhs) => new(lhs.X - rhs.X, lhs.Y - rhs.Y);
}
