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
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;

using RedBolt.Bindings.DearImGui;
using RedBolt.Container;
using RedBolt.Core;
using RedBolt.Scene;

/// <summary>SystemUI widget used to edit materials.</summary>
public class NodeInspectorWidget : RedObject
{
    public event Action<IReadOnlyList<WeakReference<Serializable>?>, AttributeInfo?>?
        OnEditNodeAttributeBegin;
    public event Action<IReadOnlyList<WeakReference<Serializable>?>, AttributeInfo?>?
        OnEditNodeAttributeEnd;
    public event Action<IReadOnlyList<WeakReference<Serializable>?>, AttributeInfo?>?
        OnEditComponentAttributeBegin;
    public event Action<IReadOnlyList<WeakReference<Serializable>?>, AttributeInfo?>?
        OnEditComponentAttributeEnd;
    public event Action<IReadOnlyList<WeakReference<Serializable>?>>? OnActionBegin;
    public event Action<IReadOnlyList<WeakReference<Serializable>?>>? OnActionEnd;
    public event Action<Component>? OnComponentRemoved;

    public NodeInspectorWidget(IReadOnlyList<WeakReference<Node>?> nodes)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        var serializableNodes = new List<WeakReference<Serializable>?>(nodes.Count);
        foreach (var reference in nodes)
        {
            if (reference is not null && reference.TryGetTarget(out var node))
            {
                _nodes.Add(new WeakReference<Node>(node));
                serializableNodes.Add(new WeakReference<Serializable>(node));
            }
            else
            {
                _nodes.Add(null);
                serializableNodes.Add(null);
            }
        }

