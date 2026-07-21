namespace RedBolt.Tests;

public sealed class RenderPassTests
{
    [Fact]
    public void Default_enabled_attribute_does_not_change_runtime_user_state()
    {
        var pass = new TrackingRenderPass();

        Assert.True(pass.IsEnabledByDefault());
        Assert.False(pass.IsEnabledEffectively());

        pass.SetEnabled(true);
        pass.SetEnabledByDefault(false);

        Assert.False(pass.IsEnabledByDefault());
        Assert.True(pass.IsEnabledEffectively());
    }

    [Fact]
    public void Clone_copies_attributes_without_copying_runtime_user_state()
    {
        var pass = new TrackingRenderPass();
        pass.SetEnabled(true);
        pass.SetEnabledByDefault(false);
        pass.SetComment("Comment");

        var clone = Assert.IsType<TrackingRenderPass>(pass.Clone());

        Assert.Equal("Tracking", clone.GetPassName());
        Assert.False(clone.IsEnabledByDefault());
        Assert.Equal("Comment", clone.GetComment());
        Assert.False(clone.IsEnabledEffectively());
    }

    [Fact]
    public void Clone_resets_empty_variant_default_without_dereferencing_it()
    {
        var pass = new NullableAttributeRenderPass();

        var clone = Assert.IsType<NullableAttributeRenderPass>(pass.Clone());

        Assert.Null(clone.Optional);
    }

    [Fact]
    public void Connect_to_render_buffer_keeps_existing_buffer_when_shared_state_changes()
    {
        var pass = new TrackingRenderPass();
        var firstBuffer = new StubRenderBuffer();
        var secondBuffer = new StubRenderBuffer();
        var sharedState = new SharedRenderPassState();

        sharedState.RenderBuffers["Test"] = firstBuffer;
        pass.Connect(sharedState, required: false);
        Assert.Same(firstBuffer, pass.Buffer);

        sharedState.RenderBuffers["Test"] = secondBuffer;
        pass.Connect(sharedState, required: false);
        Assert.Same(firstBuffer, pass.Buffer);

        sharedState.RenderBuffers.Clear();
        pass.Connect(sharedState, required: false);
        Assert.Same(firstBuffer, pass.Buffer);
    }

    private sealed class TrackingRenderPass : RenderPass
    {
        private RenderBuffer? _buffer;

        public TrackingRenderPass()
        {
            _passName = "Tracking";
        }

        public RenderBuffer? Buffer => _buffer;

        public void Connect(SharedRenderPassState sharedState, bool required)
        {
            ConnectToRenderBuffer(ref _buffer, "Test", sharedState, required);
        }
    }

    private sealed class StubRenderBuffer : RenderBuffer
    {
        public override Texture? GetTexture() => null;

        public override RenderTargetView GetView(int slice = 0) => default;

        public override RenderTargetView GetReadOnlyDepthView(int slice = 0) => default;

        public override IntRect GetViewportRect() => new(0, 0, 1, 1);
    }

    private sealed class NullableAttributeRenderPass : RenderPass
    {
        private string? _optional;

        public NullableAttributeRenderPass()
        {
            RegisterAttribute(
                "Optional",
                typeof(string),
                self => ((NullableAttributeRenderPass)self)._optional,
                (self, value) => ((NullableAttributeRenderPass)self)._optional = value as string);
        }

        public string? Optional => _optional;
    }
}
