namespace RedBolt.Tests;

using RedBolt.Actions;
using RedBolt.Core;
using RedBolt.Math;
using RedBolt.Scene;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class ActionBuilderTests
{
    [Fact]
    public void Then_and_also_match_cpp_action_nesting()
    {
        using var manager = new ActionManager(false);
        Context.Instance.RegisterSubsystem(manager);
        try
        {
            using var delay = new DelayTime();
            delay.SetDuration(2.0f);

            var action = new ActionBuilder()
                .MoveBy(1.0f, new Vector3f(1.0f, 2.0f, 3.0f))
                .Also(delay)
                .DelayTime(4.0f)
                .Build();

            var sequence = Assert.IsType<Sequence>(action);
            var parallel = Assert.IsType<Parallel>(sequence.FirstAction);
            Assert.Equal(2, parallel.NumActions);
            Assert.IsType<MoveBy>(parallel.GetAction(0));
            Assert.Same(delay, parallel.GetAction(1));
            Assert.Equal(4.0f, Assert.IsType<DelayTime>(sequence.SecondAction).Duration);
        }
        finally
        {
            Context.Instance.RemoveSubsystem<ActionManager>();
        }
    }

    [Fact]
    public void Modifiers_wrap_current_action_and_preserve_parameters()
    {
        using var manager = new ActionManager(false);
        Context.Instance.RegisterSubsystem(manager);
        try
        {
            var action = new ActionBuilder()
                .MoveBy(1.0f, new Vector2f(2.0f, 3.0f))
                .ElasticInOut(0.75f)
                .Repeat(3)
                .Build();

            var repeat = Assert.IsType<Repeat>(action);
            Assert.Equal(3, repeat.Times);
            var ease = Assert.IsType<EaseElasticInOut>(repeat.InnerAction);
            Assert.Equal(0.75f, ease.Period);
            Assert.Equal(
                new Vector3f(2.0f, 3.0f, 0.0f), Assert.IsType<MoveBy>(ease.InnerAction).PositionDelta);
        }
        finally
        {
            Context.Instance.RemoveSubsystem<ActionManager>();
        }
    }

    [Fact]
    public void Run_uses_the_provided_action_manager()
    {
        using var manager = new ActionManager(false);
        using var target = new Node();

        var state = new ActionBuilder()
            .MoveBy(1.0f, new Vector3f(1.0f, 0.0f, 0.0f))
            .Run(manager, target);

        Assert.NotNull(state);
        manager.Update(0.0f);
        manager.Update(1.0f);
        Assert.Equal(new Vector3f(1.0f, 0.0f, 0.0f), target.Position);
    }
}
