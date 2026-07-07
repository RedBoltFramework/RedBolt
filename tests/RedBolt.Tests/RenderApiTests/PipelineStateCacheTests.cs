using RedBolt;

namespace RedBolt.Tests;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class PipelineStateCacheTests
{
    private readonly HeadlessEngineFixture fixture;

    public PipelineStateCacheTests(HeadlessEngineFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    public void Pipeline_state_cache_is_registered_as_engine_subsystem()
    {
        Assert.Same(fixture.PipelineStateCache, fixture.Context.GetSubsystem<PipelineStateCache>());
    }

    [Fact]
    public void Graphics_pipeline_state_desc_supports_value_semantics_and_initialization()
    {
        var vertexShader = new ShaderVariation(new Shader("Shaders/GLSL/v2/UnitTest.glsl"), ShaderType.VS, "FOO");
        var pixelShader = new ShaderVariation(new Shader("Shaders/GLSL/v2/UnitTest.glsl"), ShaderType.PS, "BAR");

        var first = new GraphicsPipelineStateDesc
        {
            DebugName = "Opaque",
            ColorWriteEnabled = true,
            BlendMode = BlendMode.Replace,
            AlphaToCoverageEnabled = true,
            FillMode = FillMode.Solid,
            CullMode = CullMode.CounterClockwise,
            ConstantDepthBias = 1.0f,
            SlopeScaledDepthBias = 2.0f,
            ScissorTestEnabled = true,
            LineAntiAlias = true,
            DepthWriteEnabled = true,
            StencilTestEnabled = true,
            DepthCompareFunction = CompareMode.LessEqual,
            StencilCompareFunction = CompareMode.Equal,
            StencilOperationOnPassed = StencilOp.Ref,
            StencilOperationOnStencilFailed = StencilOp.Keep,
            StencilOperationOnDepthFailed = StencilOp.Zero,
            StencilCompareMask = 0x0F,
            StencilWriteMask = 0xF0,
            PrimitiveType = PrimitiveType.TriangleFan,
            ReadOnlyDepth = true,
            VertexShader = vertexShader,
            PixelShader = pixelShader
        };
        first.InputLayout.SetElements(
            new InputLayoutElementDesc(0, 32, 0, 0, VertexElementType.Vector3, VertexElementSemantic.Position, 0),
            new InputLayoutElementDesc(0, 32, 12, 0, VertexElementType.Vector3, VertexElementSemantic.Normal, 0));
        first.Output.DepthStencilFormat = TextureFormat.Depth32;
        first.Output.MultiSample = 4;
        first.Output.SetRenderTargetFormats(TextureFormat.RGBA8);
        first.Samplers.Add(new NamedSamplerStateDesc(new StringHash("sAlbedo"), SamplerStateDesc.Trilinear()));

        var second = new GraphicsPipelineStateDesc(first);

        Assert.True(first.IsInitialized());
        Assert.Equal(first, second);
        Assert.Equal(first.ToHash(), second.ToHash());
    }

    [Fact]
    public void Pipeline_state_desc_clones_mutable_state()
    {
        var graphicsDesc = new GraphicsPipelineStateDesc
        {
            DebugName = "CloneMe",
            PrimitiveType = PrimitiveType.TriangleList,
            VertexShader = new ShaderVariation(new Shader("Shaders/GLSL/v2/Clone.glsl"), ShaderType.VS),
            PixelShader = new ShaderVariation(new Shader("Shaders/GLSL/v2/Clone.glsl"), ShaderType.PS)
        };
        graphicsDesc.InputLayout.SetElements(
            new InputLayoutElementDesc(0, 16, 0, 0, VertexElementType.Vector4, VertexElementSemantic.Position, 0));
        graphicsDesc.Output.DepthStencilFormat = TextureFormat.Depth24Stencil8;
        graphicsDesc.Output.SetRenderTargetFormats(TextureFormat.RGBA8);
        graphicsDesc.Samplers.Add(new NamedSamplerStateDesc(new StringHash("Sampler"), SamplerStateDesc.Bilinear()));

        var desc = new PipelineStateDesc(graphicsDesc);
        var clonedGraphics = desc.AsGraphics();

        Assert.NotNull(clonedGraphics);

        graphicsDesc.InputLayout.Clear();
        graphicsDesc.Output.ClearRenderTargetFormats();
        graphicsDesc.Samplers.Clear();

        Assert.Single(clonedGraphics!.InputLayout.Elements.ToArray());
        Assert.Single(clonedGraphics.Output.RenderTargetFormats.ToArray());
        Assert.Equal<uint>(1u, clonedGraphics.Samplers.Size);
    }

    [Fact]
    public void Pipeline_state_cache_reuses_equal_graphics_descriptors()
    {
        var first = new GraphicsPipelineStateDesc
        {
            DebugName = "SharedState",
            VertexShader = new ShaderVariation(new Shader("Shaders/GLSL/v2/Shared.glsl"), ShaderType.VS, "UNIT"),
            PixelShader = new ShaderVariation(new Shader("Shaders/GLSL/v2/Shared.glsl"), ShaderType.PS, "UNIT")
        };
        first.Output.SetRenderTargetFormats(TextureFormat.RGBA8);

        var second = new GraphicsPipelineStateDesc(first);

        var firstState = fixture.PipelineStateCache.GetGraphicsPipelineState(first);
        var secondState = fixture.PipelineStateCache.GetGraphicsPipelineState(second);

        Assert.NotNull(firstState);
        Assert.Same(firstState, secondState);
        Assert.Equal(PipelineStateType.Graphics, firstState!.GetPipelineType());
        Assert.Equal("SharedState", firstState.GetDesc().GetDebugName());
        Assert.True(firstState.IsValid);
    }

    [Fact]
    public void Pipeline_state_cache_rejects_uninitialized_descriptors()
    {
        Assert.Null(fixture.PipelineStateCache.GetGraphicsPipelineState(new GraphicsPipelineStateDesc()));
        Assert.Null(fixture.PipelineStateCache.GetComputePipelineState(new ComputePipelineStateDesc()));
    }
}
