namespace RedBolt.Sample.InputLogger;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

using RedBolt;
using RedBolt.Bindings.DearImGui;
using RedBolt.Core;
using RedBolt.Engine;
using RedBolt.Graphics;
using RedBolt.Input;
using RedBolt.Math;
using RedBolt.Scene;

internal static class InputLoggerProgram
{
    private static int Main()
    {
        try
        {
            return new InputLoggerApplication().Run();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }
}

/// <summary>Input event logger example.</summary>
// TODO: port MainPluginApplication and URHO3D_DEFINE_PLUGIN_MAIN when the managed plugin application API is available.
// C++: class InputLogger : public MainPluginApplication
sealed class InputLoggerApplication : Application
{
    private sealed class LoggedEvent
    {
        public string EventType = string.Empty;
        public SortedDictionary<string, string> Parameters = new(StringComparer.Ordinal);
        public int Count;
        public uint TimeStamp;
    }

    private sealed class ViewportData
    {
        public Viewport Viewport = null!;
        public Scene Scene = null!;
        public Camera Camera = null!;
    }

    private readonly ViewportData?[] _viewports = new ViewportData?[2];
    private readonly List<byte[]> _displayLines = [];
    private readonly List<LoggedEvent> _eventLog = [];

    protected override void Setup(EngineParameters parameters)
    {
        parameters.ApplicationName = "RedBolt.Sample.InputLogger";
        parameters.Borderless = false;
        parameters.FullScreen = false;
        parameters.ResourcePaths = "CoreData;Data";
        parameters.ResourcePrefixPaths = ".;..";
        parameters.WindowMaximize = false;
        if (ProcessUtils.GetPlatform() != PlatformId.Web)
        {
            parameters.WindowWidth = 1280;
            parameters.WindowHeight = 720;
        }
        parameters.WindowTitle = "RedBolt InputSystem Logger";
    }

    private ViewportData CreateViewport(Color color, IntRect rect)
    {
        var scene = new Scene();
        scene.CreateComponent<Octree>();

        // Create zone
        var zoneNode = scene.CreateChild("Zone");
        var zone = zoneNode.CreateComponent<Zone>();
        zone.SetBoundingBox(new BoundingBox(-1000.0f, 1000.0f));
        zone.SetFogColor(color);

        // Create camera
        var cameraNode = scene.CreateChild("Camera");
        var camera = cameraNode.CreateComponent<Camera>();

        var viewport = new Viewport(scene, camera, rect);
        return new ViewportData { Viewport = viewport, Scene = scene, Camera = camera };
    }

