namespace RedBolt.Math;

using SystemMath = global::System.Math;

/// <summary>Perlin noise generator.</summary>
public class PerlinNoise : ICloneable
{
    /// <summary>Number of permutations.</summary>
    public const uint NumPer = 256;

    /// <summary>Construct and initialize from random generator.</summary>
    public PerlinNoise(RandomEngine engine)
    {
        for (var i = 0; i < (int)NumPer; ++i)
            _p[i] = i;
        engine.Shuffle(_p.AsSpan(0, (int)NumPer));
        Array.Copy(_p, 0, _p, (int)NumPer, (int)NumPer);
    }

    public object Clone()
    {
        var clone = (PerlinNoise)MemberwiseClone();
        clone._p = (int[])_p.Clone();
        return clone;
    }

    /// <summary>Return noise value as double.</summary>
    public double GetDouble(double x, double y, double z, int repeat = (int)NumPer)
    {
        repeat = SystemMath.Clamp(repeat, 0, (int)NumPer);

        var hX = MathDefs.AbsMod((int)SystemMath.Floor(x), repeat);
        var hY = MathDefs.AbsMod((int)SystemMath.Floor(y), repeat);
        var hZ = MathDefs.AbsMod((int)SystemMath.Floor(z), repeat);

        x -= SystemMath.Floor(x);
        y -= SystemMath.Floor(y);
        z -= SystemMath.Floor(z);

        var u = Fade(x);
        var v = Fade(y);
        var w = Fade(z);

        var aaa = Grad(_p[_p[_p[hX] + hY] + hZ], x, y, z);
        var aab = Grad(_p[_p[_p[Inc(hX, repeat)] + hY] + hZ], x - 1, y, z);
        var aba = Grad(_p[_p[_p[hX] + Inc(hY, repeat)] + hZ], x, y - 1, z);
        var abb = Grad(_p[_p[_p[Inc(hX, repeat)] + Inc(hY, repeat)] + hZ], x - 1, y - 1, z);
        var baa = Grad(_p[_p[_p[hX] + hY] + Inc(hZ, repeat)], x, y, z - 1);
        var bab = Grad(_p[_p[_p[Inc(hX, repeat)] + hY] + Inc(hZ, repeat)], x - 1, y, z - 1);
        var bba = Grad(_p[_p[_p[hX] + Inc(hY, repeat)] + Inc(hZ, repeat)], x, y - 1, z - 1);
        var bbb = Grad(_p[_p[_p[Inc(hX, repeat)] + Inc(hY, repeat)] + Inc(hZ, repeat)], x - 1, y - 1, z - 1);

        var aa = MathDefs.Lerp(aaa, aab, u);
        var ab = MathDefs.Lerp(aba, abb, u);
        var ba = MathDefs.Lerp(baa, bab, u);
        var bb = MathDefs.Lerp(bba, bbb, u);

        var a = MathDefs.Lerp(aa, ab, v);
        var b = MathDefs.Lerp(ba, bb, v);

        var res = MathDefs.Lerp(a, b, w);
        return (res + 1.0) / 2.0;
    }

    /// <summary>Return noise value as double.</summary>
    public double GetDouble(double x, int repeat = (int)NumPer)
    {
        repeat = SystemMath.Clamp(repeat, 0, (int)NumPer);

        var hX = MathDefs.AbsMod((int)SystemMath.Floor(x), repeat);

        x -= SystemMath.Floor(x);

        var u = Fade(x);

        var aa = Grad(_p[hX], x);
        var bb = Grad(_p[Inc(hX, repeat)], x - 1);

        var res = MathDefs.Lerp(aa, bb, u);

        return res + 0.5;
    }

    /// <summary>Return noise value as float.</summary>
    public float Get(float x, float y, float z, int repeat = (int)NumPer) => (float)GetDouble(x, y, z, repeat);

    /// <summary>Return noise value as float.</summary>
    public float Get(float x, int repeat = (int)NumPer) => (float)GetDouble(x, repeat);

    /// <summary>Apply 5-th order smoothstep.</summary>
    private static double Fade(double t)
    {
        return t * t * t * (t * (t * 6 - 15) + 10);
    }

    /// <summary>Increment coordinate.</summary>
    private static int Inc(int coord, int repeat) => MathDefs.AbsMod(coord + 1, repeat);

    /// <summary>Return random gradient.</summary>
    private double Grad(int hash, double x, double y, double z)
    {
        // Convert lower 4 bits of hash into 12 gradient directions
        var h = hash & 15;
        var u = h < 8 ? x : y;
        var v = h < 4 ? y : h == 12 || h == 14 ? x : z;
        return ((h & 1) == 0 ? u : -u) + ((h & 2) == 0 ? v : -v);
    }

    /// <summary>Return random gradient.</summary>
    private double Grad(int hash, double x)
    {
        // Convert lower bit of hash into gradient direction
        return (hash & 1) == 0 ? x : -x;
    }

    /// <summary>Permutations.</summary>
    private int[] _p = new int[(int)NumPer * 2];
}
