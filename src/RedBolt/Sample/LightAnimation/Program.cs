// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Sample.LightAnimation;

using System;

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

internal static class LightAnimationProgram
{
    private static int Main()
    {
        try { return new LightAnimationApplication().Run(); }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}

/// <summary>
/// Light animation example.
/// This sample is base on StaticScene, and it demonstrates:
/// - Usage of attribute animation for light color &amp; UI animation
/// </summary>
sealed class LightAnimationApplication : Application
{
    private const string NaturePath = "ThirdParty/Quaternius/StylizedNatureShowcase.glb.d/";
    private static readonly Color[] LightColors =
    [
        new Color(0.3f, 0.85f, 1.0f),
        new Color(1.0f, 0.2f, 0.45f),
        new Color(1.0f, 0.65f, 0.12f),
        new Color(0.25f, 1.0f, 0.48f),
        new Color(0.68f, 0.3f, 1.0f),
        new Color(1.0f, 0.34f, 0.16f),
    ];

    private Scene? _scene;
    private Node? _cameraNode;
    private Node? _attributeRouteNode;
    private readonly Node?[] _movingLightNodes = new Node[LightColors.Length];
    private readonly Node?[] _lightVisualNodes = new Node[LightColors.Length];
    private readonly Light?[] _movingLights = new Light[LightColors.Length];
    private readonly float[] _verticalPhases = new float[LightColors.Length];
    private readonly float[] _verticalAmplitudes = new float[LightColors.Length];
    private float _sceneTime;

    protected override void Setup(EngineParameters parameters)
    {
        parameters.ApplicationName = "RedBolt.Sample.LightAnimation";
        parameters.Borderless = false;
        parameters.FullScreen = false;
        parameters.ResourcePaths = "CoreData;SampleData";
        parameters.ResourcePrefixPaths = ".;..";
        parameters.WindowMaximize = false;
#if !REDBOLT_PLATFORM_BROWSER
            parameters.WindowWidth = 1280;
            parameters.WindowHeight = 720;
#endif
        parameters.WindowTitle = "RedBolt Light Animation";
    }

    /// <summary>Setup after engine initialization and before running the main loop.</summary>
    protected override void Start()
    {
        Context.RegisterSubsystem(new DearImGui((ImGuiConfigFlags)Engine.Parameters.DearImGuiFlags));

        // Create the UI content
        CreateInstructions();

        // Create the scene content
        CreateScene();

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

        // Turn the shared forest into a cool dusk stage. The animated light is the only strong
        // illumination, so its route is immediately readable across the trees and path.
        var zone = _scene.FindChild("Zone")!.GetComponent<Zone>()!;
        zone.SetAmbientColor(new Color(0.055f, 0.075f, 0.12f));
        zone.SetFogColor(new Color(0.035f, 0.06f, 0.12f));
        zone.SetFogStart(42.0f);
        zone.SetFogEnd(115.0f);

        var moon = _scene.FindChild("DirectionalLight")!.GetComponent<Light>()!;
        moon.SetColor(new Color(0.2f, 0.32f, 0.55f));
        moon.SetBrightness(0.5f);

        // Keep the original attribute-animation route as one of several distinct movements.
        var lightContainer = _scene.CreateChild("LightRoute");
        _attributeRouteNode = lightContainer;
        lightContainer.SetScale(new Vector3f(0.48f, 1.45f, 0.48f));
        _movingLightNodes[0] = lightContainer.CreateChild("AnimatedForestLight");
        _movingLightNodes[0]!.CreateComponent<AnimationController>()
            .PlayNew(new AnimationParameters("Animations/LightAnimation.xml").Looped());
        CreateLightVisual(0);

        // The remaining lights follow ellipses, figure-eights, crossing orbits, and a breathing
        // spiral. Each receives an independent random vertical phase and amplitude.
        for (var i = 1; i < _movingLightNodes.Length; ++i)
        {
            _movingLightNodes[i] = _scene.CreateChild($"Moving Light {i + 1}");
            CreateLightVisual(i);
        }
        for (var i = 0; i < _movingLightNodes.Length; ++i)
        {
            _verticalPhases[i] = MathDefs.Random(MathF.Tau);
            _verticalAmplitudes[i] = 1.4f + MathDefs.Random(3.2f);
        }
        AnimateLights();

        // Stone waypoints trace the same square route through the forest. Their silhouettes give
        // each color keyframe a landmark without competing with the moving light.
        var waypointPositions = new[]
        {
            new Vector3f(-16.5f, 0.0f, -16.5f), new Vector3f(16.5f, 0.0f, -16.5f),
            new Vector3f(16.5f, 0.0f, 16.5f), new Vector3f(-16.5f, 0.0f, 16.5f)
        };
        for (var i = 0; i < waypointPositions.Length; ++i)
        {
            var node = _scene.CreateChild("Light Waypoint");
            node.SetPosition(waypointPositions[i]);
            node.SetRotation(new Quaternion(0.0f, i * 71.0f, 0.0f));
            node.SetScale(2.1f);
            var model = node.CreateComponent<StaticModel>();
            model.SetModel(cache.Get<Model>($"{NaturePath}Models/Rock_Medium_{i % 3 + 1}.mdl"));
            model.SetMaterial(cache.Get<Material>($"{NaturePath}Materials/Rocks_Lit.xml"));
            model.SetCastShadows(true);
        }

        _cameraNode = _scene.FindChild("Camera")!;
        _cameraNode.SetPosition(new Vector3f(0.0f, 11.0f, -42.0f));
        _cameraNode.LookAt(new Vector3f(0.0f, 3.0f, 2.0f));
    }

