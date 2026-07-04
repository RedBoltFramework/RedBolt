using RedBolt.Bindings.Sdl;

namespace RedBolt;

public sealed class Input
{
    private const uint SdlInitVideo = 0x00000020u;
    private static readonly Dictionary<Key, Scancode> KeyToScancode = BuildKeyToScancodeMap();
    private static readonly Dictionary<Scancode, Key> ScancodeToKey = KeyToScancode.ToDictionary(static item => item.Value, static item => item.Key);

    private readonly HashSet<Key> keyDown = [];
    private readonly HashSet<Key> keyPress = [];
    private readonly HashSet<Scancode> scancodeDown = [];
    private readonly HashSet<Scancode> scancodePress = [];
    private SdlEvent? nativeEvent;
    private SdlWindow? window;
    private MouseButton mouseButtonClick;
    private MouseButton mouseButtonDown;
    private MouseButton mouseButtonPress;
    private int mouseMoveWheel;
    private IntVector2 backbufferSize = IntVector2.Zero;
    private Vector2 systemToBackbufferScale = Vector2.One;
    private bool lastMouseGrabbed;
    private bool lastMouseVisible = true;
    private MouseMode lastMouseMode = MouseMode.Absolute;

    public bool Enabled { get; set; } = true;

    public bool ExitRequested { get; private set; }

    public bool IsInitialized { get; private set; }

    public MouseMode MouseMode { get; private set; } = MouseMode.Absolute;

    public IntVector2 MouseMove { get; private set; }

    public int MouseMoveWheel => mouseMoveWheel;

    public IntVector2 MousePosition { get; private set; } = IntVector2.Zero;

    public Qualifier Qualifiers { get; private set; }

    public Vector2 SystemToBackbufferScale => systemToBackbufferScale;

    public bool MouseVisible { get; private set; } = true;

    public bool MouseGrabbed { get; private set; }

    public bool HasFocus { get; private set; } = true;

    public bool IsMinimized { get; private set; }

    public bool ToggleFullscreen { get; private set; } = true;

    public bool TouchEmulation { get; private set; }

    internal SdlWindow Window => window is not null && !window.IsNull
        ? window
        : throw new InvalidOperationException("Input window is not initialized.");

    public void Initialize(EngineParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        if (IsInitialized)
            throw new InvalidOperationException("Input system is already initialized.");

        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DISPLAY")))
            throw new InvalidOperationException("RedBolt OpenGL first-cube path requires an X11 DISPLAY.");

        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SDL_VIDEODRIVER")))
            Environment.SetEnvironmentVariable("SDL_VIDEODRIVER", "x11");

        backbufferSize = new IntVector2(parameters.WindowWidth, parameters.WindowHeight);
        systemToBackbufferScale = Vector2.One;

        var initResult = SdlNativeBindings.SdlInit(SdlInitVideo);
        if (initResult != 0)
            throw new InvalidOperationException($"Generated SDL_Init binding failed with {initResult}.");

        try
        {
            CheckSdlGl(SdlNativeBindings.SdlGlSetAttribute(SdlGLattr.SdlGlContextMajorVersion, 3), "SDL_GL_SetAttribute major version");
            CheckSdlGl(SdlNativeBindings.SdlGlSetAttribute(SdlGLattr.SdlGlContextMinorVersion, 3), "SDL_GL_SetAttribute minor version");
            CheckSdlGl(SdlNativeBindings.SdlGlSetAttribute(SdlGLattr.SdlGlContextProfileMask, (int)SdlGLprofile.SdlGlContextProfileCore), "SDL_GL_SetAttribute profile mask");
            CheckSdlGl(SdlNativeBindings.SdlGlSetAttribute(SdlGLattr.SdlGlDoublebuffer, 1), "SDL_GL_SetAttribute doublebuffer");

            using var title = new PinnedUtf8(parameters.WindowTitle);
            window = SdlNativeBindings.SdlCreateWindow(
                in title.FirstByte,
                x: 64,
                y: 64,
                w: parameters.WindowWidth,
                h: parameters.WindowHeight,
                flags: (uint)(SdlWindowFlags.SdlWindowOpengl | SdlWindowFlags.SdlWindowShown));

            if (window.IsNull)
                throw new InvalidOperationException("Generated SDL_CreateWindow binding returned a null OpenGL window.");

            nativeEvent = new SdlEvent();
            IsInitialized = true;
        }
        catch
        {
            Shutdown();
            throw;
        }
    }

