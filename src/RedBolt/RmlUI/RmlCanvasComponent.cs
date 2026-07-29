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

namespace RedBolt.RmlUI;

using System.Collections.Generic;
using System.Runtime.CompilerServices;

using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.Logging;
using RedBolt.Math;
using RedBolt.RenderAPI;
using RedBolt.Resource;
using RedBolt.Scene;
using EngineLog = RedBolt.Logging.Log;

/// <summary>Renders off-screen UI into a texture.</summary>
[Reflected(ObjectCategory.Category_RmlUI)]
public partial class RmlCanvasComponent : LogicComponent
{
    private const int UIComponentDefaultTextureSize = 512;
    private const int UIComponentMinTextureSize = 64;
    private const int UIComponentMaxTextureSize = 4096;

    /// <summary>Construct.</summary>
    public RmlCanvasComponent()
    {
        _offScreenUI = new RmlUI($"RmlTextureComponent_{RuntimeHelpers.GetHashCode(this):x}");
        _offScreenUI.OnMouseMoveEvent += RemapMousePos;
        _texture = new Texture2D();
        SetUpdateEventMask(UpdateEvent.UseUpdate);
    }

    /// <summary>Destruct.</summary>
    public override void Dispose()
    {
        // Unload document first so other components can receive events about document invalidation and null their pointers. This process
        // depends on RmlUI instance being alive.
        _offScreenUI.GetRmlContext().UnloadAllDocuments();
        _offScreenUI.OnMouseMoveEvent -= RemapMousePos;
        _offScreenUI.Dispose();
        _texture = null;
        base.Dispose();
    }

    /// <summary>Set size of texture UI will be rendered into.</summary>
    public void SetUISize(Vector2i size)
    {
        System.Diagnostics.Debug.Assert(_texture is not null);
        if (size.X < UIComponentMinTextureSize || size.X > UIComponentMaxTextureSize ||
            size.Y < UIComponentMinTextureSize || size.Y > UIComponentMaxTextureSize)
        {
            EngineLog.GetLogger().Error("RmlCanvasComponent: Invalid texture size {}x{}", size.X, size.Y);
            return;
        }

        if (_texture!.SetSize(size.X, size.Y, TextureFormat.TexFormatRgba8Unorm, TextureFlag.BindRenderTarget))
        {
            var surface = _texture.GetRenderSurface()!;
            surface.SetUpdateMode(RenderSurfaceUpdateMode.ManualUpdate);
            _offScreenUI.SetRenderTarget(surface, _clearColor);
        }
        else
        {
            _offScreenUI.SetRenderTarget();
            SetEnabled(false);
            EngineLog.GetLogger().Error("RmlCanvasComponent: Resizing of UI render-target texture failed.");
        }
        ClearTexture();
    }

    /// <summary>Set texture canvas will render into.</summary>
    public void SetTexture(Texture2D? texture)
    {
        _texture = texture;
        if (_texture is not null)
        {
            _texture.SetFilterMode(TextureFilterMode.Bilinear);
            _texture.SetAddressMode(TextureCoordinate.U, TextureAddressMode.Clamp);
            _texture.SetAddressMode(TextureCoordinate.V, TextureAddressMode.Clamp);
            _texture.SetNumLevels(1);  // No mipmaps
        }
    }

    /// <summary>Return texture where UI is rendered into.</summary>
    public Texture2D? GetTexture() => _texture;

    /// <summary>Enable input remapping (use if canvas renders on 3D objects).</summary>
    public void SetRemapMousePos(bool remap) => _remapMousePos = remap;

    /// <summary>Return whether input remapping is enabled.</summary>
    public bool GetRemapMousePos() => _remapMousePos;

    public void SetClearColor(Color clearColor) => _clearColor = clearColor;

    public Color GetClearColor() => _clearColor;

    /// <summary>Return off-screen RmlUI instance.</summary>
    public RmlUI GetUI() => _offScreenUI;

    /// <summary>Set texture (for attribute).</summary>
    protected void SetTextureRef(ResourceRef texture)
    {
        var cache = GetSubsystem<ResourceCache>()!;
        if (cache.GetResource(texture.Type, texture.Name) is Resource textureResource)
        {
            if (textureResource is Texture2D texture2D)
            {
                SetTexture(texture2D);
                SetUISize(new Vector2i(UIComponentDefaultTextureSize, UIComponentDefaultTextureSize));    // TODO: To attribute
            }
            else
                EngineLog.GetLogger().Error("Resource with name {} exists, but is not a Texture2D.", texture.Name);
        }
        else
            EngineLog.GetLogger().Error("Resource with name {} is already registered.", texture.Name);
    }

