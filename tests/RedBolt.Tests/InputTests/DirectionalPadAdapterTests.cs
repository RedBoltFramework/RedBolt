using RedBolt;

namespace RedBolt.Tests;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class DirectionalPadAdapterTests
{
    private readonly HeadlessEngineFixture _fixture;

    public DirectionalPadAdapterTests(HeadlessEngineFixture fixture)
    {
        this._fixture = fixture;
    }

    private RedBolt.Input Input => _fixture.Input;

    [Fact]
    public void Keyboard_mapping_to_dpad_works()
    {
        using var adapter = new DirectionalPadAdapter(Input);
        adapter.SetEnabled(true);
        Input.SetKey(Key.W, Scancode.W, true);
        Input.SetKey(Key.D, Scancode.D, true);

        Assert.True(adapter.GetScancodeDown(Scancode.Up));
        Assert.True(adapter.GetScancodeDown(Scancode.Right));
        Assert.True(adapter.GetKeyDown(Key.Up));
        Assert.True(adapter.GetKeyDown(Key.Right));

        Input.SetKey(Key.W, Scancode.W, false);
        Input.SetKey(Key.D, Scancode.D, false);
    }

    [Fact]
    public void Joystick_thresholds_work()
    {
        using var adapter = new DirectionalPadAdapter(Input);
        adapter.SetEnabled(true);
        adapter.SetAxisUpperThreshold(0.6f);
        adapter.SetAxisLowerThreshold(0.4f);
        Input.SetControllerAxis(ControllerAxis.LeftX, 0.7f);
        Input.SetControllerAxis(ControllerAxis.LeftY, -0.8f);

        Assert.True(adapter.GetScancodeDown(Scancode.Right));
        Assert.True(adapter.GetScancodeDown(Scancode.Up));

        Input.SetControllerAxis(ControllerAxis.LeftX, 0.0f);
        Input.SetControllerAxis(ControllerAxis.LeftY, 0.0f);
    }

    [Fact]
    public void Hat_and_external_direction_work()
    {
        using var adapter = new DirectionalPadAdapter(Input);
        adapter.SetEnabled(true);
        Input.SetHatPosition(HatPosition.LeftDown);
        adapter.SetExternalDirection(new Vector2f(1.0f, -1.0f));

        Assert.True(adapter.GetScancodeDown(Scancode.Left));
        Assert.True(adapter.GetScancodeDown(Scancode.Down));
        Assert.True(adapter.GetScancodeDown(Scancode.Right));
        Assert.True(adapter.GetScancodeDown(Scancode.Up));

        Input.SetHatPosition(HatPosition.Center);
    }

    [Fact]
    public void Configuration_roundtrips()
    {
        using var adapter = new DirectionalPadAdapter();
        adapter.SetEnabled(true);
        adapter.SetSubscriptionMask(DirectionalPadAdapterMask.Joystick | DirectionalPadAdapterMask.KeyRepeat);
        adapter.SetAxisUpperThreshold(0.75f);
        adapter.SetAxisLowerThreshold(0.25f);
        adapter.SetRepeatDelay(0.4f);
        adapter.SetRepeatInterval(0.05f);

        Assert.True(adapter.IsEnabled());
        Assert.Equal(DirectionalPadAdapterMask.Joystick | DirectionalPadAdapterMask.KeyRepeat, adapter.GetSubscriptionMask());
        Assert.True(MathDefs.ApproximatelyEquivalent(adapter.GetAxisUpperThreshold(), 0.75f));
        Assert.True(MathDefs.ApproximatelyEquivalent(adapter.GetAxisLowerThreshold(), 0.25f));
        Assert.True(MathDefs.ApproximatelyEquivalent(adapter.GetRepeatDelay(), 0.4f));
        Assert.True(MathDefs.ApproximatelyEquivalent(adapter.GetRepeatInterval(), 0.05f));
    }

    [Fact]
    public void Axis_hysteresis_and_last_source_release_work()
    {
        using var adapter = new DirectionalPadAdapter(Input);
        adapter.SetEnabled(true);
        Input.SetControllerAxis(ControllerAxis.LeftX, 0.7f);
        Assert.True(adapter.GetScancodeDown(Scancode.Right));
        Input.SetControllerAxis(ControllerAxis.LeftX, 0.5f);
        Assert.True(adapter.GetScancodeDown(Scancode.Right));

        Input.SetKey(Key.D, Scancode.D, true);
        Input.SetControllerAxis(ControllerAxis.LeftX, 0.2f);
        Assert.True(adapter.GetScancodeDown(Scancode.Right));
        Input.SetKey(Key.D, Scancode.D, false);
        Assert.False(adapter.GetScancodeDown(Scancode.Right));
    }

    [Fact]
    public void Synthesized_transitions_and_repeat_match_upstream()
    {
        using var adapter = new DirectionalPadAdapter(Input);
        adapter.SetRepeatDelay(0.2f);
        adapter.SetRepeatInterval(0.1f);
        adapter.SetEnabled(true);
        var downs = new List<InputEvents.KeyDownEvent>();
        var ups = new List<InputEvents.KeyUpEvent>();
        using var downSubscription = Context.Instance.SubscribeToEvent<InputEvents.KeyDownEvent>(adapter, (ref InputEvents.KeyDownEvent value) => downs.Add(value));
        using var upSubscription = Context.Instance.SubscribeToEvent<InputEvents.KeyUpEvent>(adapter, (ref InputEvents.KeyUpEvent value) => ups.Add(value));

        Input.SetKey(Key.W, Scancode.W, true);
        Assert.Contains(downs, value => value.Scancode == Scancode.Up && !value.Repeat);
        var update = new CoreEvents.UpdateEvent { TimeStep = 0.25f };
        Context.Instance.SendEvent(ref update);
        Assert.Contains(downs, value => value.Scancode == Scancode.Up && value.Repeat);
        Input.SetKey(Key.W, Scancode.W, false);
        Assert.Contains(ups, value => value.Scancode == Scancode.Up);
    }
}
