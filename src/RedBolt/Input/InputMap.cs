using System.Numerics;
using System.Text;

namespace RedBolt;

public sealed class InputMap : ResourceWithMetadata
{
    public const float DefaultDeadZone = 0.1f;

    private static readonly ActionMapping EmptyMapping = new();
    private readonly Dictionary<string, ActionMapping> _actions = new(StringComparer.Ordinal);
    private float _deadZone = DefaultDeadZone;
    private readonly int _ignoreJoystickId;

    public InputMap(string name, Input? input = null)
        : base(name)
    {
        Input = input;
        _ignoreJoystickId = input?.FindAccelerometerJoystickId() ?? -1;
    }

    public Input? Input { get; private set; }

    public IReadOnlyDictionary<string, ActionMapping> Mappings => _actions;

    public override void SerializeInBlock(Archive archive)
    {
        ArgumentNullException.ThrowIfNull(archive);
        ArchiveSerialization.SerializeValue(archive, "deadZone", ref _deadZone);
        SerializeMetadata(archive);

        var count = archive.IsInput ? 0u : (uint)_actions.Count;
        using var actionsBlock = archive.OpenArrayBlock("actions", count);
        if (archive.IsInput)
        {
            _actions.Clear();
            for (var i = 0u; i < actionsBlock.SizeHint; ++i)
            {
                using var actionBlock = archive.OpenUnorderedBlock("action");
                var name = string.Empty;
                ArchiveSerialization.SerializeValue(archive, "name", ref name);
                var mapping = new ActionMapping();
                mapping.SerializeInBlock(archive);
                if (!string.IsNullOrEmpty(name))
                    _actions[name] = mapping;
            }
        }
        else
        {
            foreach (var (nameValue, mapping) in _actions)
            {
                using var actionBlock = archive.OpenUnorderedBlock("action");
                var name = nameValue;
                ArchiveSerialization.SerializeValue(archive, "name", ref name);
                mapping.SerializeInBlock(archive);
            }
        }
    }

    public override bool BeginLoad(Stream source)
    {
        ArgumentNullException.ThrowIfNull(source);
        try
        {
            using var memory = new MemoryStream();
            source.CopyTo(memory);
            var data = memory.ToArray();
            Archive archive = DetectFormat(data) switch
            {
                InternalResourceFormat.Json => new JSONInputArchive(Context.Instance, Encoding.UTF8.GetString(data)),
                InternalResourceFormat.Xml => new XMLInputArchive(Context.Instance, Encoding.UTF8.GetString(data)),
                InternalResourceFormat.Binary => new BinaryInputArchive(Context.Instance, new MemoryBuffer(data.AsMemory(SimpleResource.DefaultBinaryMagic.Length))),
                _ => throw new InvalidDataException("Unknown input map format.")
            };
            using (archive.OpenUnorderedBlock("resource"))
                SerializeInBlock(archive);
            archive.Flush();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public override bool Save(Stream destination) => Save(destination, InternalResourceFormat.Json);

    public bool Save(Stream destination, InternalResourceFormat format)
    {
        ArgumentNullException.ThrowIfNull(destination);
        try
        {
            switch (format)
            {
                case InternalResourceFormat.Json:
                {
                    var archive = new JSONOutputArchive(Context.Instance);
                    using (archive.OpenUnorderedBlock("resource")) SerializeInBlock(archive);
                    destination.Write(Encoding.UTF8.GetBytes(archive.ToJsonString()));
                    return true;
                }
                case InternalResourceFormat.Xml:
                {
                    var archive = new XMLOutputArchive(Context.Instance);
                    using (archive.OpenUnorderedBlock("resource")) SerializeInBlock(archive);
                    destination.Write(Encoding.UTF8.GetBytes(archive.ToXmlString()));
                    return true;
                }
                case InternalResourceFormat.Binary:
                {
                    destination.Write(SimpleResource.DefaultBinaryMagic);
                    var buffer = new VectorBuffer();
                    var archive = new BinaryOutputArchive(Context.Instance, buffer);
                    using (archive.OpenUnorderedBlock("resource")) SerializeInBlock(archive);
                    archive.Flush();
                    destination.Write(buffer.Buffer[..checked((int)buffer.Size)]);
                    return true;
                }
                default:
                    return false;
            }
        }
        catch
        {
            return false;
        }
    }

    public void SetInput(Input? input)
    {
        Input = input;
    }

    public void SetDeadZone(float deadZone)
    {
        this._deadZone = Math.Max(deadZone, 0.0f);
    }

    public float GetDeadZone() => _deadZone;

    public void MapKeyboardKey(string action, Scancode scancode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        foreach (var mapping in _actions.Values)
            mapping.RemoveKeyboardKey(scancode);

        GetOrAddMapping(action).AddKeyboardKey(scancode);
    }

    public void MapMouseButton(string action, MouseButton mouseButton)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        foreach (var mapping in _actions.Values)
            mapping.RemoveMouseButton(mouseButton);

        GetOrAddMapping(action).AddMouseButton(mouseButton);
    }

    public void MapControllerButton(string action, ControllerButton button)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        foreach (var mapping in _actions.Values)
            mapping.RemoveControllerButton(button);

        GetOrAddMapping(action).AddControllerButton(button);
    }

