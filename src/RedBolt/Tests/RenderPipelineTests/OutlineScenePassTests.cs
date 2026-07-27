using RedBolt.RenderPipeline;
namespace RedBolt.Tests;

public sealed class OutlineScenePassTests
{
    [Fact]
    public void Class_shape_matches_native_property_mapping()
    {
        Assert.True(typeof(OutlineScenePass).IsClass);
        Assert.False(typeof(OutlineScenePass).IsSealed);
        Assert.Null(typeof(OutlineScenePass).GetMethod("GetBatches"));
        Assert.NotNull(typeof(OutlineScenePass).GetProperty(nameof(OutlineScenePass.Batches)));
        Assert.NotNull(typeof(OutlineScenePass).GetProperty(nameof(OutlineScenePass.HasBatches)));
    }
}
