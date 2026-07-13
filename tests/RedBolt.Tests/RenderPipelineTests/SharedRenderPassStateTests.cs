namespace RedBolt.Tests;

public sealed class SharedRenderPassStateTests
{
    [Fact]
    public void Shared_render_pass_state_matches_cpp_render_buffer_capacity()
    {
        var state = new SharedRenderPassState();

        Assert.Equal(128u, SharedRenderPassState.MaxRenderBuffers);
        Assert.Empty(state.RenderBuffers);
        Assert.Equal(new StringHash("GeometryBuffer.Albedo"), SharedRenderPassState.AlbedoBufferId);
        Assert.Equal(new StringHash("GeometryBuffer.Specular"), SharedRenderPassState.SpecularBufferId);
        Assert.Equal(new StringHash("GeometryBuffer.Normal"), SharedRenderPassState.NormalBufferId);
    }
}
