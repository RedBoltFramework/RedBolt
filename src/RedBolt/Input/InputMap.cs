namespace RedBolt;

public sealed class InputMap : ResourceWithMetadata
{
    public const float DefaultDeadZone = 0.1f;

    private static readonly ActionMapping EmptyMapping = new();
    private readonly Dictionary<string, ActionMapping> actions = new(StringComparer.Ordinal);
    private float deadZone = DefaultDeadZone;

    public InputMap(string name, Input? input = null)
        : base(name)
    {
        Input = input;
    }

    public Input? Input { get; private set; }

    public IReadOnlyDictionary<string, ActionMapping> Mappings => actions;

    public void SetInput(Input? input)
    {
        Input = input;
    }

    public void SetDeadZone(float deadZone)
    {
        this.deadZone = Math.Max(deadZone, 0.0f);
    }

    public float GetDeadZone() => deadZone;

    public void MapKeyboardKey(string action, Scancode scancode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        foreach (var mapping in actions.Values)
            mapping.RemoveKeyboardKey(scancode);

        GetOrAddMapping(action).AddKeyboardKey(scancode);
    }

    public void MapMouseButton(string action, MouseButton mouseButton)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        foreach (var mapping in actions.Values)
            mapping.RemoveMouseButton(mouseButton);

