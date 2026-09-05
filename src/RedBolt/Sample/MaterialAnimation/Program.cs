// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Sample.MaterialAnimation;

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
using RedBolt.RenderPipeline;
using RedBolt.Scene;
using Widgets = RedBolt.SystemUI.Widgets;

internal static class MaterialAnimationProgram
{
    private static int Main()
    {
        try
        {
            return new MaterialAnimationApplication().Run();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }
}

/// <summary>
/// Material animation example.
/// This sample is base on StaticScene, and it demonstrates:
///     - Usage of material shader animation for mush room material
/// </summary>
sealed class MaterialAnimationApplication : Application
{
    private readonly record struct PulsingNode(Node Node, Vector3f BaseScale, float Phase, float Amount, float Speed);

    private Scene? _scene;
    private Node? _cameraNode;
    private readonly List<PulsingNode> _pulsingTrees = [];
    private readonly List<PulsingNode> _pulsingStones = [];
    private float _sceneTime;

    protected override void Setup(EngineParameters parameters)
    {
        parameters.ApplicationName = "RedBolt.Sample.MaterialAnimation";
        parameters.Borderless = false;
        parameters.FullScreen = false;
        parameters.ResourcePaths = "CoreData;SampleData";
        parameters.ResourcePrefixPaths = ".;..";
        parameters.WindowMaximize = false;
#if !REDBOLT_PLATFORM_BROWSER
            parameters.WindowHeight = 720;
            parameters.WindowWidth = 1280;
#endif
        parameters.WindowTitle = "RedBolt Material Animation";
    }

    /// <summary>Setup after engine initialization and before running the main loop.</summary>
    protected override void Start()
    {
        _sampleInput = new SampleInputMap();
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
        if (!_scene.LoadFile("Scenes/StaticScene.scene"))
            throw new InvalidOperationException("Could not load the forest scene.");

        var renderPipeline = _scene.CreateComponent<RenderPipeline>();
        renderPipeline.SetRenderPassEnabled("Postprocess: Bloom", true);
        renderPipeline.SetRenderPassEnabled("Postprocess: FXAA v3", true);

        var zone = _scene.FindChild("Zone")!.GetComponent<Zone>()!;
        zone.SetAmbientColor(new Color(0.12f, 0.1f, 0.16f));
        zone.SetFogColor(new Color(0.12f, 0.08f, 0.16f));
        zone.SetFogStart(45.0f);
        zone.SetFogEnd(120.0f);

        var light = _scene.FindChild("DirectionalLight")!.GetComponent<Light>()!;
        light.SetColor(new Color(0.56f, 0.64f, 1.0f));
        light.SetBrightness(0.65f);

        CreateAccentLight("Warm Rim", new Vector3f(-9.0f, 4.5f, -2.0f), new Color(1.0f, 0.24f, 0.08f));
        CreateAccentLight("Cool Rim", new Vector3f(10.0f, 5.0f, 6.0f), new Color(0.22f, 0.48f, 1.0f));

        ConfigureMaterialAnimations();
        CollectPulsingNodes();

        // Frame the animated caps through foreground foliage and into the forest depth.
        _cameraNode = _scene.FindChild("Camera")!;
        _cameraNode.SetPosition(new Vector3f(0.0f, 6.5f, -24.0f));
        _cameraNode.LookAt(new Vector3f(0.0f, 2.0f, 2.5f));
    }

    private void ConfigureMaterialAnimations()
    {
        var stoneColor = new ValueAnimation();
        stoneColor.SetKeyFrame(0.0f, new Color(0.7f, 0.82f, 1.15f));
        stoneColor.SetKeyFrame(1.2f, new Color(1.35f, 0.48f, 0.9f));
        stoneColor.SetKeyFrame(2.4f, new Color(0.48f, 1.25f, 1.1f));
        stoneColor.SetKeyFrame(3.6f, new Color(0.7f, 0.82f, 1.15f));
        AnimateMaterial("Rocks_Lit.xml", stoneColor);
        AnimateMaterial("PathRocks_Lit.xml", stoneColor);

        var treeColor = new ValueAnimation();
        treeColor.SetKeyFrame(0.0f, new Color(0.5f, 1.1f, 1.25f));
        treeColor.SetKeyFrame(1.5f, new Color(1.25f, 0.62f, 0.85f));
        treeColor.SetKeyFrame(3.0f, new Color(0.5f, 1.1f, 1.25f));
        AnimateMaterial("Bark_NormalTree_LitNormalMap.xml", treeColor, 0.82f);
        AnimateMaterial("Leaves_NormalTree_Lit.xml", treeColor, 1.0f);
        AnimateMaterial("Bark_TwistedTree_LitNormalMap.xml", treeColor, 0.7f);
        AnimateMaterial("Leaves_TwistedTree_Lit.xml", treeColor, 0.88f);
        AnimateMaterial("Leaves_Pine_Lit.xml", treeColor, 1.13f);
    }

