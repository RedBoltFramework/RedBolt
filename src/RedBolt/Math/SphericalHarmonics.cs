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

    // C++: explicit SphericalHarmonicsColor9(const Vector3& color)
    // {
    //     static const float factor = 1.0f / (SphericalHarmonics9::cosines[0] * SphericalHarmonics9::factors[0]);
    //     values_[0] = color * factor;
    // }
    public SphericalHarmonicsColor9(Vector3f color)
    {
        var factor = 1.0f / (SphericalHarmonics9.Cosines[0] * SphericalHarmonics9.Factors[0]);
        Values[0] = color * factor;
    }

    // C++: explicit SphericalHarmonicsColor9(const Color& color) : SphericalHarmonicsColor9(color.ToVector3()) {}
    public SphericalHarmonicsColor9(Color color) : this(new Vector3f(color.R, color.G, color.B)) { }

    public SphericalHarmonicsColor9(Vector3f direction, Color color)
    {
        var basis = new SphericalHarmonics9(direction); var rgb = new Vector3f(color.R, color.G, color.B);
        for (var i = 0; i < 9; ++i) Values[i] = rgb * basis.Values[i];
    }

    // C++: SphericalHarmonicsColor9(const Vector3& dir, const Vector3& color)
    // {
    //     const SphericalHarmonics9 sh9{ dir };
    //     for (unsigned i = 0; i < 9; ++i)
    //         values_[i] = color * sh9.values_[i];
    // }
    public SphericalHarmonicsColor9(Vector3f direction, Vector3f color)
    {
        var basis = new SphericalHarmonics9(direction);
        for (var i = 0; i < 9; ++i) Values[i] = color * basis.Values[i];
    }

    // C++: Vector3 Evaluate(const Vector3& dir) const
    // {
    //     const SphericalHarmonics9 sh{ dir.Normalized() };
    //     Vector3 result;
    //     for (unsigned i = 0; i < 9; ++i)
    //         result += SphericalHarmonics9::cosines[i] * sh.values_[i] * values_[i];
    //     return result;
    // }
    // C++ passes dir.Normalized(); C# passes direction directly - SphericalHarmonics9 constructor expects normalized direction
    public Vector3f Evaluate(Vector3f direction)
    {
        var basis = new SphericalHarmonics9(direction.NormalizedOrDefault()); var result = Vector3f.ZERO;
        for (var i = 0; i < 9; ++i) result += Values[i] * (SphericalHarmonics9.Cosines[i] * basis.Values[i]);
        return result;
    }

    // C++: Vector3 EvaluateAverage() const
    // {
    //     Vector3 result;
    //     result += SphericalHarmonics9::cosines[0] * SphericalHarmonics9::factors[0] * values_[0];
    //     result -= SphericalHarmonics9::cosines[6] * SphericalHarmonics9::factors[6] * values_[6];
    //     return result;
    // }
    public Vector3f EvaluateAverage()
    {
        var result = Vector3f.ZERO;
        result += SphericalHarmonics9.Cosines[0] * SphericalHarmonics9.Factors[0] * Values[0];
        result -= SphericalHarmonics9.Cosines[6] * SphericalHarmonics9.Factors[6] * Values[6];
        return result;
    }

    // C++: SphericalHarmonicsColor9& operator +=(const SphericalHarmonicsColor9& rhs)
    // {
    //     for (unsigned i = 0; i < 9; ++i)
    //         values_[i] += rhs.values_[i];
    //     return *this;
    // }
    public void Add(SphericalHarmonicsColor9 rhs) { for (var i = 0; i < 9; ++i) Values[i] += rhs.Values[i]; }

    // C++: SphericalHarmonicsColor9& operator *=(float rhs)
    // {
    //     for (unsigned i = 0; i < 9; ++i)
    //         values_[i] *= rhs;
    //     return *this;
    // }
    public void Multiply(float scale) { for (var i = 0; i < 9; ++i) Values[i] *= scale; }

    // C++: SphericalHarmonicsColor9 operator *(float rhs) const
    // {
    //     SphericalHarmonicsColor9 result = *this;
    //     result *= rhs;
    //     return result;
    // }
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

