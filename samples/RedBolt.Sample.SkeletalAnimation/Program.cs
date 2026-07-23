using System;
using RedBolt;
using RedBolt.Bindings.DearImGui;

try { return new SkeletalAnimationApplication().Run(); }
catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }

/// <summary>
/// Skeletal animation example.
/// This sample demonstrates:
/// - Populating a 3D scene with skeletally animated AnimatedModel components;
/// - Moving the animated models and advancing their animation using a custom component
/// - Enabling a cascaded shadow map on a directional light, which allows high-quality shadows
/// over a large area (typically used in outdoor scenes for shadows cast by sunlight)
/// - Displaying renderer debug geometry
/// </summary>
sealed class SkeletalAnimationApplication : Application
{
    private Scene? _scene;
    private Node? _cameraNode;
    private bool _drawDebug;

    public SkeletalAnimationApplication()
    {
        if (Context.Instance.GetReflection<Mover3D>() is null)
            Context.Instance.AddFactoryReflection<Mover3D>();
    }

    protected override void Setup(EngineParameters parameters)
    {
        parameters.ApplicationName = "RedBolt.Sample.SkeletalAnimation";
        parameters.Borderless = false; parameters.FullScreen = false;
        parameters.ResourcePaths = "CoreData;Data";
        parameters.ResourcePrefixPaths = ".;..";
        parameters.WindowMaximize = false; parameters.WindowWidth = 1280; parameters.WindowHeight = 720;
        parameters.WindowTitle = "RedBolt Skeletal Animation";
    }

    /// <summary>Setup after engine initialization and before running the main loop.</summary>
    protected override void Start()
    {
        // Execute base class startup

        // Create the scene content
        CreateScene();

        // Create the UI content
        CreateInstructions();

        // Setup the viewport for displaying the scene
        SetupViewport();

        // Hook up to the frame update and render post-update events
        SubscribeToEvents();

        // Set the mouse mode to use in the sample
        Input.SetMouseMode(MouseMode.Free);
        Input.SetMouseVisible(true);
    }

    /// <summary>Construct the scene content.</summary>
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

        const int numModels = 30;
        const float modelMoveSpeed = 2.0f;
        const float modelRotateSpeed = 100.0f;
        var bounds = new BoundingBox(new Vector3f(-20.0f, 0.0f, -20.0f), new Vector3f(20.0f, 0.0f, 20.0f));
        for (var i = 0; i < numModels; ++i)
        {
            var modelNode = _scene.CreateChild("Jill");
            modelNode.SetPosition(new Vector3f(MathDefs.Random(40.0f) - 20.0f, 0.0f, MathDefs.Random(40.0f) - 20.0f));
            modelNode.SetRotation(new Quaternion(0.0f, MathDefs.Random(360.0f), 0.0f));
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

    /// <summary>Construct an instruction text to the UI.</summary>
    private void CreateInstructions()
    {
        Context.RegisterSubsystem(new DearImGui((ImGuiConfigFlags)Engine.Parameters.DearImGuiFlags));
        SubscribeToEvent<DearImGuiEvents.EndRenderingDearImGuiEvent>((ref DearImGuiEvents.EndRenderingDearImGuiEvent _) => DrawInstructions());
    }

    /// <summary>Set up a viewport for displaying the scene.</summary>
    private void SetupViewport()
    {
        Renderer.SetViewport(0, new Viewport(_scene!, _cameraNode!.GetComponent<Camera>()!));
    }

    /// <summary>Subscribe to application-wide logic update and post-render update events.</summary>
    private void SubscribeToEvents()
    {
        SubscribeToEvent<CoreEvents.PostRenderUpdateEvent>((ref CoreEvents.PostRenderUpdateEvent _) => HandlePostRenderUpdate());
    }

    /// <summary>Handle the logic update event.</summary>
    protected override void Update(float timeStep)
    {
        // Do not move if the UI has a focused element (the console)
        if (ImGui.GetIO().WantCaptureKeyboard)
            return;

        if (Input.ExitRequested || Input.GetKeyPress(Key.Escape)) Exit();
        if (Input.GetKeyPress(Key.Space)) _drawDebug = !_drawDebug;
    }

    /// <summary>Handle the post-render update event.</summary>
    private void HandlePostRenderUpdate()
    {
        // If draw debug mode is enabled, draw viewport debug geometry, which will show eg. drawable bounding boxes and skeleton
        // bones. Note that debug geometry has to be separately requested each frame. Disable depth test so that we can see the
        // bones properly
        if (_drawDebug)
            Renderer.DrawDebugGeometry(false);
    }

    protected override void Stop()
    {
    }

    private void DrawInstructions()
    {
        var windowOpen = true;
        if (ImGui.Begin("Skeletal Animation\0"u8, ref windowOpen, ImGuiWindowFlags.ImGuiWindowFlagsNoSavedSettings))
        {
            ImGui.TextUnformatted("Use WASD keys and right mouse button to move\0"u8);
            ImGui.TextUnformatted("Space to toggle debug geometry\0"u8);
            ImGui.TextUnformatted(_drawDebug ? "Debug geometry requested\0"u8 : "Debug geometry disabled\0"u8);
        }
        ImGui.End();
    }
}
