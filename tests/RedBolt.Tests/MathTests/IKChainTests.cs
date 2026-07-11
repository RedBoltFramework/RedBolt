using RedBolt;

namespace RedBolt.Tests;

public sealed class IKChainTests
{
    [Fact]
    public void Add_node_builds_segments_and_updates_lengths()
    {
        var first = new IKNode(Vector3f.Zero, Quaternion.Identity);
        var second = new IKNode(Vector3f.Right, Quaternion.Identity);
        var third = new IKNode(new Vector3f(1.0f, 1.0f, 0.0f), Quaternion.Identity);
        var chain = new IKChain();

        chain.AddNode(first);
        chain.AddNode(second);
        chain.AddNode(third);
        chain.UpdateLengths();

        Assert.Equal(3, chain.GetNodes().Count);
        Assert.Equal(2, chain.GetSegments().Count);
        Assert.True(MathDefs.ApproximatelyEquivalent(chain.GetSegments()[0].Length + chain.GetSegments()[1].Length, 2.0f, 0.00001f));
        Assert.True(chain.FindSegment(second).HasValue);
        Assert.Same(first, chain.FindSegment(first)!.Value.BeginNode);
    }

    [Fact]
    public void Clear_removes_nodes_and_segments()
    {
        var chain = new IKChain();
        chain.AddNode(new IKNode(Vector3f.Zero, Quaternion.Identity));

        chain.Clear();

        Assert.Empty(chain.GetNodes());
        Assert.Empty(chain.GetSegments());
    }
}
