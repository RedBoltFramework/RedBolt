namespace RedBolt.Sample.CameraShake;

using System;

using RedBolt;
using RedBolt.Bindings.DearImGui;
using RedBolt.DearImGui;
using RedBolt.Core;
using RedBolt.Engine;
using RedBolt.Graphics;
using RedBolt.Input;
using RedBolt.Platform;
using RedBolt.Scene;

internal static class CameraShakeProgram
{
    private static int Main()
    {
        try
        {
            var exitCode = new CameraShakeApplication().Run();
            if (exitCode == 0)
                Console.WriteLine("RedBolt CameraShake sample finished.");
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
/// Static 3D scene example.
/// This sample demonstrates:
///     - Creating a 3D scene with static content
///     - Displaying the scene using the Renderer subsystem
///     - Handling keyboard and mouse input to move a freelook camera
/// </summary>
sealed class CameraShakeApplication : Application
{
    private Scene? _scene;
    private Node? _cameraNode;
    private ShakeComponent? _shakeComponent;

    protected override void Setup(EngineParameters parameters)
    {
        parameters.ApplicationName = "RedBolt.Sample.CameraShake";
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
        parameters.WindowTitle = "RedBolt Camera Shake";
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
        // Set the mouse mode to use in the sample
        Input.SetMouseMode(MouseMode.Free);
        Input.SetMouseVisible(true);
    }

    /// <summary>Construct the scene content.</summary>
    private void CreateScene()
    {
        var cache = Resources;

        _scene = new Scene();
        _scene.LoadFile("Scenes/SimpleScene.scene");

        _cameraNode = _scene.FindChild("MainCamera");
        _shakeComponent = _cameraNode!.FindComponent<ShakeComponent>();
        _cameraNode.CreateComponent<FreeFlyController>();
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
        Renderer.SetViewport(0, new Viewport(_scene!, _cameraNode!.FindComponent<Camera>()!));
    }

    protected override void Update(float timeStep)
    {
        if (Input.ExitRequested || Input.GetKeyPress(Key.Escape))
        {
            Exit();
            return;
        }

        if (Input.GetKeyPress(Key.Space))
        {
            _shakeComponent!.AddTrauma(1.0f);
        }
    }

    protected override void Stop()
    {
    }

    private static void DrawInstructions()
    {
        var windowOpen = true;
        if (ImGui.Begin("Camera Shake\0"u8, ref windowOpen, ImGuiWindowFlags.ImGuiWindowFlagsNoResize | ImGuiWindowFlags.ImGuiWindowFlagsNoSavedSettings))
            ImGui.TextUnformatted("Press SPACE to shake camera\0"u8);
        ImGui.End();
    }
}
