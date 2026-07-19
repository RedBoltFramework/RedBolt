using System.Runtime.CompilerServices;

namespace RedBolt;

public static partial class MathDefs
{
    public const float Pi = MathF.PI;
    public const float HalfPi = Pi * 0.5f;
    public const int MinInt = int.MinValue;
    public const int MaxInt = int.MaxValue;
    public const uint MinUnsigned = uint.MinValue;
    public const uint MaxUnsigned = uint.MaxValue;
    public const int MinShort = 0x8000;
    public const int MaxShort = 0x7fff;

    public const float Epsilon = 0.000001f;
    public const float LargeEpsilon = 0.00005f;
    public const float MinNearClip = 0.01f;
    public const float MaxFov = 160.0f;
    public const float LargeValue = 100000000.0f;
    public const float Infinity = float.PositiveInfinity;
    public const float DegToRad = Pi / 180.0f;
    public const float DegToRad2 = Pi / 360.0f;
    public const float RadToDeg = 1.0f / DegToRad;

    /// <summary>Check whether two floating point values are equal within accuracy.</summary>
    public static bool Equals(float lhs, float rhs, float epsilon = Epsilon) =>
        lhs + epsilon >= rhs && lhs - epsilon <= rhs;

    /// <summary>Check whether two floating point values are equal within accuracy.</summary>
    public static bool Equals(double lhs, double rhs, double epsilon = Epsilon) =>
        lhs + epsilon >= rhs && lhs - epsilon <= rhs;

    /// <summary>Linear interpolation between two values.</summary>
    public static float Lerp(float lhs, float rhs, float t) =>
        (float)(lhs * (1.0 - t) + rhs * t);

    /// <summary>Linear interpolation between two values.</summary>
    public static double Lerp(double lhs, double rhs, double t) => lhs * (1.0 - t) + rhs * t;

    /// <summary>Inverse linear interpolation between two values.</summary>
    public static float InverseLerp(float lhs, float rhs, float value) => (value - lhs) / (rhs - lhs);

    /// <summary>Return the sign of a float (-1, 0 or 1).</summary>
    public static float Sign(float value) => value > 0.0f ? 1.0f : value < 0.0f ? -1.0f : 0.0f;

    /// <summary>Convert degrees to radians.</summary>
    public static float ToRadians(float degrees) => DegToRad * degrees;

    /// <summary>Convert radians to degrees.</summary>
    public static float ToDegrees(float radians) => RadToDeg * radians;

    /// <summary>Return a representation of the specified floating-point value as a single format bit layout.</summary>
    public static uint FloatToRawIntBits(float value) => unchecked((uint)BitConverter.SingleToInt32Bits(value));

    /// <summary>Check whether a floating point value is NaN.</summary>
    public static bool IsNaN(float value) => float.IsNaN(value);

    /// <summary>Check whether a floating point value is NaN.</summary>
    public static bool IsNaN(double value) => double.IsNaN(value);

    /// <summary>Check whether a floating point value is positive or negative infinity.</summary>
    public static bool IsInf(float value) => float.IsInfinity(value);

    /// <summary>Check whether a floating point value is positive or negative infinity.</summary>
    public static bool IsInf(double value) => double.IsInfinity(value);

    /// <summary>Clamp a number to a range.</summary>
    public static float Clamp(float value, float min, float max)
    {
        if (value < min)
            return min;
        else if (value > max)
            return max;
        else
            return value;
    }

    /// <summary>Clamp a number to a range.</summary>
    public static int Clamp(int value, int min, int max)
    {
        if (value < min)
            return min;
        else if (value > max)
            return max;
        else
            return value;
    }

    /// <summary>Per-component clamp of vector.</summary>
    public static Vector2f VectorClamp(Vector2f value, Vector2f min, Vector2f max) =>
        Vector2f.Max(min, Vector2f.Min(value, max));

    /// <summary>Per-component clamp of vector.</summary>
    public static Vector3f VectorClamp(Vector3f value, Vector3f min, Vector3f max) =>
        Vector3f.Max(min, Vector3f.Min(value, max));

