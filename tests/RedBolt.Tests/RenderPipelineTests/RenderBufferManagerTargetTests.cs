namespace RedBolt.Tests;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class RenderBufferManagerTargetTests : IDisposable
{
    private readonly HeadlessEngineFixture _fixture;
    private bool _graphicsInitialized;

    public RenderBufferManagerTargetTests(HeadlessEngineFixture fixture)
    {
        this._fixture = fixture;
    }

    [Fact]
    public void Set_render_targets_rect_uses_depth_first_multi_color_api()
    {
        EnsureGraphics();
        var pipeline = new TestRenderPipelineInterface();
        using var manager = new RenderBufferManager(pipeline);
        var frameInfo = pipeline.BeginFrame(manager, null, new IntRect(0, 0, 320, 200));

        var depthBuffer = manager.GetDepthStencilOutput();
        var colorBuffer = manager.GetColorOutput();

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

        Assert.IsType<TextureRenderBuffer>(manager.GetColorOutput());
        Assert.IsType<TextureRenderBuffer>(manager.GetDepthStencilOutput());
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
        _fixture.Input.Initialize(parameters);
        _fixture.Graphics.Initialize(parameters, _fixture.Input);
        _fixture.RenderDevice.RestoreDeviceState();
        _graphicsInitialized = true;
    }

    public void Dispose()
    {
        if (!_graphicsInitialized)
            return;

        _fixture.RenderDevice.InvalidateDeviceState();
        _fixture.Graphics.Shutdown();
        _fixture.Input.Shutdown();
        RbfxTestData.RemoveResourceDirs(_fixture.Resources);
    }
}
