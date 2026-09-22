// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.SystemUI;

using System;
using System.Collections.Generic;

using RedBolt.Bindings.DearImGui;
using RedBolt.Container;
using RedBolt.Core;
using RedBolt.Input;
using RedBolt.Math;
using RedBolt.Scene;
using RedBolt.Utility;

public struct SceneHierarchySettings
{
    public bool ShowTemporary;
    public bool ShowComponents = true;
    public string FilterByName = string.Empty;

    public SceneHierarchySettings()
    {
    }
}

/// <summary>Widget to render scene hierarchy.</summary>
public class SceneHierarchyWidget : RedObject
{
    public event Action<Scene, SceneSelection>? OnContextMenu;
    public event Action<Node, int, int>? OnNodeReordered;
    public event Action<Component, int, int>? OnComponentReordered;
    public event Action<Node, Node>? OnNodeReparented;

    public override void Dispose()
    {
        OnContextMenu = null;
        OnNodeReordered = null;
        OnComponentReordered = null;
        OnNodeReparented = null;
        _lastActiveObject = null;
        _pathToActiveObject.Clear();
        _rangeSelection.Clear();
        _search.Clear();
        _pendingNodeReparents.Clear();
        _nodeTitle.Dispose();
        _componentTitle.Dispose();
        _summary.Dispose();
        base.Dispose();
    }

    public void SetSettings(SceneHierarchySettings settings)
    {
        _settings = settings;
    }

    public void RenderContent(Scene scene, SceneSelection selection)
    {
        var sceneChanged = !ReferenceEquals(GetTarget(_search.LastScene), scene);
        var queryChanged = _search.CurrentQuery != _settings.FilterByName;
        _search.CurrentQuery = _settings.FilterByName;
        if (queryChanged || sceneChanged)
            UpdateSearchResults(scene);

        ProcessActiveObject(selection.ActiveObject);

        BeginRangeSelection();

        var style = ImGui.GetStyle();
        ImGui.PushStyleVar(
            ImGuiStyleVar.ImGuiStyleVarItemSpacing,
            new Vector2f(style.ItemSpacing.X, 0.0f));
        if (_search.LastQuery.Length == 0)
        {
            RenderNode(selection, scene);
        }
        else
        {
            foreach (var nodeReference in _search.LastResults)
            {
                if (nodeReference.TryGetTarget(out var node)
                    && (_settings.ShowTemporary || !node.IsTemporaryEffective()))
                {
                    RenderNode(selection, node);
                }
            }
        }
        ImGui.PopStyleVar();

        EndRangeSelection(selection);

        RenderContextMenu(scene, selection);

        ApplyPendingUpdates(scene);

        // Suppress future updates if selection was changed by the widget
        _lastActiveObject = selection.ActiveObject is { } activeObject
            ? new WeakReference<RedObject>(activeObject)
            : null;

        // Reset flag just in case
        if (_ignoreNextMouseRelease
            && ImGui.IsMouseReleased(ImGuiMouseButton.ImGuiMouseButtonLeft))
        {
            _ignoreNextMouseRelease = false;
        }
    }

    public SceneHierarchySettings GetSettings() => _settings;

