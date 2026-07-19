using RedBolt;

namespace RedBolt.Tests;

public sealed class IKSpineChainTests
{
    [Fact]
    public void Clone_preserves_derived_type_and_copies_base_containers()
    {
        var first = new IKNode(Vector3f.Zero, Quaternion.Identity);
        var second = new IKNode(Vector3f.Right, Quaternion.Identity);
        var chain = new IKSpineChain();
        chain.AddNode(first);
        chain.AddNode(second);
        chain.UpdateLengths();
        chain.Solve(Vector3f.Up, Vector3f.Right, 90.0f, new IKSettings());

        var clone = Assert.IsType<IKSpineChain>(((ICloneable)chain).Clone());

        Assert.NotSame(chain.GetNodes(), clone.GetNodes());
        Assert.NotSame(chain.GetSegments(), clone.GetSegments());
        Assert.Same(chain.GetNodes()[0], clone.GetNodes()[0]);
    }

    [Fact]
    public void Solve_preserves_segment_lengths_and_bends_toward_target()
    {
        var first = new IKNode(Vector3f.Zero, Quaternion.Identity);
        var middle = new IKNode(Vector3f.Right, Quaternion.Identity);
        var last = new IKNode(new Vector3f(2.0f, 0.0f, 0.0f), Quaternion.Identity);
        var chain = new IKSpineChain();
        chain.AddNode(first);
        chain.AddNode(middle);
        chain.AddNode(last);
        chain.UpdateLengths();

        chain.Solve(new Vector3f(1.5f, 1.0f, 0.0f), Vector3f.Right, 90.0f, new IKSettings());

        Assert.True(MathDefs.Equals((middle.Position - first.Position).Length, 1.0f));
        Assert.True(MathDefs.Equals((last.Position - middle.Position).Length, 1.0f));
        Assert.True(last.Position.Y > 0.0f);
    }
}
