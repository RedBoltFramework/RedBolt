namespace RedBolt.Tests;

using RedBolt.Actions;
using RedBolt.Core;

public sealed class ActionInstantTests
{
    [Fact]
    public void Action_has_epsilon_duration_and_reverses()
    {
        using var action = new ActionInstant();

        Assert.Equal(1.1920929E-07f, action.Duration);
        using var reversed = action.Reverse();
        Assert.IsType<ActionInstant>(reversed);
    }

    [Fact]
    public void State_is_done_and_steps_at_end()
    {
        using var action = new ActionInstant();
        using var target = new BaseAction();
        using var state = new RecordingInstantState(action, target);

        Assert.True(state.IsDone);
        state.CallStep(0.25f);
        Assert.Equal([1.0f], state.Updates);
    }

    private sealed class RecordingInstantState : ActionInstantState
    {
        public RecordingInstantState(ActionInstant action, RedObject? target)
            : base(action, target)
        {
        }

        public List<float> Updates { get; } = [];

        public void CallStep(float dt)
        {
            Step(dt);
        }

        public override void Update(float time)
        {
            Updates.Add(time);
        }
    }
}