    public void MapJoystickButton(string action, uint buttonIndex)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        foreach (var mapping in _actions.Values)
            mapping.RemoveJoystickButton(buttonIndex);

        GetOrAddMapping(action).AddJoystickButton(buttonIndex);
    }

    public void MapControllerAxis(string action, ControllerAxis axis, float neutral, float pressed)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        var mapping = ControllerAxisMapping.ForController(axis, neutral, pressed);
        foreach (var actionMapping in _actions.Values)
            actionMapping.RemoveControllerAxis(mapping);

        GetOrAddMapping(action).AddControllerAxis(mapping);
    }

    public void MapJoystickAxis(string action, uint axis, float neutral, float pressed)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        var mapping = ControllerAxisMapping.ForJoystick(axis, neutral, pressed);
        foreach (var actionMapping in _actions.Values)
            actionMapping.RemoveControllerAxis(mapping);

        GetOrAddMapping(action).AddControllerAxis(mapping);
    }

    public void MapHat(string action, HatPosition hatPosition)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        foreach (var mapping in _actions.Values)
            mapping.RemoveHat(hatPosition);

        GetOrAddMapping(action).AddHat(hatPosition);
    }

    public void MapScreenButton(string action, string elementName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        ArgumentException.ThrowIfNullOrWhiteSpace(elementName);
        foreach (var mapping in _actions.Values)
            mapping.RemoveScreenButton(elementName);

        GetOrAddMapping(action).AddScreenButton(elementName);
    }

    public ActionMapping GetMapping(string action)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        return _actions.GetValueOrDefault(action) ?? EmptyMapping;
    }

    public ActionMapping GetMappingByHash(StringHash actionHash)
    {
        foreach (var action in _actions)
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
        return GetMapping(action).Evaluate(input, _deadZone, _ignoreJoystickId);
    }

    public float EvaluateByHash(StringHash actionHash) => Input is not null ? EvaluateByHash(actionHash, Input) : 0.0f;

    public float EvaluateByHash(StringHash actionHash, Input input)
    {
        ArgumentNullException.ThrowIfNull(input);
        return GetMappingByHash(actionHash).Evaluate(input, _deadZone, _ignoreJoystickId);
    }

    public static IReadOnlyList<string> GetScanCodeNames() => Enum.GetNames<Scancode>();

    public static IReadOnlyList<string> GetControllerButtonNames() => Enum.GetNames<ControllerButton>();

    public static IReadOnlyList<string> GetControllerHatNames() => Enum.GetNames<HatPosition>();

    public static IReadOnlyList<string> GetMouseButtonNames() => Enum.GetNames<MouseButton>();

    private ActionMapping GetOrAddMapping(string action)
    {
        if (!_actions.TryGetValue(action, out var mapping))
        {
            mapping = new ActionMapping();
            _actions.Add(action, mapping);
        }

        return mapping;
    }

    private void SerializeMetadata(Archive archive)
    {
        var count = archive.IsInput ? 0u : (uint)MetadataKeys.Count;
        using var metadataBlock = archive.OpenArrayBlock("metadata", count);
        if (archive.IsInput)
        {
            RemoveAllMetadata();
            for (var i = 0u; i < metadataBlock.SizeHint; ++i)
            {
                using var itemBlock = archive.OpenUnorderedBlock("item");
                var name = string.Empty;
                var value = default(Variant);
                ArchiveSerialization.SerializeValue(archive, "name", ref name);
                using (archive.OpenUnorderedBlock("value")) ArchiveSerialization.SerializeVariantInBlock(archive, ref value);
                if (!string.IsNullOrEmpty(name)) AddMetadata(name, value);
            }
        }
        else
        {
            foreach (var keyValue in MetadataKeys)
            {
                using var itemBlock = archive.OpenUnorderedBlock("item");
                var name = keyValue;
                var value = GetMetadata(keyValue);
                ArchiveSerialization.SerializeValue(archive, "name", ref name);
                using (archive.OpenUnorderedBlock("value")) ArchiveSerialization.SerializeVariantInBlock(archive, ref value);
            }
        }
    }

    private static InternalResourceFormat DetectFormat(ReadOnlySpan<byte> data)
    {
        if (data.StartsWith(SimpleResource.DefaultBinaryMagic)) return InternalResourceFormat.Binary;
        foreach (var value in data)
        {
            if (char.IsWhiteSpace((char)value)) continue;
            return value switch { (byte)'{' => InternalResourceFormat.Json, (byte)'<' => InternalResourceFormat.Xml, _ => InternalResourceFormat.Unknown };
        }
        return InternalResourceFormat.Unknown;
    }

    public sealed class ActionMapping
    {
        private readonly List<KeyboardKeyMapping> _keyboardKeys = [];
        private readonly List<MouseButtonMapping> _mouseButtons = [];
        private readonly List<ControllerButtonMapping> _controllerButtons = [];
        private readonly List<ControllerAxisMapping> _controllerAxes = [];
        private readonly List<ControllerHatMapping> _controllerHats = [];
        private readonly List<ScreenButtonMapping> _screenButtons = [];

        public IReadOnlyList<KeyboardKeyMapping> KeyboardKeys => _keyboardKeys;

        public IReadOnlyList<MouseButtonMapping> MouseButtons => _mouseButtons;

        public IReadOnlyList<ControllerButtonMapping> ControllerButtons => _controllerButtons;

        public IReadOnlyList<ControllerAxisMapping> ControllerAxes => _controllerAxes;

        public IReadOnlyList<ControllerHatMapping> ControllerHats => _controllerHats;

        public IReadOnlyList<ScreenButtonMapping> ScreenButtons => _screenButtons;

        public void SerializeInBlock(Archive archive)
        {
            SerializeStructList(archive, "keys", "key", _keyboardKeys,
                static (Archive ar, ref KeyboardKeyMapping v) => v.SerializeInBlock(ar));
            SerializeStructList(archive, "mouseButtons", "button", _mouseButtons,
                static (Archive ar, ref MouseButtonMapping v) => v.SerializeInBlock(ar));
            SerializeStructList(archive, "buttons", "button", _controllerButtons,
                static (Archive ar, ref ControllerButtonMapping v) => v.SerializeInBlock(ar));
            SerializeStructList(archive, "axes", "axis", _controllerAxes,
                static (Archive ar, ref ControllerAxisMapping v) => v.SerializeInBlock(ar));
            SerializeStructList(archive, "hats", "hat", _controllerHats,
                static (Archive ar, ref ControllerHatMapping v) => v.SerializeInBlock(ar));
            SerializeStructList(archive, "screenButtons", "element", _screenButtons,
                static (Archive ar, ref ScreenButtonMapping v) => v.SerializeInBlock(ar));
        }

        private delegate void SerializeItem<T>(Archive archive, ref T value);

        private static void SerializeStructList<T>(Archive archive, string blockName, string itemName, List<T> values, SerializeItem<T> serialize) where T : new()
        {
            var count = archive.IsInput ? 0u : (uint)values.Count;
            using var arrayBlock = archive.OpenArrayBlock(blockName, count);
            if (archive.IsInput)
            {
                values.Clear();
                for (var i = 0u; i < arrayBlock.SizeHint; ++i)
                {
                    using var itemBlock = archive.OpenUnorderedBlock(itemName);
                    var value = new T(); serialize(archive, ref value); values.Add(value);
                }
            }
            else
            {
                foreach (var source in values)
                {
                    using var itemBlock = archive.OpenUnorderedBlock(itemName);
                    var value = source; serialize(archive, ref value);
                }
            }
        }

        // TODO: port - C++ suppresses keyboard input while a built-in UI element has focus; rbfx built-in UI is not ported.
        public float Evaluate(Input input, float deadZone, int ignoreJoystickId)
        {
            ArgumentNullException.ThrowIfNull(input);
            foreach (var key in _keyboardKeys)
            {
                var scancode = key.Scancode;
                if (IsModifierScancode(scancode, input) || input.GetScancodeDown(scancode))
                    return 1.0f;
            }

            foreach (var mouseButton in _mouseButtons)
            {
                if (input.GetMouseButtonDown(mouseButton.GetMask()))
                    return 1.0f;
            }

            var totalSum = 0.0f;
            for (uint joystickIndex = 0; joystickIndex < input.GetNumJoysticks(); ++joystickIndex)
            {
                var joystick = input.GetJoystickByIndex(joystickIndex);
                if (joystick is null)
                    continue;
                var joystickState = joystick.Value;
                if (joystickState.JoystickID == ignoreJoystickId)
                    continue;

                var isController = joystickState.IsController();
                foreach (var controllerButton in _controllerButtons)
                {
                    var matches = controllerButton.IsController == isController;
                    var pressed = controllerButton.IsController
                        ? joystickState.GetButtonDown((uint)controllerButton.GetControllerButton())
                        : joystickState.GetButtonDown(controllerButton.GetGenericButton());
                    if (matches && pressed)
                        return 1.0f;
                }

                foreach (var controllerAxis in _controllerAxes)
                {
                    if (controllerAxis.IsController != isController)
                        continue;
                    var axis = controllerAxis.IsController ? (uint)controllerAxis.GetControllerAxis() : controllerAxis.GetGenericAxis();
                    if (joystickState.HasAxisPosition(axis))
                        totalSum += controllerAxis.Translate(joystickState.GetAxisPosition(axis), deadZone);
                }

                if (joystickState.GetNumHats() > 0)
                {
                    var hatPosition = joystickState.GetHatPosition(0);
                    foreach (var controllerHat in _controllerHats)
                    {
                        if ((hatPosition & (1 << (int)controllerHat.HatPosition)) != 0)
                            return 1.0f;
                    }
                }
            }

            foreach (var screenButton in _screenButtons)
            {
                if (input.GetScreenButtonDown(screenButton.ElementName))
                    return 1.0f;
            }

            return MathDefs.Clamp(totalSum, 0.0f, 1.0f);
        }

        internal void AddKeyboardKey(Scancode scancode)
        {
            var mapping = new KeyboardKeyMapping(scancode);
            if (!_keyboardKeys.Contains(mapping))
                _keyboardKeys.Add(mapping);
        }

        internal void RemoveKeyboardKey(Scancode scancode) => _keyboardKeys.RemoveAll(m => m.Scancode == scancode);

        internal void AddMouseButton(MouseButton mouseButton)
        {
            if (mouseButton == MouseButton.None)
                return;

            var btnIndex = (uint)BitOperations.TrailingZeroCount((uint)mouseButton);
            var mapping = new MouseButtonMapping(btnIndex);
            if (!_mouseButtons.Contains(mapping))
                _mouseButtons.Add(mapping);
        }

        internal void RemoveMouseButton(MouseButton mouseButton) => _mouseButtons.RemoveAll(m => m.MouseButton == (uint)BitOperations.TrailingZeroCount((uint)mouseButton));

        internal void AddControllerButton(ControllerButton button)
        {
            var mapping = ControllerButtonMapping.ForController(button);
            if (!_controllerButtons.Contains(mapping))
                _controllerButtons.Add(mapping);
        }

        internal void RemoveControllerButton(ControllerButton button) => _controllerButtons.RemoveAll(mapping => mapping.IsController && mapping.Button == (uint)button);

        internal void AddJoystickButton(uint buttonIndex)
        {
            var mapping = ControllerButtonMapping.ForJoystick(buttonIndex);
            if (!_controllerButtons.Contains(mapping))
                _controllerButtons.Add(mapping);
        }

        internal void RemoveJoystickButton(uint buttonIndex) => _controllerButtons.RemoveAll(mapping => !mapping.IsController && mapping.Button == buttonIndex);

        internal void AddControllerAxis(ControllerAxisMapping mapping) => _controllerAxes.Add(mapping);

        internal void RemoveControllerAxis(ControllerAxisMapping mapping) => _controllerAxes.RemoveAll(existing => existing.OverlapsWith(mapping));

        internal void AddHat(HatPosition hatPosition)
        {
            var mapping = new ControllerHatMapping((uint)hatPosition);
            if (!_controllerHats.Contains(mapping))
                _controllerHats.Add(mapping);
        }

        internal void RemoveHat(HatPosition hatPosition) => _controllerHats.RemoveAll(m => m.HatPosition == (uint)hatPosition);

        internal void AddScreenButton(string elementName)
        {
            if (string.IsNullOrEmpty(elementName))
                return;

            foreach (var screenButton in _screenButtons)
            {
                if (string.Equals(screenButton.ElementName, elementName, StringComparison.Ordinal))
                    return;
            }

            _screenButtons.Add(new ScreenButtonMapping(elementName));
        }

        internal void RemoveScreenButton(string elementName) => _screenButtons.RemoveAll(screenButton => string.Equals(screenButton.ElementName, elementName, StringComparison.Ordinal));

        private static bool IsModifierScancode(Scancode scancode, Input input)
        {
            return scancode switch
            {
                Scancode.LeftCtrl => input.GetKeyDown(Key.LeftCtrl) || input.GetKeyDown(Key.RightCtrl),
                Scancode.LeftShift => input.GetKeyDown(Key.LeftShift) || input.GetKeyDown(Key.RightShift),
                Scancode.LeftAlt => input.GetKeyDown(Key.LeftAlt) || input.GetKeyDown(Key.RightAlt),
                _ => false
            };
        }
    }
}

