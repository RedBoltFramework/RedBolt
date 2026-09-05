// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Sample.PointerAdapter;

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

internal static class PointerAdapterProgram
{
    private static int Main()
    {
        try { return new PointerAdapterApplication().Run(); }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}

/// <summary>Pointer adapter example.</summary>
sealed class PointerAdapterApplication : Application
{
    private const string NaturePath = "ThirdParty/Quaternius/StylizedNatureShowcase.glb.d/";

    private Scene? _scene;
    private Node? _cameraNode;
    private PointerAdapter? _pointerAdapter;
    private Octree? _octree;
    private OutlineGroup? _hoverOutlineGroup;
    private OutlineGroup? _selectionOutlineGroup;
    private Drawable? _hoveredDrawable;
    private Drawable? _selectedDrawable;
    private Vector2i _pendingPointerPosition;
    private bool _hoverDirty;

    protected override void Setup(EngineParameters parameters)
    {
        parameters.ApplicationName = "RedBolt.Sample.PointerAdapter"; parameters.Borderless = false; parameters.FullScreen = false;
        parameters.ResourcePaths = "CoreData;SampleData"; parameters.ResourcePrefixPaths = ".;..";
        parameters.WindowMaximize = false;
#if !REDBOLT_PLATFORM_BROWSER
            parameters.WindowWidth = 1280; parameters.WindowHeight = 720;
#endif
        parameters.WindowTitle = "RedBolt Pointer Adapter";
    }

    protected override void Start()
    {
        _sampleInput = new SampleInputMap();
        Context.RegisterSubsystem(new DearImGui((ImGuiConfigFlags)Engine.Parameters.DearImGuiFlags));
        SubscribeToEvent<DearImGuiEvents.EndRenderingDearImGuiEvent>((ref DearImGuiEvents.EndRenderingDearImGuiEvent _) => DrawInstructions());
        _controlPrompts =
        [
            Resources.Get<Texture2D>("ThirdParty/KenneyInputPrompts/mouse_move.png"),
            Resources.Get<Texture2D>("ThirdParty/KenneyInputPrompts/touch_swipe_move.png"),
            Resources.Get<Texture2D>("ThirdParty/KenneyInputPrompts/keyboard_arrows.png"),
            SampleInputMap.GetControllerPrompt(ControllerButton.LeftStick),
            Resources.Get<Texture2D>("ThirdParty/KenneyInputPrompts/mouse_left.png"),
            Resources.Get<Texture2D>("ThirdParty/KenneyInputPrompts/touch_tap.png"),
            Resources.Get<Texture2D>("ThirdParty/KenneyInputPrompts/keyboard_enter.png"),
            SampleInputMap.GetControllerPrompt(ControllerButton.A)
        ];
        CreateScene();
        Renderer.SetViewport(0, new Viewport(_scene!, _cameraNode!.GetComponent<Camera>()!));
        _pointerAdapter = new PointerAdapter();
        SubscribeToEvent<InputEvents.MouseMoveEvent>(_pointerAdapter, HandleMouseMove);
        SubscribeToEvent<InputEvents.MouseButtonDownEvent>(_pointerAdapter, HandleMouseButtonDown);
        _pointerAdapter.SetEnabled(true);
        Input.SetMouseMode(MouseMode.Free); Input.SetMouseVisible(true);
    }