// C++ SphericalHarmonicsDot9 is a struct with precomputed dot-product fields:
// struct SphericalHarmonicsDot9
// {
//     Vector4 Ar_; // Dot product with (Nx, Ny, Nz, 1), red channel
//     Vector4 Ag_; // Dot product with (Nx, Ny, Nz, 1), green channel
//     Vector4 Ab_; // Dot product with (Nx, Ny, Nz, 1), blue channel
//     Vector4 Br_; // Dot product with (Nx*Ny, Ny*Nz, Nz*Nz, Nz*Nx), red channel
//     Vector4 Bg_; // Dot product with (Nx*Ny, Ny*Nz, Nz*Nz, Nz*Nx), green channel
//     Vector4 Bb_; // Dot product with (Nx*Ny, Ny*Nz, Nz*Nz, Nz*Nx), blue channel
//     Vector4 C_;  // Color, multiplied by (Nx*Nx - Ny*Ny). W is always 1.0f
// };
// TODO: port - SphericalHarmonicsDot9 deviates from C++. The C++ version
// uses precomputed Vector4 dot-product fields for optimized evaluation.
// The C# version wraps SphericalHarmonicsColor9 which is not equivalent.
// Missing: GetDebugColor(), SerializeValue.
public sealed class SphericalHarmonicsDot9
{
    // C++: Precomputed Vector4 fields (see above)
    // C# deviation: wraps harmonics + constant offset instead
    private SphericalHarmonicsColor9 _harmonics;
    private Vector3f _constantOffset;

    public SphericalHarmonicsDot9(SphericalHarmonicsColor9 harmonics)
    {
        _harmonics = harmonics;
    }

    public SphericalHarmonicsDot9()
        : this(new SphericalHarmonicsColor9())
    {
    }

    // C++: explicit SphericalHarmonicsDot9(const Vector3& color)
    // {
    //     Ar_.w_ = color.x_;
    //     Ag_.w_ = color.y_;
    //     Ab_.w_ = color.z_;
    // }
    public SphericalHarmonicsDot9(Vector3f color)
        : this(new SphericalHarmonicsColor9())
    {
        _constantOffset = color;
    }

    public SphericalHarmonicsDot9(Color color)
        : this(color.ToVector3())
    {
    }

    // C++: Vector3 Evaluate(const Vector3& dir) const - uses dot-product optimization with precomputed fields
    public Vector3f Evaluate(Vector3f direction) => _harmonics.Evaluate(direction) + _constantOffset;

    // C++: Vector3 EvaluateAverage() const { return { Ar_.w_, Ag_.w_, Ab_.w_ }; }
    // C# deviation: EvaluateAverage not directly supported; would return _constantOffset

    public SphericalHarmonicsDot9 Clone()
    {
        var result = new SphericalHarmonicsDot9();
        for (var i = 0; i < _harmonics.Values.Length; ++i)
            result._harmonics.Values[i] = _harmonics.Values[i];
        result._constantOffset = _constantOffset;
        return result;
    }

    // C++: operator+=(const SphericalHarmonicsDot9& rhs) - adds all 7 Vector4 fields
    public void Add(SphericalHarmonicsDot9 value)
    {
        _harmonics.AddScaled(value._harmonics, 1.0f);
        _constantOffset += value._constantOffset;
    }

    // C++: operator+=(const Vector3& rhs) - adds to Ar_.w_, Ag_.w_, Ab_.w_
    public void Add(Vector3f value) => _constantOffset += value;

    // C++: operator*=(float rhs) - scales all 7 Vector4 fields
    public void Scale(float scale)
    {
        _harmonics.Scale(scale);
        _constantOffset *= scale;
    }

    // C++: Color GetDebugColor() const { ... return static_cast<Color>(average).LinearToGamma(); }
    // TODO: port - C++ code:
    // Color GetDebugColor() const
    // {
    //     const Vector3 average = EvaluateAverage();
    //     return static_cast<Color>(average).LinearToGamma();
    // }
}
