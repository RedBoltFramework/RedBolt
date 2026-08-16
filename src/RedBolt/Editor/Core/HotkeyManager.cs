// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Editor.Core;

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

using RedBolt.Bindings.DearImGui;
using RedBolt.Core;
using RedBolt.Input;

/// <summary>Mouse and keyboard combination that can be used as Editor hotkey.</summary>
public sealed class EditorHotkey : ICloneable
{
    public EditorHotkey()
        : this(string.Empty)
    {
    }

    public EditorHotkey(string command)
    {
        Command = command;
    }

    private EditorHotkey(EditorHotkey other)
    {
        Command = other.Command;
        QualifiersDown = other.QualifiersDown;
        QualifiersUp = other.QualifiersUp;
        MouseButtonPressed = other.MouseButtonPressed;
        MouseButtonsDown = other.MouseButtonsDown;
        MouseButtonsUp = other.MouseButtonsUp;
        KeyPressed = other.KeyPressed;
        KeyDown = other.KeyDown;
        ScancodePressed = other.ScancodePressed;
        ScancodeDown = other.ScancodeDown;
    }

    public EditorHotkey Clone() => new(this);

    object ICloneable.Clone() => Clone();

    /// <summary>Check hotkey state.</summary>
    public bool IsValid()
    {
        return QualifiersDown != Qualifier.None
            || MouseButtonPressed != MouseButton.None
            || MouseButtonsDown != MouseButton.None
            || KeyPressed != Key.Unknown
            || KeyDown != Key.Unknown
            || ScancodePressed != Scancode.Unknown
            || ScancodeDown != Scancode.Unknown;
    }

    public bool IsTextInputFriendly()
    {
        // Ctrl and Alt hotkeys are always text-friendly, unless it is one of fixed text editor hotkeys.
        if (Test(QualifiersDown, Qualifier.Ctrl) || Test(QualifiersDown, Qualifier.Alt))
        {
            foreach (var hotkey in TextHotkeys)
            {
                var sameQualifiers = hotkey.QualifiersDown == QualifiersDown;
                var sameScancodePressed = hotkey.ScancodePressed == ScancodePressed;
                var sameScancodeDown = hotkey.ScancodePressed == ScancodeDown;
                var sameKeyPressed = hotkey.ScancodePressed == Input.GetScancodeFromKey(KeyPressed);
                var sameKeyDown = hotkey.ScancodePressed == Input.GetScancodeFromKey(KeyDown);

                if (sameQualifiers && (sameScancodePressed || sameScancodeDown || sameKeyPressed || sameKeyDown))
                    return false;
            }
            return true;
        }

        // All printable characters and some special keys are not text-friendly.
        if (ScancodePressed != Scancode.Unknown && IsInputUnavailable(ScancodePressed))
            return false;
        if (ScancodeDown != Scancode.Unknown && IsInputUnavailable(ScancodeDown))
            return false;
        if (KeyPressed != Key.Unknown && IsInputUnavailable(KeyPressed))
            return false;
        if (KeyDown != Key.Unknown && IsInputUnavailable(KeyDown))
            return false;

        // All other special keys are text-friendly.
        return true;
    }

    public bool CheckKeyboardQualifiers()
    {
        var ctrlDown = IsKeyDown(Key.LeftCtrl) || IsKeyDown(Key.RightCtrl);
        var altDown = IsKeyDown(Key.LeftAlt);
        var shiftDown = IsKeyDown(Key.LeftShift) || IsKeyDown(Key.RightShift);

        if (Test(QualifiersDown, Qualifier.Ctrl) && !ctrlDown)
            return false;
        if (Test(QualifiersDown, Qualifier.Alt) && !altDown)
            return false;
        if (Test(QualifiersDown, Qualifier.Shift) && !shiftDown)
            return false;

        if (Test(QualifiersUp, Qualifier.Ctrl) && ctrlDown)
            return false;
        if (Test(QualifiersUp, Qualifier.Alt) && altDown)
            return false;
        if (Test(QualifiersUp, Qualifier.Shift) && shiftDown)
            return false;

        return true;
    }

