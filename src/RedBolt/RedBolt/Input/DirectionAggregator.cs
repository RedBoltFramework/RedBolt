using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.Math;

namespace RedBolt.Input;

[Flags]
public enum DirectionAggregatorMask : uint
{
    None = 0,
    Keyboard = 1 << 0,
    Joystick = 1 << 1,
    Touch = 1 << 2,
    All = Keyboard | Joystick | Touch,
}

/// <summary>Class to aggregate all movement inputs into a single direction vector.</summary>
[Reflected]
public partial class DirectionAggregator : RedObject
{
    /// <summary>Type of input source.</summary>
    private enum InputType
    {
        // External input
        External = 0,
        // Keyboard input (WASD and arrows)
        Keyboard = 1,
        // Touch input
        Touch = 2,
        // Joystick Axis
        JoystickAxis = 3,
        // Joystick DPad (hat)
        JoystickDPad = 4,
    }

    // State of an axis
    private struct AxisState
    {
        // Type of input device
        public InputType Input;
        // Additional input information: Key scan code, joystick id etc.
        public uint Key;
        // Value to accumulate
        public float Value;
    }

    /// <summary>Is aggregator enabled</summary>
    private bool _enabled;
    /// <summary>Enabled subscriptions</summary>
    private DirectionAggregatorMask _enabledSubscriptions = DirectionAggregatorMask.All;
    /// <summary>Active subscriptions bitmask</summary>
    private DirectionAggregatorMask _subscriptionFlags = DirectionAggregatorMask.None;
    /// <summary>Cached input subsystem pointer</summary>
    private readonly InputSystem _input;
    /// <summary>Collection of active vertical axis inputs</summary>
    private readonly List<AxisState> _verticalAxis = new(4);
    /// <summary>Collection of active horizontal axis inputs</summary>
    private readonly List<AxisState> _horizontalAxis = new(4);
    /// <summary>Joystick axis adapter</summary>
    private readonly AxisAdapter _axisAdapter = new();
    /// <summary>Joystick to ignore (SDL gyroscope virtual joystick)</summary>
    private int _ignoreJoystickId = -1;
    /// <summary>UI element to filter touch events</summary>
    private readonly WeakReference<object> _uiElement = new(null!);
    /// <summary>Identifier of active touch</summary>
    private int? _activeTouchId;
    /// <summary>Origin of the touch</summary>
    private Vector2i _touchOrigin;
    /// <summary>Touch sensitivity</summary>
    private float _touchSensitivity;

    /// <summary>Construct.</summary>
    public DirectionAggregator()
    {
        _input = GetSubsystem<InputSystem>()!;

        // By default the full axis range is about 1 inch
        var graphics = GetSubsystem<GraphicsSystem>();
        if (graphics is not null && graphics.GetDisplayDPI().X > 0)
            _touchSensitivity = 2.0f / graphics.GetDisplayDPI().X;
        else
            _touchSensitivity = 2.0f / 96.0f;

        _ignoreJoystickId = _input.FindAccelerometerJoystickId();
    }

    /// <summary>Destruct.</summary>
    public override void Dispose()
    {
        base.Dispose();
    }

    /// <summary>Set enabled flag. The object subscribes for events when enabled.</summary>
    public void SetEnabled(bool enabled)
    {
        if (_enabled != enabled)
        {
            _enabled = enabled;
            if (_enabled)
            {
                UpdateSubscriptions(_enabledSubscriptions);
            }
            else
            {
                UpdateSubscriptions(DirectionAggregatorMask.None);
            }
        }
    }

    /// <summary>Set input device subscription mask.</summary>
    public void SetSubscriptionMask(DirectionAggregatorMask mask)
    {
        _enabledSubscriptions = mask;
        if (IsEnabled())
            UpdateSubscriptions(_enabledSubscriptions);
    }

    /// <summary>Set UI element to filter touch events. Only touch events originated in the element going to be handled.</summary>
    public void SetUIElement(object? element) => _uiElement.SetTarget(element!);

    /// <summary>Set dead zone to mitigate axis drift.</summary>
    public void SetDeadZone(float deadZone) => _axisAdapter.SetDeadZone(deadZone);

