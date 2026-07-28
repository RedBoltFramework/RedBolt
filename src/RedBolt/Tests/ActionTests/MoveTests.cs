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
    }
}
