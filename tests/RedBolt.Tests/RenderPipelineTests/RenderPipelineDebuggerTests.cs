using RedBolt;

namespace RedBolt.Tests;

public sealed class RenderPipelineDebuggerTests
{
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
}
