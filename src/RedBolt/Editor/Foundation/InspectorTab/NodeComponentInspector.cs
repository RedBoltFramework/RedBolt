// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Editor.Foundation;

using System;
using System.Collections.Generic;
using System.Diagnostics;

using RedBolt.Bindings.DearImGui;
using RedBolt.Container;
using RedBolt.Core;
using RedBolt.Editor.Core;
using RedBolt.Editor.Foundation.Shared;
using RedBolt.Editor.Project;
using RedBolt.Scene;
using RedBolt.SystemUI;
using RedBolt.Utility;

/// <summary>Scene hierarchy provider for hierarchy browser tab.</summary>
public class NodeComponentInspector : RedObject, InspectorSource
{
    public event Action? OnActivated;

    public NodeComponentInspector(Project project)
    {
        _project = new WeakReference<Project>(project);
        project.OnRequest += OnProjectRequest;
    }

    public override void Dispose()
    {
        _oldData.Clear();
        _changedNodes.Clear();
        _componentActionBuilder = null;
        _nodeActionBuilder = null;
        _actionBuffer.Dispose();
        _componentSummary.Clear();
        _componentWidget?.Dispose();
        _componentWidget = null;
        _nodeWidget?.Dispose();
        _nodeWidget = null;
        _components.Clear();
        _nodes.Clear();
        _scene = null;
        _inspectedTab = null;
        _textBuffer.Dispose();
        if (_project.TryGetTarget(out var project))
            project.OnRequest -= OnProjectRequest;
        OnActivated = null;
        base.Dispose();
    }

    /// <summary>Implement InspectorSource.</summary>
    public EditorTab? GetOwnerTab()
    {
        return _inspectedTab is not null && _inspectedTab.TryGetTarget(out var inspectedTab)
            ? inspectedTab
            : null;
    }

    public bool IsUndoSupported() => true;

    public void RenderContent()
    {
        if (_nodeWidget is not null)
        {
            _nodeWidget.RenderTitle();
            ImGui.Separator();
            _nodeWidget.RenderContent();
            ImGui.Separator();
            RenderAddComponent();
        }
        else if (_componentWidget is not null)
        {
            _componentWidget.RenderTitle();
            ImGui.Separator();
            _componentWidget.RenderContent();
        }
        else
        {
            RenderComponentSummary();
        }
    }

    public void RenderContextMenuItems()
    {
    }

    public void RenderMenu()
    {
        if (_inspectedTab is not null && _inspectedTab.TryGetTarget(out var inspectedTab))
            inspectedTab.RenderMenu();
    }

    public void ApplyHotkeys(HotkeyManager? hotkeyManager)
    {
        // TODO: Fix me. It's currently too annoying to deal with text edit hotkeys.
        // if (_inspectedTab is not null && _inspectedTab.TryGetTarget(out var inspectedTab))
        //     inspectedTab.ApplyHotkeys(hotkeyManager);
    }

    private sealed class AttributeSnapshot : IDisposable
    {
        public readonly VariantVector Values = new();
        public readonly List<PackedNodeData> Nodes = [];
        public PackedSceneData Scene = new();

        public void Dispose()
        {
            Values.Dispose();
            Nodes.Clear();
            Scene = new PackedSceneData();
        }
    }

    private static List<Node> GetSortedTopmostNodes(
        IReadOnlyList<WeakReference<Serializable>?> objects)
    {
        var nodeSet = new HashSet<Node>(ReferenceEqualityComparer.Instance);
        foreach (var reference in objects)
        {
            if (reference is null || !reference.TryGetTarget(out var serializable))
                continue;

            if (serializable is Node node)
                nodeSet.Add(node);
            else if (serializable is Component component && component.TryGetNode() is { } componentNode)
                nodeSet.Add(componentNode);
        }

        var nodeVector = new List<Node>(nodeSet.Count);
        foreach (var node in nodeSet)
        {
            var hasSelectedParent = false;
            foreach (var other in nodeSet)
            {
                if (node.IsChildOf(other))
                {
                    hasSelectedParent = true;
                    break;
                }
            }
            if (!hasSelectedParent)
                nodeVector.Add(node);
        }

        // Native code sorts by node pointer. Scene node IDs provide the stable managed identity
        // because CLR object addresses are movable.
        nodeVector.Sort(static (lhs, rhs) =>
        {
            var result = lhs.Id.CompareTo(rhs.Id);
            if (result != 0)
                return result;
            return lhs.GetIndexInParent().CompareTo(rhs.GetIndexInParent());
        });
        return nodeVector;
    }

