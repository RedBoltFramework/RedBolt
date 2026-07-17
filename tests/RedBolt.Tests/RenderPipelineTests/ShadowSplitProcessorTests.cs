namespace RedBolt.Tests;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class ShadowSplitProcessorTests
{
    private readonly HeadlessEngineFixture _fixture;

    public ShadowSplitProcessorTests(HeadlessEngineFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void LightProcessor_InitializesCookedMatricesToIdentity()
    {
        using var scene = new Scene();
        var light = scene.CreateChild("Light").CreateComponent<Light>();
        var processor = new LightProcessor(light);

        try
        {
            ref readonly var parameters = ref processor.GetParams();
            Assert.Equal(Matrix4.Identity, parameters.LightShapeMatrix);
            Assert.All(parameters.LightMatrices, matrix => Assert.Equal(Matrix4.Identity, matrix));
        }
        finally
        {
            processor.Dispose();
        }
    }

    [Fact]
    public void InitializeSpot_ConfiguresShadowCameraFromLight()
    {
        using var scene = new Scene();
        var lightNode = scene.CreateChild("Light");
        lightNode.SetWorldPosition(new Vector3f(1, 2, 3));
        lightNode.SetWorldRotation(Quaternion.Identity);
        var light = lightNode.CreateComponent<Light>();
        light.SetLightType(LightType.Spot);
        light.SetRange(25.0f);
        light.SetFov(45.0f);
        light.SetAspectRatio(1.5f);
        light.SetShadowNearFarRatio(0.01f);

        var processor = new LightProcessor(light);
        var split = new ShadowSplitProcessor(processor, 0);

        split.InitializeSpot();

        var camera = split.GetShadowCamera();
        Assert.False(camera.IsOrthographic());
        Assert.Equal(25.0f, camera.GetFarClip(), 0.001f);
        Assert.Equal(25.0f * 0.01f, camera.GetNearClip(), 0.001f);
        Assert.Equal(45.0f, camera.GetFov(), 0.001f);
        Assert.Equal(1.5f, camera.GetAspectRatio(), 0.001f);
        Assert.Equal(1.0f, camera.GetZoom(), 0.001f);
        Assert.Equal(new Vector3f(1, 2, 3), camera.Node!.GetWorldPosition());
    }

    [Fact]
    public void InitializePoint_AlignsCameraToCubeFace()
    {
        using var scene = new Scene();
        var lightNode = scene.CreateChild("Light");
        lightNode.SetWorldPosition(Vector3f.Zero);
        var light = lightNode.CreateComponent<Light>();
        light.SetLightType(LightType.Point);
        light.SetRange(10.0f);

        var processor = new LightProcessor(light);
        var split = new ShadowSplitProcessor(processor, (uint)CubeMapFace.PositiveX);
        split.InitializePoint(CubeMapFace.PositiveX);

        var camera = split.GetShadowCamera();
        Assert.Equal(90.0f, camera.GetFov(), 0.001f);
        Assert.Equal(1.0f, camera.GetAspectRatio(), 0.001f);
        Assert.False(camera.IsOrthographic());
        var direction = camera.Node!.GetWorldDirection();
        Assert.True(direction.ApproximatelyEquivalent(Vector3f.Right, 1e-5f),
            $"Expected +X direction, got {direction}");
    }

    [Fact]
    public void FinalizeShadowBatches_SetsDisableColorOutputAndInstancingFlags()
    {
        using var scene = new Scene();
        var lightNode = scene.CreateChild("Light");
        var light = lightNode.CreateComponent<Light>();
        var processor = new LightProcessor(light);
        var split = new ShadowSplitProcessor(processor, 0);

        split.FinalizeShadowBatches();

        var batches = split.GetShadowBatches();
        Assert.True((batches.Flags & BatchRenderFlag.DisableColorOutput) != 0);
        Assert.True((batches.Flags & BatchRenderFlag.EnableInstancingForStaticGeometry) != 0);
        Assert.Equal(0, batches.Batches.Length);
    }

    [Fact]
    public void DrawableProcessor_StoresGeometryZRange()
    {
        var pipeline = new TestRenderPipelineInterface();
        var drawableProcessor = new DrawableProcessor(pipeline);
        var range = new FloatRange(1.5f, 4.0f);
        drawableProcessor.SetGeometryZRange(7, range);
        Assert.Equal(range, drawableProcessor.GetGeometryZRange(7));
        Assert.Equal(default, drawableProcessor.GetGeometryZRange(99));
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
