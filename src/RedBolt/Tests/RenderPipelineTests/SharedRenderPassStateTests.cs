using RedBolt.Container;
using RedBolt.Graphics;
using RedBolt.RenderPipeline;
namespace RedBolt.Tests;

public sealed class SharedRenderPassStateTests
{
    [Fact]
    public void Shared_render_pass_state_matches_cpp_render_buffer_capacity()
    {
        var state = new SharedRenderPassState();

        Assert.Equal(128, SharedRenderPassState.MaxRenderBuffers);
        Assert.Empty(state.RenderBuffers);
        Assert.Equal(new StringHash("GeometryBuffer.Albedo"), SharedRenderPassState.AlbedoBufferId);
        Assert.Equal(new StringHash("GeometryBuffer.Specular"), SharedRenderPassState.SpecularBufferId);
        Assert.Equal(new StringHash("GeometryBuffer.Normal"), SharedRenderPassState.NormalBufferId);
    }

    [Fact]
    public void Shared_render_pass_state_releases_owned_state_on_dispose()
    {
        var pipeline = new TestRenderPipelineInterface();
        var camera = new Camera();
        var state = new SharedRenderPassState
        {
            RenderPipelineInterface = pipeline,
            RenderCamera = new WeakReference<Camera>(camera)
        };

        state.Dispose();

        Assert.Null(state.RenderPipelineInterface);
        Assert.Null(state.RenderCamera);
        Assert.Null(state.RenderBufferManager);
        Assert.Empty(state.RenderBuffers);
    }
}
