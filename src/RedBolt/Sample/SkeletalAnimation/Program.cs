// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Sample.SkeletalAnimation;

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
using Widgets = RedBolt.SystemUI.Widgets;

internal static class SkeletalAnimationProgram
{
    private static int Main()
    {
        try { return new SkeletalAnimationApplication().Run(); }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}

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
    private Texture2D[]? _controlPrompts;

    public SkeletalAnimationApplication()
    {
    }

    protected override void Setup(EngineParameters parameters)
    {
        parameters.ApplicationName = "RedBolt.Sample.SkeletalAnimation";
        parameters.Borderless = false; parameters.FullScreen = false;
        parameters.ResourcePaths = "CoreData;SampleData";
        parameters.ResourcePrefixPaths = ".;..";
        parameters.WindowMaximize = false;
#if !REDBOLT_PLATFORM_BROWSER
            parameters.WindowWidth = 1280; parameters.WindowHeight = 720;
#endif
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
        planeObject.SetMaterial(cache.Get<Material>("Materials/TechnicalDemo/SquareFloorGrid.xml"));

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

        const int numModels = 16;
        const float modelRotateSpeed = 100.0f;
        var bounds = new BoundingBox(new Vector3f(-12.0f, 0.0f, -10.0f), new Vector3f(12.0f, 0.0f, 12.0f));
        var dummyModel = cache.Get<Model>("ThirdParty/Quaternius/TechnicalDummy.glb.d/Models/Mannequin.mdl");
        var dummyMaterial = cache.Get<Material>("Materials/TechnicalDemo/Yellow.xml");
        var walkAnimation = cache.Get<Animation>("ThirdParty/Quaternius/TechnicalDummy.glb.d/Animations/Walk_Loop.ani");
        for (var i = 0; i < numModels; ++i)
        {
            var modelNode = _scene.CreateChild("Dummy");
            modelNode.SetPosition(new Vector3f(MathDefs.Random(24.0f) - 12.0f, 0.0f, MathDefs.Random(20.0f) - 8.0f));
            modelNode.SetRotation(new Quaternion(0.0f, MathDefs.Random(360.0f), 0.0f));
            var modelObject = modelNode.CreateComponent<AnimatedModel>();
            modelObject.SetModel(dummyModel);
            modelObject.SetMaterial(0, dummyMaterial);
            modelObject.SetMaterial(1, dummyMaterial);
            modelObject.SetCastShadows(true);
            var startTime = MathDefs.Random(walkAnimation.Length);
            modelNode.CreateComponent<AnimationController>().PlayNewExclusive(new AnimationParameters(walkAnimation).Looped().Time(startTime));
            modelNode.CreateComponent<Mover3D>().SetParameters(walkAnimation, startTime, modelRotateSpeed, bounds);
        }

        _cameraNode = _scene.CreateChild("Camera");
        _cameraNode.CreateComponent<FreeFlyController>();
        _cameraNode.CreateComponent<Camera>().SetFarClip(300.0f);
        _cameraNode.SetPosition(new Vector3f(0.0f, 4.0f, -14.0f));
        _cameraNode.LookAt(new Vector3f(0.0f, 1.5f, 1.0f));
    }

    /// <summary>Construct an instruction text to the UI.</summary>
    private void CreateInstructions()
    {
        Context.RegisterSubsystem(new DearImGui((ImGuiConfigFlags)Engine.Parameters.DearImGuiFlags));
        SubscribeToEvent<DearImGuiEvents.EndRenderingDearImGuiEvent>((ref DearImGuiEvents.EndRenderingDearImGuiEvent _) => DrawInstructions());
        _controlPrompts =
        [
            Resources.Get<Texture2D>("ThirdParty/KenneyInputPrompts/keyboard_w.png"),
            Resources.Get<Texture2D>("ThirdParty/KenneyInputPrompts/keyboard_a.png"),
            Resources.Get<Texture2D>("ThirdParty/KenneyInputPrompts/keyboard_s.png"),
            Resources.Get<Texture2D>("ThirdParty/KenneyInputPrompts/keyboard_d.png"),
            Resources.Get<Texture2D>("ThirdParty/KenneyInputPrompts/mouse_right.png"),
            Resources.Get<Texture2D>("ThirdParty/KenneyInputPrompts/keyboard_space.png")
        ];
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
        ImGui.SetNextWindowPos(new Vector2f(10.0f, 10.0f), ImGuiCond.ImGuiCondAlways);
        if (ImGui.Begin("Skeletal Animation"u8, ref windowOpen,
            ImGuiWindowFlags.ImGuiWindowFlagsNoResize | ImGuiWindowFlags.ImGuiWindowFlagsNoSavedSettings))
        {
            for (var i = 0; i < 3; ++i)
            {
                ImGui.Image(_controlPrompts![i], PromptIconSize);
                ImGui.SameLine();
            }
            ImGui.Image(_controlPrompts![3], PromptIconSize);
            ImGui.SameLine();
            Widgets.AlignTextToLastItem();
            ImGui.TextUnformatted("Move"u8);

            DrawPrompt(_controlPrompts[4], "Look"u8);
            DrawPrompt(_controlPrompts[5], _drawDebug ? "Hide skeletons"u8 : "Show skeletons"u8);
        }
        ImGui.End();
    }

    private static void DrawPrompt(Texture2D texture, ReadOnlySpan<byte> label)
    {
        ImGui.Image(texture, PromptIconSize);
        ImGui.SameLine();
        Widgets.AlignTextToLastItem();
        ImGui.TextUnformatted(label);
    }

    private static readonly Vector2f PromptIconSize = new(24.0f, 24.0f);
}
