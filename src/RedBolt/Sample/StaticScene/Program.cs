// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Sample.StaticScene;

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

internal static class StaticSceneProgram
{
    private static int Main()
    {
        try
        {
            using var application = new StaticScene();
            var exitCode = application.Run();
            if (exitCode == 0)
                Console.WriteLine("RedBolt StaticScene sample finished.");
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
///     - Creating a static 3D scene with components and resources
///     - Displaying the scene using the Renderer subsystem
///     - Handling keyboard and mouse input to move a freelook camera
/// </summary>
public class StaticScene : Application
{
    /// <summary>
    /// Construct.
    /// </summary>
    public StaticScene()
    {
    }

    /// <summary>
    /// Setup after engine initialization and before running the main loop.
    /// </summary>
    protected override void Start()
    {
        _sampleInput = new SampleInputMap();
        // Execute base class startup
        base.Start();

        // Create the scene content
        if (!CreateScene())
        {
            Console.Error.WriteLine("Could not load the static showcase scene.");
            Exit();
            return;
        }

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
    private bool CreateScene()
    {
        _scene = new Scene();
        if (!_scene.LoadFile("Scenes/StaticScene.scene"))
            return false;
        _cameraNode = _scene.FindChild("Camera");
        return _cameraNode is not null;
    }

    /// <summary>
    /// Construct an instruction text to the UI.
    /// </summary>
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
            Resources.Get<Texture2D>("ThirdParty/KenneyInputPrompts/mouse_right.png")
        ];

        // Construct new Text object, set string to display and font to use
        // Position the text relative to the screen center
        // The rbfx Text control is replaced by the allowed Dear ImGui sample UI surface.
    }

    /// <summary>
    /// Set up a viewport for displaying the scene.
    /// </summary>
    private void SetupViewport()
    {
        // Set up a viewport to the Renderer subsystem so that the 3D scene can be seen. We need to define the scene and the camera
        // at minimum. Additionally we could configure the viewport screen size and the rendering path (eg. forward / deferred) to
        // use, but now we just use full screen and default render path configured in the engine command line options
        var viewport = new Viewport(_scene!, _cameraNode!.GetComponent<Camera>()!);
        Renderer.SetViewport(0, viewport);
    }

    protected override void Update(float timeStep)
    {
        if (Input.ExitRequested || _sampleInput!.Pressed("Exit"))
        {
            Exit();
            return;
        }

    }

    protected override void Stop()
    {
        _sampleInput?.Dispose();
        _sampleInput = null;
        Renderer.SetNumViewports(0);
        _cameraNode = null;
        _scene?.Dispose();
        _scene = null;
        base.Stop();
    }

    private void DrawInstructions()
    {
        var windowOpen = true;
        ImGui.SetNextWindowPos(new Vector2f(10.0f, 10.0f), ImGuiCond.ImGuiCondAlways);
        if (ImGui.Begin("Static Scene"u8, ref windowOpen, ImGuiWindowFlags.ImGuiWindowFlagsAlwaysAutoResize | ImGuiWindowFlags.ImGuiWindowFlagsNoSavedSettings))
        {
            ImGui.Image(SampleInputMap.GetFreeFlyMovementPrompt(), PromptIconSize);
            ImGui.SameLine();
            Widgets.AlignTextToLastItem();
            ImGui.TextUnformatted(SampleInputMap.GetFreeFlyMovementLabel());
            ImGui.Image(SampleInputMap.GetLookPrompt(), PromptIconSize);
            ImGui.SameLine();
            Widgets.AlignTextToLastItem();
            ImGui.TextUnformatted(SampleInputMap.GetFreeFlyLookLabel());
        }
        ImGui.End();
    }

    protected override void Setup(EngineParameters parameters)
    {
        parameters.ApplicationName = "RedBolt.Sample.StaticScene";
        parameters.Borderless = false;
        parameters.FullScreen = false;
        parameters.ResourcePaths = "CoreData;SampleData";
        parameters.ResourcePrefixPaths = ".;..";
        parameters.WindowMaximize = false;
#if !REDBOLT_PLATFORM_BROWSER
            parameters.WindowHeight = 720;
            parameters.WindowWidth = 1280;
#endif
        parameters.WindowTitle = "RedBolt Static Scene";
    }

    public override void Dispose()
    {
        if (_scene is not null)
            Renderer.SetNumViewports(0);
        _cameraNode = null;
        _scene?.Dispose();
        _scene = null;
        base.Dispose();
    }

    private Scene? _scene;
    private Node? _cameraNode;
    private Texture2D[]? _controlPrompts;
    private static readonly Vector2f PromptIconSize = new(24.0f, 24.0f);
    private SampleInputMap? _sampleInput;
}