/// <summary>Helper struct to translate keyboard keys.</summary>
public struct KeyboardKeyMapping
{
    /// <summary>Construct.</summary>
    public KeyboardKeyMapping() { Scancode = default; }

    /// <summary>Construct.</summary>
    public KeyboardKeyMapping(Scancode scancode) { Scancode = scancode; }

    /// <summary>Serialize content from/to archive. May throw ArchiveException.</summary>
    public void SerializeInBlock(Archive archive)
    {
        var rawValue = (uint)Scancode;
        ArchiveSerialization.SerializeValue(archive, "scancode", ref rawValue);
        Scancode = (Scancode)rawValue;
    }

    /// <summary>Scancode.</summary>
    public Scancode Scancode;
}

/// <summary>Helper struct to translate mouse button.</summary>
public struct MouseButtonMapping
{
    /// <summary>Construct.</summary>
    public MouseButtonMapping() { MouseButton = 0; }

    /// <summary>Construct.</summary>
    public MouseButtonMapping(uint mouseButton) { MouseButton = mouseButton; }

    /// <summary>Get mouse button mask.</summary>
    public readonly MouseButton GetMask() => (MouseButton)(1u << (int)MouseButton);

    /// <summary>Serialize content from/to archive. May throw ArchiveException.</summary>
    public void SerializeInBlock(Archive archive)
    {
        var rawValue = MouseButton;
        ArchiveSerialization.SerializeValue(archive, "mouseButton", ref rawValue);
        MouseButton = rawValue;
    }