    public bool CheckMouseQualifiers()
    {
        foreach (var mouseButton in MouseButtons)
        {
            if (Test(MouseButtonsDown, mouseButton) && !IsMouseDown(mouseButton))
                return false;
            if (Test(MouseButtonsUp, mouseButton) && IsMouseDown(mouseButton))
                return false;
        }
        return true;
    }

    public bool CheckKeyboardPress()
    {
        if (KeyPressed != Key.Unknown && !IsKeyPressed(KeyPressed))
            return false;
        if (ScancodePressed != Scancode.Unknown && !IsKeyPressed(ScancodePressed))
            return false;
        if (KeyDown != Key.Unknown && !IsKeyDown(KeyDown))
            return false;
        if (ScancodeDown != Scancode.Unknown && !IsKeyDown(ScancodeDown))
            return false;
        return true;
    }

    public bool CheckMousePress()
    {
        if (MouseButtonPressed != MouseButton.None && !IsMouseClicked(MouseButtonPressed))
            return false;
        return true;
    }

    public bool Check()
    {
        if (!IsValid())
            return false;

        if (!CheckKeyboardQualifiers())
            return false;
        if (!CheckMouseQualifiers())
            return false;
        if (!CheckKeyboardPress())
            return false;
        if (!CheckMousePress())
            return false;

        return true;
    }

    /// <summary>Return hotkey text.</summary>
    public string GetQualifiersString()
    {
        var str = string.Empty;

        if (Test(QualifiersDown, Qualifier.Ctrl))
            str += "Ctrl+";
        else if (!Test(QualifiersUp, Qualifier.Ctrl))
            str += "[Ctrl?]+";

        if (Test(QualifiersDown, Qualifier.Alt))
            str += "Alt+";
        else if (!Test(QualifiersUp, Qualifier.Alt))
            str += "[Alt?]+";

        if (Test(QualifiersDown, Qualifier.Shift))
            str += "Shift+";
        else if (!Test(QualifiersUp, Qualifier.Shift))
            str += "[Shift?]+";

        return str;
    }

    public string GetPressString()
    {
        if (MouseButtonPressed != MouseButton.None)
            return Input.GetMouseButtonName(MouseButtonPressed);
        else if (KeyPressed != Key.Unknown)
            return Input.GetKeyName(KeyPressed);
        else if (ScancodePressed != Scancode.Unknown)
            return Input.GetScancodeName(ScancodePressed);
        else
            return string.Empty;
    }

    public string GetHoldString()
    {
        var result = string.Empty;

        foreach (var mouseButton in MouseButtons)
        {
            if (Test(MouseButtonsDown, mouseButton))
                result += Input.GetMouseButtonName(mouseButton) + "+";
        }

        if (KeyDown != Key.Unknown)
            result += Input.GetKeyName(KeyDown) + "+";
        else if (ScancodeDown != Scancode.Unknown)
            result += Input.GetScancodeName(ScancodeDown) + "+";

        if (result.EndsWith('+'))
            result = result[..^1];

        return result;
    }

    public override string ToString()
    {
        var qualifiers = GetQualifiersString();
        var press = GetPressString();
        var hold = GetHoldString();
        if (press.Length != 0)
            return qualifiers + press;
        else if (hold.Length != 0)
            return qualifiers + hold;
        else
            return string.Empty;
    }

    /// <summary>Hotkey builder.</summary>
    public EditorHotkey Press(Key key)
    {
        KeyPressed = key;
        return this;
    }

    public EditorHotkey Press(Scancode scancode)
    {
        ScancodePressed = scancode;
        return this;
    }

    public EditorHotkey Press(MouseButton button)
    {
        MouseButtonPressed = button;
        Set(ref MouseButtonsUp, button, false);
        return this;
    }

    public EditorHotkey Hold(Key key)
    {
        KeyDown = key;
        return this;
    }

    public EditorHotkey Hold(Scancode scancode)
    {
        ScancodeDown = scancode;
        return this;
    }

    public EditorHotkey Hold(MouseButton button)
    {
        Set(ref MouseButtonsDown, button, true);
        Set(ref MouseButtonsUp, button, false);
        return this;
    }

