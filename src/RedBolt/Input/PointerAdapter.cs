namespace RedBolt;

[Flags]
public enum PointerAdapterMask : uint
{
    None = 0,
    Mouse = 1u << 0,
    Touch = 1u << 1,
    Keyboard = 1u << 2,
    Joystick = 1u << 3,
    All = Touch | Mouse | Keyboard | Joystick
}

public sealed class PointerAdapter
{
    private readonly DirectionAggregator directionAdapter;
    private Input? input;
    private bool enabled;
    private PointerAdapterMask subscriptionMask = PointerAdapterMask.All;
    private Vector2 pointerPosition = Vector2.Zero;
    private bool pointerPressed;
    private float cursorSpeed;
    private float maxCursorSpeed = 100.0f;
    private float cursorAcceleration = 1.0f;

    public PointerAdapter(Input? input = null)
    {
        this.input = input;
        directionAdapter = new DirectionAggregator(input);
    }

    public void SetInput(Input? input)
    {
        this.input = input;
        directionAdapter.SetInput(input);
    }

    public Input? GetInput() => input;

    public void SetEnabled(bool enabled)
    {
        this.enabled = enabled;
        directionAdapter.SetEnabled(enabled);
    }

    public bool IsEnabled() => enabled;

    public void SetSubscriptionMask(PointerAdapterMask mask)
    {
        subscriptionMask = mask;

        var directionMask = DirectionAggregatorMask.None;
        if ((mask & PointerAdapterMask.Keyboard) != 0)
            directionMask |= DirectionAggregatorMask.Keyboard;
        if ((mask & PointerAdapterMask.Joystick) != 0)
            directionMask |= DirectionAggregatorMask.Joystick;
        if ((mask & PointerAdapterMask.Touch) != 0)
            directionMask |= DirectionAggregatorMask.Touch;
        directionAdapter.SetSubscriptionMask(directionMask);
    }

    public PointerAdapterMask GetSubscriptionMask() => subscriptionMask;

    public void SetUIElement(object? element)
    {
        directionAdapter.SetUIElement(element);
    }

    public object? GetUIElement() => directionAdapter.GetUIElement();

    public void SetCursorSpeed(float cursorSpeed)
    {
        maxCursorSpeed = Math.Max(cursorSpeed, 0.0f);
    }

    public float GetCursorVelocity() => maxCursorSpeed;

    public void SetCursorAcceleration(float cursorAcceleration)
    {
        this.cursorAcceleration = Math.Max(cursorAcceleration, 0.0f);
    }

    public float GetCursorAcceleration() => cursorAcceleration;

    public bool IsButtonDown() => pointerPressed;

    public IntVector2 GetPointerPosition() => Vector2.RoundToInt(pointerPosition);

    public IntVector2 GetUIPointerPosition() => GetPointerPosition();

    public DirectionAggregator GetDirectionAggregator() => directionAdapter;

    public void Update(float timeStep)
    {
        if (!enabled)
            return;

        pointerPressed = false;

        if (input is not null && (subscriptionMask & PointerAdapterMask.Mouse) != 0)
        {
            pointerPosition = input.GetMousePosition().ToVector2();
            pointerPressed = input.GetMouseButtonDown(MouseButton.Left);
        }

        if ((subscriptionMask & (PointerAdapterMask.Keyboard | PointerAdapterMask.Joystick | PointerAdapterMask.Touch)) != 0)
        {
            directionAdapter.SetEnabled(true);
            var direction = directionAdapter.GetDirection();
            if (!direction.ApproximatelyEquivalent(Vector2.Zero))
            {
                cursorSpeed = Math.Min(maxCursorSpeed, cursorSpeed + cursorAcceleration * Math.Max(timeStep, 0.0f) * maxCursorSpeed);
                pointerPosition += direction * cursorSpeed * Math.Max(timeStep, 0.0f);
            }
            else
            {
                cursorSpeed = 0.0f;
            }
        }

        if (input is not null && (subscriptionMask & PointerAdapterMask.Joystick) != 0)
            pointerPressed |= input.GetControllerButtonDown(ControllerButton.A);
    }

    public void UpdatePointer(Vector2 position, bool press, bool moveMouse = false)
    {
        pointerPosition = position;
        pointerPressed = press;
        if (moveMouse && input is not null)
            input.SetMousePosition(Vector2.RoundToInt(position));
    }
}
