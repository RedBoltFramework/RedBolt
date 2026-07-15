using System;
using System.IO;
using RedBolt;
using RedBolt.Bindings.DearImGui;

try
{
    var exitCode = new MultipleViewportsApplication().Run();
    if (exitCode == 0)
        Console.WriteLine("RedBolt MultipleViewports sample finished.");
    return exitCode;
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex);
    return 1;
}

sealed class MultipleViewportsApplication : Application
{
    private Scene? _scene;
    private Node? _cameraNode;
    private Node? _rearCameraNode;
    private ContextEventSubscription? _dearImGuiSubscription;
    private ContextEventSubscription? _postRenderSubscription;
    private bool _drawDebug;

    protected override void Setup(EngineParameters parameters)
    {
        parameters.ApplicationName = "RedBolt.Sample.MultipleViewports";
        parameters.Borderless = false;
        parameters.FullScreen = false;
        parameters.GraphicsBackend = GraphicsBackend.OpenGL;
        parameters.ResourcePaths = "CoreData;Data";
        parameters.ResourcePrefixPaths = FindResourcePrefixPath();
        parameters.WindowMaximize = false;
        parameters.WindowHeight = 720;
        parameters.WindowTitle = "RedBolt Multiple Viewports";
        parameters.WindowWidth = 1280;
    }

    protected override void Start()
    {
        Context.RegisterSubsystem(new DearImGui(Context, Engine.Parameters.DearImGuiFlags));
        _dearImGuiSubscription = Context.SubscribeToEvent<DearImGuiEvents.EndRenderingDearImGuiEvent>((ref DearImGuiEvents.EndRenderingDearImGuiEvent _) => DrawInstructions());

        // Execute base class startup
        CreateScene();
        CreateInstructions();
        SetupViewports();
        SubscribeToEvents();

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
        planeNode.SetScale(new Vector3f(100.0f, 1.0f, 100.0f));
        var planeObject = planeNode.CreateComponent<StaticModel>();
        planeObject.SetModel(cache.Get<Model>("Models/Plane.mdl"));
        planeObject.SetMaterial(cache.Get<Material>("Materials/StoneTiled.xml"));

        var zoneNode = _scene.CreateChild("Zone");
        var zone = zoneNode.CreateComponent<Zone>();
        zone.SetBoundingBox(new BoundingBox(-1000.0f, 1000.0f));
        zone.SetAmbientColor(new Color(0.15f, 0.15f, 0.15f));
        zone.SetFogColor(new Color(0.5f, 0.5f, 0.7f));
        zone.SetFogStart(100.0f);
        zone.SetFogEnd(300.0f);

        var lightNode = _scene.CreateChild("DirectionalLight");
        lightNode.SetDirection(new Vector3f(0.6f, -1.0f, 0.8f));
        var light = lightNode.CreateComponent<Light>();
        light.SetLightType(LightType.Directional);
        light.SetCastShadows(true);
        light.SetShadowBias(new BiasParameters(0.00025f, 0.5f));
        light.SetShadowCascade(new CascadeParameters(10.0f, 50.0f, 200.0f, 0.0f, 0.8f));

        for (var i = 0; i < 240; ++i)
        {
            var mushroomNode = _scene.CreateChild("Mushroom");
            mushroomNode.SetPosition(new Vector3f(MathDefs.Random(90.0f) - 45.0f, 0.0f, MathDefs.Random(90.0f) - 45.0f));
            mushroomNode.SetRotation(Quaternion.FromEulerAngles(0.0f, MathDefs.Random(360.0f), 0.0f));
            mushroomNode.SetScale(0.5f + MathDefs.Random(2.0f));
            var mushroomObject = mushroomNode.CreateComponent<StaticModel>();
            mushroomObject.SetModel(cache.Get<Model>("Models/Mushroom.mdl"));
            mushroomObject.SetMaterial(cache.Get<Material>("Materials/Mushroom.xml"));
            mushroomObject.SetCastShadows(true);
        }

        for (var i = 0; i < 20; ++i)
        {
            var size = 1.0f + MathDefs.Random(10.0f);
            var boxNode = _scene.CreateChild("Box");
            boxNode.SetPosition(new Vector3f(MathDefs.Random(80.0f) - 40.0f, size * 0.5f, MathDefs.Random(80.0f) - 40.0f));
            boxNode.SetScale(size);
            var boxObject = boxNode.CreateComponent<StaticModel>();
            boxObject.SetModel(cache.Get<Model>("Models/Box.mdl"));
            boxObject.SetMaterial(cache.Get<Material>("Materials/Stone.xml"));
            boxObject.SetCastShadows(true);
            if (size >= 3.0f)
                boxObject.SetOccluder(true);
        }

        _cameraNode = _scene.CreateChild("Camera");
        _cameraNode.CreateComponent<FreeFlyController>();
        var camera = _cameraNode.CreateComponent<Camera>();
        camera.SetFarClip(300.0f);

        _rearCameraNode = _cameraNode.CreateChild("RearCamera");
        _rearCameraNode.SetRotation(Quaternion.FromEulerAngles(0.0f, 180.0f, 0.0f));
        var rearCamera = _rearCameraNode.CreateComponent<Camera>();
        rearCamera.SetFarClip(300.0f);
        rearCamera.SetViewOverrideFlags(ViewOverride.DisableOcclusion);

        _cameraNode.SetPosition(new Vector3f(0.0f, 5.0f, 0.0f));
    }

