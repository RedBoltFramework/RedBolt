using RedBolt;
using RedBolt.Bindings.DearImGui;

try
{
    var exitCode = new RenderToTextureApplication().Run();
    if (exitCode == 0)
        Console.WriteLine("RedBolt RenderToTexture sample finished.");
    return exitCode;
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex);
    return 1;
}

sealed class RenderToTextureApplication : Application
{
    private readonly CString _instructionLine = new();
    private ContextEventSubscription? _dearImGuiSubscription;
    private Scene? _scene;
    private Scene? _renderToTextureScene;
    private Node? _cameraNode;

    protected override void Setup(EngineParameters parameters)
    {
        parameters.ApplicationName = "RedBolt.Sample.RenderToTexture";
        parameters.Borderless = false;
        parameters.FullScreen = false;
        parameters.WindowMaximize = false;
        parameters.ResourcePaths = "CoreData;Data";
        parameters.ResourcePrefixPaths = ".;..";
        parameters.WindowHeight = 720;
        parameters.WindowTitle = "RedBolt Render To Texture";
        parameters.WindowWidth = 1280;
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

    protected override void Update(float timeStep)
    {
        if (Input.ExitRequested || Input.GetKeyPress(Key.Escape))
            Exit();
    }

    protected override void Stop()
    {
        _dearImGuiSubscription?.Dispose();
        _dearImGuiSubscription = null;
        _instructionLine.Dispose();
    }

    private void CreateScene()
    {
        _renderToTextureScene = new Scene();
        _renderToTextureScene.CreateComponent<Octree>();
        var renderZone = _renderToTextureScene.CreateChild("Zone").CreateComponent<Zone>();
        renderZone.SetBoundingBox(new BoundingBox(-1000.0f, 1000.0f));
        renderZone.SetAmbientColor(new Color(0.05f, 0.1f, 0.15f));
        renderZone.SetFogColor(new Color(0.1f, 0.2f, 0.3f));
        renderZone.SetFogStart(10.0f);
        renderZone.SetFogEnd(100.0f);

        for (var i = 0; i < 2000; ++i)
        {
            var boxNode = _renderToTextureScene.CreateChild("Box");
            boxNode.SetPosition(new Vector3f(MathDefs.Random(200.0f) - 100.0f, MathDefs.Random(100.0f), MathDefs.Random(200.0f) - 100.0f));
            boxNode.SetRotation(new Quaternion(MathDefs.Random(360.0f), MathDefs.Random(360.0f), MathDefs.Random(360.0f)));
            var box = boxNode.CreateComponent<StaticModel>();
            box.SetModel(Resources.Get<Model>("Models/Box.mdl"));
            box.SetMaterial(Resources.Get<Material>("Materials/Stone.xml"));
            boxNode.CreateComponent<Rotator>().SetRotationSpeed(new Vector3f(10.0f, 20.0f, 30.0f));
        }

        var renderCameraNode = _renderToTextureScene.CreateChild("Camera");
        var renderCamera = renderCameraNode.CreateComponent<Camera>();
        renderCamera.SetFarClip(100.0f);
        var renderLight = renderCameraNode.CreateComponent<Light>();
        renderLight.SetLightType(LightType.Point);
        renderLight.SetRange(30.0f);

        _scene = new Scene();
        _scene.CreateComponent<Octree>();
        var zone = _scene.CreateChild("Zone").CreateComponent<Zone>();
        zone.SetBoundingBox(new BoundingBox(-1000.0f, 1000.0f));
        zone.SetAmbientColor(new Color(0.1f, 0.1f, 0.1f));
        zone.SetFogStart(100.0f);
        zone.SetFogEnd(300.0f);

        var lightNode = _scene.CreateChild("DirectionalLight");
        lightNode.SetDirection(new Vector3f(0.5f, -1.0f, 0.5f));
        var light = lightNode.CreateComponent<Light>();
        light.SetLightType(LightType.Directional);
        light.SetColor(new Color(0.2f, 0.2f, 0.2f));
        light.SetSpecularIntensity(1.0f);

        for (var y = -5; y <= 5; ++y)
        for (var x = -5; x <= 5; ++x)
        {
            var floorNode = _scene.CreateChild("FloorTile");
            floorNode.SetPosition(new Vector3f(x * 20.5f, -0.5f, y * 20.5f));
            floorNode.SetScale(new Vector3f(20.0f, 1.0f, 20.0f));
            var floor = floorNode.CreateComponent<StaticModel>();
            floor.SetModel(Resources.Get<Model>("Models/Box.mdl"));
            floor.SetMaterial(Resources.Get<Material>("Materials/Stone.xml"));
        }

        var screenFrameNode = _scene.CreateChild("ScreenBox");
        screenFrameNode.SetPosition(new Vector3f(0.0f, 10.0f, 0.0f));
        screenFrameNode.SetScale(new Vector3f(21.0f, 16.0f, 0.5f));
        var screenFrame = screenFrameNode.CreateComponent<StaticModel>();
        screenFrame.SetModel(Resources.Get<Model>("Models/Box.mdl"));
        screenFrame.SetMaterial(Resources.Get<Material>("Materials/Stone.xml"));

        var screenNode = _scene.CreateChild("Screen");
        screenNode.SetPosition(new Vector3f(0.0f, 10.0f, -0.27f));
        screenNode.SetRotation(new Quaternion(-90.0f, 0.0f, 0.0f));
        screenNode.SetScale(new Vector3f(20.0f, 0.0f, 15.0f));
        var screen = screenNode.CreateComponent<StaticModel>();
        screen.SetModel(Resources.Get<Model>("Models/Plane.mdl"));

        var renderTexture = new Texture2D("");
        renderTexture.SetSize(1024, 768, TextureFormat.RGBA8, TextureFlag.BindRenderTarget, 4);
        renderTexture.SetFilterMode(TextureFilterMode.Bilinear);
        var renderMaterial = new Material("");
        renderMaterial.SetTechnique(0, Resources.Get<Technique>("Techniques/DiffUnlit.xml"));
        renderMaterial.SetTexture("Albedo", renderTexture);
        renderMaterial.SetDepthBias(new BiasParameters(-0.001f, 0.0f));
        screen.SetMaterial(renderMaterial);

        var renderSurface = renderTexture.GetRenderSurface();
        var renderToTextureViewport = new Viewport(_renderToTextureScene, renderCamera);
        renderSurface?.SetViewport(0, renderToTextureViewport);

        _cameraNode = _scene.CreateChild("Camera");
        _cameraNode.CreateComponent<FreeFlyController>();
        var camera = _cameraNode.CreateComponent<Camera>();
        camera.SetFarClip(300.0f);
        _cameraNode.SetPosition(new Vector3f(0.0f, 7.0f, -30.0f));
    }

    private void DrawInstructions()
    {
        _instructionLine.FormatInPlace("Use WASD keys and mouse/touch to move");
        if (ImGui.Begin("Render To Texture\0"u8, out _, ImGuiWindowFlags.ImGuiWindowFlagsNoResize | ImGuiWindowFlags.ImGuiWindowFlagsNoSavedSettings))
            ImGui.TextUnformatted(_instructionLine.Bytes);
        ImGui.End();
    }
}
