namespace RedBolt.Tests;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class SceneProcessorTests
{
    private readonly HeadlessEngineFixture _fixture;

    public SceneProcessorTests(HeadlessEngineFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void IsLightShadowed_RespectsSettingsAndDistance()
    {
        using var scene = new Scene();
        var lightNode = scene.CreateChild("Light");
        var light = lightNode.CreateComponent<Light>();
        light.SetCastShadows(true);
        light.SetLightImportance(LightImportance.Important);
        light.SetShadowIntensity(0.0f);

        var pipeline = new TestRenderPipelineInterface();
        var processor = new SceneProcessor(pipeline, "shadow", null, null);
        processor.SetSettings(new ShaderProgramCompositorSettings());

        Assert.True(processor.IsLightShadowed(light));

        light.SetCastShadows(false);
        Assert.False(processor.IsLightShadowed(light));
    }

    [Fact]
    public void Define_RequiresOctreeAndCamera()
    {
        using var scene = new Scene();
        var cameraNode = scene.CreateChild("Camera");
        var camera = cameraNode.CreateComponent<Camera>();
        var viewport = new Viewport(scene, camera);

        var pipeline = new TestRenderPipelineInterface();
        var processor = new SceneProcessor(pipeline, "shadow", null, null);

        // No octree yet
        Assert.False(processor.Define(new CommonFrameInfo
        {
            Viewport = viewport,
            Camera = camera,
            Scene = scene
        }));

        scene.Root.CreateComponent<Octree>();
        Assert.True(processor.Define(new CommonFrameInfo
        {
            Viewport = viewport,
            Camera = camera,
            Scene = scene
        }));
        Assert.NotNull(processor.GetFrameInfo().Octree);
        Assert.NotNull(processor.GetFrameInfo().Camera);
    }

    [Fact]
    public void GetUserPass_ReturnsNullForBatchCompositor()
    {
        var pipeline = new TestRenderPipelineInterface();
        var processor = new SceneProcessor(pipeline, "shadow", null, null);
        Assert.Null(processor.GetUserPass(processor.GetBatchCompositor()));
    }

    private sealed class TestRenderPipelineInterface : IRenderPipelineInterface
    {
        public Context? GetContext() => Context.Instance;
        public RenderPipelineDebugger GetDebugger() => new();
        public bool IsLinearColorSpace => false;
        public event Action<CommonFrameInfo>? UpdateBegin;
        public event Action<CommonFrameInfo>? UpdateEnd;
        public event Action<CommonFrameInfo>? RenderBegin;
        public event Action<CommonFrameInfo>? RenderEnd;
        public event Action? PipelineStatesInvalidated;
        public event Action<RenderPipelineStats>? CollectStatistics;
    }
}