    public void BeginFrame()
    {
        ExitRequested = false;
        keyPress.Clear();
        scancodePress.Clear();
        mouseButtonClick = MouseButton.None;
        mouseButtonPress = MouseButton.None;
        mouseMoveWheel = 0;
        MouseMove = IntVector2.Zero;
    }

    public void PollEvents()
    {
        if (!Enabled || !IsInitialized || nativeEvent is null)
            return;

        while (SdlNativeBindings.SdlPollEvent(nativeEvent) != 0)
        {
            var eventType = (SdlEventType)nativeEvent.Type;
            if (eventType == SdlEventType.SdlQuit)
            {
                ExitRequested = true;
            }
            else if (eventType == SdlEventType.SdlKeydown)
            {
                HandleKeyDown(nativeEvent);
            }
            else if (eventType == SdlEventType.SdlKeyup)
            {
                HandleKeyUp(nativeEvent);
            }
            else if (eventType == SdlEventType.SdlMousebuttondown)
            {
                HandleMouseButtonDown(nativeEvent);
            }
            else if (eventType == SdlEventType.SdlMousebuttonup)
            {
                HandleMouseButtonUp(nativeEvent);
            }
            else if (eventType == SdlEventType.SdlMousemotion)
            {
                HandleMouseMotion(nativeEvent);
            }
            else if (eventType == SdlEventType.SdlMousewheel)
            {
                mouseMoveWheel += nativeEvent.WheelY;
            }
            else if (eventType == SdlEventType.SdlWindowevent && nativeEvent.WindowEvent == (byte)SdlWindowEventID.SdlWindoweventClose)
            {
                ExitRequested = true;
            }
            else if (eventType == SdlEventType.SdlWindowevent)
            {
                HandleWindowEvent(nativeEvent);
            }
        }
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
        return keyDown.Contains(key);
    }

    public bool GetKeyPress(Key key)
    {
        return keyPress.Contains(key);
    }

    public bool GetScancodeDown(Scancode scancode)
    {
        return scancodeDown.Contains(scancode);
    }

    public bool GetScancodePress(Scancode scancode)
    {
        return scancodePress.Contains(scancode);
    }

    public bool GetMouseButtonDown(MouseButton button)
    {
        return (mouseButtonDown & button) != 0;
    }

    public bool GetMouseButtonPress(MouseButton button)
    {
        return (mouseButtonPress & button) != 0;
    }

    public bool GetMouseButtonClick(MouseButton button)
    {
        return (mouseButtonClick & button) != 0;
    }

    public Vector2 GetRelativeMousePosition()
    {
        return backbufferSize.X > 0 && backbufferSize.Y > 0
            ? new Vector2((float)MousePosition.X / backbufferSize.X, (float)MousePosition.Y / backbufferSize.Y)
            : Vector2.Zero;
    }

    public IntVector2 GetMousePosition() => MousePosition;

    public IntVector2 GetMouseMove() => MouseMove;

    public int GetMouseMoveX() => MouseMove.X;

    public int GetMouseMoveY() => MouseMove.Y;

    public int GetMouseMoveWheel() => MouseMoveWheel;

    public Vector2 GetSystemToBackbufferScale() => systemToBackbufferScale;

    public IntVector2 SystemToBackbuffer(IntVector2 value) => new(
        (int)MathF.Round(value.X * systemToBackbufferScale.X),
        (int)MathF.Round(value.Y * systemToBackbufferScale.Y));

