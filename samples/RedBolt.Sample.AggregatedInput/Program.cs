using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using RedBolt;
using RedBolt.Bindings.DearImGui;

try { return new AggregatedInputApplication().Run(); }
catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }

/// <summary>Aggregated input example.</summary>
sealed class AggregatedInputApplication : Application
{
    private DirectionAggregator? _directionAggregator;
    private DirectionalPadAdapter? _directionalPadAdapter;
    private ContextEventSubscription? _dearImGuiSubscription;
    private ContextEventSubscription? _keyDownSubscription;
    private ContextEventSubscription? _keyUpSubscription;
    private byte[] _directionText = "Direction: 0 0\0"u8.ToArray();
    private byte[] _dpadText = "DPad: none\0"u8.ToArray();
    private readonly Queue<string> _events = new();

    protected override void Setup(EngineParameters parameters)
    {
        parameters.ApplicationName = "RedBolt.Sample.AggregatedInput"; parameters.Borderless = false; parameters.FullScreen = false;
        parameters.ResourcePaths = "CoreData;Data"; parameters.ResourcePrefixPaths = FindResourcePrefixPath();
        parameters.WindowMaximize = false; parameters.WindowWidth = 1280; parameters.WindowHeight = 720; parameters.WindowTitle = "RedBolt Aggregated Input";
    }

    protected override void Start()
    {
        Context.RegisterSubsystem(new DearImGui(Context, Engine.Parameters.DearImGuiFlags));
        _dearImGuiSubscription = Context.SubscribeToEvent<DearImGuiEvents.EndRenderingDearImGuiEvent>((ref DearImGuiEvents.EndRenderingDearImGuiEvent _) => Draw());
        SetupViewport();
        _directionAggregator = new DirectionAggregator(Input);
        _directionalPadAdapter = new DirectionalPadAdapter(Input);
        _keyDownSubscription = Context.SubscribeToEvent<InputEvents.KeyDownEvent>(_directionalPadAdapter, HandleKeyDown);
        _keyUpSubscription = Context.SubscribeToEvent<InputEvents.KeyUpEvent>(_directionalPadAdapter, HandleKeyUp);
        _directionAggregator.SetEnabled(true);
        _directionalPadAdapter.SetEnabled(true);
        Input.SetMouseMode(MouseMode.Free); Input.SetMouseVisible(true);
    }

    private void SetupViewport()
    {
        var scene = new Scene();
        scene.CreateComponent<Octree>();
        var zone = scene.CreateChild("Zone").CreateComponent<Zone>();
        zone.SetBoundingBox(new BoundingBox(-1000.0f, 1000.0f));
        zone.SetFogColor(Color.Black);
        var camera = scene.CreateChild("Camera").CreateComponent<Camera>();
        Renderer.SetViewport(0, new Viewport(scene, camera));
    }

    protected override void Update(float timeStep)
    {
        if (Input.ExitRequested || Input.GetKeyPress(Key.Escape)) Exit();
        var direction = _directionAggregator!.GetDirection();
        _directionText = Encoding.UTF8.GetBytes($"Direction: {direction.X:0.00} {direction.Y:0.00}\0");
        var up = _directionalPadAdapter!.GetScancodeDown(Scancode.Up);
        var down = _directionalPadAdapter.GetScancodeDown(Scancode.Down);
        var left = _directionalPadAdapter.GetScancodeDown(Scancode.Left);
        var right = _directionalPadAdapter.GetScancodeDown(Scancode.Right);
        _dpadText = Encoding.UTF8.GetBytes($"DPad: {(up ? "Up " : "")}{(down ? "Down " : "")}{(left ? "Left " : "")}{(right ? "Right" : "")}\0");
    }

    private void HandleKeyDown(ref InputEvents.KeyDownEvent value) => AddEvent($"DPad KeyDown: {value.Key}{(value.Repeat ? " repeat" : "")}");
    private void HandleKeyUp(ref InputEvents.KeyUpEvent value) => AddEvent($"DPad KeyUp: {value.Key}");
    private void AddEvent(string text) { _events.Enqueue(text); while (_events.Count > 32) _events.Dequeue(); }

    private void Draw()
    {
        if (ImGui.Begin("Aggregated Input\0"u8, out _, ImGuiWindowFlags.ImGuiWindowFlagsNoSavedSettings))
        {
            ImGui.TextUnformatted("Use WASD, arrows, joystick axes/DPad, or touch drag\0"u8);
            ImGui.TextUnformatted(_directionText); ImGui.TextUnformatted(_dpadText);
            foreach (var value in _events) ImGui.TextUnformatted(Encoding.UTF8.GetBytes(value + "\0"));
        }
        ImGui.End();
    }

    protected override void Stop()
    {
        _keyUpSubscription?.Dispose(); _keyDownSubscription?.Dispose(); _dearImGuiSubscription?.Dispose();
        _directionalPadAdapter?.Dispose(); _directionAggregator?.Dispose();
    }

    private static string FindResourcePrefixPath() { var root = Environment.GetEnvironmentVariable("REDBOLT_RBFX_BIN_DIR"); if (Valid(root, out var result)) return result; for (var current = new DirectoryInfo(AppContext.BaseDirectory); current is not null; current = current.Parent) { foreach (var path in new[] { Path.Combine(current.FullName, "bin"), Path.Combine(current.FullName, "tests", "RedBolt.Tests", "TestData", "rbfx"), Path.Combine(current.FullName, "rbfx", "bin"), Path.Combine(current.FullName, "..", "rbfx", "bin") }) if (Valid(path, out result)) return result; if (Directory.Exists(Path.Combine(current.FullName, ".git"))) break; } throw new InvalidOperationException("AggregatedInput sample could not locate rbfx CoreData/Data resource directories."); }
    private static bool Valid(string? root, out string result) { result = string.Empty; if (string.IsNullOrWhiteSpace(root)) return false; var path = Path.GetFullPath(root); if (!Directory.Exists(Path.Combine(path, "CoreData")) || !Directory.Exists(Path.Combine(path, "Data"))) return false; result = path; return true; }
}
