namespace RedBolt.Tests;

using RedBolt.Actions;
using RedBolt.Core;
using RedBolt.Math;
using RedBolt.Scene;

[Collection(nameof(HeadlessEngineCollection))]
public partial class MoveTests
{
    [Fact]
    public void Move_by_tracks_external_vector3_changes_and_reverses()
    {
        Context.Instance.RegisterAssembly(typeof(MoveTests).Assembly);
        using var manager = new ActionManager(false);
        using var target = new MoveTarget { Position = new Vector3f(1.0f, 2.0f, 3.0f) };
        using var action = new MoveBy();
        action.SetDuration(1.0f);
        action.SetPositionDelta(new Vector3f(4.0f, 6.0f, 8.0f));

        Assert.Equal(AttributeActionConstants.PositionAttribute, action.AttributeName);
        Assert.NotNull(manager.AddAction(action, target));
        manager.Update(0.0f);
        Assert.Equal(new Vector3f(1.0f, 2.0f, 3.0f), target.Position);
        target.Position += new Vector3f(10.0f, 0.0f, 0.0f);
        manager.Update(0.5f);
        Assert.Equal(new Vector3f(13.0f, 5.0f, 7.0f), target.Position);
        manager.Update(0.5f);
        Assert.Equal(new Vector3f(15.0f, 8.0f, 11.0f), target.Position);

        using var reversed = (MoveBy)action.Reverse();
        Assert.Equal(new Vector3f(-4.0f, -6.0f, -8.0f), reversed.PositionDelta);
        Assert.Equal(action.Duration, reversed.Duration);
    }

    [Fact]
    public void Move_by_supports_vector2_and_integer_vectors()
    {
        Context.Instance.RegisterAssembly(typeof(MoveTests).Assembly);
        using var target = new MoveTarget
        {
            Position2 = new Vector2f(1.0f, 2.0f),
            Position2i = new Vector2i(1, 2),
            Position3i = new Vector3i(1, 2, 3)
        };

        using (var manager = new ActionManager(false))
        using (var action = new MoveBy())
        {
            action.SetDuration(1.0f);
            action.SetAttributeName("Position2");
            action.SetPositionDelta(new Vector3f(4.0f, 6.0f, 8.0f));
            manager.AddAction(action, target);
            manager.Update(0.0f);
            manager.Update(1.0f);
            Assert.Equal(new Vector2f(5.0f, 8.0f), target.Position2);
        }

        using (var manager = new ActionManager(false))
        using (var action = new MoveBy())
        {
            action.SetDuration(1.0f);
            action.SetAttributeName("Position2i");
            action.SetPositionDelta(new Vector3f(4.0f, 6.0f, 8.0f));
            manager.AddAction(action, target);
            manager.Update(0.0f);
            manager.Update(1.0f);
            Assert.Equal(new Vector2i(5, 8), target.Position2i);
        }

        using (var manager = new ActionManager(false))
        using (var action = new MoveBy())
        {
            action.SetDuration(1.0f);
            action.SetAttributeName("Position3i");
            action.SetPositionDelta(new Vector3f(4.0f, 6.0f, 8.0f));
            manager.AddAction(action, target);
            manager.Update(0.0f);
            manager.Update(1.0f);
            Assert.Equal(new Vector3i(5, 8, 11), target.Position3i);
        }
    }

    [Fact]
    public void Move_by_quadratic_tracks_external_changes_and_reverses()
    {
        Context.Instance.RegisterAssembly(typeof(MoveTests).Assembly);
        using var manager = new ActionManager(false);
        using var target = new MoveTarget { Position = new Vector3f(1.0f, 2.0f, 3.0f) };
        using var action = new MoveByQuadratic();
        action.SetDuration(1.0f);
        action.SetPositionDelta(new Vector3f(8.0f, 12.0f, 16.0f));
        action.SetControlDelta(new Vector3f(4.0f, 8.0f, 12.0f));

        Assert.Equal(AttributeActionConstants.PositionAttribute, action.AttributeName);
        Assert.NotNull(manager.AddAction(action, target));
        manager.Update(0.0f);
        target.Position += new Vector3f(10.0f, 0.0f, 0.0f);
        manager.Update(0.5f);
        Assert.Equal(new Vector3f(15.0f, 9.0f, 13.0f), target.Position);
        manager.Update(0.5f);
        Assert.Equal(new Vector3f(19.0f, 14.0f, 19.0f), target.Position);

        using var reversed = (MoveByQuadratic)action.Reverse();
        Assert.Equal(new Vector3f(-8.0f, -12.0f, -16.0f), reversed.PositionDelta);
        Assert.Equal(new Vector3f(-4.0f, -8.0f, -12.0f), reversed.ControlDelta);
        Assert.Equal(action.Duration, reversed.Duration);
    }

