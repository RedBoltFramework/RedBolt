using RedBolt;

namespace RedBolt.Tests;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class InputTests
{
    private readonly HeadlessEngineFixture fixture;

    public InputTests(HeadlessEngineFixture fixture)
    {
        this.fixture = fixture;
    }

    private RedBolt.Input Input => fixture.Input;

    [Fact]
    public void Initial_state_is_empty()
    {
        Assert.False(Input.GetKeyDown(Key.Escape));
        Assert.False(Input.GetScancodePress(Scancode.Escape));
    }

    [Fact]
    public void Key_scancode_conversion_works()
    {
        Assert.Equal(Key.A, RedBolt.Input.GetKeyFromName("A"));
        Assert.Equal("1", RedBolt.Input.GetKeyName(Key.Num1));
        Assert.Equal(Scancode.A, RedBolt.Input.GetScancodeFromKey(Key.A));
        Assert.Equal(Key.LeftCtrl, RedBolt.Input.GetKeyFromScancode(Scancode.LeftCtrl));
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
        Assert.True(Input.GetMouseButtonDown(MouseButton.Left));
        Assert.True(Input.GetMouseButtonPress(MouseButton.Left));
        Assert.Equal(2, Input.GetMouseMoveWheel());
    }

    [Fact]
    public void Mouse_click_state_works()
    {
        Input.SetMouseButton(MouseButton.Left, true);
        Input.BeginFrame();
        Input.SetMouseButton(MouseButton.Left, false, clicks: 1);
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
        Assert.Equal(Vector2.Zero, Input.GetRelativeMousePosition());
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
        Input.SetMouseMode(MouseMode.Relative);
        Input.SetMouseMode(MouseMode.Absolute, suppressEvent: true);
        Input.ResetMouseMode();
        Assert.True(Input.IsMouseLocked());
        Assert.True(Input.IsMouseGrabbed());
    }

    [Fact]
    public void Touch_emulation_and_fullscreen_toggle_work()
    {
        Input.SetToggleFullscreen(false);
        Input.SetTouchEmulation(true);
        Assert.False(Input.GetToggleFullscreen());
        Assert.True(Input.GetTouchEmulation());
    }
}
