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
        var pipelineStateDesc = new GraphicsPipelineStateDesc
        {
            VertexShader = new RawShader(new ShaderBytecode(ShaderType.VS)),
            PixelShader = new RawShader(new ShaderBytecode(ShaderType.PS))
        };
        using var pipelineState = new PipelineState(
            _fixture.PipelineStateCache, new PipelineStateDesc(pipelineStateDesc));
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
    public void State_sort_key_is_mutable_value_and_copy_preserves_borrowed_batch()
    {
        var batch = new PipelineBatch();
        var key = new PipelineBatchByState
        {
            PrimaryKey = 1,
            SecondaryKey = 2,
            PipelineBatch = batch
        };

        var copy = key;
        copy.PrimaryKey = 3;

        Assert.Equal(1ul, key.PrimaryKey);
        Assert.Equal(3ul, copy.PrimaryKey);
        Assert.Same(batch, copy.PipelineBatch);
        Assert.Equal(ulong.MaxValue,
            (PipelineBatchByState.PixelLightMask << (int)PipelineBatchByState.PixelLightOffset)
            | (PipelineBatchByState.LightmapMask << (int)PipelineBatchByState.LightmapOffset)
            | (PipelineBatchByState.MaterialMask << (int)PipelineBatchByState.MaterialOffset)
            | (PipelineBatchByState.PipelineStateMask << (int)PipelineBatchByState.PipelineStateOffset)
            | (PipelineBatchByState.RenderOrderMask << (int)PipelineBatchByState.RenderOrderOffset));
        Assert.Equal(ulong.MaxValue,
            (PipelineBatchByState.ReservedMask << (int)PipelineBatchByState.ReservedOffset)
            | (PipelineBatchByState.VertexLightsMask << (int)PipelineBatchByState.VertexLightsOffset)
            | (PipelineBatchByState.GeometryMask << (int)PipelineBatchByState.GeometryOffset));
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

    [Fact]
    public void Back_to_front_key_preserves_cpp_unordered_float_comparison()
    {
        var left = new PipelineBatchBackToFront { RenderOrder = 1, Distance = float.NaN };
        var right = new PipelineBatchBackToFront { RenderOrder = 1, Distance = 2 };

        Assert.Equal(0, left.CompareTo(right));
        Assert.Equal(0, right.CompareTo(left));
    }

    [Fact]
    public void Batch_group_copy_preserves_borrowed_batch_view_and_copies_scalar_state()
    {
        var batch = new PipelineBatch();
        var storage = new[] { new PipelineBatchByState { PipelineBatch = batch } };
        var group = new PipelineBatchGroup<PipelineBatchByState>
        {
            Batches = storage,
            StartInstance = 4,
            NumInstances = 2
        };

        var copy = group;
        copy.StartInstance = 8;
        storage[0].PrimaryKey = 7;

        Assert.Equal(4, group.StartInstance);
        Assert.Equal(8, copy.StartInstance);
        Assert.Equal(7ul, group.Batches.Span[0].PrimaryKey);
        Assert.Equal(7ul, copy.Batches.Span[0].PrimaryKey);
        Assert.Same(batch, copy.Batches.Span[0].PipelineBatch);
        Assert.Equal(typeof(int), typeof(PipelineBatchGroup<PipelineBatchByState>)
            .GetField(nameof(PipelineBatchGroup<PipelineBatchByState>.StartInstance))!.FieldType);
        Assert.Equal(typeof(int), typeof(PipelineBatchGroup<PipelineBatchByState>)
            .GetField(nameof(PipelineBatchGroup<PipelineBatchByState>.NumInstances))!.FieldType);
    }
}
