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

        using var stream = new MemoryStream();
        Assert.True(original.SaveToFile(stream));

        stream.Position = 0;
        var restored = new ShaderBytecode();

        Assert.True(restored.LoadFromFile(stream));
        Assert.Equal(original, restored);
    }

    [Fact]
    public void Compute_pipeline_state_desc_supports_raw_shader_value_semantics()
    {
        var bytecode = new ShaderBytecode(ShaderType.CS)
        {
            Mime = "application/spirv",
            Bytecode = [7, 8, 9]
        };

        var first = new ComputePipelineStateDesc
        {
            DebugName = "ComputeRawShader",
            ComputeShader = new RawShader(bytecode)
        };
        first.Samplers.Add(new NamedSamplerStateDesc(new StringHash("sCompute"), SamplerStateDesc.Bilinear()));

        var second = new ComputePipelineStateDesc(first);

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
            Assert.Contains(bytecode.VertexAttributes, attribute => attribute.Semantic == VertexElementSemantic.Position);
            Assert.Empty(pixelShader.GetBytecode().VertexAttributes);
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
        try
        {
            action();
        }
        finally
        {
            _fixture.Graphics.Shutdown();
            _fixture.Input.Shutdown();
            RbfxTestData.RemoveResourceDirs(_fixture.Resources);
        }
    }
}
