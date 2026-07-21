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
        using var processor = new LightProcessor(light);

        ref readonly var parameters = ref processor.Params;
        Assert.Equal(Matrix4.Identity, parameters.LightShapeMatrix);
        for (var i = 0; i < Light.MaxCascadeSplits; ++i)
            Assert.Equal(Matrix4.Identity, parameters.LightMatrices[i]);
    }

    [Fact]
    public void CookedLightParams_CopyHasIndependentInlineStorage()
    {
        var original = new CookedLightParams();
        original.LightMatrices[0] = Matrix4.Identity;
        original.ShadowNormalBias[0] = 2.0f;
        original.ShadowDepthBiasMultiplier[0] = 3.0f;

        var copy = original;
        copy.LightMatrices[0] = Matrix4.Zero;
        copy.ShadowNormalBias[0] = 5.0f;
        copy.ShadowDepthBiasMultiplier[0] = 7.0f;

        Assert.Equal(Matrix4.Identity, original.LightMatrices[0]);
        Assert.Equal(2.0f, original.ShadowNormalBias[0]);
        Assert.Equal(3.0f, original.ShadowDepthBiasMultiplier[0]);
    }

    [Fact]
    public void LightProcessorCache_DoesNotKeepLightAlive()
    {
        using var cache = new LightProcessorCache();
        var weakLight = AddTransientLight(cache);

        for (var i = 0; i < 3 && IsAlive(weakLight); ++i)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        Assert.False(IsAlive(weakLight));
        cache.Update(1.0f);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static WeakReference<Light> AddTransientLight(LightProcessorCache cache)
    {
        var light = new Light();
        cache.GetLightProcessor(light);
        return new WeakReference<Light>(light);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static bool IsAlive(WeakReference<Light> light) => light.TryGetTarget(out _);

    [Fact]
    public void InitializeSpot_ConfiguresShadowCameraFromLight()
    {
        using var scene = new Scene();
        var lightNode = scene.CreateChild("Light");
        lightNode.SetWorldPosition(new Vector3f(1, 2, 3));
        lightNode.SetWorldRotation(Quaternion.IDENTITY);
        var light = lightNode.CreateComponent<Light>();
        light.SetLightType(LightType.Spot);
        light.SetRange(25.0f);
        light.SetFov(45.0f);
        light.SetAspectRatio(1.5f);
        light.SetShadowNearFarRatio(0.01f);

        using var processor = new LightProcessor(light);
        using var split = new ShadowSplitProcessor(processor, 0);

        split.InitializeSpot();

        var camera = split.ShadowCamera;
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

        using var processor = new LightProcessor(light);
        using var split = new ShadowSplitProcessor(processor, (int)CubeMapFace.PositiveX);
        split.InitializePoint(CubeMapFace.PositiveX);

        var camera = split.ShadowCamera;
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
        using var processor = new LightProcessor(light);
        using var split = new ShadowSplitProcessor(processor, 0);

        split.FinalizeShadowBatches();

        ref readonly var batches = ref split.ShadowBatches;
        Assert.True((batches.Flags & BatchRenderFlag.DisableColorOutput) != 0);
        Assert.True((batches.Flags & BatchRenderFlag.EnableInstancingForStaticGeometry) != 0);
        Assert.Empty(batches.Batches!);
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
