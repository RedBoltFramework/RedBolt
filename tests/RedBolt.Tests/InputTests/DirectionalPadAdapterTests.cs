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
        var adapter = new DirectionalPadAdapter(Input);
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
        var adapter = new DirectionalPadAdapter(Input);
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
        var adapter = new DirectionalPadAdapter(Input);
        adapter.SetEnabled(true);
        Input.SetHatPosition(HatPosition.LeftDown);
        adapter.SetExternalDirection(new Vector2(1.0f, -1.0f));

        Assert.True(adapter.GetScancodeDown(Scancode.Left));
        Assert.True(adapter.GetScancodeDown(Scancode.Down));
        Assert.True(adapter.GetScancodeDown(Scancode.Right));
        Assert.True(adapter.GetScancodeDown(Scancode.Up));

        Input.SetHatPosition(HatPosition.Center);
    }

    [Fact]
    public void Configuration_roundtrips()
    {
        var adapter = new DirectionalPadAdapter();
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
}
