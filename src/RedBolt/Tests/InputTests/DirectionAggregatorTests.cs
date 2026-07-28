namespace RedBolt.Tests;

using System.Reflection;

using RedBolt;
using RedBolt.Input;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class DirectionAggregatorTests
{
    private readonly HeadlessEngineFixture _fixture;

    public DirectionAggregatorTests(HeadlessEngineFixture fixture)
    {
        this._fixture = fixture;
    }

    private Input Input => _fixture.Input;

    [Fact]
    public void Matches_native_noncopyable_class_and_pod_axis_state_kinds()
    {
        Assert.True(typeof(DirectionAggregator).IsClass);
        Assert.False(typeof(DirectionAggregator).IsSealed);
        Assert.False(typeof(ICloneable).IsAssignableFrom(typeof(DirectionAggregator)));

        var axisState = typeof(DirectionAggregator).GetNestedType("AxisState", BindingFlags.NonPublic);
        Assert.NotNull(axisState);
        Assert.True(axisState.IsValueType);
        Assert.All(axisState.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic),
            field => Assert.True(field.FieldType.IsValueType));
    }

    [Fact]
    public void Keyboard_direction_works()
    {
        using var aggregator = new DirectionAggregator();
        aggregator.SetEnabled(true);
        Input.SetKey(Key.W, Scancode.W, true);
        Input.SetKey(Key.D, Scancode.D, true);

        Assert.True(aggregator.GetDirection().ApproximatelyEquivalent(new Vector2f(1.0f, -1.0f)));

        Input.SetKey(Key.W, Scancode.W, false);
        Input.SetKey(Key.D, Scancode.D, false);
    }

    [Fact]
    public void Joystick_and_dead_zone_work()
    {
        using var aggregator = new DirectionAggregator();
        aggregator.SetEnabled(true);
        aggregator.SetDeadZone(0.2f);
        Input.SetControllerAxis(ControllerAxis.LeftX, 0.1f);
        Input.SetControllerAxis(ControllerAxis.LeftY, -0.5f);

        Assert.True(aggregator.GetDirection().ApproximatelyEquivalent(new Vector2f(0.0f, -0.375f), 0.001f));

        Input.SetControllerAxis(ControllerAxis.LeftX, 0.0f);
        Input.SetControllerAxis(ControllerAxis.LeftY, 0.0f);
    }

    [Fact]
    public void Simultaneous_keys_are_averaged_independently()
    {
        using var aggregator = new DirectionAggregator();
        aggregator.SetEnabled(true);

        Input.SetKey(Key.Down, Scancode.Down, true);
        Input.SetKey(Key.S, Scancode.S, true);
        Assert.True(aggregator.GetDirection().ApproximatelyEquivalent(new Vector2f(0.0f, 1.0f)));
        Input.SetKey(Key.Down, Scancode.Down, false);
        Assert.True(aggregator.GetDirection().ApproximatelyEquivalent(new Vector2f(0.0f, 1.0f)));
        Input.SetKey(Key.S, Scancode.S, false);
        Assert.True(aggregator.GetDirection().ApproximatelyEquivalent(Vector2f.Zero));

        Input.SetKey(Key.Left, Scancode.Left, true);
        Input.SetKey(Key.Right, Scancode.Right, true);
        Assert.True(aggregator.GetDirection().ApproximatelyEquivalent(Vector2f.Zero));
        Input.SetKey(Key.Left, Scancode.Left, false);
        Assert.True(aggregator.GetDirection().ApproximatelyEquivalent(new Vector2f(1.0f, 0.0f)));
        Input.SetKey(Key.Right, Scancode.Right, false);
    }

    [Fact]
    public void Sources_are_averaged_and_focus_loss_clears_state()
    {
        using var aggregator = new DirectionAggregator();
        aggregator.SetEnabled(true);
        Input.SetKey(Key.A, Scancode.A, true);
        Input.SetControllerAxis(ControllerAxis.LeftX, 1.0f);
        Assert.True(aggregator.GetDirection().ApproximatelyEquivalent(Vector2f.Zero));

        var focus = new InputEvents.InputFocusEvent { Focus = false };
        Input.SendEvent(ref focus);
        Assert.True(aggregator.GetDirection().ApproximatelyEquivalent(Vector2f.Zero));
        Input.SetKey(Key.A, Scancode.A, false);
        Input.SetControllerAxis(ControllerAxis.LeftX, 0.0f);
    }

    [Fact]
    public void Disabling_source_removes_state_and_unsubscribes()
    {
        using var aggregator = new DirectionAggregator();
        aggregator.SetEnabled(true);
        Input.SetControllerAxis(ControllerAxis.LeftX, 1.0f);
        Assert.True(aggregator.GetDirection().ApproximatelyEquivalent(new Vector2f(1.0f, 0.0f)));

        aggregator.SetSubscriptionMask(aggregator.GetSubscriptionMask() & ~DirectionAggregatorMask.Joystick);
        Assert.True(aggregator.GetDirection().ApproximatelyEquivalent(Vector2f.Zero));
        Input.SetControllerAxis(ControllerAxis.LeftX, 1.0f);
        Assert.True(aggregator.GetDirection().ApproximatelyEquivalent(Vector2f.Zero));
        aggregator.SetSubscriptionMask(aggregator.GetSubscriptionMask() | DirectionAggregatorMask.Joystick);
        Assert.True(aggregator.GetDirection().ApproximatelyEquivalent(Vector2f.Zero));
        Input.SetControllerAxis(ControllerAxis.LeftX, 0.0f);
    }
}
