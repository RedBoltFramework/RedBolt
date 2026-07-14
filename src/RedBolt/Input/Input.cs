using RedBolt.Bindings.Sdl;
using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;
using System.Text;
using System.Linq;

namespace RedBolt;

/// <summary>Input state for a finger touch.</summary>
public struct TouchState
{
    /// <summary>Return last touched UI element, used by scripting integration.</summary>
    public readonly object? GetTouchedElement() => TouchedElement;

    /// <summary>Touch (finger) ID.</summary>
    public int TouchID;
    /// <summary>Position in screen coordinates.</summary>
    public Vector2i Position;
    /// <summary>Last position in screen coordinates.</summary>
    public Vector2i LastPosition;
    /// <summary>Movement since last frame.</summary>
    public Vector2i Delta;
    /// <summary>Finger pressure.</summary>
    public float Pressure;
    /// <summary>Last touched UI element from screen joystick.</summary>
    public object? TouchedElement;
}

/// <summary>Input state for a joystick.</summary>
public struct JoystickState
{
    public JoystickState()
    {
        Joystick = IntPtr.Zero;
        DeviceType = JoystickDeviceType.Unknown;
        JoystickID = 0;
        Controller = IntPtr.Zero;
        ScreenJoystick = null;
        Name = string.Empty;
        Buttons = [];
        ButtonPress = [];
        Axes = [];
        ValidAxis = [];
        Hats = [];
    }

    /// <summary>Initialize the number of buttons, axes and hats and set them to neutral state.</summary>
    public void Initialize(uint numButtons, uint numAxes, uint numHats)
    {
        Buttons = new bool[numButtons];
        ButtonPress = new bool[numButtons];
        Axes = new float[numAxes];
        ValidAxis = new bool[numAxes];
        Hats = new int[numHats];
    }

    /// <summary>Reset button, axis and hat states to neutral.</summary>
    public void Reset()
    {
        Buttons ??= [];
        ButtonPress ??= [];
        Axes ??= [];
        ValidAxis ??= [];
        Hats ??= [];
        Array.Fill(Buttons, false);
        Array.Fill(ButtonPress, false);
        Array.Fill(Axes, 0.0f);
        Array.Fill(ValidAxis, false);
        for (var i = 0; i < Hats.Length; i++)
            Hats[i] = (int)HatPosition.Center;
    }

    /// <summary>Return whether is a game controller. Game controllers will use standardized axis and button mappings.</summary>
    public readonly bool IsController() => Controller != IntPtr.Zero;

    /// <summary>Return number of buttons.</summary>
    public readonly uint GetNumButtons() => (uint)Buttons.Length;

    /// <summary>Return number of axes.</summary>
    public readonly uint GetNumAxes() => (uint)Axes.Length;

    /// <summary>Return number of hats.</summary>
    public readonly uint GetNumHats() => (uint)Hats.Length;

    /// <summary>Check if a button is held down.</summary>
    public readonly bool GetButtonDown(uint index) => index < Buttons.Length && Buttons[index];

    /// <summary>Check if a button has been pressed on this frame.</summary>
    public readonly bool GetButtonPress(uint index) => index < ButtonPress.Length && ButtonPress[index];

    /// <summary>Return axis position.</summary>
    public readonly float GetAxisPosition(uint index) => index < Axes.Length ? Axes[index] : 0.0f;

    /// <summary>Returns true if has valid axis position.</summary>
    public readonly bool HasAxisPosition(uint index) => index < ValidAxis.Length && ValidAxis[index];

    /// <summary>Return hat position.</summary>
    public readonly int GetHatPosition(uint index) => index < Hats.Length ? Hats[index] : (int)HatPosition.Center;

    /// <summary>SDL joystick.</summary>
    public IntPtr Joystick;
    /// <summary>Joystick device type.</summary>
    public JoystickDeviceType DeviceType;
    /// <summary>SDL joystick instance ID.</summary>
    public int JoystickID;
    /// <summary>SDL game controller.</summary>
    public IntPtr Controller;
    /// <summary>UI element containing the screen joystick.</summary>
    public object? ScreenJoystick;
    /// <summary>Joystick name.</summary>
    public string Name = string.Empty;
    /// <summary>Button up/down state.</summary>
    public bool[] Buttons = [];
    /// <summary>Button pressed on this frame.</summary>
    public bool[] ButtonPress = [];
    /// <summary>Axis position from -1 to 1.</summary>
    public float[] Axes = [];
    /// <summary>Valid axis position.</summary>
    public bool[] ValidAxis = [];
    /// <summary>POV hat bits.</summary>
    public int[] Hats = [];
}

public sealed class Input : Object
{
    private const uint SdlInitVideo = 0x00000020u;
    private const uint SdlInitJoystick = 0x00000200u;
    private const uint SdlInitGameController = 0x00002000u;
    private readonly HashSet<Key> _keyDown = [];
    private readonly HashSet<Key> _keyPress = [];
    private readonly HashSet<Scancode> _scancodeDown = [];
    private readonly HashSet<Scancode> _scancodePress = [];
    private readonly HashSet<ControllerButton> _controllerButtonsDown = [];
    private readonly HashSet<uint> _joystickButtonsDown = [];
    private readonly Dictionary<ControllerAxis, float> _controllerAxisPositions = new();
    private readonly Dictionary<uint, float> _joystickAxisPositions = new();
    private readonly HashSet<string> _screenButtonsDown = new(StringComparer.Ordinal);
    private readonly Dictionary<int, TouchState> _touches = [];
    private readonly Dictionary<int, JoystickState> _joysticks = [];
    private readonly Dictionary<int, SdlJoystick> _nativeJoysticks = [];
    private readonly Dictionary<int, SdlGameController> _nativeControllers = [];
    private readonly Timer _mousePressTimer = new();
    private IntRect? _explicitWindowRect;
    private SdlEvent? _nativeEvent;
    private SdlWindow? _window;
    private MouseButton _mouseButtonClick;
    private MouseButton _mouseButtonDown;
    private MouseButton _mouseButtonPress;
    private Vector2i _mousePressPosition = new(int.MinValue, int.MinValue);
    private int _mouseMoveWheel;
    private HatPosition _hatPosition = HatPosition.Center;
    private Vector2i _backbufferSize = Vector2i.Zero;
    private Vector2f _systemToBackbufferScale = Vector2f.One;
    private bool _lastMouseGrabbed;
    private bool _lastMouseVisible;
    private MouseMode _lastMouseMode = MouseMode.Absolute;
    private bool _suppressNextMouseMove;
    private bool _mouseMoveScaled;

