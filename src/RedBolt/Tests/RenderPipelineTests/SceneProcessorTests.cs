using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.RenderAPI;
using RedBolt.RenderPipeline;
using RedBolt.Scene;
namespace RedBolt.Tests;
using Scene = global::RedBolt.Scene.Scene;


[Collection(nameof(GraphicsEngineCollection))]
public sealed class SceneProcessorTests
{
    private readonly GraphicsEngineFixture _fixture;

    public SceneProcessorTests(GraphicsEngineFixture fixture)
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
            Viewport = viewport
        }));

        scene.CreateComponent<Octree>();
        Assert.True(processor.Define(new CommonFrameInfo
        {
            Viewport = viewport
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

    [Fact]
    public void CreatePass_ForwardsProcessorDependenciesAndArguments()
    {
        var pipeline = new TestRenderPipelineInterface();
        using var processor = new SceneProcessor(pipeline, "shadow", null, null);
        using var pass = processor.CreatePass<UnorderedScenePass>(DrawableProcessorPassFlag.None, "base");

        Assert.NotNull(pass);
    }

    [Fact]
    public void ScenePassDisposeReleasesDerivedBatchStorage()
    {
        var pipeline = new TestRenderPipelineInterface();
        using var processor = new SceneProcessor(pipeline, "shadow", null, null);
        var unorderedPass = processor.CreatePass<UnorderedScenePass>(DrawableProcessorPassFlag.None, "base");
        var backToFrontPass = processor.CreatePass<BackToFrontScenePass>(DrawableProcessorPassFlag.None, "alpha");
        unorderedPass.ComposeBatches();
        backToFrontPass.ComposeBatches();
        ref readonly var unorderedBatches = ref unorderedPass.GetBaseBatches();
        ref readonly var backToFrontBatches = ref backToFrontPass.GetBatches();

        Assert.NotNull(unorderedBatches.Batches);
        Assert.NotNull(backToFrontBatches.Batches);

        unorderedPass.Dispose();
        backToFrontPass.Dispose();

        Assert.Null(unorderedBatches.Batches);
        Assert.Null(backToFrontBatches.Batches);
        Assert.True(typeof(UnorderedScenePass).GetMethod(nameof(UnorderedScenePass.GetBaseBatches))!.ReturnType.IsByRef);
        Assert.True(typeof(BackToFrontScenePass).GetMethod(nameof(BackToFrontScenePass.GetBatches))!.ReturnType.IsByRef);
    }

    [Fact]
    public void PipelineStateBuilderUsesNativePointerAndPropertyShape()
    {
        var type = typeof(PipelineStateBuilder);
        var constructor = Assert.Single(type.GetConstructors());
        var nullability = new System.Reflection.NullabilityInfoContext();

        Assert.Null(type.GetMethod("GetShaderProgramCompositor"));
        Assert.Equal(typeof(ShaderProgramCompositor),
            type.GetProperty(nameof(PipelineStateBuilder.ShaderProgramCompositor))!.PropertyType);
        foreach (var parameter in constructor.GetParameters())
            Assert.Equal(System.Reflection.NullabilityState.Nullable, nullability.Create(parameter).ReadState);
    }

    [Fact]
    public void PipelineStateBuilderDisposeDestroysReusableState()
    {
        var pipeline = new TestRenderPipelineInterface();
        var processor = new SceneProcessor(pipeline, "unused", null, null);
        var builder = processor.GetPipelineStateBuilder();
        SetPrivateField(builder, "_pipelineStateDesc", new GraphicsPipelineStateDesc { DebugName = "retained" });
        var shaderProgramDesc = GetPrivateField<ShaderProgramDesc>(builder, "_shaderProgramDesc");
        shaderProgramDesc.AddCommonShaderDefines("RETAINED");
        SetPrivateField(builder, "_shaderProgramDesc", shaderProgramDesc);

        processor.Dispose();

        Assert.Equal(string.Empty,
            GetPrivateField<GraphicsPipelineStateDesc>(builder, "_pipelineStateDesc").DebugName);
        Assert.Equal(string.Empty,
            GetPrivateField<ShaderProgramDesc>(builder, "_shaderProgramDesc").CommonShaderDefines);
    }

    [Fact]
    public void BatchCompositor_dispose_releases_pipeline_subscriptions()
    {
        var pipeline = new TestRenderPipelineInterface();
        using var drawableProcessor = new DrawableProcessor(pipeline);
        var updateSubscribers = pipeline.UpdateBeginSubscriberCount;
        var invalidationSubscribers = pipeline.PipelineStatesInvalidatedSubscriberCount;
        var compositor = new BatchCompositor(pipeline, drawableProcessor, null, 0);

        Assert.Equal(updateSubscribers + 1, pipeline.UpdateBeginSubscriberCount);
        Assert.Equal(invalidationSubscribers + 1, pipeline.PipelineStatesInvalidatedSubscriberCount);

        compositor.Dispose();

        Assert.Equal(updateSubscribers, pipeline.UpdateBeginSubscriberCount);
        Assert.Equal(invalidationSubscribers, pipeline.PipelineStatesInvalidatedSubscriberCount);
    }

    [Fact]
    public void Dispose_releases_owned_processors_and_pipeline_subscriptions()
    {
        var pipeline = new TestRenderPipelineInterface();
        var processor = new SceneProcessor(pipeline, "unused", null, null);

        Assert.Equal(2, pipeline.UpdateBeginSubscriberCount);
        Assert.Equal(1, pipeline.RenderBeginSubscriberCount);
        Assert.Equal(1, pipeline.RenderEndSubscriberCount);
        Assert.Equal(1, pipeline.PipelineStatesInvalidatedSubscriberCount);
        Assert.Equal(1, pipeline.CollectStatisticsSubscriberCount);

        processor.Dispose();

        Assert.Equal(0, pipeline.UpdateBeginSubscriberCount);
        Assert.Equal(0, pipeline.RenderBeginSubscriberCount);
        Assert.Equal(0, pipeline.RenderEndSubscriberCount);
        Assert.Equal(0, pipeline.PipelineStatesInvalidatedSubscriberCount);
        Assert.Equal(0, pipeline.CollectStatisticsSubscriberCount);
    }

    private static T GetPrivateField<T>(object instance, string fieldName)
    {
        var field = instance.GetType().GetField(fieldName,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsType<T>(field.GetValue(instance));
    }

    private static void SetPrivateField<T>(object instance, string fieldName, T value)
    {
        var field = instance.GetType().GetField(fieldName,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(field);
        field.SetValue(instance, value);
    }

    private sealed class TestRenderPipelineInterface : IRenderPipelineInterface
    {
        public Context? GetContext() => Context.Instance;
        public RenderPipelineDebugger GetDebugger() => new();
        public bool IsLinearColorSpace => false;
        public event Action<CommonFrameInfo>? UpdateBegin;
        public event Action<CommonFrameInfo>? UpdateEnd { add { } remove { } }
        public event Action<CommonFrameInfo>? RenderBegin;
        public event Action<CommonFrameInfo>? RenderEnd;
        public event Action? PipelineStatesInvalidated;
        public event RenderPipelineStatsHandler? CollectStatistics;

        public int UpdateBeginSubscriberCount => UpdateBegin?.GetInvocationList().Length ?? 0;
        public int RenderBeginSubscriberCount => RenderBegin?.GetInvocationList().Length ?? 0;
        public int RenderEndSubscriberCount => RenderEnd?.GetInvocationList().Length ?? 0;
        public int PipelineStatesInvalidatedSubscriberCount => PipelineStatesInvalidated?.GetInvocationList().Length ?? 0;
        public int CollectStatisticsSubscriberCount => CollectStatistics?.GetInvocationList().Length ?? 0;
    }
}
