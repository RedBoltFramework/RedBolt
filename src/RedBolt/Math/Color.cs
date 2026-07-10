using System.Globalization;
using System.Runtime.InteropServices;

namespace RedBolt;

[StructLayout(LayoutKind.Sequential)]
public struct Color : IEquatable<Color>, IApproximateEquatable<Color>
{
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

    public static readonly Color White = new(1.0f, 1.0f, 1.0f, 1.0f);
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

    public bool Equals(Color other) => ApproximatelyEquivalent(other);
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
