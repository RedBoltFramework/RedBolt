namespace RedBolt.Tests;

using System.Collections.Generic;

using RedBolt.Actions;
using RedBolt.Core;
using RedBolt.Scene;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class SequenceTests
{
    [Fact]
    public void Sequence_combines_action_durations()
    {
        using var manager = new ActionManager(false);
        Context.Instance.RegisterSubsystem(manager);
        try
        {
            using var first = new DelayTime();
            first.SetDuration(1.0f);
            using var second = new DelayTime();
            second.SetDuration(2.0f);
            using var action = new Sequence();
            action.SetFirstAction(first);
            action.SetSecondAction(second);

            Assert.Same(first, action.FirstAction);
            Assert.Same(second, action.SecondAction);
            Assert.Equal(3.0f, action.Duration);
        }
        finally
        {
            Context.Instance.RemoveSubsystem<ActionManager>();
        }
    }

    [Fact]
    public void Sequence_runs_actions_in_order()
    {
        using var manager = new ActionManager(false);
        Context.Instance.RegisterSubsystem(manager);
        try
        {
            using var target = new Node();
            using var first = new RecordingAction();
            first.SetDuration(1.0f);
            using var second = new RecordingAction();
            second.SetDuration(2.0f);
            using var action = new Sequence();
            action.SetFirstAction(first);
            action.SetSecondAction(second);

            Assert.NotNull(manager.AddAction(action, target));
            manager.Update(0.0f);
            manager.Update(1.0f);
            manager.Update(1.0f);

            Assert.NotNull(first.State);
            Assert.NotNull(second.State);
            Assert.Equal([0.0f, 1.0f], first.State.Updates);
            Assert.True(first.State.IsStopped);
            Assert.Equal(2, second.State.Updates.Count);
            Assert.Equal(0.0f, second.State.Updates[0]);
            Assert.Equal(0.5f, second.State.Updates[1], 0.00001f);
        }
        finally
        {
            Context.Instance.RemoveSubsystem<ActionManager>();
        }
    }

    [Fact]
    public void Sequence_reverses_action_order()
    {
        using var manager = new ActionManager(false);
        Context.Instance.RegisterSubsystem(manager);
        try
        {
            using var first = new DelayTime();
            first.SetDuration(1.0f);
            using var second = new DelayTime();
            second.SetDuration(2.0f);
            using var action = new Sequence();
            action.SetFirstAction(first);
            action.SetSecondAction(second);

            using var reversed = Assert.IsType<Sequence>(action.Reverse());

            Assert.Same(second, reversed.FirstAction);
            Assert.Same(first, reversed.SecondAction);
        }
        finally
        {
            Context.Instance.RemoveSubsystem<ActionManager>();
        }
    }

    [Fact]
    public void Sequence_serializes_actions()
    {
        using var manager = new ActionManager(false);
        Context.Instance.RegisterSubsystem(manager);
        try
        {
            using var first = new DelayTime();
            first.SetDuration(1.0f);
            using var second = new DelayTime();
            second.SetDuration(2.0f);
            using var action = new Sequence();
            action.SetFirstAction(first);
            action.SetSecondAction(second);

            using var output = new TestJSONOutputArchive();
            using (output.OpenUnorderedBlock("action"))
                action.SerializeInBlock(output);

            using var input = new TestJSONInputArchive(output.ToJsonString());
            using var restored = new Sequence();
            using (input.OpenUnorderedBlock("action"))
                restored.SerializeInBlock(input);

            Assert.Equal(1.0f, Assert.IsType<DelayTime>(restored.FirstAction).Duration);
            Assert.Equal(2.0f, Assert.IsType<DelayTime>(restored.SecondAction).Duration);
        }
        finally
        {
            Context.Instance.RemoveSubsystem<ActionManager>();
        }
    }

    [Fact]
    public void Sequence_delegates_step_to_repeat_forever()
    {
        using var manager = new ActionManager(false);
        Context.Instance.RegisterSubsystem(manager);
        try
        {
            using var target = new Node();
            using var first = new DelayTime();
            first.SetDuration(1.0f);
            using var repeatedAction = new RecordingAction();
            repeatedAction.SetDuration(1.0f);
            using var second = new RepeatForever();
            second.SetInnerAction(repeatedAction);
            using var action = new Sequence();
            action.SetFirstAction(first);
            action.SetSecondAction(second);

            var state = manager.AddAction(action, target);
            Assert.NotNull(state);
            manager.Update(0.0f);
            manager.Update(1.0f);
            manager.Update(1.0f);

            Assert.False(state.IsDone);
            Assert.NotNull(repeatedAction.State);
            Assert.Equal([0.0f], repeatedAction.State.Updates);
        }
        finally
        {
            Context.Instance.RemoveSubsystem<ActionManager>();
        }
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

        public bool IsStopped { get; private set; }

        public override void Update(float time)
        {
            Updates.Add(time);
        }

        public override void Stop()
        {
            IsStopped = true;
            base.Stop();
        }
    }
}
