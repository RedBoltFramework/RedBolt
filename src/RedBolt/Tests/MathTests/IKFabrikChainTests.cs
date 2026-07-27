using RedBolt;

using RedBolt.Math;
namespace RedBolt.Tests;

public sealed class IKFabrikChainTests
{
    [Fact]
    public void Clone_preserves_derived_type_and_copies_base_containers()
    {
        var first = new IKNode(Vector3f.Zero, Quaternion.IDENTITY);
        var second = new IKNode(Vector3f.Right, Quaternion.IDENTITY);
        var chain = new IKFabrikChain();
        chain.AddNode(first);
        chain.AddNode(second);

        var clone = Assert.IsType<IKFabrikChain>(((ICloneable)chain).Clone());

        Assert.NotSame(chain.GetNodes(), clone.GetNodes());
        Assert.NotSame(chain.GetSegments(), clone.GetSegments());
        Assert.Same(chain.GetNodes()[0], clone.GetNodes()[0]);
    }

    [Fact]
    public void Solve_reaches_target_without_changing_segment_lengths()
    {
        var first = new IKNode(Vector3f.Zero, Quaternion.IDENTITY);
        var middle = new IKNode(Vector3f.Right, Quaternion.IDENTITY);
        var last = new IKNode(new Vector3f(2.0f, 0.0f, 0.0f), Quaternion.IDENTITY);
        var chain = new IKFabrikChain();
        chain.AddNode(first);
        chain.AddNode(middle);
        chain.AddNode(last);
        chain.UpdateLengths();

        chain.Solve(new Vector3f(1.0f, 1.0f, 0.0f), new IKSettings { MaxIterations = 20, Tolerance = 0.0001f });

        Assert.True((last.Position - new Vector3f(1.0f, 1.0f, 0.0f)).Length < 0.001f);
        Assert.True(MathDefs.Equals((middle.Position - first.Position).Length, 1.0f));
        Assert.True(MathDefs.Equals((last.Position - middle.Position).Length, 1.0f));
    }
}