    /// <summary>Per-component clamp of vector.</summary>
    public static Vector4f VectorClamp(Vector4f value, Vector4f min, Vector4f max) =>
        VectorMax(min, VectorMin(value, max));

    /// <summary>Smoothly damp between values.</summary>
    public static float SmoothStep(float lhs, float rhs, float t)
    {
        t = Clamp((t - lhs) / (rhs - lhs), 0.0f, 1.0f); // Saturate t
        return t * t * (3.0f - 2.0f * t);
    }

    /// <summary>Calculate exponential decay function.</summary>
    public static float ExponentialDecay(float value) => Clamp(MathF.Pow(2.0f, -value), 0.0f, 1.0f);

    public static float InverseExponentialDecay(float value) => 1.0f - ExponentialDecay(value);

    /// <summary>
    /// Apply exponential smoothing to raw value. Typical usage:
    /// <c>smoothValue = Smooth(smoothValue, rawValue, timeStep / halfTimeOfSmoothing)</c>
    /// or
    /// <c>smoothValue = Smooth(smoothValue, rawValue, timeStep * smoothingRate)</c>
    /// </summary>
    public static float Smooth(float lhs, float rhs, float t) => Lerp(lhs, rhs, InverseExponentialDecay(t));

    /// <summary>Same as <c>Smooth(lhs, rhs, timeStep / halfTime)</c>.</summary>
    public static float Smooth(float lhs, float rhs, float halfTime, float timeStep) =>
        halfTime != 0.0f ? Smooth(lhs, rhs, timeStep / halfTime) : rhs;

    /// <summary>Return sine of an angle in degrees.</summary>
    public static float Sin(float angle) => MathF.Sin(angle * DegToRad);

    /// <summary>Return cosine of an angle in degrees.</summary>
    public static float Cos(float angle) => MathF.Cos(angle * DegToRad);

    /// <summary>Return tangent of an angle in degrees.</summary>
    public static float Tan(float angle) => MathF.Tan(angle * DegToRad);

    /// <summary>Return arc sine in degrees.</summary>
    public static float Asin(float value) => RadToDeg * MathF.Asin(Clamp(value, -1.0f, 1.0f));

    /// <summary>Return arc cosine in degrees.</summary>
    public static float Acos(float value) => RadToDeg * MathF.Acos(Clamp(value, -1.0f, 1.0f));

    /// <summary>Return arc tangent in degrees.</summary>
    public static float Atan(float value) => RadToDeg * MathF.Atan(value);

    /// <summary>Return arc tangent of y/x in degrees.</summary>
    public static float Atan2(float y, float x) => RadToDeg * MathF.Atan2(y, x);

    /// <summary>Return X in power Y.</summary>
    public static float Pow(float x, float y) => MathF.Pow(x, y);

    /// <summary>Return natural logarithm of X.</summary>
    public static float Ln(float value) => MathF.Log(value);

    /// <summary>Return square root of X.</summary>
    public static float Sqrt(float value) => MathF.Sqrt(value);

    /// <summary>Return remainder of X/Y for float values.</summary>
    public static float Mod(float x, float y) => x % y;

    /// <summary>Return remainder of X/Y for integer values.</summary>
    public static int Mod(int x, int y) => x % y;

    /// <summary>Return always positive remainder of X/Y.</summary>
    public static float AbsMod(float x, float y)
    {
        var result = Mod(x, y);
        return result < 0.0f ? result + y : result;
    }

    /// <summary>Return always positive remainder of X/Y.</summary>
    public static int AbsMod(int x, int y)
    {
        var result = Mod(x, y);
        return result < 0 ? result + y : result;
    }

    /// <summary>Return fractional part of passed value in range [0, 1).</summary>
    public static float Fract(float value) => value - MathF.Floor(value);

    /// <summary>Return exact value of y if x is close to it, return x as-is otherwise.</summary>
    public static float SnapTo(float x, float y, float epsilon = Epsilon) => Equals(x, y, epsilon) ? y : x;

