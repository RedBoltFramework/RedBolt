namespace RedBolt.Tests;

public sealed class PipelineStateDescParityTests
{
    [Fact]
    public void Default_pipeline_state_description_uses_graphics_variant()
    {
        var desc = new PipelineStateDesc();

        Assert.Equal(PipelineStateType.Graphics, desc.GetPipelineType());
        Assert.NotNull(desc.AsGraphics());
        Assert.Null(desc.AsCompute());
    }
}
