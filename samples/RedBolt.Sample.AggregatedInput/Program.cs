using System;
using System.Buffers;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Text;
using RedBolt;
using RedBolt.Bindings.DearImGui;

try { return new AggregatedInputApplication().Run(); }
catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }

/// <summary>
/// Aggregated input example to compare raw input events to aggregated events.
/// Demonstrates how
/// - DirectionAggregator evaluates aggregated direction from all input devices.
/// - DirectionalPadAdapter translates input events into simple directional events similar to d-pad.
/// </summary>
sealed class AggregatedInputApplication : Application
{
    private DirectionAggregator? _aggregatedInput;
    private DirectionalPadAdapter? _dpadInput;

    private ContextEventSubscription? _dearImGuiSubscription;
    private ContextEventSubscription? _dpadKeyUpSubscription;
    private ContextEventSubscription? _dpadKeyDownSubscription;
    private ContextEventSubscription? _rawKeyUpSubscription;
    private ContextEventSubscription? _rawKeyDownSubscription;
    private ContextEventSubscription? _joystickAxisMoveSubscription;
    private ContextEventSubscription? _joystickHatMoveSubscription;
    private ContextEventSubscription? _joystickDisconnectedSubscription;
    private ContextEventSubscription? _touchBeginSubscription;
    private ContextEventSubscription? _touchMoveSubscription;
    private ContextEventSubscription? _touchEndSubscription;

    private readonly byte[] _directionText = new byte[64];
    private int _directionTextLength;
    private readonly List<byte[]> _rawEvents = [];
    private readonly List<byte[]> _filteredEvents = [];

    /// <summary>Construct.</summary>
    public AggregatedInputApplication()
    {
    }

    protected override void Setup(EngineParameters parameters)
    {
        parameters.ApplicationName = "RedBolt.Sample.AggregatedInput";
        parameters.Borderless = false;
        parameters.FullScreen = false;
        parameters.ResourcePaths = "CoreData;Data";
        parameters.ResourcePrefixPaths = ".;..";
        parameters.WindowMaximize = false;
        parameters.WindowWidth = 1280;
        parameters.WindowHeight = 720;
        parameters.WindowTitle = "RedBolt Aggregated Input";
    }

    /// <summary>Setup after engine initialization and before running the main loop.</summary>
    protected override void Start()
    {
        _aggregatedInput = new DirectionAggregator();
        _dpadInput = new DirectionalPadAdapter();

        // Create UI elements
        CreateUI();

        // Finally subscribe to events.
        SubscribeToEvents();

        // Set the mouse mode to use in the sample
        Input.SetMouseMode(MouseMode.Free);
        Input.SetMouseVisible(true);
    }

    /// <summary>Construct a new Text instance, containing the 'Hello World' String, and add it to the UI root element.</summary>
    private void CreateUI()
    {
        Context.RegisterSubsystem(new DearImGui((ImGuiConfigFlags)Engine.Parameters.DearImGuiFlags));
        _dearImGuiSubscription = Context.SubscribeToEvent<DearImGuiEvents.EndRenderingDearImGuiEvent>(
            (ref DearImGuiEvents.EndRenderingDearImGuiEvent _) => Draw());
    }

    /// <summary>Subscribe to application-wide logic update events.</summary>
    private void SubscribeToEvents()
    {
        _aggregatedInput!.SetEnabled(true);
        _dpadInput!.SetEnabled(true);
        _dpadKeyUpSubscription = Context.SubscribeToEvent<InputEvents.KeyUpEvent>(_dpadInput, HandleDPadKeyUp);
        _dpadKeyDownSubscription = Context.SubscribeToEvent<InputEvents.KeyDownEvent>(_dpadInput, HandleDPadKeyDown);
        _rawKeyUpSubscription = Context.SubscribeToEvent<InputEvents.KeyUpEvent>(Input, HandleKeyUp);
        _rawKeyDownSubscription = Context.SubscribeToEvent<InputEvents.KeyDownEvent>(Input, HandleKeyDown);
        _joystickAxisMoveSubscription =
            Context.SubscribeToEvent<InputEvents.JoystickAxisMoveEvent>(Input, HandleJoystickAxisMove);
        _joystickHatMoveSubscription =
            Context.SubscribeToEvent<InputEvents.JoystickHatMoveEvent>(Input, HandleJoystickHatMove);
        _joystickDisconnectedSubscription =
            Context.SubscribeToEvent<InputEvents.JoystickDisconnectedEvent>(Input, HandleJoystickDisconnected);
        _touchBeginSubscription = Context.SubscribeToEvent<InputEvents.TouchBeginEvent>(Input, HandleTouchBegin);
        _touchMoveSubscription = Context.SubscribeToEvent<InputEvents.TouchMoveEvent>(Input, HandleTouchMove);
        _touchEndSubscription = Context.SubscribeToEvent<InputEvents.TouchEndEvent>(Input, HandleTouchEnd);
    }

    private void Deactivate()
    {
        _aggregatedInput!.SetEnabled(false);
        _dpadInput!.SetEnabled(false);
        _touchEndSubscription?.Dispose();
        _touchMoveSubscription?.Dispose();
        _touchBeginSubscription?.Dispose();
        _joystickDisconnectedSubscription?.Dispose();
        _joystickHatMoveSubscription?.Dispose();
        _joystickAxisMoveSubscription?.Dispose();
        _rawKeyDownSubscription?.Dispose();
        _rawKeyUpSubscription?.Dispose();
        _dpadKeyDownSubscription?.Dispose();
        _dpadKeyUpSubscription?.Dispose();
        _dearImGuiSubscription?.Dispose();
    }

