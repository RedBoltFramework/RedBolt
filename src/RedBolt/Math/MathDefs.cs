namespace RedBolt;

public static partial class MathDefs
{
    public const float Pi = MathF.PI;
    public const float HalfPi = Pi / 2.0f;
    public const float PiOver2 = Pi / 2.0f;
    public const float PiOver3 = Pi / 3.0f;
    public const float PiOver4 = Pi / 4.0f;
    public const float PiOver6 = Pi / 6.0f;
    public const float TwoPi = 2.0f * Pi;
    public const float ThreePiOver2 = 3.0f * Pi / 2.0f;
    public const float E = 2.71828182845904523536f;
    public const float Log10E = 0.434294482f;
    public const float Log2E = 1.442695041f;
    public const float Epsilon = 0.000001f;
    public const float LargeEpsilon = 0.00005f;
    public const float MinNearClip = 0.01f;

    public const float DegToRad = Pi / 180.0f;
    public const float RadToDeg = 180.0f / Pi;
    public const float MHalfPi = HalfPi;
    public const float MPi = Pi;
    public const float MTwoPi = TwoPi;

    public static float Acos(float value) => MathF.Acos(Clamp(value, -1.0f, 1.0f)) * RadToDeg;

    public static float Clamp(float value, float min, float max) => Math.Min(Math.Max(value, min), max);

    public static bool Equals(float lhs, float rhs, float epsilon = Epsilon) => Math.Abs(lhs - rhs) <= epsilon;

    public static bool ApproximatelyEquivalent(float lhs, float rhs, float epsilon = Epsilon) => Math.Abs(lhs - rhs) <= epsilon;

    public static float DegreesToRadians(float degrees) => degrees * DegToRad;

    public static float DegreesToRadians2(float degrees) => degrees * DegToRad * 0.5f;

    public static float Fract(float value) => value - MathF.Floor(value);

    public static float Lerp(float lhs, float rhs, float t) => lhs * (1.0f - t) + rhs * t;

    public static float RadiansToDegrees(float radians) => radians * RadToDeg;
}