    private void RenderNode(SceneSelection selection, Node node)
    {
        if (!_settings.ShowTemporary && node.IsTemporary)
            return;

        ProcessItemIfActive(selection, node);

        var numItems = GetVisibleItemsCount(
            node, _settings.ShowTemporary, _settings.ShowComponents);
        var flags = ImGuiTreeNodeFlags.ImGuiTreeNodeFlagsOpenOnArrow
            | ImGuiTreeNodeFlags.ImGuiTreeNodeFlagsOpenOnDoubleClick
            | ImGuiTreeNodeFlags.ImGuiTreeNodeFlagsSpanAvailWidth
            | ImGuiTreeNodeFlags.ImGuiTreeNodeFlagsAllowOverlap;
        if (node.Parent is null)
            flags |= ImGuiTreeNodeFlags.ImGuiTreeNodeFlagsDefaultOpen;
        if (selection.IsSelected(node))
            flags |= ImGuiTreeNodeFlags.ImGuiTreeNodeFlagsSelected;
        if (numItems == 0)
            flags |= ImGuiTreeNodeFlags.ImGuiTreeNodeFlagsLeaf;
        if (numItems <= 2)
            flags |= ImGuiTreeNodeFlags.ImGuiTreeNodeFlagsDefaultOpen;
        var itemFlags = HierarchyItemFlag.Node;
        if (node.IsTemporaryEffective())
            itemFlags |= HierarchyItemFlag.Temporary;
        if (node.IsEnabled)
            itemFlags |= HierarchyItemFlag.Enabled;

        if (_pathToActiveObject.Contains(node))
            ImGui.SetNextItemOpen(true);

        using var guard = new IdScopeGuard(unchecked((int)node.Id));
        ImGui.PushStyleColor(ImGuiCol.ImGuiColText, GetItemColor(itemFlags));
        _nodeTitle.Set(GetNodeTitle(node));
        var opened = ImGui.TreeNodeEx(_nodeTitle.ContentBytes, flags);
        ImGui.PopStyleColor();
        var toggleSelect = IsKeyDown(Key.Ctrl);
        var rangeSelect = IsKeyDown(Key.Shift);

        ProcessRangeSelection(node, opened);

        if (ImGui.IsItemClicked(ImGuiMouseButton.ImGuiMouseButtonLeft)
            && ImGui.IsItemToggledOpen())
        {
            _ignoreNextMouseRelease = true;
        }

        if (ImGui.IsItemHovered()
            && ImGui.IsMouseReleased(ImGuiMouseButton.ImGuiMouseButtonLeft)
            && !ImGui.IsMouseDragPastThreshold(ImGuiMouseButton.ImGuiMouseButtonLeft))
        {
            if (_ignoreNextMouseRelease)
                _ignoreNextMouseRelease = false;
            else
                ProcessObjectSelected(selection, node, toggleSelect, rangeSelect);
        }
        else if (ImGui.IsItemClicked(ImGuiMouseButton.ImGuiMouseButtonRight))
        {
            if (selection.IsSelected(node))
                OpenSelectionContextMenu();
            else
            {
                ProcessObjectSelected(selection, node, toggleSelect, false);
                OpenSelectionContextMenu();
            }
        }

        if (ImGui.BeginDragDropSource())
        {
            if (!selection.IsSelected(node))
                ProcessObjectSelected(selection, node, toggleSelect, false);

            BeginSelectionDrag(node.Scene!, selection);
            ImGui.EndDragDropSource();
        }

        if (ImGui.BeginDragDropTarget())
        {
            DropPayloadToNode(node);
            ImGui.EndDragDropTarget();
        }

        if (node.Parent is { } parent)
        {
            var reorder = RenderObjectReorder(
                ref _nodeReorder, node, parent,
                "Move node up or down in the parent node"u8);
            if (reorder.HasValue)
                _pendingNodeReorder = reorder;
        }

        if (opened)
        {
            if (_settings.ShowComponents)
            {
                using var componentGuard = new IdScopeGuard("Components"u8);
                foreach (var component in node.Components)
                    RenderComponent(selection, component);
            }

            foreach (var child in node.Children)
                RenderNode(selection, child);

            ImGui.TreePop();
        }
    }

