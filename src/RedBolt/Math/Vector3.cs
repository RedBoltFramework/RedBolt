namespace RedBolt;

public readonly record struct Vector3(float X, float Y, float Z)
{
    public static Vector3 Back { get; } = new(0.0f, 0.0f, -1.0f);
    public static Vector3 Down { get; } = new(0.0f, -1.0f, 0.0f);
    public static Vector3 Forward { get; } = new(0.0f, 0.0f, 1.0f);
    public static Vector3 Left { get; } = new(-1.0f, 0.0f, 0.0f);
    public static Vector3 One { get; } = new(1.0f, 1.0f, 1.0f);
    public static Vector3 Right { get; } = new(1.0f, 0.0f, 0.0f);
    public static Vector3 Up { get; } = new(0.0f, 1.0f, 0.0f);
    public static Vector3 Zero { get; } = new(0.0f, 0.0f, 0.0f);

    public float Length => MathF.Sqrt(LengthSquared);

    public float LengthSquared => X * X + Y * Y + Z * Z;

    public Vector3 Normalized => LengthSquared > MathDefs.Epsilon ? this / Length : Zero;

    public static Vector3 FromXZ(Vector2 vector, float y = 0.0f) => new(vector.X, y, vector.Y);

    public static Vector3 operator +(Vector3 lhs, Vector3 rhs) => new(lhs.X + rhs.X, lhs.Y + rhs.Y, lhs.Z + rhs.Z);

    public static Vector3 operator -(Vector3 lhs, Vector3 rhs) => new(lhs.X - rhs.X, lhs.Y - rhs.Y, lhs.Z - rhs.Z);

    public static Vector3 operator -(Vector3 value) => new(-value.X, -value.Y, -value.Z);

    public static Vector3 operator *(Vector3 lhs, float rhs) => new(lhs.X * rhs, lhs.Y * rhs, lhs.Z * rhs);

    public static Vector3 operator *(float lhs, Vector3 rhs) => rhs * lhs;

    public static Vector3 operator /(Vector3 lhs, float rhs) => new(lhs.X / rhs, lhs.Y / rhs, lhs.Z / rhs);

    public Vector3 CrossProduct(Vector3 rhs)
    {
        return new Vector3(
            Y * rhs.Z - Z * rhs.Y,
            Z * rhs.X - X * rhs.Z,
            X * rhs.Y - Y * rhs.X);
    }

    public float DistanceToPoint(Vector3 point) => (this - point).Length;

    public float DotProduct(Vector3 rhs) => X * rhs.X + Y * rhs.Y + Z * rhs.Z;
}
