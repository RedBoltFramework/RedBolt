namespace RedBolt.Tests;

using System.Collections.Generic;

using RedBolt.Actions;
using RedBolt.Core;
using RedBolt.Scene;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class ParallelTests
{
    [Fact]
    public void Parallel_uses_longest_duration()
    {
        using var first = new DelayTime();
        first.SetDuration(1.0f);
        using var second = new DelayTime();
        second.SetDuration(2.0f);
        using var action = new Parallel();
        action.AddAction(first);
        action.AddAction(second);

        Assert.Equal(2, action.NumActions);
        Assert.Same(first, action.GetAction(0));
        Assert.Same(second, action.GetAction(1));
        Assert.Equal(2.0f, action.Duration);
    }

    [Fact]
    public void Parallel_scales_inner_action_time()
    {
        using var manager = new ActionManager(false);
        using var target = new Node();
        using var first = new RecordingAction();
        first.SetDuration(1.0f);
        using var second = new RecordingAction();
        second.SetDuration(2.0f);
        using var action = new Parallel();
        action.AddAction(first);
        action.AddAction(second);

        Assert.NotNull(manager.AddAction(action, target));
        manager.Update(0.0f);
        manager.Update(0.5f);

        Assert.NotNull(first.State);
        Assert.NotNull(second.State);
        Assert.Equal([0.0f, 0.5f], first.State.Updates);
        Assert.Equal([0.0f, 0.25f], second.State.Updates);
    }

    [Fact]
    public void Parallel_reverses_inner_actions()
    {
        using var first = new MoveBy();
        first.SetDuration(1.0f);
        first.SetPositionDelta(new Vector3f(1.0f, 2.0f, 3.0f));
        using var second = new DelayTime();
        second.SetDuration(2.0f);
        using var action = new Parallel();
        action.AddAction(first);
        action.AddAction(second);

        using var reversed = Assert.IsType<Parallel>(action.Reverse());

        Assert.Equal(2, reversed.NumActions);
        var reversedFirst = Assert.IsType<MoveBy>(reversed.GetAction(0));
        Assert.Equal(new Vector3f(-1.0f, -2.0f, -3.0f), reversedFirst.PositionDelta);
        Assert.NotSame(second, reversed.GetAction(1));
        Assert.Same(second, reversed.GetAction(1)!.Reverse());
    }

    [Fact]
    public void Parallel_serializes_inner_actions()
    {
        using var manager = new ActionManager(false);
        using var first = new DelayTime();
        first.SetDuration(1.0f);
        using var second = new DelayTime();
        second.SetDuration(2.0f);
        using var action = new Parallel();
        action.AddAction(first);
        action.AddAction(second);

        using var output = new TestJSONOutputArchive();
        using (output.OpenUnorderedBlock("action"))
            action.SerializeInBlock(output);

        using var input = new TestJSONInputArchive(output.ToJsonString());
        using var restored = new Parallel();
        using (input.OpenUnorderedBlock("action"))
            restored.SerializeInBlock(input);

        Assert.Equal(2, restored.NumActions);
        Assert.Equal(1.0f, Assert.IsType<DelayTime>(restored.GetAction(0)).Duration);
        Assert.Equal(2.0f, Assert.IsType<DelayTime>(restored.GetAction(1)).Duration);
    }

    private sealed class RecordingAction : FiniteTimeAction
    {
        public RecordingState? State { get; private set; }

        protected override ActionState StartAction(RedObject? target)
        {
            State = new RecordingState(this, target);
            return State;
        }
    }

    private sealed class RecordingState : FiniteTimeActionState
    {
        public RecordingState(FiniteTimeAction action, RedObject? target)
            : base(action, target)
        {
        }

        public List<float> Updates { get; } = [];

        public override void Update(float time)
        {
            Updates.Add(time);
        }
    }
}