        GetOrAddMapping(action).AddMouseButton(mouseButton);
    }

    public void MapControllerButton(string action, ControllerButton button)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        foreach (var mapping in actions.Values)
            mapping.RemoveControllerButton(button);

        GetOrAddMapping(action).AddControllerButton(button);
    }

    public void MapJoystickButton(string action, uint buttonIndex)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        foreach (var mapping in actions.Values)
            mapping.RemoveJoystickButton(buttonIndex);

        GetOrAddMapping(action).AddJoystickButton(buttonIndex);
    }

    public void MapControllerAxis(string action, ControllerAxis axis, float neutral, float pressed)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        var mapping = ControllerAxisMapping.ForController(axis, neutral, pressed);
        foreach (var actionMapping in actions.Values)
            actionMapping.RemoveControllerAxis(mapping);

        GetOrAddMapping(action).AddControllerAxis(mapping);
    }

    public void MapJoystickAxis(string action, uint axis, float neutral, float pressed)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        var mapping = ControllerAxisMapping.ForJoystick(axis, neutral, pressed);
        foreach (var actionMapping in actions.Values)
            actionMapping.RemoveControllerAxis(mapping);

        GetOrAddMapping(action).AddControllerAxis(mapping);
    }

    public void MapHat(string action, HatPosition hatPosition)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        foreach (var mapping in actions.Values)
            mapping.RemoveHat(hatPosition);

        GetOrAddMapping(action).AddHat(hatPosition);
    }

    public void MapScreenButton(string action, string elementName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        ArgumentException.ThrowIfNullOrWhiteSpace(elementName);
        foreach (var mapping in actions.Values)
            mapping.RemoveScreenButton(elementName);

        GetOrAddMapping(action).AddScreenButton(elementName);
    }

    public ActionMapping GetMapping(string action)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        return actions.GetValueOrDefault(action) ?? EmptyMapping;
    }

    public ActionMapping GetMappingByHash(StringHash actionHash)
    {
        foreach (var action in actions)
        {
            if (new StringHash(action.Key) == actionHash)
                return action.Value;
        }

        return EmptyMapping;
    }

    public IReadOnlyDictionary<string, ActionMapping> GetMappings() => Mappings;

    public float Evaluate(string action) => Input is not null ? Evaluate(action, Input) : 0.0f;

    public float Evaluate(string action, Input input)
    {
        ArgumentNullException.ThrowIfNull(input);
        return GetMapping(action).Evaluate(input, deadZone);
    }

    public float EvaluateByHash(StringHash actionHash) => Input is not null ? EvaluateByHash(actionHash, Input) : 0.0f;

    public float EvaluateByHash(StringHash actionHash, Input input)
    {
        ArgumentNullException.ThrowIfNull(input);
        return GetMappingByHash(actionHash).Evaluate(input, deadZone);
    }

    public static IReadOnlyList<string> GetScanCodeNames() => Enum.GetNames<Scancode>();

    public static IReadOnlyList<string> GetControllerButtonNames() => Enum.GetNames<ControllerButton>();

    public static IReadOnlyList<string> GetControllerHatNames() => Enum.GetNames<HatPosition>();

    public static IReadOnlyList<string> GetMouseButtonNames() => Enum.GetNames<MouseButton>();

    private ActionMapping GetOrAddMapping(string action)
    {
        if (!actions.TryGetValue(action, out var mapping))
        {
            mapping = new ActionMapping();
            actions.Add(action, mapping);
        }

        return mapping;
    }

    public sealed class ActionMapping
    {
        private readonly List<Scancode> keyboardKeys = [];
        private readonly List<MouseButton> mouseButtons = [];
        private readonly List<ControllerButtonMapping> controllerButtons = [];
        private readonly List<ControllerAxisMapping> controllerAxes = [];
        private readonly List<HatPosition> controllerHats = [];
        private readonly List<string> screenButtons = [];

        public IReadOnlyList<Scancode> KeyboardKeys => keyboardKeys;

        public IReadOnlyList<MouseButton> MouseButtons => mouseButtons;

        public IReadOnlyList<ControllerButtonMapping> ControllerButtons => controllerButtons;

        public IReadOnlyList<ControllerAxisMapping> ControllerAxes => controllerAxes;

        public IReadOnlyList<HatPosition> ControllerHats => controllerHats;

        public IReadOnlyList<string> ScreenButtons => screenButtons;

        public float Evaluate(Input input, float deadZone)
        {
            ArgumentNullException.ThrowIfNull(input);
            foreach (var scancode in keyboardKeys)
            {
                if (IsScancodeDown(input, scancode))
                    return 1.0f;
            }

            foreach (var mouseButton in mouseButtons)
            {
                if (input.GetMouseButtonDown(mouseButton))
                    return 1.0f;
            }

            foreach (var controllerButton in controllerButtons)
            {
                var pressed = controllerButton.IsController
                    ? input.GetControllerButtonDown(controllerButton.GetControllerButton())
                    : input.GetJoystickButtonDown(controllerButton.GetGenericButton());
                if (pressed)
                    return 1.0f;
            }

            foreach (var controllerAxis in controllerAxes)
            {
                var position = controllerAxis.IsController
                    ? input.GetControllerAxisPosition(controllerAxis.GetControllerAxis())
                    : input.GetJoystickAxisPosition(controllerAxis.GetGenericAxis());
                var translated = controllerAxis.Translate(position, deadZone);
                if (translated > 0.0f)
                    return translated;
            }

            foreach (var controllerHat in controllerHats)
            {
                if (input.GetHatDown(controllerHat))
                    return 1.0f;
            }

            foreach (var screenButton in screenButtons)
            {
                if (input.GetScreenButtonDown(screenButton))
                    return 1.0f;
            }

            return 0.0f;
        }

        internal void AddKeyboardKey(Scancode scancode)
        {
            if (!keyboardKeys.Contains(scancode))
                keyboardKeys.Add(scancode);
        }

        internal void RemoveKeyboardKey(Scancode scancode) => keyboardKeys.Remove(scancode);

        internal void AddMouseButton(MouseButton mouseButton)
        {
            if (mouseButton != MouseButton.None && !mouseButtons.Contains(mouseButton))
                mouseButtons.Add(mouseButton);
        }

        internal void RemoveMouseButton(MouseButton mouseButton) => mouseButtons.Remove(mouseButton);

        internal void AddControllerButton(ControllerButton button)
        {
            var mapping = ControllerButtonMapping.ForController(button);
            if (!controllerButtons.Contains(mapping))
                controllerButtons.Add(mapping);
        }

        internal void RemoveControllerButton(ControllerButton button) => controllerButtons.RemoveAll(mapping => mapping.IsController && mapping.Button == (uint)button);

        internal void AddJoystickButton(uint buttonIndex)
        {
            var mapping = ControllerButtonMapping.ForJoystick(buttonIndex);
            if (!controllerButtons.Contains(mapping))
                controllerButtons.Add(mapping);
        }

        internal void RemoveJoystickButton(uint buttonIndex) => controllerButtons.RemoveAll(mapping => !mapping.IsController && mapping.Button == buttonIndex);

        internal void AddControllerAxis(ControllerAxisMapping mapping) => controllerAxes.Add(mapping);

        internal void RemoveControllerAxis(ControllerAxisMapping mapping) => controllerAxes.RemoveAll(existing => existing.OverlapsWith(mapping));

        internal void AddHat(HatPosition hatPosition)
        {
            if (!controllerHats.Contains(hatPosition))
                controllerHats.Add(hatPosition);
        }

        internal void RemoveHat(HatPosition hatPosition) => controllerHats.Remove(hatPosition);

        internal void AddScreenButton(string elementName)
        {
            if (!screenButtons.Contains(elementName, StringComparer.Ordinal))
                screenButtons.Add(elementName);
        }

        internal void RemoveScreenButton(string elementName) => screenButtons.RemoveAll(screenButton => string.Equals(screenButton, elementName, StringComparison.Ordinal));

        private static bool IsScancodeDown(Input input, Scancode scancode)
        {
            return scancode switch
            {
                Scancode.LeftCtrl => input.GetKeyDown(Key.LeftCtrl) || input.GetKeyDown(Key.RightCtrl),
                Scancode.LeftShift => input.GetKeyDown(Key.LeftShift) || input.GetKeyDown(Key.RightShift),
                Scancode.LeftAlt => input.GetKeyDown(Key.LeftAlt) || input.GetKeyDown(Key.RightAlt),
                _ => input.GetScancodeDown(scancode)
            };
        }
    }
}

