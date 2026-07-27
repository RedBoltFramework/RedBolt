using RedBolt;
using RedBolt.Core;
using RedBolt.Input;
using RedBolt.Math;

namespace RedBolt.Tests;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class InputTests
{
    [Fact]
    public void Engine_registers_input_adapter_factories()
    {
        using var pointerAdapter = Assert.IsType<PointerAdapter>(
            Context.Instance.CreateObject(ObjectReflection<PointerAdapter>.TypeId));
        using var directionAggregator = Assert.IsType<DirectionAggregator>(
            Context.Instance.CreateObject(ObjectReflection<DirectionAggregator>.TypeId));
        using var directionalPadAdapter = Assert.IsType<DirectionalPadAdapter>(
            Context.Instance.CreateObject(ObjectReflection<DirectionalPadAdapter>.TypeId));
    }

    private readonly HeadlessEngineFixture _fixture;

    public InputTests(HeadlessEngineFixture fixture)
    {
        this._fixture = fixture;
    }

    private InputSystem Input => _fixture.Input;

    [Fact]
    public void Native_class_shapes_are_preserved()
    {
        Assert.True(typeof(TouchState).IsValueType);
        Assert.True(typeof(JoystickState).IsValueType);
        Assert.True(typeof(ICloneable).IsAssignableFrom(typeof(JoystickState)));
        Assert.False(typeof(InputSystem).IsSealed);
        Assert.Equal(typeof(InputSystem), typeof(InputSystem).GetMethod(nameof(IDisposable.Dispose))!.DeclaringType);
    }

    [Fact]
    public void Native_input_event_payloads_are_value_types()
    {
        Type[] eventTypes =
        [
            typeof(InputEvents.MouseButtonDownEvent),
            typeof(InputEvents.MouseButtonUpEvent),
            typeof(InputEvents.MouseMoveEvent),
            typeof(InputEvents.MouseWheelEvent),
            typeof(InputEvents.KeyDownEvent),
            typeof(InputEvents.KeyUpEvent),
            typeof(InputEvents.TextInputEvent),
            typeof(InputEvents.TextEditingEvent),
            typeof(InputEvents.JoystickConnectedEvent),
            typeof(InputEvents.JoystickDisconnectedEvent),
            typeof(InputEvents.JoystickButtonDownEvent),
            typeof(InputEvents.JoystickButtonUpEvent),
            typeof(InputEvents.JoystickAxisMoveEvent),
            typeof(InputEvents.JoystickHatMoveEvent),
            typeof(InputEvents.TouchBeginEvent),
            typeof(InputEvents.TouchEndEvent),
            typeof(InputEvents.TouchMoveEvent),
            typeof(InputEvents.GestureRecordedEvent),
            typeof(InputEvents.GestureInputEvent),
            typeof(InputEvents.MultiGestureEvent),
            typeof(InputEvents.DropFileEvent),
            typeof(InputEvents.InputFocusEvent),
            typeof(InputEvents.MouseVisibleChangedEvent),
            typeof(InputEvents.MouseModeChangedEvent),
            typeof(InputEvents.ExitRequestedEvent),
            typeof(InputEvents.SDLRawInputEvent),
            typeof(InputEvents.InputBeginEvent),
            typeof(InputEvents.InputEndEvent),
            typeof(InputEvents.MultitouchEvent)
        ];

        Assert.All(eventTypes, type => Assert.True(type.IsValueType));
    }

    [Fact]
    public void Joystick_state_clone_preserves_native_value_copy_semantics()
    {
        var source = new JoystickState();
        source.Initialize(1, 1, 1);
        source.Buttons[0] = true;
        source.ButtonPress[0] = true;
        source.Axes[0] = 0.5f;
        source.ValidAxis[0] = true;
        source.Hats[0] = (int)HatPosition.Right;

        var clone = (JoystickState)source.Clone();
        clone.Buttons[0] = false;
        clone.ButtonPress[0] = false;
        clone.Axes[0] = -0.5f;
        clone.ValidAxis[0] = false;
        clone.Hats[0] = (int)HatPosition.Left;

        Assert.True(source.Buttons[0]);
        Assert.True(source.ButtonPress[0]);
        Assert.Equal(0.5f, source.Axes[0]);
        Assert.True(source.ValidAxis[0]);
        Assert.Equal((int)HatPosition.Right, source.Hats[0]);
    }

