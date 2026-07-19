using System.Runtime.InteropServices;

using Microsoft.Extensions.Logging;

namespace RedBolt;

public class InputMap : SimpleResource
{
    private static readonly string[] ScancodeNames =
    [
        "", // 0
        "Ctrl", "Shift", "Alt", "A", // 4
        "B", "C", "D", "E", "F", "G", // 10
        "H", "I", "J", "K", "L", "M", "N", "O", "P", "Q", // 20
        "R", "S", "T", "U", "V", "W", "X", "Y", "Z", "1", // 30
        "2", "3", "4", "5", "6", "7", "8", "9", "0", "Return", // 40
        "Escape", "Backspace", "Tab", "Space", "-", "=", "[", "]", "\\", "#", // 50
        ";", "'", "`", ",", ".", "/", "CapsLock", "F1", "F2", "F3", // 60
        "F4", "F5", "F6", "F7", "F8", "F9", "F10", "F11", "F12", "PrintScreen", // 70
        "ScrollLock", "Pause", "Insert", "Home", "PageUp", "Delete", "End", "PageDown", "Right", "Left", // 80
        "Down", "Up", "Numlock", "Keypad /", "Keypad *", "Keypad -", "Keypad +", "Keypad Enter", "Keypad 1", "Keypad 2", // 90
        "Keypad 3", "Keypad 4", "Keypad 5", "Keypad 6", "Keypad 7", "Keypad 8", "Keypad 9", "Keypad 0", "Keypad .", "Non US Backslash", // 100
        "Application", "Power", "Keypad =", "F13", "F14", "F15", "F16", "F17", "F18", "F19", // 110
        "F20", "F21", "F22", "F23", "F24", // 115
        "Execute", "Help", "Menu", "Select", "Stop", // 120
        "Again", "Undo", "Cut", "Copy", "Paste", "Find", "Mute", "Volume Up", "Volume Down", "Locking Caps Lock", // 130
        "Locking Num Lock", "Locking Scroll Lock", "KP Comma", // 133
        "KP EQUALSAS400", "International1", // 135
        "International2", "International3", "International4", "International5", "International6", // 140
        "International7", "International8", "International9", "Lang1", "Lang2", "Lang3", "Lang4", "Lang5", "Lang6", "Lang7", // 150
        "Lang8", "Lang9", "AltErase", "SysReq", "Cancel", "Clear", "Prior", "Return", "Separator", "Out", // 160
        "Oper", "Clear / Again", "CrSel", "ExSel", // 164
        "165", "166", "167", "168", "169", "170", // 170
        "171", "172", "173", "174", "175", "Keypad 00", // 176
        "Keypad 000", "ThousandsSeparator", "DecimalSeparator", "CurrencyUnit", // 180
        "CurrencySubUnit", "Keypad (", "Keypad )", "Keypad {", "Keypad }", "Keypad Tab", "Keypad Backspace", "Keypad A", "Keypad B", "Keypad C", // 190
        "Keypad D", "Keypad E", "Keypad F", "Keypad XOR", "Keypad ^", "Keypad %", "Keypad <", "Keypad >", "Keypad &", "Keypad &&", // 200
        "Keypad |", "Keypad ||", "Keypad :", "Keypad #", "Keypad Space", "Keypad @", "Keypad !", "Keypad MemStore", "Keypad MemRecall", "Keypad MemClear", // 210
        "Keypad MemAdd", "Keypad MemSubtract", "Keypad MemMultiply", "Keypad MemDivide", "Keypad +/-", "Keypad Clear", "Keypad ClearEntry", "Keypad Binary", "Keypad Octal", "Keypad Decimal", // 220
        "Keypad Hexadecimal", "222", "223", "Left Ctrl", "Left Shift", "Left Alt", "Left GUI", "Right Ctrl", "Right Shift", "Right Alt", // 230
        "Right GUI", "232", "233", "234", "235", "236", "237", "238", "239", "240", "241", "242", "243", "244", "245", "246", "247", "248", "249", "250", "251", "252", "253", "254", "255", "256",
        "ModeSwitch", // 257
        "AudioNext", "AudioPrev", "AudioStop", // 260
        "AudioPlay", "AudioMute", "MediaSelect", "WWW", "Mail", "Calculator", "Computer", "AC Search", "AC Home", "AC Back", // 270
        "AC Forward", "AC Stop", "AC Refresh", "AC Bookmarks", "BrightnessDown", "BrightnessUp", "DisplaySwitch", "KBDIllumToggle", "KBDIllumDown", "KBDIllumUp", // 280
        "Eject", "Sleep", "App1", "App2", "AudioRewind", "AudioFastForward", "SoftLeft", "SoftRight", "Call", "EndCall" // 290
    ];

