using RedBolt;

namespace RedBolt.Tests;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class PipelineBatchSortKeyTests
{
    private readonly HeadlessEngineFixture _fixture;

    public PipelineBatchSortKeyTests(HeadlessEngineFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void State_sort_key_matches_cpp_bit_layout()
    {
        using var material = new Material("Sort Material");
        using var geometry = new Geometry();
        using var pipelineState = new PipelineState(_fixture.PipelineStateCache, new PipelineStateDesc());
        material.SetRenderOrder(7);
        var batch = new PipelineBatch
        {
            Material = material,
            Geometry = geometry,
            PipelineState = pipelineState,
            LightmapIndex = 11,
            PixelLightIndex = 13,
            VertexLightsHash = 17
        };

        var key = new PipelineBatchByState(batch);

        var expectedPrimary = 7ul << (int)PipelineBatchByState.RenderOrderOffset;
        expectedPrimary |= ((ulong)pipelineState.GetObjectID() & PipelineBatchByState.PipelineStateMask)
            << (int)PipelineBatchByState.PipelineStateOffset;
        expectedPrimary |= ((ulong)material.GetObjectID() & PipelineBatchByState.MaterialMask)
            << (int)PipelineBatchByState.MaterialOffset;
        expectedPrimary |= 11ul << (int)PipelineBatchByState.LightmapOffset;
        expectedPrimary |= 13ul;
        var expectedSecondary = ((ulong)geometry.GetObjectID() & PipelineBatchByState.GeometryMask)
            << (int)PipelineBatchByState.GeometryOffset;
        expectedSecondary |= 17ul << (int)PipelineBatchByState.VertexLightsOffset;
        Assert.Equal(expectedPrimary, key.PrimaryKey);
        Assert.Equal(expectedSecondary, key.SecondaryKey);
        Assert.Same(batch, key.PipelineBatch);
    }

    [Fact]
    public void Back_to_front_key_sorts_render_order_then_descending_distance()
    {
        using var firstMaterial = new Material("First");
        using var secondMaterial = new Material("Second");
        firstMaterial.SetRenderOrder(1);
        secondMaterial.SetRenderOrder(2);
        var keys = new[]
        {
            new PipelineBatchBackToFront(new PipelineBatch { Material = firstMaterial, Distance = 2 }),
            new PipelineBatchBackToFront(new PipelineBatch { Material = secondMaterial, Distance = 100 }),
            new PipelineBatchBackToFront(new PipelineBatch { Material = firstMaterial, Distance = 8 })
        };

        Array.Sort(keys);

        Assert.Equal(8, keys[0].Distance);
        Assert.Equal(2, keys[1].Distance);
        Assert.Equal(2, keys[2].RenderOrder);
    }
}