    private void CreateLightVisual(int index)
    {
        var visualNode = _movingLightNodes[index]!.CreateChild("Light Visual");
        _lightVisualNodes[index] = visualNode;

        var light = index == 0
            ? _movingLightNodes[index]!.CreateComponent<Light>()
            : visualNode.CreateComponent<Light>();
        _movingLights[index] = light;
        light.SetLightType(LightType.Point);
        light.SetRange(index == 0 ? 27.0f : 19.0f);
        light.SetBrightness(index == 0 ? 2.8f : 2.1f);
        light.SetColor(LightColors[index]);

        var halo = visualNode.CreateComponent<BillboardSet>();
        halo.SetNumBillboards(2);
        halo.SetMaterial(Resources.Get<Material>("Materials/ThirdParty/KenneyParticles/star_01.xml"));
        halo.SetFaceCameraMode(FaceCameraMode.RotateXyz);
        for (var i = 0u; i < 2; ++i)
        {
            ref var billboard = ref halo.GetBillboard(i);
            billboard.Position = Vector3f.Zero;
            billboard.Size = new Vector2f(1.25f + i * 1.35f, 1.25f + i * 1.35f);
            billboard.Color = new Color(LightColors[index].R, LightColors[index].G, LightColors[index].B,
                0.72f - i * 0.36f);
            billboard.Rotation = index * 29.0f + i * 53.0f;
            billboard.Enabled = true;
        }
        halo.Commit();
    }

    /// <summary>Construct an instruction text to the UI.</summary>
    private void CreateInstructions()
    {
        SubscribeToEvent<DearImGuiEvents.EndRenderingDearImGuiEvent>(
            (ref DearImGuiEvents.EndRenderingDearImGuiEvent _) => DrawInstructions());

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
        Renderer.SetViewport(0, new Viewport(_scene!, _cameraNode!.GetComponent<Camera>()!));
    }

    protected override void Update(float timeStep)
    {
        if (Input.ExitRequested || Input.GetKeyPress(Key.Escape))
            Exit();

        _sceneTime += timeStep;
        AnimateLights();
    }

    private void AnimateLights()
    {
        for (var i = 0; i < _movingLightNodes.Length; ++i)
        {
            var verticalOffset = MathF.Sin(_sceneTime * (0.75f + i * 0.11f) + _verticalPhases[i])
                * _verticalAmplitudes[i]
                + MathF.Sin(_sceneTime * 1.73f + _verticalPhases[i] * 0.61f) * 0.65f;
            if (i == 0)
                _attributeRouteNode!.SetPosition(new Vector3f(0.0f, verticalOffset, 0.0f));
            else
                _movingLightNodes[i]!.SetPosition(GetLightPosition(i, verticalOffset));

            _movingLights[i]!.SetBrightness(1.85f + MathF.Sin(_sceneTime * 2.1f + i * 0.83f) * 0.45f);
        }
    }

    private Vector3f GetLightPosition(int index, float verticalOffset)
    {
        var time = _sceneTime;
        return index switch
        {
            1 => new Vector3f(MathF.Cos(time * 0.62f) * 24.0f, 7.0f + verticalOffset,
                MathF.Sin(time * 0.62f) * 13.0f),
            2 => new Vector3f(MathF.Sin(time * 0.74f) * 22.0f, 8.5f + verticalOffset,
                MathF.Sin(time * 1.48f) * 11.0f),
            3 => new Vector3f(MathF.Cos(time * 0.91f + 1.2f) * 15.0f, 6.0f + verticalOffset,
                MathF.Sin(time * 0.47f) * 25.0f),
            4 => new Vector3f(MathF.Sin(time * 0.54f + 2.4f) * 27.0f, 9.0f + verticalOffset,
                MathF.Cos(time * 0.81f) * 9.0f),
            5 => new Vector3f(MathF.Cos(time * 0.68f) * (12.0f + MathF.Sin(time * 0.31f) * 8.0f),
                7.5f + verticalOffset,
                MathF.Sin(time * 0.68f) * (12.0f + MathF.Sin(time * 0.31f) * 8.0f)),
            _ => Vector3f.Zero
        };
    }

    private void DrawInstructions()
    {
        var windowOpen = true;
        ImGui.SetNextWindowPos(new Vector2f(10.0f, 10.0f), ImGuiCond.ImGuiCondAlways);
        if (ImGui.Begin("Light Animation"u8, ref windowOpen, ImGuiWindowFlags.ImGuiWindowFlagsNoResize | ImGuiWindowFlags.ImGuiWindowFlagsNoSavedSettings))
        {
            for (var i = 0; i < 3; ++i)
            {
                ImGui.Image(_controlPrompts![i], PromptIconSize);
                ImGui.SameLine();
            }
            DrawPrompt(_controlPrompts![3], "Move"u8);
            DrawPrompt(_controlPrompts[4], "Look / drag touch"u8);
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
}
