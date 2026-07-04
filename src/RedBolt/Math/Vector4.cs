namespace RedBolt;

public readonly record struct Vector4(float X, float Y, float Z, float W)
{
    public static Vector4 One { get; } = new(1.0f, 1.0f, 1.0f, 1.0f);
    public static Vector4 Zero { get; } = new(0.0f, 0.0f, 0.0f, 0.0f);

    public static Vector4 operator +(Vector4 lhs, Vector4 rhs) => new(lhs.X + rhs.X, lhs.Y + rhs.Y, lhs.Z + rhs.Z, lhs.W + rhs.W);

    public static Vector4 operator -(Vector4 lhs, Vector4 rhs) => new(lhs.X - rhs.X, lhs.Y - rhs.Y, lhs.Z - rhs.Z, lhs.W - rhs.W);

    public static Vector4 operator *(Vector4 lhs, float rhs) => new(lhs.X * rhs, lhs.Y * rhs, lhs.Z * rhs, lhs.W * rhs);

    public static Vector4 operator /(Vector4 lhs, float rhs) => new(lhs.X / rhs, lhs.Y / rhs, lhs.Z / rhs, lhs.W / rhs);
}
