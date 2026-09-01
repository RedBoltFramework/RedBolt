// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Sample.HelloGUI;

using System;

using RedBolt;
using RedBolt.Bindings.DearImGui;
using RedBolt.Container;
using RedBolt.Core;
using RedBolt.DearImGui;
using RedBolt.Engine;
using RedBolt.Graphics;
using RedBolt.Input;
using RedBolt.Math;
using RedBolt.Scene;

internal static class HelloGUIProgram
{
    private static int Main()
    {
        try
        {
            using var application = new HelloGUI();
            var exitCode = application.Run();
            if (exitCode == 0)
                Console.WriteLine("RedBolt HelloGUI sample finished.");
            return exitCode;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }
}

/// <summary>A live material preview controlled by a Dear ImGui inspector.</summary>
public class HelloGUI : Application
{
    private const int LogoWidth = 220;
    private const int LogoHeight = 90;

    protected override void Start()
    {
        _sampleInput = new SampleInputMap();
        base.Start();

        Context.RegisterSubsystem(new DearImGui((ImGuiConfigFlags)Engine.Parameters.DearImGuiFlags));
        SubscribeToEvent<DearImGuiEvents.EndRenderingDearImGuiEvent>(
            (ref DearImGuiEvents.EndRenderingDearImGuiEvent _) => DrawUI());

        _logoTexture = Resources.GetResourceOrNull<Texture2D>("Textures/Logo.png");
        CreateScene();

        Input.SetMouseMode(MouseMode.Free);
        Input.SetMouseVisible(true);
    }

    private void CreateScene()
    {
        _scene = new Scene();
        _scene.CreateComponent<Octree>();

        var zone = _scene.CreateComponent<Zone>();
        zone.SetBoundingBox(new BoundingBox(-1000.0f, 1000.0f));
        zone.SetAmbientColor(new Color(0.12f, 0.14f, 0.2f));
        zone.SetFogColor(new Color(0.035f, 0.045f, 0.07f));
        zone.SetFogStart(12.0f);
        zone.SetFogEnd(28.0f);

        var skyNode = _scene.CreateChild("Sky");
        var sky = skyNode.CreateComponent<Skybox>();
        sky.SetModel(Resources.Get<Model>("Models/Box.mdl"));
        sky.SetMaterial(Resources.Get<Material>("Materials/Skybox.xml"));

        var floorNode = _scene.CreateChild("Floor");
        floorNode.SetScale(new Vector3f(30.0f, 1.0f, 30.0f));
        var floor = floorNode.CreateComponent<StaticModel>();
        floor.SetModel(Resources.Get<Model>("Models/Plane.mdl"));
        floor.SetMaterial(Resources.Get<Material>("Materials/StoneTiled.xml"));

        var pedestalNode = _scene.CreateChild("Pedestal");
        pedestalNode.SetPosition(new Vector3f(-1.3f, 0.35f, 0.0f));
        pedestalNode.SetScale(new Vector3f(2.8f, 0.35f, 2.8f));
        var pedestal = pedestalNode.CreateComponent<StaticModel>();
        pedestal.SetModel(Resources.Get<Model>("Models/Cylinder.mdl"));
        pedestal.SetMaterial(Resources.Get<Material>("Materials/Stone.xml"));
        pedestal.SetCastShadows(true);

        _objectNode = _scene.CreateChild("Material Preview");
        _objectNode.SetPosition(new Vector3f(-1.3f, 1.25f, 0.0f));
        _objectNode.SetScale(_objectScale);
        _previewModel = _objectNode.CreateComponent<StaticModel>();
        _previewModel.SetModel(Resources.Get<Model>("Models/MaterialPreview.mdl"));
        _previewModel.SetCastShadows(_castShadows);

        _previewMaterial = new Material();
        _previewMaterial.SetTechnique(0, Resources.Get<Technique>("Techniques/PBR/PBRNoTexture.xml"));
        _previewModel.SetMaterial(_previewMaterial);
        ApplyMaterial();

        var keyLightNode = _scene.CreateChild("Key Light");
        keyLightNode.SetDirection(new Vector3f(0.5f, -1.0f, 0.7f));
        _keyLight = keyLightNode.CreateComponent<Light>();
        _keyLight.SetLightType(LightType.Directional);
        _keyLight.SetColor(new Color(1.0f, 0.82f, 0.68f));
        _keyLight.SetBrightness(_lightBrightness);
        _keyLight.SetCastShadows(true);

        var rimLightNode = _scene.CreateChild("Rim Light");
        rimLightNode.SetPosition(new Vector3f(-4.0f, 3.5f, -2.0f));
        var rimLight = rimLightNode.CreateComponent<Light>();
        rimLight.SetLightType(LightType.Point);
        rimLight.SetColor(new Color(0.2f, 0.65f, 1.0f));
        rimLight.SetBrightness(1.4f);
        rimLight.SetRange(9.0f);

        var cameraNode = _scene.CreateChild("Camera");
        cameraNode.SetPosition(new Vector3f(0.2f, 2.7f, -7.5f));
        cameraNode.LookAt(new Vector3f(-1.3f, 1.15f, 0.0f));
        var camera = cameraNode.CreateComponent<Camera>();
        camera.SetFarClip(40.0f);

        _viewport = new Viewport(_scene, camera);
        Renderer.SetViewport(0, _viewport);
    }

