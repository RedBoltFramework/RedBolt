using System.Runtime.CompilerServices;

namespace RedBolt;

[InlineArray(9)]
public struct SphericalHarmonicsFloat9
{
    private float _element0;
}

[InlineArray(9)]
public struct SphericalHarmonicsVector9
{
    private Vector3f _element0;
}

public struct SphericalHarmonics9
{
    public static ReadOnlySpan<float> Factors =>
    [0.282095f, 0.488603f, 0.488603f, 0.488603f, 1.092548f, 1.092548f, 0.315392f, 1.092548f, 0.546274f];

    public static ReadOnlySpan<float> Cosines =>
    [1.0f, 2.0f / 3.0f, 2.0f / 3.0f, 2.0f / 3.0f, 0.25f, 0.25f, 0.25f, 0.25f, 0.25f];

    public SphericalHarmonicsFloat9 Values;

    public SphericalHarmonics9(Vector3f dir)
    {
        this = default;
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

    public static SphericalHarmonics9 Zero => default;
}

public struct SphericalHarmonicsColor9
{
    public SphericalHarmonicsVector9 Values;

    public SphericalHarmonicsColor9(Vector3f color)
    {
        this = default;
        Values[0] = color * (1.0f / (SphericalHarmonics9.Cosines[0] * SphericalHarmonics9.Factors[0]));
    }

    public SphericalHarmonicsColor9(Color color) : this(color.ToVector3())
    {
    }

    public SphericalHarmonicsColor9(Vector3f dir, Vector3f color)
    {
        this = default;
        var sh = new SphericalHarmonics9(dir);
        for (var i = 0; i < 9; ++i)
            Values[i] = color * sh.Values[i];
    }

    public SphericalHarmonicsColor9(Vector3f dir, Color color) : this(dir, color.ToVector3())
    {
    }

    public readonly Vector3f Evaluate(Vector3f dir)
    {
        var sh = new SphericalHarmonics9(dir.Normalized);
        var result = Vector3f.Zero;
        for (var i = 0; i < 9; ++i)
            result += SphericalHarmonics9.Cosines[i] * sh.Values[i] * Values[i];
        return result;
    }

    public readonly Vector3f EvaluateAverage() =>
        SphericalHarmonics9.Cosines[0] * SphericalHarmonics9.Factors[0] * Values[0]
        - SphericalHarmonics9.Cosines[6] * SphericalHarmonics9.Factors[6] * Values[6];

    public static SphericalHarmonicsColor9 operator +(SphericalHarmonicsColor9 lhs, SphericalHarmonicsColor9 rhs)
    {
        for (var i = 0; i < 9; ++i)
            lhs.Values[i] += rhs.Values[i];
        return lhs;
    }

    public static SphericalHarmonicsColor9 operator *(SphericalHarmonicsColor9 lhs, float rhs)
    {
        for (var i = 0; i < 9; ++i)
            lhs.Values[i] *= rhs;
        return lhs;
    }

    public static SphericalHarmonicsColor9 Zero => default;
}

public struct SphericalHarmonicsDot9
{
    public Vector4f Ar;
    public Vector4f Ag;
    public Vector4f Ab;
    public Vector4f Br;
    public Vector4f Bg;
    public Vector4f Bb;
    public Vector4f C;

    public SphericalHarmonicsDot9(Vector3f color)
    {
        this = default;
        Ar.W = color.X;
        Ag.W = color.Y;
        Ab.W = color.Z;
    }

    public SphericalHarmonicsDot9(Color color) : this(color.ToVector3())
    {
    }

    public SphericalHarmonicsDot9(SphericalHarmonicsColor9 sh)
    {
        this = default;
        for (var i = 0; i < 9; ++i)
            sh.Values[i] *= SphericalHarmonics9.Cosines[i] * SphericalHarmonics9.Factors[i];
        sh.Values[0] -= sh.Values[6];
        sh.Values[6] *= 3.0f;
        Ar = new Vector4f(sh.Values[3].X, sh.Values[1].X, sh.Values[2].X, sh.Values[0].X);
        Ag = new Vector4f(sh.Values[3].Y, sh.Values[1].Y, sh.Values[2].Y, sh.Values[0].Y);
        Ab = new Vector4f(sh.Values[3].Z, sh.Values[1].Z, sh.Values[2].Z, sh.Values[0].Z);
        Br = new Vector4f(sh.Values[4].X, sh.Values[5].X, sh.Values[6].X, sh.Values[7].X);
        Bg = new Vector4f(sh.Values[4].Y, sh.Values[5].Y, sh.Values[6].Y, sh.Values[7].Y);
        Bb = new Vector4f(sh.Values[4].Z, sh.Values[5].Z, sh.Values[6].Z, sh.Values[7].Z);
        C = new Vector4f(sh.Values[8].X, sh.Values[8].Y, sh.Values[8].Z, 1.0f);
    }

    public readonly Vector3f Evaluate(Vector3f dir)
    {
        var a = new Vector4f(dir.X, dir.Y, dir.Z, 1.0f);
        var b = new Vector4f(dir.X * dir.Y, dir.Y * dir.Z, dir.Z * dir.Z, dir.Z * dir.X);
        var c = dir.X * dir.X - dir.Y * dir.Y;
        return new Vector3f(Dot(Ar, a) + Dot(Br, b), Dot(Ag, a) + Dot(Bg, b), Dot(Ab, a) + Dot(Bb, b))
            + new Vector3f(C.X, C.Y, C.Z) * c;
    }

    public readonly Vector3f EvaluateAverage() => new(Ar.W, Ag.W, Ab.W);

    public static SphericalHarmonicsDot9 operator +(SphericalHarmonicsDot9 lhs, SphericalHarmonicsDot9 rhs)
    {
        lhs.Ar += rhs.Ar; lhs.Ag += rhs.Ag; lhs.Ab += rhs.Ab;
        lhs.Br += rhs.Br; lhs.Bg += rhs.Bg; lhs.Bb += rhs.Bb; lhs.C += rhs.C;
        return lhs;
    }

    public static SphericalHarmonicsDot9 operator +(SphericalHarmonicsDot9 lhs, Vector3f rhs)
    {
        lhs.Ar.W += rhs.X; lhs.Ag.W += rhs.Y; lhs.Ab.W += rhs.Z;
        return lhs;
    }

    public static SphericalHarmonicsDot9 operator *(SphericalHarmonicsDot9 lhs, float rhs)
    {
        lhs.Ar *= rhs; lhs.Ag *= rhs; lhs.Ab *= rhs;
        lhs.Br *= rhs; lhs.Bg *= rhs; lhs.Bb *= rhs; lhs.C *= rhs;
        return lhs;
    }

    public readonly Color GetDebugColor()
    {
        var average = EvaluateAverage();
        return new Color(average.X, average.Y, average.Z).LinearToGamma();
    }

    public static SphericalHarmonicsDot9 Zero => default;

    private static float Dot(Vector4f lhs, Vector4f rhs) =>
        lhs.X * rhs.X + lhs.Y * rhs.Y + lhs.Z * rhs.Z + lhs.W * rhs.W;
}
