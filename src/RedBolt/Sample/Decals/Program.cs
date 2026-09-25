// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Sample.Decals;

using System;

using RedBolt.Bindings.DearImGui;
using RedBolt.Core;
using RedBolt.DearImGui;
using RedBolt.Engine;
using RedBolt.Graphics;
using RedBolt.Input;
using RedBolt.Math;
using RedBolt.Scene;
using Widgets = RedBolt.SystemUI.Widgets;

internal static class DecalsProgram
{
    private static int Main()
    {
        try
        {
            return new DecalsApplication().Run();
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }
}

/// <summary>Decals example.</summary>
sealed class DecalsApplication : Application
{
    protected override void Setup(EngineParameters parameters)
    {
        parameters.ApplicationName = "RedBolt.Sample.Decals";
        parameters.Borderless = false;
        parameters.FullScreen = false;
        parameters.ResourcePaths = "CoreData;SampleData";
        parameters.ResourcePrefixPaths = ".;..";
        parameters.WindowMaximize = false;
#if !REDBOLT_PLATFORM_BROWSER
        parameters.WindowWidth = 1280;
        parameters.WindowHeight = 720;
#endif
        parameters.WindowTitle = "RedBolt Decals";
    }

    protected override void Start()
    {
        _sampleInput = new SampleInputMap();
        _sampleInput.AddMouseButton("Fire", MouseButton.Left, ControllerButton.A, "FIRE");
        _sampleInput.AddButton("Clear", Scancode.M, ControllerButton.X, "CLEAR", 1);
        _sampleInput.AddButton("Debug", Scancode.Space, ControllerButton.Y, "DEBUG", 2);
        Context.RegisterSubsystem(new DearImGui((ImGuiConfigFlags)Engine.Parameters.DearImGuiFlags));
        _fireIcon = Resources.Get<Texture2D>("ThirdParty/KenneyInputPrompts/mouse_left.png");
        _clearIcon = Resources.Get<Texture2D>("ThirdParty/KenneyInputPrompts/keyboard_m.png");
        _debugIcon = Resources.Get<Texture2D>("ThirdParty/KenneyInputPrompts/keyboard_space.png");
        SubscribeToEvent<DearImGuiEvents.EndRenderingDearImGuiEvent>(
            (ref DearImGuiEvents.EndRenderingDearImGuiEvent _) => DrawInstructions());

        if (!CreateScene())
        {
            Console.Error.WriteLine("Could not load the decals shooting range.");
            Exit();
            return;
        }
        Renderer.SetViewport(0, new Viewport(_scene!, _cameraNode!.GetComponent<Camera>()!));
        SubscribeToEvent<CoreEvents.PostRenderUpdateEvent>(
            (ref CoreEvents.PostRenderUpdateEvent _) => HandlePostRenderUpdate());
        Input.SetMouseMode(MouseMode.Free);
        Input.SetMouseVisible(false);
    }

    private bool CreateScene()
    {
        _scene = new Scene();
        if (!_scene.LoadFile("Scenes/Decals.scene"))
            return false;
        _octree = _scene.GetComponent<Octree>();
        _cameraNode = _scene.FindChild("Camera");
        _gunNode = _cameraNode?.FindChild("BoxGun");
        return _octree is not null && _cameraNode is not null && _gunNode is not null;
    }

    protected override void Update(float timeStep)
    {
        if (Input.ExitRequested || _sampleInput!.Pressed("Exit"))
        {
            Exit();
            return;
        }

        if (_sampleInput.Pressed("Debug"))
            _drawDebug = !_drawDebug;
        if (_sampleInput.Pressed("Clear"))
            ClearDecals();
        if (_sampleInput.Pressed("Fire"))
            Shoot();

        var aimPosition = GetAimPosition();
        var aimX = Math.Clamp(aimPosition.X - 0.5f, -0.5f, 0.5f);
        var aimY = Math.Clamp(aimPosition.Y - 0.5f, -0.5f, 0.5f);
        _gunNode!.SetRotation(new Quaternion(aimY * 12.0f, aimX * 18.0f, 0.0f));
        _recoil = MathF.Max(0.0f, _recoil - timeStep * 5.0f);
        _gunNode.SetPosition(new Vector3f(0.0f, 0.0f, -0.12f * _recoil));
    }

    private void HandlePostRenderUpdate()
    {
        if (_drawDebug)
            Renderer.DrawDebugGeometry(false);
    }

