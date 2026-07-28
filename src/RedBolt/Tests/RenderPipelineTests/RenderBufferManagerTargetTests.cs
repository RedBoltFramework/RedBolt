namespace RedBolt.Tests;

using RedBolt.Engine;
using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.RenderPipeline;

[Collection(nameof(GraphicsEngineCollection))]
public sealed class RenderBufferManagerTargetTests : IDisposable
{
    private readonly GraphicsEngineFixture _fixture;
    private bool _graphicsInitialized;

    public RenderBufferManagerTargetTests(GraphicsEngineFixture fixture)
    {
        this._fixture = fixture;
    }

    [Fact]
    public void Class_shape_matches_native()
    {
        Assert.True(typeof(RenderBufferManager).IsClass);
        Assert.False(typeof(RenderBufferManager).IsSealed);
        Assert.All(
            typeof(RenderBufferManager).GetMethods().Where(method => method.Name == nameof(RenderBufferManager.CreateColorBuffer)),
            method => Assert.Equal(typeof(RenderBuffer), method.ReturnType));
    }

    [Fact]
    public void Set_render_targets_rect_uses_depth_first_multi_color_api()
    {
        EnsureGraphics();
        var pipeline = new TestRenderPipelineInterface();
        using var manager = new RenderBufferManager(pipeline);
        var frameInfo = pipeline.BeginFrame(manager, null, new IntRect(0, 0, 320, 200));

        var depthBuffer = manager.DepthStencilOutput;
        var colorBuffer = manager.ColorOutput!;

        manager.SetRenderTargetsRect(
            IntRect.Zero,
            depthBuffer,
            new[] { colorBuffer },
            readOnlyDepth: false,
            face: CubeMapFace.PositiveX);

        manager.SetOutputRenderTargetsRect(new IntRect(0, 0, 160, 100));
        pipeline.EndFrame(frameInfo);
    }

    [Fact]
    public void Backbuffer_subviewport_uses_substitute_output_buffers()
    {
        EnsureGraphics();
        var pipeline = new TestRenderPipelineInterface();
        using var manager = new RenderBufferManager(pipeline);
        manager.SetSettings(new RenderBufferManagerSettings { ReadableDepth = true });
        var viewportRect = new IntRect(0, 0, 1, 1);

        var frameInfo = pipeline.BeginFrame(manager, null, viewportRect);

        Assert.IsType<TextureRenderBuffer>(manager.ColorOutput);
        Assert.IsType<TextureRenderBuffer>(manager.DepthStencilOutput);
        pipeline.EndFrame(frameInfo);
    }

    private void EnsureGraphics()
    {
        if (_graphicsInitialized)
            return;

        var parameters = new EngineParameters
        {
            ApplicationName = "RedBolt.Tests.RenderBufferManagerTargetTests",
            Headless = false,
            WindowWidth = 64,
            WindowHeight = 64
        };
        RbfxTestData.AddResourceDirs(_fixture.Resources);
        _fixture.InitializeGraphics(parameters);
        _graphicsInitialized = true;
    }

    public void Dispose()
    {
        if (!_graphicsInitialized)
            return;

            _fixture.ResetGraphics();
        RbfxTestData.RemoveResourceDirs(_fixture.Resources);
    }
}
