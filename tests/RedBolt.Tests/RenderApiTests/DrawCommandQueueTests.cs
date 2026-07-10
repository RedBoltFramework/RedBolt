using System.Reflection;
using RedBolt;

namespace RedBolt.Tests;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class DrawCommandQueueTests
{
    private readonly HeadlessEngineFixture _fixture;

    public DrawCommandQueueTests(HeadlessEngineFixture fixture)
    {
        this._fixture = fixture;
    }

    [Fact]
    public void Draw_command_queue_records_graphics_commands_as_snapshots()
    {
        var queue = _fixture.RenderDevice.GetDefaultQueue();
        var pipelineState = CreateGraphicsPipelineState();
        var vertexBuffer0 = new RawBuffer(new RawBufferParams(BufferType.Vertex, 32u, 16u, BufferFlag.Shadowed));
        var vertexBuffer1 = new RawBuffer(new RawBufferParams(BufferType.Vertex, 48u, 16u, BufferFlag.Shadowed));
        var indexBuffer = new RawBuffer(new RawBufferParams(BufferType.Index, 12u, 2u, BufferFlag.Shadowed));
        var scissor0 = new IntRect(1, 2, 30, 40);
        var scissor1 = new IntRect(3, 4, 50, 60);

        queue.Reset();
        queue.SetPipelineState(pipelineState);
        queue.SetStencilRef(17u);
        queue.SetScissorRect(scissor0);
        queue.SetVertexBuffers([vertexBuffer0, vertexBuffer1]);
        queue.SetIndexBuffer(indexBuffer);
        queue.DrawIndexedInstanced(2u, 6u, 5u, 7u, 3u);

        queue.SetStencilRef(9u);
        queue.SetScissorRect(scissor1);
        queue.SetVertexBuffers([vertexBuffer1]);
        queue.SetIndexBuffer(null);
        queue.Draw(4u, 8u);

        var commands = GetPrivateField<List<DrawCommandDescription>>(queue, "_drawCommands");
        var scissorRects = GetPrivateField<List<IntRect>>(queue, "_scissorRects");

        Assert.Equal(2, commands.Count);
        Assert.Equal([IntRect.Zero, scissor0, scissor1], scissorRects);

        Assert.Same(pipelineState, commands[0].PipelineState);
        Assert.Equal<uint>(1u, commands[0].ScissorRect);
        Assert.Equal<uint>(17u, commands[0].StencilRef);
        Assert.Same(indexBuffer, commands[0].IndexBuffer);
        Assert.Same(vertexBuffer0, commands[0].VertexBuffers[0]);
        Assert.Same(vertexBuffer1, commands[0].VertexBuffers[1]);
        Assert.Equal<uint>(2u, commands[0].IndexStart);
        Assert.Equal<uint>(6u, commands[0].IndexCount);
        Assert.Equal<uint>(5u, commands[0].BaseVertexIndex);
        Assert.Equal<uint>(7u, commands[0].InstanceStart);
        Assert.Equal<uint>(3u, commands[0].InstanceCount);

        Assert.Same(pipelineState, commands[1].PipelineState);
        Assert.Equal<uint>(2u, commands[1].ScissorRect);
        Assert.Equal<uint>(9u, commands[1].StencilRef);
        Assert.Null(commands[1].IndexBuffer);
        Assert.Same(vertexBuffer1, commands[1].VertexBuffers[0]);
        Assert.Null(commands[1].VertexBuffers[1]);
        Assert.Equal<uint>(4u, commands[1].IndexStart);
        Assert.Equal<uint>(8u, commands[1].IndexCount);
        Assert.Equal<uint>(0u, commands[1].BaseVertexIndex);
        Assert.Equal<uint>(0u, commands[1].InstanceStart);
        Assert.Equal<uint>(0u, commands[1].InstanceCount);
    }

    [Fact]
    public void Draw_command_queue_execute_updates_render_context_stats_for_recorded_commands()
    {
        var queue = _fixture.RenderDevice.GetDefaultQueue();
        var graphicsState = CreateGraphicsPipelineState();
        var computeState = CreateComputePipelineState();

        queue.Reset();
        queue.SetClipPlaneMask(0x5Au);
        queue.SetPipelineState(graphicsState);
        queue.SetIndexBuffer(null);
        queue.DrawInstanced(2u, 5u, 7u, 3u);
        queue.SetPipelineState(computeState);
        queue.Dispatch(new Vector3i(4, 5, 6));

        _fixture.RenderContext.ResetStats();
        queue.ExecuteInContext(_fixture.RenderContext);

        var stats = _fixture.RenderContext.GetStats();
        Assert.Equal<uint>(1u, stats.NumDraws);
        Assert.Equal<uint>(15u, stats.NumPrimitives);
        Assert.Equal<uint>(1u, stats.NumDispatches);
        Assert.Equal<uint>(0x5Au, GetPrivateField<uint>(_fixture.RenderContext, "_clipPlaneMask"));
    }

    [Fact]
    public void Draw_command_queue_rejects_mismatched_pipeline_and_buffer_state()
    {
        var queue = _fixture.RenderDevice.GetDefaultQueue();
        var graphicsState = CreateGraphicsPipelineState();
        var computeState = CreateComputePipelineState();
        var indexBuffer = new RawBuffer(new RawBufferParams(BufferType.Index, 12u, 2u, BufferFlag.Shadowed));

        queue.Reset();
        Assert.Throws<InvalidOperationException>(() => queue.Draw(0u, 3u));

        queue.SetPipelineState(graphicsState);
        queue.SetIndexBuffer(indexBuffer);
        Assert.Throws<InvalidOperationException>(() => queue.Draw(0u, 3u));

        queue.SetPipelineState(computeState);
        queue.SetIndexBuffer(null);
        Assert.Throws<InvalidOperationException>(() => queue.DrawIndexed(0u, 3u));
    }

    private PipelineState CreateGraphicsPipelineState()
    {
        var cache = _fixture.Context.GetSubsystem<PipelineStateCache>();
        Assert.NotNull(cache);

        var desc = new GraphicsPipelineStateDesc
        {
            DebugName = "DrawQueueGraphics",
            VertexShader = new RawShader(new ShaderBytecode(ShaderType.VS)),
            PixelShader = new RawShader(new ShaderBytecode(ShaderType.PS))
        };
        var pipelineState = cache!.GetGraphicsPipelineState(desc);
        Assert.NotNull(pipelineState);
        return pipelineState!;
    }

    private PipelineState CreateComputePipelineState()
    {
        var cache = _fixture.Context.GetSubsystem<PipelineStateCache>();
        Assert.NotNull(cache);

        var desc = new ComputePipelineStateDesc
        {
            DebugName = "DrawQueueCompute",
            ComputeShader = new RawShader(new ShaderBytecode(ShaderType.CS))
        };
        var pipelineState = cache!.GetComputePipelineState(desc);
        Assert.NotNull(pipelineState);
        return pipelineState!;
    }

    private static T GetPrivateField<T>(object instance, string fieldName)
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsType<T>(field!.GetValue(instance));
    }
}
