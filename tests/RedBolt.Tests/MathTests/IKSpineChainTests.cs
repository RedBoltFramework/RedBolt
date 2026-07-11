using RedBolt;

namespace RedBolt.Tests;

public sealed class IKSpineChainTests
{
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

        Assert.True(MathDefs.ApproximatelyEquivalent((middle.Position - first.Position).Length, 1.0f));
        Assert.True(MathDefs.ApproximatelyEquivalent((last.Position - middle.Position).Length, 1.0f));
        Assert.True(last.Position.Y > 0.0f);
    }
}
