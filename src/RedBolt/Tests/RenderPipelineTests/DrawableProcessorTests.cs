using RedBolt.Container;
using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.RenderPipeline;
using RedBolt.Scene;
using SceneType = global::RedBolt.Scene.Scene;

namespace RedBolt.Tests;

[Collection(nameof(HeadlessEngineCollection))]
public sealed partial class DrawableProcessorTests
{
    [Fact]
    public void RenderPipelineStatsHasValueSemantics()
    {
        var stats = new RenderPipelineStats { NumLights = 1 };
        var copy = stats;

        copy.NumLights = 2;

        Assert.True(typeof(RenderPipelineStats).IsValueType);
        Assert.Equal(1, stats.NumLights);
        Assert.Equal(2, copy.NumLights);
    }

    [Fact]
    public void GeometryBatchUsesIntSourceBatchIndex()
    {
        Assert.Equal(typeof(int), typeof(GeometryBatch).GetField(nameof(GeometryBatch.SourceBatchIndex))!.FieldType);
    }

    [Fact]
    public void CollectStatisticsMutatesStatsByReference()
    {
        var pipeline = new TestRenderPipelineInterface();
        using var processor = new DrawableProcessor(pipeline);
        using var scene = new SceneType();
        var frameInfo = new FrameInfo
        {
            Scene = scene,
            Octree = scene.CreateComponent<Octree>(),
            ReflectionProbeManager = scene.CreateComponent<ReflectionProbeManager>(),
            Camera = scene.CreateChild("Camera").CreateComponent<Camera>()
        };
        var firstLight = scene.CreateChild("FirstLight").CreateComponent<Light>();
        var secondLight = scene.CreateChild("SecondLight").CreateComponent<Light>();
        var geometry = scene.CreateChild("Geometry").CreateComponent<CustomGeometry>();
        var stats = new RenderPipelineStats
        {
            NumLights = 3,
            NumGeometries = 4
        };

        processor.OnUpdateBegin(frameInfo);
        processor.ProcessVisibleDrawables([firstLight, secondLight, geometry], null);
        pipeline.RaiseCollectStatistics(ref stats);

        Assert.Equal(5, stats.NumLights);
        Assert.Equal(5, stats.NumGeometries);
    }

    [Fact]
    public void DeferredBatchContainsOnlyDeferredPass()
    {
        var drawable = new CustomGeometry();
        var pass = new Pass("deferred");
        var userData = new object();

        var batch = GeometryBatch.Deferred(drawable, 3, pass, userData);

        Assert.Same(drawable, batch.Drawable);
        Assert.Equal(3, batch.SourceBatchIndex);
        Assert.Same(userData, batch.UserData);
        Assert.Same(pass, batch.DeferredPass);
        Assert.Null(batch.UnlitBasePass);
        Assert.Null(batch.LitBasePass);
        Assert.Null(batch.LightPass);
    }

    [Fact]
    public void ForwardBatchContainsResolvedForwardPasses()
    {
        var drawable = new CustomGeometry();
        var unlitBasePass = new Pass("base");
        var litBasePass = new Pass("litbase");
        var lightPass = new Pass("light");

        var batch = GeometryBatch.Forward(drawable, 5, unlitBasePass, litBasePass, lightPass);

        Assert.Same(drawable, batch.Drawable);
        Assert.Equal(5, batch.SourceBatchIndex);
        Assert.Null(batch.DeferredPass);
        Assert.Same(unlitBasePass, batch.UnlitBasePass);
        Assert.Same(litBasePass, batch.LitBasePass);
        Assert.Same(lightPass, batch.LightPass);
    }

    [Fact]
    public void SortedOccludersCompareBySortValue()
    {
        var near = new SortedOccluder { SortValue = 1.0f };
        var far = new SortedOccluder { SortValue = 2.0f };

        Assert.True(near.CompareTo(far) < 0);
        Assert.True(far.CompareTo(near) > 0);
        Assert.Equal(0, near.CompareTo(near));
    }

