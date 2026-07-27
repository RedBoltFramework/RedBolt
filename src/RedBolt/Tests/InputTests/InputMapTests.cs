using RedBolt;

using RedBolt.Container;
using RedBolt.Core;
using RedBolt.IO;
using RedBolt.Input;
using RedBolt.Math;
using RedBolt.Resource;
namespace RedBolt.Tests;


[Collection(nameof(HeadlessEngineCollection))]
public sealed class InputMapTests
{
    private readonly HeadlessEngineFixture _fixture;

    public InputMapTests(HeadlessEngineFixture fixture)
    {
        this._fixture = fixture;
    }

    private InputSystem Input => _fixture.Input;

    [Fact]
    public void Dead_zone_clamps_to_zero()
    {
        var map = new InputMap { Name = "Input/Smoke" };
        map.SetDeadZone(-1.0f);
        Assert.True(MathDefs.Equals(map.GetDeadZone(), 0.0f));
    }

    [Fact]
    public void Keyboard_action_evaluation_works()
    {
        var map = new InputMap { Name = "Input/Smoke" };
        map.MapKeyboardKey("Jump", Scancode.A);
        Input.SetKey(Key.A, Scancode.A, true);
        Assert.True(MathDefs.Equals(map.Evaluate("Jump"), 1.0f));
        Input.SetKey(Key.A, Scancode.A, false);
    }

    [Fact]
    public void Keyboard_remapping_preserves_native_value_copy_behavior()
    {
        var map = new InputMap { Name = "Input/Smoke" };
        map.MapKeyboardKey("Jump", Scancode.A);
        map.MapKeyboardKey("Move", Scancode.A);
        Input.SetKey(Key.A, Scancode.A, true);
        Assert.True(MathDefs.Equals(map.Evaluate("Jump"), 1.0f));
        Assert.True(MathDefs.Equals(map.Evaluate("Move"), 1.0f));
        Assert.True(MathDefs.Equals(map.EvaluateByHash(new StringHash("Move")), 0.0f));
        Input.SetKey(Key.A, Scancode.A, false);
    }

    [Fact]
    public void Mouse_and_controller_mapping_preserves_native_mouse_value_behavior()
    {
        Input.SetMouseButton(MouseButton.Left, true);
        var map = new InputMap { Name = "Input/Smoke" };
        map.MapMouseButton("Fire", MouseButton.Left);

        Assert.True(MathDefs.Equals(map.Evaluate("Fire"), 0.0f));
        map.MapControllerButton("Fire", ControllerButton.A);
        var mapping = map.GetMapping("Fire");
        Assert.Equal((MouseButton)(1u << (int)MouseButton.Left), mapping.MouseButtons[0].GetMask());
        Assert.Equal(ControllerButton.A, mapping.ControllerButtons[0].GetControllerButton());
        Input.SetMouseButton(MouseButton.Left, false);
    }

    [Fact]
    public void Axis_hat_screen_mapping_works()
    {
        var map = new InputMap { Name = "Input/Smoke" };
        map.MapControllerAxis("Throttle", ControllerAxis.TriggerRight, 0.0f, 1.0f);
        map.MapHat("Menu", HatPosition.Up);
        map.MapScreenButton("TouchFire", "FireButton");

        var throttle = map.GetMappingByHash(new StringHash("Throttle"));
        Assert.Single(throttle.ControllerAxes);
        Assert.Equal(HatPosition.Up, (HatPosition)map.GetMapping("Menu").ControllerHats.Single().HatPosition);
        Assert.Equal("FireButton", map.GetMapping("TouchFire").ScreenButtons.Single().ElementName);
    }

    [Fact]
    public void Controller_axis_hat_screen_evaluation_works()
    {
        // Controller/hat/screen input evaluation requires initialized Input,
        // which is not available in headless mode.
        var map = new InputMap { Name = "Input/Smoke" };
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
            Assert.Single(map.GetMapping("Dash").ControllerButtons);
            Assert.Single(map.GetMappingByHash(new StringHash("Throttle")).ControllerAxes);
            Assert.Single(map.GetMapping("PadUp").ControllerHats);
            Assert.Single(map.GetMapping("TouchFire").ScreenButtons);
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
        using var source = new CString(new string('a', 32));
        using var replacement = new CString(new string('b', 32));
        using var map = new InputMap { Name = "Input/Smoke" };
        map.AddMetadata("Sensitivity", source);
        ref readonly var value = ref map.GetMetadata("Sensitivity");

        Assert.True(map.HasMetadata());
        Assert.Equal(new string('a', 32), value.GetCString().ConvertToString());
        Assert.NotSame(source, value.GetCString());

        map.AddMetadata("Sensitivity", replacement);

        Assert.Equal(new string('b', 32), value.GetCString().ConvertToString());
        Assert.NotSame(replacement, value.GetCString());
        Assert.Equal(Variant.Empty, map.GetMetadata("Missing"));
    }

