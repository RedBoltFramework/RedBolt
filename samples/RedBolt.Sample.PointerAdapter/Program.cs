using System;
using System.IO;
using RedBolt;
using RedBolt.Bindings.DearImGui;

try { return new PointerAdapterApplication().Run(); }
catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }

/// <summary>Pointer adapter example.</summary>
sealed class PointerAdapterApplication : Application
{
    private Scene? _scene;
    private Node? _cameraNode;
    private PointerAdapter? _pointerAdapter;
    private Octree? _octree;
    private OutlineGroup? _outlineGroup;
    private ContextEventSubscription? _mouseMoveSubscription;
    private ContextEventSubscription? _mouseDownSubscription;
    private ContextEventSubscription? _mouseUpSubscription;
    private ContextEventSubscription? _dearImGuiSubscription;

    protected override void Setup(EngineParameters parameters)
    {
        parameters.ApplicationName = "RedBolt.Sample.PointerAdapter"; parameters.Borderless = false; parameters.FullScreen = false;
        parameters.ResourcePaths = "CoreData;Data"; parameters.ResourcePrefixPaths = FindResourcePrefixPath();
        parameters.WindowMaximize = false; parameters.WindowWidth = 1280; parameters.WindowHeight = 720; parameters.WindowTitle = "RedBolt Pointer Adapter";
    }

    protected override void Start()
    {
        Context.RegisterSubsystem(new DearImGui(Context, Engine.Parameters.DearImGuiFlags));
        _dearImGuiSubscription = Context.SubscribeToEvent<DearImGuiEvents.EndRenderingDearImGuiEvent>((ref DearImGuiEvents.EndRenderingDearImGuiEvent _) => DrawInstructions());
        CreateScene();
        Renderer.SetViewport(0, new Viewport(_scene!, _cameraNode!.GetComponent<Camera>()!));
        _pointerAdapter = new PointerAdapter(Input);
        _mouseMoveSubscription = Context.SubscribeToEvent<InputEvents.MouseMoveEvent>(_pointerAdapter, HandleMouseMove);
        _mouseDownSubscription = Context.SubscribeToEvent<InputEvents.MouseButtonDownEvent>(_pointerAdapter, HandleMouseButtonDown);
        _mouseUpSubscription = Context.SubscribeToEvent<InputEvents.MouseButtonUpEvent>(_pointerAdapter, HandleMouseButtonUp);
        _pointerAdapter.SetEnabled(true);
        Input.SetMouseMode(MouseMode.Free); Input.SetMouseVisible(true);
    }

    private void CreateScene()
    {
        var cache = Resources;
        _scene = new Scene();
        _octree = _scene.CreateComponent<Octree>();
        // TODO: port scene_->CreateComponent<PhysicsWorld>(); when the managed physics subsystem is available.
        _outlineGroup = _scene.CreateComponent<OutlineGroup>();
        _outlineGroup.SetColor(Color.Yellow);

        var planeNode = _scene.CreateChild("Plane");
        planeNode.SetScale(new Vector3f(20.0f, 1.0f, 20.0f));
        var plane = planeNode.CreateComponent<StaticModel>();
        plane.SetModel(cache.Get<Model>("Models/Plane.mdl"));
        plane.SetMaterial(cache.Get<Material>("Materials/StoneTiled.xml"));

        var lightNode = _scene.CreateChild("DirectionalLight");
        lightNode.SetDirection(new Vector3f(0.6f, -1.0f, 0.8f));
        lightNode.CreateComponent<Light>().SetLightType(LightType.Directional);

        for (var i = 0; i < 100; ++i)
        {
            var node = _scene.CreateChild("Mushroom");
            node.SetPosition(new Vector3f(MathDefs.Random(18.0f) - 9.0f, 0.0f, MathDefs.Random(18.0f) - 9.0f));
            node.SetRotation(Quaternion.FromEulerAngles(0.0f, MathDefs.Random(360.0f), 0.0f));
            node.SetScale(0.5f + MathDefs.Random(1.0f));
            var model = node.CreateComponent<StaticModel>();
            model.SetModel(cache.Get<Model>("Models/Mushroom.mdl"));
            model.SetMaterial(cache.Get<Material>("Materials/Mushroom.xml"));
        }

        _cameraNode = _scene.CreateChild("Camera");
        _cameraNode.SetPosition(new Vector3f(0.0f, 10.0f, -20.0f));
        _cameraNode.LookAt(Vector3f.Zero);
        _cameraNode.CreateComponent<Camera>();
    }

    private void HandleMouseMove(ref InputEvents.MouseMoveEvent value)
    {
        var ray = _cameraNode!.GetComponent<Camera>()!.GetScreenRayFromMouse();
        var result = _octree!.RaycastSingle(ray, RayQueryLevel.RayTriangle, 1000.0f, DrawableFlags.Geometry);
        _outlineGroup!.ClearDrawables();
        if (result?.Drawable is not null) _outlineGroup.AddDrawable(result.Drawable);
    }
    private static void HandleMouseButtonDown(ref InputEvents.MouseButtonDownEvent value) { }
    private static void HandleMouseButtonUp(ref InputEvents.MouseButtonUpEvent value) { }

    protected override void Update(float timeStep) { if (Input.ExitRequested || Input.GetKeyPress(Key.Escape)) Exit(); }
    protected override void Stop()
    {
        _pointerAdapter?.SetEnabled(false);
        _mouseMoveSubscription?.Dispose(); _mouseDownSubscription?.Dispose(); _mouseUpSubscription?.Dispose();
        _dearImGuiSubscription?.Dispose(); _pointerAdapter?.Dispose();
    }
    private static void DrawInstructions() { if (ImGui.Begin("Pointer Adapter\0"u8, out _, ImGuiWindowFlags.ImGuiWindowFlagsNoSavedSettings)) { ImGui.TextUnformatted("Move the pointer with mouse, touch, keyboard, or joystick\0"u8); ImGui.TextUnformatted("Press with mouse, touch, Enter, or joystick button\0"u8); } ImGui.End(); }

    private static string FindResourcePrefixPath() { var root = Environment.GetEnvironmentVariable("REDBOLT_RBFX_BIN_DIR"); if (Valid(root, out var result)) return result; for (var current = new DirectoryInfo(AppContext.BaseDirectory); current is not null; current = current.Parent) { foreach (var path in new[] { Path.Combine(current.FullName, "bin"), Path.Combine(current.FullName, "tests", "RedBolt.Tests", "TestData", "rbfx"), Path.Combine(current.FullName, "rbfx", "bin"), Path.Combine(current.FullName, "..", "rbfx", "bin") }) if (Valid(path, out result)) return result; if (Directory.Exists(Path.Combine(current.FullName, ".git"))) break; } throw new InvalidOperationException("PointerAdapter sample could not locate rbfx CoreData/Data resource directories."); }
    private static bool Valid(string? root, out string result) { result = string.Empty; if (string.IsNullOrWhiteSpace(root)) return false; var path = Path.GetFullPath(root); if (!Directory.Exists(Path.Combine(path, "CoreData")) || !Directory.Exists(Path.Combine(path, "Data"))) return false; result = path; return true; }
}
