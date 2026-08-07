//
// Copyright (c) 2022-2022 the rbfx project.
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
