using RedBolt.Math;
using RedBolt.RenderAPI;
using RedBolt.RenderPipeline;
namespace RedBolt.Tests;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class LightmapRenderPipelineTests
{
    public LightmapRenderPipelineTests(HeadlessEngineFixture fixture)
    {
    }

    [Fact]
    public void Class_shape_matches_native()
    {
        Assert.True(typeof(LightmapRenderPipelineView).IsClass);
        Assert.False(typeof(LightmapRenderPipelineView).IsSealed);
        Assert.Null(typeof(LightmapRenderPipelineView).GetMethod("GetPositionBuffer"));
        Assert.NotNull(typeof(LightmapRenderPipelineView).GetProperty(nameof(LightmapRenderPipelineView.PositionBuffer)));
        Assert.NotNull(typeof(LightmapRenderPipelineView).GetProperty(nameof(LightmapRenderPipelineView.SmoothPositionBuffer)));
        Assert.NotNull(typeof(LightmapRenderPipelineView).GetProperty(nameof(LightmapRenderPipelineView.FaceNormalBuffer)));
        Assert.NotNull(typeof(LightmapRenderPipelineView).GetProperty(nameof(LightmapRenderPipelineView.SmoothNormalBuffer)));
        Assert.NotNull(typeof(LightmapRenderPipelineView).GetProperty(nameof(LightmapRenderPipelineView.AlbedoBuffer)));
        Assert.NotNull(typeof(LightmapRenderPipelineView).GetProperty(nameof(LightmapRenderPipelineView.EmissionBuffer)));
    }

    [Fact]
    public void Dispose_releases_owned_buffers_in_reverse_declaration_order()
    {
        var disposedBuffers = new List<string>();
        var view = new LightmapRenderPipelineView();
        SetBuffer(view, "_depthBuffer", new TrackingRenderBuffer(view, disposedBuffers, "depth"));
        SetBuffer(view, "_positionBuffer", new TrackingRenderBuffer(view, disposedBuffers, "position"));
        SetBuffer(view, "_smoothPositionBuffer", new TrackingRenderBuffer(view, disposedBuffers, "smoothPosition"));
        SetBuffer(view, "_faceNormalBuffer", new TrackingRenderBuffer(view, disposedBuffers, "faceNormal"));
        SetBuffer(view, "_smoothNormalBuffer", new TrackingRenderBuffer(view, disposedBuffers, "smoothNormal"));
        SetBuffer(view, "_albedoBuffer", new TrackingRenderBuffer(view, disposedBuffers, "albedo"));
        SetBuffer(view, "_emissionBuffer", new TrackingRenderBuffer(view, disposedBuffers, "emission"));

        view.Dispose();

        Assert.Equal(
            ["emission", "albedo", "smoothNormal", "faceNormal", "smoothPosition", "position", "depth"],
            disposedBuffers);
    }

    private static void SetBuffer(LightmapRenderPipelineView view, string fieldName, RenderBuffer buffer)
    {
        var field = typeof(LightmapRenderPipelineView).GetField(
            fieldName,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(field);
        field.SetValue(view, buffer);
    }

    private sealed class TrackingRenderBuffer : RenderBuffer
    {
        private readonly List<string> _disposedBuffers;
        private readonly string _name;

        public TrackingRenderBuffer(
            IRenderPipelineInterface renderPipeline,
            List<string> disposedBuffers,
            string name)
            : base(renderPipeline)
        {
            _disposedBuffers = disposedBuffers;
            _name = name;
        }

        public override RawTexture? GetTexture() => null;

        public override RenderTargetView GetView(int slice = 0) => default;

        public override RenderTargetView GetReadOnlyDepthView(int slice = 0) => default;

        public override IntRect GetViewportRect() => default;

        protected override void OnRenderBegin(CommonFrameInfo frameInfo)
        {
        }

        protected override void OnRenderEnd(CommonFrameInfo frameInfo)
        {
        }

        public override void Dispose()
        {
            _disposedBuffers.Add(_name);
            base.Dispose();
        }
    }
}