    private static readonly string[] ControllerButtonNames =
    [
        "A", "B", "X", "Y", "Back", "Guide", "Start", "LeftStick", "RightStick", "LeftShoulder", "RightShoulder", "Up",
        "Down", "Left", "Right", "Misc1", "Paddle1", "Paddle2", "Paddle3", "Paddle4", "Touchpad"
    ];

    private static readonly string[] ControllerHatNames = ["Up", "Right", "Down", "Left"];
    private static readonly string[] MouseButtonNames = ["Left", "Middle", "Right", "X1", "X2"];

    public readonly float DefaultDeadZone = 0.1f;

    private static readonly ActionMapping EmptyMapping = new();
    /// <summary>All mapped actions.</summary>
    private readonly Dictionary<string, ActionMapping> _actions = new(StringComparer.Ordinal);
    /// <summary>Extra data: sensitivity, controller axis dead zone, etc.</summary>
    private readonly StringVariantMap _metadata = new();
    /// <summary>Axis dead zone.</summary>
    private float _deadZone = 0.1f;
    /// <summary>Joystick to ignore (SDL gyroscope virtual joystick).</summary>
    private readonly int _ignoreJoystickId;

    /// <summary>Construct.</summary>
    public InputMap(string name)
        : base(name)
    {
        _ignoreJoystickId = this.GetSubsystem<Input>()!.FindAccelerometerJoystickId();
    }

