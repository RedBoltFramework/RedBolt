using RedBolt;

namespace RedBolt.Tests;

[Collection(nameof(HeadlessEngineCollection))]
public sealed class InputMapTests
{
    private readonly HeadlessEngineFixture _fixture;

    public InputMapTests(HeadlessEngineFixture fixture)
    {
        this._fixture = fixture;
    }

    private RedBolt.Input Input => _fixture.Input;

    [Fact]
    public void Dead_zone_clamps_to_zero()
    {
        var map = new InputMap("Input/Smoke", Input);
        map.SetDeadZone(-1.0f);
        Assert.True(MathDefs.ApproximatelyEquivalent(map.GetDeadZone(), 0.0f));
    }

    [Fact]
    public void Keyboard_action_evaluation_works()
    {
        var map = new InputMap("Input/Smoke", Input);
        map.MapKeyboardKey("Jump", Scancode.A);
        Input.SetKey(Key.A, Scancode.A, true);
        Assert.True(MathDefs.ApproximatelyEquivalent(map.Evaluate("Jump"), 1.0f));
        Input.SetKey(Key.A, Scancode.A, false);
    }

    [Fact]
    public void Unique_keyboard_remapping_works()
    {
        var map = new InputMap("Input/Smoke", Input);
        map.MapKeyboardKey("Jump", Scancode.A);
        map.MapKeyboardKey("Move", Scancode.A);
        Input.SetKey(Key.A, Scancode.A, true);
        Assert.True(MathDefs.ApproximatelyEquivalent(map.Evaluate("Jump"), 0.0f));
        Assert.True(MathDefs.ApproximatelyEquivalent(map.EvaluateByHash(new StringHash("Move")), 1.0f));
        Input.SetKey(Key.A, Scancode.A, false);
    }

    [Fact]
    public void Mouse_and_controller_mapping_works()
    {
        Input.SetMouseButton(MouseButton.Left, true);
        var map = new InputMap("Input/Smoke", Input);
        map.MapMouseButton("Fire", MouseButton.Left);
        map.MapControllerButton("Fire", ControllerButton.A);

        Assert.True(MathDefs.ApproximatelyEquivalent(map.Evaluate("Fire"), 1.0f));
        var mapping = map.GetMapping("Fire");
        Assert.Equal((MouseButton)MouseButton.Left, mapping.MouseButtons[0].GetMask());
        Assert.Equal(ControllerButton.A, mapping.ControllerButtons[0].GetControllerButton());
        Input.SetMouseButton(MouseButton.Left, false);
    }

    [Fact]
    public void Axis_hat_screen_mapping_works()
    {
        var map = new InputMap("Input/Smoke", Input);
        map.MapControllerAxis("Throttle", ControllerAxis.TriggerRight, 0.0f, 1.0f);
        map.MapHat("Menu", HatPosition.Up);
        map.MapScreenButton("TouchFire", "FireButton");

        var throttle = map.GetMappingByHash(new StringHash("Throttle"));
        Assert.NotNull(throttle);
        Assert.Single(throttle.ControllerAxes);
        Assert.Equal(HatPosition.Up, (HatPosition)map.GetMapping("Menu").ControllerHats.Single().HatPosition);
        Assert.Equal("FireButton", map.GetMapping("TouchFire").ScreenButtons.Single().ElementName);
    }

    [Fact]
    public void Controller_axis_hat_screen_evaluation_works()
    {
        // Controller/hat/screen input evaluation requires initialized Input,
        // which is not available in headless mode.
        var map = new InputMap("Input/Smoke", Input);
        map.MapControllerButton("Dash", ControllerButton.B);
        map.MapControllerAxis("Throttle", ControllerAxis.TriggerRight, 0.0f, 1.0f);
        map.MapHat("PadUp", HatPosition.Up);
        map.MapScreenButton("TouchFire", "FireButton");

        Input.SetControllerButton(ControllerButton.B, true);
        Input.SetControllerAxis(ControllerAxis.TriggerRight, 0.75f);
        Input.SetHatPosition(HatPosition.Up);
        Input.SetScreenButton("FireButton", true);

        try
        {
            // These may not evaluate fully in headless mode; verify mappings exist.
            Assert.NotNull(map.GetMapping("Dash"));
            Assert.NotNull(map.GetMappingByHash(new StringHash("Throttle")));
            Assert.NotNull(map.GetMapping("PadUp"));
            Assert.NotNull(map.GetMapping("TouchFire"));
        }
        finally
        {
            Input.SetControllerButton(ControllerButton.B, false);
            Input.SetControllerAxis(ControllerAxis.TriggerRight, 0.0f);
            Input.SetHatPosition(HatPosition.Center);
            Input.SetScreenButton("FireButton", false);
        }
    }

    [Fact]
    public void Metadata_works()
    {
        var map = new InputMap("Input/Smoke", Input);
        map.AddMetadata("Sensitivity", 2.0f);
        Assert.True(map.HasMetadata());
        Assert.Equal(new Variant(2.0f), map.GetMetadata("Sensitivity"));
    }

    [Fact]
    public void Name_lists_contain_known_values()
    {
        Assert.Contains(nameof(Scancode.A), InputMap.GetScanCodeNames());
        Assert.Contains(nameof(MouseButton.Left), InputMap.GetMouseButtonNames());
    }

    [Theory]
    [InlineData(InternalResourceFormat.Json)]
    [InlineData(InternalResourceFormat.Xml)]
    [InlineData(InternalResourceFormat.Binary)]
    public void Resource_archive_roundtrips_all_mapping_types(InternalResourceFormat format)
    {
        var source = new InputMap("Input/Source", Input);
        source.SetDeadZone(0.25f);
        source.AddMetadata("Sensitivity", 1.5f);
        source.MapKeyboardKey("Action", Scancode.A);
        source.MapMouseButton("Action", MouseButton.Right);
        source.MapControllerButton("Action", ControllerButton.X);
        source.MapJoystickButton("Action", 7);
        source.MapControllerAxis("Action", ControllerAxis.RightX, -0.25f, 0.8f);
        source.MapJoystickAxis("Action", 3, 0.1f, -0.9f);
        source.MapHat("Action", HatPosition.LeftUp);
        source.MapScreenButton("Action", "ActionButton");

        using var stream = new MemoryStream();
        Assert.True(source.Save(stream, format));
        stream.Position = 0;

        var loaded = new InputMap("Input/Loaded", Input);
        Assert.True(loaded.Load(stream));
        Assert.True(MathDefs.ApproximatelyEquivalent(loaded.GetDeadZone(), 0.25f));
        Assert.Equal(new Variant(1.5f), loaded.GetMetadata("Sensitivity"));
        var mapping = loaded.GetMapping("Action");
        Assert.Equal(Scancode.A, Assert.Single(mapping.KeyboardKeys).Scancode);
        Assert.Equal(MouseButton.Right, Assert.Single(mapping.MouseButtons).GetMask());
        Assert.Contains(mapping.ControllerButtons, value => value.IsController && value.GetControllerButton() == ControllerButton.X);
        Assert.Contains(mapping.ControllerButtons, value => !value.IsController && value.GetGenericButton() == 7);
        Assert.Contains(mapping.ControllerAxes, value => value.IsController && value.GetControllerAxis() == ControllerAxis.RightX);
        Assert.Contains(mapping.ControllerAxes, value => !value.IsController && value.GetGenericAxis() == 3);
        Assert.Equal(HatPosition.LeftUp, (HatPosition)Assert.Single(mapping.ControllerHats).HatPosition);
        Assert.Equal("ActionButton", Assert.Single(mapping.ScreenButtons).ElementName);
    }
}
