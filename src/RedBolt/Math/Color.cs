using System.Globalization;
using System.Runtime.InteropServices;

namespace RedBolt;

[StructLayout(LayoutKind.Sequential)]
public struct Color : IEquatable<Color>, IApproximateEquatable<Color>
{
    public readonly record struct ChannelMask(uint R, uint G, uint B, uint A);
    public static readonly ChannelMask ABGR = new(0x000000ff, 0x0000ff00, 0x00ff0000, 0xff000000);
    public static readonly ChannelMask ARGB = new(0x00ff0000, 0x0000ff00, 0x000000ff, 0xff000000);
    public static readonly ChannelMask RGB = new(0x00ff0000, 0x0000ff00, 0x000000ff, 0);

    public float R;
    public float G;
    public float B;
    public float A;

    public Color(float r = 1.0f, float g = 1.0f, float b = 1.0f, float a = 1.0f)
    {
        R = r;
        G = g;
        B = b;
        A = a;
    }

    public Color(Color color, float a)
    {
        R = color.R;
        G = color.G;
        B = color.B;
        A = a;
    }

    public Color(IReadOnlyList<float> data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.Count < 4)
            throw new ArgumentException("At least four components are required.", nameof(data));
        R = data[0]; G = data[1]; B = data[2]; A = data[3];
    }

    public Color(uint color, ChannelMask? mask = null)
    {
        R = G = B = 0.0f; A = 1.0f;
        FromUIntMask(color, mask ?? ABGR);
    }

    public Color(Vector3f color) : this(color.X, color.Y, color.Z) { }
    public Color(Vector4f color) : this(color.X, color.Y, color.Z, color.W) { }

    public static readonly Color White = new(1.0f, 1.0f, 1.0f, 1.0f);
    public static readonly Color Gray = new(0.5f, 0.5f, 0.5f);
    public static readonly Color Red = new(1.0f, 0.0f, 0.0f);
    public static readonly Color Green = new(0.0f, 1.0f, 0.0f);
    public static readonly Color Blue = new(0.0f, 0.0f, 1.0f);
    public static readonly Color Cyan = new(0.0f, 1.0f, 1.0f);
    public static readonly Color Magenta = new(1.0f, 0.0f, 1.0f);
    public static readonly Color Yellow = new(1.0f, 1.0f, 0.0f);
    public static readonly Color TransparentBlack = new(0.0f, 0.0f, 0.0f, 0.0f);
    public static readonly Color LuminosityGamma = new(0.299f, 0.587f, 0.114f, 0.0f);
    public static readonly Color LuminosityLinear = new(0.2126f, 0.7152f, 0.0722f, 0.0f);
    public static readonly Color Black = new(0.0f, 0.0f, 0.0f, 1.0f);
    public static readonly Color Transparent = new(0.0f, 0.0f, 0.0f, 0.0f);

    public float Average() => (R + G + B) / 3.0f;
    public float Chroma()
    {
        Bounds(out var min, out var max, clipped: true);
        return max - min;
    }

    public void Bounds(out float min, out float max, bool clipped = false)
    {
        min = Math.Min(R, Math.Min(G, B));
        max = Math.Max(R, Math.Max(G, B));
        if (clipped)
        {
            min = MathDefs.Clamp(min, 0.0f, 1.0f);
            max = MathDefs.Clamp(max, 0.0f, 1.0f);
        }
    }

    public void Clip(bool clipAlpha = false)
    {
        R = MathDefs.Clamp(R, 0.0f, 1.0f);
        G = MathDefs.Clamp(G, 0.0f, 1.0f);
        B = MathDefs.Clamp(B, 0.0f, 1.0f);
        if (clipAlpha)
            A = MathDefs.Clamp(A, 0.0f, 1.0f);
    }

    public void FromUInt(uint color)
    {
        A = ((color >> 24) & 0xff) / 255.0f;
        B = ((color >> 16) & 0xff) / 255.0f;
        G = ((color >> 8) & 0xff) / 255.0f;
        R = (color & 0xff) / 255.0f;
    }

    public void FromUIntMask(uint color, ChannelMask mask)
    {
        R = Unpack(color, mask.R, 0.0f);
        G = Unpack(color, mask.G, 0.0f);
        B = Unpack(color, mask.B, 0.0f);
        A = Unpack(color, mask.A, 1.0f);
    }

    public uint ToUIntMask(ChannelMask mask) =>
        Pack(R, mask.R) | Pack(G, mask.G) | Pack(B, mask.B) | Pack(A, mask.A);

    public void FromHSV(float h, float s, float v, float a = 1.0f) => FromHcm(h, v * s, v - v * s, a);

    public void FromHSL(float h, float s, float l, float a = 1.0f)
    {
        var chroma = (1.0f - MathF.Abs(2.0f * l - 1.0f)) * s;
        FromHcm(h, chroma, l - chroma * 0.5f, a);
    }

    // C++: else if (value < 1.0f) return Pow((value + 0.055f) / 1.055f, 2.4f);
    // C++: else return Pow(value, 2.2f);
    public static float ConvertGammaToLinear(float value) => value <= 0.04045f ? value / 12.92f : MathF.Pow((value + 0.055f) / 1.055f, 2.4f);
    // C++: if (value <= 0.0f) return 0.0f;
    // C++: else if (value <= 0.0031308f) return 12.92f * value;
    // C++: else if (value < 1.0f) return 1.055f * Pow(value, 0.4166667f) - 0.055f;
    // C++: else return Pow(value, 0.45454545f);
    public static float ConvertLinearToGamma(float value) => value <= 0.0031308f ? value * 12.92f : 1.055f * MathF.Pow(value, 1.0f / 2.4f) - 0.055f;
    public Color GammaToLinear() => new(ConvertGammaToLinear(R), ConvertGammaToLinear(G), ConvertGammaToLinear(B), A);
    public Color LinearToGamma() => new(ConvertLinearToGamma(R), ConvertLinearToGamma(G), ConvertLinearToGamma(B), A);
    public Color Abs() => new(MathF.Abs(R), MathF.Abs(G), MathF.Abs(B), MathF.Abs(A));

    public float Hue()
    {
        Bounds(out var min, out var max, clipped: true);
        return Hue(min, max);
    }

    public void Invert(bool invertAlpha = false)
    {
        R = 1.0f - R;
        G = 1.0f - G;
        B = 1.0f - B;
        if (invertAlpha)
            A = 1.0f - A;
    }

    public Color Lerp(Color rhs, float t)
    {
        var invT = 1.0f - t;
        return new Color(R * invT + rhs.R * t, G * invT + rhs.G * t, B * invT + rhs.B * t, A * invT + rhs.A * t);
    }

    public float Lightness()
    {
        Bounds(out var min, out var max, clipped: true);
        return (max + min) * 0.5f;
    }

    public float Luma() => R * 0.299f + G * 0.587f + B * 0.114f;
    public float MaxRGB() => Math.Max(R, Math.Max(G, B));
    public float MinRGB() => Math.Min(R, Math.Min(G, B));
    public float Range()
    {
        Bounds(out var min, out var max);
        return max - min;
    }

    public float SaturationHSL()
    {
        Bounds(out var min, out var max, clipped: true);
        return SaturationHSL(min, max);
    }

    public float SaturationHSV()
    {
        Bounds(out var min, out var max, clipped: true);
        return SaturationHSV(min, max);
    }

    public float SumRGB() => R + G + B;
    public Vector3f ToHSL()
    {
        Bounds(out var min, out var max, clipped: true);
        return new Vector3f(Hue(min, max), SaturationHSL(min, max), (max + min) * 0.5f);
    }

    public Vector3f ToHSV()
    {
        Bounds(out var min, out var max, clipped: true);
        return new Vector3f(Hue(min, max), SaturationHSV(min, max), max);
    }

    public uint ToUInt()
    {
        var r = (uint)Math.Clamp((int)(R * 255.0f), 0, 255);
        var g = (uint)Math.Clamp((int)(G * 255.0f), 0, 255);
        var b = (uint)Math.Clamp((int)(B * 255.0f), 0, 255);
        var a = (uint)Math.Clamp((int)(A * 255.0f), 0, 255);
        return (a << 24) | (b << 16) | (g << 8) | r;
    }

    public uint ToUIntArgb()
    {
        var r = (uint)Math.Clamp((int)(R * 255.0f), 0, 255);
        var g = (uint)Math.Clamp((int)(G * 255.0f), 0, 255);
        var b = (uint)Math.Clamp((int)(B * 255.0f), 0, 255);
        var a = (uint)Math.Clamp((int)(A * 255.0f), 0, 255);
        return (a << 24) | (r << 16) | (g << 8) | b;
    }

    public Vector3f ToVector3() => new(R, G, B);
    public Vector4f ToVector4() => new(R, G, B, A);
    public float Value() => MaxRGB();

    public bool ApproximatelyEquivalent(Color rhs, float epsilon = MathDefs.Epsilon) =>
        MathDefs.ApproximatelyEquivalent(R, rhs.R, epsilon) &&
        MathDefs.ApproximatelyEquivalent(G, rhs.G, epsilon) &&
        MathDefs.ApproximatelyEquivalent(B, rhs.B, epsilon) &&
        MathDefs.ApproximatelyEquivalent(A, rhs.A, epsilon);

    public bool Equals(Color other) => R == other.R && G == other.G && B == other.B && A == other.A;
    public override bool Equals(object? obj) => obj is Color other && Equals(other);
    public override int GetHashCode() => (int)ToUInt();
    public override string ToString() => string.Format(CultureInfo.InvariantCulture, "{0} {1} {2} {3}", R, G, B, A);

    public static bool operator ==(Color lhs, Color rhs) => lhs.Equals(rhs);
    public static bool operator !=(Color lhs, Color rhs) => !lhs.Equals(rhs);
    public static Color operator +(Color lhs, Color rhs) => new(lhs.R + rhs.R, lhs.G + rhs.G, lhs.B + rhs.B, lhs.A + rhs.A);
    public static Color operator -(Color value) => new(-value.R, -value.G, -value.B, -value.A);
    public static Color operator -(Color lhs, Color rhs) => new(lhs.R - rhs.R, lhs.G - rhs.G, lhs.B - rhs.B, lhs.A - rhs.A);
    public static Color operator *(Color lhs, float rhs) => new(lhs.R * rhs, lhs.G * rhs, lhs.B * rhs, lhs.A * rhs);
    public static Color operator *(float lhs, Color rhs) => rhs * lhs;
    public static Color operator *(Color lhs, Color rhs) => new(lhs.R * rhs.R, lhs.G * rhs.G, lhs.B * rhs.B, lhs.A * rhs.A);

    private void FromHcm(float hue, float chroma, float minimum, float alpha)
    {
        hue = MathDefs.Fract(hue) * 6.0f;
        var x = chroma * (1.0f - MathF.Abs(hue % 2.0f - 1.0f));
        (R, G, B) = hue switch
        {
            < 1.0f => (chroma, x, 0.0f),
            < 2.0f => (x, chroma, 0.0f),
            < 3.0f => (0.0f, chroma, x),
            < 4.0f => (0.0f, x, chroma),
            < 5.0f => (x, 0.0f, chroma),
            _ => (chroma, 0.0f, x)
        };
        R += minimum; G += minimum; B += minimum; A = alpha;
    }

    private static uint Pack(float value, uint mask)
    {
        if (mask == 0)
            return 0;
        var shift = System.Numerics.BitOperations.TrailingZeroCount(mask);
        return ((uint)(Math.Clamp(value, 0.0f, 1.0f) * 255.0f) << shift) & mask;
    }

    private static float Unpack(uint value, uint mask, float fallback)
    {
        if (mask == 0)
            return fallback;
        var shift = System.Numerics.BitOperations.TrailingZeroCount(mask);
        return ((value & mask) >> shift) / 255.0f;
    }

    private float Hue(float min, float max)
    {
        var chroma = max - min;
        if (chroma <= MathDefs.Epsilon)
            return 0.0f;
        if (MathDefs.Equals(G, max))
            return (B + 2.0f * chroma - R) / (6.0f * chroma);
        if (MathDefs.Equals(B, max))
            return (4.0f * chroma - G + R) / (6.0f * chroma);
        return (G - B) / (6.0f * chroma);
    }

    private float SaturationHSL(float min, float max)
    {
        var chroma = max - min;
        if (chroma <= MathDefs.Epsilon)
            return 0.0f;
        var sum = max + min;
        return sum <= 1.0f ? chroma / sum : chroma / (2.0f - sum);
    }

    private float SaturationHSV(float min, float max)
    {
        var chroma = max - min;
        return chroma <= MathDefs.Epsilon || max <= MathDefs.Epsilon ? 0.0f : chroma / max;
    }
}
