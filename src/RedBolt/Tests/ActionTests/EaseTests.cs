namespace RedBolt.Tests;

using RedBolt.Actions;
using RedBolt.Core;
using RedBolt.IO;
using RedBolt.Math;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class EaseTests
{
    [Fact]
    public void Action_ease_uses_inner_duration_and_identity_easing()
    {
        using var manager = new ActionManager(false);
        using var innerAction = new DelayTime();
        innerAction.SetDuration(2.0f);
        using var action = new ActionEase();

        Assert.Equal(1.1920929E-07f, action.Duration);
        action.SetInnerAction(innerAction);

        Assert.Same(innerAction, action.InnerAction);
        Assert.Equal(2.0f, action.Duration);
        Assert.Equal(0.25f, action.Ease(0.25f));
    }

    [Fact]
    public void Action_ease_reverses_inner_action()
    {
        using var manager = new ActionManager(false);
        using var innerAction = new DelayTime();
        innerAction.SetDuration(2.0f);
        using var action = new ActionEase();
        action.SetInnerAction(innerAction);

        using var reversed = Assert.IsType<ActionEase>(action.Reverse());

        Assert.NotNull(reversed.InnerAction);
        Assert.Equal(1.1920929E-07f, reversed.Duration);
        Assert.NotSame(innerAction, reversed.InnerAction);
        Assert.Same(innerAction, reversed.InnerAction.Reverse());
    }

    [Fact]
    public void Action_ease_serializes_inner_action()
    {
        using var manager = new ActionManager(false);
        using var innerAction = new DelayTime();
        innerAction.SetDuration(2.0f);
        using var action = new ActionEase();
        action.SetInnerAction(innerAction);

        using var output = new TestJSONOutputArchive();
        using (output.OpenUnorderedBlock("action"))
            action.SerializeInBlock(output);

        using var input = new TestJSONInputArchive(output.ToJsonString());
        using var restored = new ActionEase();
        using (input.OpenUnorderedBlock("action"))
            restored.SerializeInBlock(input);

        var restoredInner = Assert.IsType<DelayTime>(restored.InnerAction);
        Assert.Equal(2.0f, restoredInner.Duration);
    }

    [Fact]
    public void Ease_elastic_serializes_period()
    {
        using var manager = new ActionManager(false);
        using var action = new EaseElastic();

        Assert.Equal(0.3f, action.Period);
        action.SetPeriod(0.75f);

        using var output = new TestJSONOutputArchive();
        using (output.OpenUnorderedBlock("action"))
            action.SerializeInBlock(output);

        using var input = new TestJSONInputArchive(output.ToJsonString());
        using var restored = new EaseElastic();
        using (input.OpenUnorderedBlock("action"))
            restored.SerializeInBlock(input);

        Assert.Equal(0.75f, restored.Period);
    }

    [Fact]
    public void Back_in_and_out_reverse_each_other()
    {
        using var manager = new ActionManager(false);
        Context.Instance.RegisterSubsystem(manager);
        try
        {
            using var innerAction = new RecordingAction();
            innerAction.SetDuration(1.0f);
            using var action = new EaseBackIn();
            action.SetInnerAction(innerAction);

            Assert.Equal(EaseMath.BackIn(0.5f), action.Ease(0.5f));

            using var reversed = Assert.IsType<EaseBackOut>(action.Reverse());
            Assert.Equal(EaseMath.BackOut(0.5f), reversed.Ease(0.5f));
            Assert.NotNull(reversed.InnerAction);
            Assert.Same(innerAction, reversed.InnerAction.Reverse());

            using var roundTrip = Assert.IsType<EaseBackIn>(reversed.Reverse());
            Assert.NotNull(roundTrip.InnerAction);
            Assert.Same(innerAction, roundTrip.InnerAction);
        }
        finally
        {
            Context.Instance.RemoveSubsystem<ActionManager>();
        }
    }

    [Fact]
    public void Back_in_updates_inner_action_with_eased_time()
    {
        using var manager = new ActionManager(false);
        Context.Instance.RegisterSubsystem(manager);
        try
        {
            using var target = new BaseAction();
            using var innerAction = new RecordingAction();
            innerAction.SetDuration(1.0f);
            using var action = new EaseBackIn();
            action.SetInnerAction(innerAction);

            Assert.NotNull(manager.AddAction(action, target));
            manager.Update(0.0f);
            manager.Update(0.5f);

            Assert.NotNull(innerAction.State);
            Assert.Equal([0.0f, EaseMath.BackIn(0.5f)], innerAction.State.Updates);
        }
        finally
        {
            Context.Instance.RemoveSubsystem<ActionManager>();
        }
    }

    [Fact]
    public void Back_in_out_reverses_itself()
    {
        using var innerAction = new RecordingAction();
        innerAction.SetDuration(1.0f);
        using var action = new EaseBackInOut();
        action.SetInnerAction(innerAction);

        Assert.Equal(EaseMath.BackInOut(0.5f), action.Ease(0.5f));

        using var reversed = Assert.IsType<EaseBackInOut>(action.Reverse());
        Assert.NotNull(reversed.InnerAction);
        Assert.Same(innerAction, reversed.InnerAction.Reverse());
    }

    [Fact]
    public void Back_in_out_updates_inner_action_with_eased_time()
    {
        using var manager = new ActionManager(false);
        using var target = new BaseAction();
        using var innerAction = new RecordingAction();
        innerAction.SetDuration(1.0f);
        using var action = new EaseBackInOut();
        action.SetInnerAction(innerAction);

        Assert.NotNull(manager.AddAction(action, target));
        manager.Update(0.0f);
        manager.Update(0.25f);

        Assert.NotNull(innerAction.State);
        Assert.Equal([0.0f, EaseMath.BackInOut(0.25f)], innerAction.State.Updates);
    }

    [Fact]
    public void Bounce_in_and_out_reverse_each_other()
    {
        using var innerAction = new RecordingAction();
        innerAction.SetDuration(1.0f);
        using var action = new EaseBounceOut();
        action.SetInnerAction(innerAction);

        Assert.Equal(EaseMath.BounceOut(0.5f), action.Ease(0.5f));

        using var reversed = Assert.IsType<EaseBounceIn>(action.Reverse());
        Assert.Equal(EaseMath.BounceIn(0.5f), reversed.Ease(0.5f));
        Assert.NotNull(reversed.InnerAction);
        Assert.Same(innerAction, reversed.InnerAction.Reverse());
    }

    [Fact]
    public void Bounce_out_updates_inner_action_with_eased_time()
    {
        using var manager = new ActionManager(false);
        using var target = new BaseAction();
        using var innerAction = new RecordingAction();
        innerAction.SetDuration(1.0f);
        using var action = new EaseBounceOut();
        action.SetInnerAction(innerAction);

        Assert.NotNull(manager.AddAction(action, target));
        manager.Update(0.0f);
        manager.Update(0.5f);

        Assert.NotNull(innerAction.State);
        Assert.Equal([0.0f, EaseMath.BounceOut(0.5f)], innerAction.State.Updates);
    }

    [Fact]
    public void Bounce_in_out_reverses_itself()
    {
        using var innerAction = new RecordingAction();
        innerAction.SetDuration(1.0f);
        using var action = new EaseBounceInOut();
        action.SetInnerAction(innerAction);

        Assert.Equal(EaseMath.BounceInOut(0.5f), action.Ease(0.5f));

        using var reversed = Assert.IsType<EaseBounceInOut>(action.Reverse());
        Assert.NotNull(reversed.InnerAction);
        Assert.Same(innerAction, reversed.InnerAction.Reverse());
    }

    [Fact]
    public void Bounce_in_out_updates_inner_action_with_eased_time()
    {
        using var manager = new ActionManager(false);
        using var target = new BaseAction();
        using var innerAction = new RecordingAction();
        innerAction.SetDuration(1.0f);
        using var action = new EaseBounceInOut();
        action.SetInnerAction(innerAction);

        Assert.NotNull(manager.AddAction(action, target));
        manager.Update(0.0f);
        manager.Update(0.25f);

        Assert.NotNull(innerAction.State);
        Assert.Equal([0.0f, EaseMath.BounceInOut(0.25f)], innerAction.State.Updates);
    }

    [Fact]
    public void Sine_in_and_out_reverse_each_other()
    {
        using var innerAction = new RecordingAction();
        innerAction.SetDuration(1.0f);
        using var action = new EaseSineOut();
        action.SetInnerAction(innerAction);

        Assert.Equal(EaseMath.SineOut(0.5f), action.Ease(0.5f));

        using var reversed = Assert.IsType<EaseSineIn>(action.Reverse());
        Assert.Equal(EaseMath.SineIn(0.5f), reversed.Ease(0.5f));
        Assert.NotNull(reversed.InnerAction);
        Assert.Same(innerAction, reversed.InnerAction.Reverse());
    }

    [Fact]
    public void Sine_out_updates_inner_action_with_eased_time()
    {
        using var manager = new ActionManager(false);
        using var target = new BaseAction();
        using var innerAction = new RecordingAction();
        innerAction.SetDuration(1.0f);
        using var action = new EaseSineOut();
        action.SetInnerAction(innerAction);

        Assert.NotNull(manager.AddAction(action, target));
        manager.Update(0.0f);
        manager.Update(0.5f);

        Assert.NotNull(innerAction.State);
        Assert.Equal([0.0f, EaseMath.SineOut(0.5f)], innerAction.State.Updates);
    }

    [Fact]
    public void Sine_in_out_reverses_itself()
    {
        using var innerAction = new RecordingAction();
        innerAction.SetDuration(1.0f);
        using var action = new EaseSineInOut();
        action.SetInnerAction(innerAction);

        Assert.Equal(EaseMath.SineInOut(0.5f), action.Ease(0.5f));

        using var reversed = Assert.IsType<EaseSineInOut>(action.Reverse());
        Assert.NotNull(reversed.InnerAction);
        Assert.Same(innerAction, reversed.InnerAction.Reverse());
    }

    [Fact]
    public void Sine_in_out_updates_inner_action_with_eased_time()
    {
        using var manager = new ActionManager(false);
        using var target = new BaseAction();
        using var innerAction = new RecordingAction();
        innerAction.SetDuration(1.0f);
        using var action = new EaseSineInOut();
        action.SetInnerAction(innerAction);

        Assert.NotNull(manager.AddAction(action, target));
        manager.Update(0.0f);
        manager.Update(0.25f);

        Assert.NotNull(innerAction.State);
        Assert.Equal([0.0f, EaseMath.SineInOut(0.25f)], innerAction.State.Updates);
    }

    [Fact]
    public void Exponential_in_and_out_reverse_each_other()
    {
        using var innerAction = new RecordingAction();
        innerAction.SetDuration(1.0f);
        using var action = new EaseExponentialOut();
        action.SetInnerAction(innerAction);

        Assert.Equal(EaseMath.ExponentialOut(0.5f), action.Ease(0.5f));

        using var reversed = Assert.IsType<EaseExponentialIn>(action.Reverse());
        Assert.Equal(EaseMath.ExponentialIn(0.5f), reversed.Ease(0.5f));
        Assert.NotNull(reversed.InnerAction);
        Assert.Same(innerAction, reversed.InnerAction.Reverse());
    }

    [Fact]
    public void Exponential_out_updates_inner_action_with_eased_time()
    {
        using var manager = new ActionManager(false);
        using var target = new BaseAction();
        using var innerAction = new RecordingAction();
        innerAction.SetDuration(1.0f);
        using var action = new EaseExponentialOut();
        action.SetInnerAction(innerAction);

        Assert.NotNull(manager.AddAction(action, target));
        manager.Update(0.0f);
        manager.Update(0.5f);

        Assert.NotNull(innerAction.State);
        Assert.Equal([0.0f, EaseMath.ExponentialOut(0.5f)], innerAction.State.Updates);
    }

    [Fact]
    public void Exponential_in_out_reverses_itself()
    {
        using var innerAction = new RecordingAction();
        innerAction.SetDuration(1.0f);
        using var action = new EaseExponentialInOut();
        action.SetInnerAction(innerAction);

        Assert.Equal(EaseMath.ExponentialInOut(0.5f), action.Ease(0.5f));

        using var reversed = Assert.IsType<EaseExponentialInOut>(action.Reverse());
        Assert.NotNull(reversed.InnerAction);
        Assert.Same(innerAction, reversed.InnerAction.Reverse());
    }

    [Fact]
    public void Exponential_in_out_updates_inner_action_with_eased_time()
    {
        using var manager = new ActionManager(false);
        using var target = new BaseAction();
        using var innerAction = new RecordingAction();
        innerAction.SetDuration(1.0f);
        using var action = new EaseExponentialInOut();
        action.SetInnerAction(innerAction);

        Assert.NotNull(manager.AddAction(action, target));
        manager.Update(0.0f);
        manager.Update(0.25f);

        Assert.NotNull(innerAction.State);
        Assert.Equal([EaseMath.ExponentialInOut(0.0f), EaseMath.ExponentialInOut(0.25f)], innerAction.State.Updates);
    }

    [Fact]
    public void Elastic_in_and_out_reverse_each_other()
    {
        using var innerAction = new RecordingAction();
        innerAction.SetDuration(1.0f);
        using var action = new EaseElasticIn();
        action.SetInnerAction(innerAction);
        action.SetPeriod(0.75f);

        Assert.Equal(EaseMath.ElasticIn(0.5f, 0.75f), action.Ease(0.5f));

        using var reversed = Assert.IsType<EaseElasticOut>(action.Reverse());
        Assert.Equal(0.75f, reversed.Period);
        Assert.Equal(EaseMath.ElasticOut(0.5f, 0.75f), reversed.Ease(0.5f));
        Assert.NotNull(reversed.InnerAction);
        Assert.Same(innerAction, reversed.InnerAction.Reverse());

        using var roundTrip = Assert.IsType<EaseElasticIn>(reversed.Reverse());
        Assert.Equal(0.75f, roundTrip.Period);
        Assert.NotNull(roundTrip.InnerAction);
        Assert.Same(innerAction, roundTrip.InnerAction);
    }

    [Fact]
    public void Elastic_in_updates_inner_action_with_eased_time()
    {
        using var manager = new ActionManager(false);
        using var target = new BaseAction();
        using var innerAction = new RecordingAction();
        innerAction.SetDuration(1.0f);
        using var action = new EaseElasticIn();
        action.SetInnerAction(innerAction);

        Assert.NotNull(manager.AddAction(action, target));
        manager.Update(0.0f);
        manager.Update(0.5f);

        Assert.NotNull(innerAction.State);
        Assert.Equal([0.0f, EaseMath.ElasticIn(0.5f, 0.3f)], innerAction.State.Updates);
    }

    [Fact]
    public void Elastic_in_out_reverses_itself()
    {
        using var innerAction = new RecordingAction();
        innerAction.SetDuration(1.0f);
        using var action = new EaseElasticInOut();
        action.SetInnerAction(innerAction);
        action.SetPeriod(0.75f);

        Assert.Equal(EaseMath.ElasticInOut(0.5f, 0.75f), action.Ease(0.5f));

        using var reversed = Assert.IsType<EaseElasticInOut>(action.Reverse());
        Assert.Equal(0.75f, reversed.Period);
        Assert.NotNull(reversed.InnerAction);
        Assert.Same(innerAction, reversed.InnerAction.Reverse());
    }

    [Fact]
    public void Elastic_in_out_updates_inner_action_with_eased_time()
    {
        using var manager = new ActionManager(false);
        using var target = new BaseAction();
        using var innerAction = new RecordingAction();
        innerAction.SetDuration(1.0f);
        using var action = new EaseElasticInOut();
        action.SetInnerAction(innerAction);

        Assert.NotNull(manager.AddAction(action, target));
        manager.Update(0.0f);
        manager.Update(0.25f);

        Assert.NotNull(innerAction.State);
        Assert.Equal([0.0f, EaseMath.ElasticInOut(0.25f, 0.3f)], innerAction.State.Updates);
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