    public EditorHotkey Shift()
    {
        Set(ref QualifiersDown, Qualifier.Shift, true);
        Set(ref QualifiersUp, Qualifier.Shift, false);
        return this;
    }

    public EditorHotkey Ctrl()
    {
        Set(ref QualifiersDown, Qualifier.Ctrl, true);
        Set(ref QualifiersUp, Qualifier.Ctrl, false);
        return this;
    }

    public EditorHotkey Alt()
    {
        Set(ref QualifiersDown, Qualifier.Alt, true);
        Set(ref QualifiersUp, Qualifier.Alt, false);
        return this;
    }

    public EditorHotkey MaybeShift()
    {
        Set(ref QualifiersUp, Qualifier.Shift, false);
        return this;
    }

    public EditorHotkey MaybeCtrl()
    {
        Set(ref QualifiersUp, Qualifier.Ctrl, false);
        return this;
    }

    public EditorHotkey MaybeAlt()
    {
        Set(ref QualifiersUp, Qualifier.Alt, false);
        return this;
    }

    public EditorHotkey MaybeMouse()
    {
        MouseButtonsUp = MouseButton.None;
        return this;
    }

    public string Command;

    public Qualifier QualifiersDown;
    public Qualifier QualifiersUp = Qualifier.Shift | Qualifier.Ctrl | Qualifier.Alt;

    public MouseButton MouseButtonPressed;
    public MouseButton MouseButtonsDown;
    public MouseButton MouseButtonsUp = MouseButton.Any;

    public Key KeyPressed;
    public Key KeyDown;
    public Scancode ScancodePressed;
    public Scancode ScancodeDown;

    private static readonly EditorHotkey[] TextHotkeys =
    [
        new EditorHotkey().Press(Scancode.X).Ctrl(),
        new EditorHotkey().Press(Scancode.C).Ctrl(),
        new EditorHotkey().Press(Scancode.V).Ctrl(),
        new EditorHotkey().Press(Scancode.A).Ctrl(),
        new EditorHotkey().Press(Scancode.Z).Ctrl(),
        new EditorHotkey().Press(Scancode.Y).Ctrl(),
    ];

    private static readonly MouseButton[] MouseButtons =
    [
        MouseButton.Left,
        MouseButton.Right,
        MouseButton.Middle,
        MouseButton.X1,
        MouseButton.X2,
    ];

    private static bool IsPrintableKey(Key key)
    {
        return key >= (Key)' ' && key < (Key)127 || key == Key.Tab || key == Key.Return;
    }

    private static bool IsTextEditKey(Key key)
    {
        return key == Key.Backspace || key == Key.Delete || key == Key.Left || key == Key.Right || key == Key.Home
            || key == Key.End || key == Key.Up || key == Key.Down || key == Key.PageUp || key == Key.PageDown;
    }

    private static bool IsInputUnavailable(Key key)
    {
        return IsPrintableKey(key) || IsTextEditKey(key);
    }

    private static bool IsInputUnavailable(Scancode scancode)
    {
        var key = Input.GetKeyFromScancode(scancode);
        return IsPrintableKey(key) || IsTextEditKey(key);
    }

    private static bool IsKeyDown(Key key)
    {
        var imguiKey = ImGui.ImGuiImplSDL2KeyEventToImGuiKey((int)key, (int)Input.GetScancodeFromKey(key));
        return ImGui.IsKeyDown(imguiKey);
    }

    private static bool IsKeyPressed(Key key)
    {
        var imguiKey = ImGui.ImGuiImplSDL2KeyEventToImGuiKey((int)key, (int)Input.GetScancodeFromKey(key));
        return ImGui.IsKeyPressed(imguiKey);
    }

    private static bool IsKeyDown(Scancode scancode)
    {
        return IsKeyDown(Input.GetKeyFromScancode(scancode));
    }

    private static bool IsKeyPressed(Scancode scancode)
    {
        return IsKeyPressed(Input.GetKeyFromScancode(scancode));
    }

