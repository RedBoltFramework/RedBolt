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
using RedBolt.Editor.Core;
using RedBolt.Editor.Foundation.Shared;
using RedBolt.Editor.Project;
using RedBolt.Scene;
using RedBolt.SystemUI;
using RedBolt.Utility;

/// <summary>Scene hierarchy provider for hierarchy browser tab.</summary>
public class SceneHierarchy : SceneViewAddon, HierarchyBrowserSource
{
    public SceneHierarchy(SceneViewTab sceneViewTab)
        : base(sceneViewTab)
    {
        _widget.OnContextMenu += RenderSelectionContextMenu;
        _widget.OnNodeReordered += ReorderNode;
        _widget.OnComponentReordered += ReorderComponent;
        _widget.OnNodeReparented += ReparentNode;
    }

    public override void Dispose()
    {
        _widget.Dispose();
        base.Dispose();
    }

    /// <summary>Implement SceneViewAddon.</summary>
    public override string GetUniqueName() => "SceneHierarchy";

    public override void WriteIniSettings(ImGuiTextBuffer output)
    {
        var settings = _widget.GetSettings();
        IniHelpers.WriteIntToIni(
            output, "SceneHierarchy.ShowComponents", settings.ShowComponents ? 1 : 0);
        IniHelpers.WriteIntToIni(
            output, "SceneHierarchy.ShowTemporary", settings.ShowTemporary ? 1 : 0);
        IniHelpers.WriteStringToIni(
            output, "SceneHierarchy.FilterByName", settings.FilterByName);
    }

    public override void ReadIniSettings(string line)
    {
        var settings = _widget.GetSettings();
        var intValue = IniHelpers.ReadIntFromIni(line, "SceneHierarchy.ShowComponents");
        if (intValue.HasValue)
            settings.ShowComponents = intValue.Value != 0;
        intValue = IniHelpers.ReadIntFromIni(line, "SceneHierarchy.ShowTemporary");
        if (intValue.HasValue)
            settings.ShowTemporary = intValue.Value != 0;
        var stringValue = IniHelpers.ReadStringFromIni(
            line, "SceneHierarchy.FilterByName");
        if (stringValue is not null)
            settings.FilterByName = stringValue;
        _widget.SetSettings(settings);
    }

    /// <summary>Implement HierarchyBrowserSource.</summary>
    public EditorTab? GetOwnerTab()
    {
        return _owner.TryGetTarget(out var owner) ? owner : null;
    }

    public void RenderContent()
    {
        if (!_owner.TryGetTarget(out var owner)
            || owner.GetActivePage() is not { } activePage)
        {
            return;
        }

        RenderToolbar(activePage);
        if (ImGui.BeginChild("##SceneHierarchy"u8))
            _widget.RenderContent(activePage.Scene, activePage.Selection);
        ImGui.EndChild();
    }

    public void RenderContextMenuItems()
    {
    }

    public void RenderMenu()
    {
        if (_owner.TryGetTarget(out var owner) && !_reentrant)
        {
            _reentrant = true;
            try
            {
                owner.RenderMenu();
            }
            finally
            {
                _reentrant = false;
            }
        }
    }

    public override void ApplyHotkeys(HotkeyManager hotkeyManager)
    {
        if (_owner.TryGetTarget(out var owner) && !_reentrant)
        {
            _reentrant = true;
            try
            {
                owner.ApplyHotkeys(hotkeyManager);
            }
            finally
            {
                _reentrant = false;
            }
        }
    }

    void HierarchyBrowserSource.ApplyHotkeys(HotkeyManager? hotkeyManager)
    {
        if (hotkeyManager is not null)
            ApplyHotkeys(hotkeyManager);
    }

    private void RenderToolbar(SceneViewPage page)
    {
        _ = page;
        var settings = _widget.GetSettings();

        if (Widgets.ToolbarButton(
                "\uf017"u8, "Show Temporary Nodes & Components"u8,
                settings.ShowTemporary))
        {
            settings.ShowTemporary = !settings.ShowTemporary;
        }
        if (Widgets.ToolbarButton(
                "\uf542"u8, "Show Components"u8, settings.ShowComponents))
        {
            settings.ShowComponents = !settings.ShowComponents;
        }

        ImGui.BeginDisabled();
        Widgets.ToolbarButton("\uf002"u8);
        ImGui.EndDisabled();

        Widgets.InputText("##Rename"u8, ref settings.FilterByName);

        _widget.SetSettings(settings);
        if (_owner.TryGetTarget(out var owner))
            owner.SetComponentSelection(settings.ShowComponents);
    }

    private void RenderSelectionContextMenu(Scene scene, SceneSelection selection)
    {
        if (!_owner.TryGetTarget(out var owner))
            return;

        ImGui.Separator();
        owner.RenderEditMenu(scene, selection);
        ImGui.Separator();
        owner.RenderCreateMenu(scene, selection);
    }

    private void ReorderNode(Node node, int oldIndex, int newIndex)
    {
        var parentNode = node.Parent!;
        parentNode.ReorderChild(node, newIndex);
        if (_owner.TryGetTarget(out var owner))
            owner.PushAction(new ReorderNodeAction(node, oldIndex, newIndex));
    }

    private void ReorderComponent(
        Component component, int oldIndex, int newIndex)
    {
        var node = component.Node;
        node.ReorderComponent(component, newIndex);
        if (_owner.TryGetTarget(out var owner))
        {
            owner.PushAction(new ReorderComponentAction(
                component, oldIndex, newIndex));
        }
    }

    private void ReparentNode(Node parentNode, Node childNode)
    {
        if (_owner.TryGetTarget(out var owner))
            owner.PushAction(new ReparentNodeAction(childNode, parentNode));
        childNode.SetParent(parentNode);
    }

    private bool _reentrant;
    private readonly SceneHierarchyWidget _widget = new();
}
