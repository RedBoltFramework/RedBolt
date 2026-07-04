namespace RedBolt;

public static class MathDefs
{
    public const float DegToRad = MathF.PI / 180.0f;
    public const float Epsilon = 0.000001f;
    public const float LargeEpsilon = 0.00005f;
    public const float MHalfPi = MathF.PI * 0.5f;
    public const float MPi = MathF.PI;
    public const float MTwoPi = MathF.PI * 2.0f;
    public const float RadToDeg = 180.0f / MathF.PI;

    public static float Clamp(float value, float min, float max)
    {
        return Math.Min(Math.Max(value, min), max);
    }

    public static bool Equals(float lhs, float rhs, float epsilon = Epsilon)
    {
        return Math.Abs(lhs - rhs) <= epsilon;
    }
}
