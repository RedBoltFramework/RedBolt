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
    private readonly DirectionAggregator _directionAdapter;
    private Input? _input;
    private bool _enabled;
    private PointerAdapterMask _subscriptionMask = PointerAdapterMask.All;
    private Vector2 _pointerPosition = Vector2.Zero;
    private bool _pointerPressed;
    private float _cursorSpeed;
    private float _maxCursorSpeed = 100.0f;
    private float _cursorAcceleration = 1.0f;

    public PointerAdapter(Input? input = null)
    {
        this._input = input;
        _directionAdapter = new DirectionAggregator(input);
    }

    public void SetInput(Input? input)
    {
        this._input = input;
        _directionAdapter.SetInput(input);
    }

    public Input? GetInput() => _input;

    public void SetEnabled(bool enabled)
    {
        this._enabled = enabled;
        _directionAdapter.SetEnabled(enabled);
    }

    public bool IsEnabled() => _enabled;

    public void SetSubscriptionMask(PointerAdapterMask mask)
    {
        _subscriptionMask = mask;

        var directionMask = DirectionAggregatorMask.None;
        if ((mask & PointerAdapterMask.Keyboard) != 0)
            directionMask |= DirectionAggregatorMask.Keyboard;
        if ((mask & PointerAdapterMask.Joystick) != 0)
            directionMask |= DirectionAggregatorMask.Joystick;
        if ((mask & PointerAdapterMask.Touch) != 0)
            directionMask |= DirectionAggregatorMask.Touch;
        _directionAdapter.SetSubscriptionMask(directionMask);
    }

    public PointerAdapterMask GetSubscriptionMask() => _subscriptionMask;

    public void SetUIElement(object? element)
    {
        _directionAdapter.SetUIElement(element);
    }

    public object? GetUIElement() => _directionAdapter.GetUIElement();

    public void SetCursorSpeed(float cursorSpeed)
    {
        _maxCursorSpeed = Math.Max(cursorSpeed, 0.0f);
    }

    public float GetCursorVelocity() => _maxCursorSpeed;

    public void SetCursorAcceleration(float cursorAcceleration)
    {
        this._cursorAcceleration = Math.Max(cursorAcceleration, 0.0f);
    }

    public float GetCursorAcceleration() => _cursorAcceleration;

    public bool IsButtonDown() => _pointerPressed;

    public IntVector2 GetPointerPosition() => Vector2.RoundToInt(_pointerPosition);

    public IntVector2 GetUIPointerPosition() => GetPointerPosition();

    public DirectionAggregator GetDirectionAggregator() => _directionAdapter;

    public void Update(float timeStep)
    {
        if (!_enabled)
            return;

        _pointerPressed = false;

        if (_input is not null && (_subscriptionMask & PointerAdapterMask.Mouse) != 0)
        {
            _pointerPosition = _input.GetMousePosition().ToVector2();
            _pointerPressed = _input.GetMouseButtonDown(MouseButton.Left);
        }

        if ((_subscriptionMask & (PointerAdapterMask.Keyboard | PointerAdapterMask.Joystick | PointerAdapterMask.Touch)) != 0)
        {
            _directionAdapter.SetEnabled(true);
            var direction = _directionAdapter.GetDirection();
            if (!direction.ApproximatelyEquivalent(Vector2.Zero))
            {
                _cursorSpeed = Math.Min(_maxCursorSpeed, _cursorSpeed + _cursorAcceleration * Math.Max(timeStep, 0.0f) * _maxCursorSpeed);
                _pointerPosition += direction * _cursorSpeed * Math.Max(timeStep, 0.0f);
            }
            else
            {
                _cursorSpeed = 0.0f;
            }
        }

        if (_input is not null && (_subscriptionMask & PointerAdapterMask.Joystick) != 0)
            _pointerPressed |= _input.GetControllerButtonDown(ControllerButton.A);
    }

    public void UpdatePointer(Vector2 position, bool press, bool moveMouse = false)
    {
        _pointerPosition = position;
        _pointerPressed = press;
        if (moveMouse && _input is not null)
            _input.SetMousePosition(Vector2.RoundToInt(position));
    }
}
