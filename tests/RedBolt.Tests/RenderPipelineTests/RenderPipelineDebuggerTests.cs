using RedBolt;

namespace RedBolt.Tests;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class RenderPipelineDebuggerTests
{
    private readonly HeadlessEngineFixture _fixture;

    public RenderPipelineDebuggerTests(HeadlessEngineFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void Snapshot_structs_clone_owned_containers()
    {
        Assert.True(typeof(DebugFrameSnapshotBatch).IsValueType);
        Assert.True(typeof(DebugFrameSnapshotQuad).IsValueType);
        Assert.True(typeof(DebugFrameSnapshotPass).IsValueType);
        Assert.True(typeof(DebugFrameSnapshot).IsValueType);

        var geometry = new Geometry();
        var pass = new DebugFrameSnapshotPass { Name = "Forward" };
        pass.Batches.Add(new DebugFrameSnapshotBatch { Geometry = geometry });
        pass.Quads.Add(new DebugFrameSnapshotQuad { DebugComment = "First" });

        var snapshot = new DebugFrameSnapshot();
        snapshot.Passes.Add(pass);
        var copy = (DebugFrameSnapshot)snapshot.Clone();

        snapshot.Passes[0].Quads.Add(new DebugFrameSnapshotQuad { DebugComment = "Second" });

        Assert.NotSame(snapshot.Passes, copy.Passes);
        Assert.NotSame(snapshot.Passes[0].Batches, copy.Passes[0].Batches);
        Assert.NotSame(snapshot.Passes[0].Quads, copy.Passes[0].Quads);
        Assert.Single(copy.Passes[0].Quads);
        Assert.Same(geometry, copy.Passes[0].Batches[0].Geometry);
    }

    [Fact]
    public void Snapshot_state_machine_preserves_pass_and_item_order()
    {
        var debugger = new RenderPipelineDebugger();

        Assert.False(debugger.IsSnapshotInProgress());
        Assert.False(RenderPipelineDebugger.IsSnapshotInProgress(null));

        debugger.BeginSnapshot();
        debugger.ReportQuad("First", new Vector2i(320, 200));
        debugger.BeginPass("Second");
        debugger.ReportQuad("Second A");
        debugger.ReportQuad("Second B", new Vector2i(64, 32));
        debugger.EndPass();
        debugger.EndSnapshot();

        var snapshot = debugger.GetSnapshot();
        Assert.False(debugger.IsSnapshotInProgress());
        Assert.Equal(2, snapshot.Passes.Count);
        Assert.Equal("Unnamed", snapshot.Passes[0].Name);
        Assert.Equal("First", snapshot.Passes[0].Quads[0].DebugComment);
        Assert.Equal("Second", snapshot.Passes[1].Name);
        Assert.Equal(["Second A", "Second B"], snapshot.Passes[1].Quads.Select(quad => quad.DebugComment));
        Assert.Equal("+ [quad 320x200] First", snapshot.Passes[0].Quads[0].ToString());
        Assert.Equal("Pass Unnamed - 1b 4v 2t:\n\n+ [quad 320x200] First\n\n", snapshot.Passes[0].ToString());
    }

    [Fact]
    public void Begin_snapshot_replaces_previous_snapshot()
    {
        var debugger = new RenderPipelineDebugger();
        debugger.BeginSnapshot();
        debugger.ReportQuad("Old");
        debugger.EndSnapshot();

        debugger.BeginSnapshot();
        debugger.BeginPass("New");
        debugger.EndPass();
        debugger.EndSnapshot();

        var snapshot = debugger.GetSnapshot();
        Assert.Single(snapshot.Passes);
        Assert.Equal("New", snapshot.Passes[0].Name);
        Assert.Empty(snapshot.Passes[0].Quads);
        Assert.Contains("Pipeline states in scene (0)", snapshot.ToString());
        Assert.Contains("Materials in scene (0)", snapshot.ToString());
        Assert.Contains("Shaders in scene (0)", snapshot.ToString());
    }

    [Fact]
    public void Each_render_pipeline_view_owns_its_debugger()
    {
        var pipeline = new RenderPipeline();
        var firstView = pipeline.Instantiate();
        var secondView = pipeline.Instantiate();

        Assert.Same(firstView.GetDebugger(), firstView.GetDebugger());
        Assert.NotSame(firstView.GetDebugger(), secondView.GetDebugger());

        firstView.GetDebugger().BeginSnapshot();
        Assert.True(firstView.GetDebugger().IsSnapshotInProgress());
        Assert.False(secondView.GetDebugger().IsSnapshotInProgress());
    }

    [Fact]
    public void Snapshot_batch_constructor_resolves_pixel_light_from_drawable_processor()
    {
        var scene = new Scene();
        var octree = scene.CreateComponent<Octree>();
        var camera = scene.CreateChild("Camera").CreateComponent<Camera>();
        var light = scene.CreateChild("Light").CreateComponent<Light>();
        var drawable = scene.CreateChild("Model").CreateComponent<StaticModel>();

        var geometry = new Geometry();
        var material = new Material(string.Empty);
        var pipelineStateDesc = new GraphicsPipelineStateDesc
        {
            VertexShader = new RawShader(new ShaderBytecode(ShaderType.VS)),
            PixelShader = new RawShader(new ShaderBytecode(ShaderType.PS))
        };
        using var pipelineState = new PipelineState(
            _fixture.PipelineStateCache, new PipelineStateDesc(pipelineStateDesc));
        var pipelineBatch = new PipelineBatch
        {
            Drawable = drawable,
            Geometry = geometry,
            Material = material,
            PipelineState = pipelineState,
            SourceBatchIndex = 0,
            PixelLightIndex = 0,
            Distance = 12.5f
        };

        var pipeline = new RenderPipeline();
        var view = pipeline.Instantiate();
        var processor = new DrawableProcessor(view);
        processor.OnUpdateBegin(new FrameInfo { Scene = scene, Octree = octree, Camera = camera });
        processor.ProcessVisibleDrawables([light], null);

        var batch = new DebugFrameSnapshotBatch(processor, pipelineBatch, newInstancingGroup: true);
        Assert.Same(drawable, batch.Drawable);
        Assert.Same(geometry, batch.Geometry);
        Assert.Same(material, batch.Material);
        Assert.Same(light, batch.Light);
        Assert.Equal(0, batch.SourceBatchIndex);
        Assert.Equal(12.5f, batch.Distance);
        Assert.True(batch.NewInstancingGroup);
        Assert.Equal(0, batch.NumVertices);
        Assert.Equal(0, batch.NumPrimitives);

        var text = batch.ToString();
        Assert.StartsWith("*", text);
        Assert.Contains("lit with", text);
    }

    [Fact]
    public void Debugger_reports_batches_into_active_snapshot()
    {
        var scene = new Scene();
        var octree = scene.CreateComponent<Octree>();
        var camera = scene.CreateChild("Camera").CreateComponent<Camera>();
        var light = scene.CreateChild("Light").CreateComponent<Light>();
        var drawable = scene.CreateChild("Model").CreateComponent<StaticModel>();

        var pipeline = new RenderPipeline();
        var view = pipeline.Instantiate();
        var processor = new DrawableProcessor(view);
        processor.OnUpdateBegin(new FrameInfo { Scene = scene, Octree = octree, Camera = camera });
        processor.ProcessVisibleDrawables([light], null);

        var geometry = new Geometry();
        var material = new Material(string.Empty);
        var pipelineStateDesc = new GraphicsPipelineStateDesc
        {
            VertexShader = new RawShader(new ShaderBytecode(ShaderType.VS)),
            PixelShader = new RawShader(new ShaderBytecode(ShaderType.PS))
        };
        using var pipelineState = new PipelineState(
            _fixture.PipelineStateCache, new PipelineStateDesc(pipelineStateDesc));
        var pipelineBatch = new PipelineBatch
        {
            Drawable = drawable,
            Geometry = geometry,
            Material = material,
            PipelineState = pipelineState,
            SourceBatchIndex = 0,
            PixelLightIndex = 0,
            Distance = 3.0f
        };

        var debugger = view.GetDebugger();
        debugger.BeginSnapshot();
        debugger.BeginPass("Forward");

        // Direct ReportSceneBatch path used by BatchRenderer scene-batch reporting.
        debugger.ReportSceneBatch(new DebugFrameSnapshotBatch(processor, pipelineBatch, true));
        debugger.EndPass();
        debugger.EndSnapshot();

        var snapshot = debugger.GetSnapshot();
        Assert.Single(snapshot.Passes);
        Assert.Equal("Forward", snapshot.Passes[0].Name);
        Assert.Single(snapshot.Passes[0].Batches);
        Assert.Same(drawable, snapshot.Passes[0].Batches[0].Drawable);
        Assert.Same(light, snapshot.Passes[0].Batches[0].Light);
        Assert.Same(material, snapshot.Passes[0].Batches[0].Material);
        Assert.Contains(material, snapshot.SceneMaterials);
    }
}
