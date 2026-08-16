// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Utility;

using System;

using Microsoft.Extensions.Logging;

using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.Logging;
using RedBolt.Math;
using RedBolt.RenderAPI;
using RedBolt.Scene;

/// <summary>Maintains texture usable as custom backbuffer.</summary>
public class CustomBackbufferTexture : RedObject
{
    public event Action<CustomBackbufferTexture, RenderSurface>? OnRenderSurfaceCreated;

    public CustomBackbufferTexture()
    {
        _texture = new Texture2D();
    }

    public override void Dispose()
    {
        _texture.Dispose();
        OnRenderSurfaceCreated = null;
        base.Dispose();
    }

    /// <summary>Resize output texture.</summary>
    public void SetTextureSize(Vector2i size)
    {
        if (size.X == 0 || size.Y == 0)
        {
            Log.GetLogger().LogError("Invalid texture size.");
            return;
        }

        if (_textureSize != size)
        {
            _textureSize = size;
            _textureDirty = true;
        }
    }

    /// <summary>Set whether to update texture every frame.</summary>
    public void SetActive(bool active)
    {
        _isActive = active;
        var renderSurface = _texture.GetRenderSurface();
        if (renderSurface is not null)
        {
            renderSurface.SetUpdateMode(
                _isActive ? RenderSurfaceUpdateMode.UpdateAlways : RenderSurfaceUpdateMode.ManualUpdate);
        }
    }

    /// <summary>Periodical update.</summary>
    public void Update()
    {
        if (_textureDirty)
        {
            _textureDirty = false;
            _texture.SetSize(
                _textureSize.X, _textureSize.Y, TextureFormat.TexFormatRgba8Unorm,
                TextureFlag.BindRenderTarget);
            var renderSurface = _texture.GetRenderSurface();
            if (renderSurface is not null)
            {
                OnRenderSurfaceCreated?.Invoke(this, renderSurface);
                renderSurface.SetUpdateMode(
                    _isActive ? RenderSurfaceUpdateMode.UpdateAlways : RenderSurfaceUpdateMode.ManualUpdate);
            }
        }
    }

    /// <summary>Return properties</summary>
    public Texture2D Texture => _texture;

    public Vector2i TextureSize => _textureSize;

    public bool IsActive => _isActive;

    private bool _textureDirty;
    private bool _isActive;
    private Vector2i _textureSize;
    private readonly Texture2D _texture;
}

/// <summary>Renders scene to texture with its own camera.</summary>
public class SceneRendererToTexture : CustomBackbufferTexture
{
    public SceneRendererToTexture(Scene? scene)
    {
        _scene = new WeakReference<Scene>(scene!);
        _cameraNode = new Node();
        _camera = _cameraNode.CreateComponent<Camera>();
        _viewport = new Viewport(scene, _camera);

        OnRenderSurfaceCreated += SetupViewport;
    }

    public override void Dispose()
    {
        _viewport?.Dispose();
        _viewport = null;
        _camera = null;
        _cameraNode?.Dispose();
        _cameraNode = null;
        _scene.SetTarget(null!);
        base.Dispose();
    }

    /// <summary>Return properties</summary>
    public Scene? Scene => _scene.TryGetTarget(out var scene) ? scene : null;

    public Camera? Camera => _camera;

    public Node? CameraNode => _cameraNode;

    public Vector3f CameraPosition => _cameraNode!.GetWorldPosition();

    public Quaternion CameraRotation => _cameraNode!.GetWorldRotation();

    private void SetupViewport(CustomBackbufferTexture sender, RenderSurface? renderSurface)
    {
        renderSurface!.SetViewport(0, _viewport);
    }

    private readonly WeakReference<Scene> _scene;
    private Node? _cameraNode;
    private Camera? _camera;

    private Viewport? _viewport;
}
