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

public sealed class DirectionalPadAdapter : Object, IDisposable
{
    private enum InputType { Keyboard, JoystickAxis, JoystickDPad, External }
    private readonly record struct InputSource(InputType Type, int Id);

    private sealed class AggregatedState(Scancode scancode)
    {
        public Scancode Scancode { get; } = scancode;
        public HashSet<InputSource> Sources { get; } = [];
        public float TimeToRepeat { get; set; } = float.MaxValue;
        public bool IsActive => Sources.Count != 0;
    }

    private readonly List<ContextEventSubscription> _subscriptions = [];
    private readonly AggregatedState _up = new(Scancode.Up);
    private readonly AggregatedState _down = new(Scancode.Down);
    private readonly AggregatedState _left = new(Scancode.Left);
    private readonly AggregatedState _right = new(Scancode.Right);
    private Input? _input;
    private bool _enabled;
    private DirectionalPadAdapterMask _subscriptionMask = DirectionalPadAdapterMask.All;
    private float _axisUpperThreshold = 0.6f;
    private float _axisLowerThreshold = 0.4f;
    private float _repeatDelay = 0.5f;
    private float _repeatInterval = 0.03f;

    public DirectionalPadAdapter(Input? input = null) => _input = input;

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

    public void SetSubscriptionMask(DirectionalPadAdapterMask mask)
    {
        if (_subscriptionMask == mask) return;
        _subscriptionMask = mask;
        UpdateSubscriptions();
    }

    public DirectionalPadAdapterMask GetSubscriptionMask() => _subscriptionMask;
    public void SetAxisUpperThreshold(float threshold) => _axisUpperThreshold = threshold;
    public float GetAxisUpperThreshold() => _axisUpperThreshold;
    public void SetAxisLowerThreshold(float threshold) => _axisLowerThreshold = threshold;
    public float GetAxisLowerThreshold() => _axisLowerThreshold;
    public void SetRepeatDelay(float delayInSeconds) => _repeatDelay = Math.Max(delayInSeconds, float.Epsilon);
    public float GetRepeatDelay() => _repeatDelay;
    public void SetRepeatInterval(float intervalInSeconds) => _repeatInterval = Math.Max(intervalInSeconds, float.Epsilon);
    public float GetRepeatInterval() => _repeatInterval;

    public void SetExternalDirection(Vector2f direction)
    {
        SetSource(_left, new(InputType.External, 0), direction.X < 0.0f);
        SetSource(_right, new(InputType.External, 0), direction.X > 0.0f);
        SetSource(_up, new(InputType.External, 0), direction.Y < 0.0f);
        SetSource(_down, new(InputType.External, 0), direction.Y > 0.0f);
    }

    public bool GetKeyDown(Key key) => GetScancodeDown(Input.GetScancodeFromKey(key));

    public bool GetScancodeDown(Scancode scancode) => scancode switch
    {
        Scancode.Up => _up.IsActive,
        Scancode.Down => _down.IsActive,
        Scancode.Left => _left.IsActive,
        Scancode.Right => _right.IsActive,
        _ => false
    };

    private void UpdateSubscriptions()
    {
        ClearSubscriptions();
        ClearAllSources(sendEvents: true);
        if (!_enabled || _input is null || Context.TryGetInstance() is null) return;
        _subscriptions.Add(SubscribeToEvent<InputEvents.InputFocusEvent>(_input, HandleInputFocus));
        if ((_subscriptionMask & DirectionalPadAdapterMask.KeyRepeat) != 0)
            _subscriptions.Add(SubscribeToEvent<CoreEvents.UpdateEvent>(HandleUpdate));
        if ((_subscriptionMask & DirectionalPadAdapterMask.Keyboard) != 0)
        {
            _subscriptions.Add(SubscribeToEvent<InputEvents.KeyDownEvent>(_input, HandleKeyDown));
            _subscriptions.Add(SubscribeToEvent<InputEvents.KeyUpEvent>(_input, HandleKeyUp));
        }
        if ((_subscriptionMask & DirectionalPadAdapterMask.Joystick) != 0)
        {
            _subscriptions.Add(SubscribeToEvent<InputEvents.JoystickAxisMoveEvent>(_input, HandleJoystickAxisMove));
            _subscriptions.Add(SubscribeToEvent<InputEvents.JoystickHatMoveEvent>(_input, HandleJoystickHatMove));
            _subscriptions.Add(SubscribeToEvent<InputEvents.JoystickDisconnectedEvent>(_input, HandleJoystickDisconnected));
        }
    }

    private void ClearSubscriptions()
    {
        foreach (var subscription in _subscriptions) subscription.Dispose();
        _subscriptions.Clear();
    }

    private void HandleInputFocus(ref InputEvents.InputFocusEvent value) { if (!value.Focus) ClearAllSources(sendEvents: true); }

