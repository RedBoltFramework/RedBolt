namespace RedBolt.Tests;

public sealed class RenderPassTests
{
    [Fact]
    public void Connect_to_render_buffer_rebinds_replaced_buffer_and_clears_missing_optional_buffer()
    {
        var pass = new TrackingRenderPass();
        var firstBuffer = new StubRenderBuffer();
        var secondBuffer = new StubRenderBuffer();
        var sharedState = new SharedRenderPassState();

        sharedState.RenderBuffers["Test"] = firstBuffer;
        Assert.True(pass.Connect(sharedState, required: false));
        Assert.Same(firstBuffer, pass.Buffer);

        sharedState.RenderBuffers["Test"] = secondBuffer;
        Assert.True(pass.Connect(sharedState, required: false));
        Assert.Same(secondBuffer, pass.Buffer);

        sharedState.RenderBuffers.Clear();
        Assert.False(pass.Connect(sharedState, required: false));
        Assert.Null(pass.Buffer);
    }

    private sealed class TrackingRenderPass : RenderPass
    {
        private RenderBuffer? buffer;

        public TrackingRenderPass()
            : base("Tracking")
        {
        }

        public RenderBuffer? Buffer => buffer;

        public bool Connect(SharedRenderPassState sharedState, bool required)
        {
            return ConnectToRenderBuffer(ref buffer, "Test", sharedState, required);
        }

        public override RenderPass Clone() => new TrackingRenderPass();
    }

    private sealed class StubRenderBuffer : RenderBuffer
    {
        public override Texture? GetTexture() => null;

        public override RenderTargetView GetView(uint slice = 0u) => RenderTargetView.SwapChainColor();

        public override RenderTargetView GetReadOnlyDepthView(uint slice = 0u) => RenderTargetView.SwapChainDepthStencil();

        public override IntRect GetViewportRect() => new(0, 0, 1, 1);
    }
}
