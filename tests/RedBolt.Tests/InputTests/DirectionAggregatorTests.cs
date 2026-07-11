using RedBolt;

namespace RedBolt.Tests;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class DirectionAggregatorTests
{
    private readonly HeadlessEngineFixture _fixture;

    public DirectionAggregatorTests(HeadlessEngineFixture fixture)
    {
        this._fixture = fixture;
    }

    private RedBolt.Input Input => _fixture.Input;

    [Fact]
    public void Keyboard_direction_works()
    {
        using var aggregator = new DirectionAggregator(Input);
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
        using var aggregator = new DirectionAggregator(Input);
        aggregator.SetEnabled(true);
        aggregator.SetDeadZone(0.2f);
        Input.SetControllerAxis(ControllerAxis.LeftX, 0.1f);
        Input.SetControllerAxis(ControllerAxis.LeftY, -0.5f);

        Assert.True(aggregator.GetDirection().ApproximatelyEquivalent(new Vector2f(0.0f, -0.375f), 0.001f));

        Input.SetControllerAxis(ControllerAxis.LeftX, 0.0f);
        Input.SetControllerAxis(ControllerAxis.LeftY, 0.0f);
    }

    [Fact]
    public void External_and_touch_direction_work()
    {
        using var aggregator = new DirectionAggregator();
        aggregator.SetEnabled(true);
        aggregator.SetSubscriptionMask(DirectionAggregatorMask.Touch);
        aggregator.SetExternalDirection(new Vector2f(0.25f, 0.25f));
        aggregator.SetTouchDirection(new Vector2f(1.0f, -1.0f));

        Assert.True(aggregator.GetDirection().ApproximatelyEquivalent(new Vector2f(1.0f, -0.75f), 0.001f));
    }

    [Fact]
    public void Sources_are_averaged_and_focus_loss_clears_state()
    {
        using var aggregator = new DirectionAggregator(Input);
        aggregator.SetEnabled(true);
        Input.SetKey(Key.A, Scancode.A, true);
        Input.SetControllerAxis(ControllerAxis.LeftX, 1.0f);
        Assert.True(aggregator.GetDirection().ApproximatelyEquivalent(Vector2f.Zero));

        var focus = new InputEvents.InputFocusEvent { Focus = false };
        Context.Instance.SendEvent(ref focus);
        Assert.True(aggregator.GetDirection().ApproximatelyEquivalent(Vector2f.Zero));
        Input.SetKey(Key.A, Scancode.A, false);
        Input.SetControllerAxis(ControllerAxis.LeftX, 0.0f);
    }

    [Fact]
    public void Touch_events_track_single_pointer_and_release_state()
    {
        using var aggregator = new DirectionAggregator(Input);
        aggregator.SetSubscriptionMask(DirectionAggregatorMask.Touch);
        aggregator.SetEnabled(true);
        var begin = new InputEvents.TouchBeginEvent { TouchID = 7, X = 10, Y = 10 };
        var move = new InputEvents.TouchMoveEvent { TouchID = 7, X = 58, Y = -38 };
        Context.Instance.SendEvent(ref begin);
        Context.Instance.SendEvent(ref move);
        Assert.True(aggregator.GetDirection().ApproximatelyEquivalent(new Vector2f(1.0f, -1.0f)));

        var end = new InputEvents.TouchEndEvent { TouchID = 7, X = 58, Y = -38 };
        Context.Instance.SendEvent(ref end);
        Assert.True(aggregator.GetDirection().ApproximatelyEquivalent(Vector2f.Zero));
    }
}
