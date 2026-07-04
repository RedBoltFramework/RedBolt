namespace RedBolt;

public readonly record struct Quaternion
{
    public Quaternion(float w = 1.0f, float x = 0.0f, float y = 0.0f, float z = 0.0f)
    {
        W = w;
        X = x;
        Y = y;
        Z = z;
    }

    public Quaternion(float angle, Vector3 axis)
    {
        this = CreateFromAngleAxis(angle, axis);
    }

    public Quaternion(Vector3 eulerAngles)
    {
        this = CreateFromEulerAngles(eulerAngles.X, eulerAngles.Y, eulerAngles.Z);
    }

    public static Quaternion Identity { get; } = new(1.0f, 0.0f, 0.0f, 0.0f);

    public float W { get; }

    public float X { get; }

    public float Y { get; }

    public float Z { get; }

    public static Quaternion operator *(Quaternion lhs, Quaternion rhs)
    {
        return new Quaternion(
            lhs.W * rhs.W - lhs.X * rhs.X - lhs.Y * rhs.Y - lhs.Z * rhs.Z,
            lhs.W * rhs.X + lhs.X * rhs.W + lhs.Y * rhs.Z - lhs.Z * rhs.Y,
            lhs.W * rhs.Y + lhs.Y * rhs.W + lhs.Z * rhs.X - lhs.X * rhs.Z,
            lhs.W * rhs.Z + lhs.Z * rhs.W + lhs.X * rhs.Y - lhs.Y * rhs.X);
    }

    public static Vector3 operator *(Quaternion lhs, Vector3 rhs)
    {
        var qVec = new Vector3(lhs.X, lhs.Y, lhs.Z);
        var cross1 = qVec.CrossProduct(rhs);
        var cross2 = qVec.CrossProduct(cross1);
        return rhs + 2.0f * (cross1 * lhs.W + cross2);
    }

    public static Quaternion FromAngleAxis(float angle, Vector3 axis) => CreateFromAngleAxis(angle, axis);

    public static Quaternion FromEulerAngles(float x, float y, float z) => CreateFromEulerAngles(x, y, z);

    private static Quaternion CreateFromAngleAxis(float angle, Vector3 axis)
    {
        var normalizedAxis = axis.Normalized;
        var halfAngle = angle * MathDefs.DegToRad * 0.5f;
        var sin = MathF.Sin(halfAngle);
        return new Quaternion(MathF.Cos(halfAngle), normalizedAxis.X * sin, normalizedAxis.Y * sin, normalizedAxis.Z * sin);
    }

    private static Quaternion CreateFromEulerAngles(float x, float y, float z)
    {
        var qx = CreateFromAngleAxis(x, Vector3.Right);
        var qy = CreateFromAngleAxis(y, Vector3.Up);
        var qz = CreateFromAngleAxis(z, Vector3.Forward);
        return qy * qx * qz;
    }
}
