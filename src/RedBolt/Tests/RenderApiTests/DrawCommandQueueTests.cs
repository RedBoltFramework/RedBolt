using System.Reflection;
using RedBolt;

namespace RedBolt.Tests;

[Collection(nameof(GraphicsEngineCollection))]
public sealed class DrawCommandQueueTests
{
    private readonly GraphicsEngineFixture _fixture;

    public DrawCommandQueueTests(GraphicsEngineFixture fixture)
    {
        this._fixture = fixture;
    }

    [Fact]
    public void Draw_command_queue_has_native_reference_class_and_constructor_shape()
    {
        Assert.False(typeof(RedObject).IsAssignableFrom(typeof(DrawCommandQueue)));
        Assert.False(typeof(IDisposable).IsAssignableFrom(typeof(DrawCommandQueue)));
        Assert.False(typeof(DrawCommandQueue).GetMethod(nameof(DrawCommandQueue.ExecuteInContext))!.IsVirtual);

        var queue = new DrawCommandQueue(_fixture.RenderDevice);
        Assert.Empty(GetPrivateField<List<IntRect>>(queue, "_scissorRects"));
        Assert.Empty(GetPrivateField<List<DrawCommandDescription>>(queue, "_drawCommands"));
    }

    [Fact]
    public void Draw_command_description_is_value_type_with_inline_copy_semantics()
    {
        Assert.True(typeof(DrawCommandDescription).IsValueType);

        var vertexBuffer = new RawBuffer(new RawBufferParams(BufferType.Vertex, 32, 16, BufferFlag.Shadowed));
        var source = new DrawCommandDescription();
        source.VertexBuffers[0] = vertexBuffer;
        source.ConstantBuffers[0] = new ConstantBufferCollectionRef { Index = 1, Offset = 2, Size = 3 };

        var copy = source;
        source.VertexBuffers[0] = null;
        source.ConstantBuffers[0] = default;

        Assert.Same(vertexBuffer, copy.VertexBuffers[0]);
        Assert.Equal(1, copy.ConstantBuffers[0].Index);
        Assert.Equal(2, copy.ConstantBuffers[0].Offset);
        Assert.Equal(3, copy.ConstantBuffers[0].Size);
    }

    [Fact]
    public void Draw_command_queue_records_graphics_commands_as_snapshots()
    {
        var queue = _fixture.RenderDevice.GetDefaultQueue();
        var pipelineState = CreateGraphicsPipelineState();
        var vertexBuffer0 = new RawBuffer(new RawBufferParams(BufferType.Vertex, 32, 16, BufferFlag.Shadowed));
        var vertexBuffer1 = new RawBuffer(new RawBufferParams(BufferType.Vertex, 48, 16, BufferFlag.Shadowed));
        var indexBuffer = new RawBuffer(new RawBufferParams(BufferType.Index, 12, 2, BufferFlag.Shadowed));
        var scissor0 = new IntRect(1, 2, 30, 40);
        var scissor1 = new IntRect(3, 4, 50, 60);

        queue.Reset();
        queue.SetPipelineState(pipelineState);
        queue.SetStencilRef(17u);
        queue.SetScissorRect(scissor0);
        RawVertexBufferArray vertexBuffers = default;
        vertexBuffers[0] = vertexBuffer0;
        vertexBuffers[1] = vertexBuffer1;
        queue.SetVertexBuffers(vertexBuffers);
        queue.SetIndexBuffer(indexBuffer);
        queue.DrawIndexedInstanced(2, 6, 5, 7, 3);

        queue.SetStencilRef(9u);
        queue.SetScissorRect(scissor1);
        vertexBuffers = default;
        vertexBuffers[0] = vertexBuffer1;
        queue.SetVertexBuffers(vertexBuffers);
        queue.SetIndexBuffer(null);
        queue.Draw(4, 8);

        var commands = GetPrivateField<List<DrawCommandDescription>>(queue, "_drawCommands");
        var scissorRects = GetPrivateField<List<IntRect>>(queue, "_scissorRects");

        Assert.Equal(2, commands.Count);
        Assert.Equal([IntRect.Zero, scissor0, scissor1], scissorRects);

        Assert.Same(pipelineState, commands[0].PipelineState);
        Assert.Equal(1, commands[0].ScissorRect);
        Assert.Equal<uint>(17u, commands[0].StencilRef);
        Assert.Same(indexBuffer, commands[0].IndexBuffer);
        Assert.Same(vertexBuffer0, commands[0].VertexBuffers[0]);
        Assert.Same(vertexBuffer1, commands[0].VertexBuffers[1]);
        Assert.Equal(2, commands[0].IndexStart);
        Assert.Equal(6, commands[0].IndexCount);
        Assert.Equal(5, commands[0].BaseVertexIndex);
        Assert.Equal(7, commands[0].InstanceStart);
        Assert.Equal(3, commands[0].InstanceCount);

        Assert.Same(pipelineState, commands[1].PipelineState);
        Assert.Equal(2, commands[1].ScissorRect);
        Assert.Equal<uint>(9u, commands[1].StencilRef);
        Assert.Null(commands[1].IndexBuffer);
        Assert.Same(vertexBuffer1, commands[1].VertexBuffers[0]);
        Assert.Null(commands[1].VertexBuffers[1]);
        Assert.Equal(4, commands[1].IndexStart);
        Assert.Equal(8, commands[1].IndexCount);
        Assert.Equal(0, commands[1].BaseVertexIndex);
        Assert.Equal(0, commands[1].InstanceStart);
        Assert.Equal(0, commands[1].InstanceCount);
    }