    [Fact]
    public void ProcessOccludersUsesNativeScreenSizeAndTrianglePenalty()
    {
        var pipeline = new TestRenderPipelineInterface();
        using var processor = new DrawableProcessor(pipeline);
        using var scene = new SceneType();
        var frameInfo = new FrameInfo
        {
            Scene = scene,
            Octree = scene.CreateComponent<Octree>(),
            ReflectionProbeManager = scene.CreateComponent<ReflectionProbeManager>(),
            Camera = scene.CreateChild("Camera").CreateComponent<Camera>()
        };
        var expensive = scene.CreateChild("Expensive").CreateComponent<TestOccluderDrawable>();
        expensive.WorldBounds = new BoundingBox(new Vector3f(-1.0f, -1.0f, 9.0f), new Vector3f(1.0f, 1.0f, 11.0f));
        expensive.NumOccluderTriangles = 20;
        var cheap = scene.CreateChild("Cheap").CreateComponent<TestOccluderDrawable>();
        cheap.WorldBounds = expensive.WorldBounds;
        cheap.NumOccluderTriangles = 10;

        processor.OnUpdateBegin(frameInfo);
        processor.ProcessOccluders([expensive, cheap], 0.0f);

        Assert.Equal(1, expensive.UpdateBatchesCount);
        Assert.Equal(1, cheap.UpdateBatchesCount);
        Assert.Equal([cheap, expensive], processor.Occluders.Select(occluder => occluder.Drawable));

        processor.OnUpdateBegin(frameInfo);
        processor.ProcessOccluders([cheap], float.MaxValue);

        Assert.Empty(processor.Occluders);
    }

    [Fact]
    public void ProcessorPassShapeUsesNativeProperties()
    {
        Assert.Null(typeof(DrawableProcessorPass).GetMethod("GetFlags"));
        Assert.Null(typeof(DrawableProcessorPass).GetMethod("HasLightPass"));
        Assert.NotNull(typeof(DrawableProcessorPass).GetProperty(nameof(DrawableProcessorPass.Flags)));
        Assert.NotNull(typeof(DrawableProcessorPass).GetProperty(nameof(DrawableProcessorPass.HasLightPass)));
        Assert.NotNull(typeof(DrawableProcessorPass).GetProperty(nameof(DrawableProcessorPass.IsEnabled)));
    }