    private void HandleUpdate(ref CoreEvents.UpdateEvent value)
    {
        foreach (var state in States())
        {
            if (!state.IsActive) continue;
            state.TimeToRepeat -= value.TimeStep;
            if (state.TimeToRepeat < 0.0f)
            {
                state.TimeToRepeat = _repeatInterval;
                SendKeyDown(state.Scancode, repeat: true);
            }
        }
    }

    private void HandleKeyDown(ref InputEvents.KeyDownEvent value) => SetKeyboard(value.Scancode, true);
    private void HandleKeyUp(ref InputEvents.KeyUpEvent value) => SetKeyboard(value.Scancode, false);

    private void SetKeyboard(Scancode scancode, bool down)
    {
        var source = new InputSource(InputType.Keyboard, 0);
        switch (scancode)
        {
            case Scancode.W or Scancode.Up: SetSource(_up, source, down); break;
            case Scancode.S or Scancode.Down: SetSource(_down, source, down); break;
            case Scancode.A or Scancode.Left: SetSource(_left, source, down); break;
            case Scancode.D or Scancode.Right: SetSource(_right, source, down); break;
        }
    }

    private void HandleJoystickAxisMove(ref InputEvents.JoystickAxisMoveEvent value)
    {
        var source = new InputSource(InputType.JoystickAxis, value.JoystickID);
        if (value.Axis == 1)
        {
            if (value.Position > _axisUpperThreshold) { SetSource(_down, source, true); SetSource(_up, source, false); }
            else if (value.Position < -_axisUpperThreshold) { SetSource(_down, source, false); SetSource(_up, source, true); }
            else if (value.Position > -_axisLowerThreshold && value.Position < _axisLowerThreshold) { SetSource(_up, source, false); SetSource(_down, source, false); }
        }
        else if (value.Axis == 0)
        {
            if (value.Position > _axisUpperThreshold) { SetSource(_right, source, true); SetSource(_left, source, false); }
            else if (value.Position < -_axisUpperThreshold) { SetSource(_right, source, false); SetSource(_left, source, true); }
            else if (value.Position > -_axisLowerThreshold && value.Position < _axisLowerThreshold) { SetSource(_right, source, false); SetSource(_left, source, false); }
        }
    }

    private void HandleJoystickHatMove(ref InputEvents.JoystickHatMoveEvent value)
    {
        if (value.Hat != 0) return;
        var source = new InputSource(InputType.JoystickDPad, value.JoystickID);
        SetSource(_up, source, (value.Position & HatPosition.Up) != 0);
        SetSource(_down, source, (value.Position & HatPosition.Down) != 0);
        SetSource(_left, source, (value.Position & HatPosition.Left) != 0);
        SetSource(_right, source, (value.Position & HatPosition.Right) != 0);
    }

    private void HandleJoystickDisconnected(ref InputEvents.JoystickDisconnectedEvent value)
    {
        var joystickId = value.JoystickID;
        foreach (var state in States())
            RemoveSources(state, source => source.Id == joystickId && source.Type is InputType.JoystickAxis or InputType.JoystickDPad);
    }

    private void SetSource(AggregatedState state, InputSource source, bool active)
    {
        if (active)
        {
            if (!state.Sources.Add(source) || state.Sources.Count != 1) return;
            state.TimeToRepeat = _repeatDelay;
            SendKeyDown(state.Scancode, repeat: false);
        }
        else if (state.Sources.Remove(source) && state.Sources.Count == 0)
        {
            state.TimeToRepeat = float.MaxValue;
            SendKeyUp(state.Scancode);
        }
    }

    private void RemoveSources(AggregatedState state, Predicate<InputSource> predicate)
    {
        if (state.Sources.RemoveWhere(source => predicate(source)) == 0 || state.Sources.Count != 0) return;
        state.TimeToRepeat = float.MaxValue;
        SendKeyUp(state.Scancode);
    }

    private void ClearAllSources(bool sendEvents)
    {
        foreach (var state in States())
        {
            var wasActive = state.IsActive;
            state.Sources.Clear();
            state.TimeToRepeat = float.MaxValue;
            if (wasActive && sendEvents) SendKeyUp(state.Scancode);
        }
    }

    private IEnumerable<AggregatedState> States() { yield return _up; yield return _down; yield return _left; yield return _right; }

    private void SendKeyDown(Scancode scancode, bool repeat)
    {
        var eventData = new InputEvents.KeyDownEvent { Key = ToKey(scancode), Scancode = scancode, Repeat = repeat };
        SendEvent(ref eventData);
    }

    private void SendKeyUp(Scancode scancode)
    {
        var eventData = new InputEvents.KeyUpEvent { Key = ToKey(scancode), Scancode = scancode };
        SendEvent(ref eventData);
    }

    private static Key ToKey(Scancode scancode) => scancode switch
    {
        Scancode.Up => Key.Up, Scancode.Down => Key.Down,
        Scancode.Left => Key.Left, Scancode.Right => Key.Right,
        _ => Key.Unknown
    };
}