    protected override void Update(float timeStep)
    {
        if (Input.ExitRequested || _sampleInput!.Pressed("Exit"))
        {
            Exit();
            return;
        }

        if (_autoRotate)
            _objectNode?.Rotate(new Quaternion(0.0f, _rotationSpeed * timeStep, 0.0f), TransformSpace.World);
    }

    private void DrawUI()
    {
        if (_resourcesReleased || !_windowOpen)
            return;

        var windowSize = new Vector2f(
            Math.Min(390.0f, Math.Max(1.0f, Graphics.Width - 20.0f)),
            Math.Min(475.0f, Math.Max(1.0f, Graphics.Height - 20.0f)));
        var windowPosition = new Vector2f(
            Math.Max(10.0f, Graphics.Width - windowSize.X - 20.0f),
            Math.Max(10.0f, (Graphics.Height - windowSize.Y) * 0.5f));
        ImGui.SetNextWindowSize(windowSize, ImGuiCond.ImGuiCondAlways);
        ImGui.SetNextWindowPos(windowPosition, ImGuiCond.ImGuiCondAlways);

        var visible = ImGui.Begin(
            "Material Studio"u8,
            ref _windowOpen,
            ImGuiWindowFlags.ImGuiWindowFlagsNoMove |
            ImGuiWindowFlags.ImGuiWindowFlagsNoResize |
            ImGuiWindowFlags.ImGuiWindowFlagsNoSavedSettings);
        if (visible)
            DrawInspector();
        ImGui.End();

        if (!_windowOpen)
            Exit();
    }

    private void DrawInspector()
    {
        ImGui.PushStyleColor(ImGuiCol.ImGuiColChildBg, new Vector4f(0.055f, 0.065f, 0.085f, 1.0f));
        if (ImGui.BeginChild("##Brand"u8, new Vector2f(0.0f, 118.0f), ImGuiChildFlags.ImGuiChildFlagsBorders))
        {
            if (_logoTexture is not null)
            {
                var logoPosition = ImGui.GetCursorScreenPos();
                logoPosition.X += Math.Max(0.0f, (ImGui.GetContentRegionAvail().X - LogoWidth) * 0.5f);
                ImGui.SetCursorScreenPos(logoPosition);
                ImGui.Image(_logoTexture, new Vector2f(LogoWidth, LogoHeight));
            }
            else
            {
                ImGui.TextUnformatted("RedBolt 3D Game Engine"u8);
            }
        }
        ImGui.EndChild();
        ImGui.PopStyleColor();

        ImGui.PushStyleColor(ImGuiCol.ImGuiColText, new Vector4f(0.35f, 0.85f, 1.0f, 1.0f));
        ImGui.TextUnformatted("LIVE MATERIAL PREVIEW"u8);
        ImGui.PopStyleColor();
        ImGui.TextUnformatted("Tune the object and see every change in the scene."u8);

        ImGui.SeparatorText("Material"u8);
        Span<float> baseColor = [_baseColor.R, _baseColor.G, _baseColor.B];
        if (ImGui.ColorEdit3("Base color"u8, baseColor))
        {
            _baseColor = new Color(baseColor[0], baseColor[1], baseColor[2]);
            ApplyMaterial();
        }

        ImGui.SetNextItemWidth(235.0f);
        if (ImGui.SliderFloat("Metallic"u8, ref _metallic, 0.0f, 1.0f))
            ApplyMaterial();
        ImGui.SetNextItemWidth(235.0f);
        if (ImGui.SliderFloat("Roughness"u8, ref _roughness, 0.05f, 1.0f))
            ApplyMaterial();

        ImGui.SeparatorText("Presentation"u8);
        ImGui.Checkbox("Auto rotate"u8, ref _autoRotate);
        ImGui.SetNextItemWidth(235.0f);
        ImGui.SliderFloat("Rotation speed"u8, ref _rotationSpeed, 0.0f, 90.0f);

        ImGui.SetNextItemWidth(235.0f);
        if (ImGui.SliderFloat("Object scale"u8, ref _objectScale, 0.65f, 1.6f))
            _objectNode?.SetScale(_objectScale);
        ImGui.SetNextItemWidth(235.0f);
        if (ImGui.SliderFloat("Key light"u8, ref _lightBrightness, 0.2f, 2.5f))
            _keyLight?.SetBrightness(_lightBrightness);
        if (ImGui.Checkbox("Cast shadows"u8, ref _castShadows))
            _previewModel?.SetCastShadows(_castShadows);

        ImGui.Separator();
        if (ImGui.Button("Reset material"u8))
            ResetPreview();
        ImGui.SameLine();
        ImGui.TextUnformatted("Drag a slider to update the scene."u8);
    }

