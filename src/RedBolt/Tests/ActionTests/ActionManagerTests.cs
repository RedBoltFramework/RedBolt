namespace RedBolt.Tests;

using RedBolt.Actions;
using RedBolt.Core;
using RedBolt.Scene;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class ActionManagerTests
{
    [Fact]
    public void Finite_time_action_uses_first_tick_and_completes()
    {
        using var manager = new ActionManager(false);
        using var target = new Node();
        using var action = new RecordingAction();
        action.SetDuration(2.0f);

        var state = manager.AddAction(action, target);

        Assert.NotNull(state);
        Assert.Equal(1, manager.GetNumActions(target));

        manager.Update(0.5f);
        Assert.Equal([0.0f], action.State!.Updates);

        manager.Update(0.5f);
        Assert.Equal([0.0f, 0.25f], action.State.Updates);

        manager.Update(2.5f);
        Assert.Equal([0.0f, 0.25f, 1.0f], action.State.Updates);
        Assert.Null(action.State.Target);
        Assert.Same(target, action.State.OriginalTarget);
        Assert.Equal(0, manager.GetNumActions(target));
    }

    [Fact]
    public void Duplicate_action_is_rejected_for_same_target()
    {
        using var manager = new ActionManager(false);
        using var target = new Node();
        using var action = new RecordingAction();

        Assert.NotNull(manager.AddAction(action, target));
        Assert.Null(manager.AddAction(action, target));
        Assert.Equal(1, manager.GetNumActions(target));
    }

    [Fact]
    public void Cancel_all_actions_does_not_complete_them()
    {
        using var manager = new ActionManager(false);
        using var target = new Node();
        using var action = new RecordingAction();
        action.SetDuration(2.0f);
        manager.AddAction(action, target);

        manager.CancelAllActions();

        Assert.Empty(action.State!.Updates);
        Assert.Equal(0, manager.GetNumActions(target));
    }

    [Fact]
    public void Complete_all_actions_steps_to_end()
    {
        using var manager = new ActionManager(false);
        using var target = new Node();
        using var action = new RecordingAction();
        action.SetDuration(2.0f);
        manager.AddAction(action, target);

        manager.CompleteAllActions();

        Assert.Equal([0.0f, 1.0f], action.State!.Updates);
        Assert.Equal(0, manager.GetNumActions(target));
    }

    [Fact]
    public void Duration_is_clamped_and_reverse_round_trips()
    {
        using var action = new FiniteTimeAction();
        action.SetDuration(0.0f);

        Assert.Equal(1.1920929E-07f, action.Duration);

        using var reverse = action.Reverse();
        Assert.Same(action, reverse.Reverse());
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
