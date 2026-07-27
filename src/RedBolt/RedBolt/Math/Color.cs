using System.Globalization;
using SystemMath = global::System.Math;
using System.Runtime.InteropServices;

namespace RedBolt.Math;

/// <summary>RGBA color.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct Color : IEquatable<Color>
{
    /// <summary>Mask describing color channels.</summary>
    public struct ChannelMask
    {
        public ChannelMask(uint r, uint g, uint b, uint a)
        {
            R = r;
            G = g;
            B = b;
            A = a;
        }

        /// <summary>Red channel mask. If zero, red channel is set to 0.</summary>
        public uint R;
        /// <summary>Green channel mask. If zero, green channel is set to 0.</summary>
        public uint G;
        /// <summary>Blue channel mask. If zero, blue channel is set to 0.</summary>
        public uint B;
        /// <summary>Alpha channel mask. If zero, alpha channel is set to 1.</summary>
        public uint A;
    }

    /// <summary>Mask for 0xAABBGGRR layout.</summary>
    public static readonly ChannelMask ABGR = new(0x000000ff, 0x0000ff00, 0x00ff0000, 0xff000000);
    /// <summary>Mask for 0xAARRGGBB layout.</summary>
    public static readonly ChannelMask ARGB = new(0x00ff0000, 0x0000ff00, 0x000000ff, 0xff000000);
    /// <summary>Mask for 0xRRGGBB layout.</summary>
    public static readonly ChannelMask RGB = new(0x00ff0000, 0x0000ff00, 0x000000ff, 0);

    /// <summary>Construct with default values (opaque white).</summary>
    public Color()
        : this(1.0f, 1.0f, 1.0f, 1.0f)
    {
    }

    /// <summary>Copy-construct from another color.</summary>
    public Color(Color color)
        : this(color.R, color.G, color.B, color.A)
    {
    }

    /// <summary>Construct from another color and modify the alpha.</summary>
    public Color(Color color, float a)
        : this(color.R, color.G, color.B, a)
    {
    }

    /// <summary>Construct from RGB values and set alpha fully opaque.</summary>
    public Color(float r, float g, float b)
        : this(r, g, b, 1.0f)
    {
    }

    /// <summary>Construct from RGBA values.</summary>
    public Color(float r, float g, float b, float a)
    {
        R = r;
        G = g;
        B = b;
        A = a;
    }

    /// <summary>Construct from a float array.</summary>
    public Color(IReadOnlyList<float> data)
        : this(data[0], data[1], data[2], data[3])
    {
    }

    /// <summary>Construct from 32-bit integer. Default format is 0xAABBGGRR.</summary>
    public Color(uint color)
        : this(color, ABGR)
    {
    }

    /// <summary>Construct from 32-bit integer with arbitrary channel mask.</summary>
    public Color(uint color, ChannelMask mask)
        : this()
    {
        FromUIntMask(color, mask);
    }

    /// <summary>Construct from 3-vector.</summary>
    public Color(Vector3f color)
        : this(color.X, color.Y, color.Z)
    {
    }

    /// <summary>Construct from 4-vector.</summary>
    public Color(Vector4f color)
        : this(color.X, color.Y, color.Z, color.W)
    {
    }

    /// <summary>Test for equality with another color without epsilon.</summary>
    public static bool operator ==(Color lhs, Color rhs) =>
        lhs.R == rhs.R && lhs.G == rhs.G && lhs.B == rhs.B && lhs.A == rhs.A;

    /// <summary>Test for inequality with another color without epsilon.</summary>
    public static bool operator !=(Color lhs, Color rhs) =>
        lhs.R != rhs.R || lhs.G != rhs.G || lhs.B != rhs.B || lhs.A != rhs.A;

    /// <summary>Multiply with a scalar.</summary>
    public static Color operator *(Color lhs, float rhs) =>
        new(lhs.R * rhs, lhs.G * rhs, lhs.B * rhs, lhs.A * rhs);

    /// <summary>Modulate color.</summary>
    public static Color operator *(Color lhs, Color rhs) =>
        new(lhs.R * rhs.R, lhs.G * rhs.G, lhs.B * rhs.B, lhs.A * rhs.A);

    /// <summary>Add a color.</summary>
    public static Color operator +(Color lhs, Color rhs) =>
        new(lhs.R + rhs.R, lhs.G + rhs.G, lhs.B + rhs.B, lhs.A + rhs.A);

    /// <summary>Return negation.</summary>
    public static Color operator -(Color value) => new(-value.R, -value.G, -value.B, -value.A);

    /// <summary>Subtract a color.</summary>
    public static Color operator -(Color lhs, Color rhs) =>
        new(lhs.R - rhs.R, lhs.G - rhs.G, lhs.B - rhs.B, lhs.A - rhs.A);

    /// <summary>Return float data.</summary>
    public ReadOnlySpan<float> Data() => MemoryMarshal.CreateReadOnlySpan(ref R, 4);

    /// <summary>Return color packed to a 32-bit integer, with R component in the lowest 8 bits. Components are clamped to [0, 1] range.</summary>
    public uint ToUInt()
    {
        var r = (uint)SystemMath.Clamp((int)(R * 255.0f), 0, 255);
        var g = (uint)SystemMath.Clamp((int)(G * 255.0f), 0, 255);
        var b = (uint)SystemMath.Clamp((int)(B * 255.0f), 0, 255);
        var a = (uint)SystemMath.Clamp((int)(A * 255.0f), 0, 255);
        return (a << 24) | (b << 16) | (g << 8) | r;
    }

    /// <summary>Return color packed to a 32-bit integer with arbitrary channel mask. Components are clamped to [0, 1] range.</summary>
    public uint ToUIntMask(ChannelMask mask)
    {
        var max = (double)uint.MaxValue;
        var r = (uint)SystemMath.Clamp((double)R * mask.R, 0.0, max) & mask.R;
        var g = (uint)SystemMath.Clamp((double)G * mask.G, 0.0, max) & mask.G;
        var b = (uint)SystemMath.Clamp((double)B * mask.B, 0.0, max) & mask.B;
        var a = (uint)SystemMath.Clamp((double)A * mask.A, 0.0, max) & mask.A;
        return r | g | b | a;
    }

    /// <summary>Return HSL color-space representation as a Vector3; the RGB values are clipped before conversion but not changed in the process.</summary>
    public Vector3f ToHSL()
    {
        Bounds(out var min, out var max, true);
        var h = Hue(min, max);
        var s = SaturationHSL(min, max);
        var l = (max + min) * 0.5f;
        return new Vector3f(h, s, l);
    }

    /// <summary>Return HSV color-space representation as a Vector3; the RGB values are clipped before conversion but not changed in the process.</summary>
    public Vector3f ToHSV()
    {
        Bounds(out var min, out var max, true);
        var h = Hue(min, max);
        var s = SaturationHSV(min, max);
        var v = max;
        return new Vector3f(h, s, v);
    }

    /// <summary>Set RGBA values from packed 32-bit integer, with R component in the lowest 8 bits (format 0xAABBGGRR).</summary>
    public void FromUInt(uint color)
    {
        A = ((color >> 24) & 0xff) / 255.0f;
        B = ((color >> 16) & 0xff) / 255.0f;
        G = ((color >> 8) & 0xff) / 255.0f;
        R = (color & 0xff) / 255.0f;
    }

    /// <summary>Set RGBA values from packed 32-bit integer with arbitrary channel mask.</summary>
    public void FromUIntMask(uint color, ChannelMask mask)
    {
        // Channel offset is irrelevant during division, but double should be used to avoid precision loss.
        R = mask.R == 0 ? 0.0f : (float)((color & mask.R) / (double)mask.R);
        G = mask.G == 0 ? 0.0f : (float)((color & mask.G) / (double)mask.G);
        B = mask.B == 0 ? 0.0f : (float)((color & mask.B) / (double)mask.B);
        A = mask.A == 0 ? 1.0f : (float)((color & mask.A) / (double)mask.A);
    }

    /// <summary>Set RGBA values from specified HSL values and alpha.</summary>
    public void FromHSL(float h, float s, float l, float a = 1.0f)
    {
        float c;
        if (l < 0.5f)
            c = (1.0f + (2.0f * l - 1.0f)) * s;
        else
            c = (1.0f - (2.0f * l - 1.0f)) * s;
        var m = l - 0.5f * c;
        FromHCM(h, c, m);
        A = a;
    }

    /// <summary>Set RGBA values from specified HSV values and alpha.</summary>
    public void FromHSV(float h, float s, float v, float a = 1.0f)
    {
        var c = v * s;
        var m = v - c;
        FromHCM(h, c, m);
        A = a;
    }

    /// <summary>Return RGB as a three-dimensional vector.</summary>
    public Vector3f ToVector3() => new(R, G, B);

    /// <summary>Return RGBA as a four-dimensional vector.</summary>
    public Vector4f ToVector4() => new(R, G, B, A);

    /// <summary>Return sum of RGB components.</summary>
    public float SumRGB() => R + G + B;

    /// <summary>Return average value of the RGB channels.</summary>
    public float Average() => (R + G + B) / 3.0f;

    /// <summary>Return the 'grayscale' representation of RGB values, as used by JPEG and PAL/NTSC among others.</summary>
    public float Luma() => R * 0.299f + G * 0.587f + B * 0.114f;

    /// <summary>Return the colorfulness relative to the brightness of a similarly illuminated white.</summary>
    public float Chroma()
    {
        Bounds(out var min, out var max, true);
        return max - min;
    }

    /// <summary>Return hue mapped to range [0, 1.0).</summary>
    public float Hue()
    {
        Bounds(out var min, out var max, true);
        return Hue(min, max);
    }

    /// <summary>Return saturation as defined for HSL.</summary>
    public float SaturationHSL()
    {
        Bounds(out var min, out var max, true);
        return SaturationHSL(min, max);
    }

    /// <summary>Return saturation as defined for HSV.</summary>
    public float SaturationHSV()
    {
        Bounds(out var min, out var max, true);
        return SaturationHSV(min, max);
    }

    /// <summary>Return value as defined for HSV: largest value of the RGB components. Equivalent to calling MinRGB().</summary>
    public float Value() => MaxRGB();

    /// <summary>Convert single component of the color from gamma to linear space.</summary>
    public static float ConvertGammaToLinear(float value)
    {
        if (value <= 0.04045f)
            return value / 12.92f;
        if (value < 1.0f)
            return MathF.Pow((value + 0.055f) / 1.055f, 2.4f);
        return MathF.Pow(value, 2.2f);
    }

    /// <summary>Convert single component of the color from linear to gamma space.</summary>
    public static float ConvertLinearToGamma(float value)
    {
        if (value <= 0.0f)
            return 0.0f;
        if (value <= 0.0031308f)
            return 12.92f * value;
        if (value < 1.0f)
            return 1.055f * MathF.Pow(value, 0.4166667f) - 0.055f;
        return MathF.Pow(value, 0.45454545f);
    }

    /// <summary>Convert color from gamma to linear space.</summary>
    public Color GammaToLinear() =>
        new(ConvertGammaToLinear(R), ConvertGammaToLinear(G), ConvertGammaToLinear(B), A);

    /// <summary>Convert color from linear to gamma space.</summary>
    public Color LinearToGamma() =>
        new(ConvertLinearToGamma(R), ConvertLinearToGamma(G), ConvertLinearToGamma(B), A);

    /// <summary>Return lightness as defined for HSL: average of the largest and smallest values of the RGB components.</summary>
    public float Lightness()
    {
        Bounds(out var min, out var max, true);
        return (max + min) * 0.5f;
    }

    /// <summary>Stores the values of least and greatest RGB component at specified pointer addresses, optionally clipping those values to [0, 1] range.</summary>
    public void Bounds(out float min, out float max, bool clipped = false)
    {
        if (R > G)
        {
            if (G > B)
            {
                max = R;
                min = B;
            }
            else
            {
                max = R > B ? R : B;
                min = G;
            }
        }
        else
        {
            if (B > G)
            {
                max = B;
                min = R;
            }
            else
            {
                max = G;
                min = R < B ? R : B;
            }
        }

        if (clipped)
        {
            max = max > 1.0f ? 1.0f : max < 0.0f ? 0.0f : max;
            min = min > 1.0f ? 1.0f : min < 0.0f ? 0.0f : min;
        }
    }

    /// <summary>Return the largest value of the RGB components.</summary>
    public float MaxRGB()
    {
        if (R > G)
            return R > B ? R : B;
        return G > B ? G : B;
    }

    /// <summary>Return the smallest value of the RGB components.</summary>
    public float MinRGB()
    {
        if (R < G)
            return R < B ? R : B;
        return G < B ? G : B;
    }

    /// <summary>Return range, defined as the difference between the greatest and least RGB component.</summary>
    public float Range()
    {
        Bounds(out var min, out var max);
        return max - min;
    }

    /// <summary>Clip to [0, 1.0] range.</summary>
    public void Clip(bool clipAlpha = false)
    {
        R = R > 1.0f ? 1.0f : R < 0.0f ? 0.0f : R;
        G = G > 1.0f ? 1.0f : G < 0.0f ? 0.0f : G;
        B = B > 1.0f ? 1.0f : B < 0.0f ? 0.0f : B;
        if (clipAlpha)
            A = A > 1.0f ? 1.0f : A < 0.0f ? 0.0f : A;
    }

    /// <summary>Inverts the RGB channels and optionally the alpha channel as well.</summary>
    public void Invert(bool invertAlpha = false)
    {
        R = 1.0f - R;
        G = 1.0f - G;
        B = 1.0f - B;
        if (invertAlpha)
            A = 1.0f - A;
    }

    /// <summary>Return linear interpolation of this color with another color.</summary>
    public Color Lerp(Color rhs, float t)
    {
        var invT = 1.0f - t;
        return new Color(
            R * invT + rhs.R * t,
            G * invT + rhs.G * t,
            B * invT + rhs.B * t,
            A * invT + rhs.A * t);
    }

    /// <summary>Return color with absolute components.</summary>
    public Color Abs() => new(MathF.Abs(R), MathF.Abs(G), MathF.Abs(B), MathF.Abs(A));

    /// <summary>Test for equality with another color with epsilon.</summary>
    public bool Equals(Color rhs, float eps = MathDefs.Epsilon) =>
        MathDefs.Equals(R, rhs.R, eps)
        && MathDefs.Equals(G, rhs.G, eps)
        && MathDefs.Equals(B, rhs.B, eps)
        && MathDefs.Equals(A, rhs.A, eps);

    /// <summary>Return as string.</summary>
    public override string ToString() => string.Format(CultureInfo.InvariantCulture, "{0} {1} {2} {3}", R, G, B, A);

    /// <summary>Return color packed to a 32-bit integer, with B component in the lowest 8 bits. Components are clamped to [0, 1] range.</summary>
    public uint ToUIntArgb() => ToUIntMask(ARGB);

    /// <summary>Return hash value for HashSet &amp; HashMap.</summary>
    public uint ToHash() => ToUInt();

    public bool Equals(Color other) => this == other;

    public override bool Equals(object? obj) => obj is Color other && Equals(other);

    public override int GetHashCode() => (int)ToHash();

    /// <summary>Red value.</summary>
    public float R;
    /// <summary>Green value.</summary>
    public float G;
    /// <summary>Blue value.</summary>
    public float B;
    /// <summary>Alpha value.</summary>
    public float A;

    /// <summary>Opaque white color.</summary>
    public static readonly Color White = new();
    /// <summary>Opaque gray color.</summary>
    public static readonly Color Gray = new(0.5f, 0.5f, 0.5f);
    /// <summary>Opaque black color.</summary>
    public static readonly Color Black = new(0.0f, 0.0f, 0.0f);
    /// <summary>Opaque red color.</summary>
    public static readonly Color Red = new(1.0f, 0.0f, 0.0f);
    /// <summary>Opaque green color.</summary>
    public static readonly Color Green = new(0.0f, 1.0f, 0.0f);
    /// <summary>Opaque blue color.</summary>
    public static readonly Color Blue = new(0.0f, 0.0f, 1.0f);
    /// <summary>Opaque cyan color.</summary>
    public static readonly Color Cyan = new(0.0f, 1.0f, 1.0f);
    /// <summary>Opaque magenta color.</summary>
    public static readonly Color Magenta = new(1.0f, 0.0f, 1.0f);
    /// <summary>Opaque yellow color.</summary>
    public static readonly Color Yellow = new(1.0f, 1.0f, 0.0f);
    /// <summary>Transparent black color (black with no alpha).</summary>
    public static readonly Color TransparentBlack = new(0.0f, 0.0f, 0.0f, 0.0f);
    /// <summary>Color-to-gray factors for color in gamma space.</summary>
    public static readonly Color LuminosityGamma = new(0.299f, 0.587f, 0.114f, 0.0f);
    /// <summary>Color-to-gray factors for color in linear space.</summary>
    public static readonly Color LuminosityLinear = new(0.2126f, 0.7152f, 0.0722f, 0.0f);

    /// <summary>Return hue value given greatest and least RGB component, value-wise.</summary>
    private float Hue(float min, float max)
    {
        var chroma = max - min;
        // If chroma equals zero, hue is undefined
        if (chroma <= MathDefs.Epsilon)
            return 0.0f;

        // Calculate and return hue
        if (MathDefs.Equals(G, max))
            return (B + 2.0f * chroma - R) / (6.0f * chroma);
        if (MathDefs.Equals(B, max))
            return (4.0f * chroma - G + R) / (6.0f * chroma);

        var r = (G - B) / (6.0f * chroma);
        return r < 0.0f ? 1.0f + r : r >= 1.0f ? r - 1.0f : r;
    }

    /// <summary>Return saturation (HSV) given greatest and least RGB component, value-wise.</summary>
    private float SaturationHSV(float min, float max)
    {
        // Avoid div-by-zero: result undefined
        if (max <= MathDefs.Epsilon)
            return 0.0f;

        // Saturation equals chroma:value ratio
        return 1.0f - min / max;
    }

    /// <summary>Return saturation (HSL) given greatest and least RGB component, value-wise.</summary>
    private float SaturationHSL(float min, float max)
    {
        // Avoid div-by-zero: result undefined
        if (max <= MathDefs.Epsilon || min >= 1.0f - MathDefs.Epsilon)
            return 0.0f;

        // Chroma = max - min, lightness = (max + min) * 0.5
        var hl = max + min;
        if (hl <= 1.0f)
            return (max - min) / hl;
        return (min - max) / (hl - 2.0f);
    }

    /// <summary>Calculate and set RGB values. Convenience function used by FromHSV and FromHSL to avoid code duplication.</summary>
    private void FromHCM(float h, float c, float m)
    {
        if (h < 0.0f || h >= 1.0f)
            h -= MathF.Floor(h);

        var hs = h * 6.0f;
        var x = c * (1.0f - MathF.Abs(hs % 2.0f - 1.0f));

        // Reconstruct r', g', b' from hue
        if (hs < 2.0f)
        {
            B = 0.0f;
            if (hs < 1.0f)
            {
                G = x;
                R = c;
            }
            else
            {
                G = c;
                R = x;
            }
        }
        else if (hs < 4.0f)
        {
            R = 0.0f;
            if (hs < 3.0f)
            {
                G = c;
                B = x;
            }
            else
            {
                G = x;
                B = c;
            }
        }
        else
        {
            G = 0.0f;
            if (hs < 5.0f)
            {
                R = x;
                B = c;
            }
            else
            {
                R = c;
                B = x;
            }
        }

        R += m;
        G += m;
        B += m;
    }

    /// <summary>Multiply Color with a scalar.</summary>
    public static Color operator *(float lhs, Color rhs) => rhs * lhs;
}
