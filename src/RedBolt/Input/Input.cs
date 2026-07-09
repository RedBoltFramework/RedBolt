using RedBolt.Bindings.Sdl;

namespace RedBolt;

public sealed class Input
{
    private const uint SdlInitVideo = 0x00000020u;
    private static readonly Dictionary<Key, Scancode> KeyToScancode = BuildKeyToScancodeMap();
    private static readonly Dictionary<Scancode, Key> ScancodeToKey = KeyToScancode.ToDictionary(static item => item.Value, static item => item.Key);

    private readonly HashSet<Key> _keyDown = [];
    private readonly HashSet<Key> _keyPress = [];
    private readonly HashSet<Scancode> _scancodeDown = [];
    private readonly HashSet<Scancode> _scancodePress = [];
    private readonly HashSet<ControllerButton> _controllerButtonsDown = [];
    private readonly HashSet<uint> _joystickButtonsDown = [];
    private readonly Dictionary<ControllerAxis, float> _controllerAxisPositions = new();
    private readonly Dictionary<uint, float> _joystickAxisPositions = new();
    private readonly HashSet<string> _screenButtonsDown = new(StringComparer.Ordinal);
    private SdlEvent? _nativeEvent;
    private SdlWindow? _window;
    private MouseButton _mouseButtonClick;
    private MouseButton _mouseButtonDown;
    private MouseButton _mouseButtonPress;
    private int _mouseMoveWheel;
    private HatPosition _hatPosition = HatPosition.Center;
    private IntVector2 _backbufferSize = IntVector2.Zero;
    private Vector2 _systemToBackbufferScale = Vector2.One;
    private bool _lastMouseGrabbed;
    private bool _lastMouseVisible = true;
    private MouseMode _lastMouseMode = MouseMode.Absolute;

    public bool Enabled { get; set; } = true;

    public bool ExitRequested { get; private set; }

    public bool IsInitialized { get; private set; }

    public MouseMode MouseMode { get; private set; } = MouseMode.Absolute;

    public IntVector2 MouseMove { get; private set; }

    public int MouseMoveWheel => _mouseMoveWheel;

    public IntVector2 MousePosition { get; private set; } = IntVector2.Zero;

    public Qualifier Qualifiers { get; private set; }

    public Vector2 SystemToBackbufferScale => _systemToBackbufferScale;

    public bool MouseVisible { get; private set; } = true;

    public bool MouseGrabbed { get; private set; }

    public bool HasFocus { get; private set; } = true;

    public bool IsMinimized { get; private set; }

    public bool ToggleFullscreen { get; private set; } = true;

    public bool TouchEmulation { get; private set; }

    internal SdlWindow Window => _window is not null && !_window.IsNull
        ? _window
        : throw new InvalidOperationException("Input window is not initialized.");

    public nint GetWindowHandle()
    {
        return Window.Handle;
    }

