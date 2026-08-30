// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Sample.RenderToTexture;

using System;
using System.Collections.Generic;

using RedBolt;
using RedBolt.Bindings.DearImGui;
using RedBolt.Core;
using RedBolt.DearImGui;
using RedBolt.Engine;
using RedBolt.Graphics;
using RedBolt.Input;
using RedBolt.Math;
using RedBolt.RenderAPI;
using RedBolt.RenderPipeline;
using RedBolt.Resource;
using RedBolt.Scene;
using Widgets = RedBolt.SystemUI.Widgets;

internal static class RenderToTextureProgram
{
    private static int Main()
    {
        try
        {
            using var application = new RenderToTexture();
            var exitCode = application.Run();
            if (exitCode == 0)
                Console.WriteLine("RedBolt RenderToTexture sample finished.");
            return exitCode;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }
}

public class RenderToTexture : Application
{
    protected override void Start()
    {
        base.Start();
        CreateScene();
        CreateInstructions();
        SetupViewport();
        Input.SetMouseMode(MouseMode.Free);
        Input.SetMouseVisible(true);
    }

    private void CreateScene()
    {
        var cache = Resources;
        _scene = new Scene();
        _scene.CreateComponent<Octree>();

        var zone = _scene.CreateChild("Zone").CreateComponent<Zone>();
        zone.SetBoundingBox(new BoundingBox(-1000.0f, 1000.0f));
        zone.SetAmbientColor(new Color(0.32f, 0.35f, 0.42f));
        zone.SetFogColor(new Color(0.08f, 0.1f, 0.14f));
        zone.SetFogStart(45.0f);
        zone.SetFogEnd(80.0f);

        var lightNode = _scene.CreateChild("DirectionalLight");
        lightNode.SetDirection(new Vector3f(0.45f, -1.0f, 0.35f));
        var light = lightNode.CreateComponent<Light>();
        light.SetLightType(LightType.Directional);
        light.SetColor(new Color(0.8f, 0.84f, 0.92f));
        light.SetCastShadows(true);
        light.SetShadowBias(new BiasParameters(0.00025f, 0.5f));

        var fillNode = _scene.CreateChild("MirrorFillLight");
        fillNode.SetPosition(new Vector3f(0.0f, 5.0f, 10.0f));
        var fill = fillNode.CreateComponent<Light>();
        fill.SetLightType(LightType.Point);
        fill.SetColor(new Color(1.0f, 0.62f, 0.18f));
        fill.SetRange(16.0f);
        fill.SetBrightness(0.7f);

        var floorNode = _scene.CreateChild("Floor");
        floorNode.SetScale(new Vector3f(30.0f, 1.0f, 28.0f));
        var floor = floorNode.CreateComponent<StaticModel>();
        floor.SetModel(cache.Get<Model>("Models/Plane.mdl"));
        floor.SetMaterial(cache.Get<Material>("Materials/TechnicalDemo/SquareFloorGrid.xml"));

        var backWallNode = _scene.CreateChild("BackWall");
        backWallNode.SetPosition(new Vector3f(0.0f, 4.0f, MirrorPlaneZ + 0.08f));
        backWallNode.SetRotation(new Quaternion(-90.0f, 0.0f, 0.0f));
        backWallNode.SetScale(new Vector3f(30.0f, 1.0f, 8.0f));
        var backWall = backWallNode.CreateComponent<StaticModel>();
        backWall.SetModel(cache.Get<Model>("Models/Plane.mdl"));
        backWall.SetMaterial(cache.Get<Material>("Materials/TechnicalDemo/BackWallGrid.xml"));
        backWall.SetViewMask(MainViewMask);

        var sideWallMaterial = cache.Get<Material>("Materials/TechnicalDemo/SideWallGrid.xml");
        CreateBox("LeftWall", new Vector3f(-15.0f, 4.0f, 0.0f), new Vector3f(0.3f, 8.0f, 28.0f), sideWallMaterial, true);
        CreateBox("RightWall", new Vector3f(15.0f, 4.0f, 0.0f), new Vector3f(0.3f, 8.0f, 28.0f), sideWallMaterial, true);
        CreateBox("FrontWall", new Vector3f(0.0f, 4.0f, -14.0f), new Vector3f(30.0f, 8.0f, 0.3f), sideWallMaterial, true);
        _collisionBoxes.Add(new CollisionBox(0.0f, MirrorPlaneZ, 30.0f, 0.3f));

        var darkMaterial = cache.Get<Material>("Materials/TechnicalDemo/Dark.xml");
        var accentMaterial = cache.Get<Material>("Materials/TechnicalDemo/Accent.xml");
        CreateBox("MirrorFrameLeft", new Vector3f(-5.05f, 4.0f, MirrorPlaneZ - 0.15f), new Vector3f(0.45f, 6.4f, 0.35f), darkMaterial).SetViewMask(MainViewMask);
        CreateBox("MirrorFrameRight", new Vector3f(5.05f, 4.0f, MirrorPlaneZ - 0.15f), new Vector3f(0.45f, 6.4f, 0.35f), darkMaterial).SetViewMask(MainViewMask);
        CreateBox("MirrorFrameTop", new Vector3f(0.0f, 7.0f, MirrorPlaneZ - 0.15f), new Vector3f(10.55f, 0.45f, 0.35f), darkMaterial).SetViewMask(MainViewMask);
        CreateBox("MirrorFrameBottom", new Vector3f(0.0f, 1.0f, MirrorPlaneZ - 0.15f), new Vector3f(10.55f, 0.45f, 0.35f), darkMaterial).SetViewMask(MainViewMask);
        CreateBox("MirrorAccent", new Vector3f(0.0f, 7.28f, MirrorPlaneZ - 0.34f), new Vector3f(5.2f, 0.08f, 0.12f), accentMaterial).SetViewMask(MainViewMask);

        CreateBox("TestBlockLeft", new Vector3f(-9.0f, 0.75f, 4.0f), new Vector3f(2.4f, 1.5f, 2.4f), darkMaterial, true);
        CreateBox("TestBlockRight", new Vector3f(8.0f, 1.4f, 7.0f), new Vector3f(3.0f, 2.8f, 3.0f), accentMaterial, true);
        CreateBox("TestColumn", new Vector3f(-10.5f, 2.25f, 10.0f), new Vector3f(1.2f, 4.5f, 1.2f), accentMaterial, true);

        CreateCharacter(cache);

        _cameraNode = _scene.CreateChild("Camera");
        var camera = _cameraNode.CreateComponent<Camera>();
        camera.SetFarClip(80.0f);
        void UpdateShadowCascades() => light.SetShadowCascade(ShadowCascadeEstimator.Estimate(camera,
            new Vector2i(Graphics.Width, Graphics.Height), new RenderPipelineSettings(),
            new ShadowCascadeEstimateSettings(60.0f) { ShadowResolution = light.GetShadowResolution() }));
        UpdateShadowCascades();
        SubscribeToEvent<GraphicsEvents.ScreenModeEvent>(
            (ref GraphicsEvents.ScreenModeEvent _) => UpdateShadowCascades());

        _reflectionCameraNode = _scene.CreateChild("ReflectionCamera");
        var reflectionCamera = _reflectionCameraNode.CreateComponent<Camera>();
        reflectionCamera.SetFarClip(80.0f);
        reflectionCamera.SetFov(2.0f * MathF.Atan(MirrorHeight * 0.5f / MirrorCameraDistance) / MathDefs.DegToRad);
        reflectionCamera.SetViewMask(ReflectionViewMask);
        reflectionCamera.SetAutoAspectRatio(false);
        reflectionCamera.SetAspectRatio(MirrorWidth / MirrorHeight);
        _reflectionCameraNode.SetPosition(new Vector3f(0.0f, MirrorCenterY, MirrorPlaneZ + MirrorCameraDistance));
        _reflectionCameraNode.LookAt(new Vector3f(0.0f, MirrorCenterY, MirrorPlaneZ), Vector3f.Up);

        CreateMirror(cache);
        UpdateCameras();
    }

