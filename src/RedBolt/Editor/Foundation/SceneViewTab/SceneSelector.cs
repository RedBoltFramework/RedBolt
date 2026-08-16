// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Editor.Foundation;

using RedBolt.Bindings.DearImGui;
using RedBolt.Graphics;
using RedBolt.Input;
using RedBolt.Math;
using RedBolt.Scene;
using RedBolt.Utility;

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