    public bool Enabled { get; set; } = true;

    public bool ExitRequested { get; private set; }

    public bool IsInitialized { get; private set; }

    public MouseMode MouseMode { get; private set; } = MouseMode.Absolute;

    public MouseMode GetMouseMode() => MouseMode;

    public Vector2i MouseMove { get; private set; }

    public int MouseMoveWheel => _mouseMoveWheel;

    public Vector2i MousePosition { get; private set; } = Vector2i.Zero;

    public Qualifier Qualifiers { get; private set; }

    public Qualifier GetQualifiers() => Qualifiers;

    public Vector2f SystemToBackbufferScale => _systemToBackbufferScale;

    public bool MouseVisible { get; private set; }

    public bool MouseGrabbed { get; private set; }

    public bool HasFocus { get; private set; }

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

        var explicitRect = _explicitWindowRect;
        var windowWidth = explicitRect?.Width > 0 ? explicitRect.Value.Width : parameters.WindowWidth;
        var windowHeight = explicitRect?.Height > 0 ? explicitRect.Value.Height : parameters.WindowHeight;
        _backbufferSize = new Vector2i(windowWidth, windowHeight);
        _systemToBackbufferScale = Vector2f.One;
        TouchEmulation = parameters.TouchEmulation;

        if (!Context.Instance.RequireSdl(SdlInitVideo | SdlInitJoystick | SdlInitGameController))
        {
            Context.Instance.ReleaseSdl();
            throw new InvalidOperationException($"Generated SDL initialization failed: {Encoding.UTF8.GetString(SdlNativeBindings.SdlGetError()).TrimEnd('\0')}");
        }

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
                x: explicitRect?.Left ?? parameters.WindowPositionX ?? 64,
                y: explicitRect?.Top ?? parameters.WindowPositionY ?? 64,
                w: windowWidth,
                h: windowHeight,
                flags: (uint)windowFlags);

            if (_window.IsNull)
                throw new InvalidOperationException("Generated SDL_CreateWindow binding returned a null OpenGL window.");

            if (parameters.WindowMaximize)
                SdlNativeBindings.SdlMaximizeWindow(_window);

            _nativeEvent = new SdlEvent();
            ResetJoysticks();
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

    public void SetExplicitWindowRect(IntRect rect) => _explicitWindowRect = rect;

    public void ResetExplicitWindowRect() => _explicitWindowRect = null;

    public void BeginFrame()
    {
        SendEvent<InputEvents.InputBeginEvent>();

        ExitRequested = false;
        _keyPress.Clear();
        _scancodePress.Clear();
        _mouseButtonClick = MouseButton.None;
        _mouseButtonPress = MouseButton.None;
        _mouseMoveWheel = 0;
        MouseMove = Vector2i.Zero;
        foreach (var joystickId in _joysticks.Keys.ToArray())
        {
            var joystick = _joysticks[joystickId];
            Array.Fill(joystick.ButtonPress, false);
            _joysticks[joystickId] = joystick;
        }
        foreach (var touchId in _touches.Keys.ToArray())
        {
            var touch = _touches[touchId];
            touch.LastPosition = touch.Position;
            touch.Delta = Vector2i.Zero;
            _touches[touchId] = touch;
        }
    }

    public void Update()
    {
        BeginFrame();
        PollEvents();
    }

    public void PollEvents()
    {
        if (!Enabled || !IsInitialized || _nativeEvent is null)
            return;

        var dearImGui = GetSubsystem<IDearImGuiInputSink>();

        while (SdlNativeBindings.SdlPollEvent(_nativeEvent) != 0)
        {
            var eventType = (SdlEventType)_nativeEvent.Type;
            var rawEvent = new InputEvents.SDLRawInputEvent { SDLEvent = _nativeEvent.Handle };
            SendEvent(ref rawEvent);
            if (rawEvent.Consumed)
                continue;
            var eventHandledByDearImGui = dearImGui?.ProcessEvent(_nativeEvent.Handle) == true;

            if (eventHandledByDearImGui || ShouldConsumeEventByDearImGui(dearImGui, eventType))
            {
                if (eventType == SdlEventType.SdlKeydown)
                    HandleKeyDown(_nativeEvent);
                else if (eventType == SdlEventType.SdlKeyup)
                    HandleKeyUp(_nativeEvent);
                else if (eventType == SdlEventType.SdlMousebuttondown)
                    HandleMouseButtonDown(_nativeEvent);
                else if (eventType == SdlEventType.SdlMousebuttonup)
                    HandleMouseButtonUp(_nativeEvent);
                else if (eventType == SdlEventType.SdlMousemotion)
                    HandleMouseMotion(_nativeEvent);
                continue;
            }

            if (eventType == SdlEventType.SdlQuit)
            {
                ExitRequested = true;
                SendEvent<InputEvents.ExitRequestedEvent>();
            }
            else if (eventType == SdlEventType.SdlKeydown)
            {
                HandleKeyDown(_nativeEvent);
            }
            else if (eventType == SdlEventType.SdlKeyup)
            {
                HandleKeyUp(_nativeEvent);
            }
            else if (eventType == SdlEventType.SdlTextinput)
            {
                var text = new InputEvents.TextInputEvent { Text = DecodeNullTerminated(_nativeEvent.TextText) };
                SendEvent(ref text);
            }
            else if (eventType == SdlEventType.SdlTextediting)
            {
                var editing = new InputEvents.TextEditingEvent
                {
                    Composition = DecodeNullTerminated(_nativeEvent.EditText),
                    Cursor = _nativeEvent.EditStart, SelectionLength = _nativeEvent.EditLength
                };
                SendEvent(ref editing);
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
                SetMouseWheel(_nativeEvent.WheelY);
            }
            else if (eventType == SdlEventType.SdlControlleraxismotion)
            {
                var axis = (ControllerAxis)(uint)_nativeEvent.CaxisAxis;
                var position = NormalizeAxis(_nativeEvent.CaxisValue);
                _controllerAxisPositions[axis] = position;
                SetJoystickAxisState(_nativeEvent.CaxisWhich, (uint)axis, position, controller: true);
                SendJoystickAxisEvent((int)axis, position, _nativeEvent.CaxisWhich);
            }
            else if (eventType is SdlEventType.SdlControllerbuttondown or SdlEventType.SdlControllerbuttonup)
            {
                var button = (ControllerButton)(uint)_nativeEvent.CbuttonButton;
                var down = eventType == SdlEventType.SdlControllerbuttondown;
                if (down) _controllerButtonsDown.Add(button); else _controllerButtonsDown.Remove(button);
                SetJoystickButtonState(_nativeEvent.CbuttonWhich, (uint)button, down, controller: true);
                SendJoystickButtonEvent((int)button, down, _nativeEvent.CbuttonWhich);
            }
            else if (eventType == SdlEventType.SdlJoyaxismotion)
            {
                var axis = (uint)_nativeEvent.JaxisAxis;
                var position = NormalizeAxis(_nativeEvent.JaxisValue);
                _joystickAxisPositions[axis] = position;
                SetJoystickAxisState(_nativeEvent.JaxisWhich, axis, position, controller: false);
                SendJoystickAxisEvent((int)axis, position, _nativeEvent.JaxisWhich);
            }
            else if (eventType is SdlEventType.SdlJoybuttondown or SdlEventType.SdlJoybuttonup)
            {
                var button = (uint)_nativeEvent.JbuttonButton;
                var down = eventType == SdlEventType.SdlJoybuttondown;
                if (down) _joystickButtonsDown.Add(button); else _joystickButtonsDown.Remove(button);
                SetJoystickButtonState(_nativeEvent.JbuttonWhich, button, down, controller: false);
                SendJoystickButtonEvent((int)button, down, _nativeEvent.JbuttonWhich);
            }
            else if (eventType == SdlEventType.SdlJoyhatmotion)
            {
                _hatPosition = (HatPosition)(uint)_nativeEvent.JhatValue;
                SetJoystickHatState(_nativeEvent.JhatWhich, _nativeEvent.JhatHat, _hatPosition);
                var hat = new InputEvents.JoystickHatMoveEvent
                {
                    JoystickID = _nativeEvent.JhatWhich, Hat = _nativeEvent.JhatHat, Position = _hatPosition
                };
                SendEvent(ref hat);
            }
            else if (eventType is SdlEventType.SdlJoydeviceadded or SdlEventType.SdlControllerdeviceadded)
            {
                var joystickId = OpenJoystick(_nativeEvent.JdeviceWhich);
                if (joystickId >= 0)
                {
                    var connected = new InputEvents.JoystickConnectedEvent { JoystickID = joystickId };
                    SendEvent(ref connected);
                }
            }
            else if (eventType is SdlEventType.SdlJoydeviceremoved or SdlEventType.SdlControllerdeviceremoved)
            {
                var joystickId = eventType == SdlEventType.SdlControllerdeviceremoved ? _nativeEvent.CdeviceWhich : _nativeEvent.JdeviceWhich;
                if (_nativeControllers.Remove(joystickId, out var controller))
                    SdlNativeBindings.SdlGameControllerClose(controller);
                if (_nativeJoysticks.Remove(joystickId, out var joystick))
                    SdlNativeBindings.SdlJoystickClose(joystick);
                _joysticks.Remove(joystickId);
                var disconnected = new InputEvents.JoystickDisconnectedEvent { JoystickID = joystickId };
                SendEvent(ref disconnected);
            }
            else if (eventType is SdlEventType.SdlFingerdown or SdlEventType.SdlFingermotion or SdlEventType.SdlFingerup)
            {
                HandleFingerEvent(_nativeEvent, eventType);
            }
            else if (eventType == SdlEventType.SdlDropfile)
            {
                var fileNamePointer = _nativeEvent.DropFile;
                try
                {
                    var dropped = new InputEvents.DropFileEvent
                    {
                        FileName = Marshal.PtrToStringUTF8(fileNamePointer) ?? string.Empty
                    };
                    SendEvent(ref dropped);
                }
                finally
                {
                    if (fileNamePointer != IntPtr.Zero)
                        SdlNativeBindings.SdlFree(in fileNamePointer);
                }
            }
            else if (eventType == SdlEventType.SdlWindowevent && _nativeEvent.WindowEvent == (byte)SdlWindowEventID.SdlWindoweventClose)
            {
                ExitRequested = true;
                SendEvent<InputEvents.ExitRequestedEvent>();
            }
            else if (eventType == SdlEventType.SdlWindowevent)
            {
                HandleWindowEvent(_nativeEvent);
            }
        }

        if (_suppressNextMouseMove && MouseMove != Vector2i.Zero)
            UnsuppressMouseMove();

        SendEvent<InputEvents.InputEndEvent>();
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
        using var value = new CString(name);
        return (Key)SdlNativeBindings.SdlGetKeyFromName(value.Bytes);
    }

    public static Key GetKeyFromScancode(Scancode scancode) =>
        (Key)SdlNativeBindings.SdlGetKeyFromScancode((SdlScancode)scancode);

    public static string GetKeyName(Key key) => DecodeUtf8(SdlNativeBindings.SdlGetKeyName((int)key));

    public static Scancode GetScancodeFromKey(Key key) =>
        (Scancode)SdlNativeBindings.SdlGetScancodeFromKey((int)key);

    public static Scancode GetScancodeFromName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        using var value = new CString(name);
        return (Scancode)SdlNativeBindings.SdlGetScancodeFromName(value.Bytes);
    }

    public static string GetScancodeName(Scancode scancode) =>
        DecodeUtf8(SdlNativeBindings.SdlGetScancodeName((SdlScancode)scancode));

    public static string GetMouseButtonName(MouseButton button)
    {
        return button switch
        {
            MouseButton.Left => "LMB",
            MouseButton.Middle => "MMB",
            MouseButton.Right => "RMB",
            MouseButton.X1 => "X1",
            MouseButton.X2 => "X2",
            _ => "?"
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

    public bool HasJoystickAxisPosition(uint axis) => _joystickAxisPositions.ContainsKey(axis);

    public HatPosition GetHatPosition()
    {
        return _hatPosition;
    }

    public bool GetScreenButtonDown(string elementName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(elementName);
        return _screenButtonsDown.Contains(elementName);
    }

    public Vector2f GetRelativeMousePosition()
    {
        var backbufferSize = GetBackbufferSize();
        var position = GetMousePosition();
        return backbufferSize.X != 0 && backbufferSize.Y != 0
            ? new Vector2f((float)position.X / backbufferSize.X, (float)position.Y / backbufferSize.Y)
            : Vector2f.Zero;
    }

    public Vector2i GetMousePosition()
    {
        if (!IsInitialized || _window is null || _window.IsNull)
            return Vector2i.Zero;

        if (_explicitWindowRect is { } rect)
        {
            SdlNativeBindings.SdlGetGlobalMouseState(out var x, out var y);
            return SystemToBackbuffer(new Vector2i(x - rect.Left, y - rect.Top));
        }

        SdlNativeBindings.SdlGetMouseState(out var mouseX, out var mouseY);
        return SystemToBackbuffer(new Vector2i(mouseX, mouseY));
    }

    public Vector2i GetMouseMove() => _suppressNextMouseMove
        ? Vector2i.Zero
        : _mouseMoveScaled ? MouseMove : SystemToBackbuffer(MouseMove);

    public int GetMouseMoveX() => GetMouseMove().X;

    public int GetMouseMoveY() => GetMouseMove().Y;

    public int GetMouseMoveWheel() => MouseMoveWheel;

    public Vector2f GetSystemToBackbufferScale() => _systemToBackbufferScale;

    public uint GetNumTouches() => (uint)_touches.Count;

    public TouchState? GetTouch(uint index)
    {
        if (index >= _touches.Count)
            return null;

        var current = 0u;
        foreach (var touch in _touches.Values)
        {
            if (current++ == index)
                return touch;
        }

        return null;
    }

    public TouchState? GetTouchById(int touchId) => _touches.GetValueOrDefault(touchId);

    public uint GetNumJoysticks() => (uint)_joysticks.Count;

    public JoystickState? GetJoystick(int id) => _joysticks.GetValueOrDefault(id);

    public JoystickState? GetJoystickByIndex(uint index)
    {
        if (index >= _joysticks.Count)
            return null;

        var current = 0u;
        foreach (var joystick in _joysticks.Values)
        {
            if (current++ == index)
                return joystick;
        }

        return null;
    }

    public JoystickState? GetJoystickByName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        foreach (var joystick in _joysticks.Values)
        {
            if (string.Equals(joystick.Name, name, StringComparison.Ordinal))
                return joystick;
        }

        return null;
    }

    public int FindAccelerometerJoystickId()
    {
        using var hintName = new CString("SDL_ACCELEROMETER_AS_JOYSTICK");
        var hint = SdlNativeBindings.SdlGetHint(hintName.Bytes);
        if (hint.SequenceEqual("0"u8))
            return -1;

        foreach (var joystick in _joysticks.Values)
        {
            if (joystick.GetNumAxes() == 3 && joystick.GetNumButtons() == 0 && joystick.GetNumHats() == 0)
                return joystick.JoystickID;
        }

        return -1;
    }

    public bool IsScreenJoystickVisible(int id) => false;

    public bool GetScreenKeyboardSupport() => SdlNativeBindings.SdlHasScreenKeyboardSupport() == SdlBool.SdlTrue;

    public bool IsScreenKeyboardVisible() => SdlNativeBindings.SdlIsTextInputActive() == SdlBool.SdlTrue;

    public void SetScreenKeyboardVisible(bool enable)
    {
        if (enable == IsScreenKeyboardVisible())
            return;
        if (enable)
            SdlNativeBindings.SdlStartTextInput();
        else
            SdlNativeBindings.SdlStopTextInput();
    }

    public int AddScreenJoystick(XMLFile? layoutFile = null, XMLFile? styleFile = null)
    {
        // TODO: port SDL screen joystick creation and UI layout/style integration.
        return -1;
    }

    public bool RemoveScreenJoystick(int id) => false;

    public void SetScreenJoystickVisible(int id, bool enable)
    {
    }

    public bool RecordGesture()
    {
        if (SdlNativeBindings.SdlGetNumTouchDevices() == 0)
        {
            Log.Logger.LogError("Can not record gesture: no touch devices");
            return false;
        }
        return SdlNativeBindings.SdlRecordGesture(-1) != 0;
    }

    public bool SaveGestures(Serializer dest)
    {
        ArgumentNullException.ThrowIfNull(dest);
        var buffer = Marshal.AllocHGlobal(1024 * 1024);
        var rwops = SdlNativeBindings.SdlRWFromMem(buffer, 1024 * 1024);
        try
        {
            if (rwops.IsNull || SdlNativeBindings.SdlSaveAllDollarTemplates(rwops) == 0)
                return false;

            var size = (int)(rwops.HiddenMemHere - rwops.HiddenMemBase);
            var data = new byte[size];
            Marshal.Copy(buffer, data, 0, size);
            return dest.Write(data) == size;
        }
        finally
        {
            if (!rwops.IsNull)
                SdlNativeBindings.SdlRWclose(rwops);
            Marshal.FreeHGlobal(buffer);
        }
    }

    public bool SaveGesture(Serializer dest, uint gestureId)
    {
        ArgumentNullException.ThrowIfNull(dest);
        var buffer = Marshal.AllocHGlobal(1024 * 1024);
        var rwops = SdlNativeBindings.SdlRWFromMem(buffer, 1024 * 1024);
        try
        {
            if (rwops.IsNull || SdlNativeBindings.SdlSaveDollarTemplate((nint)gestureId, rwops) == 0)
                return false;

            var size = (int)(rwops.HiddenMemHere - rwops.HiddenMemBase);
            var data = new byte[size];
            Marshal.Copy(buffer, data, 0, size);
            return dest.Write(data) == size;
        }
        finally
        {
            if (!rwops.IsNull)
                SdlNativeBindings.SdlRWclose(rwops);
            Marshal.FreeHGlobal(buffer);
        }
    }

    public uint LoadGestures(Deserializer source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (SdlNativeBindings.SdlGetNumTouchDevices() == 0)
        {
            Log.Logger.LogError("Can not load gestures: no touch devices");
            return 0;
        }

        using var stream = new System.IO.MemoryStream();
        var chunk = new byte[4096];
        while (!source.Eof)
        {
            var read = source.Read(chunk);
            if (read == 0)
                break;
            stream.Write(chunk, 0, (int)read);
        }

        var data = stream.ToArray();
        var buffer = Marshal.AllocHGlobal(data.Length);
        try
        {
            Marshal.Copy(data, 0, buffer, data.Length);
            var rwops = SdlNativeBindings.SdlRWFromMem(buffer, data.Length);
            try
            {
                return rwops.IsNull ? 0u : (uint)Math.Max(0, SdlNativeBindings.SdlLoadDollarTemplates(-1, rwops));
            }
            finally
            {
                if (!rwops.IsNull)
                    SdlNativeBindings.SdlRWclose(rwops);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    public bool RemoveGesture(uint gestureId)
    {
        return SdlNativeBindings.SdlRemoveDollarTemplate((nint)gestureId) != 0;
    }

    public void RemoveAllGestures()
    {
        SdlNativeBindings.SdlRemoveAllDollarTemplates();
    }

    public Vector2i SystemToBackbuffer(Vector2i value) => new(
        (int)MathF.Round(value.X * _systemToBackbufferScale.X),
        (int)MathF.Round(value.Y * _systemToBackbufferScale.Y));

    public Vector2i BackbufferToSystem(Vector2i value) => new(
        _systemToBackbufferScale.X != 0.0f ? (int)MathF.Round(value.X / _systemToBackbufferScale.X) : value.X,
        _systemToBackbufferScale.Y != 0.0f ? (int)MathF.Round(value.Y / _systemToBackbufferScale.Y) : value.Y);

    public Vector2i GetGlobalWindowPosition()
    {
        if (!IsInitialized || _window is null || _window.IsNull)
            return Vector2i.Zero;
        SdlNativeBindings.SdlGetWindowPosition(_window, out var x, out var y);
        return new Vector2i(x, y);
    }

    public Vector2i GetGlobalWindowSize()
    {
        if (!IsInitialized || _window is null || _window.IsNull)
            return Vector2i.Zero;
        SdlNativeBindings.SdlGetWindowSize(_window, out var width, out var height);
        return new Vector2i(width, height);
    }

    public Vector2i GetBackbufferSize() => _backbufferSize;

    public void SetMousePosition(Vector2i position)
    {
        var delta = position - MousePosition;
        MousePosition = position;
        if (IsInitialized && _window is not null && !_window.IsNull)
            SdlNativeBindings.SdlWarpMouseInWindow(_window, position.X, position.Y);
        if (delta != Vector2i.Zero)
        {
            var eventData = new InputEvents.MouseMoveEvent
            {
                X = position.X, Y = position.Y, DX = delta.X, DY = delta.Y,
                Buttons = _mouseButtonDown, Qualifiers = Qualifiers
            };
            SendEvent(ref eventData);
        }
    }

    public void SetKey(Key key, bool newState)
    {
        SetKey(key, GetScancodeFromKey(key), newState);
    }

    public void SetKey(Key key, Scancode scancode, bool newState)
    {
        var wasDown = _keyDown.Contains(key);
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
        if (newState)
        {
            var eventData = new InputEvents.KeyDownEvent
            {
                Key = key, Scancode = scancode, Buttons = _mouseButtonDown,
                Qualifiers = Qualifiers, Repeat = wasDown
            };
            SendEvent(ref eventData);
        }
        else if (wasDown)
        {
            var eventData = new InputEvents.KeyUpEvent
            {
                Key = key, Scancode = scancode, Buttons = _mouseButtonDown, Qualifiers = Qualifiers
            };
            SendEvent(ref eventData);
        }
    }

    public void SetMouseButton(MouseButton button, bool newState, int clicks = 0)
    {
        if (newState)
        {
            if ((_mouseButtonDown & button) == 0)
                _mouseButtonPress |= button;
            _mouseButtonDown |= button;
            _mousePressTimer.Reset();
            _mousePressPosition = GetMousePosition();
        }
        else
        {
            if (_mousePressTimer.GetMSec() < 250 && _mousePressPosition == GetMousePosition())
                _mouseButtonClick |= button;

            if ((_mouseButtonDown & button) == 0)
                return;
            _mouseButtonDown &= ~button;
        }

        if (newState)
        {
            var eventData = new InputEvents.MouseButtonDownEvent
            {
                Button = button, Buttons = _mouseButtonDown, Qualifiers = Qualifiers, Clicks = clicks
            };
            SendEvent(ref eventData);
        }
        else
        {
            var eventData = new InputEvents.MouseButtonUpEvent
            {
                Button = button, Buttons = _mouseButtonDown, Qualifiers = Qualifiers
            };
            SendEvent(ref eventData);
        }
    }

    public void SetMouseWheel(int delta)
    {
        if (delta == 0)
            return;

        _mouseMoveWheel += delta;
        var eventData = new InputEvents.MouseWheelEvent
        {
            Wheel = delta, Buttons = _mouseButtonDown, Qualifiers = Qualifiers
        };
        SendEvent(ref eventData);
    }

    public void SetControllerButton(ControllerButton button, bool newState)
    {
        if (newState)
            _controllerButtonsDown.Add(button);
        else
            _controllerButtonsDown.Remove(button);
        SetJoystickButtonState(0, (uint)button, newState, controller: true);
        SendJoystickButtonEvent((int)button, newState);
    }

    public void SetJoystickButton(uint buttonIndex, bool newState)
    {
        if (newState)
            _joystickButtonsDown.Add(buttonIndex);
        else
            _joystickButtonsDown.Remove(buttonIndex);
        SetJoystickButtonState(0, buttonIndex, newState, controller: false);
        SendJoystickButtonEvent(checked((int)buttonIndex), newState);
    }

    public void SetControllerAxis(ControllerAxis axis, float position)
    {
        var clamped = MathDefs.Clamp(position, -1.0f, 1.0f);
        _controllerAxisPositions[axis] = clamped;
        SetJoystickAxisState(0, (uint)axis, clamped, controller: true);
        SendJoystickAxisEvent((int)axis, clamped);
    }

    public void SetJoystickAxis(uint axis, float position)
    {
        var clamped = MathDefs.Clamp(position, -1.0f, 1.0f);
        _joystickAxisPositions[axis] = clamped;
        SetJoystickAxisState(0, axis, clamped, controller: false);
        SendJoystickAxisEvent(checked((int)axis), clamped);
    }

    public void SetHatPosition(HatPosition position)
    {
        _hatPosition = position;
        SetJoystickHatState(0, 0, position);
        var eventData = new InputEvents.JoystickHatMoveEvent { JoystickID = 0, Hat = 0, Position = position };
        SendEvent(ref eventData);
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
        SetMousePosition(new Vector2i(_backbufferSize.X / 2, _backbufferSize.Y / 2));
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
        var previousMode = MouseMode;
        var changed = previousMode != mode;
        if (!changed)
        {
            if (!suppressEvent)
                _lastMouseMode = mode;
            return;
        }

        if (IsInitialized)
        {
            SuppressNextMouseMove();
            MouseMode = mode;

            if (previousMode == MouseMode.Absolute && !MouseVisible)
                SdlNativeBindings.SdlSetWindowGrab(Window, SdlBool.SdlFalse);
            else if (previousMode == MouseMode.Relative)
            {
                var result = SdlNativeBindings.SdlSetRelativeMouseMode(SdlBool.SdlFalse);
                if (result == -1)
                    SdlNativeBindings.SdlSetWindowGrab(Window, SdlBool.SdlFalse);
                SetMouseVisible(_lastMouseVisible, suppressEvent: true);
            }
            else if (previousMode == MouseMode.Wrap)
                SdlNativeBindings.SdlSetWindowGrab(Window, SdlBool.SdlFalse);
        }

        if (IsInitialized)
        {
            if (mode == MouseMode.Relative)
            {
                SetMouseVisible(false, suppressEvent: true);
                var result = SdlNativeBindings.SdlSetRelativeMouseMode(SdlBool.SdlTrue);
                if (result == -1)
                    SdlNativeBindings.SdlSetWindowGrab(Window, SdlBool.SdlTrue);
                SetMouseGrabbed(true, suppressEvent: true);
            }
            else
            {
                if (mode == MouseMode.Wrap)
                {
                    SetMouseGrabbed(true, suppressEvent: true);
                    SdlNativeBindings.SdlSetWindowGrab(Window, SdlBool.SdlTrue);
                }
                else
                    SetMouseGrabbed(!MouseVisible, suppressEvent: true);
            }
        }
        else
        {
            MouseMode = mode;
        }

        if (!suppressEvent)
            _lastMouseMode = mode;
        if (!suppressEvent)
        {
            var eventData = new InputEvents.MouseModeChangedEvent { Mode = mode, MouseLocked = IsMouseLocked() };
            SendEvent(ref eventData);
        }
    }

    public void ResetMouseMode() => SetMouseMode(_lastMouseMode);

    public bool GetEnabled() => Enabled;

    public void SetEnabled(bool enabled) => Enabled = enabled;

    public void SetMouseVisible(bool enable, bool suppressEvent = false)
    {
        var startMouseVisible = MouseVisible;
        if (TouchEmulation)
            enable = true;
        if (MouseMode == MouseMode.Relative)
        {
            if (!suppressEvent)
                _lastMouseVisible = enable;
            enable = false;
        }

        if (MouseVisible != enable)
        {
            if (IsInitialized)
            {
                if (!enable && HasFocus)
                {
                    if (MouseMode == MouseMode.Absolute)
                        SdlNativeBindings.SdlSetWindowGrab(Window, SdlBool.SdlTrue);
                    SdlNativeBindings.SdlShowCursor(0);
                    MouseVisible = false;
                }
                else if (MouseMode != MouseMode.Relative)
                {
                    SetMouseGrabbed(false, suppressEvent);
                    if (MouseMode == MouseMode.Absolute)
                        SdlNativeBindings.SdlSetWindowGrab(Window, SdlBool.SdlFalse);
                    SdlNativeBindings.SdlShowCursor(1);
                    MouseVisible = true;
                }
            }
            else
                MouseVisible = enable;

            if (MouseVisible != startMouseVisible)
            {
                SuppressNextMouseMove();
                if (!suppressEvent)
                {
                    _lastMouseVisible = MouseVisible;
                    var eventData = new InputEvents.MouseVisibleChangedEvent { Visible = MouseVisible };
                    SendEvent(ref eventData);
                }
            }
        }
    }

    public void ResetMouseVisible() => SetMouseVisible(_lastMouseVisible);

    public void SetMouseGrabbed(bool grab, bool suppressEvent = false)
    {
        MouseGrabbed = grab;
        if (!suppressEvent)
            _lastMouseGrabbed = grab;
    }

    public void ResetMouseGrabbed() => SetMouseGrabbed(_lastMouseGrabbed, suppressEvent: true);

    public void SetToggleFullscreen(bool enable) => ToggleFullscreen = enable;

    public bool GetToggleFullscreen() => ToggleFullscreen;

    public void SetTouchEmulation(bool enable)
    {
        if (TouchEmulation == enable)
            return;
        if (enable)
            SetMouseVisible(true);
        else
            _touches.Clear();
        TouchEmulation = enable;
    }

    public bool GetTouchEmulation() => TouchEmulation;

    public bool IsMouseVisible() => MouseVisible;

    public bool IsMouseGrabbed() => MouseGrabbed;

    public bool IsMouseLocked() => !((MouseMode == MouseMode.Absolute && MouseVisible) || MouseMode == MouseMode.Free);

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
        var wasInitialized = IsInitialized;
        _nativeEvent?.Dispose();
        _nativeEvent = null;

        if (_window is not null && !_window.IsNull)
            SdlNativeBindings.SdlDestroyWindow(_window);
        _window = null;

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
        _touches.Clear();
        foreach (var controller in _nativeControllers.Values)
            SdlNativeBindings.SdlGameControllerClose(controller);
        foreach (var joystick in _nativeJoysticks.Values)
            SdlNativeBindings.SdlJoystickClose(joystick);
        _nativeControllers.Clear();
        _nativeJoysticks.Clear();
        _joysticks.Clear();
        if (wasInitialized)
            Context.Instance.ReleaseSdl();
        _mouseButtonClick = MouseButton.None;
        _mouseButtonDown = MouseButton.None;
        _mouseButtonPress = MouseButton.None;
        _mouseMoveWheel = 0;
        _hatPosition = HatPosition.Center;
        MouseMove = Vector2i.Zero;
        _suppressNextMouseMove = false;
        _mouseMoveScaled = false;
        MousePosition = Vector2i.Zero;
        Qualifiers = Qualifier.None;
        _backbufferSize = Vector2i.Zero;
        _systemToBackbufferScale = Vector2f.One;
        HasFocus = false;
        IsMinimized = false;
    }

    private void HandleKeyDown(SdlEvent @event)
    {
        var scancode = (Scancode)(uint)@event.KeyKeysymScancode;
        var key = ConvertSdlKeyCode((Key)@event.KeyKeysymSym, scancode);
        SetKey(key, scancode, true);
        if (scancode == Scancode.Escape)
        {
            ExitRequested = true;
            SendEvent<InputEvents.ExitRequestedEvent>();
        }
    }

    private void HandleKeyUp(SdlEvent @event)
    {
        var scancode = (Scancode)(uint)@event.KeyKeysymScancode;
        var key = ConvertSdlKeyCode((Key)@event.KeyKeysymSym, scancode);
        SetKey(key, scancode, false);
    }

    private static Key ConvertSdlKeyCode(Key key, Scancode scancode)
    {
        if (scancode == Scancode.AcBack)
            return Key.Escape;

        var value = (uint)key;
        if (value is >= 'A' and <= 'Z')
            value += 'a' - 'A';
        return (Key)value;
    }

    private void HandleMouseButtonDown(SdlEvent @event)
    {
        var button = ToMouseButton(@event.ButtonButton);
        SetMouseButton(button, true, @event.ButtonClicks);
    }

    private void HandleMouseButtonUp(SdlEvent @event)
    {
        var button = ToMouseButton(@event.ButtonButton);
        SetMouseButton(button, false, @event.ButtonClicks);
    }

    private void HandleMouseMotion(SdlEvent @event)
    {
        MousePosition = new Vector2i(@event.MotionX, @event.MotionY);
        MouseMove = new Vector2i(MouseMove.X + @event.MotionXrel, MouseMove.Y + @event.MotionYrel);
        _mouseMoveScaled = false;
        var eventData = new InputEvents.MouseMoveEvent
        {
            X = MousePosition.X, Y = MousePosition.Y, DX = @event.MotionXrel, DY = @event.MotionYrel,
            Buttons = _mouseButtonDown, Qualifiers = Qualifiers
        };
        SendEvent(ref eventData);
    }

    private void HandleWindowEvent(SdlEvent @event)
    {
        var windowEvent = (SdlWindowEventID)@event.WindowEvent;
        if (windowEvent == SdlWindowEventID.SdlWindoweventFocusGained)
        {
            GainFocus();
            return;
        }
        else if (windowEvent == SdlWindowEventID.SdlWindoweventFocusLost)
        {
            LoseFocus();
            return;
        }
        else if (windowEvent == SdlWindowEventID.SdlWindoweventMinimized)
            IsMinimized = true;
        else if (windowEvent == SdlWindowEventID.SdlWindoweventRestored)
            IsMinimized = false;

        var eventData = new InputEvents.InputFocusEvent { Focus = HasFocus, Minimized = IsMinimized };
        SendEvent(ref eventData);
    }

    private void GainFocus()
    {
        ResetState();
        HasFocus = true;
        if (IsInitialized)
        {
            var mode = MouseMode;
            MouseMode = MouseMode.Free;
            SetMouseMode(mode, suppressEvent: true);
        }
        SuppressNextMouseMove();
        if (!MouseVisible && IsInitialized)
            SdlNativeBindings.SdlShowCursor(0);
        SendInputFocusEvent();
    }

    private void LoseFocus()
    {
        ResetState();
        HasFocus = false;
        if (IsInitialized)
        {
            var mode = MouseMode;
            SetMouseMode(MouseMode.Free, suppressEvent: true);
            MouseMode = mode;
            SdlNativeBindings.SdlShowCursor(1);
        }
        SendInputFocusEvent();
    }

    private void ResetState()
    {
        _keyDown.Clear();
        _keyPress.Clear();
        _scancodeDown.Clear();
        _scancodePress.Clear();
        _controllerButtonsDown.Clear();
        _joystickButtonsDown.Clear();
        _controllerAxisPositions.Clear();
        _joystickAxisPositions.Clear();

        foreach (var joystickId in _joysticks.Keys.ToArray())
        {
            var state = _joysticks[joystickId];
            state.Reset();
            _joysticks[joystickId] = state;
        }

        foreach (var touch in _touches.Values)
        {
            var eventData = new InputEvents.TouchEndEvent
            {
                TouchID = touch.TouchID,
                X = touch.Position.X,
                Y = touch.Position.Y
            };
            SendEvent(ref eventData);
        }
        _touches.Clear();
        _mouseButtonDown = MouseButton.None;
        _mouseButtonPress = MouseButton.None;
        _mouseButtonClick = MouseButton.None;
        Qualifiers = Qualifier.None;
        _hatPosition = HatPosition.Center;
        MouseMove = Vector2i.Zero;
        _mouseMoveWheel = 0;
    }

    private void SendInputFocusEvent()
    {
        var eventData = new InputEvents.InputFocusEvent { Focus = HasFocus, Minimized = IsMinimized };
        SendEvent(ref eventData);
    }

    private void SendJoystickButtonEvent(int button, bool down, int joystickId = 0)
    {
        if (down)
        {
            var eventData = new InputEvents.JoystickButtonDownEvent { JoystickID = joystickId, Button = button };
            SendEvent(ref eventData);
        }
        else
        {
            var eventData = new InputEvents.JoystickButtonUpEvent { JoystickID = joystickId, Button = button };
            SendEvent(ref eventData);
        }
    }

    private void SendJoystickAxisEvent(int axis, float position, int joystickId = 0)
    {
        var eventData = new InputEvents.JoystickAxisMoveEvent { JoystickID = joystickId, Axis = axis, Position = position };
        SendEvent(ref eventData);
    }

    private void HandleFingerEvent(SdlEvent @event, SdlEventType eventType)
    {
        var touchId = @event.TfingerFingerId.GetHashCode();
        var x = (int)MathF.Round(@event.TfingerX * _backbufferSize.X);
        var y = (int)MathF.Round(@event.TfingerY * _backbufferSize.Y);
        if (eventType == SdlEventType.SdlFingerdown)
        {
            _touches[touchId] = new TouchState
            {
                TouchID = touchId,
                Position = new Vector2i(x, y),
                LastPosition = new Vector2i(x, y),
                Delta = Vector2i.Zero,
                Pressure = @event.TfingerPressure
            };
            var value = new InputEvents.TouchBeginEvent { TouchID = touchId, X = x, Y = y, Pressure = @event.TfingerPressure };
            SendEvent(ref value);
        }
        else if (eventType == SdlEventType.SdlFingermotion)
        {
            var touch = _touches.GetValueOrDefault(touchId);
            touch.TouchID = touchId;
            touch.LastPosition = touch.Position;
            touch.Position = new Vector2i(x, y);
            touch.Delta = new Vector2i(
                (int)MathF.Round(@event.TfingerDx * _backbufferSize.X),
                (int)MathF.Round(@event.TfingerDy * _backbufferSize.Y));
            touch.Pressure = @event.TfingerPressure;
            _touches[touchId] = touch;
            var value = new InputEvents.TouchMoveEvent
            {
                TouchID = touchId, X = x, Y = y,
                DX = (int)MathF.Round(@event.TfingerDx * _backbufferSize.X),
                DY = (int)MathF.Round(@event.TfingerDy * _backbufferSize.Y), Pressure = @event.TfingerPressure
            };
            SendEvent(ref value);
        }
        else
        {
            var value = new InputEvents.TouchEndEvent { TouchID = touchId, X = x, Y = y };
            SendEvent(ref value);
            _touches.Remove(touchId);
        }
    }

    private void ResetJoysticks()
    {
        foreach (var controller in _nativeControllers.Values)
            SdlNativeBindings.SdlGameControllerClose(controller);
        foreach (var joystick in _nativeJoysticks.Values)
            SdlNativeBindings.SdlJoystickClose(joystick);
        _nativeControllers.Clear();
        _nativeJoysticks.Clear();
        _joysticks.Clear();

        var count = SdlNativeBindings.SdlNumJoysticks();
        for (var i = 0; i < count; i++)
            OpenJoystick(i);
    }

    private int OpenJoystick(int index)
    {
        var joystick = SdlNativeBindings.SdlJoystickOpen(index);
        if (joystick.IsNull)
            return -1;

        var joystickId = SdlNativeBindings.SdlJoystickInstanceID(joystick);
        var state = new JoystickState
        {
            Joystick = joystick.Handle,
            DeviceType = (JoystickDeviceType)SdlNativeBindings.SdlJoystickGetDeviceType(index),
            JoystickID = joystickId,
            Name = DecodeUtf8(SdlNativeBindings.SdlJoystickName(joystick))
        };

        var controller = SdlNativeBindings.SdlIsGameController(index) == SdlBool.SdlTrue
            ? SdlNativeBindings.SdlGameControllerOpen(index)
            : null;
        if (controller is { IsNull: false })
        {
            state.Controller = controller.Handle;
            _nativeControllers[joystickId] = controller;
        }

        var numButtons = SdlNativeBindings.SdlJoystickNumButtons(joystick);
        var numAxes = SdlNativeBindings.SdlJoystickNumAxes(joystick);
        var numHats = SdlNativeBindings.SdlJoystickNumHats(joystick);
        if (controller is { IsNull: false })
        {
            numButtons = Math.Max(numButtons, 15);
            numAxes = Math.Max(numAxes, 6);
        }

        state.Initialize((uint)numButtons, (uint)numAxes, (uint)numHats);
        state.JoystickID = joystickId;
        _nativeJoysticks[joystickId] = joystick;
        _joysticks[joystickId] = state;
        return joystickId;
    }

    private JoystickState EnsureJoystickState(int joystickId, bool controller)
    {
        if (_joysticks.TryGetValue(joystickId, out var existing))
            return existing;

        var state = new JoystickState();
        state.Initialize(controller ? 15u : 0u, controller ? 6u : 0u, controller ? 1u : 0u);
        state.JoystickID = joystickId;
        state.Controller = controller ? new IntPtr(1) : IntPtr.Zero;
        state.DeviceType = controller ? JoystickDeviceType.GameController : JoystickDeviceType.Unknown;
        _joysticks[joystickId] = state;
        return state;
    }

    private void SetJoystickButtonState(int joystickId, uint button, bool down, bool controller)
    {
        var state = EnsureJoystickState(joystickId, controller);
        var index = checked((int)button);
        if (index >= state.Buttons.Length)
        {
            Array.Resize(ref state.Buttons, index + 1);
            Array.Resize(ref state.ButtonPress, index + 1);
        }

        if (down && !state.Buttons[index])
            state.ButtonPress[index] = true;
        state.Buttons[index] = down;
        _joysticks[joystickId] = state;
    }

    private void SetJoystickAxisState(int joystickId, uint axis, float position, bool controller)
    {
        var state = EnsureJoystickState(joystickId, controller);
        var index = checked((int)axis);
        if (index >= state.Axes.Length)
        {
            Array.Resize(ref state.Axes, index + 1);
            Array.Resize(ref state.ValidAxis, index + 1);
        }

        state.Axes[index] = position;
        state.ValidAxis[index] = true;
        _joysticks[joystickId] = state;
    }

    private void SetJoystickHatState(int joystickId, byte hat, HatPosition position)
    {
        var state = EnsureJoystickState(joystickId, controller: false);
        var index = hat;
        if (index >= state.Hats.Length)
            Array.Resize(ref state.Hats, index + 1);
        state.Hats[index] = (int)position;
        _joysticks[joystickId] = state;
    }

    private static float NormalizeAxis(short value) => value >= 0 ? value / 32767.0f : value / 32768.0f;

    private static string DecodeUtf8(ReadOnlySpan<byte> source)
    {
        var length = source.IndexOf((byte)0);
        return Encoding.UTF8.GetString(length < 0 ? source : source[..length]);
    }

    private static string DecodeNullTerminated(IEnumerable<sbyte> source)
    {
        var bytes = source.TakeWhile(static value => value != 0).Select(static value => unchecked((byte)value)).ToArray();
        return Encoding.UTF8.GetString(bytes);
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

    private void SuppressNextMouseMove()
    {
        _suppressNextMouseMove = true;
        MouseMove = Vector2i.Zero;
    }

    private void UnsuppressMouseMove()
    {
        _suppressNextMouseMove = false;
        MouseMove = Vector2i.Zero;
        MousePosition = GetMousePosition();
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