    public override void Dispose()
    {
        _metadata.Dispose();
        _actions.Clear();
        base.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>Register object factory and attributes.</summary>
    public static void RegisterObject(Context context)
    {
        var reflection = context.AddReflection<InputMap>();
        reflection.SetObjectFactory(static () => new InputMap(string.Empty));
    }

    /// <summary>Force override of SerializeInBlock.</summary>
    public override void SerializeInBlock(Archive archive)
    {
        if (!archive.IsUnorderedAccessSupportedInCurrentBlock
            || (archive.IsInput ? archive.HasElementOrBlock("metadata") : _metadata.Count != 0))
        {
            ArchiveSerialization.SerializeValue(archive, "metadata", _metadata);
        }
        else if (archive.IsInput)
        {
            _metadata.Dispose();
        }
        ArchiveSerialization.SerializeOptionalValue(archive, "deadzone", ref _deadZone, DefaultDeadZone,
            ArchiveSerialization.SerializeValue);

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

    /// <summary>Set dead zone half-width to mitigate axis drift.</summary>
    public void SetDeadZone(float deadZone)
    {
        _deadZone = Math.Max(0.0f, deadZone);
    }

    /// <summary>Get dead zone half-width.</summary>
    public float GetDeadZone() => _deadZone;

    /// <summary>Map keyboard key to the action.</summary>
    public void MapKeyboardKey(string action, Scancode scancode)
    {
        foreach (var keyValue in _actions)
        {
            var mapping = keyValue.Value.Clone();
            mapping.KeyboardKeys.RemoveAll(value => value.Scancode == scancode);
        }

        GetOrAddMapping(action).KeyboardKeys.Add(new KeyboardKeyMapping(scancode));
    }

    /// <summary>Map controller button to the action.</summary>
    public void MapControllerButton(string action, ControllerButton button)
    {
        foreach (var keyValue in _actions)
        {
            var mapping = keyValue.Value.Clone();
            mapping.ControllerButtons.RemoveAll(
                value => value.Controller && value.Button == (uint)button);
        }

        GetOrAddMapping(action).ControllerButtons.Add(new ControllerButtonMapping(button));
    }

    /// <summary>Map generic joystick button to the action.</summary>
    public void MapJoystickButton(string action, uint buttonIndex)
    {
        foreach (var keyValue in _actions)
        {
            var mapping = keyValue.Value.Clone();
            mapping.ControllerButtons.RemoveAll(
                value => !value.Controller && value.Button == buttonIndex);
        }

        GetOrAddMapping(action).ControllerButtons.Add(new ControllerButtonMapping(buttonIndex));
    }

    /// <summary>Map controller axis to the action.</summary>
    public void MapControllerAxis(string action, ControllerAxis axis, float neutral, float pressed)
    {
        var mapping = new ControllerAxisMapping(axis, neutral, pressed);
        foreach (var keyValue in _actions)
        {
            var actionMapping = keyValue.Value.Clone();
            actionMapping.ControllerAxes.RemoveAll(value => value.OverlapsWith(mapping));
        }

        GetOrAddMapping(action).ControllerAxes.Add(mapping);
    }

    /// <summary>Map generic joystick axis to the action.</summary>
    public void MapJoystickAxis(string action, uint axis, float neutral, float pressed)
    {
        var mapping = new ControllerAxisMapping(axis, neutral, pressed);
        foreach (var keyValue in _actions)
        {
            var actionMapping = keyValue.Value.Clone();
            actionMapping.ControllerAxes.RemoveAll(value => value.OverlapsWith(mapping));
        }

        GetOrAddMapping(action).ControllerAxes.Add(mapping);
    }

    /// <summary>Map controller hat to the action.</summary>
    public void MapHat(string action, HatPosition hatPosition)
    {
        foreach (var keyValue in _actions)
        {
            var mapping = keyValue.Value.Clone();
            mapping.ControllerHats.RemoveAll(value => value.HatPosition == (uint)hatPosition);
        }

        GetOrAddMapping(action).ControllerHats.Add(new ControllerHatMapping((uint)hatPosition));
    }

    /// <summary>Map mouse button to the action.</summary>
    public void MapMouseButton(string action, MouseButton mouseButton)
    {
        uint value;
        switch (mouseButton)
        {
            case MouseButton.Left:
                value = 0;
                break;
            case MouseButton.Right:
                value = 2;
                break;
            case MouseButton.Middle:
                value = 1;
                break;
            case MouseButton.X1:
                value = 3;
                break;
            case MouseButton.X2:
                value = 4;
                break;
            default:
                Log.Logger.LogError("Can't map mouse button. Invalid MouseButton value.");
                return;
        }
        _ = value;

        foreach (var keyValue in _actions)
        {
            var mapping = keyValue.Value.Clone();
            mapping.MouseButtons.RemoveAll(item => item.MouseButton == (uint)mouseButton);
        }
        GetOrAddMapping(action).MouseButtons.Add(new MouseButtonMapping((uint)mouseButton));
    }

    /// <summary>Map screen button to the action.</summary>
    public void MapScreenButton(string action, string elementName)
    {
        foreach (var keyValue in _actions)
        {
            var mapping = keyValue.Value.Clone();
            mapping.ScreenButtons.RemoveAll(
                value => string.Equals(value.ElementName, elementName, StringComparison.Ordinal));
        }

        GetOrAddMapping(action).ScreenButtons.Add(new ScreenButtonMapping(elementName));
    }

    /// <summary>Get mapping for the action.</summary>
    public ActionMapping GetMapping(string action)
    {
        return _actions.TryGetValue(action, out var mapping) ? mapping : EmptyMapping;
    }

    /// <summary>Get mapping for the action.</summary>
    public ActionMapping GetMappingByHash(StringHash actionHash)
    {
        foreach (var action in _actions)
        {
            if (new StringHash(action.Key) == actionHash)
                return action.Value;
        }

        return EmptyMapping;
    }

    /// <summary>Add new metadata variable or overwrite old value.</summary>
    public void AddMetadata(string name, Variant value)
    {
        var copy = value.Clone();
        if (_metadata.Remove(name, out var previous))
            previous.Dispose();
        _metadata[name] = copy;
    }

    public void AddMetadata(string name, object? value)
    {
        using var variant = Variant.FromObject(value);
        AddMetadata(name, variant);
    }

    /// <summary>Remove metadata variable.</summary>
    public void RemoveMetadata(string name)
    {
        if (_metadata.Remove(name, out var value))
            value.Dispose();
    }

    /// <summary>Remove all metadata variables.</summary>
    public void RemoveAllMetadata()
    {
        _metadata.Dispose();
    }

    /// <summary>Return metadata variable.</summary>
    public Variant GetMetadata(string name)
    {
        return _metadata.GetValueOrDefault(name);
    }

    /// <summary>Return whether the resource has metadata.</summary>
    public bool HasMetadata()
    {
        return _metadata.Count != 0;
    }

    /// <summary>Get all action mappings.</summary>
    public IReadOnlyDictionary<string, ActionMapping> GetMappings() => _actions;

    /// <summary>Evaluate action state.</summary>
    public float Evaluate(string action)
    {
        if (!_actions.TryGetValue(action, out var mapping))
            return 0.0f;
        var input = this.GetSubsystem<Input>()!;
        return mapping.Evaluate(input, _deadZone, _ignoreJoystickId);
    }

    /// <summary>Evaluate action state by action string hash.</summary>
    public float EvaluateByHash(StringHash actionHash)
    {
        foreach (var action in _actions)
        {
            if (new StringHash(action.Key) == actionHash)
                return 0.0f;
        }
        return GetMappingByHash(actionHash).Evaluate(this.GetSubsystem<Input>()!, _deadZone, _ignoreJoystickId);
    }

    /// <summary>Get scancode names.</summary>
    public static IReadOnlyList<string> GetScanCodeNames() => ScancodeNames;

    /// <summary>Get controller button names.</summary>
    public static IReadOnlyList<string> GetControllerButtonNames() => ControllerButtonNames;

    /// <summary>Get controller directional pad (hat) names.</summary>
    public static IReadOnlyList<string> GetControllerHatNames() => ControllerHatNames;

    /// <summary>Get mouse button names.</summary>
    public static IReadOnlyList<string> GetMouseButtonNames() => MouseButtonNames;

    /// <summary>Load input map from config or resources.</summary>
    public static InputMap? Load(string name)
    {
        InputMap? result = null;
        if (name.Length == 0)
            return result;

        var cache = Context.Instance.GetSubsystem<ResourceCache>()!;
        var virtualFileSystem = Context.Instance.GetSubsystem<VirtualFileSystem>()!;

        var configFileId = new FileIdentifier("config", name);
        if (virtualFileSystem.Exists(configFileId))
        {
            result = new InputMap(name);
            if (result.LoadFile(configFileId.ToUri()))
                return result;
            result.Dispose();
        }
        return cache.GetResourceOrNull<InputMap>(name);
    }

    private ref ActionMapping GetOrAddMapping(string action)
    {
        ref var mapping = ref CollectionsMarshal.GetValueRefOrAddDefault(_actions, action, out var exists);
        if (!exists)
            mapping = new ActionMapping();
        return ref mapping;
    }

    public struct ActionMapping : ICloneable
    {
        public List<KeyboardKeyMapping> KeyboardKeys;
        public List<MouseButtonMapping> MouseButtons;
        public List<ControllerButtonMapping> ControllerButtons;
        public List<ControllerAxisMapping> ControllerAxes;
        public List<ControllerHatMapping> ControllerHats;
        public List<ScreenButtonMapping> ScreenButtons;

        public ActionMapping()
        {
            KeyboardKeys = [];
            MouseButtons = [];
            ControllerButtons = [];
            ControllerAxes = [];
            ControllerHats = [];
            ScreenButtons = [];
        }

        private ActionMapping(in ActionMapping source)
        {
            KeyboardKeys = new List<KeyboardKeyMapping>(source.KeyboardKeys);
            MouseButtons = new List<MouseButtonMapping>(source.MouseButtons);
            ControllerButtons = new List<ControllerButtonMapping>(source.ControllerButtons);
            ControllerAxes = new List<ControllerAxisMapping>(source.ControllerAxes);
            ControllerHats = new List<ControllerHatMapping>(source.ControllerHats);
            ScreenButtons = new List<ScreenButtonMapping>(source.ScreenButtons);
        }

        public readonly ActionMapping Clone() => new(in this);

        readonly object ICloneable.Clone() => Clone();

        /// <summary>Serialize content from/to archive. May throw ArchiveException.</summary>
        public void SerializeInBlock(Archive archive)
        {
            SerializeOptionalStructList(archive, "keys", "key", KeyboardKeys,
                static (Archive ar, ref KeyboardKeyMapping v) => v.SerializeInBlock(ar));
            SerializeOptionalStructList(archive, "buttons", "button", ControllerButtons,
                static (Archive ar, ref ControllerButtonMapping v) => v.SerializeInBlock(ar));
            SerializeOptionalStructList(archive, "axes", "axis", ControllerAxes,
                static (Archive ar, ref ControllerAxisMapping v) => v.SerializeInBlock(ar));
            SerializeOptionalStructList(archive, "hats", "hat", ControllerHats,
                static (Archive ar, ref ControllerHatMapping v) => v.SerializeInBlock(ar));
            SerializeOptionalStructList(archive, "mouseButtons", "button", MouseButtons,
                static (Archive ar, ref MouseButtonMapping v) => v.SerializeInBlock(ar));
            SerializeOptionalStructList(archive, "screenButtons", "element", ScreenButtons,
                static (Archive ar, ref ScreenButtonMapping v) => v.SerializeInBlock(ar));
        }

        private delegate void SerializeItem<T>(Archive archive, ref T value);

        private static void SerializeOptionalStructList<T>(Archive archive, string blockName, string itemName,
            List<T> values, SerializeItem<T> serialize) where T : new()
        {
            if (!archive.IsUnorderedAccessSupportedInCurrentBlock
                || (archive.IsInput ? archive.HasElementOrBlock(blockName) : values.Count != 0))
            {
                SerializeStructList(archive, blockName, itemName, values, serialize);
            }
            else if (archive.IsInput)
            {
                values.Clear();
            }
        }

        private static void SerializeStructList<T>(Archive archive, string blockName, string itemName,
            List<T> values, SerializeItem<T> serialize) where T : new()
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

        /// <summary>Evaluate action state based on current input.</summary>
        public readonly float Evaluate(Input input, float deadZone, int ignoreJoystickId)
        {
            foreach (var key in KeyboardKeys)
            {
                var scancode = key.Scancode;
                if (IsModifierScancode(scancode, input) || input.GetScancodeDown(scancode))
                    return 1.0f;
            }

            foreach (var mouseButton in MouseButtons)
            {
                if (input.GetMouseButtonDown(mouseButton.GetMask()))
                    return 1.0f;
            }

            if (ControllerAxes.Count == 0 && ControllerButtons.Count == 0 && ControllerHats.Count == 0)
                return 0.0f;

            // TODO: port UI-backed screen button evaluation when built-in rbfx UI is ported.
            // if (ui != nullptr && !screenButtons_.empty())
            // {
            //     const unsigned numTouches = input->GetNumTouches();
            //     for (unsigned i = 0; i < numTouches; ++i)
            //     {
            //         const auto touch = input->GetTouch(i);
            //         if (touch && touch->GetTouchedElement())
            //         {
            //             for (auto& screenButton : screenButtons_)
            //             {
            //                 if (screenButton.elementName_ == touch->GetTouchedElement()->GetName())
            //                     return 1.0f;
            //             }
            //         }
            //     }
            // }

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
                foreach (var controllerButton in ControllerButtons)
                {
                    if (controllerButton.Controller == isController
                        && joystickState.GetButtonDown(controllerButton.Button))
                        return 1.0f;
                }

                if (joystickState.GetNumHats() > 0)
                {
                    var hatPosition = joystickState.GetHatPosition(0);
                    foreach (var controllerHat in ControllerHats)
                    {
                        if ((hatPosition & (1 << (int)controllerHat.HatPosition)) != 0)
                            return 1.0f;
                    }
                }

                foreach (var controllerAxis in ControllerAxes)
                {
                    if (controllerAxis.Controller == isController
                        && joystickState.HasAxisPosition(controllerAxis.Axis))
                    {
                        totalSum += controllerAxis.Translate(
                            joystickState.GetAxisPosition(controllerAxis.Axis), deadZone);
                    }
                }
            }

            return MathDefs.Clamp(totalSum, 0.0f, 1.0f);
        }

        private static bool IsModifierScancode(Scancode scancode, Input input)
        {
            return scancode switch
            {
                Scancode.Ctrl => input.GetKeyDown(Key.LeftCtrl) || input.GetKeyDown(Key.RightCtrl),
                Scancode.Shift => input.GetKeyDown(Key.LeftShift) || input.GetKeyDown(Key.RightShift),
                Scancode.Alt => input.GetKeyDown(Key.LeftAlt) || input.GetKeyDown(Key.RightAlt),
                _ => false
            };
        }
    }
}

/// <summary>Helper class to translate keyboard keys.</summary>
public struct KeyboardKeyMapping
{
    /// <summary>Construct.</summary>
    public KeyboardKeyMapping() { Scancode = default; }

