using RedBolt.Core;
using RedBolt.Math;
using RedBolt.RenderAPI;
using RedBolt.RenderPipeline;

namespace RedBolt.Tests;

public sealed partial class RenderPassTests
{
    [Fact]
    public void Default_enabled_attribute_does_not_change_runtime_user_state()
    {
        var pass = new RenderPassProbe();

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
        var pass = new RenderPassProbe();
        pass.SetEnabled(true);
        pass.SetEnabledByDefault(false);
        pass.SetComment("Comment");

        var clone = Assert.IsType<RenderPassProbe>(pass.Clone());

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
        var pass = new RenderPassProbe();
        var pipeline = new StubRenderPipeline();
        using var firstBuffer = new StubRenderBuffer(pipeline);
        using var secondBuffer = new StubRenderBuffer(pipeline);
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

    [Reflected]
    private sealed partial class RenderPassProbe : RenderPass
    {
        private WeakReference<RenderBuffer>? _buffer;

        public RenderPassProbe()
        {
            _passName = "Tracking";
        }

        public RenderBuffer? Buffer => _buffer is not null && _buffer.TryGetTarget(out var buffer) ? buffer : null;

        public void Connect(SharedRenderPassState sharedState, bool required)
        {
            ConnectToRenderBuffer(ref _buffer, "Test", sharedState, required);
        }
    }

    private sealed class StubRenderBuffer : RenderBuffer
    {
        public StubRenderBuffer(IRenderPipelineInterface renderPipeline)
            : base(renderPipeline)
        {
        }

        public override RawTexture? GetTexture() => null;

        public override RenderTargetView GetView(int slice = 0) => default;

        public override RenderTargetView GetReadOnlyDepthView(int slice = 0) => default;

        public override IntRect GetViewportRect() => new(0, 0, 1, 1);

        protected override void OnRenderBegin(CommonFrameInfo frameInfo)
        {
        }

        protected override void OnRenderEnd(CommonFrameInfo frameInfo)
        {
        }
    }

    private sealed class StubRenderPipeline : IRenderPipelineInterface
    {
        public Context? GetContext() => Context.Instance;

        public RenderPipelineDebugger GetDebugger() => null!;

        public bool IsLinearColorSpace => false;

        public event Action<CommonFrameInfo>? UpdateBegin { add { } remove { } }
        public event Action<CommonFrameInfo>? UpdateEnd { add { } remove { } }
        public event Action<CommonFrameInfo>? RenderBegin { add { } remove { } }
        public event Action<CommonFrameInfo>? RenderEnd { add { } remove { } }
        public event Action? PipelineStatesInvalidated { add { } remove { } }
        public event RenderPipelineStatsHandler? CollectStatistics { add { } remove { } }
    }

    [Reflected]
    private sealed partial class NullableAttributeRenderPass : RenderPass
    {
        [Serialized("Optional")]
        private string? _optional;

        public NullableAttributeRenderPass()
        {
        }

        public string? Optional => _optional;
    }
}