    private void ApplyMaterial()
    {
        _previewMaterial?.SetShaderParameter(
            "MatDiffColor", new Vector4f(_baseColor.R, _baseColor.G, _baseColor.B, 1.0f));
        _previewMaterial?.SetShaderParameter("Metallic", _metallic);
        _previewMaterial?.SetShaderParameter("Roughness", _roughness);
    }

    private void ResetPreview()
    {
        _baseColor = new Color(0.92f, 0.12f, 0.09f);
        _metallic = 0.2f;
        _roughness = 0.35f;
        _rotationSpeed = 24.0f;
        _objectScale = 1.25f;
        _lightBrightness = 1.6f;
        _castShadows = true;
        _autoRotate = true;

        _objectNode?.SetScale(_objectScale);
        _keyLight?.SetBrightness(_lightBrightness);
        _previewModel?.SetCastShadows(_castShadows);
        ApplyMaterial();
    }

    protected override void Setup(EngineParameters parameters)
    {
        parameters.ApplicationName = "RedBolt.Sample.HelloGUI";
        parameters.Borderless = false;
        parameters.FullScreen = false;
        parameters.WindowMaximize = false;
        parameters.ResourcePaths = "CoreData;SampleData";
        parameters.ResourcePrefixPaths = ".;..";
#if !REDBOLT_PLATFORM_BROWSER
        parameters.WindowHeight = 720;
        parameters.WindowWidth = 1280;
#endif
        parameters.WindowTitle = "RedBolt Hello GUI";
    }

    protected override void Stop()
    {
        _sampleInput?.Dispose();
        _sampleInput = null;
        ReleaseResources();
        base.Stop();
    }

    public override void Dispose()
    {
        ReleaseResources();
        base.Dispose();
    }

    private void ReleaseResources()
    {
        if (_resourcesReleased)
            return;

        _resourcesReleased = true;
        if (_scene is not null)
            Renderer.SetNumViewports(0);
        _viewport?.Dispose();
        _viewport = null;
        _scene?.Dispose();
        _scene = null;
        _objectNode = null;
        _keyLight = null;
        _previewModel = null;
        _previewMaterial?.Dispose();
        _previewMaterial = null;
        _logoTexture = null;
    }

    private Scene? _scene;
    private Node? _objectNode;
    private Light? _keyLight;
    private StaticModel? _previewModel;
    private Material? _previewMaterial;
    private Viewport? _viewport;
    private Texture2D? _logoTexture;
    private Color _baseColor = new(0.92f, 0.12f, 0.09f);
    private float _metallic = 0.2f;
    private float _roughness = 0.35f;
    private float _rotationSpeed = 24.0f;
    private float _objectScale = 1.25f;
    private float _lightBrightness = 1.6f;
    private bool _castShadows = true;
    private bool _autoRotate = true;
    private bool _windowOpen = true;
    private bool _resourcesReleased;
    private SampleInputMap? _sampleInput;
}