    /// <summary>Construct.</summary>
    public KeyboardKeyMapping(Scancode scancode) { Scancode = scancode; }

    /// <summary>Serialize content from/to archive. May throw ArchiveException.</summary>
    public void SerializeInBlock(Archive archive)
    {
        ArchiveSerialization.SerializeOptionalValue(archive, "scancode", ref Scancode, RedBolt.Scancode.Unknown,
            static (Archive ar, string name, ref Scancode value) =>
                ArchiveSerialization.SerializeEnum(ar, name, ref value, InputMap.GetScanCodeNames()));
    }

    /// <summary>Scancode.</summary>
    public Scancode Scancode;
}

/// <summary>Helper class to translate controller buttons.</summary>
public struct ControllerButtonMapping
{
    public ControllerButtonMapping()
    {
    }

    /// <summary>Construct.</summary>
    public ControllerButtonMapping(ControllerButton controllerButton)
    {
        Controller = true;
        Button = (uint)controllerButton;
    }

    /// <summary>Construct.</summary>
    public ControllerButtonMapping(uint buttonIndex)
    {
        Controller = false;
        Button = buttonIndex;
    }

    /// <summary>Get controller button.</summary>
    public ControllerButton GetControllerButton() => (ControllerButton)Button;

    /// <summary>Get generic button.</summary>
    public uint GetGenericButton() => Button;