    private void CreateCharacter(ResourceCache cache)
    {
        _characterNode = _scene!.CreateChild("TechnicalDummy");
        _characterNode.SetPosition(new Vector3f(0.0f, 0.0f, -2.0f));
        var model = _characterNode.CreateComponent<AnimatedModel>();
        model.SetModel(cache.Get<Model>("ThirdParty/Quaternius/TechnicalDummy.glb.d/Models/Mannequin.mdl"));
        var material = cache.Get<Material>("Materials/TechnicalDemo/Yellow.xml");
        model.SetMaterial(0, material);
        model.SetMaterial(1, material);
        model.SetCastShadows(true);

        _walkAnimation = cache.Get<Animation>("ThirdParty/Quaternius/TechnicalDummy.glb.d/Animations/Walk_Loop.ani");
        _animationController = _characterNode.CreateComponent<AnimationController>();
        _animationController.PlayNewExclusive(new AnimationParameters(_walkAnimation).Looped());
        _animationController.SetSpeed(_walkAnimation.Name, 0.0f);
    }

    private void CreateMirror(ResourceCache cache)
    {
        var mirrorNode = _scene!.CreateChild("MirrorSurface");
        mirrorNode.SetPosition(new Vector3f(0.0f, MirrorCenterY, MirrorPlaneZ - 0.04f));
        mirrorNode.SetRotation(new Quaternion(-90.0f, 0.0f, 0.0f));
        mirrorNode.SetScale(new Vector3f(MirrorWidth, 1.0f, MirrorHeight));
        var mirror = mirrorNode.CreateComponent<StaticModel>();
        mirror.SetModel(cache.Get<Model>("Models/Plane.mdl"));
        mirror.SetViewMask(MainViewMask);

        _renderTexture = new Texture2D();
        _renderTexture.SetSize(1024, 576, TextureFormat.TexFormatRgba8Unorm, TextureFlag.BindRenderTarget, 4);
        _renderTexture.SetFilterMode(TextureFilterMode.Bilinear);

        _renderMaterial = new Material();
        _renderMaterial.SetTechnique(0, cache.Get<Technique>("Techniques/DiffUnlit.xml"));
        _renderMaterial.SetTexture("Albedo", _renderTexture);
        _renderMaterial.SetShaderParameter("UOffset", new Vector4f(-1.0f, 0.0f, 0.0f, 1.0f));
        _renderMaterial.SetDepthBias(new BiasParameters(-0.001f, 0.0f));
        mirror.SetMaterial(_renderMaterial);

        var surface = _renderTexture.GetRenderSurface();
        surface!.SetViewport(0, new Viewport(_scene, _reflectionCameraNode!.GetComponent<Camera>()!));
    }

