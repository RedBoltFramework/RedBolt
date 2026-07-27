using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace RedBolt.Math;

[InlineArray(9)]
[StructLayout(LayoutKind.Sequential)]
public struct SphericalHarmonicsFloat9
{
    private float _element0;
}

[InlineArray(9)]
[StructLayout(LayoutKind.Sequential)]
public struct SphericalHarmonicsVector9
{
    private Vector3f _element0;
}

/// <summary>Spherical harmonics scalar coefficients, 3 bands.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct SphericalHarmonics9
{
    /// <summary>Array of factors.</summary>
    public static ReadOnlySpan<float> Factors =>
    [
        0.282095f,
        0.488603f,
        0.488603f,
        0.488603f,
        1.092548f,
        1.092548f,
        0.315392f,
        1.092548f,
        0.546274f
    ];

    /// <summary>Array of cosines.</summary>
    public static ReadOnlySpan<float> Cosines =>
    [
        1.0f,
        2.0f / 3.0f,
        2.0f / 3.0f,
        2.0f / 3.0f,
        0.25f,
        0.25f,
        0.25f,
        0.25f,
        0.25f
    ];

    /// <summary>Construct default.</summary>
    public SphericalHarmonics9()
    {
        Values = default;
    }

    /// <summary>Construct SH9 coefficients from given <em>normalized</em> direction.</summary>
    public SphericalHarmonics9(Vector3f dir)
    {
        Values = default;
        var factors = Factors;
        Values[0] = factors[0];

        Values[1] = factors[1] * dir.Y;
        Values[2] = factors[2] * dir.Z;
        Values[3] = factors[3] * dir.X;

        Values[4] = factors[4] * dir.X * dir.Y;
        Values[5] = factors[5] * dir.Y * dir.Z;
        Values[6] = factors[6] * (3.0f * dir.Z * dir.Z - 1.0f);
        Values[7] = factors[7] * dir.X * dir.Z;
        Values[8] = factors[8] * (dir.X * dir.X - dir.Y * dir.Y);
    }

    /// <summary>Coefficients.</summary>
    public SphericalHarmonicsFloat9 Values;

    /// <summary>Zero harmonics.</summary>
    public static SphericalHarmonics9 Zero => default;
}

/// <summary>Spherical harmonics color coefficients, 3 bands. Use Vector3 instead of Color to save memory.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct SphericalHarmonicsColor9
{
    /// <summary>Construct default.</summary>
    public SphericalHarmonicsColor9()
    {
        Values = default;
    }

    /// <summary>Construct SH9 coefficients from given color.</summary>
    public SphericalHarmonicsColor9(Vector3f color)
    {
        Values = default;
        var factor = 1.0f / (SphericalHarmonics9.Cosines[0] * SphericalHarmonics9.Factors[0]);
        Values[0] = color * factor;
    }

    /// <summary>Construct SH9 coefficients from given color.</summary>
    public SphericalHarmonicsColor9(Color color)
        : this(color.ToVector3())
    {
    }

    /// <summary>Construct SH9 coefficients from given <em>normalized</em> direction and color.</summary>
    public SphericalHarmonicsColor9(Vector3f dir, Vector3f color)
    {
        Values = default;
        var sh9 = new SphericalHarmonics9(dir);
        for (var i = 0; i < 9; ++i)
            Values[i] = color * sh9.Values[i];
    }

    /// <summary>Construct SH9 coefficients from given <em>normalized</em> direction and color.</summary>
    public SphericalHarmonicsColor9(Vector3f dir, Color color)
        : this(dir, color.ToVector3())
    {
    }

    /// <summary>Evaluate at given direction.</summary>
    public readonly Vector3f Evaluate(Vector3f dir)
    {
        var sh = new SphericalHarmonics9(dir.Normalized);

        var result = new Vector3f();
        for (var i = 0; i < 9; ++i)
            result += SphericalHarmonics9.Cosines[i] * sh.Values[i] * Values[i];

        return result;
    }

    /// <summary>Evaluate average.</summary>
    public readonly Vector3f EvaluateAverage()
    {
        var result = new Vector3f();
        result += SphericalHarmonics9.Cosines[0] * SphericalHarmonics9.Factors[0] * Values[0];
        result -= SphericalHarmonics9.Cosines[6] * SphericalHarmonics9.Factors[6] * Values[6];
        return result;
    }

    /// <summary>Accumulate spherical harmonics (inplace).</summary>
    public static SphericalHarmonicsColor9 operator +(SphericalHarmonicsColor9 lhs, SphericalHarmonicsColor9 rhs)
    {
        for (var i = 0; i < 9; ++i)
            lhs.Values[i] += rhs.Values[i];
        return lhs;
    }

    /// <summary>Scale spherical harmonics.</summary>
    /// <remarks>Compound assignment scales spherical harmonics in place.</remarks>
    public static SphericalHarmonicsColor9 operator *(SphericalHarmonicsColor9 lhs, float rhs)
    {
        for (var i = 0; i < 9; ++i)
            lhs.Values[i] *= rhs;
        return lhs;
    }

    /// <summary>Coefficients.</summary>
    public SphericalHarmonicsVector9 Values;

    /// <summary>Zero harmonics.</summary>
    public static SphericalHarmonicsColor9 Zero => default;
}