    private static bool IsMouseDown(MouseButton buttons)
    {
        var result = false;
        foreach (var button in MouseButtons)
            result |= Test(buttons, button) && ImGui.IsMouseDown(ToImGui(button));
        return result;
    }

    private static bool IsMouseClicked(MouseButton buttons)
    {
        var result = false;
        foreach (var button in MouseButtons)
            result |= Test(buttons, button) && ImGui.IsMouseClicked(ToImGui(button));
        return result;
    }

    private static ImGuiMouseButton ToImGui(MouseButton button)
    {
        return button switch
        {
            MouseButton.Left => ImGuiMouseButton.ImGuiMouseButtonLeft,
            MouseButton.Middle => ImGuiMouseButton.ImGuiMouseButtonMiddle,
            MouseButton.Right => ImGuiMouseButton.ImGuiMouseButtonRight,
            MouseButton.X1 => (ImGuiMouseButton)3,
            MouseButton.X2 => (ImGuiMouseButton)4,
            _ => unchecked((ImGuiMouseButton)(uint)-1),
        };
    }

    private static bool Test(Qualifier flags, Qualifier value) => (flags & value) != 0;

    private static bool Test(MouseButton flags, MouseButton value) => (flags & value) != 0;

    private static void Set(ref Qualifier flags, Qualifier value, bool enabled)
    {
        if (enabled)
            flags |= value;
        else
            flags &= ~value;
    }

    private static void Set(ref MouseButton flags, MouseButton value, bool enabled)
    {
        if (enabled)
            flags |= value;
        else
            flags &= ~value;
    }
}

/// <summary>Class used to manage and dispatch hotkeys.</summary>
public class HotkeyManager : RedObject
{
    public sealed class HotkeyBinding
    {
        public HotkeyBinding(RedObject owner, EditorHotkey hotkey, Action callback)
        {
            Owner = new WeakReference<RedObject>(owner);
            Hotkey = hotkey.Clone();
            Callback = callback;
            IsTextInputFriendly = hotkey.IsTextInputFriendly();
        }

        public HotkeyBinding(EditorHotkey hotkey)
        {
            Hotkey = hotkey.Clone();
            IsPassive = true;
            IsTextInputFriendly = hotkey.IsTextInputFriendly();
        }

        public WeakReference<RedObject>? Owner;
        public EditorHotkey Hotkey;
        public Action? Callback;
        public bool IsPassive;
        public bool IsTextInputFriendly;
    }

    public HotkeyManager()
    {
    }

    /// <summary>Bind new hotkeys. Hotkeys for expired objects will be automatically removed.</summary>
    public void BindPassiveHotkey(EditorHotkey hotkey)
    {
        var binding = new HotkeyBinding(hotkey);
        _hotkeyByCommand[hotkey.Command] = [binding];
    }

    public void BindHotkey(RedObject owner, EditorHotkey hotkey, Action callback)
    {
        var weakOwner = new OwnerReference(owner);
        var binding = new HotkeyBinding(owner, hotkey, callback);
        if (!_hotkeyByOwner.TryGetValue(weakOwner, out var ownerBindings))
        {
            ownerBindings = [];
            _hotkeyByOwner.Add(weakOwner, ownerBindings);
        }
        ownerBindings.Add(binding);

        if (!_hotkeyByCommand.TryGetValue(hotkey.Command, out var commandBindings))
        {
            commandBindings = [];
            _hotkeyByCommand.Add(hotkey.Command, commandBindings);
        }
        commandBindings.Add(binding);
    }

    public void BindHotkey<T>(T owner, EditorHotkey hotkey, Action<T> callback)
        where T : RedObject
    {
        var weakOwner = new WeakReference<T>(owner);
        BindHotkey(owner, hotkey, () =>
        {
            if (weakOwner.TryGetTarget(out var target))
                callback(target);
        });
    }

    /// <summary>Return currently bound hotkey combination for given hotkey.</summary>
    public EditorHotkey GetHotkey(string command)
    {
        var ptr = FindByCommand(command);
        return ptr?.Hotkey ?? EmptyHotkey;
    }

