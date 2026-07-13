namespace RedBolt.Tests;

public sealed class ViewportRenderBufferTests
{
    [Fact]
    public void Viewport_color_render_buffer_tracks_frame_lifetime()
    {
        var buffer = new ViewportColorRenderBuffer();
        var frameInfo = new CommonFrameInfo
        {
            ViewportRect = new IntRect(1, 2, 3, 4)
        };

        Assert.Equal(IntRect.Zero, buffer.GetViewportRect());

        buffer.SetOutput(null, null, null, frameInfo.ViewportRect);
        Assert.Equal(frameInfo.ViewportRect, buffer.GetViewportRect());
        Assert.Null(buffer.GetTexture());

        buffer.OnRenderEnd(frameInfo);
        Assert.Equal(IntRect.Zero, buffer.GetViewportRect());
    }

    [Fact]
    public void Viewport_depth_stencil_render_buffer_tracks_frame_lifetime()
    {
        var buffer = new ViewportDepthStencilRenderBuffer();
        var frameInfo = new CommonFrameInfo
        {
            ViewportRect = new IntRect(1, 2, 3, 4)
        };

        Assert.Equal(IntRect.Zero, buffer.GetViewportRect());

        buffer.SetOutput(null, null, null, frameInfo.ViewportRect);
        Assert.Equal(frameInfo.ViewportRect, buffer.GetViewportRect());
        Assert.Null(buffer.GetTexture());

        buffer.OnRenderEnd(frameInfo);
        Assert.Equal(IntRect.Zero, buffer.GetViewportRect());
    }
}
