namespace RedBolt.Tests;

public sealed class RenderPathMutationTests
{
    [Fact]
    public void Render_path_remove_and_reorder_match_void_cpp_api()
    {
        var renderPath = new RenderPath();
        renderPath.SetName("RenderPaths/Test");
        var first = new TestRenderPass("First");
        var second = new TestRenderPass("Second");
        renderPath.AddPass(first);
        renderPath.AddPass(second);

        renderPath.ReorderPass(second, 0);
        renderPath.RemovePass(first);

        Assert.Same(second, Assert.Single(renderPath.Passes));
    }

    private sealed class TestRenderPass : RenderPass
    {
        public TestRenderPass(string passName)
        {
            _passName = passName;
        }

        public override RenderPass Clone() => new TestRenderPass(GetPassName());
    }
}