    private static bool HasScene(IReadOnlyList<Node> nodes)
    {
        foreach (var node in nodes)
        {
            if (ReferenceEquals(node.Scene, node))
                return true;
        }
        return false;
    }

    private void OnProjectRequest(EditorTab? senderTab, ProjectRequest request)
    {
        if (senderTab is null
            || request is not InspectNodeComponentRequest inspectNodeComponentRequest
            || inspectNodeComponentRequest.IsEmpty)
        {
            return;
        }

        var commonScene = inspectNodeComponentRequest.CommonScene;
        if (commonScene is null)
            return;

        request.QueueProcessCallback(() =>
        {
            if (!AreWeakListsEqual(_nodes, inspectNodeComponentRequest.Nodes)
                || !AreWeakListsEqual(_components, inspectNodeComponentRequest.Components)
                || _inspectedTab is null
                || !_inspectedTab.TryGetTarget(out var currentTab)
                || !ReferenceEquals(currentTab, senderTab))
            {
                CopyWeakList(_nodes, inspectNodeComponentRequest.Nodes);
                CopyWeakList(_components, inspectNodeComponentRequest.Components);
                _inspectedTab = new WeakReference<EditorTab>(senderTab);
                _scene = new WeakReference<Scene>(commonScene);
                InspectObjects();
            }
            OnActivated?.Invoke();
        });
    }

    private List<WeakReference<Node>?> CollectNodes()
    {
        foreach (var reference in _nodes)
        {
            if (reference.TryGetTarget(out var node) && node.GetType() == typeof(Scene))
                return [new WeakReference<Node>(node)];
        }

        // If nodes are present, inspect nodes.
        if (_nodes.Count != 0)
        {
            var result = new List<WeakReference<Node>?>(_nodes.Count);
            foreach (var reference in _nodes)
            {
                reference.TryGetTarget(out var node);
                result.Add(node is not null ? new WeakReference<Node>(node) : null);
            }
            return result;
        }

        return [];
    }

    private List<WeakReference<Serializable>?> CollectComponents()
    {
        // If components are of the same type, inspect components.
        if (_components.Count == 0)
            return [];

        _components[0].TryGetTarget(out var firstComponent);
        var firstType = firstComponent!.GetType();
        foreach (var reference in _components)
        {
            if (!reference.TryGetTarget(out var component) || component.GetType() != firstType)
                return [];
        }

        var result = new List<WeakReference<Serializable>?>(_components.Count);
        foreach (var reference in _components)
        {
            reference.TryGetTarget(out var component);
            result.Add(component is not null ? new WeakReference<Serializable>(component) : null);
        }
        return result;
    }

