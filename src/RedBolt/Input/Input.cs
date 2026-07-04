using RedBolt.Bindings.Sdl;

namespace RedBolt;

public sealed class Input
{
    private const uint SdlInitVideo = 0x00000020u;

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

    public bool ExitRequested { get; private set; }

    public bool IsInitialized { get; private set; }

    public MouseMode MouseMode { get; private set; } = MouseMode.Absolute;

    public IntVector2 MouseMove { get; private set; }

    public int MouseMoveWheel => mouseMoveWheel;

    public IntVector2 MousePosition { get; private set; } = IntVector2.Zero;

    public Qualifier Qualifiers { get; private set; }

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
        if (!IsInitialized || nativeEvent is null)
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
        }
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
        MouseMode = mode;
    }

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
