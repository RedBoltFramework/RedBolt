// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Input;

using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.Math;
using RedBolt.Resource;
using ResourceType = RedBolt.Resource.Resource;

public enum InputOverlayAlignment
{
    TopLeft,
    Top,
    TopRight,
    Left,
    Center,
    Right,
    BottomLeft,
    Bottom,
    BottomRight
}

public sealed class InputOverlayHint
{
    public string Name { get; internal set; } = string.Empty;
    public InputDeviceType DeviceType { get; set; }
    public Texture2D? Texture { get; set; }
    public string Text { get; set; } = string.Empty;
    public Vector2f Anchor { get; set; }
    public Vector2f Offset { get; set; }
    public InputOverlayAlignment Alignment { get; set; }
    public bool Visible { get; set; } = true;
}

public sealed class InputOverlayButton
{
    internal int TouchId = -1;
    internal string Channel = string.Empty;
    public string Name { get; internal set; } = string.Empty;
    public string Action { get; internal set; } = string.Empty;
    public Texture2D? Texture { get; set; }
    public string Text { get; set; } = string.Empty;
    public Vector2f Anchor { get; set; }
    public Vector2f Offset { get; set; }
    public InputOverlayAlignment Alignment { get; set; }
    public Vector2f Size { get; set; }
    public bool Visible { get; set; } = true;
}

public sealed class InputOverlayJoystick
{
    internal int TouchId = -1;
    internal Vector2f Origin;
    internal string HorizontalChannel = string.Empty;
    internal string VerticalChannel = string.Empty;
    internal string UpAction = string.Empty;
    internal string DownAction = string.Empty;
    internal string LeftAction = string.Empty;
    internal string RightAction = string.Empty;
    public string Name { get; internal set; } = string.Empty;
    public Vector4f ActivationRegion { get; set; } = new(0.0f, 0.0f, 0.5f, 1.0f);
    public float Radius { get; set; } = 72.0f;
    public float DeadZone { get; set; } = 0.1f;
    public bool Visible { get; set; } = true;
    public Vector2f Position { get; internal set; }
}

[Reflected(ObjectCategory.Category_Geometry)]
public partial class InputOverlay : Drawable
{
    private readonly Dictionary<string, InputOverlayHint> _hints = new(StringComparer.Ordinal);
    private readonly Dictionary<string, InputOverlayButton> _buttons = new(StringComparer.Ordinal);
    private readonly Dictionary<string, InputOverlayJoystick> _joysticks = new(StringComparer.Ordinal);
    private readonly Input _input;
    private InputMap? _inputMap;
    private Font? _font;

    public InputOverlay()
        : base(DrawableFlags.Geometry)
    {
        _input = GetSubsystem<Input>()!;
        SetCastShadows(false);
        SetOccluder(false);
        SetOccludee(false);
        SubscribeToEvent<InputEvents.TouchBeginEvent>(_input, HandleTouchBegin);
        SubscribeToEvent<InputEvents.TouchMoveEvent>(_input, HandleTouchMove);
        SubscribeToEvent<InputEvents.TouchEndEvent>(_input, HandleTouchEnd);
        SubscribeToEvent<InputEvents.InputDeviceChangedEvent>(_input, HandleInputDeviceChanged);
        SubscribeToEvent<InputEvents.InputFocusEvent>(_input, HandleInputFocus);
        SubscribeToEvent<InputEvents.InputBeginEvent>(_input, HandleInputBegin);
    }

    public InputOverlay(InputMap inputMap)
        : this()
    {
        SetInputMap(inputMap);
    }

    public IReadOnlyCollection<InputOverlayHint> Hints => _hints.Values;
    public IReadOnlyCollection<InputOverlayButton> Buttons => _buttons.Values;
    public IReadOnlyCollection<InputOverlayJoystick> Joysticks => _joysticks.Values;

    [Serialized("Opacity")]
    public float Opacity { get; set; } = 0.8f;

    [Serialized("Control Size")]
    public float ControlSize { get; set; } = 72.0f;

    [Serialized("Font Size")]
    public float FontSize { get; set; } = 18.0f;

    public void SetFont(Font? font) => _font = font;
    public Font? GetFont() => _font;

