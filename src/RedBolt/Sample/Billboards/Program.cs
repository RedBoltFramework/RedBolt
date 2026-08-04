namespace RedBolt.Sample.Billboards;

using System;
using System.Collections.Generic;

using RedBolt;
using RedBolt.Bindings.DearImGui;
using RedBolt.DearImGui;
using RedBolt.Core;
using RedBolt.Engine;
using RedBolt.Graphics;
using RedBolt.Input;
using RedBolt.Math;
using RedBolt.Platform;
using RedBolt.Scene;

internal static class BillboardsProgram
{
    private static int Main()
    {
        try
        {
            var exitCode = new BillboardsApplication().Run();
            if (exitCode == 0)
                Console.WriteLine("RedBolt Billboards sample finished.");
            return exitCode;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }
}

/// <summary>
/// Billboard example.
/// This sample demonstrates:
///     - Populating a 3D scene with billboard sets and several shadow casting spotlights
///     - Parenting scene nodes to allow more intuitive creation of groups of objects
///     - Examining rendering performance with a somewhat large object and light count
/// </summary>
sealed class BillboardsApplication : Application
{
    private Scene? _scene;
    private Node? _cameraNode;
    private readonly List<Node> _lightNodes = [];
    private readonly List<Node> _billboardNodes = [];

    /// <summary>Flag for drawing debug geometry.</summary>
    private bool _drawDebug;

    protected override void Setup(EngineParameters parameters)
    {
        parameters.ApplicationName = "RedBolt.Sample.Billboards";
        parameters.Borderless = false;
        parameters.FullScreen = false;
        parameters.ResourcePaths = "CoreData;Data";
        parameters.ResourcePrefixPaths = ".;..";
        parameters.WindowMaximize = false;
        if (ProcessUtils.GetPlatform() != PlatformId.Web)
        {
            parameters.WindowHeight = 720;
            parameters.WindowWidth = 1280;
        }
        parameters.WindowTitle = "RedBolt Billboards";
    }