        _nodeInspector = new SerializableInspectorWidget(serializableNodes);
        Debug.Assert(_nodes.Count != 0);
        _nodeInspector.OnEditAttributeBegin += (objects, attribute) =>
            OnEditNodeAttributeBegin?.Invoke(objects, attribute);
        _nodeInspector.OnEditAttributeEnd += (objects, attribute) =>
            OnEditNodeAttributeEnd?.Invoke(objects, attribute);
        _nodeInspector.OnActionBegin += objects => OnActionBegin?.Invoke(objects);
        _nodeInspector.OnActionEnd += objects => OnActionEnd?.Invoke(objects);
    }

    public override void Dispose()
    {
        _textBuffer.Dispose();
        _pendingRemoveComponents.Clear();
        for (var index = _componentInspectors.Count - 1; index >= 0; --index)
            _componentInspectors[index].Dispose();
        _componentInspectors.Clear();
        _components.Clear();
        _nodeInspector.Dispose();
        _nodes.Clear();
        OnComponentRemoved = null;
        OnActionEnd = null;
        OnActionBegin = null;
        OnEditComponentAttributeEnd = null;
        OnEditComponentAttributeBegin = null;
        OnEditNodeAttributeEnd = null;
        OnEditNodeAttributeBegin = null;
        base.Dispose();
    }

    public void RenderTitle()
    {
        _nodeInspector.RenderTitle();
    }

    public void RenderContent()
    {
        for (var index = _nodes.Count - 1; index >= 0; --index)
        {
            if (_nodes[index] is null || !_nodes[index]!.TryGetTarget(out _))
                _nodes.RemoveAt(index);
        }
        if (_nodes.Count == 0)
            return;

        _pendingRemoveComponents.Clear();

        var allComponents = GetAllComponents();
        if (!AreComponentListsEqual(_components, allComponents))
        {
            _components = new List<NodeComponentReference>(allComponents);
            for (var index = _componentInspectors.Count - 1; index >= 0; --index)
                _componentInspectors[index].Dispose();
            _componentInspectors.Clear();

            var sharedComponents = GetSharedComponents();
            foreach (var components in sharedComponents)
            {
                var serializableComponents = new List<WeakReference<Serializable>?>(components.Count);
                foreach (var reference in components)
                {
                    var componentFound = reference.TryGetTarget(out var component);
                    Debug.Assert(componentFound);
                    serializableComponents.Add(new WeakReference<Serializable>(component!));
                }
                _componentInspectors.Add(new SerializableInspectorWidget(serializableComponents));
            }

            var numSharedComponents = 0u;
            foreach (var components in sharedComponents)
                numSharedComponents += checked((uint)components.Count);
            _numSkippedComponents = checked((uint)_components.Count) - numSharedComponents;

            foreach (var inspector in _componentInspectors)
            {
                inspector.OnEditAttributeBegin += (objects, attribute) =>
                    OnEditComponentAttributeBegin?.Invoke(objects, attribute);
                inspector.OnEditAttributeEnd += (objects, attribute) =>
                    OnEditComponentAttributeEnd?.Invoke(objects, attribute);
                inspector.OnActionBegin += objects => OnActionBegin?.Invoke(objects);
                inspector.OnActionEnd += objects => OnActionEnd?.Invoke(objects);
            }
        }

        _nodeInspector.RenderContent();

        foreach (var componentInspector in _componentInspectors)
        {
            var firstReference = componentInspector.GetObjects()[0];
            if (firstReference is null || !firstReference.TryGetTarget(out var firstComponent))
                continue;

            using var guard = new IdScopeGuard(RuntimeHelpers.GetHashCode(firstComponent));

            if (ImGui.Button("\uf2ed##RemoveComponent"u8))
            {
                foreach (var reference in componentInspector.GetObjects())
                {
                    if (reference is not null
                        && reference.TryGetTarget(out var serializable)
                        && serializable is Component component)
                    {
                        _pendingRemoveComponents.Add(new WeakReference<Component>(component));
                    }
                }
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Remove this component from all selected nodes"u8);
            ImGui.SameLine();

            _textBuffer.Set(componentInspector.GetTitle());
            if (ImGui.CollapsingHeader(
                _textBuffer.ContentBytes,
                ImGuiTreeNodeFlags.ImGuiTreeNodeFlagsDefaultOpen))
            {
                componentInspector.RenderContent();
            }
        }

        if (_numSkippedComponents > 0)
        {
            ImGui.Separator();
            _textBuffer.FormatInPlace($"{_numSkippedComponents} other components skipped");
            ImGui.TextUnformatted(_textBuffer.ContentBytes);
        }

        foreach (var reference in _pendingRemoveComponents)
        {
            if (!reference.TryGetTarget(out var component))
                continue;

            OnComponentRemoved?.Invoke(component);
            if (reference.TryGetTarget(out component))
                component.Remove();
        }
    }

    public IReadOnlyList<WeakReference<Node>?> GetNodes() => _nodes;

    private readonly struct NodeComponentReference
    {
        public NodeComponentReference(Node node, Component component)
        {
            Node = new WeakReference<Node>(node);
            Component = new WeakReference<Component>(component);
        }

        public WeakReference<Node> Node { get; }

        public WeakReference<Component> Component { get; }
    }

    private static bool AreComponentListsEqual(
        IReadOnlyList<NodeComponentReference> lhs, IReadOnlyList<NodeComponentReference> rhs)
    {
        if (lhs.Count != rhs.Count)
            return false;

        for (var index = 0; index < lhs.Count; ++index)
        {
            if (!lhs[index].Node.TryGetTarget(out var lhsNode)
                || !rhs[index].Node.TryGetTarget(out var rhsNode)
                || !ReferenceEquals(lhsNode, rhsNode)
                || !lhs[index].Component.TryGetTarget(out var lhsComponent)
                || !rhs[index].Component.TryGetTarget(out var rhsComponent)
                || !ReferenceEquals(lhsComponent, rhsComponent))
            {
                return false;
            }
        }
        return true;
    }

    private List<NodeComponentReference> GetAllComponents()
    {
        var components = new List<NodeComponentReference>();
        foreach (var reference in _nodes)
        {
            var nodeFound = reference!.TryGetTarget(out var node);
            Debug.Assert(nodeFound);
            foreach (var component in node!.GetComponents())
                components.Add(new NodeComponentReference(node, component));
        }
        return components;
    }

    private List<List<WeakReference<Component>>> GetSharedComponents()
    {
        var result = new List<List<WeakReference<Component>>>();

        // Fill result from the first node as-is
        var firstNodeFound = _nodes[0]!.TryGetTarget(out var firstNode);
        Debug.Assert(firstNodeFound);
        var components = firstNode!.GetComponents();
        foreach (var component in components)
            result.Add([new WeakReference<Component>(component)]);

        // Prune all components missing in any of the other nodes
        for (var nodeIndex = 1; nodeIndex < _nodes.Count; ++nodeIndex)
        {
            var nodeFound = _nodes[nodeIndex]!.TryGetTarget(out var node);
            Debug.Assert(nodeFound);
            components = node!.GetComponents();

            // If at least one node has no components, result is empty
            if (components.Count == 0)
            {
                result.Clear();
                break;
            }

            var index = 0;
            foreach (var sharedComponents in result)
            {
                var componentFound = sharedComponents[0].TryGetTarget(out var sharedComponent);
                Debug.Assert(componentFound);
                var sharedType = sharedComponent!.GetType();

                if (index < components.Count)
                {
                    // Try to find component with the same type in the current node
                    var matchingComponentIndex = -1;
                    for (var componentIndex = index; componentIndex < components.Count; ++componentIndex)
                    {
                        if (components[componentIndex].GetType() == sharedType)
                        {
                            matchingComponentIndex = componentIndex;
                            break;
                        }
                    }
                    if (matchingComponentIndex != -1)
                    {
                        sharedComponents.Add(
                            new WeakReference<Component>(components[matchingComponentIndex]));
                        index = matchingComponentIndex + 1;
                        continue;
                    }
                }

                // Otherwise, prune the shared entry
                sharedComponents.Clear();
            }

            for (var resultIndex = result.Count - 1; resultIndex >= 0; --resultIndex)
            {
                if (result[resultIndex].Count == 0)
                    result.RemoveAt(resultIndex);
            }
        }
        return result;
    }

    private readonly List<WeakReference<Node>?> _nodes = [];
    private readonly SerializableInspectorWidget _nodeInspector;

    private List<NodeComponentReference> _components = [];
    private readonly List<SerializableInspectorWidget> _componentInspectors = [];
    private uint _numSkippedComponents;

    private readonly List<WeakReference<Component>> _pendingRemoveComponents = [];
    private readonly CString _textBuffer = new();
}