    public void Initialize(EngineParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        if (IsInitialized)
            throw new InvalidOperationException("Input system is already initialized.");

        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DISPLAY")))
            throw new InvalidOperationException("RedBolt OpenGL first-cube path requires an X11 DISPLAY.");

        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SDL_VIDEODRIVER")))
            Environment.SetEnvironmentVariable("SDL_VIDEODRIVER", "x11");

        _backbufferSize = new IntVector2(parameters.WindowWidth, parameters.WindowHeight);
        _systemToBackbufferScale = Vector2.One;
        TouchEmulation = parameters.TouchEmulation;

        var initResult = SdlNativeBindings.SdlInit(SdlInitVideo);
        if (initResult != 0)
            throw new InvalidOperationException($"Generated SDL_Init binding failed with {initResult}.");

        try
        {
            CheckSdlGl(SdlNativeBindings.SdlGlSetAttribute(SdlGLattr.SdlGlContextMajorVersion, 3), "SDL_GL_SetAttribute major version");
            CheckSdlGl(SdlNativeBindings.SdlGlSetAttribute(SdlGLattr.SdlGlContextMinorVersion, 3), "SDL_GL_SetAttribute minor version");
            CheckSdlGl(SdlNativeBindings.SdlGlSetAttribute(SdlGLattr.SdlGlContextProfileMask, (int)SdlGLprofile.SdlGlContextProfileCore), "SDL_GL_SetAttribute profile mask");
            CheckSdlGl(SdlNativeBindings.SdlGlSetAttribute(SdlGLattr.SdlGlDoublebuffer, 1), "SDL_GL_SetAttribute doublebuffer");
            CheckSdlGl(SdlNativeBindings.SdlGlSetAttribute(SdlGLattr.SdlGlMultisamplebuffers, parameters.MultiSample > 1 ? 1 : 0), "SDL_GL_SetAttribute multisample buffers");
            CheckSdlGl(SdlNativeBindings.SdlGlSetAttribute(SdlGLattr.SdlGlMultisamplesamples, Math.Max(parameters.MultiSample, 0)), "SDL_GL_SetAttribute multisample samples");

            using var title = new CString(parameters.WindowTitle);
            var windowFlags = SdlWindowFlags.SdlWindowOpengl | SdlWindowFlags.SdlWindowShown;
            if (parameters.FullScreen)
                windowFlags |= SdlWindowFlags.SdlWindowFullscreenDesktop;
            else if (parameters.Borderless)
                windowFlags |= SdlWindowFlags.SdlWindowBorderless;
            if (parameters.WindowResizable)
                windowFlags |= SdlWindowFlags.SdlWindowResizable;
            if (parameters.WindowMaximize)
                windowFlags |= SdlWindowFlags.SdlWindowMaximized;

            _window = SdlNativeBindings.SdlCreateWindow(
                title.Bytes,
                x: parameters.WindowPositionX ?? 64,
                y: parameters.WindowPositionY ?? 64,
                w: parameters.WindowWidth,
                h: parameters.WindowHeight,
                flags: (uint)windowFlags);

            if (_window.IsNull)
                throw new InvalidOperationException("Generated SDL_CreateWindow binding returned a null OpenGL window.");

            if (parameters.WindowMaximize)
                SdlNativeBindings.SdlMaximizeWindow(_window);

            _nativeEvent = new SdlEvent();
            IsInitialized = true;
        }
        catch
        {
            Shutdown();
            throw;
        }
    }

    public void SetWindowTitle(string title)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        if (!IsInitialized || _window is null || _window.IsNull)
            return;

