// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Sample.CameraShake;

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
    private Texture2D[]? _controlPrompts;
    private static readonly Vector2f PromptIconSize = new(24.0f, 24.0f);

    protected override void Setup(EngineParameters parameters)
    {
        parameters.ApplicationName = "RedBolt.Sample.CameraShake";
        parameters.Borderless = false;
        parameters.FullScreen = false;
        parameters.ResourcePaths = "CoreData;SampleData";
        parameters.ResourcePrefixPaths = ".;..";
        parameters.WindowMaximize = false;
#if !REDBOLT_PLATFORM_BROWSER
            parameters.WindowHeight = 720;
            parameters.WindowWidth = 1280;
#endif
        parameters.WindowTitle = "RedBolt Camera Shake";
    }

    /// <summary>Setup after engine initialization and before running the main loop.</summary>
    protected override void Start()
    {
        _sampleInput = new SampleInputMap();
        _sampleInput.AddButton("Shake", Scancode.Space, ControllerButton.A, "SHAKE");
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
        // Set up a viewport to the Renderer subsystem so that the 3D scene can be seen. We need to define the scene and the camera
        // at minimum. Additionally we could configure the viewport screen size and the rendering path (eg. forward / deferred) to
        // use, but now we just use full screen and default render path configured in the engine command line options
        Renderer.SetViewport(0, new Viewport(_scene!, _cameraNode!.FindComponent<Camera>()!));
    }

    protected override void Update(float timeStep)
    {
        if (Input.ExitRequested || _sampleInput!.Pressed("Exit"))
        {
            Exit();
            return;
        }

        if (_sampleInput.Pressed("Shake"))
        {
            _shakeComponent!.AddTrauma(1.0f);
        }
    }

    protected override void Stop()
    {
        _sampleInput?.Dispose();
        _sampleInput = null;
    }

    private void DrawInstructions()
    {
        var windowOpen = true;
        ImGui.SetNextWindowPos(new Vector2f(10.0f, 10.0f), ImGuiCond.ImGuiCondAlways);
        if (ImGui.Begin("Camera Shake"u8, ref windowOpen, ImGuiWindowFlags.ImGuiWindowFlagsAlwaysAutoResize | ImGuiWindowFlags.ImGuiWindowFlagsNoSavedSettings))
        {
            DrawPrompt(SampleInputMap.GetFreeFlyMovementPrompt(), SampleInputMap.GetFreeFlyMovementLabel());
            DrawPrompt(SampleInputMap.GetLookPrompt(), SampleInputMap.GetFreeFlyLookLabel());
            DrawPrompt(SampleInputMap.GetActionPrompt(_controlPrompts![5], ControllerButton.A), "Shake camera"u8);
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
    private SampleInputMap? _sampleInput;
}
