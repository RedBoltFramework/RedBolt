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
    private Input? input;
    private bool enabled;
    private DirectionalPadAdapterMask subscriptionMask = DirectionalPadAdapterMask.All;
    private float axisUpperThreshold = 0.6f;
    private float axisLowerThreshold = 0.4f;
    private float repeatDelay = 0.5f;
    private float repeatInterval = 0.03f;
    private Vector2 externalDirection = Vector2.Zero;

    public DirectionalPadAdapter(Input? input = null)
    {
        this.input = input;
    }

    public void SetInput(Input? input)
    {
        this.input = input;
    }

    public Input? GetInput() => input;

    public void SetEnabled(bool enabled)
    {
        this.enabled = enabled;
    }

    public bool IsEnabled() => enabled;

    public void SetSubscriptionMask(DirectionalPadAdapterMask mask)
    {
        subscriptionMask = mask;
    }

    public DirectionalPadAdapterMask GetSubscriptionMask() => subscriptionMask;

    public void SetAxisUpperThreshold(float threshold)
    {
        axisUpperThreshold = threshold;
    }

    public float GetAxisUpperThreshold() => axisUpperThreshold;

    public void SetAxisLowerThreshold(float threshold)
    {
        axisLowerThreshold = threshold;
    }

    public float GetAxisLowerThreshold() => axisLowerThreshold;

    public void SetRepeatDelay(float delayInSeconds)
    {
        repeatDelay = Math.Max(delayInSeconds, 0.0f);
    }

    public float GetRepeatDelay() => repeatDelay;

    public void SetRepeatInterval(float intervalInSeconds)
    {
        repeatInterval = Math.Max(intervalInSeconds, 0.0f);
    }

    public float GetRepeatInterval() => repeatInterval;

    public void SetExternalDirection(Vector2 direction)
    {
        externalDirection = direction;
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
        if (!enabled)
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
        if (input is null)
            return Vector2.Zero;

        var x = 0.0f;
        var y = 0.0f;
        x += AxisToDirection(input.GetControllerAxisPosition(ControllerAxis.LeftX));
        y += AxisToDirection(input.GetControllerAxisPosition(ControllerAxis.LeftY));
        x += AxisToDirection(input.GetJoystickAxisPosition(0u));
        y += AxisToDirection(input.GetJoystickAxisPosition(1u));

        if (input.GetHatDown(HatPosition.Right))
            x += 1.0f;
        if (input.GetHatDown(HatPosition.Left))
            x -= 1.0f;
        if (input.GetHatDown(HatPosition.Down))
            y += 1.0f;
        if (input.GetHatDown(HatPosition.Up))
            y -= 1.0f;
        return new Vector2(x, y);
    }

    private bool IsRightActive()
    {
        return externalDirection.X > axisLowerThreshold
            || ((subscriptionMask & DirectionalPadAdapterMask.Keyboard) != 0 && IsPressed(Scancode.D, Key.Right))
            || ((subscriptionMask & DirectionalPadAdapterMask.Joystick) != 0 && input is not null &&
                (input.GetControllerAxisPosition(ControllerAxis.LeftX) >= axisUpperThreshold
                 || input.GetJoystickAxisPosition(0u) >= axisUpperThreshold
                 || input.GetHatDown(HatPosition.Right)));
    }

    private bool IsLeftActive()
    {
        return externalDirection.X < -axisLowerThreshold
            || ((subscriptionMask & DirectionalPadAdapterMask.Keyboard) != 0 && IsPressed(Scancode.A, Key.Left))
            || ((subscriptionMask & DirectionalPadAdapterMask.Joystick) != 0 && input is not null &&
                (input.GetControllerAxisPosition(ControllerAxis.LeftX) <= -axisUpperThreshold
                 || input.GetJoystickAxisPosition(0u) <= -axisUpperThreshold
                 || input.GetHatDown(HatPosition.Left)));
    }

    private bool IsUpActive()
    {
        return externalDirection.Y < -axisLowerThreshold
            || ((subscriptionMask & DirectionalPadAdapterMask.Keyboard) != 0 && IsPressed(Scancode.W, Key.Up))
            || ((subscriptionMask & DirectionalPadAdapterMask.Joystick) != 0 && input is not null &&
                (input.GetControllerAxisPosition(ControllerAxis.LeftY) <= -axisUpperThreshold
                 || input.GetJoystickAxisPosition(1u) <= -axisUpperThreshold
                 || input.GetHatDown(HatPosition.Up)));
    }

    private bool IsDownActive()
    {
        return externalDirection.Y > axisLowerThreshold
            || ((subscriptionMask & DirectionalPadAdapterMask.Keyboard) != 0 && IsPressed(Scancode.S, Key.Down))
            || ((subscriptionMask & DirectionalPadAdapterMask.Joystick) != 0 && input is not null &&
                (input.GetControllerAxisPosition(ControllerAxis.LeftY) >= axisUpperThreshold
                 || input.GetJoystickAxisPosition(1u) >= axisUpperThreshold
                 || input.GetHatDown(HatPosition.Down)));
    }

    private float AxisToDirection(float axis)
    {
        if (axis >= axisUpperThreshold)
            return 1.0f;
        if (axis <= -axisUpperThreshold)
            return -1.0f;
        return 0.0f;
    }

    private bool IsPressed(Scancode scancode, Key key)
    {
        return input is not null && (input.GetScancodeDown(scancode) || input.GetKeyDown(key));
    }
}