    private void InspectObjects()
    {
        var nodes = CollectNodes();
        if (nodes.Count != 0)
        {
            _nodeWidget?.Dispose();
            _nodeWidget = new NodeInspectorWidget(nodes);
            _componentWidget?.Dispose();
            _componentWidget = null;
            _componentSummary.Clear();

            _nodeWidget.OnEditNodeAttributeBegin += BeginEditNodeAttribute;
            _nodeWidget.OnEditNodeAttributeEnd += EndEditNodeAttribute;
            _nodeWidget.OnEditComponentAttributeBegin += BeginEditComponentAttribute;
            _nodeWidget.OnEditComponentAttributeEnd += EndEditComponentAttribute;
            _nodeWidget.OnActionBegin += BeginAction;
            _nodeWidget.OnActionEnd += EndAction;
            _nodeWidget.OnComponentRemoved += RemoveComponent;
        }
        else
        {
            var components = CollectComponents();
            if (components.Count != 0)
            {
                _nodeWidget?.Dispose();
                _nodeWidget = null;
                _componentWidget?.Dispose();
                _componentWidget = new SerializableInspectorWidget(components);
                _componentSummary.Clear();

                _componentWidget.OnEditAttributeBegin += BeginEditComponentAttribute;
                _componentWidget.OnEditAttributeEnd += EndEditComponentAttribute;
                _componentWidget.OnActionBegin += BeginAction;
                _componentWidget.OnActionEnd += EndAction;
            }
            else
            {
                _nodeWidget?.Dispose();
                _nodeWidget = null;
                _componentWidget?.Dispose();
                _componentWidget = null;
                _componentSummary.Clear();

                foreach (var reference in _components)
                {
                    if (!reference.TryGetTarget(out var component))
                        continue;
                    var typeName = component.GetTypeName();
                    _componentSummary.TryGetValue(typeName, out var count);
                    _componentSummary[typeName] = count + 1;
                }
            }
        }
    }

