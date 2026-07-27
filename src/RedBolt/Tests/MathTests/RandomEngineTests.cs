using RedBolt;

using RedBolt.Math;
namespace RedBolt.Tests;

public sealed class RandomEngineTests
{
    [Fact]
    public void Class_shape_matches_native()
    {
        Assert.False(typeof(RandomEngine).IsValueType);
        Assert.False(typeof(RandomEngine).IsSealed);
        Assert.True(typeof(ICloneable).IsAssignableFrom(typeof(RandomEngine)));
        Assert.NotNull(typeof(RandomEngine).GetMethod("GetStandardNormalFloatArray", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic));
        Assert.NotNull(typeof(RandomEngine).GetMethod("GetDirection", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic));
    }

    [Fact]
    public void Deterministic_seed_produces_expected_sequence()
    {
        var rng = new RandomEngine(1u);
        Assert.Equal(2147483646u, RandomEngine.MaxRange());
        Assert.Equal("1", rng.Save());
        Assert.Equal(48270u, rng.GetUInt());

        var nativeSequence = new RandomEngine(12u);
        Assert.Equal(579251u, nativeSequence.GetUInt());
        Assert.Equal(43785880u, nativeSequence.GetUInt());
        Assert.Equal(464353102u, nativeSequence.GetUInt());
    }

    [Fact]
    public void Copies_have_independent_engine_state()
    {
        var source = new RandomEngine(7u);
        source.GetUInt();
        var copy = new RandomEngine(source);
        var clone = (RandomEngine)source.Clone();

        Assert.NotSame(source, copy);
        Assert.NotSame(source, clone);
        var expected = source.GetUInt();
        Assert.Equal(expected, copy.GetUInt());
        Assert.Equal(expected, clone.GetUInt());

        source.GetUInt();
        Assert.Equal(copy.GetUInt(), clone.GetUInt());
    }

    [Fact]
    public void Saved_state_restores_deterministically()
    {
        var state = new RandomEngine(7u).Save();
        var a = new RandomEngine(state);
        var b = new RandomEngine(state);
        Assert.Equal(a.GetUInt(), b.GetUInt());
    }

    [Fact]
    public void Invalid_state_does_not_replace_engine_state()
    {
        var rng = new RandomEngine(7u);
        var state = rng.Save();

        rng.Load("invalid");
        Assert.Equal(state, rng.Save());
        rng.Load("0");
        Assert.Equal(state, rng.Save());
        rng.Load("2147483647");
        Assert.Equal(state, rng.Save());

        Assert.Equal("1", new RandomEngine("invalid").Save());
    }

    [Fact]
    public void Bounded_integers_respect_ranges()
    {
        var rng = new RandomEngine(7u);
        Assert.True(rng.GetUInt(10u) < 10u);
        var ranged = rng.GetUInt(5u, 7u);
        Assert.True(ranged is >= 5u and < 7u);
        var signed = rng.GetInt(-3, 3);
        Assert.True(signed is >= -3 and < 3);
    }

    [Fact]
    public void Scalar_random_respects_ranges()
    {
        var rng = new RandomEngine(7u);
        Assert.False(rng.GetBool(0.0f));
        Assert.True(rng.GetBool(1.0f));
        var f = rng.GetFloat(-1.0f, 1.0f);
        Assert.True(f is >= -1.0f and <= 1.0f);
    }

    [Fact]
    public void Vector_random_produces_valid_units_and_bounds()
    {
        var dir = new RandomEngine(2u).GetDirectionVector3();
        Assert.True(MathDefs.Equals(dir.Length, 1.0f, MathDefs.LargeEpsilon));

        var quat = new RandomEngine(3u).GetQuaternion();
        Assert.True(MathDefs.Equals(quat.LengthSquared, 1.0f, MathDefs.LargeEpsilon));

        var vec = new RandomEngine(4u).GetVector3(new BoundingBox(Vector3f.Zero, Vector3f.One));
        Assert.True(vec.X is >= 0.0f and <= 1.0f);
        Assert.True(vec.Y is >= 0.0f and <= 1.0f);
        Assert.True(vec.Z is >= 0.0f and <= 1.0f);
    }

    [Fact]
    public void Global_random_helpers_work()
    {
        MathDefs.SetRandomSeed(1u);
        Assert.Equal(1u, MathDefs.GetRandomSeed());
        Assert.Equal(41, MathDefs.Rand());
        Assert.True(MathDefs.Random(3) is >= 0 and < 3);
        Assert.True(float.IsFinite(MathDefs.RandomNormal(0.0f, 1.0f)));
    }
}
