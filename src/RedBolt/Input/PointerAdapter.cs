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

public sealed class PointerAdapter : Object, IDisposable
{
    private readonly DirectionAggregator _directionAdapter;
    private readonly List<ContextEventSubscription> _subscriptions = [];
    private Input? _input;
    private bool _enabled;
    private PointerAdapterMask _subscriptionMask = PointerAdapterMask.All;
    private Vector2f _pointerPosition;
    private bool _pointerPressed;
    private int? _activeTouchId;
    private float _cursorSpeed;
    private float _maxCursorSpeed = 100.0f;
    private float _cursorAcceleration = 1.0f;

    public PointerAdapter(Input? input = null)
    {
        _input = input;
        _directionAdapter = new DirectionAggregator();
        _pointerPosition = input?.GetMousePosition().ToVector2() ?? Vector2f.Zero;
        SetSubscriptionMask(_subscriptionMask);
    }

    public override void Dispose()
    {
        ClearSubscriptions();
        _directionAdapter.Dispose();
        base.Dispose();
        GC.SuppressFinalize(this);
    }

    public void SetInput(Input? input)
    {
        if (ReferenceEquals(_input, input)) return;
        _input = input;
        if (input is not null) _pointerPosition = input.GetMousePosition().ToVector2();
        UpdateSubscriptions();
    }

    public Input? GetInput() => _input;

    public void SetEnabled(bool enabled)
    {
        if (_enabled == enabled) return;
        _enabled = enabled;
        _directionAdapter.SetEnabled(enabled);
        UpdateSubscriptions();
    }

    public bool IsEnabled() => _enabled;

    public void SetSubscriptionMask(PointerAdapterMask mask)
    {
        _subscriptionMask = mask;
        var directionMask = DirectionAggregatorMask.None;
        if ((mask & PointerAdapterMask.Keyboard) != 0) directionMask |= DirectionAggregatorMask.Keyboard;
        if ((mask & PointerAdapterMask.Joystick) != 0) directionMask |= DirectionAggregatorMask.Joystick;
        _directionAdapter.SetSubscriptionMask(directionMask);
        UpdateSubscriptions();
    }

    public PointerAdapterMask GetSubscriptionMask() => _subscriptionMask;
    public void SetUIElement(object? element) => _directionAdapter.SetUIElement(element);
    public object? GetUIElement() => _directionAdapter.GetUIElement();
    public void SetCursorSpeed(float cursorSpeed) => _cursorSpeed = Math.Max(cursorSpeed, 0.0f);
    public float GetCursorVelocity() => _maxCursorSpeed;
    public void SetCursorAcceleration(float cursorAcceleration) => _cursorAcceleration = Math.Max(cursorAcceleration, 0.0f);
    public float GetCursorAcceleration() => _cursorAcceleration;
    public bool IsButtonDown() => _pointerPressed;
    public Vector2i GetPointerPosition() => Vector2f.RoundToInt(_pointerPosition);
    public Vector2i GetUIPointerPosition() => GetPointerPosition();
    public DirectionAggregator GetDirectionAggregator() => _directionAdapter;

    public void Update(float timeStep)
    {
        if (!_enabled) return;
        var velocity = _directionAdapter.GetDirection() * _maxCursorSpeed;
        var targetSpeed = velocity.Length;
        if (targetSpeed > _maxCursorSpeed)
        {
            var limitedSpeed = MathDefs.Lerp(_cursorSpeed, targetSpeed, _cursorAcceleration);
            velocity *= limitedSpeed / targetSpeed;
        }
        UpdatePointer(_pointerPosition + velocity * Math.Max(timeStep, 0.0f), _pointerPressed, true);
    }

