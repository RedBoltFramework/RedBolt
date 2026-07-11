using RedBolt;

namespace RedBolt.Tests;

public sealed class InverseKinematicsTypesTests
{
    [Fact]
    public void Node_tracks_original_previous_and_current_transforms()
    {
        var node = new IKNode(Vector3f.Right, Quaternion.Identity);
        node.Position = Vector3f.Up;
        node.RotationDirty = true;

        node.StorePreviousTransform();

        Assert.Equal(Vector3f.Up, node.PreviousPosition);
        Assert.False(node.PositionDirty);
        Assert.False(node.RotationDirty);

        node.ResetOriginalTransform();
        Assert.Equal(Vector3f.Right, node.Position);
        Assert.Equal(Quaternion.Identity, node.Rotation);
    }

    [Fact]
    public void Segment_updates_length_direction_and_rotation()
    {
        var beginNode = new IKNode(Vector3f.Zero, Quaternion.Identity);
        var endNode = new IKNode(Vector3f.Right, Quaternion.Identity);
        var segment = new IKNodeSegment { BeginNode = beginNode, EndNode = endNode };

        segment.UpdateLength();
        Assert.Equal(1.0f, segment.Length);
        Assert.Equal(Vector3f.Right, segment.CalculateDirection());

        endNode.Position = Vector3f.Up;
        segment.UpdateRotationInNodes(false, true);

        Assert.True(beginNode.RotationDirty);
        Assert.True(endNode.RotationDirty);
        Assert.True((beginNode.Rotation * Vector3f.Right).ApproximatelyEquivalent(Vector3f.Up));
    }
}