    [Fact]
    public void ProcessorShapeUsesNativeProperties()
    {
        var type = typeof(DrawableProcessor);
        Assert.Null(type.GetMethod("GetFrameInfo"));
        Assert.Null(type.GetMethod("GetSettings"));
        Assert.Null(type.GetMethod("HasOccluders"));
        Assert.Null(type.GetMethod("GetOccluders"));
        Assert.Null(type.GetMethod("GetGeometries"));
        Assert.Null(type.GetMethod("GetSceneZRange"));
        Assert.Null(type.GetMethod("GetLights"));
        Assert.Null(type.GetMethod("GetLightProcessors"));
        Assert.Null(type.GetMethod("GetLightProcessorsByShadowMap"));
        Assert.Null(type.GetMethod("GetNumShadowedLights"));
        Assert.Null(type.GetMethod("SetGeometryRenderFlags"));
        Assert.Null(type.GetMethod("SetGeometryZRange"));
        Assert.Null(type.GetMethod("SetSceneZRange"));
        Assert.Null(type.GetMethod("SetLights"));
        Assert.Null(type.GetMethod("SetGeometries"));
        Assert.NotNull(type.GetProperty(nameof(DrawableProcessor.FrameInfo)));
        Assert.NotNull(type.GetProperty(nameof(DrawableProcessor.Settings)));
        Assert.NotNull(type.GetProperty(nameof(DrawableProcessor.HasOccluders)));
        Assert.NotNull(type.GetProperty(nameof(DrawableProcessor.Occluders)));
        Assert.NotNull(type.GetProperty(nameof(DrawableProcessor.Geometries)));
        Assert.NotNull(type.GetProperty(nameof(DrawableProcessor.SceneZRange)));
        Assert.NotNull(type.GetProperty(nameof(DrawableProcessor.Lights)));
        Assert.NotNull(type.GetProperty(nameof(DrawableProcessor.LightProcessors)));
        Assert.NotNull(type.GetProperty(nameof(DrawableProcessor.LightProcessorsByShadowMap)));
        Assert.Equal(typeof(int), type.GetMethod(nameof(DrawableProcessor.GetLight))!.GetParameters()[0].ParameterType);
        Assert.Equal(typeof(int), type.GetMethod(nameof(DrawableProcessor.GetLightProcessor))!.GetParameters()[0].ParameterType);
        Assert.Equal(typeof(int), type.GetMethod(nameof(DrawableProcessor.GetGeometryLighting))!.GetParameters()[0].ParameterType);
        Assert.Equal(typeof(int), type.GetMethod(nameof(DrawableProcessor.GetGeometryRenderFlags))!.GetParameters()[0].ParameterType);
        Assert.Equal(typeof(int), type.GetMethod(nameof(DrawableProcessor.GetGeometryZRange))!.GetParameters()[0].ParameterType);
        Assert.Equal(typeof(int), type.GetMethod(nameof(DrawableProcessor.ProcessForwardLightingForLight))!.GetParameters()[0].ParameterType);
        Assert.True(type.GetMethod(nameof(DrawableProcessor.ProcessForwardLightingForLight))!.IsPublic);
        Assert.True(type.GetMethod(nameof(DrawableProcessor.FinalizeForwardLighting))!.IsPublic);
        Assert.True(GetNonPublicMethod(type, "ProcessVisibleDrawable").IsFamily);
        Assert.True(GetNonPublicMethod(type, "ProcessQueuedDrawable").IsFamily);
        Assert.True(GetNonPublicMethod(type, "UpdateDrawableZone").IsFamily);
        Assert.True(GetNonPublicMethod(type, "UpdateDrawableReflection").IsFamily);
        Assert.True(GetNonPublicMethod(type, "QueueDrawableUpdate").IsFamily);
        Assert.True(GetNonPublicMethod(type, "QueueDrawableGeometryUpdate").IsFamily);
        Assert.True(GetNonPublicMethod(type, "CheckMaterialForAuxiliaryRenderSurfaces").IsFamily);
        Assert.True(GetNonPublicMethod(type, "CalculateBoundingBoxZRange").IsFamily);
        Assert.True(GetNonPublicMethod(type, "SortLightProcessorsByShadowMapSize").IsFamily);
        Assert.True(GetNonPublicMethod(type, "SortLightProcessorsByShadowMapTexture").IsFamily);
    }

    [Fact]
    public void ForEachParallelProcessesMultiVectorInFlattenedOrder()
    {
        var pipeline = new TestRenderPipelineInterface();
        using var processor = new DrawableProcessor(pipeline);
        var workQueue = GetPrivateField<WorkQueue>(processor, "_workQueue");
        using var values = new MultiVector<int>();
        values.Resize(3);
        values.PushBack(0, 10);
        values.PushBack(0, 11);
        values.PushBack(2, 20);
        var result = new int[3];

        WorkQueue.ForEachParallel(workQueue, values, (index, value) => result[index] = value);

        Assert.Equal([10, 11, 20], result);
    }

    [Fact]
    public void ProcessorDisposeReleasesOwnedAndAliasedPassContainers()
    {
        var pipeline = new TestRenderPipelineInterface();
        using var processorPass = new TestDrawableProcessorPass(
            pipeline, int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue);
        var processor = new DrawableProcessor(pipeline);
        processor.SetPasses([processorPass]);

        processor.Dispose();

        Assert.Empty(GetPasses(processor, "_allPasses"));
        Assert.Empty(GetPasses(processor, "_passes"));
    }

