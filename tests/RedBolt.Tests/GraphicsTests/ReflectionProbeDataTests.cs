namespace RedBolt.Tests;

public sealed class ReflectionProbeDataTests
{
    [Fact]
    public void Reflection_probe_reference_reset_clears_data_only()
    {
        var reference = new ReflectionProbeReference
        {
            Data = new ReflectionProbeData
            {
                CubemapCenter = new Vector4f(1.0f, 2.0f, 3.0f, 1.0f),
                RoughnessToLodFactor = 4.0f
            },
            Priority = 7,
            Volume = 12.5f
        };

        reference.Reset();

        Assert.Null(reference.Data);
        Assert.Equal(7, reference.Priority);
        Assert.Equal(12.5f, reference.Volume);
    }

    [Fact]
    public void Cached_drawable_reflection_has_two_probe_slots()
    {
        var cache = new CachedDrawableReflection();

        Assert.Equal(2, cache.StaticProbes.Length);
        Assert.Equal(2, cache.Probes.Length);
        Assert.Equal(-1.0f, cache.CacheInvalidationDistanceSquared);
    }
}
