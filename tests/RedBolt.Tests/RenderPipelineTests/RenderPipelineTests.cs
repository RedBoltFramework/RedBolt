using RedBolt;

namespace RedBolt.Tests;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class RenderPipelineTests
{
    private readonly HeadlessEngineFixture fixture;

    public RenderPipelineTests(HeadlessEngineFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    public void Render_pipeline_collects_render_path_parameters()
    {
        var scene = new Scene();
        var renderPipeline = scene.CreateComponent<RenderPipeline>();
        var renderPath = new RenderPath("RenderPaths/Test.xml");
        renderPath.AddPass(new ParameterRenderPass("Exposure", 1.25f));

        renderPipeline.SetRenderPath(renderPath);

        Assert.Equal(1.25f, Assert.IsType<float>(renderPipeline.GetRenderPathParameters()["Exposure"]));
    }

    [Fact]
    public void Render_pipeline_view_updates_and_renders_enabled_passes()
    {
        var scene = new Scene();
        var renderPipeline = scene.CreateComponent<RenderPipeline>();
        var renderPath = new RenderPath("RenderPaths/Test.xml");
        var trackingPass = new TrackingRenderPass("Tracking");
        renderPath.AddPass(trackingPass);
        renderPipeline.SetRenderPath(renderPath);

        var camera = scene.CreateChild("Camera").CreateComponent<Camera>();
        var viewport = new Viewport(scene, camera, new IntRect(0, 0, 320, 200));
        var view = renderPipeline.Instantiate();

        Assert.True(view.Define(viewport, fixture.Graphics));

        view.Update(fixture.Graphics);
        view.Render(fixture.Graphics, MaterialQuality.High);

        Assert.Equal(1, trackingPass.UpdateCalls);
        Assert.Equal(1, trackingPass.RenderCalls);
        Assert.Equal(new IntVector2(320, 200), view.GetFrameInfo().ViewportSize);
        Assert.Equal(0u, view.GetStats().NumGeometries);
    }

    [Fact]
    public void Render_buffer_manager_supports_secondary_color_buffer_when_requested()
    {
        var manager = new RenderBufferManager();
        manager.SetSettings(new RenderBufferManagerSettings());
        manager.SetFrameSettings(new RenderBufferManagerFrameSettings
        {
            SupportColorReadWrite = true
        });
        manager.OnViewportDefined(null, new IntRect(0, 0, 640, 360));

        var firstColor = manager.GetColorOutput();
        Assert.NotNull(manager.GetSecondaryColorTexture());

        manager.SwapColorBuffers(synchronizeContents: false);

        Assert.NotSame(firstColor, manager.GetColorOutput());
        Assert.Equal(new IntVector2(640, 360), manager.GetOutputSize());
    }

    private sealed class ParameterRenderPass : RenderPass
    {
        private readonly string parameterName;
        private readonly object? value;

        public ParameterRenderPass(string parameterName, object? value)
            : base(parameterName)
        {
            this.parameterName = parameterName;
            this.value = value;
        }

        public override void CollectParameters(IDictionary<string, object?> parameters)
        {
            DeclareParameter(parameterName, value, parameters);
        }

        public override RenderPass Clone() => new ParameterRenderPass(parameterName, value);
    }

    private sealed class TrackingRenderPass : RenderPass
    {
        public TrackingRenderPass(string passName)
            : base(passName)
        {
        }

        public int UpdateCalls { get; private set; }

        public int RenderCalls { get; private set; }

        public override void Update(SharedRenderPassState sharedState)
        {
            ++UpdateCalls;
        }

        public override void Render(SharedRenderPassState sharedState)
        {
            ++RenderCalls;
        }

        public override RenderPass Clone() => new TrackingRenderPass(GetPassName());
    }
}