    [Fact]
    public void ShadowMapTextureSortSupportsDistinctTextureIdentities()
    {
        var pipeline = new TestRenderPipelineInterface();
        using var processor = new TestDrawableProcessor(pipeline);
        using var firstLight = new Light();
        using var secondLight = new Light();
        using var firstProcessor = new LightProcessor(firstLight);
        using var secondProcessor = new LightProcessor(secondLight);
        using var firstTexture = new Texture2D { Name = "First" };
        using var secondTexture = new Texture2D { Name = "Second" };
        SetPrivateField(firstProcessor, "_shadowMap", new ShadowMapRegion { Texture = firstTexture });
        SetPrivateField(secondProcessor, "_shadowMap", new ShadowMapRegion { Texture = secondTexture });
        var lightProcessors = GetPrivateField<List<LightProcessor?>>(processor, "_lightProcessors");
        lightProcessors.Add(firstProcessor);
        lightProcessors.Add(secondProcessor);

        processor.SortByShadowMapTexture();

        Assert.Equal(2, processor.LightProcessorsByShadowMap.Count);
        Assert.Contains(firstProcessor, processor.LightProcessorsByShadowMap);
        Assert.Contains(secondProcessor, processor.LightProcessorsByShadowMap);
    }

    [Fact]
    public void UpdateBeginRebuildsEnabledPassAliasesFromOwnedPasses()
    {
        var pipeline = new TestRenderPipelineInterface();
        using var firstPass = new TestDrawableProcessorPass(
            pipeline, int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue);
        using var secondPass = new TestDrawableProcessorPass(
            pipeline, int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue);
        using var processor = new DrawableProcessor(pipeline);
        using var scene = new SceneType();
        var frameInfo = new FrameInfo
        {
            Scene = scene,
            Octree = scene.CreateComponent<Octree>(),
            Camera = scene.CreateChild("Camera").CreateComponent<Camera>()
        };
        firstPass.IsEnabled = false;
        processor.SetPasses([firstPass, secondPass]);

        processor.OnUpdateBegin(frameInfo);

        Assert.Equal([firstPass, secondPass], GetPasses(processor, "_allPasses"));
        Assert.Equal([secondPass], GetPasses(processor, "_passes"));

        firstPass.IsEnabled = true;
        secondPass.IsEnabled = false;
        processor.OnUpdateBegin(frameInfo);

        Assert.Equal([firstPass], GetPasses(processor, "_passes"));
    }

    [Fact]
    public void UpdateBeginInitializesNativeFrameState()
    {
        var pipeline = new TestRenderPipelineInterface();
        using var processor = new DrawableProcessor(pipeline);
        using var scene = new SceneType();
        var camera = scene.CreateChild("Camera").CreateComponent<Camera>();
        camera.SetViewOverrideFlags(ViewOverride.LowMaterialQuality);
        var frameInfo = new FrameInfo
        {
            TimeStep = 0.25f,
            Scene = scene,
            Octree = scene.CreateComponent<Octree>(),
            ReflectionProbeManager = scene.CreateComponent<ReflectionProbeManager>(),
            Camera = camera
        };
        var light = scene.CreateChild("Light").CreateComponent<Light>();
        processor.SetSettings(new DrawableProcessorSettings { MaterialQuality = MaterialQuality.High });

        processor.OnUpdateBegin(frameInfo);
        processor.ProcessVisibleDrawables([light], null);
        processor.OnUpdateBegin(frameInfo);

        var view = camera.GetView();
        Assert.Equal(MaterialQuality.Low, GetPrivateField<MaterialQuality>(processor, "_materialQuality"));
        Assert.Equal(
            new Vector3f(view.M20, view.M21, view.M22),
            GetPrivateField<Vector3f>(processor, "_cullCameraZAxis"));
        Assert.Equal(WorkQueue.GetThreadIndexCount(),
            GetPrivateField<FloatRange[]>(processor, "_sceneZRangeTemp").Length);
        Assert.Same(light, Assert.Single(processor.Lights));
    }

    [Fact]
    public void ProcessorPassAddsDeferredBatchBeforeForwardPasses()
    {
        var pipeline = new TestRenderPipelineInterface();
        using var processorPass = new TestDrawableProcessorPass(pipeline,
            Technique.GetPassIndex("deferred"), Technique.GetPassIndex("base"),
            Technique.GetPassIndex("litbase"), Technique.GetPassIndex("light"));
        pipeline.RaiseUpdateBegin();
        var technique = new Technique { Name = "test" };
        technique.CreatePass("deferred");
        technique.CreatePass("base");

        var result = processorPass.AddBatch(0, new CustomGeometry(), 0, technique);

        Assert.True(result.Added);
        Assert.False(result.ForwardLitAdded);
        Assert.Equal(1, processorPass.BatchCount);
        Assert.NotNull(processorPass.FirstBatch.DeferredPass);
        Assert.Null(processorPass.FirstBatch.UnlitBasePass);
    }

