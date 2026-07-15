namespace RedBolt;

public readonly struct SphericalHarmonics9
{
    public static readonly float[] Factors = [0.282095f, 0.488603f, 0.488603f, 0.488603f, 1.092548f, 1.092548f, 0.315392f, 1.092548f, 0.546274f];
    public static readonly float[] Cosines = [1f, 2f / 3f, 2f / 3f, 2f / 3f, .25f, .25f, .25f, .25f, .25f];
    public readonly float[] Values;
    public SphericalHarmonics9(Vector3f d)
    {
        Values = new float[9];
        Values[0] = Factors[0]; Values[1] = Factors[1] * d.Y; Values[2] = Factors[2] * d.Z; Values[3] = Factors[3] * d.X;
        Values[4] = Factors[4] * d.X * d.Y; Values[5] = Factors[5] * d.Y * d.Z; Values[6] = Factors[6] * (3 * d.Z * d.Z - 1);
        Values[7] = Factors[7] * d.X * d.Z; Values[8] = Factors[8] * (d.X * d.X - d.Y * d.Y);
    }
}

public struct SphericalHarmonicsColor9
{
    public Vector3f[] Values = new Vector3f[9];

    public SphericalHarmonicsColor9() { }

    public SphericalHarmonicsColor9(Vector3f color)
    {
        var factor = 1.0f / (SphericalHarmonics9.Cosines[0] * SphericalHarmonics9.Factors[0]);
        Values[0] = color * factor;
    }

    public SphericalHarmonicsColor9(Color color) : this(new Vector3f(color.R, color.G, color.B)) { }

    public SphericalHarmonicsColor9(Vector3f direction, Color color)
    {
        var basis = new SphericalHarmonics9(direction); var rgb = new Vector3f(color.R, color.G, color.B);
        for (var i = 0; i < 9; ++i) Values[i] = rgb * basis.Values[i];
    }

    public SphericalHarmonicsColor9(Vector3f direction, Vector3f color)
    {
        var basis = new SphericalHarmonics9(direction);
        for (var i = 0; i < 9; ++i) Values[i] = color * basis.Values[i];
    }

    public Vector3f Evaluate(Vector3f direction)
    {
        var basis = new SphericalHarmonics9(direction.NormalizedOrDefault()); var result = Vector3f.ZERO;
        for (var i = 0; i < 9; ++i) result += Values[i] * (SphericalHarmonics9.Cosines[i] * basis.Values[i]);
        return result;
    }

    public Vector3f EvaluateAverage()
    {
        var result = Vector3f.ZERO;
        result += SphericalHarmonics9.Cosines[0] * SphericalHarmonics9.Factors[0] * Values[0];
        result -= SphericalHarmonics9.Cosines[6] * SphericalHarmonics9.Factors[6] * Values[6];
        return result;
    }

    public void Add(SphericalHarmonicsColor9 rhs) { for (var i = 0; i < 9; ++i) Values[i] += rhs.Values[i]; }

    public void Multiply(float scale) { for (var i = 0; i < 9; ++i) Values[i] *= scale; }

    public SphericalHarmonicsColor9 Scaled(float scale) { var result = Clone(); result.Multiply(scale); return result; }

    public SphericalHarmonicsColor9 Clone()
    {
        var result = new SphericalHarmonicsColor9();
        for (var i = 0; i < 9; ++i) result.Values[i] = Values[i];
        return result;
    }

    // Retained for backward compatibility with existing C# callers:
    // C++ equivalent: operator+=
    public void AddScaled(SphericalHarmonicsColor9 value, float scale) { for (var i = 0; i < 9; ++i) Values[i] += value.Values[i] * scale; }
    // C++ equivalent: operator*=
    public void Scale(float scale) { Multiply(scale); }
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

    public SphericalHarmonicsDot9(SphericalHarmonicsColor9 harmonics)
    {
        var sh = harmonics.Clone();
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

        C = new Vector4f(sh.Values[8], 1.0f);
    }

    public SphericalHarmonicsDot9(Vector3f color)
    {
        Ar = new Vector4f(0.0f, 0.0f, 0.0f, color.X);
        Ag = new Vector4f(0.0f, 0.0f, 0.0f, color.Y);
        Ab = new Vector4f(0.0f, 0.0f, 0.0f, color.Z);
        Br = default;
        Bg = default;
        Bb = default;
        C = default;
    }

    public SphericalHarmonicsDot9(Color color)
        : this(color.ToVector3())
    {
    }

    public Vector3f Evaluate(Vector3f direction)
    {
        var a = new Vector4f(direction, 1.0f);
        var b = new Vector4f(direction.X * direction.Y, direction.Y * direction.Z, direction.Z * direction.Z, direction.Z * direction.X);
        var c = direction.X * direction.X - direction.Y * direction.Y;

        var result = new Vector3f(Ar.DotProduct(a), Ag.DotProduct(a), Ab.DotProduct(a));
        result.X += Br.DotProduct(b);
        result.Y += Bg.DotProduct(b);
        result.Z += Bb.DotProduct(b);
        result += C.ToVector3() * c;
        return result;
    }

    public Vector3f EvaluateAverage() => new(Ar.W, Ag.W, Ab.W);

    public SphericalHarmonicsDot9 Clone()
        => this;

    public void Add(SphericalHarmonicsDot9 value)
    {
        Ar += value.Ar;
        Ag += value.Ag;
        Ab += value.Ab;
        Br += value.Br;
        Bg += value.Bg;
        Bb += value.Bb;
        C += value.C;
    }

    public void Add(Vector3f value)
    {
        Ar.W += value.X;
        Ag.W += value.Y;
        Ab.W += value.Z;
    }

    public void Scale(float scale)
    {
        Ar *= scale;
        Ag *= scale;
        Ab *= scale;
        Br *= scale;
        Bg *= scale;
        Bb *= scale;
        C *= scale;
    }

    public SphericalHarmonicsDot9 Scaled(float scale)
    {
        var result = this;
        result.Scale(scale);
        return result;
    }

    public Color GetDebugColor() => new Color(EvaluateAverage()).LinearToGamma();
}
