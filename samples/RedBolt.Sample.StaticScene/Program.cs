using System;
using RedBolt;
using RedBolt.Bindings.DearImGui;

try
{
    var exitCode = new StaticSceneApplication().Run();
    if (exitCode == 0)
        Console.WriteLine("RedBolt StaticScene sample finished.");
    return exitCode;
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex);
    return 1;
}

sealed class StaticSceneApplication : Application
{
    private Scene? _scene;
    private Node? _cameraNode;
    private readonly Random _random = new();
    private readonly CString _instructionLine = new();
    private ContextEventSubscription? _dearImGuiSubscription;

    protected override void Setup(EngineParameters parameters)
    {
        parameters.ApplicationName = "RedBolt.Sample.StaticScene";
        parameters.Borderless = false;
        parameters.FullScreen = false;
        parameters.WindowMaximize = false;
        parameters.ResourcePaths = "CoreData;Data";
        parameters.ResourcePrefixPaths = ".;..";
        parameters.WindowMaximize = false;
        parameters.WindowHeight = 720;
        parameters.WindowTitle = "RedBolt Static Scene";
        parameters.WindowWidth = 1280;
    }

    protected override void Start()
    {
        Context.RegisterSubsystem(new DearImGui(Context, Engine.Parameters.DearImGuiFlags));
        _dearImGuiSubscription = Context.SubscribeToEvent<DearImGuiEvents.EndRenderingDearImGuiEvent>((ref DearImGuiEvents.EndRenderingDearImGuiEvent _) => DrawInstructions());

        // Execute base class startup
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

    private void CreateScene()
    {
        // Create the Octree component to the scene. This is required before adding any drawable components, or else nothing will
        // show up. The default octree volume will be from (-1000, -1000, -1000) to (1000, 1000, 1000) in world coordinates; it
        // is also legal to place objects outside the volume but their visibility can then not be checked in a hierarchically
        // optimizing manner
        _scene = new Scene();
        _scene.CreateComponent<Octree>();

        // Create a child scene node (at world origin) and a StaticModel component into it. Set the StaticModel to show a simple
        // plane mesh with a "stone" material. Note that naming the scene nodes is optional. Scale the scene node larger
        // (100 x 100 world units)
        var planeNode = _scene.CreateChild("Plane");
        planeNode.SetScale(new Vector3f(100.0f, 1.0f, 100.0f));
        var planeObject = planeNode.CreateComponent<StaticModel>();
        planeObject.SetModel(Resources.Get<Model>("Models/Plane.mdl"));
        planeObject.SetMaterial(Resources.Get<Material>("Materials/StoneTiled.xml"));

        // Create a directional light to the world so that we can see something. The light scene node's orientation controls the
        // light direction; we will use the SetDirection() function which calculates the orientation from a forward direction vector.
        // The light will use default settings (white light, no shadows)
        var lightNode = _scene.CreateChild("DirectionalLight");
        lightNode.SetDirection(new Vector3f(0.6f, -1.0f, 0.8f)); // The direction vector does not need to be normalized
        var light = lightNode.CreateComponent<Light>();
        light.SetLightType(LightType.Directional);

        // Create more StaticModel objects to the scene, randomly positioned, rotated and scaled. For rotation, we construct a
        // quaternion from Euler angles where the Y angle (rotation about the Y axis) is randomized. The mushroom model contains
        // LOD levels, so the StaticModel component will automatically select the LOD level according to the view distance (you'll
        // see the model get simpler as it moves further away). Finally, rendering a large number of the same object with the
        // same material allows instancing to be used, if the GPU supports it. This reduces the amount of CPU work in rendering the
        // scene.
        const int numObjects = 200;
        for (var i = 0; i < numObjects; ++i)
        {
            var mushroomNode = _scene.CreateChild("Mushroom");
            mushroomNode.SetPosition(new Vector3f(Random(90.0f) - 45.0f, 0.0f, Random(90.0f) - 45.0f));
            mushroomNode.SetRotation(Quaternion.FromEulerAngles(0.0f, Random(360.0f), 0.0f));
            mushroomNode.SetScale(0.5f + Random(2.0f));
            var mushroomObject = mushroomNode.CreateComponent<StaticModel>();
            mushroomObject.SetModel(Resources.Get<Model>("Models/Mushroom.mdl"));
            mushroomObject.SetMaterial(Resources.Get<Material>("Materials/Mushroom.xml"));
        }

        // Create a scene node for the camera, which we will move around
        // The camera will use default settings (1000 far clip distance, 45 degrees FOV, set aspect ratio automatically)
        _cameraNode = _scene.CreateChild("Camera");
        _cameraNode.CreateComponent<Camera>();
        var controller = _cameraNode.CreateComponent<FreeFlyController>();

        // Set an initial position for the camera scene node above the plane
        _cameraNode.SetPosition(new Vector3f(0.0f, 5.0f, 0.0f));
    }

    private void CreateInstructions()
    {
        // The rbfx Text control is replaced by the allowed Dear ImGui sample UI surface.
    }

    private void SetupViewport()
    {
        // Set up a viewport to the Renderer subsystem so that the 3D scene can be seen. We need to define the scene and the camera
        // at minimum. Additionally we could configure the viewport screen size and the rendering path (eg. forward / deferred) to
        // use, but now we just use full screen and default render path configured in the engine command line options
        Renderer.SetViewport(0, new Viewport(_scene!, _cameraNode!.GetComponent<Camera>()!));
    }

    protected override void Update(float timeStep)
    {
        if (Input.ExitRequested || Input.GetKeyPress(Key.Escape))
        {
            Exit();
            return;
        }

    }

    protected override void Stop()
    {
        _dearImGuiSubscription?.Dispose();
        _dearImGuiSubscription = null;

        _instructionLine.Dispose();
    }

    private void DrawInstructions()
    {
        _instructionLine.FormatInPlace("Use WASD keys and mouse/touch to move");
        if (ImGui.Begin("Static Scene\0"u8, out _, ImGuiWindowFlags.ImGuiWindowFlagsNoResize | ImGuiWindowFlags.ImGuiWindowFlagsNoSavedSettings))
            ImGui.TextUnformatted(_instructionLine.Bytes);
        ImGui.End();
    }
}