    [Fact]
    public void ProcessorPassAddsForwardBatchAndTracksLightPass()
    {
        var pipeline = new TestRenderPipelineInterface();
        using var processorPass = new TestDrawableProcessorPass(pipeline,
            Technique.GetPassIndex("deferred"), Technique.GetPassIndex("base"),
            Technique.GetPassIndex("litbase"), Technique.GetPassIndex("light"));
        pipeline.RaiseUpdateBegin();
        var technique = new Technique { Name = "test" };
        technique.CreatePass("base");
        technique.CreatePass("litbase");
        technique.CreatePass("light");

        var result = processorPass.AddBatch(0, new CustomGeometry(), 0, technique);

        Assert.True(result.Added);
        Assert.True(result.ForwardLitAdded);
        Assert.Equal(1, processorPass.BatchCount);
        Assert.NotNull(processorPass.FirstBatch.UnlitBasePass);
        Assert.NotNull(processorPass.FirstBatch.LitBasePass);
        Assert.NotNull(processorPass.FirstBatch.LightPass);
    }

    [Fact]
    public void UpdateBeginClearsBatchesAndCopiesColorSpace()
    {
        var pipeline = new TestRenderPipelineInterface();
        using var processorPass = new TestDrawableProcessorPass(pipeline,
            int.MaxValue, Technique.GetPassIndex("base"), int.MaxValue, int.MaxValue);
        pipeline.RaiseUpdateBegin();
        var technique = new Technique { Name = "test" };
        technique.CreatePass("base");
        processorPass.AddBatch(0, new CustomGeometry(), 0, technique);

        pipeline.IsLinearColorSpace = true;
        pipeline.RaiseUpdateBegin();

        Assert.Equal(0, processorPass.BatchCount);
        Assert.True(processorPass.IsLinearColorSpace);
    }

    [Fact]
    public void ProcessorPassDisposeDestroysOwnedGeometryBatches()
    {
        var pipeline = new TestRenderPipelineInterface();
        var processorPass = new TestDrawableProcessorPass(pipeline,
            int.MaxValue, Technique.GetPassIndex("base"), int.MaxValue, int.MaxValue);
        pipeline.RaiseUpdateBegin();
        var technique = new Technique { Name = "test" };
        technique.CreatePass("base");
        processorPass.AddBatch(0, new CustomGeometry(), 0, technique);

        processorPass.Dispose();

        Assert.Equal(0, processorPass.BatchCount);
    }

    [Fact]
    public void PipelineBatchDescCapturesDrawableStateAndCreatesBatchKey()
    {
        var drawable = new CustomGeometry();
        drawable.SetNumGeometries(1);
        drawable.BeginGeometry(0, PrimitiveType.TriangleList);
        drawable.DefineVertex(Vector3f.Zero);
        drawable.DefineVertex(Vector3f.Right);
        drawable.DefineVertex(Vector3f.Up);
        drawable.Commit();
        var material = new Material { Name = "test" };
        drawable.SetMaterial(material);
        var pass = new Pass("base");

        var batch = new PipelineBatchDesc(drawable, 0, pass);
        var key = batch.GetKey();

        Assert.Same(drawable, key.Drawable);
        Assert.Same(batch.Geometry, key.Geometry);
        Assert.Same(material, key.Material);
        Assert.Same(pass, key.Pass);
        Assert.Equal(drawable.GetPipelineStateHash(), key.DrawableHash);
        Assert.Equal(batch.GeometryType, key.GeometryType);
    }