    /// <summary>Round value down.</summary>
    public static float Floor(float x) => MathF.Floor(x);

    /// <summary>Round value down to nearest number that can be represented as i*y, where i is integer.</summary>
    public static float SnapFloor(float x, float y) => MathF.Floor(x / y) * y;

    /// <summary>Round value down. Returns integer value.</summary>
    public static int FloorToInt(float x) => (int)MathF.Floor(x);

    /// <summary>Round value to nearest integer.</summary>
    public static float Round(float x) => MathF.Round(x, MidpointRounding.AwayFromZero);

    /// <summary>Compute average value of the range.</summary>
    public static float Average(IEnumerable<float> values)
    {
        var average = 0.0f;
        uint size = 0;
        foreach (var value in values)
        {
            average += value;
            ++size;
        }

        return size != 0 ? average / size : average;
    }

    /// <summary>Compute average value of the range.</summary>
    public static double Average(IEnumerable<double> values)
    {
        var average = 0.0;
        uint size = 0;
        foreach (var value in values)
        {
            average += value;
            ++size;
        }

        return size != 0 ? average / size : average;
    }

    /// <summary>Round value to nearest number that can be represented as i*y, where i is integer.</summary>
    public static float SnapRound(float x, float y) => Round(x / y) * y;

    /// <summary>Round value to nearest integer.</summary>
    public static int RoundToInt(float x) => (int)Round(x);

    /// <summary>Round value to nearest multiple.</summary>
    public static float RoundToNearestMultiple(float x, float multiple)
    {
        var mag = MathF.Abs(x);
        multiple = MathF.Abs(multiple);
        var remainder = Mod(mag, multiple);
        if (remainder >= multiple / 2.0f)
            return (FloorToInt(mag / multiple) * multiple + multiple) * Sign(x);
        else
            return (FloorToInt(mag / multiple) * multiple) * Sign(x);
    }

    /// <summary>Round value up.</summary>
    public static float Ceil(float x) => MathF.Ceiling(x);

    /// <summary>Round value up to nearest number that can be represented as i*y, where i is integer.</summary>
    public static float SnapCeil(float x, float y) => MathF.Ceiling(x / y) * y;

    /// <summary>Round value up.</summary>
    public static int CeilToInt(float x) => (int)MathF.Ceiling(x);

    /// <summary>Check whether an unsigned integer is a power of two.</summary>
    public static bool IsPowerOfTwo(uint value)
    {
        return (value & (value - 1u)) == 0 && value != 0;
    }

    /// <summary>Round up to next power of two.</summary>
    public static uint NextPowerOfTwo(uint value)
    {
        // http://graphics.stanford.edu/~seander/bithacks.html#RoundUpPowerOf2
        value = unchecked(value - 1u);
        value |= value >> 1;
        value |= value >> 2;
        value |= value >> 4;
        value |= value >> 8;
        value |= value >> 16;
        return unchecked(value + 1u);
    }

    /// <summary>Round up or down to the closest power of two.</summary>
    public static uint ClosestPowerOfTwo(uint value)
    {
        var next = NextPowerOfTwo(value);
        var prev = next >> 1;
        return unchecked(value - prev) > unchecked(next - value) ? next : prev;
    }

    /// <summary>Return log base two or the MSB position of the given value.</summary>
    public static uint LogBaseTwo(uint value)
    {
        // http://graphics.stanford.edu/~seander/bithacks.html#IntegerLogObvious
        uint result = 0;
        while ((value >>= 1) != 0) // Unroll for more speed...
            ++result;
        return result;
    }

    /// <summary>Count the number of set bits in a mask.</summary>
    public static uint CountSetBits(uint value)
    {
        // Brian Kernighan's method
        uint count;
        for (count = 0; value != 0; ++count)
            value &= value - 1u;
        return count;
    }

