//
// Copyright (c) 2017-2020 the rbfx project.
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
//

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
