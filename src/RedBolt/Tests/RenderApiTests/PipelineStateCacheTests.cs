using RedBolt;
using RedBolt.Container;
using RedBolt.Core;
using RedBolt.Engine;
using RedBolt.Graphics;
using CullMode = global::RedBolt.Graphics.CullMode;
using FillMode = global::RedBolt.Graphics.FillMode;
using RedBolt.RenderAPI;
using ShaderType = global::RedBolt.RenderAPI.ShaderType;

namespace RedBolt.Tests;

[Collection(nameof(GraphicsEngineCollection))]
public sealed class PipelineStateCacheTests
{
    private readonly GraphicsEngineFixture _fixture;

    public PipelineStateCacheTests(GraphicsEngineFixture fixture)
    {
        this._fixture = fixture;
    }

    [Fact]
    public void Pipeline_state_cache_is_registered_as_engine_subsystem()
    {
        Assert.Same(_fixture.PipelineStateCache, _fixture.Context.GetSubsystem<PipelineStateCache>());
    }

    [Fact]
    public void Pipeline_state_cache_matches_native_reference_class_and_weak_owner_lifetime()
    {
        Assert.False(typeof(PipelineStateCache).IsSealed);

        var cache = new PipelineStateCache();
        var graphicsDesc = new GraphicsPipelineStateDesc
        {
            VertexShader = CreateRawShader(ShaderType.VS),
            PixelShader = CreateRawShader(ShaderType.PS)
        };
        var pipelineState = cache.GetGraphicsPipelineState(graphicsDesc);
        Assert.NotNull(pipelineState);

        var ownerField = typeof(PipelineState).GetField(
            "_owner", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var owner = Assert.IsType<WeakReference<PipelineStateCache>>(ownerField!.GetValue(pipelineState));
        Assert.True(owner.TryGetTarget(out var ownerBeforeDispose));
        Assert.Same(cache, ownerBeforeDispose);

        cache.Dispose();

        Assert.False(owner.TryGetTarget(out _));
        pipelineState!.Dispose();
    }

    [Fact]
    public void Graphics_pipeline_state_desc_supports_value_semantics_and_initialization()
    {
        var vertexShader = CreateRawShader(ShaderType.VS);
        var pixelShader = CreateRawShader(ShaderType.PS);

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
        first.InputLayout.Size = 2;
        first.InputLayout.Elements[0] = new InputLayoutElementDesc(
            0, 32, 0, 0, VertexElementType.Vector3, VertexElementSemantic.Position, 0);
        first.InputLayout.Elements[1] = new InputLayoutElementDesc(
            0, 32, 12, 0, VertexElementType.Vector3, VertexElementSemantic.Normal, 0);
        first.Output.DepthStencilFormat = TextureFormat.TexFormatD32Float;
        first.Output.MultiSample = 4;
        first.Output.NumRenderTargets = 1;
        first.Output.RenderTargetFormats[0] = TextureFormat.TexFormatRgba8Unorm;
        first.Samplers.Add(new StringHash("sAlbedo"), SamplerStateDesc.Trilinear());

        var second = first;

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
            VertexShader = CreateRawShader(ShaderType.VS),
            PixelShader = CreateRawShader(ShaderType.PS)
        };
        graphicsDesc.InputLayout.Size = 1;
        graphicsDesc.InputLayout.Elements[0] = new InputLayoutElementDesc(
            0, 16, 0, 0, VertexElementType.Vector4, VertexElementSemantic.Position, 0);
        graphicsDesc.Output.DepthStencilFormat = TextureFormat.TexFormatD24UnormS8Uint;
        graphicsDesc.Output.NumRenderTargets = 1;
        graphicsDesc.Output.RenderTargetFormats[0] = TextureFormat.TexFormatRgba8Unorm;
        graphicsDesc.Samplers.Add(new StringHash("Sampler"), SamplerStateDesc.Bilinear());

        var desc = new PipelineStateDesc(graphicsDesc);
        var clonedGraphics = desc.AsGraphics();

        Assert.NotNull(clonedGraphics);

        graphicsDesc.InputLayout = default;
        graphicsDesc.Output.NumRenderTargets = 0;
        graphicsDesc.Samplers.Clear();

        Assert.True(clonedGraphics.HasValue);
        var cloned = clonedGraphics.Value;
        Assert.Equal(1, cloned.InputLayout.Size);
        Assert.Equal(1, cloned.Output.NumRenderTargets);
        Assert.Equal(1, cloned.Samplers.Size);
    }

    [Fact]
    public void Pipeline_state_cache_reuses_equal_graphics_descriptors()
    {
        var first = new GraphicsPipelineStateDesc
        {
            DebugName = "SharedState",
            VertexShader = CreateRawShader(ShaderType.VS),
            PixelShader = CreateRawShader(ShaderType.PS)
        };
        first.Output.NumRenderTargets = 1;
        first.Output.RenderTargetFormats[0] = TextureFormat.TexFormatRgba8Unorm;

        var second = first;

        var firstState = _fixture.PipelineStateCache.GetGraphicsPipelineState(first);
        var secondState = _fixture.PipelineStateCache.GetGraphicsPipelineState(second);

        Assert.NotNull(firstState);
        Assert.Same(firstState, secondState);
        Assert.Equal(PipelineStateType.Graphics, firstState!.GetPipelineType());
        Assert.Equal("SharedState", firstState.GetDesc().GetDebugName());
        Assert.False(firstState.IsValid);
        Assert.Null(firstState.GetHandle());
    }