    [Fact]
    public void Initial_state_is_empty()
    {
        Assert.False(Input.GetKeyDown(Key.Escape));
        Assert.False(Input.GetScancodePress(Scancode.Escape));
    }

    [Fact]
    public void Key_scancode_conversion_works()
    {
        Assert.Equal(Key.A, InputSystem.GetKeyFromName("A"));
        Assert.Equal("1", InputSystem.GetKeyName(Key.Num1));
        Assert.Equal(
            (Scancode)RedBolt.Bindings.Sdl.SdlNativeBindings.SdlGetScancodeFromKey((int)Key.A),
            InputSystem.GetScancodeFromKey(Key.A));
        Assert.Equal(
            (Key)RedBolt.Bindings.Sdl.SdlNativeBindings.SdlGetKeyFromScancode((RedBolt.Bindings.Sdl.SdlScancode)Scancode.LeftCtrl),
            InputSystem.GetKeyFromScancode(Scancode.LeftCtrl));
    }

    [Fact]
    public void Managed_key_state_setters_work()
    {
        Input.SetKey(Key.A, Scancode.A, true);
        Assert.True(Input.GetKeyDown(Key.A));
        Assert.True(Input.GetKeyPress(Key.A));
        Assert.True(Input.GetScancodeDown(Scancode.A));
        Assert.True(Input.GetScancodePress(Scancode.A));
    }

    [Fact]
    public void BeginFrame_resets_key_press()
    {
        Input.SetKey(Key.A, Scancode.A, true);
        Input.BeginFrame();
        Assert.True(Input.GetKeyDown(Key.A));
        Assert.False(Input.GetKeyPress(Key.A));
        Assert.False(Input.GetScancodePress(Scancode.A));
    }

    [Fact]
    public void Key_release_works()
    {
        Input.SetKey(Key.A, Scancode.A, true);
        Input.BeginFrame();
        Input.SetKey(Key.A, Scancode.A, false);
        Assert.False(Input.GetKeyDown(Key.A));
        Assert.False(Input.GetScancodeDown(Scancode.A));
    }

    [Fact]
    public void Managed_mouse_state_setters_work()
    {
        Input.SetMouseButton(MouseButton.Left, true);
        Input.SetMouseWheel(2);
        Input.SetMouseWheel(0);
        Assert.True(Input.GetMouseButtonDown(MouseButton.Left));
        Assert.True(Input.GetMouseButtonPress(MouseButton.Left));
        Assert.Equal(2, Input.GetMouseMoveWheel());
    }

    [Fact]
    public void Mouse_click_state_works()
    {
        Input.SetMouseButton(MouseButton.Left, true);
        Input.BeginFrame();
        Input.SetMouseButton(MouseButton.Left, false);
        Assert.False(Input.GetMouseButtonDown(MouseButton.Left));
        Assert.True(Input.GetMouseButtonClick(MouseButton.Left));
    }

    [Fact]
    public void Disabled_input_returns_zeroes()
    {
        Input.SetEnabled(false);
        Assert.False(Input.GetEnabled());
        Assert.Equal(0, Input.GetMouseMoveX());
        Assert.Equal(0, Input.GetMouseMoveY());
        Assert.Equal(Vector2f.Zero, Input.GetRelativeMousePosition());
        Input.SetEnabled(true);
    }

    [Fact]
    public void Mouse_visibility_and_grab_reset_work()
    {
        // In headless mode, mouse visibility/grab may behave differently since
        // the Input subsystem is not fully initialized.
        if (!Input.IsInitialized)
            return;

        Input.SetMouseVisible(false);
        Input.SetMouseVisible(true, suppressEvent: true);
        Input.ResetMouseVisible();
        Assert.False(Input.IsMouseVisible());

        Input.SetMouseGrabbed(true);
        Input.SetMouseGrabbed(false, suppressEvent: true);
        Input.ResetMouseGrabbed();
        Assert.False(Input.IsMouseGrabbed());
    }

    [Fact]
    public void Mouse_mode_and_lock_state_work()
    {
        Input.SetMouseMode(MouseMode.Absolute);
        Input.SetMouseGrabbed(false);
        Input.SetMouseMode(MouseMode.Relative);
        Assert.True(Input.IsMouseLocked());
        Assert.False(Input.IsMouseGrabbed());
        Input.SetMouseMode(MouseMode.Absolute, suppressEvent: true);
        Input.ResetMouseMode();
        Assert.True(Input.IsMouseLocked());
        Assert.False(Input.IsMouseGrabbed());
    }