    private void CreateInstructions()
    {
        // The rbfx Text control is replaced by the allowed Dear ImGui sample UI surface.
    }

    private void SetupViewports()
    {
        Renderer.SetNumViewports(2);
        Renderer.SetViewport(0, new Viewport(_scene!, _cameraNode!.GetComponent<Camera>()!));
        Renderer.SetViewport(1, new Viewport(
            _scene!,
            _rearCameraNode!.GetComponent<Camera>()!,
            new IntRect(Graphics.Width * 2 / 3, 32, Graphics.Width - 32, Graphics.Height / 3)));
    }

    private void SubscribeToEvents()
    {
        _postRenderSubscription = Context.SubscribeToEvent<CoreEvents.PostRenderUpdateEvent>((ref CoreEvents.PostRenderUpdateEvent _) => HandlePostRenderUpdate());
    }

    private void MoveCamera(float timeStep)
    {
        if (Context.GetSubsystem<DearImGui>()?.WantCaptureKeyboard == true)
            return;

        if (Input.GetKeyPress(Key.Space))
            _drawDebug = !_drawDebug;
    }

    private void HandlePostRenderUpdate()
    {
        if (_drawDebug)
            Renderer.DrawDebugGeometry(false);
    }

    private void DrawInstructions()
    {
        if (ImGui.Begin("Multiple Viewports\0"u8, out _, ImGuiWindowFlags.ImGuiWindowFlagsNoResize | ImGuiWindowFlags.ImGuiWindowFlagsNoSavedSettings))
        {
            ImGui.TextUnformatted("Use WASD keys and mouse/touch to move\nB to toggle bloom, F to toggle FXAA\nSpace to toggle debug geometry\0"u8);
            if (ImGui.Button(_drawDebug ? "Debug geometry: On\0"u8 : "Debug geometry: Off\0"u8))
                _drawDebug = !_drawDebug;
        }
        ImGui.End();
    }

    protected override void Update(float timeStep)
    {
        if (Input.ExitRequested || Input.GetKeyPress(Key.Escape))
        {
            Exit();
            return;
        }

        MoveCamera(timeStep);
    }

    protected override void Stop()
    {
        _postRenderSubscription?.Dispose();
        _postRenderSubscription = null;
        _dearImGuiSubscription?.Dispose();
        _dearImGuiSubscription = null;
    }

    private static string FindResourcePrefixPath()
    {
        var envRoot = Environment.GetEnvironmentVariable("REDBOLT_RBFX_BIN_DIR");
        if (TryNormalizeResourcePrefixPath(envRoot, out var normalizedEnvRoot))
            return normalizedEnvRoot;

        for (var current = new DirectoryInfo(AppContext.BaseDirectory); current is not null; current = current.Parent)
        {
            if (TryNormalizeResourcePrefixPath(Path.Combine(current.FullName, "bin"), out var localBinRoot))
                return localBinRoot;
            if (TryNormalizeResourcePrefixPath(Path.Combine(current.FullName, "tests", "RedBolt.Tests", "TestData", "rbfx"), out var testDataRoot))
                return testDataRoot;
            if (TryNormalizeResourcePrefixPath(Path.Combine(current.FullName, "rbfx", "bin"), out var repoRbfxBinRoot))
                return repoRbfxBinRoot;
            if (TryNormalizeResourcePrefixPath(Path.Combine(current.FullName, "..", "rbfx", "bin"), out var siblingRbfxBinRoot))
                return siblingRbfxBinRoot;

            if (Directory.Exists(Path.Combine(current.FullName, ".git")))
                break;
        }

        throw new InvalidOperationException("MultipleViewports sample could not locate rbfx CoreData/Data resource directories.");
    }

    private static bool TryNormalizeResourcePrefixPath(string? root, out string normalizedRoot)
    {
        normalizedRoot = string.Empty;
        if (string.IsNullOrWhiteSpace(root))
            return false;

        var candidateRoot = Path.GetFullPath(root);
        if (!Directory.Exists(Path.Combine(candidateRoot, "CoreData")) ||
            !Directory.Exists(Path.Combine(candidateRoot, "Data")))
            return false;

        normalizedRoot = candidateRoot;
        return true;
    }
}
