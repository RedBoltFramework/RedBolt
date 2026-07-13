namespace RedBolt.Tests;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class RenderPipelineInterfaceTests(HeadlessEngineFixture fixture)
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
    public void Auto_exposure_resets_adapted_luminance_after_device_reset()
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
}
