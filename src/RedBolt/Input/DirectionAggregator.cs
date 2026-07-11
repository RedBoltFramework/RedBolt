namespace RedBolt;

[Flags]
public enum DirectionAggregatorMask : uint
{
    None = 0,
    Keyboard = 1u << 0,
    Joystick = 1u << 1,
    Touch = 1u << 2,
    All = Keyboard | Joystick | Touch
}

public sealed class DirectionAggregator : Object, IDisposable
{
    private enum InputType { Keyboard, JoystickAxis, JoystickDPad, Touch }
    private readonly record struct AxisState(InputType Input, int Key, float Value);

    private readonly List<AxisState> _horizontalAxis = [];
    private readonly List<AxisState> _verticalAxis = [];
    private readonly List<ContextEventSubscription> _subscriptions = [];
    private readonly AxisAdapter _axisAdapter = new();
    private Input? _input;
    private bool _enabled;
    private DirectionAggregatorMask _subscriptionMask = DirectionAggregatorMask.All;
    private Vector2f _externalDirection = Vector2f.Zero;
    private Vector2f _touchDirection = Vector2f.Zero;
    private object? _uiElement;
    private int? _activeTouchId;
    private Vector2i _touchOrigin;
    private const float TouchSensitivity = 2.0f / 96.0f;

    public DirectionAggregator(Input? input = null) => _input = input;

    public void Dispose()
    {
        ClearSubscriptions();
        GC.SuppressFinalize(this);
    }

    public void SetInput(Input? input)
    {
        if (ReferenceEquals(_input, input)) return;
        _input = input;
        UpdateSubscriptions();
    }

    public Input? GetInput() => _input;

    public void SetEnabled(bool enabled)
    {
        if (_enabled == enabled) return;
        _enabled = enabled;
        UpdateSubscriptions();
    }

    public bool IsEnabled() => _enabled;

    public void SetSubscriptionMask(DirectionAggregatorMask mask)
    {
        if (_subscriptionMask == mask) return;
        _subscriptionMask = mask;
        UpdateSubscriptions();
    }

    public DirectionAggregatorMask GetSubscriptionMask() => _subscriptionMask;
    public void SetUIElement(object? element) => _uiElement = element;
    public object? GetUIElement() => _uiElement;
    public void SetDeadZone(float deadZone) => _axisAdapter.SetDeadZone(deadZone);
    public float GetDeadZone() => _axisAdapter.GetDeadZone();
    public void SetExternalDirection(Vector2f direction) => _externalDirection = direction;
    public void SetTouchDirection(Vector2f direction) => _touchDirection = direction;

    public Vector2f GetDirection()
    {
        if (!_enabled) return Vector2f.Zero;
        var x = _horizontalAxis.Count != 0 ? _horizontalAxis.Average(static state => state.Value) : 0.0f;
        var y = _verticalAxis.Count != 0 ? _verticalAxis.Average(static state => state.Value) : 0.0f;
        var direction = new Vector2f(x, y) + _externalDirection + _touchDirection;
        return new Vector2f(MathDefs.Clamp(direction.X, -1.0f, 1.0f), MathDefs.Clamp(direction.Y, -1.0f, 1.0f));
    }

    private void UpdateSubscriptions()
    {
        ClearSubscriptions();
        _horizontalAxis.Clear();
        _verticalAxis.Clear();
        _activeTouchId = null;
        if (!_enabled || _input is null || Context.TryGetInstance() is null) return;

        _subscriptions.Add(SubscribeToEvent<InputEvents.InputFocusEvent>(HandleInputFocus));
        if ((_subscriptionMask & DirectionAggregatorMask.Keyboard) != 0)
        {
            _subscriptions.Add(SubscribeToEvent<InputEvents.KeyDownEvent>(HandleKeyDown));
            _subscriptions.Add(SubscribeToEvent<InputEvents.KeyUpEvent>(HandleKeyUp));
        }
        if ((_subscriptionMask & DirectionAggregatorMask.Joystick) != 0)
        {
            _subscriptions.Add(SubscribeToEvent<InputEvents.JoystickAxisMoveEvent>(HandleJoystickAxisMove));
            _subscriptions.Add(SubscribeToEvent<InputEvents.JoystickHatMoveEvent>(HandleJoystickHatMove));
            _subscriptions.Add(SubscribeToEvent<InputEvents.JoystickDisconnectedEvent>(HandleJoystickDisconnected));
        }
        if ((_subscriptionMask & DirectionAggregatorMask.Touch) != 0)
        {
            _subscriptions.Add(SubscribeToEvent<InputEvents.TouchBeginEvent>(HandleTouchBegin));
            _subscriptions.Add(SubscribeToEvent<InputEvents.TouchMoveEvent>(HandleTouchMove));
            _subscriptions.Add(SubscribeToEvent<InputEvents.TouchEndEvent>(HandleTouchEnd));
        }
    }

