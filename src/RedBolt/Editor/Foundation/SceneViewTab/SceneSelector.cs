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

namespace RedBolt.Editor.Foundation;

using RedBolt.Bindings.DearImGui;
using RedBolt.Graphics;
using RedBolt.Input;
using RedBolt.Math;
using RedBolt.Scene;
using RedBolt.Utility;

// TODO: port Foundation_SceneSelector when editor plugins are ported.
// void Foundation_SceneSelector(Context* context, SceneViewTab* sceneViewTab)
// {
//     sceneViewTab->RegisterAddon<SceneSelector>();
// }

/// <summary>Add-on to manage scene selection with mouse and render debug geometry.</summary>
public class SceneSelector : SceneViewAddon
{
    public SceneSelector(SceneViewTab owner)
        : base(owner)
    {
    }

    /// <summary>Implement SceneViewAddon.</summary>
    public override string GetUniqueName() => "Selector";

    public override int GetInputPriority() => int.MinValue;

    public override void ProcessInput(SceneViewPage scenePage, ref bool mouseConsumed)
    {
        var scene = scenePage.Scene;

        if (!mouseConsumed)
        {
            if (ImGui.IsItemHovered()
                && ImGui.IsMouseReleased(ImGuiMouseButton.ImGuiMouseButtonLeft)
                && !ImGui.IsMouseDragPastThreshold(ImGuiMouseButton.ImGuiMouseButtonLeft))
            {
                mouseConsumed = true;
                var selectedNode = QuerySelectedNode(scene, scenePage.CameraRay);

                var toggle = IsKeyDown(Key.LeftCtrl) || IsKeyDown(Key.RightCtrl);
                var append = IsKeyDown(Key.LeftShift) || IsKeyDown(Key.RightShift);
                SelectNode(scenePage.Selection, selectedNode, toggle, append);
            }
        }
    }

    private Drawable? QuerySelectedDrawable(
        Scene scene, Ray cameraRay, RayQueryLevel level)
    {
        // Keep native behavior: level is currently ignored by rbfx.
        _ = level;
        var results = SceneViewFunctions.QueryGeometriesFromScene(scene, cameraRay);

        foreach (var result in results)
        {
            if (result.Drawable is { Scene: not null } drawable)
                return drawable;
        }

        return null;
    }

    private Node? QuerySelectedNode(Scene scene, Ray cameraRay)
    {
        var selectedDrawable = QuerySelectedDrawable(
            scene, cameraRay, RayQueryLevel.RayTriangle);
        selectedDrawable ??= QuerySelectedDrawable(
            scene, cameraRay, RayQueryLevel.RayObb);

        var selectedNode = selectedDrawable?.TryGetNode();

        while (selectedNode is not null && selectedNode.IsTemporary)
            selectedNode = selectedNode.Parent;

        return selectedNode;
    }

    private void SelectNode(
        SceneSelection selection, Node? node, bool toggle, bool append)
    {
        selection.ConvertToNodes();

        if (toggle)
            selection.SetSelected(node, !selection.IsSelected(node));
        else if (append)
            selection.SetSelected(node, true);
        else
        {
            selection.Clear();
            selection.SetSelected(node, true);
        }
    }

    private static bool IsKeyDown(Key key)
    {
        var imguiKey = ImGui.ImGuiImplSDL2KeyEventToImGuiKey(
            (int)key, (int)Input.GetScancodeFromKey(key));
        return ImGui.IsKeyDown(imguiKey);
    }
}