    private void AddFilteredEvent(string str)
    {
        _filteredEvents.Add(Encoding.UTF8.GetBytes(str + "\0"));
        if (_filteredEvents.Count > 32)
            _filteredEvents.RemoveAt(0);
    }

    private void AddRawEvent(string str)
    {
        _rawEvents.Add(Encoding.UTF8.GetBytes(str + "\0"));
        if (_rawEvents.Count > 32)
            _rawEvents.RemoveAt(0);
    }

    private void HandleDPadKeyDown(ref InputEvents.KeyDownEvent value)
    {
        AddFilteredEvent($"KeyDown: Key {Input.GetKeyName(value.Key)}, Scancode {Input.GetScancodeName(value.Scancode)}" +
            (value.Repeat ? ", R" : ""));
    }

    private void HandleDPadKeyUp(ref InputEvents.KeyUpEvent value)
    {
        AddFilteredEvent($"KeyUp: Key {Input.GetKeyName(value.Key)}, Scancode {Input.GetScancodeName(value.Scancode)}");
    }

    private void HandleKeyDown(ref InputEvents.KeyDownEvent value)
    {
        if (value.Repeat)
            return;

        AddRawEvent($"KeyDown: Key {Input.GetKeyName(value.Key)}, Scancode {Input.GetScancodeName(value.Scancode)}");
    }

    private void HandleKeyUp(ref InputEvents.KeyUpEvent value)
    {
        AddRawEvent($"KeyUp: Key {Input.GetKeyName(value.Key)}, Scancode {Input.GetScancodeName(value.Scancode)}");
    }

    private void HandleJoystickAxisMove(ref InputEvents.JoystickAxisMoveEvent value)
    {
        AddRawEvent($"AxisMove: Axis {value.Axis}, Value {value.Position}");
    }

    private void HandleJoystickHatMove(ref InputEvents.JoystickHatMoveEvent value)
    {
        var positionName = value.Position switch
        {
            HatPosition.Right => "Right",
            HatPosition.Left => "Left",
            HatPosition.Up => "Up",
            HatPosition.Down => "Down",
            HatPosition.Center => "Center",
            HatPosition.RightDown => "Right Down",
            HatPosition.LeftDown => "Left Down",
            HatPosition.RightUp => "Right Up",
            HatPosition.LeftUp => "Left Up",
            _ => ""
        };
        AddRawEvent($"HatMove: Hat {value.Hat}, Value {positionName}");
    }

    private void HandleJoystickDisconnected(ref InputEvents.JoystickDisconnectedEvent value)
    {
        AddRawEvent($"JoystickDisconnected: Id {value.JoystickID}");
    }

    private void HandleTouchBegin(ref InputEvents.TouchBeginEvent value)
    {
        AddRawEvent($"TouchBegin: Id {value.TouchID}");
    }

    private void HandleTouchMove(ref InputEvents.TouchMoveEvent value)
    {
        AddRawEvent($"TouchMove: Id {value.TouchID}");
    }

    private void HandleTouchEnd(ref InputEvents.TouchEndEvent value)
    {
        AddRawEvent($"TouchEnd: Id {value.TouchID}");
    }

    /// <summary>Handle the logic update event.</summary>
    protected override void Update(float timeStep)
    {
        if (Input.ExitRequested || Input.GetKeyPress(Key.Escape))
            Exit();

        var direction = _aggregatedInput!.GetDirection();
        var directionText = _directionText.AsSpan();
        "Direction: "u8.CopyTo(directionText);
        var offset = "Direction: "u8.Length;
        _ = Utf8Formatter.TryFormat(direction.X, directionText[offset..], out var written, new StandardFormat('F', 2));
        offset += written;
        directionText[offset++] = (byte)' ';
        _ = Utf8Formatter.TryFormat(direction.Y, directionText[offset..], out written, new StandardFormat('F', 2));
        offset += written;
        directionText[offset++] = 0;
        _directionTextLength = offset;
    }

    private void Draw()
    {
        if (ImGui.Begin("Aggregated Input\0"u8, out _, ImGuiWindowFlags.ImGuiWindowFlagsNoSavedSettings))
        {
            ImGui.TextUnformatted("Move marker around with keyboard, joystick or touch\0"u8);
            ImGui.TextUnformatted(_directionText.AsSpan(0, _directionTextLength));

            var up = _dpadInput!.GetScancodeDown(Scancode.Up);
            var down = _dpadInput.GetScancodeDown(Scancode.Down);
            var left = _dpadInput.GetScancodeDown(Scancode.Left);
            var right = _dpadInput.GetScancodeDown(Scancode.Right);
            if (!up && !down && !left && !right)
                ImGui.TextUnformatted("DPad: none\0"u8);
            else
            {
                if (up)
                    ImGui.TextUnformatted("DPad: Up\0"u8);
                if (down)
                    ImGui.TextUnformatted("DPad: Down\0"u8);
                if (left)
                    ImGui.TextUnformatted("DPad: Left\0"u8);
                if (right)
                    ImGui.TextUnformatted("DPad: Right\0"u8);
            }

            ImGui.Separator();
            ImGui.TextUnformatted("Input events:\0"u8);
            for (var i = _rawEvents.Count - 1; i >= 0; --i)
                ImGui.TextUnformatted(_rawEvents[i]);

            ImGui.Separator();
            ImGui.TextUnformatted("Filtered events:\0"u8);
            for (var i = _filteredEvents.Count - 1; i >= 0; --i)
                ImGui.TextUnformatted(_filteredEvents[i]);
        }
        ImGui.End();
    }

    protected override void Stop()
    {
        Deactivate();
        _dpadInput?.Dispose();
        _aggregatedInput?.Dispose();
    }
}
