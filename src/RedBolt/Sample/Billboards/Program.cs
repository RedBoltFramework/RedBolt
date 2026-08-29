// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Sample.Billboards;

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

internal static class BillboardsProgram
{
    private static int Main()
    {
        try
        {
            var exitCode = new BillboardsApplication().Run();
            if (exitCode == 0)
                Console.WriteLine("RedBolt Billboards sample finished.");
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
/// Billboard showcase built around a luminous beacon.
/// This sample demonstrates layered additive billboards, directional ribbons,
/// sorted lit smoke, fixed-screen-size lights, and animated billboard data.
/// </summary>
sealed class BillboardsApplication : Application
{
    private const string NaturePath = "ThirdParty/Quaternius/StylizedNatureShowcase.glb.d/";
    private const int BeaconCount = 18;
    private const int RibbonCount = 120;
    private const int FogCount = 56;
    private const int FireflyCount = 120;
    private const int EmberCount = 72;

    private Scene? _scene;
    private Node? _cameraNode;
    private BillboardSet? _beacon;
    private BillboardSet? _ribbons;
    private BillboardSet? _fog;
    private BillboardSet? _fireflies;
    private BillboardSet? _embers;
    private Light? _beaconLight;
    private Texture2D[]? _controlPrompts;
    private float _sceneTime;
    private bool _drawDebug;
    private static readonly Vector2f PromptIconSize = new(24.0f, 24.0f);

    protected override void Setup(EngineParameters parameters)
    {
        parameters.ApplicationName = "RedBolt.Sample.Billboards";
        parameters.Borderless = false;
        parameters.FullScreen = false;
        parameters.ResourcePaths = "CoreData;SampleData";
        parameters.ResourcePrefixPaths = ".;..";
        parameters.WindowMaximize = false;
#if !REDBOLT_PLATFORM_BROWSER
        parameters.WindowHeight = 720;
        parameters.WindowWidth = 1280;
#endif
        parameters.WindowTitle = "RedBolt Billboards — Spirit Grove";
    }

    protected override void Start()
    {
        Context.RegisterSubsystem(new DearImGui((ImGuiConfigFlags)Engine.Parameters.DearImGuiFlags));
        SubscribeToEvent<DearImGuiEvents.EndRenderingDearImGuiEvent>(
            (ref DearImGuiEvents.EndRenderingDearImGuiEvent _) => DrawInstructions());

        CreateScene();
        _controlPrompts =
        [
            Resources.Get<Texture2D>("ThirdParty/KenneyInputPrompts/keyboard_w.png"),
            Resources.Get<Texture2D>("ThirdParty/KenneyInputPrompts/keyboard_a.png"),
            Resources.Get<Texture2D>("ThirdParty/KenneyInputPrompts/keyboard_s.png"),
            Resources.Get<Texture2D>("ThirdParty/KenneyInputPrompts/keyboard_d.png"),
            Resources.Get<Texture2D>("ThirdParty/KenneyInputPrompts/mouse_right.png"),
            Resources.Get<Texture2D>("ThirdParty/KenneyInputPrompts/keyboard_space.png")
        ];
        SetupViewport();
        SubscribeToEvent<CoreEvents.PostRenderUpdateEvent>(
            (ref CoreEvents.PostRenderUpdateEvent _) => HandlePostRenderUpdate());

        Input.SetMouseMode(MouseMode.Free);
        Input.SetMouseVisible(true);
    }

    private void CreateScene()
    {
        _scene = new Scene();
        _scene.CreateComponent<Octree>();
        _scene.CreateComponent<DebugRenderer>();
        var renderPipeline = _scene.CreateComponent<RenderPipeline>();
        renderPipeline.SetRenderPassEnabled("Postprocess: Bloom", true);
        renderPipeline.SetRenderPassEnabled("Postprocess: FXAA v3", true);

        var zone = _scene.CreateComponent<Zone>();
        zone.SetBoundingBox(new BoundingBox(-1000.0f, 1000.0f));
        zone.SetAmbientColor(new Color(0.07f, 0.1f, 0.16f));
        zone.SetFogColor(new Color(0.012f, 0.025f, 0.055f));
        zone.SetFogStart(38.0f);
        zone.SetFogEnd(78.0f);

        CreateEnvironment();
        CreateLights();
        CreateBillboards();

        _cameraNode = _scene.CreateChild("Camera");
        _cameraNode.CreateComponent<FreeFlyController>();
        var camera = _cameraNode.CreateComponent<Camera>();
        camera.SetFarClip(100.0f);
        _cameraNode.SetPosition(new Vector3f(0.0f, 12.5f, -32.0f));
        _cameraNode.LookAt(new Vector3f(0.0f, 7.0f, 0.0f));
    }

    private void CreateEnvironment()
    {
        CreateNatureModel("Meadow", "MeadowGround", new Vector3f(0.0f, -0.5f, 0.0f), 0.0f, 5.5f,
            "MeadowGrass_Lit.xml");

        const int rockCount = 18;
        for (var i = 0; i < rockCount; ++i)
        {
            var angle = i * 360.0f / rockCount;
            var radians = angle * MathDefs.DegToRad;
            var model = $"RockPath_Round_Small_{i % 3 + 1}";
            CreateNatureModel("Ritual Stone", model,
                new Vector3f(MathF.Sin(radians) * 7.2f, -0.2f, MathF.Cos(radians) * 7.2f),
                angle + i % 2 * 17.0f, 1.15f + i % 4 * 0.12f, "PathRocks_Lit.xml");
        }

        CreateNatureModel("Twisted Tree", "TwistedTree_1", new Vector3f(-9.5f, -0.5f, 3.0f), 32.0f, 1.6f,
            "Bark_TwistedTree_LitNormalMap.xml", "Leaves_TwistedTree_Lit.xml");
        CreateNatureModel("Twisted Tree", "TwistedTree_1", new Vector3f(13.5f, -0.5f, 7.5f), -48.0f, 1.25f,
            "Bark_TwistedTree_LitNormalMap.xml", "Leaves_TwistedTree_Lit.xml");

        const int treeCount = 15;
        for (var i = 0; i < treeCount; ++i)
        {
            var angle = i * 137.5f + 18.0f;
            var radians = angle * MathDefs.DegToRad;
            var radius = 17.0f + i % 4 * 3.0f;
            var model = i % 4 == 0 ? "Pine_2" : i % 3 == 0 ? "CommonTree_3" : "CommonTree";
            var leaves = model == "Pine_2" ? "Leaves_Pine_Lit.xml" : "Leaves_NormalTree_Lit.xml";
            var x = MathF.Sin(radians) * radius;
            var z = MathF.Cos(radians) * radius;
            if (z < -10.0f && MathF.Abs(x) < 10.0f)
                x += x < 0.0f ? -13.0f : 13.0f;
            CreateNatureModel("Grove Tree", model, new Vector3f(x, -0.5f, z),
                angle * 1.6f, 1.35f + i % 5 * 0.2f, "Bark_NormalTree_LitNormalMap.xml", leaves);
        }

        const int undergrowthCount = 32;
        for (var i = 0; i < undergrowthCount; ++i)
        {
            var angle = i * 137.5f;
            var radians = angle * MathDefs.DegToRad;
            var radius = 9.5f + i % 8 * 1.55f;
            var position = new Vector3f(MathF.Sin(radians) * radius, -0.45f, MathF.Cos(radians) * radius);
            if (i % 4 == 0)
                CreateNatureModel("Flowers", "Flower_4_Group", position, angle, 1.1f + i % 3 * 0.15f,
                    "Leaves_Lit.xml", "Flowers_Lit.xml");
            else if (i % 5 == 0)
                CreateNatureModel("Flowering Bush", "Bush_Common_Flowers", position, angle, 1.0f + i % 3 * 0.2f,
                    "Leaves_NormalTree_Lit.xml", "Flowers_Lit.xml");
            else
                CreateNatureModel("Fern", "Fern_1", position, angle, 1.0f + i % 4 * 0.18f, "Leaves_Lit.xml");
        }
    }

    private void CreateNatureModel(string name, string modelName, Vector3f position, float yaw, float scale,
        string firstMaterial, string? secondMaterial = null)
    {
        var node = _scene!.CreateChild(name);
        node.SetPosition(position);
        node.SetRotation(new Quaternion(0.0f, yaw, 0.0f));
        node.SetScale(scale);
        var model = node.CreateComponent<StaticModel>();
        model.SetModel(Resources.Get<Model>($"{NaturePath}Models/{modelName}.mdl"));
        model.SetMaterial(0, Resources.Get<Material>($"{NaturePath}Materials/{firstMaterial}"));
        if (secondMaterial is not null)
            model.SetMaterial(1, Resources.Get<Material>($"{NaturePath}Materials/{secondMaterial}"));
        model.SetCastShadows(true);
    }

    private void CreateLights()
    {
        var moonNode = _scene!.CreateChild("Moonlight");
        moonNode.SetRotation(new Quaternion(35.0f, -35.0f, 0.0f));
        var moon = moonNode.CreateComponent<Light>();
        moon.SetLightType(LightType.Directional);
        moon.SetColor(new Color(0.42f, 0.58f, 0.95f));
        moon.SetBrightness(1.15f);
        moon.SetCastShadows(true);

        var beaconLightNode = _scene.CreateChild("BeaconLight");
        beaconLightNode.SetPosition(new Vector3f(0.0f, 7.0f, 0.0f));
        _beaconLight = beaconLightNode.CreateComponent<Light>();
        _beaconLight.SetLightType(LightType.Point);
        _beaconLight.SetRange(28.0f);
        _beaconLight.SetColor(new Color(0.1f, 0.72f, 1.0f));
        _beaconLight.SetBrightness(2.4f);

        var emberLightNode = _scene.CreateChild("EmberLight");
        emberLightNode.SetPosition(new Vector3f(4.0f, 2.0f, -2.0f));
        var emberLight = emberLightNode.CreateComponent<Light>();
        emberLight.SetLightType(LightType.Point);
        emberLight.SetRange(13.0f);
        emberLight.SetColor(new Color(1.0f, 0.28f, 0.08f));
        emberLight.SetBrightness(0.8f);
    }

    private void CreateBillboards()
    {
        _beacon = CreateBillboardSet("BeaconCore", BeaconCount,
            "Materials/ThirdParty/KenneyParticles/star_01.xml", false, FaceCameraMode.RotateXyz);
        _ribbons = CreateBillboardSet("EnergyRibbons", RibbonCount,
            "Materials/ThirdParty/KenneyParticles/star_01.xml", false, FaceCameraMode.Direction);
        _fog = CreateBillboardSet("GroundFog", FogCount,
            "Materials/ThirdParty/KenneyParticles/smoke_01.xml", true, FaceCameraMode.RotateY);
        _fireflies = CreateBillboardSet("Fireflies", FireflyCount,
            "Materials/ThirdParty/KenneyParticles/star_01.xml", false, FaceCameraMode.RotateXyz);
        _fireflies.SetFixedScreenSize(true);
        _embers = CreateBillboardSet("Embers", EmberCount,
            "Materials/ThirdParty/KenneyParticles/flame_02.xml", false, FaceCameraMode.RotateXyz);

        AnimateBillboards();
    }

    private BillboardSet CreateBillboardSet(string name, int count, string materialName, bool sorted,
        FaceCameraMode faceCameraMode)
    {
        var node = _scene!.CreateChild(name);
        var set = node.CreateComponent<BillboardSet>();
        set.SetNumBillboards((uint)count);
        set.SetMaterial(Resources.Get<Material>(materialName));
        set.SetSorted(sorted);
        set.SetFaceCameraMode(faceCameraMode);
        return set;
    }

    private void SetupViewport()
    {
        Renderer.SetViewport(0, new Viewport(_scene!, _cameraNode!.GetComponent<Camera>()!));
    }

    protected override void Update(float timeStep)
    {
        if (Input.ExitRequested || Input.GetKeyPress(Key.Escape))
        {
            Exit();
            return;
        }

        if (Input.GetKeyPress(Key.Space))
            _drawDebug = !_drawDebug;

        _sceneTime += timeStep;
        AnimateBillboards();
    }

    private void AnimateBillboards()
    {
        AnimateBeacon();
        AnimateRibbons();
        AnimateFog();
        AnimateFireflies();
        AnimateEmbers();
    }

    private void AnimateBeacon()
    {
        var beacon = _beacon!;
        for (var i = 0; i < BeaconCount; ++i)
        {
            var progress = i / (float)(BeaconCount - 1);
            var pulse = 0.82f + MathF.Sin(_sceneTime * 2.4f + i * 0.72f) * 0.18f;
            ref var billboard = ref beacon.GetBillboard((uint)i);
            billboard.Position = new Vector3f(0.0f, 1.2f + progress * 18.0f, 0.0f);
            billboard.Size = new Vector2f((3.2f - progress * 2.15f) * pulse,
                (3.2f - progress * 2.15f) * pulse);
            billboard.Color = new Color(0.45f + progress * 0.35f, 0.82f, 1.0f, 0.72f - progress * 0.22f);
            billboard.Rotation = _sceneTime * (i % 2 == 0 ? 24.0f : -31.0f) + i * 29.0f;
            billboard.Enabled = true;
        }
        beacon.Commit();
        _beaconLight!.SetBrightness(2.25f + MathF.Sin(_sceneTime * 2.4f) * 0.35f);
    }

    private void AnimateRibbons()
    {
        var ribbons = _ribbons!;
        const int strandCount = 2;
        var pointsPerStrand = RibbonCount / strandCount;
        for (var i = 0; i < RibbonCount; ++i)
        {
            var strand = i % strandCount;
            var point = i / strandCount;
            var progress = point / (float)(pointsPerStrand - 1);
            var direction = strand == 0 ? 1.0f : -1.0f;
            var angle = progress * MathF.Tau * 2.25f + _sceneTime * direction * 0.72f + strand * MathF.PI;
            var radius = 2.6f + MathF.Sin(progress * MathF.PI) * 1.9f;
            var position = new Vector3f(MathF.Cos(angle) * radius, 1.0f + progress * 17.5f,
                MathF.Sin(angle) * radius);
            var tangent = new Vector3f(-MathF.Sin(angle) * direction, 0.36f, MathF.Cos(angle) * direction).Normalized;
            var fade = MathF.Sin(progress * MathF.PI);
            ref var billboard = ref ribbons.GetBillboard((uint)i);
            billboard.Position = position;
            billboard.Direction = tangent;
            billboard.Size = new Vector2f(0.16f + fade * 0.18f, 0.75f + fade * 0.65f);
            billboard.Color = strand == 0
                ? new Color(0.18f, 0.82f, 1.0f, 0.25f + fade * 0.7f)
                : new Color(0.82f, 0.22f, 1.0f, 0.2f + fade * 0.62f);
            billboard.Rotation = 0.0f;
            billboard.Enabled = true;
        }
        ribbons.Commit();
    }

    private void AnimateFog()
    {
        var fog = _fog!;
        for (var i = 0; i < FogCount; ++i)
        {
            var angle = i * 2.399963f;
            var radius = 5.5f + i % 8 * 1.35f;
            var drift = _sceneTime * (0.08f + i % 5 * 0.012f);
            ref var billboard = ref fog.GetBillboard((uint)i);
            billboard.Position = new Vector3f(
                MathF.Cos(angle + drift) * radius + MathF.Sin(_sceneTime * 0.3f + i) * 1.3f,
                0.5f + i % 4 * 0.25f, MathF.Sin(angle + drift) * radius);
            billboard.Size = new Vector2f(5.2f + i % 4 * 0.8f, 2.25f + i % 3 * 0.5f);
            billboard.Color = new Color(0.2f, 0.42f + i % 3 * 0.035f, 0.62f, 0.2f + i % 4 * 0.025f);
            billboard.Rotation = i * 41.0f + MathF.Sin(_sceneTime * 0.2f + i) * 8.0f;
            billboard.Enabled = true;
        }
        fog.Commit();
    }

    private void AnimateFireflies()
    {
        var fireflies = _fireflies!;
        for (var i = 0; i < FireflyCount; ++i)
        {
            var angle = i * 2.399963f + MathF.Sin(_sceneTime * 0.27f + i * 0.31f) * 0.45f;
            var radius = 9.0f + i % 13 * 1.25f;
            var pulse = 0.45f + MathF.Sin(_sceneTime * 3.0f + i * 1.71f) * 0.35f;
            ref var billboard = ref fireflies.GetBillboard((uint)i);
            billboard.Position = new Vector3f(MathF.Cos(angle) * radius,
                1.2f + i % 11 * 0.7f + MathF.Sin(_sceneTime * 0.8f + i) * 0.55f,
                MathF.Sin(angle) * radius);
            billboard.Size = new Vector2f(14.0f + i % 5 * 2.0f, 14.0f + i % 5 * 2.0f);
            billboard.Color = i % 5 == 0
                ? new Color(0.35f, 0.82f, 1.0f, pulse)
                : new Color(0.62f, 1.0f, 0.58f, pulse);
            billboard.Enabled = true;
        }
        fireflies.Commit();
    }

    private void AnimateEmbers()
    {
        var embers = _embers!;
        for (var i = 0; i < EmberCount; ++i)
        {
            var progress = (_sceneTime * (0.07f + i % 7 * 0.008f) + i * 0.618034f) % 1.0f;
            var angle = i * 2.399963f + _sceneTime * (0.25f + i % 3 * 0.04f);
            var radius = 1.0f + i % 9 * 0.36f + progress * 2.0f;
            var fade = MathF.Sin(progress * MathF.PI);
            ref var billboard = ref embers.GetBillboard((uint)i);
            billboard.Position = new Vector3f(MathF.Cos(angle) * radius, 0.8f + progress * 13.0f,
                MathF.Sin(angle) * radius);
            billboard.Size = new Vector2f(0.22f + fade * 0.35f, 0.38f + fade * 0.55f);
            billboard.Color = new Color(1.0f, 0.24f + fade * 0.48f, 0.05f, fade * 0.9f);
            billboard.Rotation = i * 53.0f - _sceneTime * 38.0f;
            billboard.Enabled = true;
        }
        embers.Commit();
    }

    private void DrawInstructions()
    {
        var windowOpen = true;
        ImGui.SetNextWindowPos(new Vector2f(10.0f, 10.0f), ImGuiCond.ImGuiCondAlways);
        if (ImGui.Begin("Spirit Grove"u8, ref windowOpen,
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
            ImGui.Image(_controlPrompts[4], PromptIconSize);
            ImGui.SameLine();
            Widgets.AlignTextToLastItem();
            ImGui.TextUnformatted("Look / drag touch"u8);
            ImGui.Image(_controlPrompts[5], PromptIconSize);
            ImGui.SameLine();
            Widgets.AlignTextToLastItem();
            ImGui.TextUnformatted("Toggle debug geometry"u8);
            ImGui.Separator();
            ImGui.TextUnformatted("386 billboards: facing, directional, sorted, fixed-size"u8);
        }
        ImGui.End();
    }

    private void HandlePostRenderUpdate()
    {
        if (_drawDebug)
            Renderer.DrawDebugGeometry(true);
    }
}
