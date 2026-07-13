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

public sealed class SphericalHarmonicsColor9
{
    public Vector3f[] Values { get; } = new Vector3f[9];
    public SphericalHarmonicsColor9() { }
    public SphericalHarmonicsColor9(Vector3f direction, Color color)
    {
        var basis = new SphericalHarmonics9(direction); var rgb = new Vector3f(color.R, color.G, color.B);
        for (var i = 0; i < 9; ++i) Values[i] = rgb * basis.Values[i];
    }
    public Vector3f Evaluate(Vector3f direction)
    {
        var basis = new SphericalHarmonics9(direction.NormalizedOrDefault()); var result = Vector3f.ZERO;
        for (var i = 0; i < 9; ++i) result += Values[i] * (SphericalHarmonics9.Cosines[i] * basis.Values[i]);
        return result;
    }
    public void AddScaled(SphericalHarmonicsColor9 value, float scale) { for (var i = 0; i < 9; ++i) Values[i] += value.Values[i] * scale; }
    public void Scale(float scale) { for (var i = 0; i < 9; ++i) Values[i] *= scale; }
}

public sealed class SphericalHarmonicsDot9
{
    private readonly SphericalHarmonicsColor9 _harmonics;
    private Vector3f _constantOffset;

    public SphericalHarmonicsDot9(SphericalHarmonicsColor9 harmonics)
    {
        _harmonics = harmonics;
    }

    public SphericalHarmonicsDot9()
        : this(new SphericalHarmonicsColor9())
    {
    }

    public SphericalHarmonicsDot9(Color color)
        : this(color.ToVector3())
    {
    }

    public SphericalHarmonicsDot9(Vector3f color)
        : this(new SphericalHarmonicsColor9())
    {
        _constantOffset = color;
    }

    public Vector3f Evaluate(Vector3f direction) => _harmonics.Evaluate(direction) + _constantOffset;

    public SphericalHarmonicsDot9 Clone()
    {
        var result = new SphericalHarmonicsDot9();
        for (var i = 0; i < _harmonics.Values.Length; ++i)
            result._harmonics.Values[i] = _harmonics.Values[i];
        result._constantOffset = _constantOffset;
        return result;
    }

    public void Add(SphericalHarmonicsDot9 value)
    {
        _harmonics.AddScaled(value._harmonics, 1.0f);
        _constantOffset += value._constantOffset;
    }

    public void Add(Vector3f value) => _constantOffset += value;

    public void Scale(float scale)
    {
        _harmonics.Scale(scale);
        _constantOffset *= scale;
    }
}