/// <summary>Spherical harmonics, optimized for dot products.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct SphericalHarmonicsDot9
{
    /// <summary>Construct default.</summary>
    public SphericalHarmonicsDot9()
    {
        this = default;
    }

    /// <summary>Construct from color in linear color space.</summary>
    public SphericalHarmonicsDot9(Vector3f color)
    {
        this = default;
        Ar.W = color.X;
        Ag.W = color.Y;
        Ab.W = color.Z;
    }

    /// <summary>Construct from color in linear color space.</summary>
    public SphericalHarmonicsDot9(Color color)
        : this(color.ToVector3())
    {
    }

    /// <summary>Construct from spherical harmonics.</summary>
    public SphericalHarmonicsDot9(SphericalHarmonicsColor9 sh)
    {
        this = default;

        // Precompute everything
        for (var i = 0; i < 9; ++i)
            sh.Values[i] *= SphericalHarmonics9.Cosines[i] * SphericalHarmonics9.Factors[i];

        // Repack 6-th component (3 * z * z - 1)
        sh.Values[0] -= sh.Values[6];
        sh.Values[6] *= 3.0f;

        // (Nx, Ny, Nz, 1)
        Ar = new Vector4f(sh.Values[3].X, sh.Values[1].X, sh.Values[2].X, sh.Values[0].X);
        Ag = new Vector4f(sh.Values[3].Y, sh.Values[1].Y, sh.Values[2].Y, sh.Values[0].Y);
        Ab = new Vector4f(sh.Values[3].Z, sh.Values[1].Z, sh.Values[2].Z, sh.Values[0].Z);

        // (Nx*Ny, Ny*Nz, Nz*Nz, Nz*Nx)
        Br = new Vector4f(sh.Values[4].X, sh.Values[5].X, sh.Values[6].X, sh.Values[7].X);
        Bg = new Vector4f(sh.Values[4].Y, sh.Values[5].Y, sh.Values[6].Y, sh.Values[7].Y);
        Bb = new Vector4f(sh.Values[4].Z, sh.Values[5].Z, sh.Values[6].Z, sh.Values[7].Z);

        // Nx*Nx - Ny*Ny
        C = new Vector4f(sh.Values[8], 1.0f);
    }

    /// <summary>Evaluate at given direction.</summary>
    public readonly Vector3f Evaluate(Vector3f dir)
    {
        var a = new Vector4f(dir, 1.0f);
        var b = new Vector4f(dir.X * dir.Y, dir.Y * dir.Z, dir.Z * dir.Z, dir.Z * dir.X);
        var c = dir.X * dir.X - dir.Y * dir.Y;

        var result = new Vector3f();
        result.X = Ar.DotProduct(a);
        result.Y = Ag.DotProduct(a);
        result.Z = Ab.DotProduct(a);
        result.X += Br.DotProduct(b);
        result.Y += Bg.DotProduct(b);
        result.Z += Bb.DotProduct(b);
        result += C.ToVector3() * c;
        return result;
    }

    /// <summary>Evaluate average.</summary>
    public readonly Vector3f EvaluateAverage()
    {
        return new Vector3f(Ar.W, Ag.W, Ab.W);
    }

    /// <summary>Accumulate spherical harmonics (inplace).</summary>
    public static SphericalHarmonicsDot9 operator +(SphericalHarmonicsDot9 lhs, SphericalHarmonicsDot9 rhs)
    {
        lhs.Ar += rhs.Ar;
        lhs.Ag += rhs.Ag;
        lhs.Ab += rhs.Ab;
        lhs.Br += rhs.Br;
        lhs.Bg += rhs.Bg;
        lhs.Bb += rhs.Bb;
        lhs.C += rhs.C;
        return lhs;
    }

    /// <summary>Accumulate spherical harmonics with constant value.</summary>
    public static SphericalHarmonicsDot9 operator +(SphericalHarmonicsDot9 lhs, Vector3f rhs)
    {
        lhs.Ar.W += rhs.X;
        lhs.Ag.W += rhs.Y;
        lhs.Ab.W += rhs.Z;
        return lhs;
    }

    /// <summary>Scale spherical harmonics.</summary>
    /// <remarks>Compound assignment scales spherical harmonics in place.</remarks>
    public static SphericalHarmonicsDot9 operator *(SphericalHarmonicsDot9 lhs, float rhs)
    {
        lhs.Ar *= rhs;
        lhs.Ag *= rhs;
        lhs.Ab *= rhs;
        lhs.Br *= rhs;
        lhs.Bg *= rhs;
        lhs.Bb *= rhs;
        lhs.C *= rhs;
        return lhs;
    }

    /// <summary>Return color for SH debug rendering.</summary>
    public readonly Color GetDebugColor()
    {
        var average = EvaluateAverage();
        return new Color(average).LinearToGamma();
    }

    /// <summary>Dot product with (Nx, Ny, Nz, 1), red channel.</summary>
    public Vector4f Ar;

    /// <summary>Dot product with (Nx, Ny, Nz, 1), green channel.</summary>
    public Vector4f Ag;

    /// <summary>Dot product with (Nx, Ny, Nz, 1), blue channel.</summary>
    public Vector4f Ab;

    /// <summary>Dot product with (Nx*Ny, Ny*Nz, Nz*Nz, Nz*Nx), red channel.</summary>
    public Vector4f Br;

    /// <summary>Dot product with (Nx*Ny, Ny*Nz, Nz*Nz, Nz*Nx), green channel.</summary>
    public Vector4f Bg;

    /// <summary>Dot product with (Nx*Ny, Ny*Nz, Nz*Nz, Nz*Nx), blue channel.</summary>
    public Vector4f Bb;

    /// <summary>Color, multiplied by (Nx*Nx - Ny*Ny). W is always 1.0f.</summary>
    public Vector4f C;

    /// <summary>Zero harmonics.</summary>
    public static SphericalHarmonicsDot9 Zero => default;
}