    private void ClearSubscriptions()
    {
        foreach (var subscription in _subscriptions) subscription.Dispose();
        _subscriptions.Clear();
    }

    private void HandleInputFocus(ref InputEvents.InputFocusEvent value)
    {
        if (!value.Focus) { _horizontalAxis.Clear(); _verticalAxis.Clear(); }
    }

    private void HandleKeyDown(ref InputEvents.KeyDownEvent value) => UpdateKey(value.Scancode, true);
    private void HandleKeyUp(ref InputEvents.KeyUpEvent value) => UpdateKey(value.Scancode, false);

    private void UpdateKey(Scancode scancode, bool down)
    {
        var value = down ? 1.0f : 0.0f;
        switch (scancode)
        {
            case Scancode.W or Scancode.Up: UpdateAxis(_verticalAxis, new(InputType.Keyboard, (int)scancode, -value)); break;
            case Scancode.S or Scancode.Down: UpdateAxis(_verticalAxis, new(InputType.Keyboard, (int)scancode, value)); break;
            case Scancode.A or Scancode.Left: UpdateAxis(_horizontalAxis, new(InputType.Keyboard, (int)scancode, -value)); break;
            case Scancode.D or Scancode.Right: UpdateAxis(_horizontalAxis, new(InputType.Keyboard, (int)scancode, value)); break;
        }
    }

    private void HandleJoystickAxisMove(ref InputEvents.JoystickAxisMoveEvent value)
    {
        if (value.Axis == 0) UpdateAxis(_horizontalAxis, new(InputType.JoystickAxis, value.JoystickID, value.Position));
        else if (value.Axis == 1) UpdateAxis(_verticalAxis, new(InputType.JoystickAxis, value.JoystickID, value.Position));
    }

    private void HandleJoystickHatMove(ref InputEvents.JoystickHatMoveEvent value)
    {
        if (value.Hat != 0) return;
        var x = ((value.Position & HatPosition.Right) != 0 ? 1.0f : 0.0f) - ((value.Position & HatPosition.Left) != 0 ? 1.0f : 0.0f);
        var y = ((value.Position & HatPosition.Down) != 0 ? 1.0f : 0.0f) - ((value.Position & HatPosition.Up) != 0 ? 1.0f : 0.0f);
        UpdateAxis(_horizontalAxis, new(InputType.JoystickDPad, value.JoystickID, x));
        UpdateAxis(_verticalAxis, new(InputType.JoystickDPad, value.JoystickID, y));
    }

    private void HandleJoystickDisconnected(ref InputEvents.JoystickDisconnectedEvent value)
    {
        var joystickId = value.JoystickID;
        _horizontalAxis.RemoveAll(state => state.Key == joystickId && state.Input is InputType.JoystickAxis or InputType.JoystickDPad);
        _verticalAxis.RemoveAll(state => state.Key == joystickId && state.Input is InputType.JoystickAxis or InputType.JoystickDPad);
    }

    private void HandleTouchBegin(ref InputEvents.TouchBeginEvent value)
    {
        if (_activeTouchId.HasValue) return;
        _activeTouchId = value.TouchID;
        _touchOrigin = new Vector2i(value.X, value.Y);
    }

    private void HandleTouchMove(ref InputEvents.TouchMoveEvent value)
    {
        if (_activeTouchId != value.TouchID) return;
        var position = new Vector2i(value.X, value.Y);
        var dx = MathDefs.Clamp((position.X - _touchOrigin.X) * TouchSensitivity, -1.0f, 1.0f);
        var dy = MathDefs.Clamp((position.Y - _touchOrigin.Y) * TouchSensitivity, -1.0f, 1.0f);
        _touchOrigin = new Vector2i(position.X - (int)(dx / TouchSensitivity), position.Y - (int)(dy / TouchSensitivity));
        UpdateAxis(_horizontalAxis, new(InputType.Touch, 0, dx));
        UpdateAxis(_verticalAxis, new(InputType.Touch, 0, dy));
    }

    private void HandleTouchEnd(ref InputEvents.TouchEndEvent value)
    {
        if (_activeTouchId != value.TouchID) return;
        _activeTouchId = null;
        UpdateAxis(_horizontalAxis, new(InputType.Touch, 0, 0.0f));
        UpdateAxis(_verticalAxis, new(InputType.Touch, 0, 0.0f));
    }

    private void UpdateAxis(List<AxisState> activeStates, AxisState state)
    {
        var adjustedValue = _axisAdapter.Transform(state.Value);
        var index = activeStates.FindIndex(value => value.Input == state.Input && value.Key == state.Key);
        if (index >= 0)
        {
            if (adjustedValue == 0.0f) activeStates.RemoveAt(index);
            else activeStates[index] = state with { Value = adjustedValue };
        }
        else if (adjustedValue != 0.0f)
            activeStates.Add(state with { Value = adjustedValue });
    }
}