    protected override void Start()
    {
        Context.RegisterSubsystem(new DearImGui((ImGuiConfigFlags)Engine.Parameters.DearImGuiFlags));
        SubscribeToEvent<DearImGuiEvents.EndRenderingDearImGuiEvent>(
            (ref DearImGuiEvents.EndRenderingDearImGuiEvent _) => Draw());

        // Create viewports
        _viewports[0] = CreateViewport(new Color(0x00 / 255f, 0x47 / 255f, 0xab / 255f), IntRect.Zero);
        _viewports[1] = CreateViewport(
            new Color(0x00 / 255f, 0x11 / 255f, 0x67 / 255f), new IntRect(350, 50, 450, 100));

        // Setup engine state
        Renderer.SetNumViewports(2);
        Renderer.SetViewport(0, _viewports[0]!.Viewport);
        Renderer.SetViewport(1, _viewports[1]!.Viewport);
        Input.SetMouseVisible(true);
        Input.SetMouseMode(MouseMode.Free);
        //Input.SetMouseMode(MouseMode.Wrap);

        SubscribeToEvent<InputEvents.MouseButtonDownEvent>(
            (ref InputEvents.MouseButtonDownEvent e) => OnInputEvent(DecodeEvent(
                "MouseButtonDown", ("Button", ((int)e.Button).ToString(CultureInfo.InvariantCulture)),
                ("Clicks", e.Clicks.ToString(CultureInfo.InvariantCulture)))));        SubscribeToEvent<InputEvents.MouseButtonUpEvent>(
            (ref InputEvents.MouseButtonUpEvent e) => OnInputEvent(DecodeEvent(
                "MouseButtonUp", ("Button", ((int)e.Button).ToString(CultureInfo.InvariantCulture)))));        SubscribeToEvent<InputEvents.MouseMoveEvent>(
            (ref InputEvents.MouseMoveEvent e) => OnInputEvent(DecodeEvent(
                "MouseMove", ("X", e.X.ToString(CultureInfo.InvariantCulture)),
                ("Y", e.Y.ToString(CultureInfo.InvariantCulture)))));        SubscribeToEvent<InputEvents.MouseWheelEvent>(
            (ref InputEvents.MouseWheelEvent e) => OnInputEvent(DecodeEvent(
                "MouseWheel", ("Wheel", e.Wheel.ToString(CultureInfo.InvariantCulture)))));        SubscribeToEvent<InputEvents.KeyDownEvent>(
            (ref InputEvents.KeyDownEvent e) => OnInputEvent(DecodeEvent(
                "KeyDown", ("Key", ((int)e.Key).ToString(CultureInfo.InvariantCulture)),
                ("Repeat", e.Repeat ? "true" : "false"))));        SubscribeToEvent<InputEvents.KeyUpEvent>(
            (ref InputEvents.KeyUpEvent e) => OnInputEvent(DecodeEvent(
                "KeyUp", ("Key", ((int)e.Key).ToString(CultureInfo.InvariantCulture)))));        SubscribeToEvent<InputEvents.TextInputEvent>(
            (ref InputEvents.TextInputEvent e) => OnInputEvent(DecodeEvent("TextInput", ("Text", e.Text))));        SubscribeToEvent<InputEvents.TextEditingEvent>(
            (ref InputEvents.TextEditingEvent e) =>
                OnInputEvent(DecodeEvent("TextEditing", ("Text", e.Composition))));        SubscribeToEvent<InputEvents.JoystickConnectedEvent>(
            (ref InputEvents.JoystickConnectedEvent e) => OnInputEvent(DecodeEvent(
                "JoystickConnected", ("JoystickID", e.JoystickID.ToString(CultureInfo.InvariantCulture)))));        SubscribeToEvent<InputEvents.JoystickDisconnectedEvent>(
            (ref InputEvents.JoystickDisconnectedEvent e) => OnInputEvent(DecodeEvent(
                "JoystickDisconnected", ("JoystickID", e.JoystickID.ToString(CultureInfo.InvariantCulture)))));        SubscribeToEvent<InputEvents.JoystickButtonDownEvent>(
            (ref InputEvents.JoystickButtonDownEvent e) => OnInputEvent(DecodeEvent(
                "JoystickButtonDown", ("JoystickID", e.JoystickID.ToString(CultureInfo.InvariantCulture)),
                ("Button", e.Button.ToString(CultureInfo.InvariantCulture)))));        SubscribeToEvent<InputEvents.JoystickButtonUpEvent>(
            (ref InputEvents.JoystickButtonUpEvent e) => OnInputEvent(DecodeEvent(
                "JoystickButtonUp", ("JoystickID", e.JoystickID.ToString(CultureInfo.InvariantCulture)),
                ("Button", e.Button.ToString(CultureInfo.InvariantCulture)))));        SubscribeToEvent<InputEvents.JoystickAxisMoveEvent>(
            (ref InputEvents.JoystickAxisMoveEvent e) => OnInputEvent(DecodeEvent(
                "JoystickAxisMove", ("JoystickID", e.JoystickID.ToString(CultureInfo.InvariantCulture)),
                ("Axis", e.Axis.ToString(CultureInfo.InvariantCulture)),
                ("Position", e.Position.ToString(CultureInfo.InvariantCulture)))));        SubscribeToEvent<InputEvents.JoystickHatMoveEvent>(
            (ref InputEvents.JoystickHatMoveEvent e) => OnInputEvent(DecodeEvent(
                "JoystickHatMove", ("JoystickID", e.JoystickID.ToString(CultureInfo.InvariantCulture)),
                ("Hat", e.Hat.ToString(CultureInfo.InvariantCulture)))));        SubscribeToEvent<InputEvents.TouchBeginEvent>(
            (ref InputEvents.TouchBeginEvent e) => OnInputEvent(DecodeEvent(
                "TouchBegin", ("TouchID", e.TouchID.ToString(CultureInfo.InvariantCulture)),
                ("Pressure", e.Pressure.ToString(CultureInfo.InvariantCulture)))));        SubscribeToEvent<InputEvents.TouchEndEvent>(
            (ref InputEvents.TouchEndEvent e) => OnInputEvent(DecodeEvent(
                "TouchEnd", ("TouchID", e.TouchID.ToString(CultureInfo.InvariantCulture)))));        SubscribeToEvent<InputEvents.TouchMoveEvent>(
            (ref InputEvents.TouchMoveEvent e) => OnInputEvent(DecodeEvent(
                "TouchMove", ("TouchID", e.TouchID.ToString(CultureInfo.InvariantCulture)),
                ("Pressure", e.Pressure.ToString(CultureInfo.InvariantCulture)))));        SubscribeToEvent<InputEvents.GestureRecordedEvent>(
            (ref InputEvents.GestureRecordedEvent e) => OnInputEvent(DecodeEvent(
                "GestureRecorded", ("GestureID", e.GestureID.ToString(CultureInfo.InvariantCulture)))));        SubscribeToEvent<InputEvents.GestureInputEvent>(
            (ref InputEvents.GestureInputEvent e) => OnInputEvent(DecodeEvent(
                "GestureInput", ("GestureID", e.GestureID.ToString(CultureInfo.InvariantCulture)),
                ("CenterX", e.CenterX.ToString(CultureInfo.InvariantCulture)),
                ("CenterY", e.CenterY.ToString(CultureInfo.InvariantCulture)),
                ("NumFingers", e.NumFingers.ToString(CultureInfo.InvariantCulture)),
                ("Error", e.Error.ToString(CultureInfo.InvariantCulture)))));        SubscribeToEvent<InputEvents.MultiGestureEvent>(
            (ref InputEvents.MultiGestureEvent e) => OnInputEvent(DecodeEvent(
                "MultiGesture", ("CenterX", e.CenterX.ToString(CultureInfo.InvariantCulture)),
                ("CenterY", e.CenterY.ToString(CultureInfo.InvariantCulture)),
                ("NumFingers", e.NumFingers.ToString(CultureInfo.InvariantCulture)),
                ("DTheta", e.DTheta.ToString(CultureInfo.InvariantCulture)),
                ("DDist", e.DDist.ToString(CultureInfo.InvariantCulture)))));    }

