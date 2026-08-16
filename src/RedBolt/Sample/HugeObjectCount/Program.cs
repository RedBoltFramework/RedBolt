// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Sample.HugeObjectCount;

using System;
using System.Collections.Generic;

using RedBolt;
using RedBolt.Bindings.DearImGui;
using RedBolt.DearImGui;
using RedBolt.Container;
using RedBolt.Core;
using RedBolt.Engine;
using RedBolt.Graphics;
using RedBolt.Input;
using RedBolt.Math;
using RedBolt.Scene;

internal static class HugeObjectCountProgram
{
    private static int Main()
    {
        try { return new HugeObjectCountApplication().Run(); }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}

/// <summary>
/// Huge object count example.
/// This sample demonstrates:
///     - Creating a scene with 250 x 250 simple objects
///     - Competing with http://yosoygames.com.ar/wp/2013/07/ogre-2-0-is-up-to-3x-faster/ :)
///     - Allowing examination of performance hotspots in the rendering code
///     - Using the profiler to measure the time taken to animate the scene
///     - Optionally speeding up rendering by grouping objects with the StaticModelGroup component
/// </summary>
sealed class HugeObjectCountApplication : Application
{
    private Scene? _scene;
    private Node? _cameraNode;
    private CString _fpsText = "FPS: 0";
    /// <summary>Box scene nodes.</summary>
    private readonly List<Node> _boxNodes = [];
    /// <summary>Animation flag.</summary>
    private bool _animate;
    /// <summary>Group optimization flag.</summary>
    private bool _useGroups;

    protected override void Setup(EngineParameters parameters)
    {
        parameters.ApplicationName = "RedBolt.Sample.HugeObjectCount";
        parameters.Borderless = false; parameters.FullScreen = false;
        parameters.ResourcePaths = "CoreData;Data"; parameters.ResourcePrefixPaths = ".;..";
        parameters.WindowMaximize = false;
#if !REDBOLT_PLATFORM_BROWSER
            parameters.WindowHeight = 720; parameters.WindowWidth = 1280;
#endif
        parameters.WindowTitle = "RedBolt Huge Object Count";
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
        if (_scene is null) _scene = new Scene();
        else _scene.Clear();

        // Create the Octree component to the scene so that drawable objects can be rendered. Use default volume
        // (-1000, -1000, -1000) to (1000, 1000, 1000)
        _scene.CreateComponent<Octree>();
        // Create a Zone for ambient light & fog control
        var zoneNode = _scene.CreateChild("Zone");
        var zone = zoneNode.CreateComponent<Zone>();
        zone.SetBoundingBox(new BoundingBox(-1000.0f, 1000.0f));
        zone.SetFogColor(new Color(0.2f, 0.2f, 0.2f)); zone.SetFogStart(200.0f); zone.SetFogEnd(300.0f);
        // Create a directional light
        var lightNode = _scene.CreateChild("DirectionalLight");
        lightNode.SetDirection(new Vector3f(-0.6f, -1.0f, -0.8f)); // The direction vector does not need to be normalized
        var light = lightNode.CreateComponent<Light>(); light.SetLightType(LightType.Directional);

        _boxNodes.Clear();
        if (!_useGroups)
        {
            light.SetColor(new Color(0.7f, 0.35f, 0.0f));
            // Create individual box StaticModels in the scene
            for (var y = -125; y < 125; ++y)
                for (var x = -125; x < 125; ++x)
                {
                    var boxNode = _scene.CreateChild("Box");
                    boxNode.SetPosition(new Vector3f(x * 0.3f, 0.0f, y * 0.3f)); boxNode.SetScale(0.25f);
                    var boxObject = boxNode.CreateComponent<StaticModel>();
                    boxObject.SetModel(cache.Get<Model>("Models/Box.mdl")); _boxNodes.Add(boxNode);
                }
        }
        else
        {
            light.SetColor(new Color(0.6f, 0.6f, 0.6f)); light.SetSpecularIntensity(1.5f);
            // Create StaticModelGroups in the scene
            StaticModelGroup? lastGroup = null;
            for (var y = -125; y < 125; ++y)
                for (var x = -125; x < 125; ++x)
                {
                    // Create new group if no group yet, or the group has already "enough" objects. The tradeoff is between culling
                    // accuracy and the amount of CPU processing needed for all the objects. Note that the group's own transform
                    // does not matter, and it does not render anything if instance nodes are not added to it
                    if (lastGroup is null || lastGroup.GetNumInstanceNodes() >= 25 * 25)
                    {
                        var boxGroupNode = _scene.CreateChild("BoxGroup");
                        lastGroup = boxGroupNode.CreateComponent<StaticModelGroup>();
                        lastGroup.SetModel(cache.Get<Model>("Models/Box.mdl"));
                    }
                    var boxNode = _scene.CreateChild("Box");
                    boxNode.SetPosition(new Vector3f(x * 0.3f, 0.0f, y * 0.3f)); boxNode.SetScale(0.25f);
                    _boxNodes.Add(boxNode); lastGroup.AddInstanceNode(boxNode);
                }
        }

        // Create or reattach the camera.
        if (_cameraNode is not null) _scene.AddChild(_cameraNode);
        else
        {
            _cameraNode = _scene.CreateChild("Camera"); _cameraNode.CreateComponent<FreeFlyController>();
            _cameraNode.SetPosition(new Vector3f(0.0f, 10.0f, -100.0f));
            var camera = _cameraNode.CreateComponent<Camera>(); camera.SetFarClip(300.0f);
        }
    }

