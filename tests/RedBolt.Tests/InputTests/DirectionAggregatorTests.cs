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
        var aggregator = new DirectionAggregator(Input);
        aggregator.SetEnabled(true);
        Input.SetKey(Key.W, Scancode.W, true);
        Input.SetKey(Key.D, Scancode.D, true);

        Assert.True(aggregator.GetDirection().ApproximatelyEquivalent(new Vector2(1.0f, -1.0f)));

        Input.SetKey(Key.W, Scancode.W, false);
        Input.SetKey(Key.D, Scancode.D, false);
    }

    [Fact]
    public void Joystick_and_dead_zone_work()
    {
        var aggregator = new DirectionAggregator(Input);
        aggregator.SetEnabled(true);
        aggregator.SetDeadZone(0.2f);
        Input.SetControllerAxis(ControllerAxis.LeftX, 0.1f);
        Input.SetControllerAxis(ControllerAxis.LeftY, -0.5f);

        Assert.True(aggregator.GetDirection().ApproximatelyEquivalent(new Vector2(0.0f, -0.375f), 0.001f));

        Input.SetControllerAxis(ControllerAxis.LeftX, 0.0f);
        Input.SetControllerAxis(ControllerAxis.LeftY, 0.0f);
    }

    [Fact]
    public void External_and_touch_direction_work()
    {
        var aggregator = new DirectionAggregator();
        aggregator.SetEnabled(true);
        aggregator.SetSubscriptionMask(DirectionAggregatorMask.Touch);
        aggregator.SetExternalDirection(new Vector2(0.25f, 0.25f));
        aggregator.SetTouchDirection(new Vector2(1.0f, -1.0f));

        Assert.True(aggregator.GetDirection().ApproximatelyEquivalent(new Vector2(1.0f, -0.75f), 0.001f));
    }
}