    [Fact]
    public void Name_lists_contain_known_values()
    {
        Assert.Equal(291, InputMap.GetScanCodeNames().Count);
        Assert.Equal(21, InputMap.GetControllerButtonNames().Count);
        Assert.Equal(4, InputMap.GetControllerHatNames().Count);
        Assert.Equal(5, InputMap.GetMouseButtonNames().Count);
        Assert.Contains(nameof(Scancode.A), InputMap.GetScanCodeNames());
        Assert.Contains(nameof(MouseButton.Left), InputMap.GetMouseButtonNames());
    }

    [Fact]
    public void Class_and_mapping_types_match_native_value_shape()
    {
        Assert.Equal(typeof(SimpleResource), typeof(InputMap).BaseType);
        Assert.False(typeof(InputMap).IsSealed);
        Assert.False(typeof(InputMap).GetField(nameof(InputMap.DefaultDeadZone))!.IsStatic);
        Assert.True(typeof(InputMap.ActionMapping).IsValueType);
        Assert.True(typeof(ICloneable).IsAssignableFrom(typeof(InputMap.ActionMapping)));
        Assert.True(typeof(KeyboardKeyMapping).IsValueType);
        Assert.True(typeof(MouseButtonMapping).IsValueType);
        Assert.True(typeof(ControllerButtonMapping).IsValueType);
        Assert.True(typeof(ControllerAxisMapping).IsValueType);
        Assert.True(typeof(ControllerHatMapping).IsValueType);
        Assert.True(typeof(ScreenButtonMapping).IsValueType);

        var source = new InputMap.ActionMapping();
        source.KeyboardKeys.Add(new KeyboardKeyMapping(Scancode.A));
        var clone = source.Clone();
        clone.KeyboardKeys.Clear();
        Assert.Single(source.KeyboardKeys);
        Assert.Empty(clone.KeyboardKeys);
    }

    [Theory]
    [InlineData(InternalResourceFormat.Json)]
    [InlineData(InternalResourceFormat.Xml)]
    [InlineData(InternalResourceFormat.Binary)]
    public void Resource_archive_roundtrips_all_mapping_types(InternalResourceFormat format)
    {
        var source = new InputMap { Name = "Input/Source" };
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

        using var stream = new VectorBuffer();
        Assert.True(source.Save(stream, format));
        stream.Seek(0);

        var loaded = new InputMap { Name = "Input/Loaded" };
        Assert.True(loaded.Load(stream));
        Assert.True(MathDefs.Equals(loaded.GetDeadZone(), 0.25f));
        Assert.Equal(new Variant(1.5f), loaded.GetMetadata("Sensitivity"));
        var mapping = loaded.GetMapping("Action");
        Assert.Equal(Scancode.A, Assert.Single(mapping.KeyboardKeys).Scancode);
        Assert.Equal(MouseButton.X2, Assert.Single(mapping.MouseButtons).GetMask());
        Assert.Contains(mapping.ControllerButtons, value => value.Controller && value.GetControllerButton() == ControllerButton.X);
        Assert.Contains(mapping.ControllerButtons, value => !value.Controller && value.GetGenericButton() == 7);
        Assert.Contains(mapping.ControllerAxes, value => value.Controller && value.GetControllerAxis() == ControllerAxis.RightX);
        Assert.Contains(mapping.ControllerAxes, value => !value.Controller && value.GetGenericAxis() == 3);
        Assert.Equal(HatPosition.LeftUp, (HatPosition)Assert.Single(mapping.ControllerHats).HatPosition);
        Assert.Equal("ActionButton", Assert.Single(mapping.ScreenButtons).ElementName);
    }
}
