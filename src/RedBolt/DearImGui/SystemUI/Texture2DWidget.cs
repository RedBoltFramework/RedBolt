// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.SystemUI;

using RedBolt.Bindings.DearImGui;
using RedBolt.Graphics;
using RedBolt.Math;

/// <summary>SystemUI widget to preview texture.</summary>
public class Texture2DWidget : BaseWidget
{
    public Texture2DWidget(Texture2D? resource)
    {
        _resource = resource;
    }

    public override void Dispose()
    {
        _textureReference.Dispose();
        _resource = null;
        base.Dispose();
    }

    public override void RenderContent()
    {
        var texture = GetTexture2D();
        if (texture is null)
            return;

        var contentPosition = ImGui.GetCursorPos();
        var region = ImGui.GetContentRegionAvail();
        var contentSize = new Vector2f(region.X, region.X);
        var previewSize = Widgets.FitContent(
            contentSize, new Vector2f(texture.Width, texture.Height));
        ImGui.SetCursorPos(contentPosition + new Vector2f((contentSize.X - previewSize.X) * 0.5f, 0.0f));
        Widgets.Image(texture, _textureReference, previewSize);
    }

    public Texture2D? GetTexture2D() => _resource;

    private Texture2D? _resource;
    private readonly ImTextureRef _textureReference = new();
}