    [Fact]
    public void Move_by_quadratic_supports_vector2_and_integer_vectors()
    {
        Context.Instance.RegisterAssembly(typeof(MoveTests).Assembly);
        using var target = new MoveTarget
        {
            Position2 = new Vector2f(1.0f, 2.0f),
            Position2i = new Vector2i(1, 2),
            Position3i = new Vector3i(1, 2, 3)
        };

        using (var manager = new ActionManager(false))
        using (var action = new MoveByQuadratic())
        {
            action.SetDuration(1.0f);
            action.SetAttributeName("Position2");
            action.SetPositionDelta(new Vector3f(8.0f, 12.0f, 16.0f));
            action.SetControlDelta(new Vector3f(4.0f, 8.0f, 12.0f));
            manager.AddAction(action, target);
            manager.Update(0.0f);
            manager.Update(0.5f);
            Assert.Equal(new Vector2f(5.0f, 9.0f), target.Position2);
        }

        using (var manager = new ActionManager(false))
        using (var action = new MoveByQuadratic())
        {
            action.SetDuration(1.0f);
            action.SetAttributeName("Position2i");
            action.SetPositionDelta(new Vector3f(8.0f, 12.0f, 16.0f));
            action.SetControlDelta(new Vector3f(4.0f, 8.0f, 12.0f));
            manager.AddAction(action, target);
            manager.Update(0.0f);
            manager.Update(0.5f);
            Assert.Equal(new Vector2i(5, 9), target.Position2i);
        }

        using (var manager = new ActionManager(false))
        using (var action = new MoveByQuadratic())
        {
            action.SetDuration(1.0f);
            action.SetAttributeName("Position3i");
            action.SetPositionDelta(new Vector3f(8.0f, 12.0f, 16.0f));
            action.SetControlDelta(new Vector3f(4.0f, 8.0f, 12.0f));
            manager.AddAction(action, target);
            manager.Update(0.0f);
            manager.Update(0.5f);
            Assert.Equal(new Vector3i(5, 9, 13), target.Position3i);
        }
    }

    [Fact]
    public void Jump_by_triggers_once_and_reverses()
    {
        Context.Instance.RegisterAssembly(typeof(MoveTests).Assembly);
        using var manager = new ActionManager(false);
        using var target = new MoveTarget { Position = new Vector3f(1.0f, 2.0f, 3.0f) };
        using var action = new JumpBy();
        action.SetAttributeName("Position");
        action.SetDuration(1.0f);
        action.SetPositionDelta(new Vector3f(4.0f, 6.0f, 8.0f));

        Assert.NotNull(manager.AddAction(action, target));
        manager.Update(0.0f);
        Assert.Equal(new Vector3f(5.0f, 8.0f, 11.0f), target.Position);
        manager.Update(0.5f);
        Assert.Equal(new Vector3f(5.0f, 8.0f, 11.0f), target.Position);

        using var reversed = (JumpBy)action.Reverse();
        Assert.Equal(new Vector3f(-4.0f, -6.0f, -8.0f), reversed.PositionDelta);
        Assert.Equal(action.Duration, reversed.Duration);
    }

    [Fact]
    public void Jump_by_supports_vector2_and_integer_vectors()
    {
        Context.Instance.RegisterAssembly(typeof(MoveTests).Assembly);
        using var target = new MoveTarget
        {
            Position2 = new Vector2f(1.0f, 2.0f),
            Position2i = new Vector2i(1, 2),
            Position3i = new Vector3i(1, 2, 3)
        };

        using (var manager = new ActionManager(false))
        using (var action = new JumpBy())
        {
            action.SetAttributeName("Position2");
            action.SetPositionDelta(new Vector3f(4.0f, 6.0f, 8.0f));
            manager.AddAction(action, target);
            manager.Update(0.0f);
            Assert.Equal(new Vector2f(5.0f, 8.0f), target.Position2);
        }

        using (var manager = new ActionManager(false))
        using (var action = new JumpBy())
        {
            action.SetAttributeName("Position2i");
            action.SetPositionDelta(new Vector3f(4.0f, 6.0f, 8.0f));
            manager.AddAction(action, target);
            manager.Update(0.0f);
            Assert.Equal(new Vector2i(5, 8), target.Position2i);
        }

        using (var manager = new ActionManager(false))
        using (var action = new JumpBy())
        {
            action.SetAttributeName("Position3i");
            action.SetPositionDelta(new Vector3f(4.0f, 6.0f, 8.0f));
            manager.AddAction(action, target);
            manager.Update(0.0f);
            Assert.Equal(new Vector3i(5, 8, 11), target.Position3i);
        }
    }

