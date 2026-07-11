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

    [Fact]
    public void Shader_parameter_groups_allocate_store_and_reuse_reflected_blocks()
    {
        var queue = _fixture.RenderDevice.GetDefaultQueue();
        var pipelineState = CreateGraphicsPipelineState();
        var reflection = pipelineState.GetReflection();
        var uniformBuffers = GetPrivateField<UniformBufferReflection?[]>(reflection, "_uniformBuffers");
        var uniformBuffer = new UniformBufferReflection();
        SetProperty(uniformBuffer, nameof(UniformBufferReflection.Size), 16u);
        SetProperty(uniformBuffer, nameof(UniformBufferReflection.Hash), 42u);
        uniformBuffers[(int)ShaderParameterGroup.Material] = uniformBuffer;
        var uniforms = GetPrivateField<Dictionary<StringHash, UniformReflection>>(reflection, "_uniforms");
        var parameterName = new StringHash("MatValue");
        uniforms[parameterName] = new UniformReflection(
            "MatValue", ShaderParameterGroup.Material, 4, 4,
            RedBolt.Bindings.Diligent.ShaderCodeVariableClass.ShaderCodeVariableClassScalar,
            RedBolt.Bindings.Diligent.ShaderCodeBasicType.ShaderCodeBasicTypeFloat, 1, 1, 0);

        queue.Reset();
        queue.SetPipelineState(pipelineState);
        Assert.True(queue.BeginShaderParameterGroup(ShaderParameterGroup.Material));
        Assert.True(queue.AddShaderParameter(parameterName, 3.5f));
        queue.CommitShaderParameterGroup(ShaderParameterGroup.Material);
        Assert.False(queue.BeginShaderParameterGroup(ShaderParameterGroup.Material));

        var collection = queue.GetConstantBufferCollection();
        Assert.Equal(Math.Max(16u, _fixture.RenderDevice.GetCaps().ConstantBufferOffsetAlignment), collection.GetBufferSize(0));
        Assert.Equal(3.5f, BitConverter.ToSingle(collection.GetBufferData(0).Span.Slice(4, 4)));
        Assert.True(queue.BeginShaderParameterGroup(ShaderParameterGroup.Material, differentFromPrevious: true));
    }

    [Fact]
    public void Empty_resource_groups_commit_as_stable_command_ranges()
    {
        var queue = _fixture.RenderDevice.GetDefaultQueue();
        queue.Reset();
        queue.SetPipelineState(CreateGraphicsPipelineState());
        queue.CommitShaderResources();
        queue.CommitUnorderedAccessViews();
        queue.SetIndexBuffer(null);
        queue.Draw(0, 3);

        var command = Assert.Single(GetPrivateField<List<DrawCommandDescription>>(queue, "_drawCommands"));
        Assert.Equal(new ShaderResourceRange(0, 0), command.ShaderResources);
        Assert.Equal(new ShaderResourceRange(0, 0), command.UnorderedAccessViews);
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

    private static void SetProperty<T>(object instance, string propertyName, T value)
    {
        var property = instance.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(property);
        property!.SetValue(instance, value);
    }
}
