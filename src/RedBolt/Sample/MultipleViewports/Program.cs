// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Sample.MultipleViewports;

using System;

using RedBolt;
using RedBolt.Bindings.DearImGui;
using RedBolt.DearImGui;
using RedBolt.Core;
using RedBolt.Engine;
using RedBolt.Graphics;
using RedBolt.Input;
using RedBolt.Math;
using RedBolt.Scene;

internal static class MultipleViewportsProgram
{
    private static int Main()
    {
        try
        {
            using var application = new MultipleViewports();
            var exitCode = application.Run();
            if (exitCode == 0)
                Console.WriteLine("RedBolt MultipleViewports sample finished.");
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
/// Multiple viewports example.
/// This sample demonstrates:
///     - Setting up two viewports with two separate cameras
///     - Adding post processing effects to a viewport's render path and toggling them
/// </summary>
public class MultipleViewports : Application
{
    /// <summary>
    /// Construct.
    /// </summary>
    public MultipleViewports()
    {
        _drawDebug = false;
        _animateCamera = Environment.GetEnvironmentVariable("REDBOLT_MULTIPLE_VIEWPORTS_ANIMATE_CAMERA") == "1";
        _cameraAnimationDelay = 7.0f;
    }

    /// <summary>
    /// Setup after engine initialization and before running the main loop.
    /// </summary>
    protected override void Start()
    {
        // Execute base class startup
        base.Start();

        // Create the scene content
        CreateScene();

        // Create the UI content
        CreateInstructions();

        // Setup the viewports for displaying the scene
        SetupViewports();

        // Hook up to the frame update and render post-update events
        SubscribeToEvents();

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

        // Create octree, use default volume (-1000, -1000, -1000) to (1000, 1000, 1000)
        // Also create a DebugRenderer component so that we can draw debug geometry
        _scene.CreateComponent<Octree>();
        _scene.CreateComponent<DebugRenderer>();

        // Create scene node & StaticModel component for showing a static plane
        var planeNode = _scene.CreateChild("Plane");
        planeNode.SetScale(new Vector3f(100.0f, 1.0f, 100.0f));
        var planeObject = planeNode.CreateComponent<StaticModel>();
        planeObject.SetModel(cache.Get<Model>("Models/Plane.mdl"));
        planeObject.SetMaterial(cache.Get<Material>("Materials/StoneTiled.xml"));

        // Create a Zone component for ambient lighting & fog control
        var zoneNode = _scene.CreateChild("Zone");
        var zone = zoneNode.CreateComponent<Zone>();
        zone.SetBoundingBox(new BoundingBox(-1000.0f, 1000.0f));
        zone.SetAmbientColor(new Color(0.15f, 0.15f, 0.15f));
        zone.SetFogColor(new Color(0.5f, 0.5f, 0.7f));
        zone.SetFogStart(100.0f);
        zone.SetFogEnd(300.0f);

        // Create a directional light to the world. Enable cascaded shadows on it
        var lightNode = _scene.CreateChild("DirectionalLight");
        lightNode.SetDirection(new Vector3f(0.6f, -1.0f, 0.8f));
        var light = lightNode.CreateComponent<Light>();
        light.SetLightType(LightType.Directional);
        light.SetCastShadows(true);
        light.SetShadowBias(new BiasParameters(0.00025f, 0.5f));
        // Set cascade splits at 10, 50 and 200 world units, fade shadows out at 80% of maximum shadow distance
        light.SetShadowCascade(new CascadeParameters(10.0f, 50.0f, 200.0f, 0.0f, 0.8f));

        // Create some mushrooms
        const int NumMushrooms = 240;
        for (var i = 0; i < NumMushrooms; ++i)
        {
            var mushroomNode = _scene.CreateChild("Mushroom");
            mushroomNode.SetPosition(new Vector3f(MathDefs.Random(90.0f) - 45.0f, 0.0f, MathDefs.Random(90.0f) - 45.0f));
            mushroomNode.SetRotation(new Quaternion(0.0f, MathDefs.Random(360.0f), 0.0f));
            mushroomNode.SetScale(0.5f + MathDefs.Random(2.0f));
            var mushroomObject = mushroomNode.CreateComponent<StaticModel>();
            mushroomObject.SetModel(cache.Get<Model>("Models/Mushroom.mdl"));
            mushroomObject.SetMaterial(cache.Get<Material>("Materials/Mushroom.xml"));
            mushroomObject.SetCastShadows(true);
        }

        // Create randomly sized boxes. If boxes are big enough, make them occluders
        const int NumBoxes = 20;
        for (var i = 0; i < NumBoxes; ++i)
        {
            var boxNode = _scene.CreateChild("Box");
            var size = 1.0f + MathDefs.Random(10.0f);
            boxNode.SetPosition(new Vector3f(MathDefs.Random(80.0f) - 40.0f, size * 0.5f, MathDefs.Random(80.0f) - 40.0f));
            boxNode.SetScale(size);
            var boxObject = boxNode.CreateComponent<StaticModel>();
            boxObject.SetModel(cache.Get<Model>("Models/Box.mdl"));
            boxObject.SetMaterial(cache.Get<Material>("Materials/Stone.xml"));
            boxObject.SetCastShadows(true);
            if (size >= 3.0f)
                boxObject.SetOccluder(true);
        }

        // Create the cameras. Limit far clip distance to match the fog
        _cameraNode = _scene.CreateChild("Camera");
        _cameraNode.CreateComponent<FreeFlyController>();
        var camera = _cameraNode.CreateComponent<Camera>();
        camera.SetFarClip(300.0f);

        // Parent the rear camera node to the front camera node and turn it 180 degrees to face backward
        // Here, we use the angle-axis constructor for Quaternion instead of the usual Euler angles
        _rearCameraNode = _cameraNode.CreateChild("RearCamera");
        _rearCameraNode.Rotate(new Quaternion(180.0f, Vector3f.Up));
        var rearCamera = _rearCameraNode.CreateComponent<Camera>();
        rearCamera.SetFarClip(300.0f);
        // Because the rear viewport is rather small, disable occlusion culling from it. Use the camera's
        // "view override flags" for this. We could also disable eg. shadows or force low material quality
        // if we wanted
        rearCamera.SetViewOverrideFlags(ViewOverride.DisableOcclusion);

        // Set an initial position for the front camera scene node above the plane
        _cameraNode.SetPosition(new Vector3f(0.0f, 5.0f, 0.0f));
    }

    /// <summary>
    /// Construct an instruction text to the UI.
    /// </summary>
    private void CreateInstructions()
    {
        var dearImGui = new DearImGui((ImGuiConfigFlags)Engine.Parameters.DearImGuiFlags)
        {
            PassThroughEvents = true
        };
        Context.RegisterSubsystem(dearImGui);
        SubscribeToEvent<DearImGuiEvents.EndRenderingDearImGuiEvent>((ref DearImGuiEvents.EndRenderingDearImGuiEvent _) => DrawInstructions());

        // Construct new Text object, set string to display and font to use
        // The text has multiple rows. Center them in relation to each other
        // Position the text relative to the screen center
        // The rbfx Text control is replaced by the allowed Dear ImGui sample UI surface.
    }

    /// <summary>
    /// Set up viewports.
    /// </summary>
    private void SetupViewports()
    {
        Renderer.SetNumViewports(2);

        // Set up the front camera viewport
        var viewport = new Viewport(_scene!, _cameraNode!.GetComponent<Camera>()!);
        Renderer.SetViewport(0, viewport);

        // Set up the rear camera viewport on top of the front view ("rear view mirror")
        // The viewport index must be greater in that case, otherwise the view would be left behind
        var rearViewport = new Viewport(
            _scene!,
            _rearCameraNode!.GetComponent<Camera>()!,
            new IntRect(Graphics.Width * 2 / 3, 32, Graphics.Width - 32, Graphics.Height / 3));
        Renderer.SetViewport(1, rearViewport);
    }

    /// <summary>
    /// Subscribe to application-wide logic update and post-render update events.
    /// </summary>
    private void SubscribeToEvents()
    {
        // Subscribe HandlePostRenderUpdate() method for processing the post-render update event, during which we request
        // debug geometry
        SubscribeToEvent<CoreEvents.PostRenderUpdateEvent>((ref CoreEvents.PostRenderUpdateEvent _) => HandlePostRenderUpdate());
    }

    /// <summary>
    /// Read input and moves the camera.
    /// </summary>
    private void MoveCamera(float timeStep)
    {
        // Do not move if the UI has a focused element (the console)
        if (ImGui.GetIO().WantCaptureKeyboard)
            return;

        // Toggle debug geometry with space
        if (Input.GetKeyPress(Key.Space))
            _drawDebug = !_drawDebug;
    }

    /// <summary>
    /// Handle the logic update event.
    /// </summary>
    protected override void Update(float timeStep)
    {
        if (Input.ExitRequested || Input.GetKeyPress(Key.Escape))
        {
            Exit();
            return;
        }

        if (_animateCamera)
        {
            _cameraAnimationDelay -= timeStep;
            if (_cameraAnimationDelay <= 0.0f)
                _cameraNode!.Translate(new Vector3f(0.0f, 0.0f, 0.1f * timeStep));
        }

        // Move the camera, scale movement with time step
        MoveCamera(timeStep);
    }

    /// <summary>
    /// Handle the post-render update event.
    /// </summary>
    private void HandlePostRenderUpdate()
    {
        // If draw debug mode is enabled, draw viewport debug geometry, which will show eg. drawable bounding boxes and skeleton
        // bones. Disable depth test so that we can see the effect of occlusion
        if (_drawDebug)
            Renderer.DrawDebugGeometry(false);
    }

    private void DrawInstructions()
    {
        var windowOpen = true;
        if (ImGui.Begin("Multiple Viewports"u8, ref windowOpen, ImGuiWindowFlags.ImGuiWindowFlagsNoResize | ImGuiWindowFlags.ImGuiWindowFlagsNoSavedSettings))
        {
            ImGui.TextUnformatted("Use WASD keys and mouse/touch to move\nB to toggle bloom, F to toggle FXAA\nSpace to toggle debug geometry"u8);
            if (ImGui.Button(_drawDebug ? "Debug geometry: On"u8 : "Debug geometry: Off"u8))
                _drawDebug = !_drawDebug;
        }
        ImGui.End();
    }

    protected override void Setup(EngineParameters parameters)
    {
        parameters.ApplicationName = "RedBolt.Sample.MultipleViewports";
        parameters.Borderless = false;
        parameters.FullScreen = false;
        parameters.ResourcePaths = "CoreData;Data";
        parameters.ResourcePrefixPaths = ".;..";
        parameters.WindowMaximize = false;
#if !REDBOLT_PLATFORM_BROWSER
            parameters.WindowHeight = 720;
            parameters.WindowWidth = 1280;
#endif
        parameters.WindowTitle = "RedBolt Multiple Viewports";
    }

    protected override void Stop()
    {
        Renderer.SetNumViewports(0);
        _rearCameraNode = null;
        _cameraNode = null;
        _scene?.Dispose();
        _scene = null;
        base.Stop();
    }

    public override void Dispose()
    {
        if (_scene is not null)
            Renderer.SetNumViewports(0);
        _rearCameraNode = null;
        _cameraNode = null;
        _scene?.Dispose();
        _scene = null;
        base.Dispose();
    }

    private Scene? _scene;
    private Node? _cameraNode;
    /// <summary>
    /// Rear-facing camera scene node.
    /// </summary>
    private Node? _rearCameraNode;
    /// <summary>
    /// Flag for drawing debug geometry.
    /// </summary>
    private bool _drawDebug;
    private readonly bool _animateCamera;
    private float _cameraAnimationDelay;
}