    /// <summary>Construct an instruction text to the UI.</summary>
    private static void CreateInstructions() {}

    /// <summary>Set up a viewport for displaying the scene.</summary>
    private void SetupViewport()
    {
        // Set up a viewport to the Renderer subsystem so that the 3D scene can be seen
        Renderer.SetViewport(0, new Viewport(_scene!, _cameraNode!.GetComponent<Camera>()!));
    }

    /// <summary>Animate the scene.</summary>
    private void AnimateObjects(float timeStep)
    {
        const float rotateSpeed = 15.0f;
        // Rotate about the Z axis (roll)
        var rotateQuaternion = new Quaternion(rotateSpeed * timeStep, Vector3f.Forward);
        for (var i = 0; i < _boxNodes.Count; ++i) _boxNodes[i].Rotate(rotateQuaternion);
    }

    /// <summary>Handle the logic update event.</summary>
    protected override void Update(float timeStep)
    {
        if (Input.ExitRequested || Input.GetKeyPress(Key.Escape)) { Exit(); return; }
        // Toggle animation with space
        if (Input.GetKeyPress(Key.Space)) _animate = !_animate;
        // Toggle grouped / ungrouped mode
        if (Input.GetKeyPress(Key.G)) { _useGroups = !_useGroups; CreateScene(); }
        // Animate scene if enabled
        if (_animate) AnimateObjects(timeStep);
    }

    private void DrawInstructions()
    {
        var windowOpen = true;
        if (ImGui.Begin("Huge Object Count"u8, ref windowOpen, ImGuiWindowFlags.ImGuiWindowFlagsNoResize | ImGuiWindowFlags.ImGuiWindowFlagsNoSavedSettings))
        {
            var time = GetSubsystem<Time>()!;
            _fpsText.FormatInPlace($"FPS: {time.GetFramesPerSecond():F1}");
            ImGui.TextUnformatted("Use WASD keys and mouse/touch to move\nSpace to toggle animation\nG to toggle object group optimization"u8);
            ImGui.TextUnformatted(_fpsText.ContentBytes);
            if (ImGui.Button(_animate ? "Animation: On"u8 : "Animation: Off"u8)) _animate = !_animate;
            if (ImGui.Button(_useGroups ? "Groups: On"u8 : "Groups: Off"u8)) { _useGroups = !_useGroups; CreateScene(); }
        }
        ImGui.End();
    }
}