    /// <summary>Get enabled flag.</summary>
    public bool IsEnabled() => _enabled;

    /// <summary>Get input device subscription mask.</summary>
    public DirectionAggregatorMask GetSubscriptionMask() => _enabledSubscriptions;

    /// <summary>Get UI element to filter touch events.</summary>
    public object? GetUIElement() => _uiElement.TryGetTarget(out var element) ? element : null;

    /// <summary>Get dead zone.</summary>
    public float GetDeadZone() => _axisAdapter.GetDeadZone();

    /// <summary>Get aggregated direction vector with X pointing right and Y pointing down (similar to gamepad axis).</summary>
    public Vector2f GetDirection()
    {
        var result = Vector2f.Zero;

        if (_horizontalAxis.Count != 0)
        {
            foreach (var activeState in _horizontalAxis)
                result.X += activeState.Value;
            result.X *= 1.0f / _horizontalAxis.Count;
        }
        if (_verticalAxis.Count != 0)
        {
            foreach (var activeState in _verticalAxis)
                result.Y += activeState.Value;
            result.Y *= 1.0f / _verticalAxis.Count;
        }

        return result;
    }

    private void UpdateSubscriptions(DirectionAggregatorMask flags)
    {
        var toSubscribe = flags & ~_subscriptionFlags;
        var toUnsubscribe = _subscriptionFlags & ~flags;

        if (_subscriptionFlags == DirectionAggregatorMask.None && flags != DirectionAggregatorMask.None)
        {
            SubscribeToEvent<InputEvents.InputFocusEvent>(_input, HandleInputFocus);
        }
        else if (_subscriptionFlags != DirectionAggregatorMask.None && flags == DirectionAggregatorMask.None)
        {
            UnsubscribeFromEvent<InputEvents.InputFocusEvent>(_input);
        }

        _subscriptionFlags = flags;
        if ((toSubscribe & DirectionAggregatorMask.Keyboard) != 0)
        {
            SubscribeToEvent<InputEvents.KeyUpEvent>(_input, HandleKeyUp);
            SubscribeToEvent<InputEvents.KeyDownEvent>(_input, HandleKeyDown);
        }
        else if ((toUnsubscribe & DirectionAggregatorMask.Keyboard) != 0)
        {
            UnsubscribeFromEvent<InputEvents.KeyUpEvent>();
            UnsubscribeFromEvent<InputEvents.KeyDownEvent>();

            _horizontalAxis.RemoveAll(static key => key.Input == InputType.Keyboard);
            _verticalAxis.RemoveAll(static key => key.Input == InputType.Keyboard);
        }
        if ((toSubscribe & DirectionAggregatorMask.Joystick) != 0)
        {
            SubscribeToEvent<InputEvents.JoystickAxisMoveEvent>(_input, HandleJoystickAxisMove);
            SubscribeToEvent<InputEvents.JoystickHatMoveEvent>(_input, HandleJoystickHatMove);
            SubscribeToEvent<InputEvents.JoystickDisconnectedEvent>(_input, HandleJoystickDisconnected);
        }
        else if ((toUnsubscribe & DirectionAggregatorMask.Joystick) != 0)
        {
            UnsubscribeFromEvent<InputEvents.JoystickAxisMoveEvent>();
            UnsubscribeFromEvent<InputEvents.JoystickHatMoveEvent>();
            UnsubscribeFromEvent<InputEvents.JoystickDisconnectedEvent>();

            _horizontalAxis.RemoveAll(static key => key.Input >= InputType.JoystickAxis);
            _verticalAxis.RemoveAll(static key => key.Input >= InputType.JoystickAxis);
        }
        if ((toSubscribe & DirectionAggregatorMask.Touch) != 0)
        {
            SubscribeToEvent<InputEvents.TouchBeginEvent>(_input, HandleTouchBegin);
            SubscribeToEvent<InputEvents.TouchMoveEvent>(_input, HandleTouchMove);
            SubscribeToEvent<InputEvents.TouchEndEvent>(_input, HandleTouchEnd);
        }
        else if ((toUnsubscribe & DirectionAggregatorMask.Touch) != 0)
        {
            UnsubscribeFromEvent<InputEvents.TouchBeginEvent>();
            UnsubscribeFromEvent<InputEvents.TouchMoveEvent>();
            UnsubscribeFromEvent<InputEvents.TouchEndEvent>();

            _horizontalAxis.RemoveAll(static key => key.Input == InputType.Touch);
            _verticalAxis.RemoveAll(static key => key.Input == InputType.Touch);
            _activeTouchId = null;
        }
    }

