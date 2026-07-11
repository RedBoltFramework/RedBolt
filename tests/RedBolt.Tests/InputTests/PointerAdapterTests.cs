using RedBolt;

namespace RedBolt.Tests;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class PointerAdapterTests
{
    private readonly HeadlessEngineFixture _fixture;

    public PointerAdapterTests(HeadlessEngineFixture fixture)
    {
        this._fixture = fixture;
    }

    private RedBolt.Input Input => _fixture.Input;

    [Fact]
    public void Mouse_input_updates_pointer_state()
    {
        using var adapter = new PointerAdapter(Input);
        adapter.SetEnabled(true);
        adapter.SetSubscriptionMask(PointerAdapterMask.Mouse);

        Input.SetMousePosition(new Vector2i(12, 34));
        Input.SetMouseButton(MouseButton.Left, true);
        adapter.Update(0.0f);

        Assert.Equal(new Vector2i(12, 34), adapter.GetPointerPosition());
        Assert.True(adapter.IsButtonDown());
    }

    [Fact]
    public void Direction_adapter_moves_pointer()
    {
        using var adapter = new PointerAdapter();
        adapter.SetEnabled(true);
        adapter.SetSubscriptionMask(PointerAdapterMask.Keyboard);
        adapter.SetCursorSpeed(20.0f);
        adapter.SetCursorAcceleration(1.0f);
        adapter.GetDirectionAggregator().SetExternalDirection(new Vector2f(1.0f, 0.0f));
        adapter.Update(0.5f);

        Assert.Equal(new Vector2i(50, 0), adapter.GetPointerPosition());
    }

    [Fact]
    public void Joystick_button_sets_pressed()
    {
        using var adapter = new PointerAdapter(Input);
        adapter.SetEnabled(true);
        adapter.SetSubscriptionMask(PointerAdapterMask.Joystick);

        Input.SetControllerButton(ControllerButton.A, true);
        Assert.True(adapter.IsButtonDown());
        Input.SetControllerButton(ControllerButton.A, false);
        Assert.False(adapter.IsButtonDown());
    }

    [Fact]
    public void Configuration_and_external_update_work()
    {
        using var adapter = new PointerAdapter();
        adapter.SetEnabled(true);
        adapter.SetSubscriptionMask(PointerAdapterMask.Keyboard | PointerAdapterMask.Joystick);
        adapter.SetCursorSpeed(150.0f);
        adapter.SetCursorAcceleration(2.0f);
        adapter.SetUIElement("VirtualStick");
        adapter.UpdatePointer(new Vector2f(3.0f, 4.0f), press: true);

        Assert.True(adapter.IsEnabled());
        Assert.Equal(PointerAdapterMask.Keyboard | PointerAdapterMask.Joystick, adapter.GetSubscriptionMask());
        Assert.True(MathDefs.ApproximatelyEquivalent(adapter.GetCursorVelocity(), 100.0f));
        Assert.True(MathDefs.ApproximatelyEquivalent(adapter.GetCursorAcceleration(), 2.0f));
        Assert.Equal("VirtualStick", adapter.GetUIElement());
        Assert.Equal(new Vector2i(3, 4), adapter.GetPointerPosition());
        Assert.True(adapter.IsButtonDown());
    }

    [Fact]
    public void Touch_owns_pointer_until_matching_release()
    {
        using var adapter = new PointerAdapter(Input);
        adapter.SetSubscriptionMask(PointerAdapterMask.Mouse | PointerAdapterMask.Touch);
        adapter.SetEnabled(true);
        var begin = new InputEvents.TouchBeginEvent { TouchID = 4, X = 20, Y = 30 };
        Input.SendEvent(ref begin);
        Assert.Equal(new Vector2i(20, 30), adapter.GetPointerPosition());
        Assert.True(adapter.IsButtonDown());

        Input.SetMousePosition(new Vector2i(100, 100));
        Assert.Equal(new Vector2i(20, 30), adapter.GetPointerPosition());
        var end = new InputEvents.TouchEndEvent { TouchID = 4, X = 25, Y = 35 };
        Input.SendEvent(ref end);
        Assert.Equal(new Vector2i(25, 35), adapter.GetPointerPosition());
        Assert.False(adapter.IsButtonDown());
    }
}
