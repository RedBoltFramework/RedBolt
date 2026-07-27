using RedBolt;
using RedBolt.Math;

namespace RedBolt.Tests;

public sealed class InverseKinematicsChainsTests
{
    [Fact]
    public void Trigonometric_chain_copy_has_independent_storage_and_shared_nodes()
    {
        Assert.False(typeof(IKTrigonometricChain).IsSealed);
        Assert.False(typeof(IKTrigonometricChain).IsValueType);
        Assert.True(typeof(ICloneable).IsAssignableFrom(typeof(IKTrigonometricChain)));

        var first = new IKNode(Vector3f.Zero, Quaternion.IDENTITY);
        var middle = new IKNode(Vector3f.Right, Quaternion.IDENTITY);
        var end = new IKNode(new Vector3f(2.0f, 0.0f, 0.0f), Quaternion.IDENTITY);
        var chain = new IKTrigonometricChain();
        chain.Initialize(first, middle, end);
        chain.UpdateLengths();

        var copy = (IKTrigonometricChain)chain.Clone();

        Assert.Same(first, copy.GetBeginNode());
        Assert.Same(middle, copy.GetMiddleNode());
        Assert.Same(end, copy.GetEndNode());
        Assert.Equal(chain.GetFirstLength(), copy.GetFirstLength());
        Assert.Equal(chain.GetSecondLength(), copy.GetSecondLength());

        var otherFirst = new IKNode(Vector3f.Zero, Quaternion.IDENTITY);
        var otherMiddle = new IKNode(Vector3f.Up, Quaternion.IDENTITY);
        var otherEnd = new IKNode(new Vector3f(0.0f, 2.0f, 0.0f), Quaternion.IDENTITY);
        copy.Initialize(otherFirst, otherMiddle, otherEnd);

        Assert.Same(first, chain.GetBeginNode());
        Assert.Same(middle, chain.GetMiddleNode());
        Assert.Same(end, chain.GetEndNode());
        Assert.Same(otherFirst, copy.GetBeginNode());
        Assert.Same(otherMiddle, copy.GetMiddleNode());
        Assert.Same(otherEnd, copy.GetEndNode());
    }

    [Fact]
    public void Eye_chain_copy_has_independent_values_and_shared_root_node()
    {
        Assert.False(typeof(IKEyeChain).IsSealed);
        Assert.False(typeof(IKEyeChain).IsValueType);
        Assert.True(typeof(ICloneable).IsAssignableFrom(typeof(IKEyeChain)));

        var root = new IKNode(Vector3f.Zero, Quaternion.IDENTITY);
        var chain = new IKEyeChain();
        chain.Initialize(root);
        chain.SetEyeTransform(Vector3f.Right, Vector3f.Forward);

        var copy = (IKEyeChain)chain.Clone();

        root.Rotation = new Quaternion(90.0f, Vector3f.Up);
        Assert.True(chain.SolveLookTo(Vector3f.Right).Equivalent(copy.SolveLookTo(Vector3f.Right)));

        copy.SetEyeTransform(Vector3f.Up, Vector3f.Down);
        Assert.Equal(Vector3f.Right, chain.GetLocalEyeOffset());
        Assert.Equal(Vector3f.Forward, chain.GetLocalEyeDirection());
        Assert.Equal(Vector3f.Up, copy.GetLocalEyeOffset());
        Assert.Equal(Vector3f.Down, copy.GetLocalEyeDirection());
    }

    [Fact]
    public void Trigonometric_chain_solves_two_segments()
    {
        var first = new IKNode(Vector3f.Zero, Quaternion.IDENTITY);
        var middle = new IKNode(Vector3f.Right, Quaternion.IDENTITY);
        var end = new IKNode(new Vector3f(2.0f, 0.0f, 0.0f), Quaternion.IDENTITY);
        var chain = new IKTrigonometricChain();
        chain.Initialize(first, middle, end);
        chain.UpdateLengths();

        chain.Solve(new Vector3f(1.0f, 1.0f, 0.0f), Vector3f.Up, Vector3f.Up, 0.0f, 180.0f);

        Assert.True(end.Position.ApproximatelyEquivalent(new Vector3f(1.0f, 1.0f, 0.0f)));
        Assert.True(chain.GetFirstLength() > 0.0f);
        Assert.Same(first, chain.GetBeginNode());
        Assert.Same(middle, chain.GetMiddleNode());
        Assert.Same(end, chain.GetEndNode());
    }

    [Fact]
    public void Eye_chain_solves_look_to_and_local_transform()
    {
        var root = new IKNode(Vector3f.Zero, Quaternion.IDENTITY);
        var chain = new IKEyeChain();
        chain.Initialize(root);
        chain.SetEyeTransform(Vector3f.Zero, Vector3f.Forward);

        Assert.True(chain.SolveLookTo(Vector3f.Right).Equivalent(new Quaternion(90.0f, Vector3f.Up)));

        chain.SetLocalEyeTransform(Vector3f.Right, Vector3f.Up);
        Assert.Equal(Vector3f.Right, chain.GetLocalEyeOffset());
        Assert.Equal(Vector3f.Up, chain.GetLocalEyeDirection());
    }
}
