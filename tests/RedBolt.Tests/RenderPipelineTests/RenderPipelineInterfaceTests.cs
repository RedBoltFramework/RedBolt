namespace RedBolt.Tests;

[Collection(nameof(GraphicsEngineCollection))]
public sealed class RenderPipelineInterfaceTests(GraphicsEngineFixture fixture)
{
    [Fact]
    public void Render_pipeline_view_exposes_global_context()
    {
        var scene = new Scene();
        var camera = scene.CreateChild("Camera").CreateComponent<Camera>();
        scene.CreateComponent<RenderPipeline>();
        var viewport = new Viewport(scene, camera);
        viewport.AllocateView();

        var view = viewport.GetRenderPipelineView();

        Assert.NotNull(view);
        Assert.Same(Context.Instance, view!.GetContext());
    }

    [Fact]
    public void Render_pipeline_view_dispose_releases_signal_subscribers()
    {
        var scene = new Scene();
        var pipeline = scene.CreateComponent<RenderPipeline>();
        var view = new TrackingRenderPipelineView(pipeline);
        var updateBeginCount = 0;
        view.UpdateBegin += _ => ++updateBeginCount;

        view.RaiseUpdateBeginForTest();
        view.Dispose();
        view.RaiseUpdateBeginForTest();

        Assert.Equal(1, updateBeginCount);
    }

    [Fact]
    public void Auto_exposure_resets_adapted_luminance_after_device_reset()
    {
        var parameters = new EngineParameters
        {
            ApplicationName = "RedBolt.Tests.RenderPipelineInterfaceTests",
            Headless = false,
            WindowWidth = 64,
            WindowHeight = 64
        };
        fixture.InitializeGraphics(parameters);
        try
        {
            var scene = new Scene();
            var camera = scene.CreateChild("Camera").CreateComponent<Camera>();
            scene.CreateComponent<RenderPipeline>();
            var viewport = new Viewport(scene, camera);
            viewport.AllocateView();

            var view = viewport.GetRenderPipelineView();
            Assert.NotNull(view);

            var pass = new AutoExposurePass();
            pass.InitializeView(view!);

            var initializedField = typeof(AutoExposurePass).GetField("_isAdaptedLuminanceInitialized",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.NotNull(initializedField);
            initializedField!.SetValue(pass, true);

            Assert.True(fixture.RenderDevice.EmulateLossAndRestore());

            Assert.False((bool)initializedField.GetValue(pass)!);
        }
        finally
        {
            fixture.ResetGraphics();
        }
    }

    private sealed class TrackingRenderPipelineView(RenderPipeline renderPipeline) : RenderPipelineView(renderPipeline)
    {
        private RenderPipelineStats _stats;

        public override bool IsLinearColorSpace => false;

        public void RaiseUpdateBeginForTest() => RaiseUpdateBegin(default);

        public override bool Define(RenderSurface? renderTarget, Viewport viewport, Graphics graphics) => true;

        public override void Update(Graphics graphics)
        {
        }

        public override void Render(Graphics graphics, MaterialQuality materialQuality)
        {
        }

        public override FrameInfo GetFrameInfo() => default;

        public override ref readonly RenderPipelineStats GetStats() => ref _stats;

        public override void DrawDebugGeometries(bool depthTest)
        {
        }

        public override void DrawDebugLights(bool depthTest)
        {
        }
    }
}
