using RedBolt;
using RedBolt.Math;

namespace RedBolt.Tests;

public sealed class InverseKinematicsTypesTests
{
    [Fact]
    public void Settings_are_an_independent_reference_free_value_with_native_defaults()
    {
        Assert.True(typeof(IKSettings).IsValueType);
        Assert.False(System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<IKSettings>());

        var settings = new IKSettings();

        Assert.Equal(10u, settings.MaxIterations);
        Assert.Equal(0.001f, settings.Tolerance);
        Assert.False(settings.ContinuousRotations);

        var copy = settings;
        copy.MaxIterations = 20;
        copy.Tolerance = 0.5f;
        copy.ContinuousRotations = true;

        Assert.Equal(10u, settings.MaxIterations);
        Assert.Equal(0.001f, settings.Tolerance);
        Assert.False(settings.ContinuousRotations);
    }

    [Fact]
    public void Default_node_and_copy_match_native_value_construction()
    {
        Assert.False(typeof(IKNode).IsSealed);
        Assert.False(typeof(IKNode).IsValueType);
        Assert.True(typeof(ICloneable).IsAssignableFrom(typeof(IKNode)));

        var node = new IKNode();

        Assert.Equal(Vector3f.Zero, node.LocalOriginalPosition);
        Assert.Equal(Quaternion.IDENTITY, node.LocalOriginalRotation);
        Assert.Equal(Vector3f.Zero, node.OriginalPosition);
        Assert.Equal(Quaternion.IDENTITY, node.OriginalRotation);
        Assert.Equal(Vector3f.Zero, node.PreviousPosition);
        Assert.Equal(Quaternion.IDENTITY, node.PreviousRotation);
        Assert.Equal(Vector3f.Zero, node.Position);
        Assert.Equal(Quaternion.IDENTITY, node.Rotation);
        Assert.False(node.PositionDirty);
        Assert.False(node.RotationDirty);

        node.LocalOriginalPosition = Vector3f.Right;
        node.LocalOriginalRotation = new Quaternion(10.0f, Vector3f.Up);
        node.OriginalPosition = Vector3f.Up;
        node.OriginalRotation = new Quaternion(20.0f, Vector3f.Up);
        node.PreviousPosition = Vector3f.Forward;
        node.PreviousRotation = new Quaternion(30.0f, Vector3f.Up);
        node.Position = Vector3f.Left;
        node.Rotation = new Quaternion(40.0f, Vector3f.Up);
        node.PositionDirty = true;
        node.RotationDirty = true;

        var copy = (IKNode)node.Clone();

        Assert.NotSame(node, copy);
        Assert.Equal(node.LocalOriginalPosition, copy.LocalOriginalPosition);
        Assert.Equal(node.LocalOriginalRotation, copy.LocalOriginalRotation);
        Assert.Equal(node.OriginalPosition, copy.OriginalPosition);
        Assert.Equal(node.OriginalRotation, copy.OriginalRotation);
        Assert.Equal(node.PreviousPosition, copy.PreviousPosition);
        Assert.Equal(node.PreviousRotation, copy.PreviousRotation);
        Assert.Equal(node.Position, copy.Position);
        Assert.Equal(node.Rotation, copy.Rotation);
        Assert.Equal(node.PositionDirty, copy.PositionDirty);
        Assert.Equal(node.RotationDirty, copy.RotationDirty);

        copy.Position = Vector3f.Down;
        Assert.Equal(Vector3f.Left, node.Position);
    }

    [Fact]
    public void Segment_copy_has_independent_value_state_and_shared_node_aliases()
    {
        Assert.True(typeof(IKNodeSegment).IsValueType);

        var beginNode = new IKNode(Vector3f.Zero, Quaternion.IDENTITY);
        var endNode = new IKNode(Vector3f.Right, Quaternion.IDENTITY);
        var segment = new IKNodeSegment { BeginNode = beginNode, EndNode = endNode, Length = 1.0f };

        var copy = segment;
        copy.BeginNode = new IKNode(Vector3f.Up, Quaternion.IDENTITY);
        copy.Length = 2.0f;

        Assert.Same(beginNode, segment.BeginNode);
        Assert.Same(endNode, segment.EndNode);
        Assert.Same(endNode, copy.EndNode);
        Assert.Equal(1.0f, segment.Length);
        Assert.Equal(2.0f, copy.Length);
    }

    [Fact]
    public void Node_tracks_original_previous_and_current_transforms()
    {
        var node = new IKNode(Vector3f.Right, Quaternion.IDENTITY);
        node.Position = Vector3f.Up;
        node.RotationDirty = true;

        node.StorePreviousTransform();

        Assert.Equal(Vector3f.Up, node.PreviousPosition);
        Assert.False(node.PositionDirty);
        Assert.False(node.RotationDirty);

        node.ResetOriginalTransform();
        Assert.Equal(Vector3f.Right, node.Position);
        Assert.Equal(Quaternion.IDENTITY, node.Rotation);
    }

    [Fact]
    public void Segment_updates_length_direction_and_rotation()
    {
        var beginNode = new IKNode(Vector3f.Zero, Quaternion.IDENTITY);
        var endNode = new IKNode(Vector3f.Right, Quaternion.IDENTITY);
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