    /// <summary>Serialize content from/to archive. May throw ArchiveException.</summary>
    public void SerializeInBlock(Archive archive)
    {
        ArchiveSerialization.SerializeOptionalValue(archive, "controller", ref Controller, false,
            ArchiveSerialization.SerializeValue);
        if (Controller)
        {
            var button = (ControllerButton)Button;
            ArchiveSerialization.SerializeOptionalValue(archive, "button", ref button, default,
                static (Archive ar, string name, ref ControllerButton value) =>
                    ArchiveSerialization.SerializeEnum(ar, name, ref value, InputMap.GetControllerButtonNames()));
            Button = (uint)button;
        }
        else
        {
            ArchiveSerialization.SerializeOptionalValue(archive, "button", ref Button, 0u,
                ArchiveSerialization.SerializeValue);
        }
    }

    public bool Controller;
    public uint Button;
}

/// <summary>Helper class to translate controller axis.</summary>
public struct ControllerAxisMapping
{
    public ControllerAxisMapping()
    {
        Neutral = 0.0f;
        Pressed = 1.0f;
    }

    public ControllerAxisMapping(ControllerAxis controllerAxis, float neutral = 0.0f, float pressed = 1.0f)
    {
        Controller = true;
        Axis = (uint)controllerAxis;
        Neutral = neutral;
        Pressed = pressed;
    }

