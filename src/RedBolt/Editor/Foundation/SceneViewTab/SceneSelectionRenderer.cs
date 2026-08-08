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

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

using RedBolt.Bindings.DearImGui;
using RedBolt.Editor.Core;
using RedBolt.Graphics;
using RedBolt.IO;
using RedBolt.Math;
using RedBolt.Scene;

/// <summary>Add-on to manage scene selection with mouse and render debug geometry.</summary>
public class SceneSelectionRenderer : SceneViewAddon
{
    public const uint DirectSelectionRenderOrder = 255;
    public const uint IndirectSelectionRenderOrder = 254;

    public struct Settings : ISimpleSettingsPageValues
    {
        public Settings()
        {
            DirectSelectionColor = new Color(0xfd5602u, Color.RGB);
            IndirectSelectionColor = new Color(0x009dffu, Color.RGB);
            DebugGeometryDepthTest = false;
        }

        public string UniqueName => "Editor.Scene:Selection";

        public void SerializeInBlock(Archive archive)
        {
            ArchiveSerialization.SerializeOptionalValue(
                archive, "DirectSelectionColor", ref DirectSelectionColor,
                new Settings().DirectSelectionColor, ArchiveSerialization.SerializeValue);
            ArchiveSerialization.SerializeOptionalValue(
                archive, "IndirectSelectionColor", ref IndirectSelectionColor,
                new Settings().IndirectSelectionColor, ArchiveSerialization.SerializeValue);
            ArchiveSerialization.SerializeOptionalValue(
                archive, "DebugGeometryDepthTest", ref DebugGeometryDepthTest,
                new Settings().DebugGeometryDepthTest, ArchiveSerialization.SerializeValue);
        }

        public void RenderSettings()
        {
            Span<float> directColor =
                [DirectSelectionColor.R, DirectSelectionColor.G, DirectSelectionColor.B];
            if (ImGui.ColorEdit3("Direct Selection"u8, directColor))
            {
                DirectSelectionColor = new Color(
                    directColor[0], directColor[1], directColor[2], DirectSelectionColor.A);
            }

            Span<float> indirectColor =
                [IndirectSelectionColor.R, IndirectSelectionColor.G, IndirectSelectionColor.B];
            if (ImGui.ColorEdit3("Indirect Selection"u8, indirectColor))
            {
                IndirectSelectionColor = new Color(
                    indirectColor[0], indirectColor[1], indirectColor[2], IndirectSelectionColor.A);
            }
            ImGui.Checkbox("Depth Test for Debug Geometry"u8, ref DebugGeometryDepthTest);
        }

        public Color DirectSelectionColor;
        public Color IndirectSelectionColor;
        public bool DebugGeometryDepthTest;
    }

    public class SettingsPage : SimpleSettingsPage<Settings>
    {
    }

    public struct PageState
    {
        public WeakReference<OutlineGroup>? DirectSelection;
        public WeakReference<OutlineGroup>? IndirectSelection;
        public uint CurrentRevision;
    }

    public SceneSelectionRenderer(SceneViewTab owner, SettingsPage settings)
        : base(owner)
    {
        _settings = new WeakReference<SettingsPage>(settings);
    }

    public override void Dispose()
    {
        _settings.SetTarget(null!);
        base.Dispose();
    }

    /// <summary>Implement SceneViewAddon.</summary>
    public override string GetUniqueName() => "SelectionRenderer";

    public override void Render(SceneViewPage scenePage)
    {
        if (!_settings.TryGetTarget(out var settings))
            return;
        ref readonly var config = ref settings.Values;
        ref var state = ref GetOrInitializeState(scenePage);

        if (PrepareInternalComponents(scenePage, ref state))
        {
            state.DirectSelection!.TryGetTarget(out var directSelection);
            state.IndirectSelection!.TryGetTarget(out var indirectSelection);
            directSelection!.SetColor(config.DirectSelectionColor);
            indirectSelection!.SetColor(config.IndirectSelectionColor);

            if (state.CurrentRevision != scenePage.Selection.Revision)
            {
                state.CurrentRevision = scenePage.Selection.Revision;
                UpdateInternalComponents(scenePage, ref state);
            }
        }

        var scene = scenePage.Scene;
        foreach (var nodeReference in scenePage.Selection.Nodes)
        {
            if (nodeReference.TryGetTarget(out var node))
                DrawNodeSelection(scene, node, !ReferenceEquals(node, node.Scene));
        }

        foreach (var componentReference in scenePage.Selection.Components)
        {
            if (componentReference.TryGetTarget(out var component))
                DrawComponentSelection(scene, component);
        }
    }

    public override bool RenderTabContextMenu()
    {
        if (ImGui.MenuItem("Draw Debug Geometry"u8, default, _drawDebugGeometry))
            _drawDebugGeometry = !_drawDebugGeometry;
        return true;
    }

    public override void WriteIniSettings(ImGuiTextBuffer output)
    {
        IniHelpers.WriteIntToIni(
            output, "SceneSelectionRenderer.DrawDebugGeometry", _drawDebugGeometry ? 1 : 0);
    }

