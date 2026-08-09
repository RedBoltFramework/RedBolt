namespace RedBolt.Input;

using SystemMath = global::System.Math;

using RedBolt.Core;
using RedBolt.Scene;

[Flags]
public enum DirectionalPadAdapterMask : uint
{
    None = 0,
    Keyboard = 1 << 0,
    Joystick = 1 << 1,
    KeyRepeat = 1 << 2,
    All = Keyboard | Joystick | KeyRepeat,
}

/// <summary>
/// Adapter to translate gamepad axis and DPad messages along with keyboard (WASD and arrows) and externally provided
/// directions into keyboard arrow messages.
/// </summary>
/// <remarks>
/// DPadAdapter collects all inputs that it can categorize as a movement into specific direction. When at least one
/// input received it sends a corresponding keyboard message about arrow key been pressed. When last input is
/// released it sends a message about key been released. It can also be used as a substitute for Input as it implements
/// GetScancodeDown and GetKeyDown methods but it only supports arrow key and scan codes.
/// </remarks>
[Reflected]
public partial class DirectionalPadAdapter : RedObject
{
    private enum InputType
    {
        External = 0,
        Keyboard = 1,
        JoystickAxis = 100,
        JoystickDPad = 200,
    }

    private class AggregatedState : ICloneable
    {
        public AggregatedState(Scancode scancode)
        {
            Scancode = scancode;
            ActiveSources = new List<InputType>(4);
            TimeToRepeat = float.MaxValue;
        }

        public Scancode Scancode;
        public List<InputType> ActiveSources;
        public float TimeToRepeat;

        public bool Append(InputType inputType)
        {
            for (var i = 0; i < ActiveSources.Count; ++i)
            {
                if (ActiveSources[i] == inputType)
                {
                    return false;
                }
            }
            ActiveSources.Add(inputType);
            return ActiveSources.Count == 1;
        }

        public bool Remove(InputType inputType)
        {
            for (var i = 0; i < ActiveSources.Count; ++i)
            {
                if (ActiveSources[i] == inputType)
                {
                    ActiveSources[i] = ActiveSources[^1];
                    ActiveSources.RemoveAt(ActiveSources.Count - 1);
                    if (ActiveSources.Count == 0)
                    {
                        TimeToRepeat = float.MaxValue;
                        return true;
                    }
                }
            }
            return false;
        }

        public bool RemoveIf(Predicate<InputType> predicate)
        {
            if (ActiveSources.Count == 0)
            {
                return false;
            }
            for (var i = ActiveSources.Count; i-- > 0;)
            {
                if (predicate(ActiveSources[i]))
                {
                    ActiveSources[i] = ActiveSources[^1];
                    ActiveSources.RemoveAt(ActiveSources.Count - 1);
                }
            }
            if (ActiveSources.Count == 0)
            {
                TimeToRepeat = float.MaxValue;
                return true;
            }
            return false;
        }

        public bool IsActive() => ActiveSources.Count != 0;

        public object Clone()
        {
            var clone = new AggregatedState(Scancode);
            clone.ActiveSources.AddRange(ActiveSources);
            clone.TimeToRepeat = TimeToRepeat;
            return clone;
        }
    }

    /// <summary>Is adapter enabled</summary>
    private bool _enabled;
    /// <summary>Enabled subscriptions</summary>
    private DirectionalPadAdapterMask _enabledSubscriptions = DirectionalPadAdapterMask.All;
    /// <summary>Active subscriptions bitmask</summary>
    private DirectionalPadAdapterMask _subscriptionFlags = DirectionalPadAdapterMask.None;
    private readonly Input _input;
    private readonly AggregatedState _up;
    private readonly AggregatedState _down;
    private readonly AggregatedState _left;
    private readonly AggregatedState _right;
    private int _ignoreJoystickId = int.MinValue;
    private float _axisUpperThreshold = 0.6f;
    private float _axisLowerThreshold = 0.4f;
    private float _repeatDelay = 0.5f;
    private float _repeatInterval = 0.03f;

    /// <summary>Construct.</summary>
    public DirectionalPadAdapter()
    {
        _input = GetSubsystem<Input>()!;
        _up = new AggregatedState(Scancode.Up);
        _down = new AggregatedState(Scancode.Down);
        _left = new AggregatedState(Scancode.Left);
        _right = new AggregatedState(Scancode.Right);

        var numJoysticks = _input.GetNumJoysticks();
        for (var i = 0u; i < numJoysticks; ++i)
        {
            var joystick = _input.GetJoystickByIndex(i)!.Value;
            if (joystick.GetNumAxes() == 3 && joystick.GetNumButtons() == 0 && joystick.GetNumHats() == 0)
            {
                _ignoreJoystickId = joystick.JoystickID;
            }
        }
    }