    private void Shoot()
    {
        var ray = GetAimRay();
        using var query = new RayOctreeQuery(ray, RayQueryLevel.RayTriangle, 100.0f, DrawableFlags.Geometry);
        _octree!.Raycast(query);
        _recoil = 1.0f;

        RayQueryResult? surfaceHit = null;
        foreach (var hit in query.Result)
        {
            if (hit.Drawable is StaticModel && hit.Node is not null && !hit.Node.IsChildOf(_gunNode))
            {
                surfaceHit = hit;
                break;
            }
        }

        if (!surfaceHit.HasValue)
            return;

        var result = surfaceHit.Value;
        var targetNode = result.Node!;
        var decal = targetNode.GetComponent<DecalSet>();
        if (decal is null)
        {
            decal = targetNode.CreateComponent<DecalSet>();
            decal.SetMaterial(Resources.Get<Material>("Materials/TechnicalDemo/BulletHole.xml"));
        }

        var decalDirection = -result.Normal;
        var decalUp = MathF.Abs(decalDirection.DotProduct(Vector3f.Up)) > 0.99f
            ? Vector3f.Forward
            : Vector3f.Up;
        var decalRotation = new Quaternion();
        decalRotation.FromLookRotation(decalDirection, decalUp);
        decal.AddDecal(result.Drawable, result.Position, decalRotation, 0.42f, 1.0f, 0.35f,
            Vector2f.Zero, Vector2f.One);
    }

    private Ray GetAimRay()
    {
        var aimPosition = GetAimPosition();
        return _cameraNode!.GetComponent<Camera>()!.GetScreenRay(aimPosition.X, aimPosition.Y);
    }

    private Vector2f GetAimPosition()
    {
        if (Input.ActiveInputDevice == InputDeviceType.Touch)
        {
            var position = Input.GetTouch(0)!.Value.Position;
            var backbufferSize = Input.GetBackbufferSize();
            return new Vector2f(
                position.X / (float)backbufferSize.X,
                position.Y / (float)backbufferSize.Y);
        }

        return Input.GetRelativeMousePosition();
    }

    private void ClearDecals()
    {
        foreach (var node in _scene!.GetChildren(true))
            node.GetComponent<DecalSet>()?.RemoveAllDecals();
    }

    private void DrawInstructions()
    {
        var windowOpen = true;
        ImGui.SetNextWindowPos(new Vector2f(10.0f, 10.0f), ImGuiCond.ImGuiCondAlways);
        if (ImGui.Begin("Decals"u8, ref windowOpen,
            ImGuiWindowFlags.ImGuiWindowFlagsNoResize | ImGuiWindowFlags.ImGuiWindowFlagsNoSavedSettings))
        {
            DrawPrompt(SampleInputMap.GetActionPrompt(_fireIcon!, ControllerButton.A), "Fire"u8);
            DrawPrompt(SampleInputMap.GetActionPrompt(_clearIcon!, ControllerButton.X), "Clear impacts"u8);
            DrawPrompt(SampleInputMap.GetActionPrompt(_debugIcon!, ControllerButton.Y),
                _drawDebug ? "Hide debug geometry"u8 : "Show debug geometry"u8);
        }
        ImGui.End();

        var viewport = ImGui.GetMainViewport();
        var drawList = ImGui.GetForegroundDrawList(viewport);
        // ImGui uses logical coordinates while input and camera picking use backbuffer pixels.
        // Derive the crosshair from the same normalized aim as the shot, including the active touch.
        var aimPosition = GetAimPosition();
        var center = viewport.Pos + new Vector2f(aimPosition.X * viewport.Size.X, aimPosition.Y * viewport.Size.Y);
        const float gap = 4.0f;
        const float length = 10.0f;
        var color = Color.White.ToUInt();
        drawList.AddLine(center - new Vector2f(length, 0.0f), center - new Vector2f(gap, 0.0f), color, 2.0f);
        drawList.AddLine(center + new Vector2f(gap, 0.0f), center + new Vector2f(length, 0.0f), color, 2.0f);
        drawList.AddLine(center - new Vector2f(0.0f, length), center - new Vector2f(0.0f, gap), color, 2.0f);
        drawList.AddLine(center + new Vector2f(0.0f, gap), center + new Vector2f(0.0f, length), color, 2.0f);
    }

    private static void DrawPrompt(Texture2D texture, ReadOnlySpan<byte> label)
    {
        ImGui.Image(texture, IconSize);
        ImGui.SameLine();
        Widgets.AlignTextToLastItem();
        ImGui.TextUnformatted(label);
    }

    private static readonly Vector2f IconSize = new(24.0f, 24.0f);
    protected override void Stop()
    {
        _sampleInput?.Dispose();
        _sampleInput = null;
        Renderer.SetNumViewports(0);
        _scene?.Dispose();
        _scene = null;
        base.Stop();
    }

    private Scene? _scene;
    private Node? _cameraNode;
    private Node? _gunNode;
    private Octree? _octree;
    private bool _drawDebug;
    private float _recoil;
    private Texture2D? _fireIcon;
    private Texture2D? _clearIcon;
    private Texture2D? _debugIcon;
    private SampleInputMap? _sampleInput;
}
