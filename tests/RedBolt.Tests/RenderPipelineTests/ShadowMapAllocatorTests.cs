using RedBolt;

namespace RedBolt.Tests;

public sealed class ShadowMapAllocatorTests
{
    [Fact]
    public void Shadow_map_region_returns_requested_split()
    {
        var texture = new Texture2D("Shadow Map");
        var region = new ShadowMapRegion
        {
            PageIndex = 3,
            Texture = texture,
            Rect = new IntRect(8, 12, 72, 44)
        };

        var split = region.GetSplit(5, new Vector2i(4, 2));

        Assert.Equal(3, split.PageIndex);
        Assert.Same(texture, split.Texture);
        Assert.Equal(new IntRect(24, 28, 40, 44), split.Rect);
    }

    [Fact]
    public void Shadow_map_region_matches_native_value_shape()
    {
        var region = default(ShadowMapRegion);
        Assert.False(region);

        region.PageIndex = 2;
        region.Texture = new Texture2D("Shadow Map");
        region.Rect = new IntRect(1, 2, 3, 4);

        Assert.True(region);
        Assert.Equal(2, region.PageIndex);
        Assert.Equal(new IntRect(1, 2, 3, 4), region.Rect);
    }
}