    /// <summary>Set enabled flag. The object subscribes for events when enabled.</summary>
    public void SetEnabled(bool enabled)
    {
        if (_enabled != enabled)
        {
            _enabled = enabled;
            if (_enabled)
            {
                UpdateSubscriptions(_enabledSubscriptions);
            }
            else
            {
                UpdateSubscriptions(DirectionalPadAdapterMask.None);
            }
        }
    }

    /// <summary>Set input device subscription mask.</summary>
    public void SetSubscriptionMask(DirectionalPadAdapterMask mask)
    {
        _enabledSubscriptions = mask;
        if (IsEnabled())
            UpdateSubscriptions(_enabledSubscriptions);
    }

    /// <summary>Set axis upper threshold. Axis value greater than threshold is interpreted as key press.</summary>
    public void SetAxisUpperThreshold(float threshold) => _axisUpperThreshold = threshold;

    /// <summary>Set axis lower threshold. Axis value lower than threshold is interpreted as key press.</summary>
    public void SetAxisLowerThreshold(float threshold) => _axisLowerThreshold = threshold;

    /// <summary>Set repeat delay in seconds.</summary>
    public void SetRepeatDelay(float delayInSeconds)
    {
        _repeatDelay = SystemMath.Max(delayInSeconds, MathF.BitIncrement(1.0f) - 1.0f);
    }

    /// <summary>Set repeat interval in seconds.</summary>
    public void SetRepeatInterval(float intervalInSeconds)
    {
        _repeatInterval = SystemMath.Max(intervalInSeconds, MathF.BitIncrement(1.0f) - 1.0f);
    }

    /// <summary>Get enabled flag.</summary>
    public bool IsEnabled() => _enabled;

    /// <summary>Get input device subscription mask.</summary>
    public DirectionalPadAdapterMask GetSubscriptionMask() => _enabledSubscriptions;

    /// <summary>Check if a key is held down by Key code. Only Up, Down, Left and Right keys are supported.</summary>
    public bool GetKeyDown(Key key) => GetScancodeDown(Input.GetScancodeFromKey(key));

    /// <summary>Check if a key is held down by scancode. Only Up, Down, Left and Right scancodes are supported.</summary>
    public bool GetScancodeDown(Scancode scancode)
    {
        switch (scancode)
        {
            case Scancode.Up: return _up.IsActive();
            case Scancode.Down: return _down.IsActive();
            case Scancode.Left: return _left.IsActive();
            case Scancode.Right: return _right.IsActive();
            default: break;
        }
        return false;
    }

    /// <summary>Get axis upper threshold. Axis value greater than threshold is interpreted as key press.</summary>
    public float GetAxisUpperThreshold() => _axisUpperThreshold;

    /// <summary>Get axis lower threshold. Axis value lower than threshold is interpreted as key press.</summary>
    public float GetAxisLowerThreshold() => _axisLowerThreshold;

    /// <summary>Get repeat delay in seconds.</summary>
    public float GetRepeatDelay() => _repeatDelay;

    /// <summary>Get repeat interval in seconds.</summary>
    public float GetRepeatInterval() => _repeatInterval;