    private void RenderComponent(SceneSelection selection, Component component)
    {
        if (component.IsTemporary && !_settings.ShowTemporary)
            return;

        ProcessItemIfActive(selection, component);

        var flags = ImGuiTreeNodeFlags.ImGuiTreeNodeFlagsOpenOnArrow
            | ImGuiTreeNodeFlags.ImGuiTreeNodeFlagsOpenOnDoubleClick
            | ImGuiTreeNodeFlags.ImGuiTreeNodeFlagsSpanAvailWidth
            | ImGuiTreeNodeFlags.ImGuiTreeNodeFlagsAllowOverlap
            | ImGuiTreeNodeFlags.ImGuiTreeNodeFlagsLeaf;
        if (selection.IsSelected(component))
            flags |= ImGuiTreeNodeFlags.ImGuiTreeNodeFlagsSelected;
        var itemFlags = HierarchyItemFlag.Component;
        if (component.IsTemporary || component.Node.IsTemporaryEffective())
            itemFlags |= HierarchyItemFlag.Temporary;
        if (component.IsEnabledEffective)
            itemFlags |= HierarchyItemFlag.Enabled;

        using var guard = new IdScopeGuard(unchecked((int)component.Id));
        ImGui.PushStyleColor(ImGuiCol.ImGuiColText, GetItemColor(itemFlags));
        _componentTitle.Set(component.GetTypeName());
        var opened = ImGui.TreeNodeEx(_componentTitle.ContentBytes, flags);
        ImGui.PopStyleColor();
        var toggleSelect = IsKeyDown(Key.Ctrl);
        var rangeSelect = IsKeyDown(Key.Shift);

        ProcessRangeSelection(component, opened);

        if (ImGui.IsItemHovered()
            && ImGui.IsMouseReleased(ImGuiMouseButton.ImGuiMouseButtonLeft)
            && !ImGui.IsMouseDragPastThreshold(ImGuiMouseButton.ImGuiMouseButtonLeft))
        {
            ProcessObjectSelected(selection, component, toggleSelect, rangeSelect);
        }
        else if (ImGui.IsItemClicked(ImGuiMouseButton.ImGuiMouseButtonRight))
        {
            if (selection.IsSelected(component))
                OpenSelectionContextMenu();
            else
            {
                ProcessObjectSelected(selection, component, toggleSelect, false);
                OpenSelectionContextMenu();
            }
        }

        if (ImGui.BeginDragDropSource())
        {
            if (!selection.IsSelected(component))
                ProcessObjectSelected(selection, component, toggleSelect, false);

            BeginSelectionDrag(component.Scene!, selection);
            ImGui.EndDragDropSource();
        }

        var reorder = RenderObjectReorder(
            ref _componentReorder, component, component.Node,
            "Move component up or down in the node"u8);
        if (reorder.HasValue)
            _pendingComponentReorder = reorder;

        if (opened)
            ImGui.TreePop();
    }

