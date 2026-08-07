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

namespace RedBolt.SystemUI;

using System;
using System.Text;

using RedBolt.Bindings.DearImGui;
using RedBolt.Core;
using RedBolt.Math;

public static class Widgets
{
    /// <summary>Underline previously rendered item (usually text).</summary>
    public static void Underline(Color color)
    {
        var minimum = ImGui.GetItemRectMin();
        var maximum = ImGui.GetItemRectMax();
        using var drawList = ImGui.GetWindowDrawList();
        drawList.AddLine(new Vector2f(minimum.X, maximum.Y), maximum, color.ToUInt(), 1.0f);
    }

    /// <summary>Render clickable URL text.</summary>
    public static void TextURL(ReadOnlySpan<byte> label, ReadOnlySpan<byte> url)
    {
        using var style = ImGui.GetStyle();
        using var colors = style.Colors;

        ImGui.TextUnformatted(label);
        Underline(new Color(colors[(int)ImGuiCol.ImGuiColText]));

        var isHovered = ImGui.IsItemHovered();
        var isOpened = isHovered && ImGui.IsMouseClicked(ImGuiMouseButton.ImGuiMouseButtonLeft);

        if (isHovered)
        {
            ImGui.SetTooltip(url);
            ImGui.SetMouseCursor(ImGuiMouseCursor.ImGuiMouseCursorHand);
        }

        if (isOpened)
            ProcessUtils.OpenURL(Encoding.UTF8.GetString(url));
    }
}
