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
using RedBolt.Container;
using RedBolt.Editor.Core;
using RedBolt.Editor.Project;
using RedBolt.Math;

/// <summary>Tab that displays project settings.</summary>
public class SettingsTab : EditorTab
{
    public SettingsTab()
        : base("Settings", "5123082a-1ded-4de7-bab0-b48a3d56a073",
            EditorTabFlag.None, EditorTabPlacement.DockRight)
    {
    }

    public override void Dispose()
    {
        _label.Dispose();
        base.Dispose();
    }

    /// <summary>Implement EditorTab.</summary>
    public override void ApplyHotkeys(HotkeyManager hotkeyManager)
    {
        if (_selectedGroup is null)
            return;

        foreach (var (_, page) in _selectedGroup.Pages)
            page.ApplyHotkeys(hotkeyManager);
    }

    public override void RenderContent()
    {
        if (_selectedGroup is null || _selectedGroup.Pages.Count == 0)
            _selectNextValidGroup = true;

        const float verticalThresholdIn = 700.0f;
        const float verticalThresholdOut = 750.0f;
        var totalWidth = ImGui.GetContentRegionAvail().X;
        var totalHeight = ImGui.GetContentRegionAvail().Y;

        if (!_useVerticalLayout.HasValue || totalWidth > verticalThresholdOut)
            _useVerticalLayout = false;
        else if (totalWidth < verticalThresholdIn)
            _useVerticalLayout = true;

        if (_useVerticalLayout.Value)
        {
            if (ImGui.BeginChild("##SettingsTree"u8, new Vector2f(totalWidth, totalHeight * 0.3f)))
                RenderSettingsTree();
            ImGui.EndChild();

            ImGui.Separator();

            if (ImGui.BeginChild("##SettingsPage"u8, ImGui.GetContentRegionAvail()))
                RenderCurrentGroup();
            ImGui.EndChild();
        }
        else if (ImGui.BeginTable("##ResourceBrowserTab"u8, 2, ImGuiTableFlags.ImGuiTableFlagsResizable))
        {
            ImGui.TableSetupColumn(
                "Left"u8, ImGuiTableColumnFlags.ImGuiTableColumnFlagsWidthStretch, 0.35f);
            ImGui.TableSetupColumn(
                "Right"u8, ImGuiTableColumnFlags.ImGuiTableColumnFlagsWidthStretch, 0.65f);

            ImGui.TableNextRow();

            ImGui.TableSetColumnIndex(0);
            if (ImGui.BeginChild("##SettingsTree"u8, ImGui.GetContentRegionAvail()))
                RenderSettingsTree();
            ImGui.EndChild();

            ImGui.TableSetColumnIndex(1);
            if (ImGui.BeginChild("##SettingsPage"u8, ImGui.GetContentRegionAvail()))
                RenderCurrentGroup();
            ImGui.EndChild();

            ImGui.EndTable();
        }
    }

    /// <summary>Implement EditorTab.</summary>
    protected override bool IsMarkedUnsaved()
    {
        return GetProject().HasUnsavedChanges;
    }

    private void RenderSettingsTree()
    {
        var project = GetProject();
        var settingsManager = project.SettingsManager;

        var rootGroup = settingsManager.PageTree;
        foreach (var (shortName, childGroup) in rootGroup.Children)
            RenderSettingsSubtree(childGroup, shortName);
    }

    private void RenderSettingsSubtree(SettingsPageGroup group, string shortName)
    {
        _label.Set(shortName);
        ImGui.PushID(_label.ContentBytes);
        try
        {
            if (_selectNextValidGroup && group.Pages.Count != 0)
            {
                _selectNextValidGroup = false;
                _selectedGroup = group;
            }

            var flags = ImGuiTreeNodeFlags.ImGuiTreeNodeFlagsOpenOnArrow
                | ImGuiTreeNodeFlags.ImGuiTreeNodeFlagsOpenOnDoubleClick
                | ImGuiTreeNodeFlags.ImGuiTreeNodeFlagsSpanFullWidth
                | ImGuiTreeNodeFlags.ImGuiTreeNodeFlagsDefaultOpen;
            if (group.Children.Count == 0)
                flags |= ImGuiTreeNodeFlags.ImGuiTreeNodeFlagsLeaf;
            if (ReferenceEquals(group, _selectedGroup))
                flags |= ImGuiTreeNodeFlags.ImGuiTreeNodeFlagsSelected;

            if (_selectNextValidGroup)
                ImGui.SetNextItemOpen(true);
            var isOpen = ImGui.TreeNodeEx(_label.ContentBytes, flags);

            // Process clicking
            var isContextMenuOpen = ImGui.IsItemClicked(ImGuiMouseButton.ImGuiMouseButtonRight);
            if (ImGui.IsItemClicked(ImGuiMouseButton.ImGuiMouseButtonLeft))
            {
                _selectedGroup = group;
                if (group.Pages.Count == 0)
                    _selectNextValidGroup = true;
            }

            // Render children
            if (isOpen)
            {
                foreach (var (childShortName, childGroup) in group.Children)
                    RenderSettingsSubtree(childGroup, childShortName);
                ImGui.TreePop();
            }
        }
        finally
        {
            ImGui.PopID();
        }
    }

    private void RenderCurrentGroup()
    {
        if (_selectedGroup is not null && _selectedGroup.Pages.Count != 0)
        {
            foreach (var (section, page) in _selectedGroup.Pages)
                RenderPage(section, page);
        }
    }

    private void RenderPage(string section, SettingsPage page)
    {
        _label.Set(page.UniqueName);
        ImGui.PushID(_label.ContentBytes);
        try
        {
            if (page.CanResetToDefault)
            {
                if (ImGui.Button("\uf1da##Revert"u8))
                    page.ResetToDefaults();
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Revert settings to default values"u8);
            }

            var showPage = true;
            if (section.Length != 0)
            {
                ImGui.SameLine();
                _label.Set(section);
                showPage = ImGui.CollapsingHeader(
                    _label.ContentBytes, ImGuiTreeNodeFlags.ImGuiTreeNodeFlagsDefaultOpen);
            }

            if (showPage)
                page.RenderSettings();
        }
        finally
        {
            ImGui.PopID();
        }
    }

    private bool _selectNextValidGroup;
    private SettingsPageGroup? _selectedGroup;

    private bool? _useVerticalLayout;
    private readonly CString _label = new();
}