    /// <summary>Setup after engine initialization and before running the main loop.</summary>
    protected override void Start()
    {
        Context.RegisterSubsystem(new DearImGui((ImGuiConfigFlags)Engine.Parameters.DearImGuiFlags));
        SubscribeToEvent<DearImGuiEvents.EndRenderingDearImGuiEvent>((ref DearImGuiEvents.EndRenderingDearImGuiEvent _) => DrawInstructions());

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

        // Create octree, use default volume (-1000, -1000, -1000) to (1000, 1000, 1000)
        // Also create a DebugRenderer component so that we can draw debug geometry
        _scene.CreateComponent<Octree>();
        _scene.CreateComponent<DebugRenderer>();

        // Create a Zone component for ambient lighting & fog control
        var zoneNode = _scene.CreateChild("Zone");
        var zone = zoneNode.CreateComponent<Zone>();
        zone.SetBoundingBox(new BoundingBox(-1000.0f, 1000.0f));
        zone.SetAmbientColor(new Color(0.1f, 0.1f, 0.1f));
        zone.SetFogStart(100.0f);
        zone.SetFogEnd(300.0f);

        // Create a directional light without shadows
        var lightNode = _scene.CreateChild("DirectionalLight");
        lightNode.SetDirection(new Vector3f(0.5f, -1.0f, 0.5f));
        var light = lightNode.CreateComponent<Light>();
        light.SetLightType(LightType.Directional);
        light.SetColor(new Color(0.2f, 0.2f, 0.2f));
        light.SetSpecularIntensity(1.0f);

        // Create a "floor" consisting of several tiles
        for (var y = -5; y <= 5; ++y)
        {
            for (var x = -5; x <= 5; ++x)
            {
                var floorNode = _scene.CreateChild("FloorTile");
                floorNode.SetPosition(new Vector3f(x * 20.5f, -0.5f, y * 20.5f));
                floorNode.SetScale(new Vector3f(20.0f, 1.0f, 20.0f));
                var floorObject = floorNode.CreateComponent<StaticModel>();
                floorObject.SetModel(cache.Get<Model>("Models/Box.mdl"));
                floorObject.SetMaterial(cache.Get<Material>("Materials/Stone.xml"));
            }
        }

        // Create groups of mushrooms, which act as shadow casters
        const int numMushroomGroups = 25;
        const int numMushrooms = 25;
        for (var i = 0; i < numMushroomGroups; ++i)
        {
            // First create a scene node for the group. The individual mushrooms nodes will be created as children
            var groupNode = _scene.CreateChild("MushroomGroup");
            groupNode.SetPosition(new Vector3f(MathDefs.Random(190.0f) - 95.0f, 0.0f, MathDefs.Random(190.0f) - 95.0f));
            for (var j = 0; j < numMushrooms; ++j)
            {
                var mushroomNode = groupNode.CreateChild("Mushroom");
                mushroomNode.SetPosition(new Vector3f(MathDefs.Random(25.0f) - 12.5f, 0.0f, MathDefs.Random(25.0f) - 12.5f));
                mushroomNode.SetRotation(new Quaternion(0.0f, MathDefs.Random() * 360.0f, 0.0f));
                mushroomNode.SetScale(1.0f + MathDefs.Random() * 4.0f);
                var mushroomObject = mushroomNode.CreateComponent<StaticModel>();
                mushroomObject.SetModel(cache.Get<Model>("Models/Mushroom.mdl"));
                mushroomObject.SetMaterial(cache.Get<Material>("Materials/Mushroom.xml"));
                mushroomObject.SetCastShadows(true);
            }
        }

        // Create billboard sets (floating smoke)
        const int numBillboardNodes = 25;
        const int numBillboards = 10;
        for (var i = 0; i < numBillboardNodes; ++i)
        {
            var smokeNode = _scene.CreateChild("Smoke");
            smokeNode.SetPosition(new Vector3f(MathDefs.Random(200.0f) - 100.0f, MathDefs.Random(20.0f) + 10.0f, MathDefs.Random(200.0f) - 100.0f));
            var billboardObject = smokeNode.CreateComponent<BillboardSet>();
            billboardObject.SetNumBillboards((uint)numBillboards);
            billboardObject.SetMaterial(cache.Get<Material>("Materials/LitSmoke.xml"));
            billboardObject.SetSorted(true);
            for (var j = 0; j < numBillboards; ++j)
            {
                ref var billboard = ref billboardObject.GetBillboard((uint)j);
                billboard.Position = new Vector3f(MathDefs.Random(12.0f) - 6.0f, MathDefs.Random(8.0f) - 4.0f, MathDefs.Random(12.0f) - 6.0f);
                billboard.Size = new Vector2f(MathDefs.Random(2.0f) + 3.0f, MathDefs.Random(2.0f) + 3.0f);
                billboard.Rotation = MathDefs.Random() * 360.0f;
                billboard.Enabled = true;
            }

            // After modifying the billboards, they need to be "committed" so that the BillboardSet updates its internals
            billboardObject.Commit();
        }

        // Create shadow casting spotlights
        const int numLights = 9;
        for (var i = 0; i < numLights; ++i)
        {
            lightNode = _scene.CreateChild("SpotLight");
            light = lightNode.CreateComponent<Light>();
            const float angle = 0.0f;
            var position = new Vector3f((i % 3) * 60.0f - 60.0f, 45.0f, i / 3.0f * 60.0f - 60.0f);
            var color = new Color(((i + 1) & 1) * 0.5f + 0.5f, (((i + 1) >> 1) & 1) * 0.5f + 0.5f, (((i + 1) >> 2) & 1) * 0.5f + 0.5f);
            lightNode.SetPosition(position);
            lightNode.SetDirection(new Vector3f(MathDefs.Sin(angle), -1.5f, MathDefs.Cos(angle)));
            light.SetLightType(LightType.Spot);
            light.SetRange(90.0f);
            light.SetRampTexture(cache.Get<Texture2D>("Textures/RampExtreme.png"));
            light.SetFov(45.0f);
            light.SetColor(color);
            light.SetSpecularIntensity(1.0f);
            light.SetCastShadows(true);
            light.SetShadowBias(new BiasParameters(0.00002f, 0.0f));

            // Configure shadow fading for the lights. When they are far away enough, the lights eventually become unshadowed for
            // better GPU performance. Note that we could also set the maximum distance for each object to cast shadows
            light.SetShadowFadeDistance(100.0f); // Fade start distance
            light.SetShadowDistance(125.0f); // Fade end distance, shadows are disabled
            // Set half resolution for the shadow maps for increased performance
            light.SetShadowResolution(0.5f);
            // The spot lights will not have anything near them, so move the near plane of the shadow camera farther
            // for better shadow depth resolution
            light.SetShadowNearFarRatio(0.01f);
        }

        // Create the camera. Limit far clip distance to match the fog
        _cameraNode = _scene.CreateChild("Camera");
        _cameraNode.CreateComponent<FreeFlyController>();
        var camera = _cameraNode.CreateComponent<Camera>();
        camera.SetFarClip(300.0f);

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
        // Set up a viewport to the Renderer subsystem so that the 3D scene can be seen
        Renderer.SetViewport(0, new Viewport(_scene!, _cameraNode!.GetComponent<Camera>()!));
    }

