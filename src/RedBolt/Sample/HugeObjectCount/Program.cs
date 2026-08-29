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
using Widgets = RedBolt.SystemUI.Widgets;

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
///     - Creating a forest with up to 250 x 250 trees
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
    /// <summary>Tree scene nodes and animation parameters.</summary>
    private readonly List<TreeAnimation> _trees = [];
    /// <summary>Animation flag.</summary>
    private bool _animate;
    private float _animationTime;
    /// <summary>Group optimization flag.</summary>
    private bool _useGroups = true;

    protected override void Setup(EngineParameters parameters)
    {
        parameters.ApplicationName = "RedBolt.Sample.HugeObjectCount";
        parameters.Borderless = false; parameters.FullScreen = false;
        parameters.ResourcePaths = "CoreData;SampleData";
        parameters.ResourcePrefixPaths = ".;..";
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
        zone.SetAmbientColor(new Color(0.31f, 0.34f, 0.3f));
        zone.SetFogColor(new Color(0.66f, 0.76f, 0.86f)); zone.SetFogStart(150.0f); zone.SetFogEnd(450.0f);
        var skyNode = _scene.CreateChild("Sky");
        var sky = skyNode.CreateComponent<Skybox>();
        sky.SetModel(cache.Get<Model>("Models/Box.mdl")); sky.SetMaterial(cache.Get<Material>("Materials/Skybox.xml"));
        var groundNode = _scene.CreateChild("MeadowGround"); groundNode.SetScale(new Vector3f(800.0f, 1.0f, 800.0f));
        var ground = groundNode.CreateComponent<StaticModel>();
        ground.SetModel(cache.Get<Model>("Models/Plane.mdl"));
        ground.SetMaterial(cache.Get<Material>("ThirdParty/Quaternius/StylizedNatureShowcase.glb.d/Materials/MeadowGrass_Lit.xml"));
        // Create a directional light
        var lightNode = _scene.CreateChild("DirectionalLight");
        lightNode.SetDirection(new Vector3f(0.6f, -1.0f, 0.8f)); // The direction vector does not need to be normalized
        var light = lightNode.CreateComponent<Light>(); light.SetLightType(LightType.Directional);
        light.SetColor(new Color(1.0f, 0.84f, 0.68f));

        var treeModel = cache.Get<Model>("ThirdParty/Quaternius/StylizedNatureShowcase.glb.d/Models/CommonTree.mdl");
        var barkMaterial = cache.Get<Material>("ThirdParty/Quaternius/StylizedNatureShowcase.glb.d/Materials/Bark_NormalTree_LitNormalMap.xml");
        var leavesMaterial = cache.Get<Material>("ThirdParty/Quaternius/StylizedNatureShowcase.glb.d/Materials/Leaves_NormalTree_Lit.xml");

        _trees.Clear();
        var minX = _useGroups ? -125 : -64;
        var maxX = _useGroups ? 125 : 65;
        var minY = _useGroups ? -125 : -64;
        var maxY = _useGroups ? 125 : 65;
        var spacingX = _useGroups ? 3.0f : 5.8f;
        var spacingY = _useGroups ? 3.0f : 5.8f;
        if (!_useGroups)
        {
            // Create individual tree StaticModels in the scene
            for (var y = minY; y < maxY; ++y)
                for (var x = minX; x < maxX; ++x)
                {
                    var treeNode = CreateTreeNode(x, y, spacingX, spacingY);
                    var treeObject = treeNode.CreateComponent<StaticModel>();
                    treeObject.SetModel(treeModel); treeObject.SetMaterial(0, barkMaterial); treeObject.SetMaterial(1, leavesMaterial);
                    AddTreeAnimation(treeNode, x, y);
                }
        }
        else
        {
            // Create StaticModelGroups in the scene
            StaticModelGroup? lastGroup = null;
            for (var y = minY; y < maxY; ++y)
                for (var x = minX; x < maxX; ++x)
                {
                    // Create new group if no group yet, or the group has already "enough" objects. The tradeoff is between culling
                    // accuracy and the amount of CPU processing needed for all the objects. Note that the group's own transform
                    // does not matter, and it does not render anything if instance nodes are not added to it
                    if (lastGroup is null || lastGroup.GetNumInstanceNodes() >= 25 * 25)
                    {
                        var treeGroupNode = _scene.CreateChild("TreeGroup");
                        lastGroup = treeGroupNode.CreateComponent<StaticModelGroup>();
                        lastGroup.SetModel(treeModel); lastGroup.SetMaterial(0, barkMaterial); lastGroup.SetMaterial(1, leavesMaterial);
                    }
                    var treeNode = CreateTreeNode(x, y, spacingX, spacingY);
                    AddTreeAnimation(treeNode, x, y); lastGroup.AddInstanceNode(treeNode);
                }
        }

        // Create or reattach the camera.
        if (_cameraNode is not null) _scene.AddChild(_cameraNode);
        else
        {
            _cameraNode = _scene.CreateChild("Camera"); _cameraNode.CreateComponent<FreeFlyController>();
            _cameraNode.SetPosition(new Vector3f(0.0f, 8.0f, -400.0f));
            var camera = _cameraNode.CreateComponent<Camera>(); camera.SetFarClip(500.0f);
        }
    }

    private Node CreateTreeNode(int x, int y, float spacingX, float spacingY)
    {
        var seed = GetTreeSeed(x, y);
        var jitterX = GetRandom(seed) - 0.5f;
        var jitterY = GetRandom(seed + 1) - 0.5f;
        var treeNode = _scene!.CreateChild("Tree");
        treeNode.SetPosition(new Vector3f(x * spacingX + (y & 1) * spacingX * 0.5f + jitterX * spacingX * 0.4f,
            0.0f, y * spacingY + jitterY * spacingY * 0.4f));
        treeNode.SetRotation(new Quaternion(0.0f, GetRandom(seed + 2) * 360.0f, 0.0f));
        treeNode.SetScale(0.85f + GetRandom(seed + 3) * 0.3f);
        return treeNode;
    }

    private void AddTreeAnimation(Node treeNode, int x, int y)
    {
        var seed = GetTreeSeed(x, y);
        _trees.Add(new TreeAnimation(treeNode, new Quaternion(0.0f, GetRandom(seed + 2) * 360.0f, 0.0f),
            GetRandom(seed + 4) * 2.0f * MathDefs.Pi, 0.85f + GetRandom(seed + 5) * 0.3f,
            1.0f + GetRandom(seed + 6) * 0.75f));
    }

    private static uint GetTreeSeed(int x, int y) => unchecked((uint)x * 0x8da6b343u ^ (uint)y * 0xd8163841u);

    private static float GetRandom(uint seed)
    {
        seed ^= seed >> 16;
        seed *= 0x7feb352du;
        seed ^= seed >> 15;
        seed *= 0x846ca68bu;
        seed ^= seed >> 16;
        return (seed & 0xffffu) / 65535.0f;
    }

    /// <summary>Construct an instruction text to the UI.</summary>
    private void CreateInstructions()
    {
        _controlPrompts =
        [
            Resources.Get<Texture2D>("ThirdParty/KenneyInputPrompts/keyboard_w.png"),
            Resources.Get<Texture2D>("ThirdParty/KenneyInputPrompts/keyboard_a.png"),
            Resources.Get<Texture2D>("ThirdParty/KenneyInputPrompts/keyboard_s.png"),
            Resources.Get<Texture2D>("ThirdParty/KenneyInputPrompts/keyboard_d.png"),
            Resources.Get<Texture2D>("ThirdParty/KenneyInputPrompts/mouse_right.png"),
            Resources.Get<Texture2D>("ThirdParty/KenneyInputPrompts/keyboard_space.png"),
            Resources.Get<Texture2D>("ThirdParty/KenneyInputPrompts/keyboard_g.png")
        ];
    }

    /// <summary>Set up a viewport for displaying the scene.</summary>
    private void SetupViewport()
    {
        // Set up a viewport to the Renderer subsystem so that the 3D scene can be seen
        Renderer.SetViewport(0, new Viewport(_scene!, _cameraNode!.GetComponent<Camera>()!));
    }

    /// <summary>Animate the scene.</summary>
    private void AnimateObjects(float timeStep)
    {
        using var profile = Profiler.Profile("AnimateObjects");

        _animationTime += timeStep;
        for (var i = 0; i < _trees.Count; ++i)
        {
            var tree = _trees[i];
            var sway = MathF.Sin(_animationTime * tree.Speed + tree.Phase) * tree.Amplitude;
            var crossSway = MathF.Sin(_animationTime * tree.Speed * 0.73f + tree.Phase * 1.37f) * tree.Amplitude * 0.45f;
            tree.Node.SetRotation(tree.BaseRotation * new Quaternion(crossSway, 0.0f, sway));
        }
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
        ImGui.SetNextWindowPos(new Vector2f(10.0f, 10.0f), ImGuiCond.ImGuiCondAlways);
        if (ImGui.Begin("Huge Forest Demo"u8, ref windowOpen, ImGuiWindowFlags.ImGuiWindowFlagsNoResize | ImGuiWindowFlags.ImGuiWindowFlagsNoSavedSettings))
        {
            for (var i = 0; i < 3; ++i)
            {
                ImGui.Image(_controlPrompts![i], PromptIconSize);
                ImGui.SameLine();
            }
            DrawPrompt(_controlPrompts![3], "Move"u8);
            DrawPrompt(_controlPrompts[4], "Look / drag touch"u8);
            DrawPrompt(_controlPrompts[5], "Toggle animation"u8);
            DrawPrompt(_controlPrompts[6], "Toggle tree grouping"u8);
            ImGui.Separator();

            var time = GetSubsystem<Time>()!;
            _fpsText.FormatInPlace($"{_trees.Count:N0} trees | FPS: {time.GetFramesPerSecond():F1}");
            ImGui.TextUnformatted(_fpsText.ContentBytes);
            ImGui.Checkbox("Animation"u8, ref _animate);
            if (ImGui.Checkbox("Grouping"u8, ref _useGroups)) CreateScene();
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

    private Texture2D[]? _controlPrompts;
    private static readonly Vector2f PromptIconSize = new(24.0f, 24.0f);

    private readonly struct TreeAnimation(Node node, Quaternion baseRotation, float phase, float speed, float amplitude)
    {
        public Node Node { get; } = node;
        public Quaternion BaseRotation { get; } = baseRotation;
        public float Phase { get; } = phase;
        public float Speed { get; } = speed;
        public float Amplitude { get; } = amplitude;
    }
}
