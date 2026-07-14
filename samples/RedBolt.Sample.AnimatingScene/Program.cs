using System;
using System.IO;
using RedBolt;
using RedBolt.Bindings.DearImGui;

try
{
    var exitCode = new AnimatingSceneApplication().Run();
    if (exitCode == 0)
        Console.WriteLine("RedBolt AnimatingScene sample finished.");
    return exitCode;
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex);
    return 1;
}

sealed class AnimatingSceneApplication : Application
{
    private readonly CString _instructionLine = new();
    private ContextEventSubscription? _dearImGuiSubscription;

    protected override void Setup(EngineParameters parameters)
    {
        parameters.ApplicationName = "RedBolt.Sample.AnimatingScene";
        parameters.Borderless = false;
        parameters.GraphicsBackend = GraphicsBackend.OpenGL;
        parameters.ResourcePaths = "CoreData;Data";
        parameters.ResourcePrefixPaths = FindResourcePrefixPath();
        parameters.WindowMaximize = false;
        parameters.WindowHeight = 720;
        parameters.WindowTitle = "RedBolt Animating Scene";
        parameters.WindowWidth = 1280;
    }

    protected override void Start()
    {
        // Execute base class startup
        Context.RegisterSubsystem(new DearImGui(Context, Engine.Parameters.DearImGuiFlags));
        _dearImGuiSubscription = Context.SubscribeToEvent<DearImGuiEvents.EndRenderingDearImGuiEvent>((ref DearImGuiEvents.EndRenderingDearImGuiEvent _) => DrawInstructions());

        // Create the scene content
        var scene = new Scene();
        scene.CreateComponent<RenderPipeline>();
        scene.CreateComponent<Octree>();

        // Create a Zone component into a child scene node. The Zone controls ambient lighting and fog settings. Like the Octree,
        // it also defines its volume with a bounding box, but can be rotated (so it does not need to be aligned to the world X, Y
        // and Z axes.) Drawable objects "pick up" the zone they belong to and use it when rendering; several zones can exist
        var zoneNode = scene.CreateChild("Zone");
        var zone = zoneNode.CreateComponent<Zone>();
        // Set same volume as the Octree, set a close bluish fog and some ambient light
        zone.SetBoundingBox(new BoundingBox(-1000.0f, 1000.0f));
        zone.SetAmbientColor(new Color(0.05f, 0.1f, 0.15f));
        zone.SetFogColor(new Color(0.1f, 0.2f, 0.3f));
        zone.SetFogStart(10.0f);
        zone.SetFogEnd(100.0f);

        // Create randomly positioned and oriented box StaticModels in the scene
        for (var i = 0; i < 2000; ++i)
        {
            var boxNode = scene.CreateChild("Box");
            boxNode.SetPosition(new Vector3f(MathDefs.Random(200.0f) - 100.0f, MathDefs.Random(200.0f) - 100.0f, MathDefs.Random(200.0f) - 100.0f));
            // Orient using random pitch, yaw and roll Euler angles
            boxNode.SetRotation(Quaternion.FromEulerAngles(MathDefs.Random(360.0f), MathDefs.Random(360.0f), MathDefs.Random(360.0f)));
            var boxObject = boxNode.CreateComponent<StaticModel>();
            boxObject.SetModel(Resources.Get<Model>("Models/Box.mdl"));
            boxObject.SetMaterial(Resources.Get<Material>("Materials/Stone.xml"));
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
        var cameraNode = scene.CreateChild("Camera");
        cameraNode.CreateComponent<FreeFlyController>().SetInput(Input);
        var camera = cameraNode.CreateComponent<Camera>();
        camera.SetFarClip(100.0f);

        // Create a point light to the camera scene node
        var light = cameraNode.CreateComponent<Light>();
        light.SetLightType(LightType.Point);
        light.SetRange(30.0f);

        // Create the UI content
        Renderer.SetViewport(0, new Viewport(scene, camera));
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

    private void DrawInstructions()
    {
        _instructionLine.FormatInPlace("Use WASD keys and mouse/touch to move");
        if (ImGui.Begin("Animating Scene\0"u8, out _, ImGuiWindowFlags.ImGuiWindowFlagsNoResize | ImGuiWindowFlags.ImGuiWindowFlagsNoSavedSettings))
            ImGui.TextUnformatted(_instructionLine.Bytes);
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

        throw new InvalidOperationException("AnimatingScene sample could not locate rbfx CoreData/Data resource directories.");
    }

    private static bool TryNormalizeResourcePrefixPath(string? root, out string normalizedRoot)
    {
        normalizedRoot = string.Empty;
        if (string.IsNullOrWhiteSpace(root))
            return false;

        var candidateRoot = Path.GetFullPath(root);
        if (!Directory.Exists(Path.Combine(candidateRoot, "CoreData")) ||
            !Directory.Exists(Path.Combine(candidateRoot, "Data")))
        {
            return false;
        }

        normalizedRoot = candidateRoot;
        return true;
    }

}
