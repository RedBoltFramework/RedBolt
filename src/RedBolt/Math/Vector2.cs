namespace RedBolt;

public readonly record struct Vector2(float X, float Y)
{
    public static Vector2 Down { get; } = new(0.0f, -1.0f);
    public static Vector2 Left { get; } = new(-1.0f, 0.0f);
    public static Vector2 One { get; } = new(1.0f, 1.0f);
    public static Vector2 Right { get; } = new(1.0f, 0.0f);
    public static Vector2 Up { get; } = new(0.0f, 1.0f);
    public static Vector2 Zero { get; } = new(0.0f, 0.0f);

    public float Length => MathF.Sqrt(LengthSquared);

    public float LengthSquared => X * X + Y * Y;

    public Vector2 Normalized => LengthSquared > MathDefs.Epsilon ? this / Length : Zero;

    public static Vector2 operator +(Vector2 lhs, Vector2 rhs) => new(lhs.X + rhs.X, lhs.Y + rhs.Y);

    public static Vector2 operator -(Vector2 lhs, Vector2 rhs) => new(lhs.X - rhs.X, lhs.Y - rhs.Y);

    public static Vector2 operator -(Vector2 value) => new(-value.X, -value.Y);

    public static Vector2 operator *(Vector2 lhs, float rhs) => new(lhs.X * rhs, lhs.Y * rhs);

    public static Vector2 operator *(float lhs, Vector2 rhs) => rhs * lhs;

    public static Vector2 operator /(Vector2 lhs, float rhs) => new(lhs.X / rhs, lhs.Y / rhs);

    public float DotProduct(Vector2 rhs) => X * rhs.X + Y * rhs.Y;
}