    public override void ReadIniSettings(string line)
    {
        var value = IniHelpers.ReadIntFromIni(line, "SceneSelectionRenderer.DrawDebugGeometry");
        if (value.HasValue)
            _drawDebugGeometry = value.Value != 0;
    }

    private ref PageState GetOrInitializeState(SceneViewPage scenePage)
    {
        ref var stateWrapped = ref scenePage.GetAddonData(this);
        stateWrapped ??= new PageState();
        return ref Unsafe.Unbox<PageState>(stateWrapped);
    }

    private bool PrepareInternalComponents(SceneViewPage scenePage, ref PageState state)
    {
        var scene = scenePage.Scene;
        var directSelection = GetTarget(state.DirectSelection);
        var indirectSelection = GetTarget(state.IndirectSelection);
        if (directSelection is null || indirectSelection is null)
        {
            directSelection?.Remove();
            indirectSelection?.Remove();

            state.CurrentRevision = 0;

            // Remove all existing temporary debug outline groups.
            var outlineGroups = new List<OutlineGroup>();
            scene.GetComponents(outlineGroups);
            outlineGroups.RemoveAll(static group => !group.IsTemporary || !group.IsDebug());
            foreach (var group in outlineGroups)
                group.Remove();

            // Create new outline groups.
            directSelection = scene.CreateComponent<OutlineGroup>();
            directSelection.SetRenderOrder(DirectSelectionRenderOrder);
            directSelection.SetTemporary(true);
            directSelection.SetDebug(true);
            state.DirectSelection = new WeakReference<OutlineGroup>(directSelection);

            indirectSelection = scene.CreateComponent<OutlineGroup>();
            indirectSelection.SetRenderOrder(IndirectSelectionRenderOrder);
            indirectSelection.SetTemporary(true);
            indirectSelection.SetDebug(true);
            state.IndirectSelection = new WeakReference<OutlineGroup>(indirectSelection);
        }

        return directSelection is not null && indirectSelection is not null;
    }

    private void UpdateInternalComponents(SceneViewPage scenePage, ref PageState state)
    {
        var scene = scenePage.Scene;
        var directSelection = GetTarget(state.DirectSelection)!;
        var indirectSelection = GetTarget(state.IndirectSelection)!;

        directSelection.ClearDrawables();

        foreach (var componentReference in scenePage.Selection.Components)
        {
            if (componentReference.TryGetTarget(out var component) && component.TryGetNode() is { } node)
                AddNodeDrawablesToGroup(node, directSelection);
        }

        foreach (var nodeReference in scenePage.Selection.Nodes)
        {
            if (nodeReference.TryGetTarget(out var node))
                AddNodeDrawablesToGroup(node, directSelection);
        }

        indirectSelection.ClearDrawables();

        foreach (var componentReference in scenePage.Selection.Components)
        {
            if (componentReference.TryGetTarget(out var component)
                && component.TryGetNode() is { } node
                && !ReferenceEquals(node, scene))
            {
                AddNodeChildrenDrawablesToGroup(node, indirectSelection, directSelection);
            }
        }

        foreach (var nodeReference in scenePage.Selection.Nodes)
        {
            if (nodeReference.TryGetTarget(out var node) && !ReferenceEquals(node, scene))
                AddNodeChildrenDrawablesToGroup(node, indirectSelection, directSelection);
        }
    }

    private void AddNodeDrawablesToGroup(
        Node node, OutlineGroup group, OutlineGroup? excludeGroup = null)
    {
        foreach (var outlinedComponent in node.Components)
        {
            if (outlinedComponent is Drawable drawable)
            {
                if (excludeGroup is not null && excludeGroup.ContainsDrawable(drawable))
                    continue;
                group.AddDrawable(drawable);
            }
        }
    }

    private void AddNodeChildrenDrawablesToGroup(
        Node node, OutlineGroup group, OutlineGroup? excludeGroup = null)
    {
        foreach (var child in node.Children)
        {
            AddNodeDrawablesToGroup(child, group, excludeGroup);
            AddNodeChildrenDrawablesToGroup(child, group, excludeGroup);
        }
    }

    private bool NeedDepthTest(Component component)
    {
        // TODO: Customize per component type.
        return _settings.TryGetTarget(out var settings) && settings.Values.DebugGeometryDepthTest;
    }

    private void DrawNodeSelection(Scene scene, Node node, bool recursive)
    {
        foreach (var component in node.Components)
            DrawComponentSelection(scene, component);

        if (recursive)
        {
            foreach (var child in node.Children)
                DrawNodeSelection(scene, child, true);
        }
    }

    private void DrawComponentSelection(Scene scene, Component component)
    {
        if (!_drawDebugGeometry)
            return;
        var debugRenderer = scene.GetComponent<DebugRenderer>();
        component.DrawDebugGeometry(debugRenderer, NeedDepthTest(component));
    }

    private static OutlineGroup? GetTarget(WeakReference<OutlineGroup>? reference)
    {
        return reference is not null
            && reference.TryGetTarget(out var target)
            && target.TryGetNode() is not null
                ? target
                : null;
    }

    private readonly WeakReference<SettingsPage> _settings;
    private bool _drawDebugGeometry = true;
}
