// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.SystemUI;

using System;

using RedBolt.Bindings.DearImGui;
using RedBolt.Graphics;
using RedBolt.Input;
using RedBolt.Math;
using RedBolt.RenderPipeline;
using RedBolt.Resource;
using RedBolt.Scene;
using RedBolt.Utility;

/// <summary>SystemUI widget to preview scene.</summary>
public class SceneWidget : BaseWidget
{
    public SceneWidget()
    {
    }

    public override void Dispose()
    {
        _textureReference.Dispose();
        _lightNode = null;
        _lightPivotNode = null;
        _renderer?.Dispose();
        _renderer = null;
        _scene?.Dispose();
        _scene = null;
        base.Dispose();
    }

    public override void RenderContent()
    {
        if (_scene is null)
            return;

        var renderer = GetRenderer()!;

        if (!_scene.HasComponent<DebugRenderer>())
        {
            var debug = _scene.GetOrCreateComponent<DebugRenderer>();
            debug.SetTemporary(true);
            debug.SetLineAntiAlias(true);
        }
        var contentPosition = ImGui.GetCursorPos();

        if (_lightPivotNode is not null)
        {
            var cameraNode = renderer.CameraNode!;
            _lightPivotNode.SetRotation(cameraNode.GetWorldRotation());
        }

        var contentSize = ImGui.GetContentRegionAvail();
        renderer.SetTextureSize(Vector2f.RoundToInt(contentSize));
        renderer.Update();

        var sceneTexture = renderer.Texture;
        ImGui.SetCursorPos(contentPosition);
        var imageBegin = ImGui.GetCursorPos();
        Widgets.ImageItem(
            sceneTexture, _textureReference, new Vector2f(sceneTexture.Width, sceneTexture.Height));
        var imageEnd = ImGui.GetCursorPos();

        // Enable basic orbit controls similar to MaterialInspectorWidget
        if (renderer.Camera is { } camera)
        {
            var cameraNode = camera.Node;
            var moveAndOrbit = cameraNode.GetOrCreateComponent<MoveAndOrbitComponent>();

            var distance = cameraNode.GetPosition().Length;
            // Interact only when the image is hovered
            if (ImGui.IsItemHovered())
            {
                if (ImGui.IsMouseDown(ImGuiMouseButton.ImGuiMouseButtonRight))
                {
                    var mouseDelta = ImGui.GetIO().MouseDelta;
                    moveAndOrbit.SetYaw(moveAndOrbit.GetYaw() + mouseDelta.X * 0.9f);
                    moveAndOrbit.SetPitch(moveAndOrbit.GetPitch() + mouseDelta.Y * 0.9f);
                }

                if (MathF.Abs(ImGui.GetIO().MouseWheel) > 0.05f)
                {
                    if (ImGui.GetIO().MouseWheel > 0.0f)
                        distance *= 0.8f;
                    else
                        distance *= 1.3f;
                }
            }
            distance = MathDefs.Clamp(distance, moveAndOrbit.GetMinDistance(), moveAndOrbit.GetMaxDistance());

            cameraNode.SetRotation(moveAndOrbit.GetYawPitchRotation());
            cameraNode.SetPosition(distance * (cameraNode.GetRotation() * Vector3f.Back));
        }
    }

    public Scene? GetScene() => _scene;

    public SceneRendererToTexture? GetRenderer()
    {
        if (_scene is null)
            return null;

        if (_renderer is null)
        {
            _renderer = new SceneRendererToTexture(_scene);
            _renderer.SetActive(true);
        }
        return _renderer;
    }

    public Camera? GetCamera() => GetRenderer() is not null ? GetRenderer()!.Camera : null;

    public Scene CreateDefaultScene()
    {
        var cache = GetSubsystem<ResourceCache>();
        _renderer?.Dispose();
        _renderer = null;
        var scene = new Scene();
        _scene?.Dispose();
        _scene = scene;
        _scene.CreateComponent<Octree>();
        var zone = _scene.CreateComponent<Zone>();
        var skybox = _scene.CreateComponent<Skybox>();
        zone.SetBoundingBox(new BoundingBox(Vector3f.One * -1000.0f, Vector3f.One * 1000.0f));
        skybox.SetModel(cache.Get<Model>("Models/Box.mdl"));
        skybox.SetMaterial(cache.Get<Material>("Materials/DefaultSkybox.xml"));
        zone.SetZoneTexture(cache.Get<TextureCube>("Textures/DefaultSkybox.xml"));
        _lightPivotNode = _scene.CreateChild("DirectionalLightPivot");
        _lightNode = _lightPivotNode.CreateChild("DirectionalLight");
        var light = _lightNode.CreateComponent<Light>();
        light.SetLightType(LightType.Directional);
        light.SetCastShadows(true);
        _lightNode.LookAt(Vector3f.Forward - Vector3f.Up);
        return _scene;
    }

    public void SetSkyboxTexture(Texture? texture)
    {
        var scene = GetScene();
        if (scene is null)
            return;

        var zone = scene.FindComponent<Zone>();
        if (zone is not null)
            zone.SetZoneTexture(texture);
        var skybox = scene.FindComponent<Skybox>();
        if (skybox is not null)
        {
            var material = new Material();
            var cache = GetSubsystem<ResourceCache>();
            material.SetTechnique(0, cache.Get<Technique>("Techniques/DiffSkybox.xml"));
            material.SetTexture(ShaderResources.Albedo, texture);
            material.SetCullMode(CullMode.None);
            skybox.SetMaterial(material);
        }
    }

    public void LookAt(in BoundingBox box)
    {
        var camera = GetCamera();
        if (camera is null)
            return;

        var node = camera.Node;
        node.SetPosition(box.Center + box.Size.Length * new Vector3f(1.0f, 1.0f, 1.0f));
        node.LookAt(box.Center);
    }

    private Scene? _scene;
    private SceneRendererToTexture? _renderer;
    private Node? _lightPivotNode;
    private Node? _lightNode;
    private readonly ImTextureRef _textureReference = new();
}
