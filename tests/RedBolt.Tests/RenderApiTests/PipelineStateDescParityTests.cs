namespace RedBolt.Tests;

public sealed class PipelineStateDescParityTests
{
    [Fact]
    public void Pipeline_state_description_contains_only_its_selected_variant()
    {
        var graphics = new GraphicsPipelineStateDesc();
        var compute = new ComputePipelineStateDesc();

        var graphicsDesc = new PipelineStateDesc(graphics);
        var computeDesc = new PipelineStateDesc(compute);

        Assert.NotNull(graphicsDesc.AsGraphics());
        Assert.Null(graphicsDesc.AsCompute());
        Assert.Null(computeDesc.AsGraphics());
        Assert.NotNull(computeDesc.AsCompute());
    }

    [Fact]
    public void Default_pipeline_state_description_uses_graphics_variant()
    {
        var desc = new PipelineStateDesc();

        Assert.Equal(PipelineStateType.Graphics, desc.GetPipelineType());
        Assert.NotNull(desc.AsGraphics());
        Assert.Null(desc.AsCompute());
    }
}