    public ControllerAxisMapping(uint axisIndex, float neutral = 0.0f, float pressed = 1.0f)
    {
        Controller = false;
        Axis = axisIndex;
        Neutral = neutral;
        Pressed = pressed;
    }

    /// <summary>Get controller button.</summary>
    public ControllerAxis GetControllerAxis() => (ControllerAxis)Axis;

    /// <summary>Get generic button.</summary>
    public uint GetGenericAxis() => Axis;

    /// <summary>Serialize content from/to archive. May throw ArchiveException.</summary>
    public void SerializeInBlock(Archive archive)
    {
        ArchiveSerialization.SerializeOptionalValue(archive, "controller", ref Controller, false,
            ArchiveSerialization.SerializeValue);
        ArchiveSerialization.SerializeOptionalValue(archive, "axis", ref Axis, 0u,
            ArchiveSerialization.SerializeValue);
        ArchiveSerialization.SerializeOptionalValue(archive, "neutral", ref Neutral, 0.0f,
            ArchiveSerialization.SerializeValue);
        ArchiveSerialization.SerializeOptionalValue(archive, "pressed", ref Pressed, 1.0f,
            ArchiveSerialization.SerializeValue);
    }

    /// <summary>Check if the mapping overlaps with the other one, including matching axis index and controller type.</summary>
    public bool OverlapsWith(ControllerAxisMapping mapping)
    {
        if (mapping.Controller != Controller || mapping.Axis != Axis)
            return false;

        var min = Math.Min(Neutral, Pressed);
        var max = Math.Max(Neutral, Pressed);
        var otherMin = Math.Min(mapping.Neutral, mapping.Pressed);
        var otherMax = Math.Max(mapping.Neutral, mapping.Pressed);
        return !(max <= otherMin || otherMax <= min);
    }

