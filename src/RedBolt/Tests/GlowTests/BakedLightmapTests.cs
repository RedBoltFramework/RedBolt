namespace RedBolt.Tests;

using RedBolt.Glow;
using RedBolt.Math;

public sealed class BakedLightmapTests
{
    [Fact]
    public void Baked_lightmap_allocates_square_linear_buffer()
    {
        var lightmap = new BakedLightmap(4);

        Assert.Equal(4, lightmap.LightmapSize);
        Assert.Equal(16, lightmap.Lightmap.Count);
        Assert.All(lightmap.Lightmap, value => Assert.Equal(Vector3f.Zero, value));
    }

    [Fact]
    public void Baked_lightmap_clone_copies_vector_storage()
    {
        var lightmap = new BakedLightmap(2);
        var clone = lightmap.Clone();

        lightmap.Lightmap[0] = Vector3f.One;

        Assert.Equal(Vector3f.Zero, clone.Lightmap[0]);
    }
}
