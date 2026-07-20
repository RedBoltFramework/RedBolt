using System;
using System.Collections.Generic;
using System.Text;
using RedBolt;
using RedBolt.Bindings.DearImGui;

try { return new InputLoggerApplication().Run(); }
catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }

/// <summary>Input event logger example.</summary>
sealed class InputLoggerApplication : Application
{
    private sealed class LoggedEvent { public string EventType = string.Empty; public string Parameters = string.Empty; public uint Count = 1; public long TimeStamp; }
    private readonly List<LoggedEvent> _eventLog = [];
    private readonly List<ContextEventSubscription> _subscriptions = [];
    private readonly List<byte[]> _displayLines = [];
    private ContextEventSubscription? _dearImGuiSubscription;

    protected override void Setup(EngineParameters parameters) { parameters.ApplicationName = "RedBolt.Sample.InputLogger"; parameters.Borderless = false; parameters.FullScreen = false; parameters.ResourcePaths = "CoreData;Data"; parameters.ResourcePrefixPaths = ".;.."; parameters.WindowMaximize = false; parameters.WindowWidth = 1280; parameters.WindowHeight = 720; parameters.WindowTitle = "RedBolt Input Logger"; }
    protected override void Start()
    {
        Context.RegisterSubsystem(new DearImGui((ImGuiConfigFlags)Engine.Parameters.DearImGuiFlags));
        _dearImGuiSubscription = Context.SubscribeToEvent<DearImGuiEvents.EndRenderingDearImGuiEvent>((ref DearImGuiEvents.EndRenderingDearImGuiEvent _) => Draw());
        CreateViewports();
        Input.SetMouseVisible(true); Input.SetMouseMode(MouseMode.Free);
        _subscriptions.Add(Context.SubscribeToEvent<InputEvents.MouseButtonDownEvent>((ref InputEvents.MouseButtonDownEvent e) => AddEvent("MouseButtonDown", $"Button={e.Button}, Clicks={e.Clicks}")));
        _subscriptions.Add(Context.SubscribeToEvent<InputEvents.MouseButtonUpEvent>((ref InputEvents.MouseButtonUpEvent e) => AddEvent("MouseButtonUp", $"Button={e.Button}")));
        _subscriptions.Add(Context.SubscribeToEvent<InputEvents.MouseMoveEvent>((ref InputEvents.MouseMoveEvent e) => AddEvent("MouseMove", $"X={e.X}, Y={e.Y}")));
        _subscriptions.Add(Context.SubscribeToEvent<InputEvents.MouseWheelEvent>((ref InputEvents.MouseWheelEvent e) => AddEvent("MouseWheel", $"Wheel={e.Wheel}")));
        _subscriptions.Add(Context.SubscribeToEvent<InputEvents.KeyDownEvent>((ref InputEvents.KeyDownEvent e) => AddEvent("KeyDown", $"Key={e.Key}, Repeat={e.Repeat}")));
        _subscriptions.Add(Context.SubscribeToEvent<InputEvents.KeyUpEvent>((ref InputEvents.KeyUpEvent e) => AddEvent("KeyUp", $"Key={e.Key}")));
        _subscriptions.Add(Context.SubscribeToEvent<InputEvents.TextInputEvent>((ref InputEvents.TextInputEvent e) => AddEvent("TextInput", $"Text={e.Text}")));
        _subscriptions.Add(Context.SubscribeToEvent<InputEvents.TextEditingEvent>((ref InputEvents.TextEditingEvent e) => AddEvent("TextEditing", $"Text={e.Composition}")));
        _subscriptions.Add(Context.SubscribeToEvent<InputEvents.JoystickConnectedEvent>((ref InputEvents.JoystickConnectedEvent e) => AddEvent("JoystickConnected", $"JoystickID={e.JoystickID}")));
        _subscriptions.Add(Context.SubscribeToEvent<InputEvents.JoystickDisconnectedEvent>((ref InputEvents.JoystickDisconnectedEvent e) => AddEvent("JoystickDisconnected", $"JoystickID={e.JoystickID}")));
        _subscriptions.Add(Context.SubscribeToEvent<InputEvents.JoystickButtonDownEvent>((ref InputEvents.JoystickButtonDownEvent e) => AddEvent("JoystickButtonDown", $"JoystickID={e.JoystickID}, Button={e.Button}")));
        _subscriptions.Add(Context.SubscribeToEvent<InputEvents.JoystickButtonUpEvent>((ref InputEvents.JoystickButtonUpEvent e) => AddEvent("JoystickButtonUp", $"JoystickID={e.JoystickID}, Button={e.Button}")));
        _subscriptions.Add(Context.SubscribeToEvent<InputEvents.JoystickAxisMoveEvent>((ref InputEvents.JoystickAxisMoveEvent e) => AddEvent("JoystickAxisMove", $"JoystickID={e.JoystickID}, Axis={e.Axis}, Position={e.Position}")));
        _subscriptions.Add(Context.SubscribeToEvent<InputEvents.JoystickHatMoveEvent>((ref InputEvents.JoystickHatMoveEvent e) => AddEvent("JoystickHatMove", $"JoystickID={e.JoystickID}, Hat={e.Hat}")));
        _subscriptions.Add(Context.SubscribeToEvent<InputEvents.TouchBeginEvent>((ref InputEvents.TouchBeginEvent e) => AddEvent("TouchBegin", $"TouchID={e.TouchID}, Pressure={e.Pressure}")));
        _subscriptions.Add(Context.SubscribeToEvent<InputEvents.TouchEndEvent>((ref InputEvents.TouchEndEvent e) => AddEvent("TouchEnd", $"TouchID={e.TouchID}")));
        _subscriptions.Add(Context.SubscribeToEvent<InputEvents.TouchMoveEvent>((ref InputEvents.TouchMoveEvent e) => AddEvent("TouchMove", $"TouchID={e.TouchID}, Pressure={e.Pressure}")));
        _subscriptions.Add(Context.SubscribeToEvent<InputEvents.GestureRecordedEvent>((ref InputEvents.GestureRecordedEvent e) => AddEvent("GestureRecorded", $"GestureID={e.GestureID}")));
        _subscriptions.Add(Context.SubscribeToEvent<InputEvents.GestureInputEvent>((ref InputEvents.GestureInputEvent e) => AddEvent("GestureInput", $"GestureID={e.GestureID}, Fingers={e.NumFingers}, Error={e.Error}")));
        _subscriptions.Add(Context.SubscribeToEvent<InputEvents.MultiGestureEvent>((ref InputEvents.MultiGestureEvent e) => AddEvent("MultiGesture", $"DTheta={e.DTheta}, DDist={e.DDist}")));
    }
    private void CreateViewports()
    {
        Renderer.SetNumViewports(2);
        Renderer.SetViewport(0, CreateViewport(new Color(0x00 / 255f, 0x47 / 255f, 0xab / 255f), IntRect.Zero));
        Renderer.SetViewport(1, CreateViewport(new Color(0x00 / 255f, 0x11 / 255f, 0x67 / 255f), new IntRect(350, 50, 450, 100)));
    }
    private static Viewport CreateViewport(Color color, IntRect rect)
    {
        var scene = new Scene(); scene.CreateComponent<Octree>(); var zoneNode = scene.CreateChild("Zone"); var zone = zoneNode.CreateComponent<Zone>(); zone.SetBoundingBox(new BoundingBox(-1000.0f, 1000.0f)); zone.SetFogColor(color); var camera = scene.CreateChild("Camera").CreateComponent<Camera>(); return new Viewport(scene, camera, rect);
    }
    private void AddEvent(string eventType, string parameters)
    {
        var now = Environment.TickCount64;
        var mergeable = eventType is "KeyDown" or "TextInput" or "MouseMove" or "MouseWheel" or "JoystickButtonDown" or "JoystickAxisMove" or "JoystickHatMove" or "TouchMove";
        var existing = mergeable ? _eventLog.Find(value => value.EventType == eventType && now - value.TimeStamp <= 250) : null;
        if (existing is not null) { ++existing.Count; existing.Parameters = parameters; existing.TimeStamp = now; }
        else { _eventLog.Insert(0, new LoggedEvent { EventType = eventType, Parameters = parameters, TimeStamp = now }); while (_eventLog.Count > 100) _eventLog.RemoveAt(_eventLog.Count - 1); }
        UpdateText();
    }
    private void UpdateText()
    {
        _displayLines.Clear(); var mouse = Input.GetMousePosition(); var backbuffer = Input.GetBackbufferSize(); AddLine($"Mouse Position: {mouse.X} {mouse.Y} / {backbuffer.X} {backbuffer.Y}");
        foreach (var value in _eventLog) AddLine(value.EventType + (value.Count > 1 ? $" x{value.Count}" : "") + (value.Parameters.Length != 0 ? $" ({value.Parameters})" : ""));
    }
    private void AddLine(string value) => _displayLines.Add(Encoding.UTF8.GetBytes(value + "\0"));
    private void Draw() { if (ImGui.Begin("Input Logger\0"u8, out _, ImGuiWindowFlags.ImGuiWindowFlagsNoSavedSettings)) foreach (var line in _displayLines) ImGui.TextUnformatted(line); ImGui.End(); }
    protected override void Update(float timeStep) { if (Input.ExitRequested || Input.GetKeyPress(Key.Escape)) Exit(); UpdateText(); }
    protected override void Stop() { foreach (var value in _subscriptions) value.Dispose(); _subscriptions.Clear(); _dearImGuiSubscription?.Dispose(); Renderer.SetNumViewports(0); }
}
