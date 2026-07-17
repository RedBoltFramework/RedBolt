namespace RedBolt.Tests;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class RenderBufferManagerTargetTests
{
    private readonly HeadlessEngineFixture _fixture;

    public RenderBufferManagerTargetTests(HeadlessEngineFixture fixture)
    {
        this._fixture = fixture;
    }

    [Fact]
    public void Set_render_targets_rect_uses_depth_first_multi_color_api()
    {
        var manager = new RenderBufferManager();
        manager.OnViewportDefined(null, new IntRect(0, 0, 320, 200));

        var depthBuffer = manager.GetDepthStencilOutput();
        var colorBuffer = manager.GetColorOutput();

        manager.SetRenderTargetsRect(
            _fixture.Graphics,
            IntRect.Zero,
            depthBuffer,
            new[] { colorBuffer },
            readOnlyDepth: false,
            face: CubeMapFace.PositiveX);

        manager.SetOutputRenderTargetsRect(_fixture.Graphics, new IntRect(0, 0, 160, 100));
    }

    [Fact]
    public void Backbuffer_subviewport_uses_substitute_output_buffers()
    {
        var manager = new RenderBufferManager();
        manager.SetSettings(new RenderBufferManagerSettings { ReadableDepth = true });
        var viewportRect = new IntRect(0, 0, 1, 1);

        manager.OnViewportDefined(null, viewportRect);

        Assert.IsType<TextureRenderBuffer>(manager.GetColorOutput());
        Assert.IsType<TextureRenderBuffer>(manager.GetDepthStencilOutput());
    }
}
