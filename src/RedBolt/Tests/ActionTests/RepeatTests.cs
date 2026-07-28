namespace RedBolt.Tests;

using System.Collections.Generic;

using RedBolt.Actions;
using RedBolt.Core;
using RedBolt.Scene;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class RepeatTests
{
    [Fact]
    public void Repeat_uses_inner_duration_and_times()
    {
        using var manager = new ActionManager(false);
        Context.Instance.RegisterSubsystem(manager);
        try
        {
            using var innerAction = new DelayTime();
            innerAction.SetDuration(2.0f);
            using var action = new Repeat();
            action.SetInnerAction(innerAction);
            action.SetTimes(3);

            Assert.Same(innerAction, action.InnerAction);
            Assert.Equal(3, action.Times);
            Assert.Equal(6.0f, action.Duration);
        }
        finally
        {
            Context.Instance.RemoveSubsystem<ActionManager>();
        }
    }

    [Fact]
    public void Repeat_restarts_inner_action()
    {
        using var manager = new ActionManager(false);
        Context.Instance.RegisterSubsystem(manager);
        try
        {
            using var target = new Node();
            using var innerAction = new RecordingAction();
            innerAction.SetDuration(1.0f);
            using var action = new Repeat();
            action.SetInnerAction(innerAction);
            action.SetTimes(3);

            Assert.NotNull(manager.AddAction(action, target));
            manager.Update(0.0f);
            manager.Update(1.0f);
            manager.Update(0.5f);

            Assert.Equal(2, innerAction.States.Count);
            Assert.Equal([0.0f, 1.0f, 1.0f], innerAction.States[0].Updates);
            Assert.True(innerAction.States[0].IsStopped);
            Assert.True(innerAction.States[0].IsDisposed);
            Assert.Single(innerAction.States[1].Updates);
            Assert.Equal(0.5f, innerAction.States[1].Updates[0], 0.00001f);
        }
        finally
        {
            Context.Instance.RemoveSubsystem<ActionManager>();
        }
    }

    [Fact]
    public void Repeat_reverses_inner_action()
    {
        using var manager = new ActionManager(false);
        Context.Instance.RegisterSubsystem(manager);
        try
        {
            using var innerAction = new MoveBy();
            innerAction.SetDuration(2.0f);
            innerAction.SetPositionDelta(new Vector3f(1.0f, 2.0f, 3.0f));
            using var action = new Repeat();
            action.SetInnerAction(innerAction);
            action.SetTimes(3);

            using var reversed = Assert.IsType<Repeat>(action.Reverse());

            Assert.Equal(3, reversed.Times);
            var reversedInner = Assert.IsType<MoveBy>(reversed.InnerAction);
            Assert.Equal(new Vector3f(-1.0f, -2.0f, -3.0f), reversedInner.PositionDelta);
        }
        finally
        {
            Context.Instance.RemoveSubsystem<ActionManager>();
        }
    }

    [Fact]
    public void Repeat_serializes_inner_action()
    {
        using var manager = new ActionManager(false);
        Context.Instance.RegisterSubsystem(manager);
        try
        {
            using var innerAction = new DelayTime();
            innerAction.SetDuration(2.0f);
            using var action = new Repeat();
            action.SetInnerAction(innerAction);

            using var output = new TestJSONOutputArchive();
            using (output.OpenUnorderedBlock("action"))
                action.SerializeInBlock(output);

            using var input = new TestJSONInputArchive(output.ToJsonString());
            using var restored = new Repeat();
            using (input.OpenUnorderedBlock("action"))
                restored.SerializeInBlock(input);

            Assert.Equal(2.0f, Assert.IsType<DelayTime>(restored.InnerAction).Duration);
        }
        finally
        {
            Context.Instance.RemoveSubsystem<ActionManager>();
        }
    }

    [Fact]
    public void Repeat_forever_uses_maximum_duration()
    {
        using var manager = new ActionManager(false);
        Context.Instance.RegisterSubsystem(manager);
        try
        {
            using var innerAction = new DelayTime();
            innerAction.SetDuration(2.0f);
            using var action = new RepeatForever();
            action.SetInnerAction(innerAction);

            Assert.Same(innerAction, action.InnerAction);
            Assert.Equal(float.MaxValue, action.Duration);
        }
        finally
        {
            Context.Instance.RemoveSubsystem<ActionManager>();
        }
    }

    [Fact]
    public void Repeat_forever_restarts_inner_action()
    {
        using var manager = new ActionManager(false);
        Context.Instance.RegisterSubsystem(manager);
        try
        {
            using var target = new Node();
            using var innerAction = new RecordingAction();
            innerAction.SetDuration(1.0f);
            using var action = new RepeatForever();
            action.SetInnerAction(innerAction);

            var state = manager.AddAction(action, target);
            Assert.NotNull(state);
            manager.Update(0.0f);
            manager.Update(1.5f);

            Assert.False(state.IsDone);
            Assert.Equal(2, innerAction.States.Count);
            Assert.Equal([0.0f, 1.0f], innerAction.States[0].Updates);
            Assert.False(innerAction.States[0].IsStopped);
            Assert.True(innerAction.States[0].IsDisposed);
            Assert.Equal([0.0f, 0.0f], innerAction.States[1].Updates);
        }
        finally
        {
            Context.Instance.RemoveSubsystem<ActionManager>();
        }
    }

    [Fact]
    public void Repeat_forever_reverses_inner_action()
    {
        using var manager = new ActionManager(false);
        Context.Instance.RegisterSubsystem(manager);
        try
        {
            using var innerAction = new MoveBy();
            innerAction.SetDuration(2.0f);
            innerAction.SetPositionDelta(new Vector3f(1.0f, 2.0f, 3.0f));
            using var action = new RepeatForever();
            action.SetInnerAction(innerAction);

            using var reversed = Assert.IsType<RepeatForever>(action.Reverse());

            var reversedInner = Assert.IsType<MoveBy>(reversed.InnerAction);
            Assert.Equal(new Vector3f(-1.0f, -2.0f, -3.0f), reversedInner.PositionDelta);
        }
        finally
        {
            Context.Instance.RemoveSubsystem<ActionManager>();
        }
    }

    [Fact]
    public void Repeat_forever_serializes_inner_action()
    {
        using var manager = new ActionManager(false);
        Context.Instance.RegisterSubsystem(manager);
        try
        {
            using var innerAction = new DelayTime();
            innerAction.SetDuration(2.0f);
            using var action = new RepeatForever();
            action.SetInnerAction(innerAction);

            using var output = new TestJSONOutputArchive();
            using (output.OpenUnorderedBlock("action"))
                action.SerializeInBlock(output);

            using var input = new TestJSONInputArchive(output.ToJsonString());
            using var restored = new RepeatForever();
            using (input.OpenUnorderedBlock("action"))
                restored.SerializeInBlock(input);

            Assert.Equal(2.0f, Assert.IsType<DelayTime>(restored.InnerAction).Duration);
        }
        finally
        {
            Context.Instance.RemoveSubsystem<ActionManager>();
        }
    }

    private sealed class RecordingAction : FiniteTimeAction
    {
        public List<RecordingState> States { get; } = [];

        protected override ActionState StartAction(RedObject? target)
        {
            var state = new RecordingState(this, target);
            States.Add(state);
            return state;
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

        public bool IsDisposed { get; private set; }

        public override void Dispose()
        {
            IsDisposed = true;
            base.Dispose();
        }

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