    /// <summary>Mouse button.</summary>
    public uint MouseButton;
}

/// <summary>Helper struct to translate controller hat position.</summary>
public struct ControllerHatMapping
{
    /// <summary>Construct.</summary>
    public ControllerHatMapping() { HatPosition = 0; }

    /// <summary>Construct.</summary>
    public ControllerHatMapping(uint hatPosition) { HatPosition = hatPosition; }

    /// <summary>Serialize content from/to archive. May throw ArchiveException.</summary>
    public void SerializeInBlock(Archive archive)
    {
        var rawValue = HatPosition;
        ArchiveSerialization.SerializeValue(archive, "hatPosition", ref rawValue);
        HatPosition = rawValue;
    }

    /// <summary>Hat position.</summary>
    public uint HatPosition;
}

/// <summary>Helper struct to translate UI button to input value.</summary>
public struct ScreenButtonMapping
{
    /// <summary>Construct.</summary>
    public ScreenButtonMapping() { ElementName = string.Empty; }

    /// <summary>Construct.</summary>
    public ScreenButtonMapping(string elementName) { ElementName = elementName; }

    /// <summary>Serialize content from/to archive. May throw ArchiveException.</summary>
    public void SerializeInBlock(Archive archive)
    {
        var name = ElementName;
        ArchiveSerialization.SerializeValue(archive, "elementName", ref name);
        ElementName = name;
    }

