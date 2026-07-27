using RedBolt.Graphics;
using RedBolt.RenderAPI;
using RedBolt.RenderPipeline;
namespace RedBolt.Tests;

public sealed class StaticPipelineStateCacheTests
{
    [Fact]
    public void Static_pipeline_state_id_uses_managed_index_type()
    {
        Assert.Equal(typeof(int), Enum.GetUnderlyingType(typeof(StaticPipelineStateId)));
        Assert.Equal(0, (int)StaticPipelineStateId.Invalid);
    }

    [Fact]
    public void Create_state_deduplicates_cpp_descriptor_values()
    {
        using var cache = new StaticPipelineStateCache();
        var description = new GraphicsPipelineStateDesc
        {
            DebugName = "Static state",
            BlendMode = BlendMode.Alpha
        };

        var first = cache.CreateState(description);
        var second = cache.CreateState(description);

        Assert.NotEqual(StaticPipelineStateId.Invalid, first);
        Assert.Equal(first, second);
    }

    [Fact]
    public void Create_state_stores_cpp_value_copy()
    {
        using var cache = new StaticPipelineStateCache();
        var description = new GraphicsPipelineStateDesc
        {
            DebugName = "Static state",
            BlendMode = BlendMode.Alpha
        };
        var original = description;

        var first = cache.CreateState(description);
        description.BlendMode = BlendMode.Add;

        Assert.Equal(first, cache.CreateState(original));
        Assert.NotEqual(first, cache.CreateState(description));
    }
}
