using RedBolt;

namespace RedBolt.Tests;

public sealed class StateManagerTests
{
    [Fact]
    public void Application_state_uses_int_viewport_counts_and_indices()
    {
        using var state = new TestState();
        var count = 2;
        var index = 1;

        state.SetNumViewports(count);
        state.SetViewport(index, null);

        Assert.False(state.IsActive);
        Assert.True(state.IsMouseVisible);
        Assert.False(state.IsMouseGrabbed);
        Assert.Equal(MouseMode.Free, state.MouseMode);
        Assert.Null(state.GetViewport(index));
        Assert.Null(state.GetViewportForScene(null, index));
        Assert.Equal(new Color(0.0f, 0.0f, 0.0f), state.DefaultFogColor);
    }

    [Fact]
    public void State_manager_copies_queued_bundle_values()
    {
        using var engine = new Engine();
        var stateManager = engine.GetSubsystem<StateManager>()!;
        stateManager.Reset();
        stateManager.FadeInDuration = 0.0f;
        stateManager.FadeOutDuration = 0.0f;
        using var initialState = new TestState();
        using var targetState = new TestState();

        stateManager.EnqueueState(initialState);
        stateManager.Update(1.0f);

        using var bundle = new StringVariantMap
        {
            ["Value"] = new Variant(1)
        };
        stateManager.EnqueueState(targetState, bundle);
        bundle["Value"].Dispose();
        bundle["Value"] = new Variant(2);

        stateManager.Update(1.0f);

        Assert.Same(targetState, stateManager.State);
        Assert.Equal(targetState.GetTypeNameHash(), stateManager.TargetState);
        Assert.Equal(MathDefs.Epsilon, stateManager.FadeInDuration);
        Assert.Equal(MathDefs.Epsilon, stateManager.FadeOutDuration);
        Assert.Equal(1, targetState.Value);
        stateManager.Reset();
    }

    [Fact]
    public void State_manager_transition_and_reset_events_match_cpp_order()
    {
        using var engine = new Engine();
        var stateManager = engine.GetSubsystem<StateManager>()!;
        stateManager.Reset();
        using var state = new TestState();
        var stateType = state.GetTypeNameHash();
        var events = new List<(string Name, StringHash From, StringHash To)>();
        using var started = stateManager.SubscribeToEvent<StateManagerEvents.StateTransitionStartedEvent>(
            (ref StateManagerEvents.StateTransitionStartedEvent eventData) => events.Add(("Started", eventData.From, eventData.To)));
        using var entering = stateManager.SubscribeToEvent<StateManagerEvents.EnteringApplicationStateEvent>(
            (ref StateManagerEvents.EnteringApplicationStateEvent eventData) => events.Add(("Entering", eventData.From, eventData.To)));
        using var leaving = stateManager.SubscribeToEvent<StateManagerEvents.LeavingApplicationStateEvent>(
            (ref StateManagerEvents.LeavingApplicationStateEvent eventData) => events.Add(("Leaving", eventData.From, eventData.To)));
        using var complete = stateManager.SubscribeToEvent<StateManagerEvents.StateTransitionCompleteEvent>(
            (ref StateManagerEvents.StateTransitionCompleteEvent eventData) => events.Add(("Complete", eventData.From, eventData.To)));

        stateManager.EnqueueState(state);
        stateManager.Update(1.0f);

        Assert.Equal(
        [
            ("Started", StringHash.Empty, stateType),
            ("Entering", StringHash.Empty, stateType),
            ("Complete", StringHash.Empty, stateType)
        ], events);

        events.Clear();
        stateManager.Reset();

        Assert.Equal(
        [
            ("Started", stateType, StringHash.Empty),
            ("Leaving", stateType, StringHash.Empty),
            ("Complete", stateType, StringHash.Empty)
        ], events);
    }

    private sealed class TestState : ApplicationState
    {
        public int Value { get; private set; }

        public override void Activate(StringVariantMap bundle)
        {
            if (bundle.TryGetValue("Value", out var value))
                Value = value.GetInt();
        }
    }
}
