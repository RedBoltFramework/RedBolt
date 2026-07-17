using System;
using System.IO;
using RedBolt;
using RedBolt.Bindings.DearImGui;

try { return new SkeletalAnimationApplication().Run(); }
catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }

/// <summary>Skeletal animation example.</summary>
sealed class SkeletalAnimationApplication : Application
{
    private Scene? _scene;
    private Node? _cameraNode;
    private bool _drawDebug;
    private ContextEventSubscription? _dearImGuiSubscription;

    protected override void Setup(EngineParameters parameters)
    {
        parameters.ApplicationName = "RedBolt.Sample.SkeletalAnimation";
        parameters.Borderless = false; parameters.FullScreen = false;
        parameters.ResourcePaths = "CoreData;Data";
        parameters.ResourcePrefixPaths = FindResourcePrefixPath();
        parameters.WindowMaximize = false; parameters.WindowWidth = 1280; parameters.WindowHeight = 720;
        parameters.WindowTitle = "RedBolt Skeletal Animation";
    }

    protected override void Start()
    {
        Context.RegisterSubsystem(new DearImGui(Context, Engine.Parameters.DearImGuiFlags));
        _dearImGuiSubscription = Context.SubscribeToEvent<DearImGuiEvents.EndRenderingDearImGuiEvent>((ref DearImGuiEvents.EndRenderingDearImGuiEvent _) => DrawInstructions());
        CreateScene();
        Renderer.SetViewport(0, new Viewport(_scene!, _cameraNode!.GetComponent<Camera>()!));
        Input.SetMouseMode(MouseMode.Free);
        Input.SetMouseVisible(true);
    }

    private void CreateScene()
    {
        var cache = Resources;
        _scene = new Scene();
        _scene.CreateComponent<Octree>();
        _scene.CreateComponent<DebugRenderer>();

        var planeNode = _scene.CreateChild("Plane");
        planeNode.SetScale(new Vector3f(50.0f, 1.0f, 50.0f));
        var planeObject = planeNode.CreateComponent<StaticModel>();
        planeObject.SetModel(cache.Get<Model>("Models/Plane.mdl"));
        planeObject.SetMaterial(cache.Get<Material>("Materials/StoneTiled.xml"));

        var zoneNode = _scene.CreateChild("Zone");
        var zone = zoneNode.CreateComponent<Zone>();
        zone.SetBoundingBox(new BoundingBox(-1000.0f, 1000.0f));
        zone.SetAmbientColor(new Color(0.5f, 0.5f, 0.5f));
        zone.SetFogColor(new Color(0.4f, 0.5f, 0.8f));
        zone.SetFogStart(100.0f); zone.SetFogEnd(300.0f);

        var lightNode = _scene.CreateChild("DirectionalLight");
        lightNode.SetDirection(new Vector3f(0.6f, -1.0f, 0.8f));
        var light = lightNode.CreateComponent<Light>();
        light.SetLightType(LightType.Directional); light.SetCastShadows(true);
        light.SetColor(new Color(0.5f, 0.5f, 0.5f));
        light.SetShadowBias(new BiasParameters(0.00025f, 0.5f));
        light.SetShadowCascade(new CascadeParameters(10.0f, 50.0f, 200.0f, 0.0f, 0.8f));

        const uint numModels = 30;
        const float modelMoveSpeed = 2.0f;
        const float modelRotateSpeed = 100.0f;
        var bounds = new BoundingBox(new Vector3f(-20.0f, 0.0f, -20.0f), new Vector3f(20.0f, 0.0f, 20.0f));
        for (var i = 0u; i < numModels; ++i)
        {
            var modelNode = _scene.CreateChild("Jill");
            modelNode.SetPosition(new Vector3f(MathDefs.Random(40.0f) - 20.0f, 0.0f, MathDefs.Random(40.0f) - 20.0f));
            modelNode.SetRotation(Quaternion.FromEulerAngles(0.0f, MathDefs.Random(360.0f), 0.0f));
            var modelObject = modelNode.CreateComponent<AnimatedModel>();
            modelObject.SetModel(cache.Get<Model>("Models/Kachujin/Kachujin.mdl"));
            modelObject.SetMaterial(cache.Get<Material>("Models/Kachujin/Materials/Kachujin.xml"));
            modelObject.SetCastShadows(true);
            var walkAnimation = cache.Get<Animation>("Models/Kachujin/Kachujin_Walk.ani");
            var startTime = MathDefs.Random(walkAnimation.Length);
            modelNode.CreateComponent<AnimationController>().PlayNewExclusive(new AnimationParameters(walkAnimation).Looped().Time(startTime));
            modelNode.CreateComponent<Mover3D>().SetParameters(modelMoveSpeed, modelRotateSpeed, bounds);
        }

        _cameraNode = _scene.CreateChild("Camera");
        _cameraNode.CreateComponent<FreeFlyController>();
        _cameraNode.CreateComponent<Camera>().SetFarClip(300.0f);
        _cameraNode.SetPosition(new Vector3f(0.0f, 5.0f, 0.0f));
    }

    protected override void Update(float timeStep)
    {
        if (Input.ExitRequested || Input.GetKeyPress(Key.Escape)) Exit();
        if (Input.GetKeyPress(Key.Space)) _drawDebug = !_drawDebug;
        // TODO: port Renderer::DrawDebugGeometry(false) when the managed renderer exposes it.
    }

    protected override void Stop() { _dearImGuiSubscription?.Dispose(); _dearImGuiSubscription = null; }

    private void DrawInstructions()
    {
        if (ImGui.Begin("Skeletal Animation\0"u8, out _, ImGuiWindowFlags.ImGuiWindowFlagsNoSavedSettings))
        {
            ImGui.TextUnformatted("Use WASD keys and right mouse button to move\0"u8);
            ImGui.TextUnformatted("Space to toggle debug geometry\0"u8);
            ImGui.TextUnformatted(_drawDebug ? "Debug geometry requested\0"u8 : "Debug geometry disabled\0"u8);
        }
        ImGui.End();
    }

    private static string FindResourcePrefixPath()
    {
        var root = Environment.GetEnvironmentVariable("REDBOLT_RBFX_BIN_DIR");
        if (Valid(root, out var result)) return result;
        for (var current = new DirectoryInfo(AppContext.BaseDirectory); current is not null; current = current.Parent)
        {
            foreach (var path in new[] { Path.Combine(current.FullName, "bin"), Path.Combine(current.FullName, "tests", "RedBolt.Tests", "TestData", "rbfx"), Path.Combine(current.FullName, "rbfx", "bin"), Path.Combine(current.FullName, "..", "rbfx", "bin") })
                if (Valid(path, out result)) return result;
            if (Directory.Exists(Path.Combine(current.FullName, ".git"))) break;
        }
        throw new InvalidOperationException("SkeletalAnimation sample could not locate rbfx CoreData/Data resource directories.");
    }
    private static bool Valid(string? root, out string result) { result = string.Empty; if (string.IsNullOrWhiteSpace(root)) return false; var path = Path.GetFullPath(root); if (!Directory.Exists(Path.Combine(path, "CoreData")) || !Directory.Exists(Path.Combine(path, "Data"))) return false; result = path; return true; }
}