    public void UpdatePointer(Vector2f position, bool press, bool moveMouse = false)
    {
        var previousPosition = GetPointerPosition();
        if (!_pointerPosition.ApproximatelyEquivalent(position))
        {
            _pointerPosition = position;
            var newPosition = GetPointerPosition();
            if (moveMouse && _input is not null) _input.SetMousePosition(newPosition);
            if (newPosition != previousPosition)
            {
                var move = new InputEvents.MouseMoveEvent
                {
                    X = newPosition.X, Y = newPosition.Y,
                    DX = newPosition.X - previousPosition.X, DY = newPosition.Y - previousPosition.Y,
                    Buttons = _pointerPressed ? MouseButton.Left : MouseButton.None,
                    Qualifiers = _input?.Qualifiers ?? Qualifier.None
                };
                SendEvent(ref move);
            }
        }

        if (press == _pointerPressed) return;
        _pointerPressed = press;
        if (press)
        {
            var down = new InputEvents.MouseButtonDownEvent
            {
                Button = MouseButton.Left, Buttons = MouseButton.Left,
                Qualifiers = _input?.Qualifiers ?? Qualifier.None, Clicks = 1
            };
            SendEvent(ref down);
        }
        else
        {
            var up = new InputEvents.MouseButtonUpEvent
            {
                Button = MouseButton.Left, Buttons = MouseButton.None,
                Qualifiers = _input?.Qualifiers ?? Qualifier.None
            };
            SendEvent(ref up);
        }
    }

    private void UpdateSubscriptions()
    {
        ClearSubscriptions();
        _activeTouchId = null;
        if (!_enabled || _input is null) return;
        _subscriptions.Add(SubscribeToEvent<CoreEvents.UpdateEvent>(HandleUpdate));
        if ((_subscriptionMask & PointerAdapterMask.Mouse) != 0)
        {
            _subscriptions.Add(SubscribeToEvent<InputEvents.MouseMoveEvent>(_input, HandleMouseMove));
            _subscriptions.Add(SubscribeToEvent<InputEvents.MouseButtonDownEvent>(_input, HandleMouseButtonDown));
            _subscriptions.Add(SubscribeToEvent<InputEvents.MouseButtonUpEvent>(_input, HandleMouseButtonUp));
        }
        if ((_subscriptionMask & PointerAdapterMask.Touch) != 0)
        {
            _subscriptions.Add(SubscribeToEvent<InputEvents.TouchBeginEvent>(_input, HandleTouchBegin));
            _subscriptions.Add(SubscribeToEvent<InputEvents.TouchMoveEvent>(_input, HandleTouchMove));
            _subscriptions.Add(SubscribeToEvent<InputEvents.TouchEndEvent>(_input, HandleTouchEnd));
        }
        if ((_subscriptionMask & PointerAdapterMask.Joystick) != 0)
        {
            _subscriptions.Add(SubscribeToEvent<InputEvents.JoystickButtonDownEvent>(_input, HandleJoystickButtonDown));
            _subscriptions.Add(SubscribeToEvent<InputEvents.JoystickButtonUpEvent>(_input, HandleJoystickButtonUp));
        }
    }

    private void ClearSubscriptions()
    {
        foreach (var subscription in _subscriptions) subscription.Dispose();
        _subscriptions.Clear();
    }

    private void HandleUpdate(ref CoreEvents.UpdateEvent value) => Update(value.TimeStep);
    private void HandleMouseMove(ref InputEvents.MouseMoveEvent value) { if (!_activeTouchId.HasValue) UpdatePointer(new(value.X, value.Y), _pointerPressed); }
    private void HandleMouseButtonDown(ref InputEvents.MouseButtonDownEvent value) { if (!_activeTouchId.HasValue) UpdatePointer(_pointerPosition, true); }
    private void HandleMouseButtonUp(ref InputEvents.MouseButtonUpEvent value) { if (!_activeTouchId.HasValue) UpdatePointer(_pointerPosition, false); }
    private void HandleTouchBegin(ref InputEvents.TouchBeginEvent value)
    {
        if (_activeTouchId.HasValue) return;
        _activeTouchId = value.TouchID;
        UpdatePointer(new(value.X, value.Y), true, true);
    }
    private void HandleTouchMove(ref InputEvents.TouchMoveEvent value) { if (_activeTouchId == value.TouchID) UpdatePointer(new(value.X, value.Y), true, true); }
    private void HandleTouchEnd(ref InputEvents.TouchEndEvent value)
    {
        if (_activeTouchId != value.TouchID) return;
        UpdatePointer(new(value.X, value.Y), false, true);
        _activeTouchId = null;
    }
    private void HandleJoystickButtonDown(ref InputEvents.JoystickButtonDownEvent value) { if (value.Button == 0) UpdatePointer(_pointerPosition, true); }
    private void HandleJoystickButtonUp(ref InputEvents.JoystickButtonUpEvent value) { if (value.Button == 0) UpdatePointer(_pointerPosition, false); }
}
