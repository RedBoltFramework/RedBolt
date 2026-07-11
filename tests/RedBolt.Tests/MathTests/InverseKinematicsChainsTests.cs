using RedBolt;

namespace RedBolt.Tests;

public sealed class InverseKinematicsChainsTests
{
    [Fact]
    public void Trigonometric_chain_solves_two_segments()
    {
        var first = new IKNode(Vector3f.Zero, Quaternion.Identity);
        var middle = new IKNode(Vector3f.Right, Quaternion.Identity);
        var end = new IKNode(new Vector3f(2.0f, 0.0f, 0.0f), Quaternion.Identity);
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
        var root = new IKNode(Vector3f.Zero, Quaternion.Identity);
        var chain = new IKEyeChain();
        chain.Initialize(root);
        chain.SetEyeTransform(Vector3f.Zero, Vector3f.Forward);

        Assert.True(chain.SolveLookTo(Vector3f.Right).Equivalent(Quaternion.FromAngleAxis(90.0f, Vector3f.Up)));

        chain.SetLocalEyeTransform(Vector3f.Right, Vector3f.Up);
        Assert.Equal(Vector3f.Right, chain.GetLocalEyeOffset());
        Assert.Equal(Vector3f.Up, chain.GetLocalEyeDirection());
    }
}