    [Fact]
    public void PipelineBatchDescInitializesShadowAndLitLightState()
    {
        var batch = new PipelineBatchDesc();
        using var lightProcessor = new LightProcessor(new Light());

        batch.InitializeShadowBatch(lightProcessor, 3, 17);

        Assert.Equal(int.MaxValue, batch.PixelLightIndex);
        Assert.Same(lightProcessor, batch.PixelLightForPipelineState);
        Assert.Equal(3, batch.PixelLightForPipelineStateIndex);
        Assert.Equal(17u, batch.PixelLightForPipelineStateHash);

        batch.InitializeLitBatch(lightProcessor, 5, 23);

        Assert.Equal(5, batch.PixelLightIndex);
        Assert.Same(lightProcessor, batch.PixelLightForPipelineState);
        Assert.Equal(5, batch.PixelLightForPipelineStateIndex);
        Assert.Equal(23u, batch.PixelLightForPipelineStateHash);
    }

    private sealed class TestDrawableProcessorPass : DrawableProcessorPass
    {
        public TestDrawableProcessorPass(IRenderPipelineInterface renderPipeline,
            int deferredPassIndex, int unlitBasePassIndex, int litBasePassIndex, int lightPassIndex)
            : base(renderPipeline, DrawableProcessorPassFlag.None,
                deferredPassIndex, unlitBasePassIndex, litBasePassIndex, lightPassIndex)
        {
        }

        public int BatchCount => GeometryBatches.Size();
        public GeometryBatch FirstBatch => GeometryBatches.First();
        public bool IsLinearColorSpace => LinearColorSpace;
    }

    private sealed class TestDrawableProcessor : DrawableProcessor
    {
        public TestDrawableProcessor(IRenderPipelineInterface renderPipeline)
            : base(renderPipeline)
        {
        }

        public void SortByShadowMapTexture() => SortLightProcessorsByShadowMapTexture();
    }

    [Reflected(InheritAttributes = false)]
    private sealed partial class TestOccluderDrawable : Drawable
    {
        public BoundingBox WorldBounds { get; set; } = new();
        public uint NumOccluderTriangles { get; set; }
        public int UpdateBatchesCount { get; private set; }

        public override void UpdateBatches(FrameInfo frame)
        {
            ++UpdateBatchesCount;
            base.UpdateBatches(frame);
        }

        public override uint GetNumOccluderTriangles() => NumOccluderTriangles;

        protected override void OnWorldBoundingBoxUpdate() => _worldBoundingBox = WorldBounds;
    }

    private static List<DrawableProcessorPass> GetPasses(DrawableProcessor processor, string fieldName)
        => GetPrivateField<List<DrawableProcessorPass>>(processor, fieldName);

    private static T GetPrivateField<T>(DrawableProcessor processor, string fieldName)
    {
        var field = typeof(DrawableProcessor).GetField(
            fieldName,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsType<T>(field.GetValue(processor));
    }

    private static System.Reflection.MethodInfo GetNonPublicMethod(Type type, string methodName)
    {
        var method = type.GetMethod(methodName,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(method);
        return method;
    }

    private static void SetPrivateField<T>(object instance, string fieldName, T value)
    {
        var field = instance.GetType().GetField(fieldName,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? instance.GetType().BaseType?.GetField(fieldName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(field);
        field.SetValue(instance, value);
    }

    private sealed class TestRenderPipelineInterface : IRenderPipelineInterface
    {
        public Context? GetContext() => Context.Instance;
        public RenderPipelineDebugger GetDebugger() => null!;
        public bool IsLinearColorSpace { get; set; }
        public event Action<CommonFrameInfo>? UpdateBegin;
        public event Action<CommonFrameInfo>? UpdateEnd { add { } remove { } }
        public event Action<CommonFrameInfo>? RenderBegin { add { } remove { } }
        public event Action<CommonFrameInfo>? RenderEnd { add { } remove { } }
        public event Action? PipelineStatesInvalidated { add { } remove { } }
        public event RenderPipelineStatsHandler? CollectStatistics;

        public void RaiseUpdateBegin() => UpdateBegin?.Invoke(default);
        public void RaiseCollectStatistics(ref RenderPipelineStats stats) => CollectStatistics?.Invoke(ref stats);
    }
}