    public EditorHotkey GetHotkey(EditorHotkey hotkey)
    {
        return GetHotkey(hotkey.Command);
    }

    public string GetHotkeyLabel(EditorHotkey hotkey)
    {
        return GetHotkey(hotkey).ToString();
    }

    public bool IsHotkeyActive(EditorHotkey hotkey)
    {
        return GetHotkey(hotkey).Check();
    }

    public IReadOnlyDictionary<string, List<HotkeyBinding>> Bindings => _hotkeyByCommand;

    /// <summary>Routine maintenance, no user logic is performed.</summary>
    public void RemoveExpired()
    {
        HashSet<string> commandsToCheck = [];
        foreach (var elem in _hotkeyByOwner)
        {
            var owner = elem.Key;
            var bindings = elem.Value;
            if (!owner.TryGetTarget(out _) && bindings.Count != 0)
            {
                commandsToCheck.Add(bindings[0].Hotkey.Command);
                _hotkeyByOwner.Remove(owner);
            }
        }

        foreach (var command in commandsToCheck)
        {
            if (!_hotkeyByCommand.TryGetValue(command, out var bindings))
                continue;

            bindings.RemoveAll(IsBindingExpired);
            if (bindings.Count == 0)
                _hotkeyByCommand.Remove(command);
        }
    }

    public void Update()
    {
        if (_cleanupTimer.GetMSec(false) >= _cleanupMs)
        {
            _cleanupTimer.Reset();
            RemoveExpired();
        }

        _invokedCommands.Clear();

        _isTextInputConsumed = ImGui.GetIO().WantTextInput;
    }

    /// <summary>Check and invoke all hotkeys corresponding to the owner.</summary>
    public void InvokeFor(RedObject owner)
    {
        var weakOwner = new OwnerReference(owner);
        if (!_hotkeyByOwner.TryGetValue(weakOwner, out var bindings))
            return;

        foreach (var bindingPtr in bindings)
        {
            if (_isTextInputConsumed && !bindingPtr.Hotkey.IsTextInputFriendly())
                continue;

            var command = bindingPtr.Hotkey.Command;
            if (!_invokedCommands.Contains(command) && bindingPtr.Hotkey.Check())
            {
                bindingPtr.Callback!();
                _invokedCommands.Add(command);
            }
        }
    }

    private static bool IsBindingExpired(HotkeyBinding ptr)
    {
        return !ptr.IsPassive && (ptr.Owner is null || !ptr.Owner.TryGetTarget(out _));
    }

    private HotkeyBinding? FindByCommand(string command)
    {
        if (_hotkeyByCommand.TryGetValue(command, out var bindings) && bindings.Count != 0)
            return bindings[0];
        return null;
    }

    private readonly struct OwnerReference : IEquatable<OwnerReference>
    {
        public OwnerReference(RedObject owner)
        {
            _reference = new WeakReference<RedObject>(owner);
            _hashCode = RuntimeHelpers.GetHashCode(owner);
        }

        public bool TryGetTarget(out RedObject? target) => _reference.TryGetTarget(out target);

        public bool Equals(OwnerReference other)
        {
            if (ReferenceEquals(_reference, other._reference))
                return true;

            return _reference.TryGetTarget(out var target)
                && other._reference.TryGetTarget(out var otherTarget)
                && ReferenceEquals(target, otherTarget);
        }

        public override bool Equals(object? obj) => obj is OwnerReference other && Equals(other);

        public override int GetHashCode() => _hashCode;

        private readonly WeakReference<RedObject> _reference;
        private readonly int _hashCode;
    }

    private static readonly EditorHotkey EmptyHotkey = new();

    private readonly uint _cleanupMs = 1000;
    private readonly Timer _cleanupTimer = new();

    private readonly Dictionary<OwnerReference, List<HotkeyBinding>> _hotkeyByOwner = [];
    private readonly SortedDictionary<string, List<HotkeyBinding>> _hotkeyByCommand = new(StringComparer.Ordinal);

    private readonly HashSet<string> _invokedCommands = new(StringComparer.Ordinal);
    private bool _isTextInputConsumed;
}