    private void AnimateMaterial(string materialName, ValueAnimation animation, float speed = 1.0f)
    {
        var material = Resources.Get<Material>(
            $"ThirdParty/Quaternius/StylizedNatureShowcase.glb.d/Materials/{materialName}");
        material.SetScene(_scene);
        material.SetShaderParameterAnimation("MatDiffColor", animation);
        material.SetShaderParameterAnimationSpeed("MatDiffColor", speed);
    }

    private void CollectPulsingNodes()
    {
        var treeIndex = 0;
        var stoneIndex = 0;
        foreach (var node in _scene!.GetChildren(true))
        {
            if (node.Name is "Tree" or "AccentTree" or "HeroTree")
            {
                _pulsingTrees.Add(new PulsingNode(node, node.Scale, treeIndex * 2.399963f,
                    0.008f + treeIndex % 5 * 0.002f, 0.55f + treeIndex % 7 * 0.035f));
                ++treeIndex;
            }
            else if (node.Name is "Rock" or "PathStone")
            {
                _pulsingStones.Add(new PulsingNode(node, node.Scale, stoneIndex * 1.618034f,
                    0.045f + stoneIndex % 4 * 0.012f, 0.9f + stoneIndex % 5 * 0.08f));
                ++stoneIndex;
            }
        }
    }

    private void CreateAccentLight(string name, Vector3f position, Color color)
    {
        var node = _scene!.CreateChild(name);
        node.SetPosition(position);
        var light = node.CreateComponent<Light>();
        light.SetLightType(LightType.Point);
        light.SetRange(22.0f);
        light.SetBrightness(1.8f);
        light.SetColor(color);
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
            Resources.Get<Texture2D>("ThirdParty/KenneyInputPrompts/mouse_right.png")
        ];
    }

    /// <summary>Set up a viewport for displaying the scene.</summary>
    private void SetupViewport()
    {
        // Set up a viewport to the Renderer subsystem so that the 3D scene can be seen. We need to define the scene and the camera
        // at minimum. Additionally we could configure the viewport screen size and the rendering path (eg. forward / deferred) to
        // use, but now we just use full screen and default render path configured in the engine command line options
        Renderer.SetViewport(0, new Viewport(_scene!, _cameraNode!.GetComponent<Camera>()!));
    }

    protected override void Update(float timeStep)
    {
        if (Input.ExitRequested || _sampleInput!.Pressed("Exit"))
            Exit();

        _sceneTime += timeStep;
        PulseNodes(_pulsingTrees);
        PulseNodes(_pulsingStones);
    }

    private void PulseNodes(List<PulsingNode> nodes)
    {
        foreach (var pulsingNode in nodes)
        {
            var pulse = 1.0f + MathF.Sin(_sceneTime * pulsingNode.Speed + pulsingNode.Phase) * pulsingNode.Amount;
            pulsingNode.Node.SetScale(pulsingNode.BaseScale * pulse);
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
        if (ImGui.Begin("Material Animation"u8, ref windowOpen, ImGuiWindowFlags.ImGuiWindowFlagsAlwaysAutoResize | ImGuiWindowFlags.ImGuiWindowFlagsNoSavedSettings))
        {
            DrawPrompt(SampleInputMap.GetFreeFlyMovementPrompt(), SampleInputMap.GetFreeFlyMovementLabel());
            DrawPrompt(SampleInputMap.GetLookPrompt(), SampleInputMap.GetFreeFlyLookLabel());
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
    private SampleInputMap? _sampleInput;
}