    public void SetFontAttr(ResourceRef value)
    {
        _font = string.IsNullOrEmpty(value.Name)
            ? null
            : GetSubsystem<ResourceCache>()!.GetResourceOrNull<Font>(value.Name);
    }

    [Serialized("Font", DefaultSource = nameof(GetFontAttributeDefault), Setter = nameof(SetFontAttr))]
    public ResourceRef GetFontAttr() => ResourceType.GetResourceRef(_font, ObjectReflection<Font>.TypeId);

    [Serialized("Control Color")]
    public Color ControlColor { get; set; } = Color.White;

    public void SetInputMap(InputMap? inputMap)
    {
        if (ReferenceEquals(_inputMap, inputMap))
            return;
        if (_inputMap is not null)
        {
            foreach (var button in _buttons.Values)
                _inputMap.UnmapScreenButton(button.Action, button.Channel);
            foreach (var joystick in _joysticks.Values)
                UnmapJoystick(_inputMap, joystick);
        }
        _inputMap = inputMap;
        if (_inputMap is not null)
        {
            foreach (var button in _buttons.Values)
                _inputMap.MapScreenButton(button.Action, button.Channel);
            foreach (var joystick in _joysticks.Values)
                MapJoystick(_inputMap, joystick);
        }
    }
    public InputMap? GetInputMap() => _inputMap;