public readonly record struct ControllerButtonMapping(bool IsController, uint Button)
{
    public static ControllerButtonMapping ForController(ControllerButton button) => new(true, (uint)button);

    public static ControllerButtonMapping ForJoystick(uint buttonIndex) => new(false, buttonIndex);

    public ControllerButton GetControllerButton() => (ControllerButton)Button;

    public uint GetGenericButton() => Button;
}

public readonly record struct ControllerAxisMapping(bool IsController, uint Axis, float Neutral, float Pressed)
{
    public static ControllerAxisMapping ForController(ControllerAxis axis, float neutral = 0.0f, float pressed = 1.0f) => new(true, (uint)axis, neutral, pressed);

    public static ControllerAxisMapping ForJoystick(uint axis, float neutral = 0.0f, float pressed = 1.0f) => new(false, axis, neutral, pressed);

    public ControllerAxis GetControllerAxis() => (ControllerAxis)Axis;

    public uint GetGenericAxis() => Axis;

    public bool OverlapsWith(ControllerAxisMapping mapping)
    {
        if (mapping.IsController != IsController || mapping.Axis != Axis)
            return false;

        var min = Math.Min(Neutral, Pressed);
        var max = Math.Max(Neutral, Pressed);
        var otherMin = Math.Min(mapping.Neutral, mapping.Pressed);
        var otherMax = Math.Max(mapping.Neutral, mapping.Pressed);
        return !(max <= otherMin || otherMax <= min);
    }

    public float Translate(float position, float deadZone)
    {
        if (MathDefs.ApproximatelyEquivalent(position, Pressed))
            return 1.0f;

        var positive = Pressed >= Neutral;
        var neutral = positive ? Math.Min(Neutral + deadZone, Pressed) : Math.Max(Neutral - deadZone, Pressed);
        var min = Math.Min(neutral, Pressed);
        var max = Math.Max(neutral, Pressed);
        if (position < min || position > max)
            return 0.0f;

        var range = Math.Max(max - min, MathDefs.Epsilon);
        return positive ? (position - min) / range : (max - position) / range;
    }
}
