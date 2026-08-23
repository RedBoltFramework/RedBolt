// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Sample.CameraOperator;

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

internal static class CameraOperatorProgram
{
    private static int Main()
    {
        try { return new CameraOperatorSampleApplication().Run(); }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}

/// <summary>
/// Camera operator sample.
/// This sample demonstrates:
///     - How camera operator tracks objects in space to keep them in camera view
///     - How camera operator parameters work
/// </summary>
sealed class CameraOperatorSampleApplication : Application
{
    private Scene? _scene;
    private Node? _cameraNode;
    private CameraOperator? _cameraOperator;
    private Node? _cubeA;
    private Node? _cubeB;
    private float _angle;
    private bool _trackCubeA;
    private bool _trackCubeB;
    private bool _orthographic;

    protected override void Setup(EngineParameters parameters)
    {
        parameters.ApplicationName = "RedBolt.Sample.CameraOperator";
        parameters.Borderless = false; parameters.FullScreen = false;
        parameters.ResourcePaths = "CoreData;SampleData"; parameters.ResourcePrefixPaths = ".;..";
        parameters.WindowMaximize = false;
#if !REDBOLT_PLATFORM_BROWSER
            parameters.WindowHeight = 720; parameters.WindowWidth = 1280;
#endif
        parameters.WindowTitle = "RedBolt Camera Operator";
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
        Input.SetMouseMode(MouseMode.Free); Input.SetMouseVisible(true);
    }

    /// <summary>Construct the scene content.</summary>
    private void CreateScene()
    {
        var cache = Resources;
        _scene = new Scene(); _scene.LoadFile("Scenes/CameraOperatorScene.scene");
        _cameraNode = _scene.FindChild("MainCamera");
        _cameraOperator = _cameraNode!.FindComponent<CameraOperator>();
        _cameraNode.CreateComponent<FreeFlyController>();
        _cubeA = _scene.FindChild("CubeA", true);
        _cubeB = _scene.FindChild("CubeB", true);
    }

    /// <summary>Construct an instruction text to the UI.</summary>
    private static void CreateInstructions() {}

    /// <summary>Set up a viewport for displaying the scene.</summary>
    private void SetupViewport()
    {
        // Set up a viewport to the Renderer subsystem so that the 3D scene can be seen. We need to define the scene and the
        // camera at minimum. Additionally we could configure the viewport screen size and the rendering path (eg. forward /
        // deferred) to use, but now we just use full screen and default render path configured in the engine command line
        // options
        Renderer.SetViewport(0, new Viewport(_scene!, _cameraNode!.FindComponent<Camera>()!));
    }

    protected override void Update(float timeStep)
    {
        if (Input.ExitRequested || Input.GetKeyPress(Key.Escape)) { Exit(); return; }
        _angle += timeStep * 45.0f;
        _cubeA!.SetPosition(new Vector3f(0, 4 * MathDefs.Cos(_angle), 0));
        _cubeB!.SetPosition(new Vector3f(0, 0, 4 * MathDefs.Cos(_angle + 90)));
    }

    private void TrackCubeAToggled(bool state)
    {
        if (state) _cameraOperator!.TrackNode(_cubeA);
        else _cameraOperator!.RemoveTrackedNode(_cubeA);
    }

    private void TrackCubeBToggled(bool state)
    {
        if (state) _cameraOperator!.TrackNode(_cubeB);
        else _cameraOperator!.RemoveTrackedNode(_cubeB);
    }

    private void OrthographicToggled(bool state)
    {
        _cameraNode!.FindComponent<Camera>()!.SetOrthographic(state);
    }

    private void DrawInstructions()
    {
        var windowOpen = true;
        if (ImGui.Begin("Camera Operator"u8, ref windowOpen, ImGuiWindowFlags.ImGuiWindowFlagsNoResize | ImGuiWindowFlags.ImGuiWindowFlagsNoSavedSettings))
        {
            ImGui.TextUnformatted("Right click to rotate camera"u8);
            if (ImGui.Checkbox("Track Cube A"u8, ref _trackCubeA))
                TrackCubeAToggled(_trackCubeA);
            if (ImGui.Checkbox("Track Cube B"u8, ref _trackCubeB))
                TrackCubeBToggled(_trackCubeB);
            if (ImGui.Checkbox("Orthographic"u8, ref _orthographic))
                OrthographicToggled(_orthographic);
        }
        ImGui.End();
    }
}
