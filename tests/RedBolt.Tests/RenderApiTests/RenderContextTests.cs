using RedBolt;

namespace RedBolt.Tests;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class RenderContextTests
{
    private readonly HeadlessEngineFixture _fixture;

    public RenderContextTests(HeadlessEngineFixture fixture)
    {
        this._fixture = fixture;
    }

    [Fact]
    public void Render_context_binds_swap_chain_targets_and_tracks_viewport_state()
    {
        WithInitializedGraphics(() =>
        {
            var renderContext = _fixture.RenderContext;

            renderContext.SetSwapChainRenderTargets();

            Assert.True(renderContext.IsSwapChainRenderTarget());
            Assert.Equal(new Vector2i(64, 64), renderContext.GetCurrentRenderTargetSize());
            Assert.Equal(new IntRect(0, 0, 64, 64), renderContext.GetCurrentViewport());

            renderContext.SetViewport(new IntRect(4, 8, 16, 24));

            Assert.Equal(new IntRect(4, 8, 16, 24), renderContext.GetCurrentViewport());
            Assert.Equal(new Vector2i(64, 64), renderContext.GetCurrentRenderTargetSize());

            var outputDesc = renderContext.GetCurrentRenderTargetsDesc();
            Assert.Equal<uint>(1u, outputDesc.NumRenderTargets);
            Assert.NotEqual(TextureFormat.Unknown, outputDesc.RenderTargetFormats[0]);
            Assert.NotEqual(TextureFormat.Unknown, outputDesc.DepthStencilFormat);
            Assert.True(outputDesc.MultiSample >= 1u);
        });
    }

    [Fact]
    public void Render_context_binds_texture_targets_and_reports_bound_textures()
    {
        WithInitializedGraphics(() =>
        {
            var renderContext = _fixture.RenderContext;
            var colorTexture = new Texture2D("Textures/RenderContextColor");
            var depthTexture = new Texture2D("Textures/RenderContextDepth");
            Assert.True(colorTexture.SetSize(32, 16, TextureFormat.RGBA8));
            Assert.True(depthTexture.SetSize(32, 16, TextureFormat.Depth24Stencil8));

            renderContext.SetRenderTargets(RenderTargetView.Texture(depthTexture), [RenderTargetView.Texture(colorTexture)]);

            Assert.False(renderContext.IsSwapChainRenderTarget());
            Assert.Equal(new Vector2i(32, 16), renderContext.GetCurrentRenderTargetSize());
            Assert.Equal(new IntRect(0, 0, 32, 16), renderContext.GetCurrentViewport());
            Assert.True(renderContext.IsBoundAsRenderTarget(colorTexture));
            Assert.True(renderContext.IsBoundAsRenderTarget(depthTexture));

            var outputDesc = renderContext.GetCurrentRenderTargetsDesc();
            Assert.Equal<uint>(1u, outputDesc.NumRenderTargets);
            Assert.Equal(TextureFormat.RGBA8, outputDesc.RenderTargetFormats[0]);
            Assert.Equal(TextureFormat.Depth24Stencil8, outputDesc.DepthStencilFormat);
            Assert.Equal<uint>(1u, outputDesc.MultiSample);

            renderContext.ResetRenderTargets();

            Assert.False(renderContext.IsBoundAsRenderTarget(colorTexture));
            Assert.Equal(default, renderContext.GetCurrentRenderTargetSize());
            Assert.Equal<uint>(0u, renderContext.GetCurrentRenderTargetsDesc().NumRenderTargets);
        });
    }

    [Fact]
    public void Render_context_binds_and_clears_multiple_color_targets()
    {
        WithInitializedGraphics(() =>
        {
            var renderContext = _fixture.RenderContext;
            var firstColor = new Texture2D("Textures/RenderContextFirstColor");
            var secondColor = new Texture2D("Textures/RenderContextSecondColor");
            var depth = new Texture2D("Textures/RenderContextMrtDepth");
            Assert.True(firstColor.SetSize(32, 16, TextureFormat.RGBA8));
            Assert.True(secondColor.SetSize(32, 16, TextureFormat.RG16F));
            Assert.True(depth.SetSize(32, 16, TextureFormat.Depth24Stencil8));

            renderContext.SetRenderTargets(RenderTargetView.Texture(depth),
                [RenderTargetView.Texture(firstColor), RenderTargetView.Texture(secondColor)]);

            var outputDesc = renderContext.GetCurrentRenderTargetsDesc();
            Assert.Equal<uint>(2u, outputDesc.NumRenderTargets);
            Assert.Equal(TextureFormat.RGBA8, outputDesc.RenderTargetFormats[0]);
            Assert.Equal(TextureFormat.RG16F, outputDesc.RenderTargetFormats[1]);
            Assert.True(renderContext.IsBoundAsRenderTarget(firstColor));
            Assert.True(renderContext.IsBoundAsRenderTarget(secondColor));
            renderContext.ClearRenderTarget(1, Color.Black);
        });
    }

    [Fact]
    public void Render_scope_submits_balanced_debug_group_when_enabled()
    {
        WithInitializedGraphics(() =>
        {
            var renderContext = _fixture.RenderContext;
            renderContext.SetSwapChainRenderTargets();
            var before = renderContext.GetCurrentViewport();

            using (new RenderScope(renderContext, "EnabledScope"))
            {
                renderContext.SetClipPlaneMask(7);
            }

            Assert.True(renderContext.IsDebugScopeEnabled());
            Assert.Equal(before, renderContext.GetCurrentViewport());
        });
    }

    private void WithInitializedGraphics(Action action)
    {
        var parameters = new EngineParameters
        {
            ApplicationName = "RedBolt.Tests.RenderContextTests",
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
