namespace RedBolt.Tests;

public sealed class ViewportRenderBufferTests
{
    [Fact]
    public void Viewport_color_render_buffer_tracks_frame_lifetime()
    {
        var pipeline = new StubRenderPipeline();
        using var buffer = new ViewportColorRenderBuffer(pipeline);
        var frameInfo = new CommonFrameInfo
        {
            ViewportRect = new IntRect(1, 2, 3, 4)
        };

        Assert.Equal(IntRect.Zero, buffer.GetViewportRect());

        pipeline.RaiseRenderBegin(frameInfo);
        Assert.Equal(frameInfo.ViewportRect, buffer.GetViewportRect());
        Assert.Null(buffer.GetTexture());

        pipeline.RaiseRenderEnd(frameInfo);
        Assert.Equal(IntRect.Zero, buffer.GetViewportRect());
    }

    [Fact]
    public void Viewport_depth_stencil_render_buffer_tracks_frame_lifetime()
    {
        var pipeline = new StubRenderPipeline();
        using var buffer = new ViewportDepthStencilRenderBuffer(pipeline);
        var frameInfo = new CommonFrameInfo
        {
            ViewportRect = new IntRect(1, 2, 3, 4)
        };

        Assert.Equal(IntRect.Zero, buffer.GetViewportRect());

        pipeline.RaiseRenderBegin(frameInfo);
        Assert.Equal(frameInfo.ViewportRect, buffer.GetViewportRect());
        Assert.Null(buffer.GetTexture());

        pipeline.RaiseRenderEnd(frameInfo);
        Assert.Equal(IntRect.Zero, buffer.GetViewportRect());
    }

    private sealed class StubRenderPipeline : IRenderPipelineInterface
    {
        public Context? GetContext() => Context.Instance;

        public RenderPipelineDebugger GetDebugger() => null!;

        public bool IsLinearColorSpace => false;

        public event Action<CommonFrameInfo>? UpdateBegin { add { } remove { } }
        public event Action<CommonFrameInfo>? UpdateEnd { add { } remove { } }
        public event Action<CommonFrameInfo>? RenderBegin;
        public event Action<CommonFrameInfo>? RenderEnd;
        public event Action? PipelineStatesInvalidated { add { } remove { } }
        public event RenderPipelineStatsHandler? CollectStatistics { add { } remove { } }

        public void RaiseRenderBegin(CommonFrameInfo frameInfo) => RenderBegin?.Invoke(frameInfo);

        public void RaiseRenderEnd(CommonFrameInfo frameInfo) => RenderEnd?.Invoke(frameInfo);
    }
}
