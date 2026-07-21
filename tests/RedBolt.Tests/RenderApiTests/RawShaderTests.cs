using RedBolt;

namespace RedBolt.Tests;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class RawShaderTests
{
    private readonly HeadlessEngineFixture _fixture;

    public RawShaderTests(HeadlessEngineFixture fixture)
    {
        this._fixture = fixture;
    }

    [Fact]
    public void Shader_bytecode_round_trips_stream_state()
    {
        var original = new ShaderBytecode(ShaderType.VS)
        {
            Mime = "application/glsl",
            Bytecode = [1, 2, 3, 4]
        };
        original.VertexAttributes.Add(new VertexShaderAttribute(VertexElementSemantic.Position, 0, 0));
        original.VertexAttributes.Add(new VertexShaderAttribute(VertexElementSemantic.Normal, 0, 0));

        using var buffer = new VectorBuffer();
        Assert.True(original.SaveToFile(buffer));

        buffer.Seek(0);
        var restored = new ShaderBytecode();

        Assert.True(restored.LoadFromFile(buffer));
        AssertShaderBytecodeEqual(original, restored);
    }

    [Fact]
    public void Shader_bytecode_round_trips_archive_state()
    {
        var original = new ShaderBytecode(ShaderType.VS)
        {
            Mime = "application/spirv",
            Bytecode = [9, 8, 7, 6]
        };
        original.VertexAttributes.Add(new VertexShaderAttribute(VertexElementSemantic.TexCoord, 2, 1));
        var output = new JSONOutputArchive();
        using (output.OpenUnorderedBlock("shader")) original.SerializeInBlock(output);

        var restored = new ShaderBytecode();
        var input = new JSONInputArchive(output.ToJsonString());
        using (input.OpenUnorderedBlock("shader")) restored.SerializeInBlock(input);

        AssertShaderBytecodeEqual(original, restored);
    }

    [Fact]
    public void Shader_bytecode_and_vertex_attributes_match_native_value_types()
    {
        Assert.True(typeof(ShaderBytecode).IsValueType);
        Assert.True(typeof(VertexShaderAttribute).IsValueType);
        Assert.Equal(typeof(int), typeof(VertexShaderAttribute).GetProperty(nameof(VertexShaderAttribute.SemanticIndex))!.PropertyType);
        Assert.Equal(typeof(int), typeof(VertexShaderAttribute).GetProperty(nameof(VertexShaderAttribute.InputIndex))!.PropertyType);

        var original = new ShaderBytecode(ShaderType.VS)
        {
            Mime = "application/spirv",
            Bytecode = [1, 2, 3]
        };
        original.VertexAttributes.Add(new VertexShaderAttribute(VertexElementSemantic.Position, 0, 2));

        var copy = (ShaderBytecode)original.Clone();
        copy.Bytecode[0] = 9;
        copy.VertexAttributes[0] = new VertexShaderAttribute(VertexElementSemantic.Normal, 1, 4);

        Assert.Equal((byte)1, original.Bytecode[0]);
        Assert.Equal(new VertexShaderAttribute(VertexElementSemantic.Position, 0, 2), original.VertexAttributes[0]);
    }

    [Fact]
    public void Compute_pipeline_state_desc_supports_raw_shader_value_semantics()
    {
        var bytecode = new ShaderBytecode(ShaderType.CS)
        {
            Mime = "application/spirv",
            Bytecode = []
        };

        var first = new ComputePipelineStateDesc
        {
            DebugName = "ComputeRawShader",
            ComputeShader = new RawShader(bytecode)
        };
        first.Samplers.Add(new StringHash("sCompute"), SamplerStateDesc.Bilinear());

        var second = first;

        Assert.True(first.IsInitialized());
        Assert.Equal(first, second);
        Assert.Equal(first.ToHash(), second.ToHash());
    }

    [Fact]
    public void Graphics_shader_variations_compile_to_native_handles_when_graphics_is_initialized()
    {
        WithInitializedGraphics(() =>
        {
            var vertexShader = _fixture.Graphics.GetShader(ShaderType.VS, "X_CopyTexture");
            var pixelShader = _fixture.Graphics.GetShader(ShaderType.PS, "X_CopyTexture");

            Assert.NotNull(vertexShader);
            Assert.NotNull(pixelShader);
            Assert.NotNull(vertexShader!.GetHandle());
            Assert.NotNull(pixelShader!.GetHandle());
            Assert.False(vertexShader.GetHandle()!.IsNull);
            Assert.False(pixelShader.GetHandle()!.IsNull);

            var bytecode = vertexShader.GetBytecode();
            Assert.Equal(ShaderType.VS, bytecode.Type);
            Assert.Equal("application/glsl", bytecode.Mime);
            if (bytecode.VertexAttributes.Count == 0
                && _fixture.Graphics.GetSupportedMultiSample(TextureFormat.TexFormatRgba8Unorm, 4) <= 1)
                return;
            Assert.Contains(bytecode.VertexAttributes, attribute => attribute.Semantic == VertexElementSemantic.Position);
            Assert.Empty(pixelShader.GetBytecode().VertexAttributes);
        });
    }

    [Fact]
    public void Fullscreen_copy_shader_variations_compile_to_native_handles_when_graphics_is_initialized()
    {
        WithInitializedGraphics(() =>
        {
            const string defines = "URHO3D_GEOMETRY_STATIC";
            var vertexShader = _fixture.Graphics.GetShader(ShaderType.VS, "v2/X_CopyFramebuffer", defines);
            var pixelShader = _fixture.Graphics.GetShader(ShaderType.PS, "v2/X_CopyFramebuffer", defines);

            Assert.NotNull(vertexShader);
            Assert.NotNull(pixelShader);
            Assert.NotNull(vertexShader!.GetHandle());
            Assert.NotNull(pixelShader!.GetHandle());
            Assert.False(vertexShader.GetHandle()!.IsNull);
            Assert.False(pixelShader.GetHandle()!.IsNull);

            var renderBufferManager = new RenderBufferManager();
            var pipelineStateId = renderBufferManager.CreateQuadPipelineState(
                BlendMode.Replace,
                "v2/X_CopyFramebuffer",
                string.Empty,
                [new NamedSamplerStateDesc("Albedo", SamplerStateDesc.Bilinear())]);
            var pipelineState = renderBufferManager.GetQuadPipelineState(pipelineStateId);
            Assert.NotNull(pipelineState);
            Assert.True(pipelineState!.IsValid);
        });
    }

    private void WithInitializedGraphics(Action action)
    {
        var parameters = new EngineParameters
        {
            ApplicationName = "RedBolt.Tests.RawShaderTests",
            Headless = false,
            WindowWidth = 64,
            WindowHeight = 64
        };

        RbfxTestData.AddResourceDirs(_fixture.Resources);
        _fixture.Input.Initialize(parameters);
        _fixture.Graphics.Initialize(parameters, _fixture.Input);
        _fixture.RenderDevice.RestoreDeviceState();
        try
        {
            action();
        }
        finally
        {
            _fixture.RenderDevice.InvalidateDeviceState();
            _fixture.Graphics.Shutdown();
            _fixture.Input.Shutdown();
            RbfxTestData.RemoveResourceDirs(_fixture.Resources);
        }
    }

    private static void AssertShaderBytecodeEqual(in ShaderBytecode expected, in ShaderBytecode actual)
    {
        Assert.Equal(expected.Type, actual.Type);
        Assert.Equal(expected.Mime, actual.Mime);
        Assert.Equal(expected.Bytecode, actual.Bytecode);
        Assert.Equal(expected.VertexAttributes, actual.VertexAttributes);
    }
}