    private void UpdateSubscriptions(DirectionalPadAdapterMask flags)
    {
        var toSubscribe = flags & ~_subscriptionFlags;
        var toUnsubscribe = _subscriptionFlags & ~flags;

        if (_subscriptionFlags == DirectionalPadAdapterMask.None && flags != DirectionalPadAdapterMask.None)
        {
            SubscribeToEvent<InputEvents.InputFocusEvent>(_input, HandleInputFocus);
        }
        else if (_subscriptionFlags != DirectionalPadAdapterMask.None && flags == DirectionalPadAdapterMask.None)
        {
            UnsubscribeFromEvent<InputEvents.InputFocusEvent>(_input);
        }

        _subscriptionFlags = flags;
        if ((toSubscribe & DirectionalPadAdapterMask.KeyRepeat) != 0)
        {
            SubscribeToEvent<CoreEvents.UpdateEvent>(HandleUpdate);
        }
        else if ((toUnsubscribe & DirectionalPadAdapterMask.Keyboard) != 0)
        {
            UnsubscribeFromEvent<CoreEvents.UpdateEvent>();
        }

        if ((toSubscribe & DirectionalPadAdapterMask.Keyboard) != 0)
        {
            SubscribeToEvent<InputEvents.KeyUpEvent>(_input, HandleKeyUp);
            SubscribeToEvent<InputEvents.KeyDownEvent>(_input, HandleKeyDown);
        }
        else if ((toUnsubscribe & DirectionalPadAdapterMask.Keyboard) != 0)
        {
            UnsubscribeFromEvent<InputEvents.KeyUpEvent>();
            UnsubscribeFromEvent<InputEvents.KeyDownEvent>();

            RemoveIf(_up, static input => input == InputType.Keyboard);
            RemoveIf(_down, static input => input == InputType.Keyboard);
            RemoveIf(_left, static input => input == InputType.Keyboard);
            RemoveIf(_right, static input => input == InputType.Keyboard);
        }
        if ((toSubscribe & DirectionalPadAdapterMask.Joystick) != 0)
        {
            SubscribeToEvent<InputEvents.JoystickAxisMoveEvent>(_input, HandleJoystickAxisMove);
            SubscribeToEvent<InputEvents.JoystickHatMoveEvent>(_input, HandleJoystickHatMove);
            SubscribeToEvent<InputEvents.JoystickDisconnectedEvent>(_input, HandleJoystickDisconnected);
        }
        else if ((toUnsubscribe & DirectionalPadAdapterMask.Joystick) != 0)
        {
            UnsubscribeFromEvent<InputEvents.JoystickAxisMoveEvent>();
            UnsubscribeFromEvent<InputEvents.JoystickHatMoveEvent>();
            UnsubscribeFromEvent<InputEvents.JoystickDisconnectedEvent>();

            RemoveIf(_up, static input => input >= InputType.JoystickAxis);
            RemoveIf(_down, static input => input >= InputType.JoystickAxis);
            RemoveIf(_left, static input => input >= InputType.JoystickAxis);
            RemoveIf(_right, static input => input >= InputType.JoystickAxis);
        }
    }

    private void HandleInputFocus(ref InputEvents.InputFocusEvent value)
    {
        if (!value.Focus)
        {
            // Clear all input and send even if necessary.
            RemoveIf(_up, static input => true);
            RemoveIf(_down, static input => true);
            RemoveIf(_left, static input => true);
            RemoveIf(_right, static input => true);
        }
    }

    private void HandleUpdate(ref CoreEvents.UpdateEvent value)
    {
        var timeStep = value.TimeStep;
        for (var i = 0; i < 4; ++i)
        {
            var state = i switch { 0 => _up, 1 => _down, 2 => _left, _ => _right };
            if (state.IsActive())
            {
                state.TimeToRepeat -= timeStep;
                if (state.TimeToRepeat < 0)
                {
                    state.TimeToRepeat = _repeatInterval;
                    SendKeyDown(state.Scancode, true);
                }
            }
        }
    }

    private void HandleKeyDown(ref InputEvents.KeyDownEvent value)
    {
        switch (value.Scancode)
        {
            case Scancode.W:
            case Scancode.Up:
                Append(_up, InputType.Keyboard);
                break;
            case Scancode.S:
            case Scancode.Down:
                Append(_down, InputType.Keyboard);
                break;
            case Scancode.A:
            case Scancode.Left:
                Append(_left, InputType.Keyboard);
                break;
            case Scancode.D:
            case Scancode.Right:
                Append(_right, InputType.Keyboard);
                break;
        }
    }

    private void HandleKeyUp(ref InputEvents.KeyUpEvent value)
    {
        switch (value.Scancode)
        {
            case Scancode.W:
            case Scancode.Up:
                Remove(_up, InputType.Keyboard);
                break;
            case Scancode.S:
            case Scancode.Down:
                Remove(_down, InputType.Keyboard);
                break;
            case Scancode.A:
            case Scancode.Left:
                Remove(_left, InputType.Keyboard);
                break;
            case Scancode.D:
            case Scancode.Right:
                Remove(_right, InputType.Keyboard);
                break;
        }
    }

