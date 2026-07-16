using RedBolt;

namespace RedBolt.Tests;

public sealed class ShadowMapAllocatorTests
{
    [Fact]
    public void Shadow_map_region_returns_requested_split()
    {
        var texture = new Texture2D("Shadow Map");
        var region = new ShadowMapRegion(3, texture, new IntRect(8, 12, 72, 44));

        var split = region.GetSplit(5, new Vector2i(4, 2));

        Assert.Equal(3u, split.PageIndex);
        Assert.Same(texture, split.Texture);
        Assert.Equal(new IntRect(24, 28, 40, 44), split.Rect);
    }
}
