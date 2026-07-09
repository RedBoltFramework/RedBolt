namespace RedBolt;

[Flags]
public enum DirectionalPadAdapterMask : uint
{
    None = 0,
    Keyboard = 1u << 0,
    Joystick = 1u << 1,
    KeyRepeat = 1u << 2,
    All = Keyboard | Joystick | KeyRepeat
}

public sealed class DirectionalPadAdapter
{
    private Input? _input;
    private bool _enabled;
    private DirectionalPadAdapterMask _subscriptionMask = DirectionalPadAdapterMask.All;
    private float _axisUpperThreshold = 0.6f;
    private float _axisLowerThreshold = 0.4f;
    private float _repeatDelay = 0.5f;
    private float _repeatInterval = 0.03f;
    private Vector2 _externalDirection = Vector2.Zero;

    public DirectionalPadAdapter(Input? input = null)
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

    public void SetSubscriptionMask(DirectionalPadAdapterMask mask)
    {
        _subscriptionMask = mask;
    }

    public DirectionalPadAdapterMask GetSubscriptionMask() => _subscriptionMask;

    public void SetAxisUpperThreshold(float threshold)
    {
        _axisUpperThreshold = threshold;
    }

    public float GetAxisUpperThreshold() => _axisUpperThreshold;

    public void SetAxisLowerThreshold(float threshold)
    {
        _axisLowerThreshold = threshold;
    }

    public float GetAxisLowerThreshold() => _axisLowerThreshold;

    public void SetRepeatDelay(float delayInSeconds)
    {
        _repeatDelay = Math.Max(delayInSeconds, 0.0f);
    }

    public float GetRepeatDelay() => _repeatDelay;

    public void SetRepeatInterval(float intervalInSeconds)
    {
        _repeatInterval = Math.Max(intervalInSeconds, 0.0f);
    }

    public float GetRepeatInterval() => _repeatInterval;

    public void SetExternalDirection(Vector2 direction)
    {
        _externalDirection = direction;
    }

    public bool GetKeyDown(Key key)
    {
        return key switch
        {
            Key.Up => GetScancodeDown(Scancode.Up),
            Key.Down => GetScancodeDown(Scancode.Down),
            Key.Left => GetScancodeDown(Scancode.Left),
            Key.Right => GetScancodeDown(Scancode.Right),
            _ => false
        };
    }

    public bool GetScancodeDown(Scancode scancode)
    {
        if (!_enabled)
            return false;

        return scancode switch
        {
            Scancode.Up => IsUpActive(),
            Scancode.Down => IsDownActive(),
            Scancode.Left => IsLeftActive(),
            Scancode.Right => IsRightActive(),
            _ => false
        };
    }

    private Vector2 GetKeyboardDirection()
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
        return new Vector2(x, y);
    }

    private Vector2 GetJoystickDirection()
    {
        if (_input is null)
            return Vector2.Zero;

        var x = 0.0f;
        var y = 0.0f;
        x += AxisToDirection(_input.GetControllerAxisPosition(ControllerAxis.LeftX));
        y += AxisToDirection(_input.GetControllerAxisPosition(ControllerAxis.LeftY));
        x += AxisToDirection(_input.GetJoystickAxisPosition(0u));
        y += AxisToDirection(_input.GetJoystickAxisPosition(1u));

        if (_input.GetHatDown(HatPosition.Right))
            x += 1.0f;
        if (_input.GetHatDown(HatPosition.Left))
            x -= 1.0f;
        if (_input.GetHatDown(HatPosition.Down))
            y += 1.0f;
        if (_input.GetHatDown(HatPosition.Up))
            y -= 1.0f;
        return new Vector2(x, y);
    }

    private bool IsRightActive()
    {
        return _externalDirection.X > _axisLowerThreshold
            || ((_subscriptionMask & DirectionalPadAdapterMask.Keyboard) != 0 && IsPressed(Scancode.D, Key.Right))
            || ((_subscriptionMask & DirectionalPadAdapterMask.Joystick) != 0 && _input is not null &&
                (_input.GetControllerAxisPosition(ControllerAxis.LeftX) >= _axisUpperThreshold
                 || _input.GetJoystickAxisPosition(0u) >= _axisUpperThreshold
                 || _input.GetHatDown(HatPosition.Right)));
    }

    private bool IsLeftActive()
    {
        return _externalDirection.X < -_axisLowerThreshold
            || ((_subscriptionMask & DirectionalPadAdapterMask.Keyboard) != 0 && IsPressed(Scancode.A, Key.Left))
            || ((_subscriptionMask & DirectionalPadAdapterMask.Joystick) != 0 && _input is not null &&
                (_input.GetControllerAxisPosition(ControllerAxis.LeftX) <= -_axisUpperThreshold
                 || _input.GetJoystickAxisPosition(0u) <= -_axisUpperThreshold
                 || _input.GetHatDown(HatPosition.Left)));
    }

    private bool IsUpActive()
    {
        return _externalDirection.Y < -_axisLowerThreshold
            || ((_subscriptionMask & DirectionalPadAdapterMask.Keyboard) != 0 && IsPressed(Scancode.W, Key.Up))
            || ((_subscriptionMask & DirectionalPadAdapterMask.Joystick) != 0 && _input is not null &&
                (_input.GetControllerAxisPosition(ControllerAxis.LeftY) <= -_axisUpperThreshold
                 || _input.GetJoystickAxisPosition(1u) <= -_axisUpperThreshold
                 || _input.GetHatDown(HatPosition.Up)));
    }

    private bool IsDownActive()
    {
        return _externalDirection.Y > _axisLowerThreshold
            || ((_subscriptionMask & DirectionalPadAdapterMask.Keyboard) != 0 && IsPressed(Scancode.S, Key.Down))
            || ((_subscriptionMask & DirectionalPadAdapterMask.Joystick) != 0 && _input is not null &&
                (_input.GetControllerAxisPosition(ControllerAxis.LeftY) >= _axisUpperThreshold
                 || _input.GetJoystickAxisPosition(1u) >= _axisUpperThreshold
                 || _input.GetHatDown(HatPosition.Down)));
    }

    private float AxisToDirection(float axis)
    {
        if (axis >= _axisUpperThreshold)
            return 1.0f;
        if (axis <= -_axisUpperThreshold)
            return -1.0f;
        return 0.0f;
    }

    private bool IsPressed(Scancode scancode, Key key)
    {
        return _input is not null && (_input.GetScancodeDown(scancode) || _input.GetKeyDown(key));
    }
}
