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
    private Input? input;
    private readonly AxisAdapter axisAdapter = new();
    private bool enabled;
    private DirectionAggregatorMask subscriptionMask = DirectionAggregatorMask.All;
    private Vector2 externalDirection = Vector2.Zero;
    private Vector2 touchDirection = Vector2.Zero;
    private object? uiElement;

    public DirectionAggregator(Input? input = null)
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

    public void SetSubscriptionMask(DirectionAggregatorMask mask)
    {
        subscriptionMask = mask;
    }

    public DirectionAggregatorMask GetSubscriptionMask() => subscriptionMask;

    public void SetUIElement(object? element)
    {
        uiElement = element;
    }

    public object? GetUIElement() => uiElement;

    public void SetDeadZone(float deadZone)
    {
        axisAdapter.SetDeadZone(deadZone);
    }

    public float GetDeadZone() => axisAdapter.GetDeadZone();

    public void SetExternalDirection(Vector2 direction)
    {
        externalDirection = direction;
    }

    public void SetTouchDirection(Vector2 direction)
    {
        touchDirection = direction;
    }

    public Vector2 GetDirection()
    {
        if (!enabled)
            return Vector2.Zero;

        var direction = externalDirection;
        if ((subscriptionMask & DirectionAggregatorMask.Touch) != 0)
            direction += touchDirection;
        if (input is not null)
        {
            if ((subscriptionMask & DirectionAggregatorMask.Keyboard) != 0)
                direction += GetKeyboardDirection();
            if ((subscriptionMask & DirectionAggregatorMask.Joystick) != 0)
                direction += GetJoystickDirection();
        }

        return ClampDirection(direction);
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

        var x = axisAdapter.Transform(input.GetControllerAxisPosition(ControllerAxis.LeftX));
        var y = axisAdapter.Transform(input.GetControllerAxisPosition(ControllerAxis.LeftY));
        x += axisAdapter.Transform(input.GetJoystickAxisPosition(0u));
        y += axisAdapter.Transform(input.GetJoystickAxisPosition(1u));

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

    private bool IsPressed(Scancode scancode, Key key)
    {
        return input is not null && (input.GetScancodeDown(scancode) || input.GetKeyDown(key));
    }

    private static Vector2 ClampDirection(Vector2 direction)
    {
        return new Vector2(
            MathDefs.Clamp(direction.X, -1.0f, 1.0f),
            MathDefs.Clamp(direction.Y, -1.0f, 1.0f));
    }
}
