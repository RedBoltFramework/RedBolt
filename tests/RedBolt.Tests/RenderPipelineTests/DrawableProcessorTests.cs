namespace RedBolt.Tests;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class DrawableProcessorTests
{
    [Fact]
    public void DeferredBatchContainsOnlyDeferredPass()
    {
        var drawable = new CustomGeometry();
        var pass = new Pass("deferred");
        var userData = new object();

        var batch = GeometryBatch.Deferred(drawable, 3, pass, userData);

        Assert.Same(drawable, batch.Drawable);
        Assert.Equal(3u, batch.SourceBatchIndex);
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
        Assert.Equal(5u, batch.SourceBatchIndex);
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
    public void ProcessorPassAddsDeferredBatchBeforeForwardPasses()
    {
        var pipeline = new TestRenderPipelineInterface();
        using var processorPass = new TestDrawableProcessorPass(pipeline,
            Technique.GetPassIndex("deferred"), Technique.GetPassIndex("base"),
            Technique.GetPassIndex("litbase"), Technique.GetPassIndex("light"));
        pipeline.RaiseUpdateBegin();
        var technique = new Technique("test");
        technique.CreatePass("deferred");
        technique.CreatePass("base");

        var result = processorPass.AddBatch(0, new CustomGeometry(), 0, technique);

        Assert.True(result.Added);
        Assert.False(result.ForwardLitAdded);
        Assert.Equal(1u, processorPass.BatchCount);
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
        var technique = new Technique("test");
        technique.CreatePass("base");
        technique.CreatePass("litbase");
        technique.CreatePass("light");

        var result = processorPass.AddBatch(0, new CustomGeometry(), 0, technique);

        Assert.True(result.Added);
        Assert.True(result.ForwardLitAdded);
        Assert.Equal(1u, processorPass.BatchCount);
        Assert.NotNull(processorPass.FirstBatch.UnlitBasePass);
        Assert.NotNull(processorPass.FirstBatch.LitBasePass);
        Assert.NotNull(processorPass.FirstBatch.LightPass);
    }

    [Fact]
    public void UpdateBeginClearsBatchesAndCopiesColorSpace()
    {
        var pipeline = new TestRenderPipelineInterface();
        using var processorPass = new TestDrawableProcessorPass(pipeline,
            uint.MaxValue, Technique.GetPassIndex("base"), uint.MaxValue, uint.MaxValue);
        pipeline.RaiseUpdateBegin();
        var technique = new Technique("test");
        technique.CreatePass("base");
        processorPass.AddBatch(0, new CustomGeometry(), 0, technique);

        pipeline.IsLinearColorSpace = true;
        pipeline.RaiseUpdateBegin();

        Assert.Equal(0u, processorPass.BatchCount);
        Assert.True(processorPass.IsLinearColorSpace);
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
        var material = new Material("test");
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
            uint deferredPassIndex, uint unlitBasePassIndex, uint litBasePassIndex, uint lightPassIndex)
            : base(renderPipeline, DrawableProcessorPassFlag.None,
                deferredPassIndex, unlitBasePassIndex, litBasePassIndex, lightPassIndex)
        {
        }

        public uint BatchCount => GeometryBatches.Size();
        public GeometryBatch FirstBatch => GeometryBatches.First();
        public bool IsLinearColorSpace => LinearColorSpace;
    }

    private sealed class TestRenderPipelineInterface : IRenderPipelineInterface
    {
        public Context? GetContext() => Context.Instance;
        public RenderPipelineDebugger GetDebugger() => null!;
        public bool IsLinearColorSpace { get; set; }
        public event Action<CommonFrameInfo>? UpdateBegin;
        public event Action<CommonFrameInfo>? UpdateEnd;
        public event Action<CommonFrameInfo>? RenderBegin;
        public event Action<CommonFrameInfo>? RenderEnd;
        public event Action? PipelineStatesInvalidated;
        public event Action<RenderPipelineStats>? CollectStatistics;

        public void RaiseUpdateBegin() => UpdateBegin?.Invoke(default);
    }
}