    private ReorderInfo? RenderObjectReorder(
        ref ReorderInfo? info,
        RedObject redObject,
        Node parentNode,
        ReadOnlySpan<byte> hint)
    {
        var objectId = GetObjectId(redObject);
        var reorderingThisObject = ImGui.IsItemHovered()
            || info is { } currentInfo && currentInfo.Id == objectId;
        if (!reorderingThisObject)
            return null;

        var reorderButtonWidth = ImGui.CalcTextSize(Icons.UpDownU8).X;

        ImGui.SameLine();
        var maximumX = ImGui.GetContentRegionAvail().X
            + ImGui.GetCursorScreenPos().X - ImGui.GetWindowPos().X;
        ImGui.SetCursorPosX(maximumX - reorderButtonWidth * 2.0f);
        ImGui.SmallButton(Icons.UpDownU8);

        if (ImGui.IsItemActive())
        {
            var oldIndex = GetObjectIndexInParent(parentNode, redObject);

            if (ImGui.IsItemActivated())
                info = new ReorderInfo { Id = objectId, OldIndex = oldIndex };

            var state = info!.Value;
            var mousePosition = ImGui.GetMousePos();
            var newIndex = oldIndex;

            // Prevent jitter by adding dead zone for reordering in the opposite direction.
            var decrementY = Math.Min(
                ImGui.GetItemRectMin().Y, state.DecrementMaxY ?? float.MaxValue);
            var incrementY = Math.Max(
                ImGui.GetItemRectMax().Y, state.IncrementMinY ?? -float.MaxValue);

            if (mousePosition.Y < decrementY && newIndex > 0)
            {
                state.IncrementMinY = decrementY;
                state.DecrementMaxY = null;
                --newIndex;
            }
            else if (mousePosition.Y > incrementY)
            {
                state.IncrementMinY = null;
                state.DecrementMaxY = incrementY;
                ++newIndex; // It's okay to overflow
            }

            info = state;
            if (newIndex != oldIndex)
            {
                return new ReorderInfo
                {
                    Id = objectId,
                    OldIndex = oldIndex,
                    NewIndex = newIndex,
                };
            }
        }
        else if (info is { } endingInfo && endingInfo.Id == objectId)
        {
            info = null;
        }
        else if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(hint);
        }
        return null;
    }

    private void ApplyPendingUpdates(Scene scene)
    {
        if (_pendingNodeReorder is { } nodeReorder)
        {
            if (scene.GetNode(nodeReorder.Id) is { } node)
            {
                OnNodeReordered?.Invoke(
                    node, nodeReorder.OldIndex, nodeReorder.NewIndex);
            }
            _pendingNodeReorder = null;
        }

        if (_pendingComponentReorder is { } componentReorder)
        {
            if (scene.GetComponent(componentReorder.Id) is { } component)
            {
                OnComponentReordered?.Invoke(
                    component, componentReorder.OldIndex, componentReorder.NewIndex);
            }
            _pendingComponentReorder = null;
        }

        if (_pendingNodeReparents.Count != 0)
        {
            foreach (var info in _pendingNodeReparents)
            {
                var childNode = scene.GetNode(info.ChildId);
                var parentNode = scene.GetNode(info.ParentId);
                if (childNode is not null && parentNode is not null)
                    OnNodeReparented?.Invoke(parentNode, childNode);
            }
            _pendingNodeReparents.Clear();
        }
    }

    private void ProcessObjectSelected(
        SceneSelection selection, RedObject redObject, bool toggle, bool range)
    {
        var activeObject = selection.ActiveObject;

        if (toggle)
            selection.SetSelected(redObject, !selection.IsSelected(redObject));
        else if (range && activeObject is not null && _wasActiveObjectVisible
            && !ReferenceEquals(activeObject, redObject))
        {
            _rangeSelection.PendingRequest = new RangeSelectionRequest(
                activeObject, redObject);
        }
        else
        {
            selection.Clear();
            selection.SetSelected(redObject, true);
        }
    }

    private void ProcessItemIfActive(SceneSelection selection, RedObject currentItem)
    {
        if (ReferenceEquals(selection.ActiveObject, currentItem))
        {
            _isActiveObjectVisible = true;
            if (_scrollToActiveObject)
            {
                ImGui.SetScrollHereY();
                _scrollToActiveObject = false;
            }
        }
    }

    private void ProcessActiveObject(RedObject? activeObject)
    {
        _pathToActiveObject.Clear();
        if (!ReferenceEquals(GetTarget(_lastActiveObject), activeObject))
        {
            _lastActiveObject = activeObject is not null
                ? new WeakReference<RedObject>(activeObject)
                : null;
            _scrollToActiveObject = true;

            var currentPathNode = activeObject switch
            {
                Component component => component.TryGetNode(),
                Node node => node.Parent,
                _ => null,
            };
            while (currentPathNode is not null)
            {
                _pathToActiveObject.Add(currentPathNode);
                currentPathNode = currentPathNode.Parent;
            }
        }
    }

    private void OpenSelectionContextMenu()
    {
        _openContextMenu = true;
    }

    private void RenderContextMenu(Scene scene, SceneSelection selection)
    {
        if (_openContextMenu)
        {
            if (OnContextMenu is not null)
                ImGui.OpenPopup("##ContextMenu"u8);
            _openContextMenu = false;
        }

        if (ImGui.BeginPopup("##ContextMenu"u8))
        {
            ImGui.BeginDisabled();
            _summary.FormatInPlace($"Selected: {selection.GetSummary(scene)}");
            ImGui.TextUnformatted(_summary.ContentBytes);
            ImGui.EndDisabled();

            OnContextMenu?.Invoke(scene, selection);
            ImGui.EndPopup();
        }
    }

    private void BeginRangeSelection()
    {
        _wasActiveObjectVisible = _isActiveObjectVisible;
        _isActiveObjectVisible = false;
        _rangeSelection.Result.Clear();
        _rangeSelection.IsActive = false;
        _rangeSelection.CurrentRequest = _rangeSelection.PendingRequest;
        _rangeSelection.PendingRequest = null;
    }

    private void ProcessRangeSelection(RedObject currentObject, bool open)
    {
        _ = open;
        if (_rangeSelection.CurrentRequest is not { } request)
            return;

        var weakObject = new WeakReference<RedObject>(currentObject);
        var isBorder = request.IsBorder(currentObject);

        if (!_rangeSelection.IsActive && isBorder)
        {
            _rangeSelection.IsActive = true;
            _rangeSelection.Result.Add(weakObject);
        }
        else if (_rangeSelection.IsActive && isBorder)
        {
            _rangeSelection.Result.Add(weakObject);
            _rangeSelection.IsActive = false;
            _rangeSelection.CurrentRequest = null;
        }
        else if (_rangeSelection.IsActive)
        {
            _rangeSelection.Result.Add(weakObject);
        }
    }

    private void EndRangeSelection(SceneSelection selection)
    {
        _rangeSelection.CurrentRequest = null;

        if (!_rangeSelection.IsActive)
        {
            foreach (var reference in _rangeSelection.Result)
            {
                if (reference.TryGetTarget(out var redObject))
                    selection.SetSelected(redObject, true);
            }
        }
    }

    private void UpdateSearchResults(Scene scene)
    {
        var sceneChanged = !ReferenceEquals(GetTarget(_search.LastScene), scene);
        _search.LastScene = new WeakReference<Scene>(scene);

        // Early return if search was canceled
        if (_search.CurrentQuery.Length == 0)
        {
            _search.LastResults.Clear();
            _search.LastQuery = string.Empty;
            return;
        }

        var resultsExpired = sceneChanged
            || _search.LastResults.Count == 0
            || !_search.LastQuery.Contains(_search.CurrentQuery, StringComparison.Ordinal);
        _search.LastQuery = _search.CurrentQuery;

        if (resultsExpired)
        {
            _search.LastResults.Clear();
            foreach (var child in scene.GetChildren(true))
            {
                if (child.Name.Contains(
                        _search.CurrentQuery, StringComparison.OrdinalIgnoreCase))
                {
                    _search.LastResults.Add(new WeakReference<Node>(child));
                }
            }
        }
        else
        {
            _search.LastResults.RemoveAll(reference =>
                !reference.TryGetTarget(out var node)
                || !node.Name.Contains(
                    _search.CurrentQuery, StringComparison.OrdinalIgnoreCase));
        }
    }

    private void BeginSelectionDrag(Scene scene, SceneSelection selection)
    {
        DragDropPayload.UpdateSource(() =>
        {
            var payload = new NodeComponentDragDropPayload
            {
                Scene = new WeakReference<Scene>(scene),
                DisplayString = selection.GetSummary(scene),
            };

            foreach (var node in selection.NodesAndScenes)
                payload.Nodes.Add(node);

            foreach (var component in selection.Components)
                payload.Components.Add(component);

            return payload;
        });
    }

    private void DropPayloadToNode(Node parentNode)
    {
        var payload = DragDropPayload.Get() as NodeComponentDragDropPayload;
        if (payload is not null && CanBeDroppedTo(parentNode, payload))
        {
            var acceptedPayload = ImGui.AcceptDragDropPayload("DragDropPayload"u8);
            if (!acceptedPayload.IsNull)
            {
                foreach (var childReference in payload.Nodes)
                {
                    if (childReference.TryGetTarget(out var childNode))
                    {
                        _pendingNodeReparents.Add(new ReparentInfo
                        {
                            ParentId = parentNode.Id,
                            ChildId = childNode.Id,
                        });
                    }
                }
            }
        }
    }

    private static string GetNodeTitle(Node node)
    {
        var isScene = node.Parent is null;
        var title = (isScene ? Icons.Cubes : Icons.Cube) + " ";
        if (node.Name.Length != 0)
            title += node.Name;
        else if (isScene)
            title += "Scene";
        else
            title += $"Node {node.Id}";
        return title;
    }

    private static bool CanBeDroppedTo(
        Node parentNode, NodeComponentDragDropPayload payload)
    {
        if (payload.Nodes.Count == 0
            || payload.Scene is null
            || !payload.Scene.TryGetTarget(out var payloadScene)
            || !ReferenceEquals(parentNode.Scene, payloadScene))
        {
            return false;
        }

        foreach (var reference in payload.Nodes)
        {
            if (!reference.TryGetTarget(out var node))
                return false;
            if (ReferenceEquals(node.Scene, node))
                return false;
            if (parentNode.IsChildOf(node))
                return false;
        }
        return true;
    }

    private static Vector4f GetItemColor(HierarchyItemFlag flags)
    {
        var enabled = flags.HasFlag(HierarchyItemFlag.Enabled);
        var temporary = flags.HasFlag(HierarchyItemFlag.Temporary);
        if (flags.HasFlag(HierarchyItemFlag.Component))
        {
            if (temporary)
            {
                return enabled
                    ? new Vector4f(0.65f, 0.65f, 1.00f, 1.00f)
                    : new Vector4f(0.25f, 0.25f, 0.50f, 1.00f);
            }
            else
            {
                return enabled
                    ? new Vector4f(1.00f, 1.00f, 0.35f, 1.00f)
                    : new Vector4f(0.50f, 0.50f, 0.00f, 1.00f);
            }
        }
        else
        {
            if (temporary)
            {
                return enabled
                    ? new Vector4f(0.65f, 0.65f, 1.00f, 1.00f)
                    : new Vector4f(0.25f, 0.25f, 0.50f, 1.00f);
            }
            else
            {
                return enabled
                    ? new Vector4f(1.00f, 1.00f, 1.00f, 1.00f)
                    : new Vector4f(0.50f, 0.50f, 0.50f, 1.00f);
            }
        }
    }

    private static int GetVisibleItemsCount(
        Node node, bool showTemporary, bool showComponents)
    {
        var numChildren = showTemporary
            ? node.GetNumChildren()
            : node.GetNumPersistentChildren();
        var numComponents = showComponents
            ? showTemporary ? node.GetNumComponents() : node.GetNumPersistentComponents()
            : 0;
        return numChildren + numComponents;
    }

    private static int GetObjectIndexInParent(Node parentNode, RedObject redObject)
    {
        return redObject switch
        {
            Node node => parentNode.GetChildIndex(node),
            Component component => parentNode.GetComponentIndex(component),
            _ => int.MaxValue,
        };
    }

    private static uint GetObjectId(RedObject redObject)
    {
        return redObject switch
        {
            Node node => node.Id,
            Component component => component.Id,
            _ => 0,
        };
    }

    private static bool IsKeyDown(Key key)
    {
        var imguiKey = ImGui.ImGuiImplSDL2KeyEventToImGuiKey(
            (int)key, (int)Input.GetScancodeFromKey(key));
        return ImGui.IsKeyDown(imguiKey);
    }

    private static T? GetTarget<T>(WeakReference<T>? reference)
        where T : class
    {
        return reference is not null && reference.TryGetTarget(out var target)
            ? target
            : null;
    }

    [Flags]
    private enum HierarchyItemFlag
    {
        Node = 1 << 0,
        Component = 1 << 1,
        Enabled = 1 << 2,
        Temporary = 1 << 3,
        Subsystem = 1 << 4,
    }

    private struct ReorderInfo
    {
        public uint Id;
        public int OldIndex;
        public int NewIndex;
        public float? DecrementMaxY;
        public float? IncrementMinY;
    }

    private struct ReparentInfo
    {
        public uint ParentId;
        public uint ChildId;
    }

    private readonly struct RangeSelectionRequest
    {
        public RangeSelectionRequest(RedObject from, RedObject to)
        {
            From = new WeakReference<RedObject>(from);
            To = new WeakReference<RedObject>(to);
        }

        public bool IsBorder(RedObject redObject)
        {
            return ReferenceEquals(GetTarget(From), redObject)
                || ReferenceEquals(GetTarget(To), redObject);
        }

        public readonly WeakReference<RedObject> From;
        public readonly WeakReference<RedObject> To;
    }

    private sealed class RangeSelection
    {
        public void Clear()
        {
            PendingRequest = null;
            CurrentRequest = null;
            IsActive = false;
            Result.Clear();
        }

        public RangeSelectionRequest? PendingRequest;
        public RangeSelectionRequest? CurrentRequest;
        public bool IsActive;
        public readonly List<WeakReference<RedObject>> Result = [];
    }

    private sealed class NodeSearch
    {
        public void Clear()
        {
            CurrentQuery = string.Empty;
            LastScene = null;
            LastQuery = string.Empty;
            LastResults.Clear();
        }

        public string CurrentQuery = string.Empty;
        public WeakReference<Scene>? LastScene;
        public string LastQuery = string.Empty;
        public readonly List<WeakReference<Node>> LastResults = [];
    }

    private SceneHierarchySettings _settings = new();

    /// <summary>UI state.</summary>
    private bool _isActiveObjectVisible;
    private bool _wasActiveObjectVisible;

    private bool _ignoreNextMouseRelease;

    private bool _scrollToActiveObject;
    private WeakReference<RedObject>? _lastActiveObject;
    private readonly List<Node> _pathToActiveObject = [];

    private readonly RangeSelection _rangeSelection = new();

    private readonly NodeSearch _search = new();

    private ReorderInfo? _nodeReorder;
    private ReorderInfo? _pendingNodeReorder;
    private ReorderInfo? _componentReorder;
    private ReorderInfo? _pendingComponentReorder;

    private readonly List<ReparentInfo> _pendingNodeReparents = [];

    private bool _openContextMenu;

    private readonly CString _nodeTitle = new();
    private readonly CString _componentTitle = new();
    private readonly CString _summary = new();
}