    [Fact]
    public void Scale_by_tracks_external_changes_and_reverses()
    {
        Context.Instance.RegisterAssembly(typeof(MoveTests).Assembly);
        using var manager = new ActionManager(false);
        using var target = new MoveTarget { Scale = new Vector3f(1.0f, 2.0f, 3.0f) };
        using var action = new ScaleBy();
        action.SetAttributeName("Scale");
        action.SetDuration(1.0f);
        action.SetScaleDelta(new Vector3f(2.0f, 3.0f, 4.0f));

        Assert.NotNull(manager.AddAction(action, target));
        manager.Update(0.0f);
        target.Scale *= 2.0f;
        manager.Update(0.5f);
        Assert.Equal(new Vector3f(3.0f, 8.0f, 15.0f), target.Scale);
        manager.Update(0.5f);
        Assert.Equal(new Vector3f(4.0f, 12.0f, 24.0f), target.Scale);

        using var reversed = (ScaleBy)action.Reverse();
        Assert.Equal(new Vector3f(0.5f, 1.0f / 3.0f, 0.25f), reversed.ScaleDelta);
        Assert.Equal(action.Duration, reversed.Duration);
    }

    [Fact]
    public void Scale_by_supports_vector2()
    {
        Context.Instance.RegisterAssembly(typeof(MoveTests).Assembly);
        using var manager = new ActionManager(false);
        using var target = new MoveTarget { Scale2 = new Vector2f(1.0f, 2.0f) };
        using var action = new ScaleBy();
        action.SetAttributeName("Scale2");
        action.SetDuration(1.0f);
        action.SetScaleDelta(new Vector3f(2.0f, 3.0f, 4.0f));

        Assert.NotNull(manager.AddAction(action, target));
        manager.Update(0.0f);
        manager.Update(0.5f);
        Assert.Equal(new Vector2f(1.5f, 4.0f), target.Scale2);
        manager.Update(0.5f);
        Assert.Equal(new Vector2f(2.0f, 6.0f), target.Scale2);
    }

    [Fact]
    public void Rotate_by_tracks_external_changes_and_reverses()
    {
        Context.Instance.RegisterAssembly(typeof(MoveTests).Assembly);
        using var manager = new ActionManager(false);
        using var target = new MoveTarget { Rotation = Quaternion.IDENTITY };
        using var action = new RotateBy();
        action.SetAttributeName("Rotation");
        action.SetDuration(1.0f);
        action.SetRotationDelta(new Quaternion(90.0f, Vector3f.Up));

        Assert.NotNull(manager.AddAction(action, target));
        manager.Update(0.0f);
        var externalRotation = new Quaternion(30.0f, Vector3f.Right);
        target.Rotation = externalRotation;
        manager.Update(0.5f);
        Assert.True(
            target.Rotation.Equivalent(externalRotation * new Quaternion(45.0f, Vector3f.Up), 0.001f));
        manager.Update(0.5f);
        Assert.True(
            target.Rotation.Equivalent(externalRotation * new Quaternion(90.0f, Vector3f.Up), 0.001f));

        using var reversed = (RotateBy)action.Reverse();
        Assert.True(reversed.RotationDelta.Equivalent(action.RotationDelta.Inverse, 0.001f));
        Assert.Equal(action.Duration, reversed.Duration);
    }

    [Fact]
    public void Rotate_around_matches_parent_space_node_rotation_and_reverses()
    {
        var position = new Vector3f(3.0f, 2.0f, 0.0f);
        var rotation = new Quaternion(15.0f, new Vector3f(1.0f, 0.0f, 0.0f));
        var pivot = new Vector3f(1.0f, -2.0f, 0.0f);
        var delta = new Quaternion(75.0f, new Vector3f(0.0f, 2.0f, 3.0f));

        using var manager = new ActionManager(false);
        using var movedNode = new Node();
        movedNode.SetPosition(position);
        movedNode.SetRotation(rotation);
        using var expectedNode = new Node();
        expectedNode.SetPosition(position);
        expectedNode.SetRotation(rotation);
        expectedNode.RotateAround(pivot, delta, TransformSpace.Parent);
        using var action = new RotateAround();
        action.SetDuration(2.0f);
        action.SetRotationDelta(delta);
        action.SetPivot(pivot);

        Assert.NotNull(manager.AddAction(action, movedNode));
        for (var i = 0; i < 5; ++i)
            manager.Update(0.5f);

        Assert.True(movedNode.Rotation.Equivalent(expectedNode.Rotation, 0.001f));
        Assert.True(movedNode.Position.ApproximatelyEquivalent(expectedNode.Position, 0.001f));

        using var reversed = (RotateAround)action.Reverse();
        Assert.True(reversed.RotationDelta.Equivalent(action.RotationDelta.Inverse, 0.001f));
        Assert.Equal(action.Pivot, reversed.Pivot);
        Assert.Equal(action.Duration, reversed.Duration);
    }

    [Reflected]
    private sealed partial class MoveTarget : Serializable
    {
        [Serialized("Position")]
        public Vector3f Position { get; set; }

        [Serialized("Position2")]
        public Vector2f Position2 { get; set; }

        [Serialized("Position2i")]
        public Vector2i Position2i { get; set; }

        [Serialized("Position3i")]
        public Vector3i Position3i { get; set; }

        [Serialized("Scale")]
        public Vector3f Scale { get; set; }

        [Serialized("Scale2")]
        public Vector2f Scale2 { get; set; }

        [Serialized("Rotation")]
        public Quaternion Rotation { get; set; }
    }
}