    /// <summary>Update a hash with the given 8-bit value using the SDBM algorithm.</summary>
    public static uint SDBMHash(uint hash, byte value) =>
        unchecked(value + (hash << 6) + (hash << 16) - hash);

    /// <summary>Convert float to half float. From https://gist.github.com/martinkallman/5049614</summary>
    public static ushort FloatToHalf(float value)
    {
        unchecked
        {
            var inu = FloatToRawIntBits(value);
            var t1 = inu & 0x7fffffffu;       // Non-sign bits
            var t2 = inu & 0x80000000u;       // Sign bit
            var t3 = inu & 0x7f800000u;       // Exponent

            t1 >>= 13;                        // Align mantissa on MSB
            t2 >>= 16;                        // Shift sign bit into position

            t1 -= 0x1c000u;                   // Adjust bias

            t1 = t3 < 0x38800000u ? 0u : t1;  // Flush-to-zero
            t1 = t3 > 0x47000000u ? 0x7bffu : t1; // Clamp-to-max
            t1 = t3 == 0u ? 0u : t1;          // Denormals-as-zero

            t1 |= t2;                         // Re-insert sign bit

            return (ushort)t1;
        }
    }

    /// <summary>Convert half float to float. From https://gist.github.com/martinkallman/5049614</summary>
    public static float HalfToFloat(ushort value)
    {
        var t1 = (uint)(value & 0x7fffu);      // Non-sign bits
        var t2 = (uint)(value & 0x8000u);      // Sign bit
        var t3 = (uint)(value & 0x7c00u);      // Exponent

        t1 <<= 13;                             // Align mantissa on MSB
        t2 <<= 16;                             // Shift sign bit into position

        t1 += 0x38000000u;                     // Adjust bias

        t1 = t3 == 0u ? 0u : t1;               // Denormals-as-zero

        t1 |= t2;                              // Re-insert sign bit

        return BitConverter.UInt32BitsToSingle(t1);
    }

    /// <summary>Wrap a value fitting it in the range defined by [min, max)</summary>
    public static float Wrap(float value, float min, float max)
    {
        var range = max - min;
        return min + Mod(value, range);
    }

    /// <summary>Wrap a value fitting it in the range defined by [min, max)</summary>
    public static int Wrap(int value, int min, int max)
    {
        var range = max - min;
        return min + Mod(value, range);
    }

    /// <summary>Calculate both sine and cosine, with angle in degrees.</summary>
    public static void SinCos(float angle, out float sin, out float cos)
    {
        var angleRadians = angle * DegToRad;
        sin = MathF.Sin(angleRadians);
        cos = MathF.Cos(angleRadians);
    }

    /// <summary>Return max number of bytes taken by a variable-length encoded integer of the given type.</summary>
    public static int MaxVariableLengthBytes<T>() where T : unmanaged => (Unsafe.SizeOf<T>() * 8 + 6) / 7;

    /// <summary>Convert integer to variable-length encoded byte array. Returns the number of bytes written.</summary>
    public static uint EncodeVariableLength(uint value, Span<byte> destination)
    {
        var maxBytes = (uint)MaxVariableLengthBytes<uint>();
        for (uint i = 0; i < maxBytes; ++i)
        {
            destination[(int)i] = (byte)(value & 0x7f);
            value >>= 7;
            if (value == 0)
                return i + 1;
            destination[(int)i] |= 0x80;
        }
        return maxBytes;
    }

    /// <summary>Convert integer to variable-length encoded byte array. Returns the number of bytes written.</summary>
    public static uint EncodeVariableLength(ulong value, Span<byte> destination)
    {
        var maxBytes = (uint)MaxVariableLengthBytes<ulong>();
        for (uint i = 0; i < maxBytes; ++i)
        {
            destination[(int)i] = (byte)(value & 0x7f);
            value >>= 7;
            if (value == 0)
                return i + 1;
            destination[(int)i] |= 0x80;
        }
        return maxBytes;
    }

