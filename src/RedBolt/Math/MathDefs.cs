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
    public const float MaxFov = 160.0f;
    public const float LargeValue = 100000000.0f;
    public const float Infinity = float.PositiveInfinity;

    public const int MinInt = int.MinValue;
    public const int MaxInt = int.MaxValue;
    public const uint MinUnsigned = uint.MinValue;
    public const uint MaxUnsigned = uint.MaxValue;
    public const short MinShort = short.MinValue;
    public const short MaxShort = short.MaxValue;

    public const float DegToRad = Pi / 180.0f;
    public const float DegToRad2 = Pi / 360.0f;
    public const float RadToDeg = 180.0f / Pi;
    public const float MHalfPi = HalfPi;
    public const float MPi = Pi;
    public const float MTwoPi = TwoPi;

    public static float Acos(float value) => MathF.Acos(Clamp(value, -1.0f, 1.0f)) * RadToDeg;

    public static float Asin(float value) => MathF.Asin(Clamp(value, -1.0f, 1.0f)) * RadToDeg;

    public static float Atan(float value) => MathF.Atan(value) * RadToDeg;

    public static float Atan2(float y, float x) => MathF.Atan2(y, x) * RadToDeg;

    public static float Clamp(float value, float min, float max) => Math.Min(Math.Max(value, min), max);

    public static uint ClosestPowerOfTwo(uint value)
    {
        var next = NextPowerOfTwo(value);
        var previous = next >> 1;
        return value - previous > next - value ? next : previous;
    }

    public static uint CountSetBits(uint value)
    {
        var count = 0u;
        while (value != 0)
        {
            value &= value - 1u;
            ++count;
        }

        return count;
    }

    public static float Cos(float angle) => MathF.Cos(angle * DegToRad);

    public static bool Equals(float lhs, float rhs, float epsilon = Epsilon) => Math.Abs(lhs - rhs) <= epsilon;

    public static bool ApproximatelyEquivalent(float lhs, float rhs, float epsilon = Epsilon) => Math.Abs(lhs - rhs) <= epsilon;

    public static float ExponentialDecay(float value) => Clamp(MathF.Pow(2.0f, -value), 0.0f, 1.0f);

    public static float DegreesToRadians(float degrees) => degrees * DegToRad;

    public static float DegreesToRadians2(float degrees) => degrees * DegToRad * 0.5f;

    public static float Floor(float value) => MathF.Floor(value);

    public static int FloorToInt(float value) => (int)MathF.Floor(value);

    public static ushort FloatToHalf(float value)
    {
        unchecked
        {
            var bits = BitConverter.SingleToUInt32Bits(value);
            var t1 = bits & 0x7fffffffu;
            var t2 = bits & 0x80000000u;
            var t3 = bits & 0x7f800000u;

            t1 >>= 13;
            t2 >>= 16;
            t1 -= 0x1c000u;
            t1 = t3 < 0x38800000u ? 0u : t1;
            t1 = t3 > 0x47000000u ? 0x7bffu : t1;
            t1 = t3 == 0u ? 0u : t1;
            return (ushort)(t1 | t2);
        }
    }

    public static float Fract(float value) => value - MathF.Floor(value);

    public static float HalfToFloat(ushort value)
    {
        var t1 = (uint)(value & 0x7fffu);
        var t2 = (uint)(value & 0x8000u);
        var t3 = (uint)(value & 0x7c00u);

        t1 <<= 13;
        t2 <<= 16;
        t1 += 0x38000000u;
        t1 = t3 == 0u ? 0u : t1;
        t1 |= t2;
        return BitConverter.UInt32BitsToSingle(t1);
    }

    public static float InverseExponentialDecay(float value) => 1.0f - ExponentialDecay(value);

    public static float InverseLerp(float lhs, float rhs, float value) => (value - lhs) / (rhs - lhs);

    public static bool IsInf(float value) => float.IsInfinity(value);

    public static bool IsNaN(float value) => float.IsNaN(value);

    public static bool IsPowerOfTwo(uint value) => value != 0 && (value & (value - 1u)) == 0;

    public static float Lerp(float lhs, float rhs, float t) => lhs * (1.0f - t) + rhs * t;

    public static uint LogBaseTwo(uint value)
    {
        var result = 0u;
        while ((value >>= 1) != 0)
            ++result;
        return result;
    }

    public static float Ln(float value) => MathF.Log(value);

    public static float Mod(float x, float y) => x % y;

    public static int Mod(int x, int y) => x % y;

    public static float AbsMod(float x, float y)
    {
        var result = Mod(x, y);
        return result < 0.0f ? result + y : result;
    }

    public static int AbsMod(int x, int y)
    {
        var result = Mod(x, y);
        return result < 0 ? result + y : result;
    }

    public static uint NextPowerOfTwo(uint value)
    {
        if (value == 0)
            return 0;

        --value;
        value |= value >> 1;
        value |= value >> 2;
        value |= value >> 4;
        value |= value >> 8;
        value |= value >> 16;
        return value + 1;
    }

    public static float Pow(float x, float y) => MathF.Pow(x, y);

    public static float RadiansToDegrees(float radians) => radians * RadToDeg;

    public static float Round(float value) => MathF.Round(value, MidpointRounding.AwayFromZero);

    public static int RoundToInt(float value) => (int)Round(value);

    public static float RoundToNearestMultiple(float value, float multiple)
    {
        var magnitude = Math.Abs(value);
        multiple = Math.Abs(multiple);
        var remainder = Mod(magnitude, multiple);
        var rounded = remainder >= multiple / 2.0f
            ? FloorToInt(magnitude / multiple) * multiple + multiple
            : FloorToInt(magnitude / multiple) * multiple;
        return rounded * Sign(value);
    }

    public static uint SDBMHash(uint hash, byte value) => value + (hash << 6) + (hash << 16) - hash;

    public static float Sign(float value) => value > 0.0f ? 1.0f : value < 0.0f ? -1.0f : 0.0f;

    public static float Sin(float angle) => MathF.Sin(angle * DegToRad);

    public static float Smooth(float lhs, float rhs, float t) => Lerp(lhs, rhs, InverseExponentialDecay(t));

    public static float Smooth(float lhs, float rhs, float halfTime, float timeStep) => halfTime != 0.0f ? Smooth(lhs, rhs, timeStep / halfTime) : rhs;

    public static float SmoothStep(float lhs, float rhs, float t)
    {
        t = Clamp((t - lhs) / (rhs - lhs), 0.0f, 1.0f);
        return t * t * (3.0f - 2.0f * t);
    }

    public static float SnapCeil(float value, float step) => MathF.Ceiling(value / step) * step;

    public static float SnapFloor(float value, float step) => MathF.Floor(value / step) * step;

    public static float SnapRound(float value, float step) => Round(value / step) * step;

    public static float SnapTo(float value, float target, float epsilon = Epsilon) => Equals(value, target, epsilon) ? target : value;

    public static float Sqrt(float value) => MathF.Sqrt(value);

    public static float Tan(float angle) => MathF.Tan(angle * DegToRad);

    public static float Wrap(float value, float min, float max) => min + Mod(value, max - min);

    public static int Wrap(int value, int min, int max) => min + Mod(value, max - min);

    public static float Ceil(float value) => MathF.Ceiling(value);

    public static int CeilToInt(float value) => (int)MathF.Ceiling(value);
}
