using System;
using RedBolt;
using RedBolt.Bindings.DearImGui;

#if !REDBOLT_WEB_SAMPLE
try { return new PointerAdapterApplication().Run(); }
catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
#endif

/// <summary>Pointer adapter example.</summary>
sealed class PointerAdapterApplication : Application
{
    private Scene? _scene;
    private Node? _cameraNode;
    private PointerAdapter? _pointerAdapter;
    private Octree? _octree;
    private OutlineGroup? _outlineGroup;

    protected override void Setup(EngineParameters parameters)
    {
        parameters.ApplicationName = "RedBolt.Sample.PointerAdapter"; parameters.Borderless = false; parameters.FullScreen = false;
        parameters.ResourcePaths = "CoreData;Data"; parameters.ResourcePrefixPaths = ".;..";
        parameters.WindowMaximize = false;
        if (ProcessUtils.GetPlatform() != PlatformId.Web)
        {
            parameters.WindowWidth = 1280; parameters.WindowHeight = 720;
        }
        parameters.WindowTitle = "RedBolt Pointer Adapter";
    }

    protected override void Start()
    {
        Context.RegisterSubsystem(new DearImGui((ImGuiConfigFlags)Engine.Parameters.DearImGuiFlags));
        SubscribeToEvent<DearImGuiEvents.EndRenderingDearImGuiEvent>((ref DearImGuiEvents.EndRenderingDearImGuiEvent _) => DrawInstructions());
        CreateScene();
        Renderer.SetViewport(0, new Viewport(_scene!, _cameraNode!.GetComponent<Camera>()!));
        _pointerAdapter = new PointerAdapter();
        SubscribeToEvent<InputEvents.MouseMoveEvent>(_pointerAdapter, HandleMouseMove);
        SubscribeToEvent<InputEvents.MouseButtonDownEvent>(_pointerAdapter, HandleMouseButtonDown);
        SubscribeToEvent<InputEvents.MouseButtonUpEvent>(_pointerAdapter, HandleMouseButtonUp);
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
            node.SetRotation(new Quaternion(0.0f, MathDefs.Random(360.0f), 0.0f));
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
        var ray = Renderer.GetViewport(0)!.GetScreenRay(value.X, value.Y);
        using var query = new RayOctreeQuery(ray, RayQueryLevel.RayTriangle, 100.0f, DrawableFlags.Geometry, 1);
        _octree!.RaycastSingle(query);
        _outlineGroup!.ClearDrawables();
        if (query.Result.Count != 0)
            _outlineGroup.AddDrawable(query.Result[0].Drawable!);
    }
    private void HandleMouseButtonDown(ref InputEvents.MouseButtonDownEvent value) { _outlineGroup!.SetColor(Color.Red); }
    private void HandleMouseButtonUp(ref InputEvents.MouseButtonUpEvent value) { _outlineGroup!.SetColor(Color.White); }

    protected override void Update(float timeStep) { if (Input.ExitRequested || Input.GetKeyPress(Key.Escape)) Exit(); }
    protected override void Stop()
    {
        _pointerAdapter?.SetEnabled(false);
        _pointerAdapter?.Dispose();
    }
    private static void DrawInstructions() { var windowOpen = true; if (ImGui.Begin("Pointer Adapter\0"u8, ref windowOpen, ImGuiWindowFlags.ImGuiWindowFlagsNoSavedSettings)) { ImGui.TextUnformatted("Move the pointer with mouse, touch, keyboard, or joystick\0"u8); ImGui.TextUnformatted("Press with mouse, touch, Enter, or joystick button\0"u8); } ImGui.End(); }
}