    /// <summary>Get texture (for attribute).</summary>
    [Serialized("Texture", DefaultSource = nameof(GetTextureRefDefault), Setter = nameof(SetTextureRef))]
    protected ResourceRef GetTextureRef()
    {
        if (_texture is null)
            return new ResourceRef(ObjectReflection<Texture2D>.TypeId);
        return new ResourceRef(ObjectReflection<Texture2D>.TypeId, _texture.GetName());
    }

    /// <summary>Handle component being added to Node or removed from it.</summary>
    protected override void OnNodeSet(Node? previousNode, Node? currentNode)
    {
        if (_node is null)
            ClearTexture();
    }

    /// <summary>Handle component being enabled or disabled.</summary>
    public override void OnSetEnabled()
    {
        if (!_enabled)
            ClearTexture();
        _offScreenUI.SetRendering(_enabled);
        _offScreenUI.SetBlockEvents(!_enabled);
    }

    /// <summary>Set texture color to transparent.</summary>
    protected void ClearTexture()
    {
        if (_texture is null)
            return;

        using var clear = new Image();
        var width = _texture.Width;
        var height = _texture.Height;
        if (width > 0 && height > 0)
        {
            clear.SetSize(width, height, 4);
            clear.Clear(_clearColor);
            _texture.SetData(0, 0, 0, width, height, clear.GetData());
        }
    }

    /// <summary>Convert screen coordinates to context-local coordinates of RmlUI instance.</summary>
    protected void RemapMousePos(ref Vector2i screenPos)
    {
        if (!_remapMousePos || _node is null)
            return;

        var ui = GetSubsystem<RmlUI>();
        if (ui is not null)
        {
            var context = ui.GetRmlContext();
            var hoverElement = context.GetHoverElement();
            if (!ui.GetBlockEvents() && hoverElement?.Handle != context.GetRootElement().Handle)
            {
                // Cursor hovers UI rendered into backbuffer. Do not process any input here.
                screenPos = new Vector2i(-1, -1);
                return;
            }
        }

        var scene = _node.Scene;
        var model = _node.GetComponent<StaticModel>();
        var renderer = GetSubsystem<Renderer>();
        var octree = scene?.GetComponent<Octree>();
        if (scene is null || model is null || renderer is null || octree is null)
            return;

        Viewport? viewport = null;
        for (uint i = 0; i < renderer.GetNumViewports(); ++i)
        {
            var candidate = renderer.GetViewport(i);
            if (candidate is not null)
            {
                var rect = candidate.GetRect();
                if (ReferenceEquals(candidate.GetScene(), scene))
                {
                    if (rect == IntRect.Zero)
                    {
                        // Save full-screen viewport only if we do not have a better smaller candidate.
                        if (viewport is null)
                            viewport = candidate;
                    }
                    else if (rect.Contains(screenPos))
                        // Small viewports override full-screen one (picture-in-picture situation).
                        viewport = candidate;
                    break;
                }
            }
        }

        if (viewport is null)
            return;

        var camera = viewport.GetCamera();
        if (camera is null)
            return;

        var viewportRect = viewport.GetRect();
        if (viewportRect == IntRect.Zero)
        {
            var graphics = GetSubsystem<Graphics>()!;
            viewportRect.Right = graphics.Width;
            viewportRect.Bottom = graphics.Height;
        }

        var ray = camera.GetScreenRay(
            (float)screenPos.X / viewportRect.Width,
            (float)screenPos.Y / viewportRect.Height);
        var queryResults = new List<RayQueryResult>();
        using var query = new RayOctreeQuery(
            queryResults, ray, RayQueryLevel.RayTriangleUv, float.PositiveInfinity,
            DrawableFlags.Geometry, Drawable.DefaultViewMask);
        octree.Raycast(query);
        if (queryResults.Count == 0)
            return;

        foreach (var queryResult in queryResults)
        {
            if (!ReferenceEquals(queryResult.Drawable, model))
            {
                // ignore billboard sets by default
                if (queryResult.Drawable is BillboardSet)
                    continue;
                return;
            }

            var uv = queryResult.TextureUV;
            var uiSize = _offScreenUI.GetRmlContext().GetDimensions();
            screenPos = new Vector2i((int)(uv.X * uiSize.X), (int)(uv.Y * uiSize.Y));
        }
    }

    private static object? GetTextureRefDefault() => new ResourceRef(ObjectReflection<Texture2D>.TypeId);

    /// <summary>Texture that UIElement will be rendered into.</summary>
    private Texture2D? _texture;
    /// <summary>Subsystem that handles UI rendering to the texture.</summary>
    private readonly RmlUI _offScreenUI;
    /// <summary>Flag indicating that this component remaps mouse position on to a sibling StaticModel if present.</summary>
    [Serialized("Remap Mouse Position")]
    private bool _remapMousePos = true;
    /// <summary>Color to clear texture to</summary>
    [Serialized("Clear Color")]
    private Color _clearColor = Color.TransparentBlack;
}