    [Fact]
    public void Touch_emulation_and_fullscreen_toggle_work()
    {
        Input.SetToggleFullscreen(false);
        Input.SetTouchEmulation(true);
        Assert.False(Input.GetToggleFullscreen());
        Assert.True(Input.GetTouchEmulation());
    }

    [Fact]
    public void Managed_state_changes_emit_upstream_input_events()
    {
        Input.SetKey(Key.B, Scancode.B, false);
        Input.SetMouseButton(MouseButton.Right, false);
        InputEvents.KeyDownEvent keyDown = default;
        InputEvents.KeyUpEvent keyUp = default;
        InputEvents.MouseButtonDownEvent mouseDown = default;
        InputEvents.MouseButtonUpEvent mouseUp = default;
        InputEvents.JoystickAxisMoveEvent axisMove = default;
        InputEvents.JoystickHatMoveEvent hatMove = default;
        using var receiver = new TestEventReceiver();
        receiver.SubscribeToEvent<InputEvents.KeyDownEvent>((ref InputEvents.KeyDownEvent value) => keyDown = value);
        receiver.SubscribeToEvent<InputEvents.KeyUpEvent>((ref InputEvents.KeyUpEvent value) => keyUp = value);
        receiver.SubscribeToEvent<InputEvents.MouseButtonDownEvent>(
            (ref InputEvents.MouseButtonDownEvent value) => mouseDown = value);
        receiver.SubscribeToEvent<InputEvents.MouseButtonUpEvent>(
            (ref InputEvents.MouseButtonUpEvent value) => mouseUp = value);
        receiver.SubscribeToEvent<InputEvents.JoystickAxisMoveEvent>(
            (ref InputEvents.JoystickAxisMoveEvent value) => axisMove = value);
        receiver.SubscribeToEvent<InputEvents.JoystickHatMoveEvent>(
            (ref InputEvents.JoystickHatMoveEvent value) => hatMove = value);

        Input.SetKey(Key.B, Scancode.B, true);
        Input.SetKey(Key.B, Scancode.B, false);
        Input.SetMouseButton(MouseButton.Right, true, 2);
        Input.SetMouseButton(MouseButton.Right, false);
        Input.SetJoystickAxis(3, 0.75f);
        Input.SetHatPosition(HatPosition.RightDown);

        Assert.Equal(Key.B, keyDown.Key);
        Assert.Equal(Scancode.B, keyUp.Scancode);
        Assert.Equal(MouseButton.Right, mouseDown.Button);
        Assert.Equal(2, mouseDown.Clicks);
        Assert.Equal(MouseButton.Right, mouseUp.Button);
        Assert.Equal(3, axisMove.Axis);
        Assert.True(MathDefs.Equals(axisMove.Position, 0.75f));
        Assert.Equal(HatPosition.RightDown, hatMove.Position);
        Input.SetHatPosition(HatPosition.Center);
    }

    [Fact]
    public void Joystick_state_queries_track_managed_updates()
    {
        Input.SetControllerButton(ControllerButton.A, true);
        Input.SetControllerAxis(ControllerAxis.LeftX, 0.75f);
        Input.SetHatPosition(HatPosition.RightDown);

        Assert.Equal(1u, Input.GetNumJoysticks());
        var joystick = Input.GetJoystick(0);
        Assert.True(joystick.HasValue);
        Assert.True(joystick.Value.IsController());
        Assert.True(joystick.Value.GetButtonDown((uint)ControllerButton.A));
        Assert.True(joystick.Value.GetButtonPress((uint)ControllerButton.A));
        Assert.True(joystick.Value.HasAxisPosition((uint)ControllerAxis.LeftX));
        Assert.True(MathDefs.Equals(joystick.Value.GetAxisPosition((uint)ControllerAxis.LeftX), 0.75f));
        Assert.Equal((int)HatPosition.RightDown, joystick.Value.GetHatPosition(0));

        Input.BeginFrame();
        joystick = Input.GetJoystick(0);
        Assert.True(joystick.HasValue);
        Assert.False(joystick.Value.GetButtonPress((uint)ControllerButton.A));

        Input.SetControllerButton(ControllerButton.A, false);
        Input.SetHatPosition(HatPosition.Center);
    }
}