    public IntVector2 BackbufferToSystem(IntVector2 value) => new(
        systemToBackbufferScale.X != 0.0f ? (int)MathF.Round(value.X / systemToBackbufferScale.X) : value.X,
        systemToBackbufferScale.Y != 0.0f ? (int)MathF.Round(value.Y / systemToBackbufferScale.Y) : value.Y);

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
            if (keyDown.Add(key))
                keyPress.Add(key);
            if (scancode != Scancode.Unknown && scancodeDown.Add(scancode))
                scancodePress.Add(scancode);
        }
        else
        {
            keyDown.Remove(key);
            scancodeDown.Remove(scancode);
        }

        UpdateQualifiers(key, newState);
    }

    public void SetMouseButton(MouseButton button, bool newState, int clicks = 0)
    {
        if (newState)
        {
            if ((mouseButtonDown & button) == 0)
                mouseButtonPress |= button;
            mouseButtonDown |= button;
        }
        else
        {
            mouseButtonDown &= ~button;
        }

        if (clicks > 0)
            mouseButtonClick |= button;
    }

    public void SetMouseWheel(int delta)
    {
        mouseMoveWheel += delta;
    }

    public void CenterMousePosition()
    {
        MousePosition = new IntVector2(backbufferSize.X / 2, backbufferSize.Y / 2);
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
            lastMouseMode = mode;
    }

    public void ResetMouseMode() => SetMouseMode(lastMouseMode);

    public bool GetEnabled() => Enabled;

    public void SetEnabled(bool enabled) => Enabled = enabled;

    public void SetMouseVisible(bool enable, bool suppressEvent = false)
    {
        MouseVisible = enable;
        if (!suppressEvent)
            lastMouseVisible = enable;
    }

    public void ResetMouseVisible() => SetMouseVisible(lastMouseVisible);

    public void SetMouseGrabbed(bool grab, bool suppressEvent = false)
    {
        MouseGrabbed = grab;
        if (!suppressEvent)
            lastMouseGrabbed = grab;
    }

    public void ResetMouseGrabbed() => SetMouseGrabbed(lastMouseGrabbed);

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
        nativeEvent?.Dispose();
        nativeEvent = null;

        if (window is not null && !window.IsNull)
            SdlNativeBindings.SdlDestroyWindow(window);
        window = null;

        if (IsInitialized)
            SdlNativeBindings.SdlQuit();

        IsInitialized = false;
        ExitRequested = false;
        keyDown.Clear();
        keyPress.Clear();
        scancodeDown.Clear();
        scancodePress.Clear();
        mouseButtonClick = MouseButton.None;
        mouseButtonDown = MouseButton.None;
        mouseButtonPress = MouseButton.None;
        mouseMoveWheel = 0;
        MouseMove = IntVector2.Zero;
        MousePosition = IntVector2.Zero;
        Qualifiers = Qualifier.None;
        backbufferSize = IntVector2.Zero;
        systemToBackbufferScale = Vector2.One;
        HasFocus = true;
        IsMinimized = false;
    }

    private void HandleKeyDown(SdlEvent @event)
    {
        var key = (Key)@event.KeyKeysymSym;
        var scancode = (Scancode)(uint)@event.KeyKeysymScancode;
        if (keyDown.Add(key))
            keyPress.Add(key);
        if (scancodeDown.Add(scancode))
            scancodePress.Add(scancode);

        UpdateQualifiers(key, isDown: true);
        if (scancode == Scancode.Escape)
            ExitRequested = true;
    }

    private void HandleKeyUp(SdlEvent @event)
    {
        var key = (Key)@event.KeyKeysymSym;
        var scancode = (Scancode)(uint)@event.KeyKeysymScancode;
        keyDown.Remove(key);
        scancodeDown.Remove(scancode);
        UpdateQualifiers(key, isDown: false);
    }

    private void HandleMouseButtonDown(SdlEvent @event)
    {
        var button = ToMouseButton(@event.ButtonButton);
        mouseButtonDown |= button;
        mouseButtonPress |= button;
    }

    private void HandleMouseButtonUp(SdlEvent @event)
    {
        var button = ToMouseButton(@event.ButtonButton);
        mouseButtonDown &= ~button;
        mouseButtonClick |= button;
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
