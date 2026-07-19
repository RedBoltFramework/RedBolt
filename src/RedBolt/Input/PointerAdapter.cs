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

public class PointerAdapter : Object
{
    /// <summary>Construct.</summary>
    public PointerAdapter()
    {
        _directionAdapter = new DirectionAggregator();

        // Propagate default subscription mask to direction adapter.
        SetSubscriptionMask(GetSubscriptionMask());

        var input = GetSubsystem<Input>()!;
        _pointerPosition = input.GetMousePosition().ToVector2();

        var graphics = GetSubsystem<Graphics>();
        if (graphics is not null)
        {
            _maxCursorSpeed = Math.Max(graphics.Width, graphics.Height) / 2.0f;
        }
    }

    public override void Dispose()
    {
        _directionAdapter.Dispose();
        base.Dispose();
    }

    /// <summary>Set enabled flag. The object subscribes for events when enabled.</summary>
    public void SetEnabled(bool enabled)
    {
        if (_enabled != enabled)
        {
            _directionAdapter.SetEnabled(enabled);
            _enabled = enabled;
            if (_enabled)
            {
                UpdateSubscriptions(_enabledSubscriptions);
            }
            else
            {
                UpdateSubscriptions(PointerAdapterMask.None);
            }
        }
    }

    /// <summary>Set subscription mask.</summary>
    public void SetSubscriptionMask(PointerAdapterMask mask)
    {
        _enabledSubscriptions = mask;

        // Propagate keyboard settings to the underlying direction adapter.
        var flags = DirectionAggregatorMask.None;
        if ((_enabledSubscriptions & PointerAdapterMask.Keyboard) != 0)
            flags |= DirectionAggregatorMask.Keyboard;
        if ((_enabledSubscriptions & PointerAdapterMask.Joystick) != 0)
            flags |= DirectionAggregatorMask.Joystick;
        _directionAdapter.SetSubscriptionMask(flags);

        if (IsEnabled())
            UpdateSubscriptions(_enabledSubscriptions);
    }

    // TODO: port void SetUIElement(UIElement* element);

    /// <summary>Set maximum cursor velocity.</summary>
    public void SetCursorSpeed(float cursorSpeed)
    {
        _cursorSpeed = cursorSpeed;
    }

    /// <summary>Set cursor acceleration factor.</summary>
    public void SetCursorAcceleration(float cursorAcceleration)
    {
        _cursorAcceleration = cursorAcceleration;
    }

    /// <summary>Get enabled flag.</summary>
    public bool IsEnabled() => _enabled;

    /// <summary>Get subscription mask.</summary>
    public PointerAdapterMask GetSubscriptionMask() => _enabledSubscriptions;

    // TODO: port UIElement* GetUIElement() const;

    /// <summary>Is button down (left mouse button, touch or gamepad A button).</summary>
    public bool IsButtonDown() => _pointerPressed;

    /// <summary>Set maximum cursor velocity.</summary>
    public float GetCursorVelocity() => _maxCursorSpeed;

    /// <summary>Set cursor acceleration factor.</summary>
    public float GetCursorAcceleration() => _cursorAcceleration;

    /// <summary>Get last known pointer position.</summary>
    public Vector2i GetPointerPosition() => _pointerPosition.ToIntVector2();

    // TODO: port IntVector2 GetUIPointerPosition() const;

    /// <summary>Get DirectionAggregator instance used to handle joystick and keyboard input.</summary>
    public DirectionAggregator GetDirectionAggregator()
    {
        return _directionAdapter;
    }