    private void CreateScene()
    {
        var cache = Resources;
        _scene = new Scene();
        if (!_scene.LoadFile("Scenes/StaticScene.scene"))
            throw new InvalidOperationException("Could not load the forest scene.");

        _octree = _scene.GetComponent<Octree>()!;
        // TODO: port scene_->CreateComponent<PhysicsWorld>(); when the managed physics subsystem is available.
        _hoverOutlineGroup = _scene.CreateComponent<OutlineGroup>();
        _hoverOutlineGroup.SetColor(Color.Yellow);
        _selectionOutlineGroup = _scene.CreateComponent<OutlineGroup>();
        _selectionOutlineGroup.SetColor(Color.Red);
        _scene.FindChild("MeadowGround")!.GetComponent<StaticModel>()!.SetViewMask(2);

        var renderPipeline = _scene.CreateComponent<RenderPipeline>();
        renderPipeline.SetRenderPassEnabled("Postprocess: Draw Outlines", true);
        renderPipeline.SetRenderPassEnabled("Postprocess: FXAA v3", true);

        var zone = _scene.FindChild("Zone")!.GetComponent<Zone>()!;
        zone.SetAmbientColor(new Color(0.2f, 0.25f, 0.2f));
        zone.SetFogColor(new Color(0.48f, 0.62f, 0.66f));
        zone.SetFogStart(48.0f);
        zone.SetFogEnd(125.0f);

        var sunNode = _scene.FindChild("DirectionalLight")!;
        sunNode.SetDirection(new Vector3f(0.6f, -1.0f, 0.5f));
        var sun = sunNode.GetComponent<Light>()!;
        sun.SetColor(new Color(1.0f, 0.82f, 0.62f));
        sun.SetBrightness(1.1f);

        // Open a compact clearing in the shared forest so every interaction target has a clean silhouette.
        foreach (var node in _scene.GetChildren())
        {
            var position = node.Position;
            if (MathF.Abs(position.X) < 9.0f && position.Z > -8.0f && position.Z < 13.0f
                && node.Name is "Tree" or "Rock" or "PathStone" or "Undergrowth")
                node.Remove();
        }

        CreateNatureModel("Rock Anchor", "Rock_Medium_1", new Vector3f(-5.5f, 0.0f, 3.0f),
            18.0f, 2.4f, "Rocks_Lit.xml");
        CreateNatureModel("Rock Anchor", "Rock_Medium_2", new Vector3f(5.4f, 0.0f, 4.5f),
            -32.0f, 2.25f, "Rocks_Lit.xml");
        CreateNatureModel("Low Rock", "RockPath_Round_Small_1", new Vector3f(-2.5f, 0.12f, 0.5f),
            25.0f, 1.45f, "PathRocks_Lit.xml");
        CreateNatureModel("Low Rock", "RockPath_Round_Small_3", new Vector3f(2.8f, 0.12f, 7.2f),
            -15.0f, 1.55f, "PathRocks_Lit.xml");

        var logNode = _scene.CreateChild("Fallen Log");
        logNode.SetPosition(new Vector3f(0.3f, 0.25f, 3.7f));
        logNode.SetRotation(new Quaternion(0.0f, -28.0f, 0.0f));
        logNode.SetScale(6.0f);
        var log = logNode.CreateComponent<StaticModel>();
        log.SetModel(cache.Get<Model>("ThirdParty/KenneyNature/log.glb.d/Models/Mesh log.mdl"));
        log.SetMaterial(0, cache.Get<Material>("ThirdParty/KenneyNature/log.glb.d/Materials/woodBark_Lit.xml"));
        log.SetMaterial(1, cache.Get<Material>("ThirdParty/KenneyNature/log.glb.d/Materials/woodInner_Lit.xml"));
        log.SetCastShadows(true);

        Vector3f[] grassPositions =
        [
            new(-4.2f, 0.0f, -0.3f), new(-1.2f, 0.0f, 7.7f), new(1.8f, 0.0f, 0.2f),
            new(4.4f, 0.0f, 1.2f), new(6.6f, 0.0f, 7.5f), new(-6.8f, 0.0f, 7.0f)
        ];
        foreach (var position in grassPositions)
        {
            var grassNode = _scene.CreateChild("Clearing Grass");
            grassNode.SetPosition(position);
            grassNode.SetRotation(new Quaternion(0.0f, position.X * 19.0f, 0.0f));
            grassNode.SetScale(1.35f);
            var grass = grassNode.CreateComponent<StaticModel>();
            grass.SetModel(cache.Get<Model>("ThirdParty/KenneyNature/grass.glb.d/Models/Mesh grass.mdl"));
            grass.SetMaterial(cache.Get<Material>("ThirdParty/KenneyNature/grass.glb.d/Materials/grass_Lit.xml"));
        }

        var rimNode = _scene.CreateChild("Warm Rim Light");
        rimNode.SetPosition(new Vector3f(-4.0f, 5.0f, 0.0f));
        var rim = rimNode.CreateComponent<Light>();
        rim.SetLightType(LightType.Point);
        rim.SetColor(new Color(1.0f, 0.38f, 0.12f));
        rim.SetRange(18.0f);
        rim.SetBrightness(1.5f);

        _cameraNode = _scene.FindChild("Camera")!;
        _cameraNode.RemoveComponent<FreeFlyController>();
        _cameraNode.SetPosition(new Vector3f(0.0f, 5.4f, -17.0f));
        _cameraNode.LookAt(new Vector3f(0.0f, 1.4f, 3.5f));
        _cameraNode.GetComponent<Camera>()!.SetFov(43.0f);

    }