    private StaticModel CreateBox(string name, Vector3f position, Vector3f scale, Material material, bool collidable = false)
    {
        var node = _scene!.CreateChild(name);
        node.SetPosition(position);
        node.SetScale(scale);
        var model = node.CreateComponent<StaticModel>();
        model.SetModel(Resources.Get<Model>("Models/Box.mdl"));
        model.SetMaterial(material);
        model.SetCastShadows(true);
        if (collidable)
            _collisionBoxes.Add(new CollisionBox(position.X, position.Z, scale.X, scale.Z));
        return model;
    }

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

        if (Input.GetMouseButtonDown(MouseButton.Right))
        {
            var mouseMove = Input.GetMouseMove();
            _cameraYaw += mouseMove.X * 0.12f;
            _cameraPitch = MathDefs.Clamp(_cameraPitch + mouseMove.Y * 0.12f, -10.0f, 42.0f);
        }

        var yawRadians = _cameraYaw * MathDefs.DegToRad;
        var forward = new Vector3f(MathF.Sin(yawRadians), 0.0f, MathF.Cos(yawRadians));
        var right = new Vector3f(forward.Z, 0.0f, -forward.X);
        var move = Vector3f.Zero;
        if (Input.GetScancodeDown(Scancode.W))
            move += forward;
        if (Input.GetScancodeDown(Scancode.S))
            move -= forward;
        if (Input.GetScancodeDown(Scancode.A))
            move -= right;
        if (Input.GetScancodeDown(Scancode.D))
            move += right;

        if (move.LengthSquared > 0.0f)
        {
            move.Normalize();
            var previousPosition = _characterNode!.Position;
            var position = MoveCharacter(previousPosition, move * (CharacterSpeed * timeStep));
            _characterNode.SetPosition(position);
            var actualMove = position - previousPosition;
            if (actualMove.LengthSquared > 0.0f)
            {
                _characterNode.LookAt(position + actualMove, Vector3f.Up);
                _animationController!.SetSpeed(_walkAnimation!.Name, 1.4f);
            }
            else
                _animationController!.SetSpeed(_walkAnimation!.Name, 0.0f);
        }
        else
            _animationController!.SetSpeed(_walkAnimation!.Name, 0.0f);

        UpdateCameras();
    }

    private Vector3f MoveCharacter(Vector3f position, Vector3f movement)
    {
        var candidate = new Vector3f(position.X + movement.X, position.Y, position.Z);
        if (!Collides(candidate))
            position.X = candidate.X;
        candidate = new Vector3f(position.X, position.Y, position.Z + movement.Z);
        if (!Collides(candidate))
            position.Z = candidate.Z;
        return position;
    }