    private void HandleInputFocus(ref InputEvents.InputFocusEvent value)
    {
        if (!value.Focus)
        {
            _horizontalAxis.Clear();
            _verticalAxis.Clear();
        }
    }

    private void HandleKeyDown(ref InputEvents.KeyDownEvent value)
    {
        var scancode = (uint)value.Scancode;
        switch (value.Scancode)
        {
            case Scancode.W:
            case Scancode.Up: UpdateAxis(_verticalAxis, new AxisState { Input = InputType.Keyboard, Key = scancode, Value = -1.0f }); break;
            case Scancode.S:
            case Scancode.Down: UpdateAxis(_verticalAxis, new AxisState { Input = InputType.Keyboard, Key = scancode, Value = 1.0f }); break;
            case Scancode.A:
            case Scancode.Left: UpdateAxis(_horizontalAxis, new AxisState { Input = InputType.Keyboard, Key = scancode, Value = -1.0f }); break;
            case Scancode.D:
            case Scancode.Right: UpdateAxis(_horizontalAxis, new AxisState { Input = InputType.Keyboard, Key = scancode, Value = 1.0f }); break;
        }
    }

    private void HandleKeyUp(ref InputEvents.KeyUpEvent value)
    {
        var scancode = (uint)value.Scancode;
        switch (value.Scancode)
        {
            case Scancode.W:
            case Scancode.Up: UpdateAxis(_verticalAxis, new AxisState { Input = InputType.Keyboard, Key = scancode, Value = 0.0f }); break;
            case Scancode.S:
            case Scancode.Down: UpdateAxis(_verticalAxis, new AxisState { Input = InputType.Keyboard, Key = scancode, Value = 0.0f }); break;
            case Scancode.A:
            case Scancode.Left: UpdateAxis(_horizontalAxis, new AxisState { Input = InputType.Keyboard, Key = scancode, Value = 0.0f }); break;
            case Scancode.D:
            case Scancode.Right: UpdateAxis(_horizontalAxis, new AxisState { Input = InputType.Keyboard, Key = scancode, Value = 0.0f }); break;
        }
    }

    private void HandleJoystickAxisMove(ref InputEvents.JoystickAxisMoveEvent value)
    {
        var joystickId = (uint)value.JoystickID;
        if (value.JoystickID == _ignoreJoystickId)
            return;

        var axisIndex = (uint)value.Axis;
        var axisValue = value.Position;

        // Up-Down
        if (axisIndex == 1)
        {
            UpdateAxis(_verticalAxis, new AxisState { Input = InputType.JoystickAxis, Key = joystickId, Value = axisValue });
        }
        // Left-Right
        if (axisIndex == 0)
        {
            UpdateAxis(_horizontalAxis, new AxisState { Input = InputType.JoystickAxis, Key = joystickId, Value = axisValue });
        }
    }

    private void HandleJoystickHatMove(ref InputEvents.JoystickHatMoveEvent value)
    {
        var hatIndex = (uint)value.Hat;
        if (hatIndex != 0)
            return;

        var joystickId = (uint)value.JoystickID;
        var position = value.Position;

        var horizontalValue = ((position & HatPosition.Right) != 0 ? 1.0f : 0.0f) + ((position & HatPosition.Left) != 0 ? -1.0f : 0.0f);
        UpdateAxis(_horizontalAxis, new AxisState { Input = InputType.JoystickDPad, Key = joystickId, Value = horizontalValue });
        var verticalValue = ((position & HatPosition.Down) != 0 ? 1.0f : 0.0f) + ((position & HatPosition.Up) != 0 ? -1.0f : 0.0f);
        UpdateAxis(_verticalAxis, new AxisState { Input = InputType.JoystickDPad, Key = joystickId, Value = verticalValue });
    }