    public InputOverlayHint AddHint(string name, InputDeviceType deviceType, Texture2D? texture, string text,
        Vector2f anchor, Vector2f offset, InputOverlayAlignment alignment = InputOverlayAlignment.Center)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var hint = new InputOverlayHint
        {
            Name = name, DeviceType = deviceType, Texture = texture, Text = text,
            Anchor = anchor, Offset = offset, Alignment = alignment
        };
        _hints[name] = hint;
        return hint;
    }

    public bool RemoveHint(string name) => _hints.Remove(name);
    public void SetHintVisible(string name, bool visible) => _hints[name].Visible = visible;
    public bool IsHintVisible(string name)
    {
        var hint = _hints[name];
        return hint.Visible && hint.DeviceType == _input.ActiveInputDevice;
    }
    public void UpdateHint(string name, Texture2D? texture, string text)
    {
        var hint = _hints[name];
        hint.Texture = texture;
        hint.Text = text;
    }

    public InputOverlayButton AddButton(string name, string action, Texture2D? texture, string text,
        Vector2f anchor, Vector2f offset, InputOverlayAlignment alignment = InputOverlayAlignment.Center,
        Vector2f? size = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        RemoveButton(name);
        var channel = $"InputOverlay.{name}";
        var button = new InputOverlayButton
        {
            Name = name, Action = action, Channel = channel, Texture = texture, Text = text,
            Anchor = anchor, Offset = offset, Alignment = alignment,
            Size = size ?? new Vector2f(ControlSize, ControlSize)
        };
        _buttons[name] = button;
        _inputMap?.MapScreenButton(action, channel);
        return button;
    }

    public bool RemoveButton(string name)
    {
        if (!_buttons.Remove(name, out var button))
            return false;
        _input.SetScreenButton(button.Channel, false);
        _inputMap?.UnmapScreenButton(button.Action, button.Channel);
        return true;
    }

    public void SetButtonVisible(string name, bool visible)
    {
        var button = _buttons[name];
        button.Visible = visible;
        if (!visible)
            Release(button);
    }
    public bool IsButtonVisible(string name) => _buttons[name].Visible && _input.ActiveInputDevice == InputDeviceType.Touch;
    public void UpdateButton(string name, Texture2D? texture, string text)
    {
        var button = _buttons[name];
        button.Texture = texture;
        button.Text = text;
    }

    public InputOverlayJoystick AddJoystick(string name, string upAction, string downAction,
        string leftAction, string rightAction, Vector4f? activationRegion = null, float radius = 72.0f,
        float deadZone = 0.1f)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        RemoveJoystick(name);
        var joystick = new InputOverlayJoystick
        {
            Name = name,
            HorizontalChannel = $"InputOverlay.{name}.X",
            VerticalChannel = $"InputOverlay.{name}.Y",
            UpAction = upAction, DownAction = downAction, LeftAction = leftAction, RightAction = rightAction,
            ActivationRegion = activationRegion ?? new Vector4f(0.0f, 0.0f, 0.5f, 1.0f),
            Radius = MathF.Max(radius, 1.0f),
            DeadZone = MathDefs.Clamp(deadZone, 0.0f, 1.0f)
        };
        _joysticks[name] = joystick;
        if (_inputMap is not null)
        {
            MapJoystick(_inputMap, joystick);
        }
        return joystick;
    }

    public bool RemoveJoystick(string name)
    {
        if (!_joysticks.Remove(name, out var joystick))
            return false;
        Release(joystick);
        if (_inputMap is not null)
            UnmapJoystick(_inputMap, joystick);
        return true;
    }

    public void SetJoystickVisible(string name, bool visible)
    {
        var joystick = _joysticks[name];
        joystick.Visible = visible;
        if (!visible)
            Release(joystick);
    }
    public bool IsJoystickVisible(string name) => _joysticks[name].Visible && _input.ActiveInputDevice == InputDeviceType.Touch;
    public void UpdateJoystick(string name, Vector4f activationRegion, float radius, float deadZone)
    {
        var joystick = _joysticks[name];
        joystick.ActivationRegion = activationRegion;
        joystick.Radius = MathF.Max(radius, 1.0f);
        joystick.DeadZone = MathDefs.Clamp(deadZone, 0.0f, 1.0f);
    }

    public override void Dispose()
    {
        ResetControls();
        SetInputMap(null);
        _buttons.Clear();
        _joysticks.Clear();
        _hints.Clear();
        base.Dispose();
        GC.SuppressFinalize(this);
    }

    protected override void OnWorldBoundingBoxUpdate()
    {
        _worldBoundingBox = new BoundingBox(float.MinValue, float.MaxValue);
        _worldBoundingBoxDirty = false;
    }

    private void HandleTouchBegin(ref InputEvents.TouchBeginEvent value)
    {
        if (_input.ActiveInputDevice != InputDeviceType.Touch)
            return;
        var point = new Vector2f(value.X, value.Y);
        foreach (var button in _buttons.Values)
        {
            if (button.Visible && button.TouchId < 0 && HitTest(button, point))
            {
                button.TouchId = value.TouchID;
                _input.SetScreenButton(button.Channel, true);
                return;
            }
        }
        var size = _input.GetBackbufferSize();
        if (size.X <= 0 || size.Y <= 0)
            return;
        var normalized = new Vector2f(point.X / size.X, point.Y / size.Y);
        foreach (var joystick in _joysticks.Values)
        {
            var region = joystick.ActivationRegion;
            if (joystick.Visible && joystick.TouchId < 0 && normalized.X >= region.X && normalized.Y >= region.Y
                && normalized.X <= region.Z && normalized.Y <= region.W)
            {
                joystick.TouchId = value.TouchID;
                joystick.Origin = point;
                joystick.Position = point;
                return;
            }
        }
    }

    private void HandleTouchMove(ref InputEvents.TouchMoveEvent value)
    {
        var point = new Vector2f(value.X, value.Y);
        foreach (var button in _buttons.Values)
        {
            if (button.TouchId == value.TouchID)
                _input.SetScreenButton(button.Channel, button.Visible && HitTest(button, point));
        }
        foreach (var joystick in _joysticks.Values)
        {
            if (joystick.TouchId != value.TouchID)
                continue;
            var delta = point - joystick.Origin;
            var length = delta.Length;
            if (length > joystick.Radius)
                delta *= joystick.Radius / length;
            joystick.Position = joystick.Origin + delta;
            ApplyJoystick(joystick);
        }
    }

    private void HandleTouchEnd(ref InputEvents.TouchEndEvent value)
    {
        foreach (var button in _buttons.Values)
        {
            if (button.TouchId == value.TouchID)
                Release(button);
        }
        foreach (var joystick in _joysticks.Values)
        {
            if (joystick.TouchId == value.TouchID)
                Release(joystick);
        }
    }

    private void HandleInputDeviceChanged(ref InputEvents.InputDeviceChangedEvent value)
    {
        if (value.DeviceType != InputDeviceType.Touch)
            ResetControls();
    }

    private void HandleInputBegin(ref InputEvents.InputBeginEvent value)
    {
        foreach (var joystick in _joysticks.Values)
        {
            if (joystick.TouchId < 0)
                continue;
            ApplyJoystick(joystick);
        }
    }

    private void HandleInputFocus(ref InputEvents.InputFocusEvent value)
    {
        if (!value.Focus)
            ResetControls();
    }

    private bool HitTest(InputOverlayButton button, Vector2f point)
    {
        var scale = _input.SystemToBackbufferScale;
        var size = new Vector2f(button.Size.X * scale.X, button.Size.Y * scale.Y);
        var viewport = _input.GetBackbufferSize();
        var origin = new Vector2f(button.Anchor.X * viewport.X + button.Offset.X * scale.X,
            button.Anchor.Y * viewport.Y + button.Offset.Y * scale.Y);
        var alignment = GetAlignment(button.Alignment);
        origin -= new Vector2f(size.X * alignment.X, size.Y * alignment.Y);
        return point.X >= origin.X && point.Y >= origin.Y && point.X <= origin.X + size.X && point.Y <= origin.Y + size.Y;
    }

    private static Vector2f GetAlignment(InputOverlayAlignment alignment) => alignment switch
    {
        InputOverlayAlignment.TopLeft => new Vector2f(0.0f, 0.0f),
        InputOverlayAlignment.Top => new Vector2f(0.5f, 0.0f),
        InputOverlayAlignment.TopRight => new Vector2f(1.0f, 0.0f),
        InputOverlayAlignment.Left => new Vector2f(0.0f, 0.5f),
        InputOverlayAlignment.Center => new Vector2f(0.5f, 0.5f),
        InputOverlayAlignment.Right => new Vector2f(1.0f, 0.5f),
        InputOverlayAlignment.BottomLeft => new Vector2f(0.0f, 1.0f),
        InputOverlayAlignment.Bottom => new Vector2f(0.5f, 1.0f),
        _ => new Vector2f(1.0f, 1.0f)
    };

    private void ResetControls()
    {
        foreach (var button in _buttons.Values)
            Release(button);
        foreach (var joystick in _joysticks.Values)
            Release(joystick);
    }

    private void Release(InputOverlayButton button)
    {
        button.TouchId = -1;
        _input.SetScreenButton(button.Channel, false);
    }

    private void Release(InputOverlayJoystick joystick)
    {
        joystick.TouchId = -1;
        joystick.Position = joystick.Origin;
        _input.SetScreenAxis(joystick.HorizontalChannel, 0.0f);
        _input.SetScreenAxis(joystick.VerticalChannel, 0.0f);
    }

    private void ApplyJoystick(InputOverlayJoystick joystick)
    {
        var value = (joystick.Position - joystick.Origin) / joystick.Radius;
        if (value.Length <= joystick.DeadZone)
            value = Vector2f.Zero;
        _input.SetScreenAxis(joystick.HorizontalChannel, value.X);
        _input.SetScreenAxis(joystick.VerticalChannel, value.Y);
    }

    private static void MapJoystick(InputMap inputMap, InputOverlayJoystick joystick)
    {
        inputMap.MapScreenAxis(joystick.LeftAction, joystick.HorizontalChannel, 0.0f, -1.0f);
        inputMap.MapScreenAxis(joystick.RightAction, joystick.HorizontalChannel, 0.0f, 1.0f);
        inputMap.MapScreenAxis(joystick.UpAction, joystick.VerticalChannel, 0.0f, -1.0f);
        inputMap.MapScreenAxis(joystick.DownAction, joystick.VerticalChannel, 0.0f, 1.0f);
    }

    private static void UnmapJoystick(InputMap inputMap, InputOverlayJoystick joystick)
    {
        inputMap.UnmapScreenAxis(joystick.LeftAction, joystick.HorizontalChannel);
        inputMap.UnmapScreenAxis(joystick.RightAction, joystick.HorizontalChannel);
        inputMap.UnmapScreenAxis(joystick.UpAction, joystick.VerticalChannel);
        inputMap.UnmapScreenAxis(joystick.DownAction, joystick.VerticalChannel);
    }

    private static object? GetFontAttributeDefault() => new ResourceRef(ObjectReflection<Font>.TypeId);
}