    /// <summary>Decode variable-length encoded integer (one step). Return true if the value is complete.</summary>
    public static bool DecodeVariableLength(ref uint value, ref uint offset, byte current)
    {
        value |= (uint)(current & 0x7f) << (int)offset;
        offset += 7;
        return (current & 0x80) == 0;
    }

    /// <summary>Decode variable-length encoded integer (one step). Return true if the value is complete.</summary>
    public static bool DecodeVariableLength(ref ulong value, ref uint offset, byte current)
    {
        value |= (ulong)(current & 0x7f) << (int)offset;
        offset += 7;
        return (current & 0x80) == 0;
    }

    /// <summary>Zigzag encode signed integer as unsigned.</summary>
    public static byte ZigzagEncode(sbyte value) =>
        unchecked((byte)(((byte)value << 1) ^ (byte)(value >> 6)));

    /// <summary>Zigzag encode signed integer as unsigned.</summary>
    public static ushort ZigzagEncode(short value) =>
        unchecked((ushort)(((ushort)value << 1) ^ (ushort)(value >> 14)));

    /// <summary>Zigzag encode signed integer as unsigned.</summary>
    public static uint ZigzagEncode(int value) =>
        unchecked(((uint)value << 1) ^ (uint)(value >> 30));

    /// <summary>Zigzag encode signed integer as unsigned.</summary>
    public static ulong ZigzagEncode(long value) =>
        unchecked(((ulong)value << 1) ^ (ulong)(value >> 62));

    /// <summary>Zigzag decode unsigned integer as signed.</summary>
    public static sbyte ZigzagDecode(byte value) =>
        unchecked((sbyte)((value >> 1) ^ -(sbyte)(value & 1)));

    /// <summary>Zigzag decode unsigned integer as signed.</summary>
    public static short ZigzagDecode(ushort value) =>
        unchecked((short)((value >> 1) ^ -(short)(value & 1)));

    /// <summary>Zigzag decode unsigned integer as signed.</summary>
    public static int ZigzagDecode(uint value) => unchecked((int)(value >> 1) ^ -(int)(value & 1));

    /// <summary>Zigzag decode unsigned integer as signed.</summary>
    public static long ZigzagDecode(ulong value) => unchecked((long)(value >> 1) ^ -(long)(value & 1));

    /// <summary>Per-component linear interpolation between two 4-vectors.</summary>
    public static Vector4f VectorLerp(Vector4f lhs, Vector4f rhs, Vector4f t) => lhs + (rhs - lhs) * t;

    /// <summary>Per-component min of two 4-vectors.</summary>
    public static Vector4f VectorMin(Vector4f lhs, Vector4f rhs) => new(
        lhs.X < rhs.X ? lhs.X : rhs.X,
        lhs.Y < rhs.Y ? lhs.Y : rhs.Y,
        lhs.Z < rhs.Z ? lhs.Z : rhs.Z,
        lhs.W < rhs.W ? lhs.W : rhs.W);

    /// <summary>Per-component max of two 4-vectors.</summary>
    public static Vector4f VectorMax(Vector4f lhs, Vector4f rhs) => new(
        lhs.X > rhs.X ? lhs.X : rhs.X,
        lhs.Y > rhs.Y ? lhs.Y : rhs.Y,
        lhs.Z > rhs.Z ? lhs.Z : rhs.Z,
        lhs.W > rhs.W ? lhs.W : rhs.W);

    /// <summary>Per-component floor of 4-vector.</summary>
    public static Vector4f VectorFloor(Vector4f value) =>
        new(Floor(value.X), Floor(value.Y), Floor(value.Z), Floor(value.W));

    /// <summary>Per-component round of 4-vector.</summary>
    public static Vector4f VectorRound(Vector4f value) =>
        new(Round(value.X), Round(value.Y), Round(value.Z), Round(value.W));

    /// <summary>Per-component ceil of 4-vector.</summary>
    public static Vector4f VectorCeil(Vector4f value) =>
        new(Ceil(value.X), Ceil(value.Y), Ceil(value.Z), Ceil(value.W));
}