    private void RenderComponentSummary()
    {
        if (ImGui.BeginTable("##Components"u8, 2))
        {
            ImGui.TableSetupColumn("Component"u8);
            ImGui.TableSetupColumn("Count"u8);
            ImGui.TableHeadersRow();

            foreach (var (typeName, count) in _componentSummary)
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                _textBuffer.Set(typeName);
                ImGui.TextUnformatted(_textBuffer.ContentBytes);
                ImGui.TableNextColumn();
                _textBuffer.FormatInPlace($"{count}");
                ImGui.TextUnformatted(_textBuffer.ContentBytes);
            }
            ImGui.EndTable();
        }
    }

    private void RenderAddComponent()
    {
        if (ImGui.Button("\uf0fe Add Component"u8))
            ImGui.OpenPopup("##AddComponent"u8);
        if (ImGui.BeginPopup("##AddComponent"u8))
        {
            var componentType = CreateComponentMenuFunctions.RenderCreateComponentMenu();
            if (componentType is not null)
            {
                AddComponentToNodes(componentType.TypeId);
                ImGui.CloseCurrentPopup();
            }
            ImGui.EndPopup();
        }
    }

    private void BeginEditNodeAttribute(
        IReadOnlyList<WeakReference<Serializable>?> objects, AttributeInfo? attribute)
    {
        if (objects.Count == 0)
            return;

        Debug.Assert(_nodeActionBuilder is null);
        Debug.Assert(attribute is not null);
        _nodeActionBuilder = new ChangeNodeAttributesActionBuilder(
            _actionBuffer, GetScene(), CastVectorTo<Node>(objects), attribute!);
    }

    private void EndEditNodeAttribute(
        IReadOnlyList<WeakReference<Serializable>?> objects, AttributeInfo? attribute)
    {
        if (objects.Count == 0)
            return;

        Debug.Assert(_nodeActionBuilder is not null);
        GetInspectedTab().PushAction(_nodeActionBuilder!.Build()!);
        _nodeActionBuilder = null;
    }

    private void BeginEditComponentAttribute(
        IReadOnlyList<WeakReference<Serializable>?> objects, AttributeInfo? attribute)
    {
        if (objects.Count == 0)
            return;

        Debug.Assert(_componentActionBuilder is null);
        Debug.Assert(attribute is not null);
        _componentActionBuilder = new ChangeComponentAttributesActionBuilder(
            _actionBuffer, GetScene(), CastVectorTo<Component>(objects), attribute!);
    }

    private void EndEditComponentAttribute(
        IReadOnlyList<WeakReference<Serializable>?> objects, AttributeInfo? attribute)
    {
        if (objects.Count == 0)
            return;

        Debug.Assert(_componentActionBuilder is not null);
        GetInspectedTab().PushAction(_componentActionBuilder!.Build()!);
        _componentActionBuilder = null;
    }

    private void BeginAction(IReadOnlyList<WeakReference<Serializable>?> objects)
    {
        _oldData.Clear();
        _changedNodes = GetSortedTopmostNodes(objects);
        if (HasScene(_changedNodes))
        {
            // Disable undo/redo for scene actions for simplicity and performance.
            // TODO: Implement it?
            _changedNodes.Clear();
            return;
        }

        foreach (var node in _changedNodes)
            _oldData.Add(new PackedNodeData(node));
    }

    private void EndAction(IReadOnlyList<WeakReference<Serializable>?> objects)
    {
        for (var i = 0; i < _changedNodes.Count; ++i)
        {
            GetInspectedTab().PushAction(
                new ChangeNodeSubtreeAction(GetScene(), _oldData[i], _changedNodes[i]));
        }
    }

    private void AddComponentToNodes(StringHash componentType)
    {
        if (_nodeWidget is null)
            return;

        foreach (var reference in _nodeWidget.GetNodes())
        {
            if (reference is null || !reference.TryGetTarget(out var node))
                continue;

            var builder = new CreateComponentActionBuilder(node, componentType);
            if (node.CreateComponent(componentType) is { } component)
                GetInspectedTab().PushAction(builder.Build(component)!);
        }
    }

    private void RemoveComponent(Component component)
    {
        var builder = new RemoveComponentActionBuilder(component);
        component.Remove();
        GetInspectedTab().PushAction(builder.Build()!);
    }

    private static List<T?> CastVectorTo<T>(IReadOnlyList<WeakReference<Serializable>?> objects)
        where T : Serializable
    {
        var result = new List<T?>(objects.Count);
        foreach (var reference in objects)
        {
            Serializable? serializable = null;
            reference?.TryGetTarget(out serializable);
            result.Add(serializable as T);
        }
        return result;
    }

    private static bool AreWeakListsEqual<T>(
        IReadOnlyList<WeakReference<T>> lhs, IReadOnlyList<WeakReference<T>> rhs)
        where T : class
    {
        if (lhs.Count != rhs.Count)
            return false;

        for (var i = 0; i < lhs.Count; ++i)
        {
            lhs[i].TryGetTarget(out var lhsTarget);
            rhs[i].TryGetTarget(out var rhsTarget);
            if (!ReferenceEquals(lhsTarget, rhsTarget))
                return false;
        }
        return true;
    }

    private static void CopyWeakList<T>(
        List<WeakReference<T>> destination, IReadOnlyList<WeakReference<T>> source)
        where T : class
    {
        destination.Clear();
        foreach (var reference in source)
        {
            if (reference.TryGetTarget(out var target))
                destination.Add(new WeakReference<T>(target));
            else
                destination.Add(new WeakReference<T>(null!));
        }
    }

    private Scene? GetScene()
    {
        return _scene is not null && _scene.TryGetTarget(out var scene) ? scene : null;
    }

    private EditorTab GetInspectedTab()
    {
        EditorTab? inspectedTab = null;
        var tabFound = _inspectedTab is not null && _inspectedTab.TryGetTarget(out inspectedTab);
        Debug.Assert(tabFound);
        return inspectedTab!;
    }

    private readonly WeakReference<Project> _project;

    private WeakReference<EditorTab>? _inspectedTab;
    private WeakReference<Scene>? _scene;
    private readonly List<WeakReference<Node>> _nodes = [];
    private readonly List<WeakReference<Component>> _components = [];

    private NodeInspectorWidget? _nodeWidget;
    private SerializableInspectorWidget? _componentWidget;
    private readonly SortedDictionary<string, uint> _componentSummary = new(StringComparer.Ordinal);
    private readonly CString _textBuffer = new();

    private readonly ChangeAttributeBuffer _actionBuffer = new();
    private ChangeNodeAttributesActionBuilder? _nodeActionBuilder;
    private ChangeComponentAttributesActionBuilder? _componentActionBuilder;

    private List<Node> _changedNodes = [];
    private readonly List<PackedNodeData> _oldData = [];
}