    /// <summary>Translate axis value into 0..1 range. Returns 0 if the value is outside of the range.</summary>
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

    public bool Controller;
    public uint Axis;
    public float Neutral;
    public float Pressed;
}

/// <summary>Helper class to translate controller hat position.</summary>
public struct ControllerHatMapping
{
    /// <summary>Construct.</summary>
    public ControllerHatMapping() { HatPosition = 0; }

    /// <summary>Construct.</summary>
    public ControllerHatMapping(uint hatPosition) { HatPosition = hatPosition; }

    /// <summary>Serialize content from/to archive. May throw ArchiveException.</summary>
    public void SerializeInBlock(Archive archive)
    {
        var hatPosition = (RedBolt.HatPosition)HatPosition;
        ArchiveSerialization.SerializeOptionalValue(archive, "hat", ref hatPosition, default,
            static (Archive ar, string name, ref RedBolt.HatPosition value) =>
                ArchiveSerialization.SerializeEnum(ar, name, ref value, InputMap.GetControllerHatNames()));
        HatPosition = (uint)hatPosition;
    }

    /// <summary>Hat position.</summary>
    public uint HatPosition;
}

/// <summary>Helper class to translate mouse button.</summary>
public struct MouseButtonMapping
{
    /// <summary>Construct.</summary>
    public MouseButtonMapping() { MouseButton = 0; }

    /// <summary>Construct.</summary>
    public MouseButtonMapping(uint mouseButton) { MouseButton = mouseButton; }

    /// <summary>Serialize content from/to archive. May throw ArchiveException.</summary>
    public void SerializeInBlock(Archive archive)
    {
        var button = (RedBolt.MouseButton)MouseButton;
        ArchiveSerialization.SerializeOptionalValue(archive, "button", ref button, default,
            static (Archive ar, string name, ref RedBolt.MouseButton value) =>
                ArchiveSerialization.SerializeEnum(ar, name, ref value, InputMap.GetMouseButtonNames()));
        MouseButton = (uint)button;
    }

    /// <summary>Get mouse button mask.</summary>
    public readonly MouseButton GetMask() => (MouseButton)(1u << (int)MouseButton);

    /// <summary>Mouse button.</summary>
    public uint MouseButton;
}

/// <summary>Helper class to translate UI button to input value.</summary>
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
        ArchiveSerialization.SerializeOptionalValue(archive, "elementName", ref name, string.Empty,
            ArchiveSerialization.SerializeValue);
        ElementName = name;
    }

    /// <summary>Element name.</summary>
    public string ElementName;
}