    private void UpdateSubscriptions(PointerAdapterMask flags)
    {
        var input = GetSubsystem<Input>()!;

        var toSubscribe = flags & ~_subscriptionFlags;
        var toUnsubscribe = _subscriptionFlags & ~flags;

        if (_subscriptionFlags == PointerAdapterMask.None && flags != PointerAdapterMask.None)
        {
            SubscribeToEvent<CoreEvents.UpdateEvent>(HandleUpdate);
        }
        else if (_subscriptionFlags != PointerAdapterMask.None && flags == PointerAdapterMask.None)
        {
            UnsubscribeFromEvent<CoreEvents.UpdateEvent>();
        }

        _subscriptionFlags = flags;
        if ((toSubscribe & PointerAdapterMask.Mouse) != 0)
        {
            SubscribeToEvent<InputEvents.MouseMoveEvent>(input, HandleMouseMove);
            SubscribeToEvent<InputEvents.MouseButtonUpEvent>(input, HandleMouseButtonUp);
            SubscribeToEvent<InputEvents.MouseButtonDownEvent>(input, HandleMouseButtonDown);
        }
        else if ((toUnsubscribe & PointerAdapterMask.Mouse) != 0)
        {
            UnsubscribeFromEvent<InputEvents.MouseMoveEvent>();
        }
        if ((toSubscribe & PointerAdapterMask.Touch) != 0)
        {
            SubscribeToEvent<InputEvents.TouchBeginEvent>(input, HandleTouchBegin);
            SubscribeToEvent<InputEvents.TouchMoveEvent>(input, HandleTouchMove);
            SubscribeToEvent<InputEvents.TouchEndEvent>(input, HandleTouchEnd);
        }
        else if ((toUnsubscribe & PointerAdapterMask.Touch) != 0)
        {
            UnsubscribeFromEvent<InputEvents.TouchBeginEvent>();
            UnsubscribeFromEvent<InputEvents.TouchMoveEvent>();
            UnsubscribeFromEvent<InputEvents.TouchEndEvent>();
            _activeTouchId = null;
        }
        if ((toSubscribe & PointerAdapterMask.Joystick) != 0)
        {
            SubscribeToEvent<InputEvents.JoystickButtonDownEvent>(input, HandleJoystickButton);
            SubscribeToEvent<InputEvents.JoystickButtonUpEvent>(input, HandleJoystickButton);
        }
        else if ((toUnsubscribe & PointerAdapterMask.Joystick) != 0)
        {
            UnsubscribeFromEvent<InputEvents.JoystickButtonDownEvent>();
            UnsubscribeFromEvent<InputEvents.JoystickButtonUpEvent>();
        }
    }

    private void HandleUpdate(ref CoreEvents.UpdateEvent args)
    {
        var aggregatedDirection = _directionAdapter.GetDirection();

        var timeStep = args.TimeStep;

        var velocity = aggregatedDirection * _maxCursorSpeed;
        var targetSpeed = velocity.Length;
        if (targetSpeed > _maxCursorSpeed)
        {
            var limitedSpeed = MathDefs.Lerp(_cursorSpeed, targetSpeed, _cursorAcceleration);
            velocity *= limitedSpeed / targetSpeed;
        }

        UpdatePointer(_pointerPosition + velocity * timeStep, _pointerPressed, true);
    }

    private void HandleMouseMove(ref InputEvents.MouseMoveEvent args)
    {
        // Ignore mouse movement if touch is active.
        if (_activeTouchId.HasValue)
            return;

        UpdatePointer(new Vector2i(args.X, args.Y).ToVector2(), _pointerPressed, false);
    }

    private void HandleMouseButtonUp(ref InputEvents.MouseButtonUpEvent args)
    {
        // Ignore mouse movement if touch is active.
        if (_activeTouchId.HasValue)
            return;

        UpdatePointer(_pointerPosition, false, false);
    }

    private void HandleMouseButtonDown(ref InputEvents.MouseButtonDownEvent args)
    {
        // Ignore mouse movement if touch is active.
        if (_activeTouchId.HasValue)
            return;

        UpdatePointer(_pointerPosition, true, false);
    }

    private void HandleTouchBegin(ref InputEvents.TouchBeginEvent args)
    {
        // Do nothing if already is tracking touch
        if (_activeTouchId.HasValue)
            return;

        var input = GetSubsystem<Input>()!;

        // Start tracking touch
        var touchState = input.GetTouchById(args.TouchID);
        if (touchState.HasValue && ReferenceEquals(touchState.Value.TouchedElement, _directionAdapter.GetUIElement()))
        {
            _activeTouchId = touchState.Value.TouchID;
            UpdatePointer(new Vector2i(args.X, args.Y).ToVector2(), true, true);
        }
    }