    /// <summary>Element name.</summary>
    public string ElementName;
}

public record struct ControllerButtonMapping(bool IsController, uint Button)
{
    public static ControllerButtonMapping ForController(ControllerButton button) => new(true, (uint)button);

    public static ControllerButtonMapping ForJoystick(uint buttonIndex) => new(false, buttonIndex);

    public ControllerButton GetControllerButton() => (ControllerButton)Button;

    public uint GetGenericButton() => Button;

    public void SerializeInBlock(Archive archive)
    {
        var controller = IsController;
        var button = Button;
        ArchiveSerialization.SerializeValue(archive, "controller", ref controller);
        ArchiveSerialization.SerializeValue(archive, "button", ref button);
        this = new ControllerButtonMapping(controller, button);
    }
}

public record struct ControllerAxisMapping(bool IsController, uint Axis, float Neutral, float Pressed)
{
    public static ControllerAxisMapping ForController(ControllerAxis axis, float neutral = 0.0f, float pressed = 1.0f) => new(true, (uint)axis, neutral, pressed);

    public static ControllerAxisMapping ForJoystick(uint axis, float neutral = 0.0f, float pressed = 1.0f) => new(false, axis, neutral, pressed);

    public ControllerAxis GetControllerAxis() => (ControllerAxis)Axis;

    public uint GetGenericAxis() => Axis;

    public void SerializeInBlock(Archive archive)
    {
        var controller = IsController;
        var axis = Axis;
        var neutral = Neutral;
        var pressed = Pressed;
        ArchiveSerialization.SerializeValue(archive, "controller", ref controller);
        ArchiveSerialization.SerializeValue(archive, "axis", ref axis);
        ArchiveSerialization.SerializeValue(archive, "neutral", ref neutral);
        ArchiveSerialization.SerializeValue(archive, "pressed", ref pressed);
        this = new ControllerAxisMapping(controller, axis, neutral, pressed);
    }

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
