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
}
