using System;
using RedBolt;
using RedBolt.Bindings.DearImGui;

try
{
    using var application = new AnimatingScene();
    var exitCode = application.Run();
    if (exitCode == 0)
        Console.WriteLine("RedBolt AnimatingScene sample finished.");
    return exitCode;
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex);
    return 1;
}

/// <summary>
/// Animating 3D scene example.
/// This sample demonstrates:
///     - Creating a 3D scene and using a custom component to animate the objects
///     - Controlling scene ambience with the Zone component
///     - Attaching a light to an object (the camera)
/// </summary>
public class AnimatingScene : Application
{
    /// <summary>
    /// Construct.
    /// </summary>
    public AnimatingScene()
    {
        if (Context.Instance.GetReflection<Rotator>() is null)
            Context.Instance.AddFactoryReflection<Rotator>();
    }

    /// <summary>
    /// Setup after engine initialization and before running the main loop.
    /// </summary>
    protected override void Start()
    {
        // Execute base class startup
        base.Start();
        Context.RegisterSubsystem(new DearImGui((ImGuiConfigFlags)Engine.Parameters.DearImGuiFlags));

        // Create the scene content
        CreateScene();

        // Create the UI content
        CreateInstructions();

        // Setup the viewport for displaying the scene
        SetupViewport();
        // Set the mouse mode to use in the sample
        Input.SetMouseMode(MouseMode.Free);
        Input.SetMouseVisible(true);
    }

    /// <summary>
    /// Construct the scene content.
    /// </summary>
    private void CreateScene()
    {
        var cache = Resources;

        _scene = new Scene();

        // Create the Octree component to the scene so that drawable objects can be rendered. Use default volume
        // (-1000, -1000, -1000) to (1000, 1000, 1000)
        _scene.CreateComponent<Octree>();

        // Create a Zone component into a child scene node. The Zone controls ambient lighting and fog settings. Like the Octree,
        // it also defines its volume with a bounding box, but can be rotated (so it does not need to be aligned to the world X, Y
        // and Z axes.) Drawable objects "pick up" the zone they belong to and use it when rendering; several zones can exist
        var zoneNode = _scene.CreateChild("Zone");
        var zone = zoneNode.CreateComponent<Zone>();
        // Set same volume as the Octree, set a close bluish fog and some ambient light
        zone.SetBoundingBox(new BoundingBox(-1000.0f, 1000.0f));
        zone.SetAmbientColor(new Color(0.05f, 0.1f, 0.15f));
        zone.SetFogColor(new Color(0.1f, 0.2f, 0.3f));
        zone.SetFogStart(10.0f);
        zone.SetFogEnd(100.0f);

        // Create randomly positioned and oriented box StaticModels in the scene
        const int NumObjects = 2000;
        for (var i = 0; i < NumObjects; ++i)
        {
            var boxNode = _scene.CreateChild("Box");
            boxNode.SetPosition(new Vector3f(MathDefs.Random(200.0f) - 100.0f, MathDefs.Random(200.0f) - 100.0f, MathDefs.Random(200.0f) - 100.0f));
            // Orient using random pitch, yaw and roll Euler angles
            boxNode.SetRotation(new Quaternion(MathDefs.Random(360.0f), MathDefs.Random(360.0f), MathDefs.Random(360.0f)));
            var boxObject = boxNode.CreateComponent<StaticModel>();
            boxObject.SetModel(cache.Get<Model>("Models/Box.mdl"));
            boxObject.SetMaterial(cache.Get<Material>("Materials/Stone.xml"));

            // Add our custom Rotator component which will rotate the scene node each frame, when the scene sends its update event.
            // The Rotator component derives from the base class LogicComponent, which has convenience functionality to subscribe
            // to the various update events, and forward them to virtual functions that can be implemented by subclasses. This way
            // writing logic/update components in C++ becomes similar to scripting.
            // Now we simply set same rotation speed for all objects
            var rotator = boxNode.CreateComponent<Rotator>();
            rotator.SetRotationSpeed(new Vector3f(10.0f, 20.0f, 30.0f));
        }

        // Create the camera. Let the starting position be at the world origin. As the fog limits maximum visible distance, we can
        // bring the far clip plane closer for more effective culling of distant objects
        _cameraNode = _scene.CreateChild("Camera");
        _cameraNode.CreateComponent<FreeFlyController>();
        var camera = _cameraNode.CreateComponent<Camera>();
        camera.SetFarClip(100.0f);

        // Create a point light to the camera scene node
        var light = _cameraNode.CreateComponent<Light>();
        light.SetLightType(LightType.Point);
        light.SetRange(30.0f);
    }

    /// <summary>
    /// Construct an instruction text to the UI.
    /// </summary>
    private void CreateInstructions()
    {
        _dearImGuiSubscription = Context.SubscribeToEvent<DearImGuiEvents.EndRenderingDearImGuiEvent>((ref DearImGuiEvents.EndRenderingDearImGuiEvent _) => DrawInstructions());
    }

    /// <summary>
    /// Set up a viewport for displaying the scene.
    /// </summary>
    private void SetupViewport()
    {
        var renderer = Renderer;

        // Set up a viewport to the Renderer subsystem so that the 3D scene can be seen
        _viewport = new Viewport(_scene!, _cameraNode!.GetComponent<Camera>());
        renderer.SetViewport(0, _viewport);
    }

    protected override void Setup(EngineParameters parameters)
    {
        parameters.ApplicationName = "RedBolt.Sample.AnimatingScene";
        parameters.Borderless = false;
        parameters.FullScreen = false;
        parameters.WindowMaximize = false;
        parameters.ResourcePaths = "CoreData;Data";
        parameters.ResourcePrefixPaths = ".;..";
        parameters.WindowHeight = 720;
        parameters.WindowTitle = "RedBolt Animating Scene";
        parameters.WindowWidth = 1280;
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
        Renderer.SetNumViewports(0);
        base.Stop();
    }

    public override void Dispose()
    {
        _dearImGuiSubscription?.Dispose();
        _dearImGuiSubscription = null;
        _instructionLine.Dispose();
        if (_viewport is not null)
            Renderer.SetNumViewports(0);
        _cameraNode = null;
        _scene?.Dispose();
        _scene = null;
        _viewport?.Dispose();
        _viewport = null;
        base.Dispose();
    }

    private void DrawInstructions()
    {
        _instructionLine.FormatInPlace("Use WASD keys and mouse/touch to move");
        var windowOpen = true;
        if (ImGui.Begin("Animating Scene\0"u8, ref windowOpen, ImGuiWindowFlags.ImGuiWindowFlagsNoResize | ImGuiWindowFlags.ImGuiWindowFlagsNoSavedSettings))
            ImGui.TextUnformatted(_instructionLine.Bytes);
        ImGui.End();
    }

    private Viewport? _viewport;
    private Scene? _scene;
    private Node? _cameraNode;
    private readonly CString _instructionLine = new();
    private ContextEventSubscription? _dearImGuiSubscription;
}