    private void OnInputEvent(LoggedEvent inputEvent)
    {
        if (!MergeEvent(inputEvent))
            AddEvent(inputEvent);
        UpdateText();
    }

    private static LoggedEvent DecodeEvent(
        string eventType, params (string Name, string Value)[] parameters)
    {
        var inputEvent = new LoggedEvent
        {
            Count = 1,
            TimeStamp = Time.GetSystemTime(),
            EventType = eventType,
        };
        foreach (var (name, value) in parameters)
            inputEvent.Parameters[name] = value;
        return inputEvent;
    }

    private bool MergeEvent(LoggedEvent inputEvent)
    {
        var sameEvent = _eventLog.Find(
            existingEvent => CanMergeEventWith(inputEvent, existingEvent));
        if (sameEvent is null)
            return false;

        ++sameEvent.Count;
        sameEvent.Parameters = inputEvent.Parameters;
        sameEvent.TimeStamp = inputEvent.TimeStamp;
        return true;
    }

    private static bool CanMergeEventWith(LoggedEvent inputEvent, LoggedEvent existingEvent)
    {
        var mergeable = inputEvent.EventType is
            "KeyDown" or
            "TextInput" or
            "MouseMove" or
            "MouseWheel" or
            "JoystickButtonDown" or
            "JoystickAxisMove" or
            "JoystickHatMove" or
            "TouchMove";

        const uint mergeThresholdMs = 250;
        if (inputEvent.EventType != existingEvent.EventType || !mergeable)
            return false;
        if (unchecked(inputEvent.TimeStamp - existingEvent.TimeStamp) > mergeThresholdMs)
            return false;

        foreach (var (name, oldValue) in existingEvent.Parameters)
        {
            if (name is "Clicks" or "X" or "Y" or "Position")
                continue;
            if (!inputEvent.Parameters.TryGetValue(name, out var newValue) || newValue != oldValue)
                return false;
        }

        return true;
    }

    private void AddEvent(LoggedEvent inputEvent)
    {
        _eventLog.Insert(0, inputEvent);
        while (_eventLog.Count > 100)
            _eventLog.RemoveAt(_eventLog.Count - 1);
    }

    protected override void Update(float timeStep)
    {
        if (Input.ExitRequested || Input.GetKeyPress(Key.Escape))
            Exit();
    }

    private void UpdateText()
    {
        var mousePosition = Input.GetMousePosition();
        var backbufferSize = Input.GetBackbufferSize();

        _displayLines.Clear();
        AddLine(
            $"Mouse Position: {mousePosition.X} {mousePosition.Y} / {backbufferSize.X} {backbufferSize.Y}");

        for (var index = 0; index < 2; ++index)
        {
            var position = _viewports[index]!.Camera.MousePosition;
            var isInside = Rect.Positive.IsInside(position) != Intersection.Outside;
            AddLine($"- relative to Viewport #{index}: ({(isInside ? "in" : "out")}) {position}");
        }

        AddLine(string.Empty);

        foreach (var inputEvent in _eventLog)
        {
            var text = inputEvent.EventType;
            if (inputEvent.Count > 1)
                text += $" x{inputEvent.Count}";
            if (inputEvent.Parameters.Count != 0)
            {
                text += " (";
                var firstParam = true;
                foreach (var (name, value) in inputEvent.Parameters)
                {
                    if (!firstParam)
                        text += ", ";
                    text += name;
                    text += "=";
                    text += value;
                    firstParam = false;
                }
                text += ")";
            }
            AddLine(text);
        }
    }

    private void AddLine(string value)
    {
        _displayLines.Add(Encoding.UTF8.GetBytes(value + "\0"));
    }

    private void Draw()
    {
        var windowOpen = true;
        if (ImGui.Begin("InputSystem Logger\0"u8, ref windowOpen, ImGuiWindowFlags.ImGuiWindowFlagsNoSavedSettings))
        {
            foreach (var line in _displayLines)
                ImGui.TextUnformatted(line);
        }
        ImGui.End();
    }

    protected override void Stop()
    {

        Renderer.SetNumViewports(0);

        Array.Clear(_viewports);
    }
}