    [Fact]
    public void Shader_parameter_groups_allocate_store_and_reuse_reflected_blocks()
    {
        var queue = _fixture.RenderDevice.GetDefaultQueue();
        var pipelineState = CreateGraphicsPipelineState();
        var reflection = new ShaderProgramReflection(Array.Empty<RawShader?>());
        SetField(pipelineState, "_reflection", reflection);
        var uniformBuffers = GetPrivateField<UniformBufferReflection?[]>(reflection, "_uniformBuffers");
        var uniformBuffer = new UniformBufferReflection();
        SetProperty(uniformBuffer, nameof(UniformBufferReflection.Size), 16);
        SetProperty(uniformBuffer, nameof(UniformBufferReflection.Hash), 42u);
        uniformBuffers[(int)ShaderParameterGroup.Material] = uniformBuffer;
        var uniforms = GetPrivateField<Dictionary<StringHash, UniformReflection>>(reflection, "_uniforms");
        var parameterName = new StringHash("MatValue");
        uniforms[parameterName] = new UniformReflection(
            parameterName, ShaderParameterGroup.Material, 4, 4,
            RedBolt.Bindings.Diligent.ShaderCodeVariableClass.ShaderCodeVariableClassScalar,
            RedBolt.Bindings.Diligent.ShaderCodeBasicType.ShaderCodeBasicTypeFloat, 1, 1, 0);

        queue.Reset();
        queue.SetPipelineState(pipelineState);
        Assert.True(queue.BeginShaderParameterGroup(ShaderParameterGroup.Material));
        queue.AddShaderParameter(parameterName, 3.5f);
        queue.CommitShaderParameterGroup(ShaderParameterGroup.Frame);
        Assert.False(queue.BeginShaderParameterGroup(ShaderParameterGroup.Material));

        var collection = GetNestedField<ConstantBufferCollection>(queue, "_constantBuffers", "Collection");
        Assert.Equal(Math.Max(16, checked((int)_fixture.RenderDevice.GetCaps().ConstantBufferOffsetAlignment)), collection.GetBufferSize(0));
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

    private static T GetPrivateField<T>(object instance, string fieldName)
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsType<T>(field!.GetValue(instance));
    }

    private static T GetNestedField<T>(object instance, string parentFieldName, string fieldName)
    {
        var parentField = instance.GetType().GetField(parentFieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(parentField);
        var parent = parentField!.GetValue(instance);
        Assert.NotNull(parent);
        var field = parent.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsType<T>(field!.GetValue(parent));
    }

    private static void SetProperty<T>(object instance, string propertyName, T value)
    {
        var property = instance.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(property);
        property!.SetValue(instance, value);
    }

    private static void SetField<T>(object instance, string fieldName, T value)
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field!.SetValue(instance, value);
    }
}