    private bool Collides(Vector3f position)
    {
        for (var i = 0; i < _collisionBoxes.Count; ++i)
        {
            var box = _collisionBoxes[i];
            if (position.X > box.MinX - CharacterRadius && position.X < box.MaxX + CharacterRadius
                && position.Z > box.MinZ - CharacterRadius && position.Z < box.MaxZ + CharacterRadius)
                return true;
        }
        return false;
    }

    private void UpdateCameras()
    {
        var target = _characterNode!.Position + new Vector3f(0.0f, 1.45f, 0.0f);
        var yaw = _cameraYaw * MathDefs.DegToRad;
        var pitch = _cameraPitch * MathDefs.DegToRad;
        var direction = new Vector3f(MathF.Sin(yaw) * MathF.Cos(pitch), -MathF.Sin(pitch), MathF.Cos(yaw) * MathF.Cos(pitch));
        var cameraPosition = target - direction * CameraDistance;
        _cameraNode!.SetPosition(cameraPosition);
        _cameraNode.LookAt(target, Vector3f.Up);
    }

    private void DrawInstructions()
    {
        var windowOpen = true;
        ImGui.SetNextWindowPos(new Vector2f(10.0f, 10.0f), ImGuiCond.ImGuiCondAlways);
        if (ImGui.Begin("Render To Texture"u8, ref windowOpen,
            ImGuiWindowFlags.ImGuiWindowFlagsNoResize | ImGuiWindowFlags.ImGuiWindowFlagsNoSavedSettings))
        {
            for (var i = 0; i < 3; ++i)
            {
                ImGui.Image(_controlPrompts![i], PromptIconSize);
                ImGui.SameLine();
            }
            DrawPrompt(_controlPrompts![3], "Move dummy"u8);
            DrawPrompt(_controlPrompts[4], "Orbit camera"u8);
            ImGui.Separator();
            ImGui.TextUnformatted("The wall mirror is rendered to a texture."u8);
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

    protected override void Stop()
    {
        ReleaseScene();
        base.Stop();
    }

    protected override void Setup(EngineParameters parameters)
    {
        parameters.ApplicationName = "RedBolt.Sample.RenderToTexture";
        parameters.Borderless = false;
        parameters.FullScreen = false;
        parameters.WindowMaximize = false;
        parameters.ResourcePaths = "CoreData;SampleData";
        parameters.ResourcePrefixPaths = ".;..";
#if !REDBOLT_PLATFORM_BROWSER
        parameters.WindowHeight = 720;
        parameters.WindowWidth = 1280;
#endif
        parameters.WindowTitle = "RedBolt Render To Texture";
    }

    public override void Dispose()
    {
        ReleaseScene();
        base.Dispose();
    }

    private void ReleaseScene()
    {
        if (_scene is not null)
            Renderer.SetNumViewports(0);
        _reflectionCameraNode = null;
        _cameraNode = null;
        _characterNode = null;
        _animationController = null;
        _walkAnimation = null;
        _scene?.Dispose();
        _scene = null;
        _renderMaterial?.Dispose();
        _renderMaterial = null;
        _renderTexture?.Dispose();
        _renderTexture = null;
    }

    private const uint ReflectionViewMask = 1;
    private const uint MainViewMask = 2;
    private const float MirrorPlaneZ = 13.7f;
    private const float MirrorCenterY = 4.0f;
    private const float MirrorWidth = 9.6f;
    private const float MirrorHeight = 5.4f;
    private const float MirrorCameraDistance = 12.0f;
    private const float CharacterSpeed = 4.5f;
    private const float CharacterRadius = 0.45f;
    private const float CameraDistance = 8.0f;
    private static readonly Vector2f PromptIconSize = new(24.0f, 24.0f);

    private Scene? _scene;
    private Node? _cameraNode;
    private Node? _reflectionCameraNode;
    private Node? _characterNode;
    private Animation? _walkAnimation;
    private AnimationController? _animationController;
    private Texture2D? _renderTexture;
    private Material? _renderMaterial;
    private Texture2D[]? _controlPrompts;
    private float _cameraYaw;
    private float _cameraPitch = 14.0f;
    private readonly List<CollisionBox> _collisionBoxes = [];

    private readonly struct CollisionBox
    {
        public CollisionBox(float centerX, float centerZ, float sizeX, float sizeZ)
        {
            MinX = centerX - sizeX * 0.5f;
            MaxX = centerX + sizeX * 0.5f;
            MinZ = centerZ - sizeZ * 0.5f;
            MaxZ = centerZ + sizeZ * 0.5f;
        }

        public float MinX { get; }
        public float MaxX { get; }
        public float MinZ { get; }
        public float MaxZ { get; }
    }
}
