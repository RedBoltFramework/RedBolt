using RedBolt;

using RedBolt.Container;
using RedBolt.Graphics;
using RedBolt.RenderAPI;
using RedBolt.RenderPipeline;
namespace RedBolt.Tests;
using ShaderType = global::RedBolt.RenderAPI.ShaderType;


[Collection(nameof(GraphicsEngineCollection))]
public sealed class PipelineBatchSortKeyTests
{
    private readonly GraphicsEngineFixture _fixture;

    public PipelineBatchSortKeyTests(GraphicsEngineFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void State_sort_key_matches_cpp_bit_layout()
    {
        using var material = new Material { Name = "Sort Material" };
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

        var batches = new List<PipelineBatch> { batch };
        var key = new PipelineBatchByState(batches, 0);

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
        Assert.Equal(batch, key.PipelineBatch);
        Assert.True(typeof(PipelineBatch).IsValueType);
        Assert.True(typeof(PipelineBatchDesc).IsValueType);
    }

    [Fact]
    public void State_sort_key_is_a_mutable_value()
    {
        var key = new PipelineBatchByState
        {
            PrimaryKey = 1,
            SecondaryKey = 2
        };

        var copy = key;
        copy.PrimaryKey = 3;

        Assert.Equal(1ul, key.PrimaryKey);
        Assert.Equal(3ul, copy.PrimaryKey);
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
        using var firstMaterial = new Material { Name = "First" };
        using var secondMaterial = new Material { Name = "Second" };
        firstMaterial.SetRenderOrder(1);
        secondMaterial.SetRenderOrder(2);
        var batches = new List<PipelineBatch>
        {
            new() { Material = firstMaterial, Distance = 2 },
            new() { Material = secondMaterial, Distance = 100 },
            new() { Material = firstMaterial, Distance = 8 }
        };
        var keys = new[]
        {
            new PipelineBatchBackToFront(batches, 0),
            new PipelineBatchBackToFront(batches, 1),
            new PipelineBatchBackToFront(batches, 2)
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
    public void Batch_group_copy_preserves_batch_storage_and_copies_scalar_state()
    {
        using var material = new Material { Name = "Group Material" };
        using var geometry = new Geometry();
        using var pipelineState = new PipelineState(_fixture.PipelineStateCache,
            new PipelineStateDesc(new GraphicsPipelineStateDesc
            {
                VertexShader = new RawShader(new ShaderBytecode(ShaderType.VS)),
                PixelShader = new RawShader(new ShaderBytecode(ShaderType.PS))
            }));
        var batches = new List<PipelineBatch>
        {
            new() { Material = material, Geometry = geometry, PipelineState = pipelineState }
        };
        var storage = new List<PipelineBatchByState> { new(batches, 0) };
        var group = new PipelineBatchGroup<PipelineBatchByState>
        {
            Batches = storage,
            StartInstance = 4,
            NumInstances = 2
        };

        var copy = group;
        copy.StartInstance = 8;
        var storedBatch = storage[0];
        storedBatch.PrimaryKey = 7;
        storage[0] = storedBatch;

        Assert.Equal(4, group.StartInstance);
        Assert.Equal(8, copy.StartInstance);
        Assert.Same(storage, group.Batches);
        Assert.Same(storage, copy.Batches);
        Assert.Equal(7ul, group.Batches![0].PrimaryKey);
        Assert.Equal(7ul, copy.Batches![0].PrimaryKey);
        Assert.Equal(batches[0], copy.Batches[0].PipelineBatch);
        Assert.Equal(typeof(int), typeof(PipelineBatchGroup<PipelineBatchByState>)
            .GetField(nameof(PipelineBatchGroup<PipelineBatchByState>.StartInstance))!.FieldType);
        Assert.Equal(typeof(int), typeof(PipelineBatchGroup<PipelineBatchByState>)
            .GetField(nameof(PipelineBatchGroup<PipelineBatchByState>.NumInstances))!.FieldType);
    }

    [Fact]
    public void Sort_keys_retain_aliases_to_pipeline_batch_storage()
    {
        using var material = new Material { Name = "Alias Material" };
        using var geometry = new Geometry();
        using var pipelineState = new PipelineState(_fixture.PipelineStateCache,
            new PipelineStateDesc(new GraphicsPipelineStateDesc
            {
                VertexShader = new RawShader(new ShaderBytecode(ShaderType.VS)),
                PixelShader = new RawShader(new ShaderBytecode(ShaderType.PS))
            }));
        var batches = new MultiVector<PipelineBatch>();
        batches.Resize(1);
        var index = batches.PushBack(0, new PipelineBatch
        {
            Material = material,
            Geometry = geometry,
            PipelineState = pipelineState,
            Distance = 1
        });
        var keys = new List<PipelineBatchByState>();

        BatchCompositor.FillSortKeys(keys, batches);
        batches[index].Distance = 2;

        Assert.Equal(2, keys[0].PipelineBatch.Distance);
    }
}