    private void HandleJoystickAxisMove(ref InputEvents.JoystickAxisMoveEvent value)
    {
        var joystickId = value.JoystickID;
        if (joystickId == _ignoreJoystickId)
            return;

        var eventId = (InputType)((int)InputType.JoystickAxis + joystickId);
        var axisIndex = value.Axis;
        var axisValue = value.Position;

        // Up-Down
        if (axisIndex == 1)
        {
            if (axisValue > _axisUpperThreshold)
            {
                Append(_down, eventId);
                Remove(_up, eventId);
            }
            else if (axisValue < -_axisUpperThreshold)
            {
                Remove(_down, eventId);
                Append(_up, eventId);
            }
            else if (axisValue > -_axisLowerThreshold && axisValue < _axisLowerThreshold)
            {
                Remove(_up, eventId);
                Remove(_down, eventId);
            }
        }
        // Left-Right
        if (axisIndex == 0)
        {
            if (axisValue > _axisUpperThreshold)
            {
                Append(_right, eventId);
                Remove(_left, eventId);
            }
            else if (axisValue < -_axisUpperThreshold)
            {
                Append(_left, eventId);
                Remove(_right, eventId);
            }
            else if (axisValue > -_axisLowerThreshold && axisValue < _axisLowerThreshold)
            {
                Remove(_right, eventId);
                Remove(_left, eventId);
            }
        }
    }

    private void HandleJoystickHatMove(ref InputEvents.JoystickHatMoveEvent value)
    {
        var hatIndex = value.Hat;
        if (hatIndex != 0)
            return;

        var joystickId = value.JoystickID;
        var eventId = (InputType)((int)InputType.JoystickDPad + joystickId);
        var position = value.Position;

        if ((position & HatPosition.Up) != 0)
            Append(_up, eventId);
        else
            Remove(_up, eventId);

        if ((position & HatPosition.Down) != 0)
            Append(_down, eventId);
        else
            Remove(_down, eventId);

        if ((position & HatPosition.Left) != 0)
            Append(_left, eventId);
        else
            Remove(_left, eventId);

        if ((position & HatPosition.Right) != 0)
            Append(_right, eventId);
        else
            Remove(_right, eventId);

        var horizontalValue = ((position & HatPosition.Right) != 0 ? 1.0f : 0.0f)
            + ((position & HatPosition.Left) != 0 ? -1.0f : 0.0f);
        var verticalValue = ((position & HatPosition.Down) != 0 ? 1.0f : 0.0f)
            + ((position & HatPosition.Up) != 0 ? -1.0f : 0.0f);
    }

    private void HandleJoystickDisconnected(ref InputEvents.JoystickDisconnectedEvent value)
    {
        var joystickId = value.JoystickID;

        // Cancel Axis states.
        var joyEventId = (InputType)((int)InputType.JoystickAxis + joystickId);
        Remove(_up, joyEventId);
        Remove(_down, joyEventId);
        Remove(_left, joyEventId);
        Remove(_right, joyEventId);

        // Cancel DPad states.
        var dpadEventId = (InputType)((int)InputType.JoystickDPad + joystickId);
        Remove(_up, dpadEventId);
        Remove(_down, dpadEventId);
        Remove(_left, dpadEventId);
        Remove(_right, dpadEventId);
    }

    // Append input to set.
    private void Append(AggregatedState activeInput, InputType input)
    {
        if (activeInput.Append(input))
        {
            SendKeyDown(activeInput.Scancode, false);
            activeInput.TimeToRepeat = _repeatDelay;
        }
    }

    // Remove input from set.
    private void Remove(AggregatedState activeInput, InputType input)
    {
        if (activeInput.Remove(input))
        {
            SendKeyUp(activeInput.Scancode);
        }
    }

    // Remove input from set.
    private void RemoveIf(AggregatedState activeInput, Predicate<InputType> predicate)
    {
        if (activeInput.RemoveIf(predicate))
        {
            SendKeyUp(activeInput.Scancode);
        }
    }

    private void SendKeyUp(Scancode key)
    {
        var eventData = new InputEvents.KeyUpEvent
        {
            Scancode = key,
            Buttons = MouseButton.None,
            Qualifiers = Qualifier.None,
        };
        switch (key)
        {
            case Scancode.Up: eventData.Key = Key.Up; break;
            case Scancode.Down: eventData.Key = Key.Down; break;
            case Scancode.Left: eventData.Key = Key.Left; break;
            case Scancode.Right: eventData.Key = Key.Right; break;
            default: eventData.Key = Key.Unknown; break;
        }
        SendEvent(ref eventData);
    }

    private void SendKeyDown(Scancode key, bool repeat)
    {
        var eventData = new InputEvents.KeyDownEvent
        {
            Scancode = key,
            Buttons = MouseButton.None,
            Qualifiers = Qualifier.None,
            Repeat = repeat,
        };
        switch (key)
        {
            case Scancode.Up: eventData.Key = Key.Up; break;
            case Scancode.Down: eventData.Key = Key.Down; break;
            case Scancode.Left: eventData.Key = Key.Left; break;
            case Scancode.Right: eventData.Key = Key.Right; break;
            default: eventData.Key = Key.Unknown; break;
        }
        SendEvent(ref eventData);
    }
}