    private StaticModel CreateNatureModel(string name, string modelName, Vector3f position, float yaw,
        float scale, string materialName)
    {
        var node = _scene!.CreateChild(name);
        node.SetPosition(position);
        node.SetRotation(new Quaternion(0.0f, yaw, 0.0f));
        node.SetScale(scale);
        var model = node.CreateComponent<StaticModel>();
        model.SetModel(Resources.Get<Model>($"{NaturePath}Models/{modelName}.mdl"));
        model.SetMaterial(Resources.Get<Material>($"{NaturePath}Materials/{materialName}"));
        model.SetCastShadows(true);
        return model;
    }

    private void HandleMouseMove(ref InputEvents.MouseMoveEvent value)
    {
        // Mouse input may deliver many events between rendered frames. Keep only the latest position so
        // triangle raycasts cannot build an input-event backlog while the pointer moves quickly.
        _pendingPointerPosition = new Vector2i(value.X, value.Y);
        _hoverDirty = true;
    }

    private void UpdateHoveredDrawable()
    {
        var ray = Renderer.GetViewport(0)!.GetScreenRay(_pendingPointerPosition.X, _pendingPointerPosition.Y);
        using var query = new RayOctreeQuery(ray, RayQueryLevel.RayTriangle, 100.0f, DrawableFlags.Geometry, 1);
        _octree!.RaycastSingle(query);

        var hoveredDrawable = query.Result.Count != 0 ? query.Result[0].Drawable : null;
        if (ReferenceEquals(hoveredDrawable, _hoveredDrawable))
            return;

        _hoveredDrawable = hoveredDrawable;
        UpdateHoverOutline();
    }

    private void UpdateHoverOutline()
    {
        _hoverOutlineGroup!.ClearDrawables();
        if (_hoveredDrawable is not null && !ReferenceEquals(_hoveredDrawable, _selectedDrawable))
            _hoverOutlineGroup.AddDrawable(_hoveredDrawable);
    }

    private void HandleMouseButtonDown(ref InputEvents.MouseButtonDownEvent value)
    {
        // Resolve the latest coalesced move before selecting, even if movement and click arrived in one frame.
        if (_hoverDirty)
        {
            _hoverDirty = false;
            UpdateHoveredDrawable();
        }

        if (_hoveredDrawable is null || ReferenceEquals(_hoveredDrawable, _selectedDrawable))
            return;

        _selectionOutlineGroup!.ClearDrawables();
        _selectionOutlineGroup.AddDrawable(_hoveredDrawable);
        _selectedDrawable = _hoveredDrawable;
        UpdateHoverOutline();
    }

    protected override void Update(float timeStep)
    {
        if (Input.ExitRequested || _sampleInput!.Pressed("Exit"))
            Exit();

        if (_hoverDirty)
        {
            _hoverDirty = false;
            UpdateHoveredDrawable();
        }
    }
    protected override void Stop()
    {
        _sampleInput?.Dispose();
        _sampleInput = null;
        _pointerAdapter?.SetEnabled(false);
        _pointerAdapter?.Dispose();
    }
    private void DrawInstructions()
    {
        var windowOpen = true;
        ImGui.SetNextWindowPos(new Vector2f(10.0f, 10.0f), ImGuiCond.ImGuiCondAlways);
        if (ImGui.Begin("Pointer Adapter"u8, ref windowOpen,
                ImGuiWindowFlags.ImGuiWindowFlagsNoResize | ImGuiWindowFlags.ImGuiWindowFlagsNoSavedSettings))
        {
            DrawPromptGroup(0, "Move pointer"u8);
            DrawPromptGroup(4, "Select"u8);
        }
        ImGui.End();
    }

    private void DrawPromptGroup(int firstPrompt, ReadOnlySpan<byte> label)
    {
        switch (Input.ActiveInputDevice)
        {
            case InputDeviceType.Touch:
                ImGui.Image(_controlPrompts![firstPrompt + 1], PromptIconSize);
                break;
            case InputDeviceType.Joystick:
                ImGui.Image(_controlPrompts![firstPrompt + 3], PromptIconSize);
                break;
            default:
                ImGui.Image(_controlPrompts![firstPrompt], PromptIconSize);
                ImGui.SameLine();
                ImGui.Image(_controlPrompts[firstPrompt + 2], PromptIconSize);
                break;
        }
        ImGui.SameLine();
        Widgets.AlignTextToLastItem();
        ImGui.TextUnformatted(label);
    }

    private Texture2D[]? _controlPrompts;
    private static readonly Vector2f PromptIconSize = new(24.0f, 24.0f);
    private SampleInputMap? _sampleInput;
}
