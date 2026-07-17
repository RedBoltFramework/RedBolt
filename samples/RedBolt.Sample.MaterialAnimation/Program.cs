using System;
using System.IO;
using RedBolt;
using RedBolt.Bindings.DearImGui;

try
{
    return new MaterialAnimationApplication().Run();
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex);
    return 1;
}

/// <summary>
/// Material animation example.
/// This sample is base on StaticScene, and it demonstrates:
///     - Usage of material shader animation for mush room material
/// </summary>
sealed class MaterialAnimationApplication : Application
{
    private Scene? _scene;
    private Node? _cameraNode;
    private ContextEventSubscription? _dearImGuiSubscription;

    protected override void Setup(EngineParameters parameters)
    {
        parameters.ApplicationName = "RedBolt.Sample.MaterialAnimation";
        parameters.Borderless = false;
        parameters.FullScreen = false;
        parameters.ResourcePaths = "CoreData;Data";
        parameters.ResourcePrefixPaths = FindResourcePrefixPath();
        parameters.WindowMaximize = false;
        parameters.WindowHeight = 720;
        parameters.WindowTitle = "RedBolt Material Animation";
        parameters.WindowWidth = 1280;
    }

    /// <summary>Setup after engine initialization and before running the main loop.</summary>
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

    /// <summary>Construct the scene content.</summary>
    private void CreateScene()
    {
        var cache = Resources;
        _scene = new Scene();

        // Create the Octree component to the scene. This is required before adding any drawable components, or else nothing will
        // show up. The default octree volume will be from (-1000, -1000, -1000) to (1000, 1000, 1000) in world coordinates; it
        // is also legal to place objects outside the volume but their visibility can then not be checked in a hierarchically
        // optimizing manner
        _scene.CreateComponent<Octree>();

        // Create a child scene node (at world origin) and a StaticModel component into it. Set the StaticModel to show a simple
        // plane mesh with a "stone" material. Note that naming the scene nodes is optional. Scale the scene node larger
        // (100 x 100 world units)
        var planeNode = _scene.CreateChild("Plane");
        planeNode.SetScale(new Vector3f(100.0f, 1.0f, 100.0f));
        var planeObject = planeNode.CreateComponent<StaticModel>();
        planeObject.SetModel(cache.Get<Model>("Models/Plane.mdl"));
        planeObject.SetMaterial(cache.Get<Material>("Materials/StoneTiled.xml"));

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
        var mushroomMaterial = cache.Get<Material>("Materials/Mushroom.xml");
        // Apply shader parameter animation to material
        var specColorAnimation = new ValueAnimation(string.Empty);
        specColorAnimation.SetKeyFrame(0.0f, new Color(0.1f, 0.1f, 0.1f, 16.0f));
        specColorAnimation.SetKeyFrame(1.0f, new Color(1.0f, 0.0f, 0.0f, 2.0f));
        specColorAnimation.SetKeyFrame(2.0f, new Color(1.0f, 1.0f, 0.0f, 2.0f));
        specColorAnimation.SetKeyFrame(3.0f, new Color(0.1f, 0.1f, 0.1f, 16.0f));
        // Optionally associate material with scene to make sure shader parameter animation respects scene time scale
        mushroomMaterial.SetScene(_scene);
        mushroomMaterial.SetShaderParameterAnimation("MatSpecColor", specColorAnimation);

        const uint numObjects = 200;
        for (var i = 0u; i < numObjects; ++i)
        {
            var mushroomNode = _scene.CreateChild("Mushroom");
            mushroomNode.SetPosition(new Vector3f(MathDefs.Random(90.0f) - 45.0f, 0.0f, MathDefs.Random(90.0f) - 45.0f));
            mushroomNode.SetRotation(Quaternion.FromEulerAngles(0.0f, MathDefs.Random(360.0f), 0.0f));
            mushroomNode.SetScale(0.5f + MathDefs.Random(2.0f));
            var mushroomObject = mushroomNode.CreateComponent<StaticModel>();
            mushroomObject.SetModel(cache.Get<Model>("Models/Mushroom.mdl"));
            mushroomObject.SetMaterial(mushroomMaterial);
        }

        // Create a scene node for the camera, which we will move around
        // The camera will use default settings (1000 far clip distance, 45 degrees FOV, set aspect ratio automatically)
        _cameraNode = _scene.CreateChild("Camera");
        _cameraNode.CreateComponent<FreeFlyController>();
        _cameraNode.CreateComponent<Camera>();

        // Set an initial position for the camera scene node above the plane
        _cameraNode.SetPosition(new Vector3f(0.0f, 5.0f, 0.0f));
    }

    /// <summary>Construct an instruction text to the UI.</summary>
    private static void CreateInstructions()
    {
    }

    /// <summary>Set up a viewport for displaying the scene.</summary>
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
            Exit();
    }

    protected override void Stop()
    {
        _dearImGuiSubscription?.Dispose();
        _dearImGuiSubscription = null;
    }

    private static void DrawInstructions()
    {
        if (ImGui.Begin("Material Animation\0"u8, out _, ImGuiWindowFlags.ImGuiWindowFlagsNoResize | ImGuiWindowFlags.ImGuiWindowFlagsNoSavedSettings))
            ImGui.TextUnformatted("Use WASD keys and mouse/touch to move\0"u8);
        ImGui.End();
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
        throw new InvalidOperationException("MaterialAnimation sample could not locate rbfx CoreData/Data resource directories.");
    }

    private static bool TryNormalizeResourcePrefixPath(string? root, out string normalizedRoot)
    {
        normalizedRoot = string.Empty;
        if (string.IsNullOrWhiteSpace(root))
            return false;
        var candidateRoot = Path.GetFullPath(root);
        if (!Directory.Exists(Path.Combine(candidateRoot, "CoreData")) || !Directory.Exists(Path.Combine(candidateRoot, "Data")))
            return false;
        normalizedRoot = candidateRoot;
        return true;
    }
}
