using RedBolt;

namespace RedBolt.Tests;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class PointerAdapterTests
{
    private readonly HeadlessEngineFixture fixture;

    public PointerAdapterTests(HeadlessEngineFixture fixture)
    {
        this.fixture = fixture;
    }

    private RedBolt.Input Input => fixture.Input;

    [Fact]
    public void Mouse_input_updates_pointer_state()
    {
        var adapter = new PointerAdapter(Input);
        adapter.SetEnabled(true);
        adapter.SetSubscriptionMask(PointerAdapterMask.Mouse);

        Input.SetMousePosition(new IntVector2(12, 34));
        Input.SetMouseButton(MouseButton.Left, true);
        adapter.Update(0.0f);

        Assert.Equal(new IntVector2(12, 34), adapter.GetPointerPosition());
        Assert.True(adapter.IsButtonDown());
    }

    [Fact]
    public void Direction_adapter_moves_pointer()
    {
        var adapter = new PointerAdapter();
        adapter.SetEnabled(true);
        adapter.SetSubscriptionMask(PointerAdapterMask.Keyboard);
        adapter.SetCursorSpeed(20.0f);
        adapter.SetCursorAcceleration(1.0f);
        adapter.GetDirectionAggregator().SetExternalDirection(new Vector2(1.0f, 0.0f));
        adapter.Update(0.5f);

        Assert.Equal(new IntVector2(5, 0), adapter.GetPointerPosition());
    }

    [Fact]
    public void Joystick_button_sets_pressed()
    {
        var adapter = new PointerAdapter(Input);
        adapter.SetEnabled(true);
        adapter.SetSubscriptionMask(PointerAdapterMask.Joystick);

        Input.SetControllerButton(ControllerButton.A, true);
        adapter.Update(0.0f);
        Input.SetControllerButton(ControllerButton.A, false);

        Assert.True(adapter.IsButtonDown());
    }

    [Fact]
    public void Configuration_and_external_update_work()
    {
        var adapter = new PointerAdapter();
        adapter.SetEnabled(true);
        adapter.SetSubscriptionMask(PointerAdapterMask.Keyboard | PointerAdapterMask.Joystick);
        adapter.SetCursorSpeed(150.0f);
        adapter.SetCursorAcceleration(2.0f);
        adapter.SetUIElement("VirtualStick");
        adapter.UpdatePointer(new Vector2(3.0f, 4.0f), press: true);

        Assert.True(adapter.IsEnabled());
        Assert.Equal(PointerAdapterMask.Keyboard | PointerAdapterMask.Joystick, adapter.GetSubscriptionMask());
        Assert.True(MathDefs.ApproximatelyEquivalent(adapter.GetCursorVelocity(), 150.0f));
        Assert.True(MathDefs.ApproximatelyEquivalent(adapter.GetCursorAcceleration(), 2.0f));
        Assert.Equal("VirtualStick", adapter.GetUIElement());
        Assert.Equal(new IntVector2(3, 4), adapter.GetPointerPosition());
        Assert.True(adapter.IsButtonDown());
    }
}
