using RedBolt;

namespace RedBolt.Tests;

public sealed class IKChainTests
{
    [Fact]
    public void Copy_construction_copies_containers_and_preserves_node_aliases()
    {
        var first = new IKNode(Vector3f.Zero, Quaternion.Identity);
        var second = new IKNode(Vector3f.Right, Quaternion.Identity);
        var chain = new IKChain();
        chain.AddNode(first);
        chain.AddNode(second);
        chain.UpdateLengths();

        var copy = (IKChain)chain.Clone();

        Assert.NotSame(chain.GetNodes(), copy.GetNodes());
        Assert.NotSame(chain.GetSegments(), copy.GetSegments());
        Assert.Same(chain.GetNodes()[0], copy.GetNodes()[0]);
        Assert.Same(chain.GetSegments()[0].BeginNode, copy.GetSegments()[0].BeginNode);

        chain.GetNodes().Clear();
        chain.GetSegments().Clear();
        Assert.Equal(2, copy.GetNodes().Count);
        Assert.Single(copy.GetSegments());
    }

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
        Assert.True(MathDefs.Equals(chain.GetSegments()[0].Length + chain.GetSegments()[1].Length, 2.0f, 0.00001f));
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
