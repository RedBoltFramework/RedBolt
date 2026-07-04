namespace RedBolt;

public sealed class PerlinNoise
{
    private readonly int[] permutation = new int[512];

    public PerlinNoise()
        : this(RandomEngine.GetDefaultEngine())
    {
    }

    public PerlinNoise(RandomEngine random)
    {
        ArgumentNullException.ThrowIfNull(random);
        var basePermutation = Enumerable.Range(0, 256).ToArray();
        random.Shuffle(basePermutation);
        for (var i = 0; i < 512; ++i)
            permutation[i] = basePermutation[i & 255];
    }

    public float Get(float x)
    {
        var floor = MathF.Floor(x);
        var xi = ((int)floor) & 255;
        var xf = x - floor;
        var u = Fade(xf);
        var a = Gradient(permutation[xi], xf);
        var b = Gradient(permutation[xi + 1], xf - 1.0f);
        return MathDefs.Lerp(a, b, u) * 0.5f + 0.5f;
    }

    private static float Fade(float t)
    {
        return t * t * t * (t * (t * 6.0f - 15.0f) + 10.0f);
    }

    private static float Gradient(int hash, float x)
    {
        return (hash & 1) == 0 ? x : -x;
    }
}