    private void HandleJoystickDisconnected(ref InputEvents.JoystickDisconnectedEvent value)
    {
        var joystickId = (uint)value.JoystickID;

        // Cancel Axis states.
        var joyEventId = (InputType)(uint)InputType.JoystickAxis;
        UpdateAxis(_verticalAxis, new AxisState { Input = joyEventId, Key = joystickId, Value = 0.0f });
        UpdateAxis(_horizontalAxis, new AxisState { Input = joyEventId, Key = joystickId, Value = 0.0f });

        // Cancel DPad states.
        var dpadEventId = (InputType)(uint)InputType.JoystickDPad;
        UpdateAxis(_verticalAxis, new AxisState { Input = dpadEventId, Key = joystickId, Value = 0.0f });
        UpdateAxis(_horizontalAxis, new AxisState { Input = dpadEventId, Key = joystickId, Value = 0.0f });
    }

    private void HandleTouchBegin(ref InputEvents.TouchBeginEvent value)
    {
        // Do nothing if already is tracking touch
        if (_activeTouchId.HasValue)
            return;

        // Start tracking touch
        var touchState = _input.GetTouchById(value.TouchID);
        if (touchState.HasValue && ReferenceEquals(touchState.Value.TouchedElement, GetUIElement()))
        {
            _activeTouchId = touchState.Value.TouchID;
            _touchOrigin = new Vector2i(value.X, value.Y);
        }
    }

    private void HandleTouchMove(ref InputEvents.TouchMoveEvent value)
    {
        // Do nothing if not tracking touch
        if (!_activeTouchId.HasValue)
            return;
        // Validate touch id
        var touchId = value.TouchID;
        if (touchId != _activeTouchId.Value)
            return;

        var position = new Vector2i(value.X, value.Y);

        var dx = MathDefs.Clamp((position.X - _touchOrigin.X) * _touchSensitivity, -1.0f, 1.0f);
        var dy = MathDefs.Clamp((position.Y - _touchOrigin.Y) * _touchSensitivity, -1.0f, 1.0f);

        // Reset origin position if touch left the -1..1 range
        _touchOrigin.X = position.X - (int)(dx / _touchSensitivity);
        _touchOrigin.Y = position.Y - (int)(dy / _touchSensitivity);

        UpdateAxis(_horizontalAxis, new AxisState { Input = InputType.Touch, Key = 0, Value = dx });
        UpdateAxis(_verticalAxis, new AxisState { Input = InputType.Touch, Key = 0, Value = dy });
    }

    private void HandleTouchEnd(ref InputEvents.TouchEndEvent value)
    {
        // Do nothing if not tracking touch
        if (!_activeTouchId.HasValue)
            return;
        // Stop tracking touch
        var touchId = value.TouchID;
        if (touchId != _activeTouchId.Value)
            return;

        _activeTouchId = null;
        UpdateAxis(_horizontalAxis, new AxisState { Input = InputType.Touch, Key = 0, Value = 0.0f });
        UpdateAxis(_verticalAxis, new AxisState { Input = InputType.Touch, Key = 0, Value = 0.0f });
    }

    private void UpdateAxis(List<AxisState> activeStates, AxisState state)
    {
        // Apply dead zone
        var adjustedValue = _axisAdapter.Transform(state.Value);

        // Find and replace value
        for (var i = 0; i < activeStates.Count; ++i)
        {
            if (activeStates[i].Input == state.Input && activeStates[i].Key == state.Key)
            {
                if (adjustedValue == 0.0f)
                {
                    activeStates[i] = activeStates[^1];
                    activeStates.RemoveAt(activeStates.Count - 1);
                }
                else
                {
                    var activeState = activeStates[i];
                    activeState.Value = adjustedValue;
                    activeStates[i] = activeState;
                }
                return;
            }
        }
        // Add value if not found
        if (adjustedValue != 0.0f)
            activeStates.Add(new AxisState { Input = state.Input, Key = state.Key, Value = adjustedValue });
    }
}
