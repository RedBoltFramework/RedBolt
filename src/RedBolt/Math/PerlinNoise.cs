namespace RedBolt;

public sealed class PerlinNoise
{
    public const int NumPer = 256;

    private readonly int[] _permutation = new int[512];

    public PerlinNoise()
        : this(RandomEngine.GetDefaultEngine())
    {
    }

    public PerlinNoise(RandomEngine random)
    {
        ArgumentNullException.ThrowIfNull(random);
        var basePermutation = Enumerable.Range(0, NumPer).ToArray();
        random.Shuffle(basePermutation);
        for (var i = 0; i < 512; ++i)
            _permutation[i] = basePermutation[i & 255];
    }

    public double GetDouble(double x, double y, double z, int repeat = NumPer)
    {
        repeat = Math.Clamp(repeat, 0, NumPer);

        var hX = MathDefs.AbsMod((int)Math.Floor(x), repeat);
        var hY = MathDefs.AbsMod((int)Math.Floor(y), repeat);
        var hZ = MathDefs.AbsMod((int)Math.Floor(z), repeat);

        x -= Math.Floor(x);
        y -= Math.Floor(y);
        z -= Math.Floor(z);

        var u = Fade(x);
        var v = Fade(y);
        var w = Fade(z);

        var aaa = Gradient(_permutation[_permutation[_permutation[hX] + hY] + hZ], x, y, z);
        var aab = Gradient(_permutation[_permutation[_permutation[Inc(hX, repeat)] + hY] + hZ], x - 1.0, y, z);
        var aba = Gradient(_permutation[_permutation[_permutation[hX] + Inc(hY, repeat)] + hZ], x, y - 1.0, z);
        var abb = Gradient(_permutation[_permutation[_permutation[Inc(hX, repeat)] + Inc(hY, repeat)] + hZ], x - 1.0, y - 1.0, z);
        var baa = Gradient(_permutation[_permutation[_permutation[hX] + hY] + Inc(hZ, repeat)], x, y, z - 1.0);
        var bab = Gradient(_permutation[_permutation[_permutation[Inc(hX, repeat)] + hY] + Inc(hZ, repeat)], x - 1.0, y, z - 1.0);
        var bba = Gradient(_permutation[_permutation[_permutation[hX] + Inc(hY, repeat)] + Inc(hZ, repeat)], x, y - 1.0, z - 1.0);
        var bbb = Gradient(_permutation[_permutation[_permutation[Inc(hX, repeat)] + Inc(hY, repeat)] + Inc(hZ, repeat)], x - 1.0, y - 1.0, z - 1.0);

        var aa = Lerp(aaa, aab, u);
        var ab = Lerp(aba, abb, u);
        var ba = Lerp(baa, bab, u);
        var bb = Lerp(bba, bbb, u);
        var a = Lerp(aa, ab, v);
        var b = Lerp(ba, bb, v);
        return (Lerp(a, b, w) + 1.0) / 2.0;
    }

    public double GetDouble(double x, int repeat = NumPer)
    {
        repeat = Math.Clamp(repeat, 0, NumPer);
        var hX = MathDefs.AbsMod((int)Math.Floor(x), repeat);
        x -= Math.Floor(x);
        var result = Lerp(Gradient(_permutation[hX], x),
            Gradient(_permutation[Inc(hX, repeat)], x - 1.0), Fade(x));
        return result + 0.5;
    }

    public float Get(float x, float y, float z, int repeat = NumPer) => (float)GetDouble(x, y, z, repeat);

    public float Get(float x, int repeat = NumPer) => (float)GetDouble(x, repeat);

    private static double Fade(double t) => t * t * t * (t * (t * 6.0 - 15.0) + 10.0);

    private static double Lerp(double lhs, double rhs, double t) => lhs * (1.0 - t) + rhs * t;

    private static int Inc(int coordinate, int repeat) => MathDefs.AbsMod(coordinate + 1, repeat);

    private static double Gradient(int hash, double x, double y, double z)
    {
        var h = hash & 15;
        var u = h < 8 ? x : y;
        var v = h < 4 ? y : h is 12 or 14 ? x : z;
        return ((h & 1) == 0 ? u : -u) + ((h & 2) == 0 ? v : -v);
    }

    private static double Gradient(int hash, double x) => (hash & 1) == 0 ? x : -x;
}
