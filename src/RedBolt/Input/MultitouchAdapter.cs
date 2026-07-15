namespace RedBolt;

/// <summary>Multitouch event type.</summary>
public enum MultitouchEventType
{
    MultitouchBegin,
    MultitouchEnd,
    MultitouchMove,
    MultitouchCancel,
}

/// <summary>Multitouch adapter. Translates touch events into multitouch gestures.</summary>
public sealed class MultitouchAdapter : Object, IDisposable
{
    private struct ActiveTouch
    {
        public int TouchID;
        public Vector2i Pos;
    }

    private bool _enabled;
    private bool _acceptTouches = true;
    private Vector2i _lastKnownPosition;
    private Vector2i _lastKnownSize;
    private readonly List<ActiveTouch> _touches = [];
    private readonly List<ContextEventSubscription> _subscriptions = [];

    /// <summary>Construct.</summary>
    public MultitouchAdapter()
    {
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        UnsubscribeFromEvents();
        base.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>Set whether the adapter is enabled.</summary>
    public void SetEnabled(bool enabled)
    {
        if (enabled != _enabled)
        {
            _enabled = enabled;
            if (_enabled)
                SubscribeToEvents();
            else
                UnsubscribeFromEvents();
        }
    }

    /// <summary>Return whether the adapter is enabled.</summary>
    public bool IsEnabled() => _enabled;

    private void SubscribeToEvents()
    {
        UnsubscribeFromEvents();
        _subscriptions.Add(SubscribeToEvent<InputEvents.TouchBeginEvent>(HandleTouchBegin));
        _subscriptions.Add(SubscribeToEvent<InputEvents.TouchEndEvent>(HandleTouchEnd));
        _subscriptions.Add(SubscribeToEvent<InputEvents.TouchMoveEvent>(HandleTouchMove));
    }

    private void UnsubscribeFromEvents()
    {
        foreach (var subscription in _subscriptions)
            subscription.Dispose();
        _subscriptions.Clear();
    }

    private void HandleTouchBegin(ref InputEvents.TouchBeginEvent eventData)
    {
        if (!_acceptTouches)
            return;

        var touchId = eventData.TouchID;

        foreach (var activeTouch in _touches)
        {
            if (activeTouch.TouchID == touchId)
                return;
        }

        SendEvent(MultitouchEventType.MultitouchCancel);

        _touches.Add(new ActiveTouch
        {
            TouchID = touchId,
            Pos = new Vector2i(eventData.X, eventData.Y)
        });
        SendEvent(MultitouchEventType.MultitouchBegin);
    }

    private void HandleTouchEnd(ref InputEvents.TouchEndEvent eventData)
    {
        var touchId = eventData.TouchID;

        for (var i = 0; i < _touches.Count; ++i)
        {
            if (_touches[i].TouchID == touchId)
            {
                _touches[i] = new ActiveTouch
                {
                    TouchID = _touches[i].TouchID,
                    Pos = new Vector2i(eventData.X, eventData.Y)
                };
                SendEvent(MultitouchEventType.MultitouchEnd);

                _touches.RemoveAt(i);

                _acceptTouches = _touches.Count == 0;

                return;
            }
        }
    }

    private void HandleTouchMove(ref InputEvents.TouchMoveEvent eventData)
    {
        if (!_acceptTouches)
            return;

        var touchId = eventData.TouchID;

        for (var i = 0; i < _touches.Count; ++i)
        {
            if (_touches[i].TouchID == touchId)
            {
                _touches[i] = new ActiveTouch
                {
                    TouchID = _touches[i].TouchID,
                    Pos = new Vector2i(eventData.X, eventData.Y)
                };
                SendEvent(MultitouchEventType.MultitouchMove);
                return;
            }
        }
    }

    private void SendEvent(MultitouchEventType eventType)
    {
        if (_touches.Count == 0)
            return;

        var center = Vector2i.Zero;
        var min = new Vector2i(int.MaxValue, int.MaxValue);
        var max = new Vector2i(int.MinValue, int.MinValue);
        foreach (var activeTouch in _touches)
        {
            center += activeTouch.Pos;
            min = Vector2i.Min(min, activeTouch.Pos);
            max = Vector2i.Max(max, activeTouch.Pos);
        }

        center = new Vector2i(center.X / _touches.Count, center.Y / _touches.Count);
        var size = max - min;

        var eventData = new InputEvents.MultitouchEvent
        {
            NumFingers = _touches.Count,
            EventType = (int)eventType,
            X = center.X,
            Y = center.Y,
            Size = size
        };

        if (eventType == MultitouchEventType.MultitouchBegin)
        {
            _lastKnownPosition = center;
            _lastKnownSize = size;
        }

        eventData.DX = center.X - _lastKnownPosition.X;
        eventData.DY = center.Y - _lastKnownPosition.Y;
        eventData.DSize = size - _lastKnownSize;

        _lastKnownPosition = center;
        _lastKnownSize = size;

        SendEvent(ref eventData);
    }
}