    private void HandleTouchMove(ref InputEvents.TouchMoveEvent args)
    {
        // Do nothing if not tracking touch
        if (!_activeTouchId.HasValue)
            return;
        // Validate touch id
        var touchId = args.TouchID;
        if (touchId != _activeTouchId.Value)
            return;

        UpdatePointer(new Vector2i(args.X, args.Y).ToVector2(), true, true);
    }

    private void HandleTouchEnd(ref InputEvents.TouchEndEvent args)
    {
        // Do nothing if not tracking touch
        if (!_activeTouchId.HasValue)
            return;
        // Stop tracking touch
        var touchId = args.TouchID;
        if (touchId != _activeTouchId.Value)
            return;

        UpdatePointer(new Vector2i(args.X, args.Y).ToVector2(), false, true);

        _activeTouchId = null;
    }

    private void HandleJoystickButton(ref InputEvents.JoystickButtonDownEvent args)
    {
        if (args.Button == 0)
        {
            UpdatePointer(_pointerPosition, true, false);
        }
    }

    private void HandleJoystickButton(ref InputEvents.JoystickButtonUpEvent args)
    {
        if (args.Button == 0)
        {
            UpdatePointer(_pointerPosition, false, false);
        }
    }

    private void UpdatePointer(Vector2f position, bool press, bool moveMouse)
    {
        var input = GetSubsystem<Input>()!;

        if (!_pointerPosition.ApproximatelyEquivalent(position))
        {
            var prevPosition = _pointerPosition.ToIntVector2();

            _pointerPosition = position;

            var newPosition = _pointerPosition.ToIntVector2();

            if (moveMouse)
            {
                input.SetMousePosition(newPosition);
            }

            if (newPosition != prevPosition)
            {
                var eventData = new InputEvents.MouseMoveEvent
                {
                    X = newPosition.X,
                    Y = newPosition.Y,
                    DX = newPosition.X - prevPosition.X,
                    DY = newPosition.Y - prevPosition.Y,
                    Buttons = _pointerPressed ? MouseButton.Left : MouseButton.None,
                    Qualifiers = input.Qualifiers
                };
                SendEvent(ref eventData);
            }
        }

        if (press != _pointerPressed)
        {
            _pointerPressed = press;
            if (_pointerPressed)
            {
                var eventData = new InputEvents.MouseButtonDownEvent
                {
                    Button = MouseButton.Left,
                    Buttons = _pointerPressed ? MouseButton.Left : MouseButton.None,
                    Qualifiers = input.Qualifiers,
                    Clicks = 1
                };
                SendEvent(ref eventData);
            }
            else
            {
                var eventData = new InputEvents.MouseButtonUpEvent
                {
                    Button = MouseButton.None,
                    Buttons = _pointerPressed ? MouseButton.Left : MouseButton.None,
                    Qualifiers = input.Qualifiers
                };
                SendEvent(ref eventData);
            }
        }
    }

    // Keyboard and joystick adapter to move the cursor.
    private readonly DirectionAggregator _directionAdapter;
    /// <summary>Is aggregator enabled</summary>
    private bool _enabled;
    /// <summary>Enabled subscriptions</summary>
    private PointerAdapterMask _enabledSubscriptions = PointerAdapterMask.All;
    /// <summary>Active subscriptions bitmask</summary>
    private PointerAdapterMask _subscriptionFlags = PointerAdapterMask.None;
    /// <summary>Last known pointer position.</summary>
    /// <remarks>This is required in case of SDL can't set a mouse position on the platform.
    /// Also it is a floating point vector to handle analog axis input correctly.</remarks>
    private Vector2f _pointerPosition;
    /// <summary>Is there an active "pressed" event.</summary>
    private bool _pointerPressed;
    /// <summary>Identifier of active touch</summary>
    private int? _activeTouchId;
    /// <summary>Current cursor velocity.</summary>
    private float _cursorSpeed;
    /// <summary>Max cursor velocity.</summary>
    private float _maxCursorSpeed = 100.0f;
    /// <summary>Cursor acceleration.</summary>
    private float _cursorAcceleration = 1.0f;
}