    [Fact]
    public void Pipeline_state_cache_rejects_uninitialized_descriptors()
    {
        Assert.Null(_fixture.PipelineStateCache.GetGraphicsPipelineState(new GraphicsPipelineStateDesc()));
        Assert.Null(_fixture.PipelineStateCache.GetComputePipelineState(new ComputePipelineStateDesc()));
    }

    [Fact]
    public void Pipeline_state_cache_restore_does_not_reenter_pipeline_states()
    {
        var desc = new GraphicsPipelineStateDesc
        {
            DebugName = "RestoreState",
            VertexShader = CreateRawShader(ShaderType.VS),
            PixelShader = CreateRawShader(ShaderType.PS)
        };
        desc.Output.NumRenderTargets = 1;
        desc.Output.RenderTargetFormats[0] = TextureFormat.TexFormatRgba8Unorm;

        var state = _fixture.PipelineStateCache.GetGraphicsPipelineState(desc);

        Assert.NotNull(state);
        _fixture.PipelineStateCache.Restore();
        Assert.NotNull(state);
    }

    [Fact]
    public void Pipeline_state_dispose_releases_cache_entry()
    {
        var desc = new GraphicsPipelineStateDesc
        {
            DebugName = "DisposableState",
            VertexShader = CreateRawShader(ShaderType.VS),
            PixelShader = CreateRawShader(ShaderType.PS)
        };
        desc.Output.NumRenderTargets = 1;
        desc.Output.RenderTargetFormats[0] = TextureFormat.TexFormatRgba8Unorm;

        var firstState = _fixture.PipelineStateCache.GetGraphicsPipelineState(desc);
        Assert.NotNull(firstState);

        firstState!.Dispose();

        var secondState = _fixture.PipelineStateCache.GetGraphicsPipelineState(desc);
        Assert.NotNull(secondState);
        Assert.NotSame(firstState, secondState);
    }

    [Fact]
    public void Pipeline_state_cache_matches_unavailable_native_cache_lifecycle()
    {
        _fixture.PipelineStateCache.Initialize([1, 2, 3]);

        Assert.Empty(_fixture.PipelineStateCache.GetCachedData());
    }

    [Fact]
    public void Graphics_pipeline_state_creates_native_handle_binding_and_reflection()
    {
        WithInitializedGraphics(() =>
        {
            var desc = new GraphicsPipelineStateDesc
            {
                DebugName = "NativePipelineState",
                ColorWriteEnabled = true,
                DepthCompareFunction = CompareMode.Always,
                PrimitiveType = PrimitiveType.TriangleList,
                VertexShader = new TestRawShader(_fixture.Graphics.GetShader(ShaderType.VS, "X_CopyTexture")!.GetBytecode()),
                PixelShader = _fixture.Graphics.GetShader(ShaderType.PS, "X_CopyTexture")
            };
            desc.InputLayout.Size = 1;
            desc.InputLayout.Elements[0] = new InputLayoutElementDesc(
                0, 12, 0, 0, VertexElementType.Vector3, VertexElementSemantic.Position, 0);
            desc.Output.NumRenderTargets = 1;
            desc.Output.RenderTargetFormats[0] = TextureFormat.TexFormatRgba8Unorm;
            desc.Samplers.Add(new StringHash("Albedo"), SamplerStateDesc.Trilinear());

            var state = _fixture.PipelineStateCache.GetGraphicsPipelineState(desc);

            Assert.NotNull(state);
            Assert.True(state!.IsValid);
            Assert.NotNull(state.GetHandle());
            Assert.NotNull(state.GetShaderResourceBinding());
            Assert.NotNull(state.GetReflection());

            var vertexBytecode = desc.VertexShader!.GetBytecode();
            ((TestRawShader)desc.VertexShader).Reload(vertexBytecode);
            Assert.False(state.IsValid);
            Assert.False(state.IsDataLost());

            _fixture.RenderDevice.Present();
            Assert.True(state.IsValid);
            Assert.False(state.IsDataLost());
        });
    }

    private sealed class TestRawShader(ShaderBytecode bytecode) : RawShader(bytecode)
    {
        public void Reload(ShaderBytecode value) => CreateFromBinary(value);
    }

    private static RawShader CreateRawShader(ShaderType type) => new(new ShaderBytecode(type));

    private void WithInitializedGraphics(Action action)
    {
        var parameters = new EngineParameters
        {
            ApplicationName = "RedBolt.Tests.PipelineStateCacheTests",
            Headless = false,
            WindowWidth = 64,
            WindowHeight = 64
        };
        RbfxTestData.AddResourceDirs(_fixture.Resources);
        _fixture.InitializeGraphics(parameters);
        try { action(); }
        finally
        {
            _fixture.ResetGraphics();
            RbfxTestData.RemoveResourceDirs(_fixture.Resources);
        }
    }
}