    /// <summary>Subscribe to application-wide logic update and post-render update events.</summary>
    private void SubscribeToEvents()
    {
        // Subscribe HandlePostRenderUpdate() function for processing the post-render update event, during which we request
        // debug geometry
        SubscribeToEvent<CoreEvents.PostRenderUpdateEvent>((ref CoreEvents.PostRenderUpdateEvent _) => HandlePostRenderUpdate());
    }

    /// <summary>Read input and moves the camera.</summary>
    private void MoveCamera(float timeStep)
    {
        // Do not move if the UI has a focused element (the console)
        if (ImGui.GetIO().WantCaptureKeyboard)
            return;

        // Toggle debug geometry with space
        if (Input.GetKeyPress(Key.Space))
            _drawDebug = !_drawDebug;
    }

    /// <summary>Animate the scene.</summary>
    private void AnimateScene(float timeStep)
    {
        // Get the light and billboard scene nodes
        _scene!.GetChildrenWithComponent<Light>(_lightNodes);
        _scene.GetChildrenWithComponent<BillboardSet>(_billboardNodes);
        const float lightRotationSpeed = 20.0f;
        const float billboardRotationSpeed = 50.0f;

        // Rotate the lights around the world Y-axis
        for (var i = 0; i < _lightNodes.Count; ++i)
            _lightNodes[i].Rotate(new Quaternion(0.0f, lightRotationSpeed * timeStep, 0.0f), TransformSpace.World);

        // Rotate the individual billboards within the billboard sets, then recommit to make the changes visible
        for (var i = 0; i < _billboardNodes.Count; ++i)
        {
            var billboardObject = _billboardNodes[i].GetComponent<BillboardSet>()!;
            var numBillboards = (int)billboardObject.GetNumBillboards();
            for (var j = 0; j < numBillboards; ++j)
            {
                ref var billboard = ref billboardObject.GetBillboard((uint)j);
                billboard.Rotation += billboardRotationSpeed * timeStep;
            }
            billboardObject.Commit();
        }
    }

    /// <summary>Handle the logic update event.</summary>
    protected override void Update(float timeStep)
    {
        if (Input.ExitRequested || Input.GetKeyPress(Key.Escape))
        {
            Exit();
            return;
        }

        // Move the camera and animate the scene, scale movement with time step
        AnimateScene(timeStep);
        MoveCamera(timeStep);
    }

    /// <summary>Handle the post-render update event.</summary>
    private void HandlePostRenderUpdate()
    {
        // If draw debug mode is enabled, draw viewport debug geometry. This time use depth test, as otherwise the result becomes
        // hard to interpret due to large object count
        if (_drawDebug)
            Renderer.DrawDebugGeometry(true);
    }

    protected override void Stop()
    {
    }

    private void DrawInstructions()
    {
        var windowOpen = true;
        if (ImGui.Begin("Billboards"u8, ref windowOpen, ImGuiWindowFlags.ImGuiWindowFlagsNoResize | ImGuiWindowFlags.ImGuiWindowFlagsNoSavedSettings))
        {
            ImGui.TextUnformatted("Use WASD keys and mouse/touch to move\nSpace to toggle debug geometry"u8);
            if (ImGui.Button(_drawDebug ? "Debug geometry: On"u8 : "Debug geometry: Off"u8))
                _drawDebug = !_drawDebug;
        }
        ImGui.End();
    }
}
