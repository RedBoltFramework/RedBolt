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

public sealed class DirectionAggregator
{
    private Input? _input;
    private readonly AxisAdapter _axisAdapter = new();
    private bool _enabled;
    private DirectionAggregatorMask _subscriptionMask = DirectionAggregatorMask.All;
    private Vector2f _externalDirection = Vector2f.Zero;
    private Vector2f _touchDirection = Vector2f.Zero;
    private object? _uiElement;

    public DirectionAggregator(Input? input = null)
    {
        this._input = input;
    }

    public void SetInput(Input? input)
    {
        this._input = input;
    }

    public Input? GetInput() => _input;

    public void SetEnabled(bool enabled)
    {
        this._enabled = enabled;
    }

    public bool IsEnabled() => _enabled;

    public void SetSubscriptionMask(DirectionAggregatorMask mask)
    {
        _subscriptionMask = mask;
    }

    public DirectionAggregatorMask GetSubscriptionMask() => _subscriptionMask;

    public void SetUIElement(object? element)
    {
        _uiElement = element;
    }

    public object? GetUIElement() => _uiElement;

    public void SetDeadZone(float deadZone)
    {
        _axisAdapter.SetDeadZone(deadZone);
    }

    public float GetDeadZone() => _axisAdapter.GetDeadZone();

    public void SetExternalDirection(Vector2f direction)
    {
        _externalDirection = direction;
    }

    public void SetTouchDirection(Vector2f direction)
    {
        _touchDirection = direction;
    }

    public Vector2f GetDirection()
    {
        if (!_enabled)
            return Vector2f.Zero;

        var direction = _externalDirection;
        if ((_subscriptionMask & DirectionAggregatorMask.Touch) != 0)
            direction += _touchDirection;
        if (_input is not null)
        {
            if ((_subscriptionMask & DirectionAggregatorMask.Keyboard) != 0)
                direction += GetKeyboardDirection();
            if ((_subscriptionMask & DirectionAggregatorMask.Joystick) != 0)
                direction += GetJoystickDirection();
        }

        return ClampDirection(direction);
    }

    private Vector2f GetKeyboardDirection()
    {
        var x = 0.0f;
        var y = 0.0f;
        if (IsPressed(Scancode.D, Key.Right))
            x += 1.0f;
        if (IsPressed(Scancode.A, Key.Left))
            x -= 1.0f;
        if (IsPressed(Scancode.S, Key.Down))
            y += 1.0f;
        if (IsPressed(Scancode.W, Key.Up))
            y -= 1.0f;
        return new Vector2f(x, y);
    }

    private Vector2f GetJoystickDirection()
    {
        if (_input is null)
            return Vector2f.Zero;

        var x = _axisAdapter.Transform(_input.GetControllerAxisPosition(ControllerAxis.LeftX));
        var y = _axisAdapter.Transform(_input.GetControllerAxisPosition(ControllerAxis.LeftY));
        x += _axisAdapter.Transform(_input.GetJoystickAxisPosition(0u));
        y += _axisAdapter.Transform(_input.GetJoystickAxisPosition(1u));

        if (_input.GetHatDown(HatPosition.Right))
            x += 1.0f;
        if (_input.GetHatDown(HatPosition.Left))
            x -= 1.0f;
        if (_input.GetHatDown(HatPosition.Down))
            y += 1.0f;
        if (_input.GetHatDown(HatPosition.Up))
            y -= 1.0f;

        return new Vector2f(x, y);
    }

    private bool IsPressed(Scancode scancode, Key key)
    {
        return _input is not null && (_input.GetScancodeDown(scancode) || _input.GetKeyDown(key));
    }

    private static Vector2f ClampDirection(Vector2f direction)
    {
        return new Vector2f(
            MathDefs.Clamp(direction.X, -1.0f, 1.0f),
            MathDefs.Clamp(direction.Y, -1.0f, 1.0f));
    }
}