        using var pinnedTitle = new CString(title);
        SdlNativeBindings.SdlSetWindowTitle(_window, pinnedTitle.Bytes);
    }

    public void BeginFrame()
    {
        Context.Instance.SendEvent<InputEvents.InputBeginEvent>();

        ExitRequested = false;
        _keyPress.Clear();
        _scancodePress.Clear();
        _mouseButtonClick = MouseButton.None;
        _mouseButtonPress = MouseButton.None;
        _mouseMoveWheel = 0;
        MouseMove = IntVector2.Zero;
    }

    public void PollEvents()
    {
        if (!Enabled || !IsInitialized || _nativeEvent is null)
            return;

        var dearImGui = Context.Instance.GetSubsystem<IDearImGuiInputSink>();

        while (SdlNativeBindings.SdlPollEvent(_nativeEvent) != 0)
        {
            var eventType = (SdlEventType)_nativeEvent.Type;
            var eventHandledByDearImGui = dearImGui?.ProcessEvent(_nativeEvent.Handle) == true;

            if (eventHandledByDearImGui || ShouldConsumeEventByDearImGui(dearImGui, eventType))
                continue;

            if (eventType == SdlEventType.SdlQuit)
            {
                ExitRequested = true;
            }
            else if (eventType == SdlEventType.SdlKeydown)
            {
                HandleKeyDown(_nativeEvent);
            }
            else if (eventType == SdlEventType.SdlKeyup)
            {
                HandleKeyUp(_nativeEvent);
            }
            else if (eventType == SdlEventType.SdlMousebuttondown)
            {
                HandleMouseButtonDown(_nativeEvent);
            }
            else if (eventType == SdlEventType.SdlMousebuttonup)
            {
                HandleMouseButtonUp(_nativeEvent);
            }
            else if (eventType == SdlEventType.SdlMousemotion)
            {
                HandleMouseMotion(_nativeEvent);
            }
            else if (eventType == SdlEventType.SdlMousewheel)
            {
                _mouseMoveWheel += _nativeEvent.WheelY;
            }
            else if (eventType == SdlEventType.SdlWindowevent && _nativeEvent.WindowEvent == (byte)SdlWindowEventID.SdlWindoweventClose)
            {
                ExitRequested = true;
            }
            else if (eventType == SdlEventType.SdlWindowevent)
            {
                HandleWindowEvent(_nativeEvent);
            }
        }

        Context.Instance.SendEvent<InputEvents.InputEndEvent>();
    }

    private static bool ShouldConsumeEventByDearImGui(IDearImGuiInputSink? dearImGui, SdlEventType eventType)
    {
        if (dearImGui is null)
            return false;

        return eventType switch
        {
            SdlEventType.SdlKeydown or SdlEventType.SdlKeyup => dearImGui.WantCaptureKeyboard,
            SdlEventType.SdlMousebuttondown or SdlEventType.SdlMousebuttonup or SdlEventType.SdlMousemotion or SdlEventType.SdlMousewheel => dearImGui.WantCaptureMouse,
            _ => false
        };
    }

    public static Key GetKeyFromName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (TryParseNamedValue(name, out Key key))
            return key;
        return name.Length == 1 ? (Key)char.ToLowerInvariant(name[0]) : Key.Unknown;
    }

    public static Key GetKeyFromScancode(Scancode scancode) => ScancodeToKey.GetValueOrDefault(scancode, Key.Unknown);

    public static string GetKeyName(Key key)
    {
        return key switch
        {
            >= Key.A and <= Key.Z => key.ToString(),
            >= Key.Num0 and <= Key.Num9 => ((char)key).ToString(),
            Key.Unknown => string.Empty,
            _ => key.ToString()
        };
    }

    public static Scancode GetScancodeFromKey(Key key) => KeyToScancode.GetValueOrDefault(key, Scancode.Unknown);

    public static Scancode GetScancodeFromName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (TryParseNamedValue(name, out Scancode scancode))
            return scancode;
        return GetScancodeFromKey(GetKeyFromName(name));
    }

    public static string GetScancodeName(Scancode scancode) => scancode == Scancode.Unknown ? string.Empty : scancode.ToString();

    public static string GetMouseButtonName(MouseButton button)
    {
        return button switch
        {
            MouseButton.Left => nameof(MouseButton.Left),
            MouseButton.Middle => nameof(MouseButton.Middle),
            MouseButton.Right => nameof(MouseButton.Right),
            MouseButton.X1 => nameof(MouseButton.X1),
            MouseButton.X2 => nameof(MouseButton.X2),
            _ => string.Empty
        };
    }

    public bool GetKeyDown(Key key)
    {
        return _keyDown.Contains(key);
    }

    public bool GetKeyPress(Key key)
    {
        return _keyPress.Contains(key);
    }

    public bool GetScancodeDown(Scancode scancode)
    {
        return _scancodeDown.Contains(scancode);
    }

    public bool GetScancodePress(Scancode scancode)
    {
        return _scancodePress.Contains(scancode);
    }

    public bool GetMouseButtonDown(MouseButton button)
    {
        return (_mouseButtonDown & button) != 0;
    }

    public bool GetMouseButtonPress(MouseButton button)
    {
        return (_mouseButtonPress & button) != 0;
    }

    public bool GetMouseButtonClick(MouseButton button)
    {
        return (_mouseButtonClick & button) != 0;
    }

    public bool GetControllerButtonDown(ControllerButton button)
    {
        return _controllerButtonsDown.Contains(button);
    }

    public bool GetJoystickButtonDown(uint buttonIndex)
    {
        return _joystickButtonsDown.Contains(buttonIndex);
    }

    public float GetControllerAxisPosition(ControllerAxis axis)
    {
        return _controllerAxisPositions.GetValueOrDefault(axis);
    }

    public float GetJoystickAxisPosition(uint axis)
    {
        return _joystickAxisPositions.GetValueOrDefault(axis);
    }

    public HatPosition GetHatPosition()
    {
        return _hatPosition;
    }

    public bool GetHatDown(HatPosition position)
    {
        return position == HatPosition.Center
            ? _hatPosition == HatPosition.Center
            : (_hatPosition & position) == position;
    }

    public bool GetScreenButtonDown(string elementName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(elementName);
        return _screenButtonsDown.Contains(elementName);
    }

    public Vector2 GetRelativeMousePosition()
    {
        return _backbufferSize.X > 0 && _backbufferSize.Y > 0
            ? new Vector2((float)MousePosition.X / _backbufferSize.X, (float)MousePosition.Y / _backbufferSize.Y)
            : Vector2.Zero;
    }

    public IntVector2 GetMousePosition() => MousePosition;

    public IntVector2 GetMouseMove() => MouseMove;

    public int GetMouseMoveX() => MouseMove.X;

    public int GetMouseMoveY() => MouseMove.Y;

    public int GetMouseMoveWheel() => MouseMoveWheel;

    public Vector2 GetSystemToBackbufferScale() => _systemToBackbufferScale;

    public IntVector2 SystemToBackbuffer(IntVector2 value) => new(
        (int)MathF.Round(value.X * _systemToBackbufferScale.X),
        (int)MathF.Round(value.Y * _systemToBackbufferScale.Y));

    public IntVector2 BackbufferToSystem(IntVector2 value) => new(
        _systemToBackbufferScale.X != 0.0f ? (int)MathF.Round(value.X / _systemToBackbufferScale.X) : value.X,
        _systemToBackbufferScale.Y != 0.0f ? (int)MathF.Round(value.Y / _systemToBackbufferScale.Y) : value.Y);

    public void SetMousePosition(IntVector2 position)
    {
        MousePosition = position;
    }

    public void SetKey(Key key, bool newState)
    {
        SetKey(key, GetScancodeFromKey(key), newState);
    }

    public void SetKey(Key key, Scancode scancode, bool newState)
    {
        if (newState)
        {
            if (_keyDown.Add(key))
                _keyPress.Add(key);
            if (scancode != Scancode.Unknown && _scancodeDown.Add(scancode))
                _scancodePress.Add(scancode);
        }
        else
        {
            _keyDown.Remove(key);
            _scancodeDown.Remove(scancode);
        }

        UpdateQualifiers(key, newState);
    }

    public void SetMouseButton(MouseButton button, bool newState, int clicks = 0)
    {
        if (newState)
        {
            if ((_mouseButtonDown & button) == 0)
                _mouseButtonPress |= button;
            _mouseButtonDown |= button;
        }
        else
        {
            _mouseButtonDown &= ~button;
        }

        if (clicks > 0)
            _mouseButtonClick |= button;
    }

    public void SetMouseWheel(int delta)
    {
        _mouseMoveWheel += delta;
    }

    public void SetControllerButton(ControllerButton button, bool newState)
    {
        if (newState)
            _controllerButtonsDown.Add(button);
        else
            _controllerButtonsDown.Remove(button);
    }

    public void SetJoystickButton(uint buttonIndex, bool newState)
    {
        if (newState)
            _joystickButtonsDown.Add(buttonIndex);
        else
            _joystickButtonsDown.Remove(buttonIndex);
    }

    public void SetControllerAxis(ControllerAxis axis, float position)
    {
        _controllerAxisPositions[axis] = MathDefs.Clamp(position, -1.0f, 1.0f);
    }

    public void SetJoystickAxis(uint axis, float position)
    {
        _joystickAxisPositions[axis] = MathDefs.Clamp(position, -1.0f, 1.0f);
    }

    public void SetHatPosition(HatPosition position)
    {
        _hatPosition = position;
    }

    public void SetScreenButton(string elementName, bool newState)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(elementName);
        if (newState)
            _screenButtonsDown.Add(elementName);
        else
            _screenButtonsDown.Remove(elementName);
    }

    public void CenterMousePosition()
    {
        MousePosition = new IntVector2(_backbufferSize.X / 2, _backbufferSize.Y / 2);
    }

    public bool GetQualifierDown(Qualifier qualifier)
    {
        return qualifier == Qualifier.Any
            ? Qualifiers != Qualifier.None
            : (Qualifiers & qualifier) != 0;
    }

    public bool GetQualifierPress(Qualifier qualifier)
    {
        return qualifier switch
        {
            Qualifier.Shift => GetKeyPress(Key.LeftShift) || GetKeyPress(Key.RightShift),
            Qualifier.Ctrl => GetKeyPress(Key.LeftCtrl) || GetKeyPress(Key.RightCtrl),
            Qualifier.Alt => GetKeyPress(Key.LeftAlt) || GetKeyPress(Key.RightAlt),
            Qualifier.Any => GetQualifierPress(Qualifier.Shift) || GetQualifierPress(Qualifier.Ctrl) || GetQualifierPress(Qualifier.Alt),
            _ => false
        };
    }

    public void SetMouseMode(MouseMode mode, bool suppressEvent = false)
    {
        if (mode == MouseMode.Absolute)
            SetMouseGrabbed(false, suppressEvent: true);
        else if (mode == MouseMode.Relative || mode == MouseMode.Wrap)
            SetMouseGrabbed(true, suppressEvent: true);

        MouseMode = mode;
        if (!suppressEvent)
            _lastMouseMode = mode;
    }

    public void ResetMouseMode() => SetMouseMode(_lastMouseMode);

    public bool GetEnabled() => Enabled;

    public void SetEnabled(bool enabled) => Enabled = enabled;

    public void SetMouseVisible(bool enable, bool suppressEvent = false)
    {
        MouseVisible = enable;
        if (!suppressEvent)
            _lastMouseVisible = enable;
    }

    public void ResetMouseVisible() => SetMouseVisible(_lastMouseVisible);

    public void SetMouseGrabbed(bool grab, bool suppressEvent = false)
    {
        MouseGrabbed = grab;
        if (!suppressEvent)
            _lastMouseGrabbed = grab;
    }

    public void ResetMouseGrabbed() => SetMouseGrabbed(_lastMouseGrabbed);

    public void SetToggleFullscreen(bool enable) => ToggleFullscreen = enable;

    public bool GetToggleFullscreen() => ToggleFullscreen;

    public void SetTouchEmulation(bool enable) => TouchEmulation = enable;

    public bool GetTouchEmulation() => TouchEmulation;

    public bool IsMouseVisible() => MouseVisible;

    public bool IsMouseGrabbed() => MouseGrabbed;

    public bool IsMouseLocked() => MouseMode != MouseMode.Absolute;

    public bool GetFocus() => HasFocus;

    internal NativeWindowInfo GetNativeWindowInfo()
    {
        using var wmInfo = CreateInitializedWmInfo();
        if (SdlNativeBindings.SdlGetWindowWMInfo(Window, wmInfo) != SdlBool.SdlTrue)
            throw new InvalidOperationException("Generated SDL_GetWindowWMInfo binding failed for the OpenGL window.");

        if (wmInfo.Subsystem != SdlSyswmType.SdlSyswmX11)
            throw new InvalidOperationException($"RedBolt OpenGL first-cube path requires SDL X11 WM info, got {wmInfo.Subsystem}.");

        if (wmInfo.InfoX11Display == IntPtr.Zero || wmInfo.InfoX11Window == 0)
            throw new InvalidOperationException("SDL X11 WM info did not contain display and window handles.");

        return new NativeWindowInfo(wmInfo.InfoX11Display, checked((uint)wmInfo.InfoX11Window));
    }

    public void Shutdown()
    {
        _nativeEvent?.Dispose();
        _nativeEvent = null;

        if (_window is not null && !_window.IsNull)
            SdlNativeBindings.SdlDestroyWindow(_window);
        _window = null;

        if (IsInitialized)
            SdlNativeBindings.SdlQuit();

        IsInitialized = false;
        ExitRequested = false;
        _keyDown.Clear();
        _keyPress.Clear();
        _scancodeDown.Clear();
        _scancodePress.Clear();
        _controllerButtonsDown.Clear();
        _joystickButtonsDown.Clear();
        _controllerAxisPositions.Clear();
        _joystickAxisPositions.Clear();
        _screenButtonsDown.Clear();
        _mouseButtonClick = MouseButton.None;
        _mouseButtonDown = MouseButton.None;
        _mouseButtonPress = MouseButton.None;
        _mouseMoveWheel = 0;
        _hatPosition = HatPosition.Center;
        MouseMove = IntVector2.Zero;
        MousePosition = IntVector2.Zero;
        Qualifiers = Qualifier.None;
        _backbufferSize = IntVector2.Zero;
        _systemToBackbufferScale = Vector2.One;
        HasFocus = true;
        IsMinimized = false;
    }

    private void HandleKeyDown(SdlEvent @event)
    {
        var key = (Key)@event.KeyKeysymSym;
        var scancode = (Scancode)(uint)@event.KeyKeysymScancode;
        if (_keyDown.Add(key))
            _keyPress.Add(key);
        if (_scancodeDown.Add(scancode))
            _scancodePress.Add(scancode);

        UpdateQualifiers(key, isDown: true);
        if (scancode == Scancode.Escape)
            ExitRequested = true;
    }

    private void HandleKeyUp(SdlEvent @event)
    {
        var key = (Key)@event.KeyKeysymSym;
        var scancode = (Scancode)(uint)@event.KeyKeysymScancode;
        _keyDown.Remove(key);
        _scancodeDown.Remove(scancode);
        UpdateQualifiers(key, isDown: false);
    }

    private void HandleMouseButtonDown(SdlEvent @event)
    {
        var button = ToMouseButton(@event.ButtonButton);
        _mouseButtonDown |= button;
        _mouseButtonPress |= button;
    }

    private void HandleMouseButtonUp(SdlEvent @event)
    {
        var button = ToMouseButton(@event.ButtonButton);
        _mouseButtonDown &= ~button;
        _mouseButtonClick |= button;
    }

    private void HandleMouseMotion(SdlEvent @event)
    {
        MousePosition = new IntVector2(@event.MotionX, @event.MotionY);
        MouseMove = new IntVector2(MouseMove.X + @event.MotionXrel, MouseMove.Y + @event.MotionYrel);
    }

    private void HandleWindowEvent(SdlEvent @event)
    {
        var windowEvent = (SdlWindowEventID)@event.WindowEvent;
        if (windowEvent == SdlWindowEventID.SdlWindoweventFocusGained)
            HasFocus = true;
        else if (windowEvent == SdlWindowEventID.SdlWindoweventFocusLost)
            HasFocus = false;
        else if (windowEvent == SdlWindowEventID.SdlWindoweventMinimized)
            IsMinimized = true;
        else if (windowEvent == SdlWindowEventID.SdlWindoweventRestored)
            IsMinimized = false;
    }

    private void UpdateQualifiers(Key key, bool isDown)
    {
        var qualifier = key switch
        {
            Key.LeftShift or Key.RightShift => Qualifier.Shift,
            Key.LeftCtrl or Key.RightCtrl => Qualifier.Ctrl,
            Key.LeftAlt or Key.RightAlt => Qualifier.Alt,
            _ => Qualifier.None
        };

        if (qualifier == Qualifier.None)
            return;

        if (isDown)
            Qualifiers |= qualifier;
        else
            Qualifiers &= ~qualifier;
    }

    private static MouseButton ToMouseButton(byte button)
    {
        return button switch
        {
            1 => MouseButton.Left,
            2 => MouseButton.Middle,
            3 => MouseButton.Right,
            4 => MouseButton.X1,
            5 => MouseButton.X2,
            _ => MouseButton.None
        };
    }

    private static bool TryParseNamedValue<T>(string name, out T value)
        where T : struct, Enum
    {
        var normalized = name.Replace(" ", string.Empty, StringComparison.Ordinal).Replace("_", string.Empty, StringComparison.Ordinal);
        foreach (var enumName in Enum.GetNames<T>())
        {
            if (string.Equals(enumName, normalized, StringComparison.OrdinalIgnoreCase))
            {
                value = Enum.Parse<T>(enumName);
                return true;
            }
        }

        value = default;
        return false;
    }

    private static Dictionary<Key, Scancode> BuildKeyToScancodeMap()
    {
        var result = new Dictionary<Key, Scancode>
        {
            [Key.Escape] = Scancode.Escape,
            [Key.Space] = Scancode.Space,
            [Key.Right] = Scancode.Right,
            [Key.Left] = Scancode.Left,
            [Key.Down] = Scancode.Down,
            [Key.Up] = Scancode.Up,
            [Key.LeftCtrl] = Scancode.LeftCtrl,
            [Key.LeftShift] = Scancode.LeftShift,
            [Key.LeftAlt] = Scancode.LeftAlt,
            [Key.RightCtrl] = Scancode.RightCtrl,
            [Key.RightShift] = Scancode.RightShift,
            [Key.RightAlt] = Scancode.RightAlt
        };

        for (var key = Key.A; key <= Key.Z; ++key)
            result[key] = (Scancode)((uint)Scancode.A + ((uint)key - (uint)Key.A));
        return result;
    }

    private static SdlSysWMinfo CreateInitializedWmInfo()
    {
        using var version = new SdlVersion();
        SdlNativeBindings.SdlGetVersion(version);

        return new SdlSysWMinfo
        {
            VersionMajor = version.Major,
            VersionMinor = version.Minor,
            VersionPatch = version.Patch
        };
    }

    private static void CheckSdlGl(int result, string operation)
    {
        if (result != 0)
            throw new InvalidOperationException($"{operation} failed with {result}.");
    }

    internal readonly record struct NativeWindowInfo(IntPtr Display, uint WindowId);
}
