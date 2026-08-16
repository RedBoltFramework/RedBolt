// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Utility;

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

using RedBolt.Core;
using RedBolt.IO;
using RedBolt.Scene;

/// <summary>Packed selected nodes and components.</summary>
public class PackedSceneSelection : IArchiveSerializable, ICloneable, IEquatable<PackedSceneSelection?>
{
    public List<uint> NodeIds = [];
    public List<uint> ComponentIds = [];

    public uint ActiveNodeOrSceneId;
    public uint ActiveNodeId;
    public uint ActiveComponentId;

    public void Clear()
    {
        NodeIds.Clear();
        ComponentIds.Clear();
        ActiveNodeOrSceneId = 0;
        ActiveNodeId = 0;
        ActiveComponentId = 0;
    }

    public void SerializeInBlock(Archive archive)
    {
        ArchiveSerialization.SerializeVector(
            archive, "NodeIds", NodeIds, ArchiveSerialization.SerializeValue);
        ArchiveSerialization.SerializeVector(
            archive, "ComponentIds", ComponentIds, ArchiveSerialization.SerializeValue);
        ArchiveSerialization.SerializeValue(archive, "ActiveNodeOrSceneId", ref ActiveNodeOrSceneId);
        ArchiveSerialization.SerializeValue(archive, "ActiveNodeId", ref ActiveNodeId);
        ArchiveSerialization.SerializeValue(archive, "ActiveComponentId", ref ActiveComponentId);
    }

    public PackedSceneSelection Clone()
    {
        return new PackedSceneSelection
        {
            NodeIds = [.. NodeIds],
            ComponentIds = [.. ComponentIds],
            ActiveNodeOrSceneId = ActiveNodeOrSceneId,
            ActiveNodeId = ActiveNodeId,
            ActiveComponentId = ActiveComponentId,
        };
    }

    object ICloneable.Clone() => Clone();

    public bool Equals(PackedSceneSelection? other)
    {
        return other is not null
            && EqualLists(NodeIds, other.NodeIds)
            && EqualLists(ComponentIds, other.ComponentIds)
            && ActiveNodeOrSceneId == other.ActiveNodeOrSceneId
            && ActiveNodeId == other.ActiveNodeId
            && ActiveComponentId == other.ActiveComponentId;
    }

    public override bool Equals(object? obj) => Equals(obj as PackedSceneSelection);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var nodeId in NodeIds)
            hash.Add(nodeId);
        foreach (var componentId in ComponentIds)
            hash.Add(componentId);
        hash.Add(ActiveNodeOrSceneId);
        hash.Add(ActiveNodeId);
        hash.Add(ActiveComponentId);
        return hash.ToHashCode();
    }

    public static bool operator ==(PackedSceneSelection? lhs, PackedSceneSelection? rhs)
    {
        return ReferenceEquals(lhs, rhs) || lhs is not null && lhs.Equals(rhs);
    }

    public static bool operator !=(PackedSceneSelection? lhs, PackedSceneSelection? rhs) => !(lhs == rhs);

    private static bool EqualLists(List<uint> lhs, List<uint> rhs)
    {
        if (lhs.Count != rhs.Count)
            return false;
        for (var i = 0; i < lhs.Count; ++i)
        {
            if (lhs[i] != rhs[i])
                return false;
        }
        return true;
    }
}

/// <summary>Selected nodes and components in the Scene.</summary>
public class SceneSelection
{
    public event Action<SceneSelection>? OnChanged;

    /// <summary>Return current state</summary>
    public uint Revision => _revision;

    public bool IsEmpty => _nodesAndScenes.Count == 0 && _components.Count == 0;

    public bool IsSelected(Component? component)
    {
        return component is not null && ContainsTarget(_components, component);
    }

    public bool IsSelected(Node? node, bool effectively = false)
    {
        return node is not null && (effectively
            ? ContainsTarget(_effectiveNodesAndScenes, node)
            : ContainsTarget(_nodesAndScenes, node));
    }

    public bool IsSelected(RedObject? redObject)
    {
        return redObject is not null && ContainsTarget(_objects, redObject);
    }

    public Node? ActiveNodeOrScene => GetTarget(_activeNodeOrScene);

    public Node? ActiveNode => GetTarget(_activeNode);

    public RedObject? ActiveObject => GetTarget(_activeObject);

    public IReadOnlySet<WeakReference<Node>> NodesAndScenes => _nodesAndScenes;

    public IReadOnlySet<WeakReference<Node>> Nodes => _nodes;

    public IReadOnlySet<WeakReference<Node>> EffectiveNodesAndScenes => _effectiveNodesAndScenes;

    public IReadOnlySet<WeakReference<Node>> EffectiveNodes => _effectiveNodes;

    public IReadOnlySet<WeakReference<Component>> Components => _components;

    public string GetSummary(Scene? scene)
    {
        var numNodes = _nodes.Count;
        var numComponents = _components.Count;
        var hasScene = _nodesAndScenes.Count != numNodes;

        var elements = new List<string>();

        if (hasScene)
            elements.Add("Scene");

        if (numNodes == 1)
        {
            var nodeReference = GetFirst(_nodes)!;
            nodeReference.TryGetTarget(out var node);
            if (!string.IsNullOrEmpty(node!.Name))
                elements.Add($"Node '{node.Name}'");
            else
                elements.Add($"Node {node.Id}");
        }
        else if (numNodes > 1)
            elements.Add($"{numNodes} Nodes");

        if (numComponents == 1)
        {
            var componentReference = GetFirst(_components)!;
            componentReference.TryGetTarget(out var component);
            elements.Add(component!.GetTypeName());
        }
        else if (numComponents > 1)
            elements.Add($"{numComponents} Components");

        return string.Join(", ", elements);
    }

    /// <summary>Cleanup expired selection.</summary>
    public void Update()
    {
        var componentsSize = _components.Count;
        var numNodes = _nodesAndScenes.Count;

        _components.RemoveWhere(static component => !component.TryGetTarget(out _));
        _nodesAndScenes.RemoveWhere(static node => !node.TryGetTarget(out _));
        _nodes.RemoveWhere(static node => !node.TryGetTarget(out _));

        // No need to check nodes_ because it is a subset of nodesAndScenes_
        if (_components.Count != componentsSize || _nodesAndScenes.Count != numNodes)
        {
            UpdateEffectiveNodes();
            NotifyChanged();
        }
    }

    /// <summary>Save selection.</summary>
    public void Save(PackedSceneSelection packedSelection)
    {
        packedSelection.Clear();

        foreach (var nodeReference in _nodesAndScenes)
        {
            if (nodeReference.TryGetTarget(out var node))
                packedSelection.NodeIds.Add(node.Id);
        }

        foreach (var componentReference in _components)
        {
            if (componentReference.TryGetTarget(out var component))
                packedSelection.ComponentIds.Add(component.Id);
        }

        packedSelection.NodeIds.Sort();
        packedSelection.ComponentIds.Sort();

        packedSelection.ActiveNodeOrSceneId = ActiveNodeOrScene?.Id ?? 0;
        packedSelection.ActiveNodeId = ActiveNode?.Id ?? 0;
        var activeComponent = ActiveObject as Component;
        packedSelection.ActiveComponentId = activeComponent?.Id ?? 0;
    }

    /// <summary>Load selection.</summary>
    public void Load(Scene? scene, PackedSceneSelection packedSelection)
    {
        ClearInternal();
        var targetScene = scene!;

        foreach (var nodeId in packedSelection.NodeIds)
        {
            var node = targetScene.GetNode(nodeId);
            if (node is not null)
            {
                var weakNode = CreateWeakReference(node);
                _nodesAndScenes.Add(weakNode);
                if (!ReferenceEquals(node, targetScene))
                    _nodes.Add(weakNode);
            }
        }

        foreach (var componentId in packedSelection.ComponentIds)
        {
            var component = targetScene.GetComponent(componentId);
            if (component is not null)
                _components.Add(CreateWeakReference(component));
        }

        var packedActiveNodeOrScene = packedSelection.ActiveNodeOrSceneId != 0
            ? targetScene.GetNode(packedSelection.ActiveNodeOrSceneId)
            : null;
        _activeNodeOrScene = packedActiveNodeOrScene is not null
            ? CreateWeakReference(packedActiveNodeOrScene)
            : null;
        var packedActiveNode = packedSelection.ActiveNodeId != 0
            ? targetScene.GetNode(packedSelection.ActiveNodeId)
            : null;
        _activeNode = packedActiveNode is not null ? CreateWeakReference(packedActiveNode) : null;
        var packedActiveComponent = packedSelection.ActiveComponentId != 0
            ? targetScene.GetComponent(packedSelection.ActiveComponentId)
            : null;
        _activeObject = packedActiveComponent is not null
            ? CreateWeakReference<RedObject>(packedActiveComponent)
            : null;

        var activeNode = ActiveNode;
        if (activeNode is not null && ReferenceEquals(activeNode.GetScene(), activeNode))
            _activeNode = null;
        if (ActiveObject is null)
        {
            activeNode = ActiveNode;
            _activeObject = activeNode is not null ? CreateWeakReference<RedObject>(activeNode) : null;
        }

        UpdateEffectiveNodes();
        NotifyChanged();
    }

    /// <summary>Return packed selection.</summary>
    public PackedSceneSelection Pack()
    {
        var packedSelection = new PackedSceneSelection();
        Save(packedSelection);
        return packedSelection;
    }

    /// <summary>Clear selection.</summary>
    public void Clear()
    {
        ClearInternal();
        NotifyChanged();
    }

    /// <summary>Convert component selection to node selection.</summary>
    public void ConvertToNodes()
    {
        if (_components.Count == 0)
            return;

        foreach (var componentReference in _components)
        {
            if (componentReference.TryGetTarget(out var component))
            {
                var node = component.TryGetNode();
                if (node is not null)
                    SetSelected(node, true);
            }
        }
        _components.Clear();

        NotifyChanged();
    }

    /// <summary>Set whether the component is selected.</summary>
    public void SetSelected(Component? component, bool selected, bool activated = true)
    {
        if (component is null)
            return;

        if (selected)
        {
            var weakComponent = CreateWeakReference(component);
            var node = component.TryGetNode();
            if (node is not null)
                UpdateActiveObject(CreateWeakReference(node), weakComponent, activated);
            _objects.Add(CreateWeakReference<RedObject>(component));
            _components.Add(weakComponent);
        }
        else
        {
            RemoveTarget(_objects, component);
            RemoveTarget(_components, component);
        }

        UpdateEffectiveNodes();

        NotifyChanged();
    }

    /// <summary>Set whether the node is selected.</summary>
    public void SetSelected(Node? node, bool selected, bool activated = true)
    {
        if (node is null)
            return;

        if (selected)
        {
            var weakNode = CreateWeakReference(node);
            UpdateActiveObject(weakNode, null, activated);
            _objects.Add(CreateWeakReference<RedObject>(node));
            _nodesAndScenes.Add(weakNode);
            if (node.GetParent() is not null)
                _nodes.Add(weakNode);
        }
        else
        {
            RemoveTarget(_objects, node);
            RemoveTarget(_nodesAndScenes, node);
            RemoveTarget(_nodes, node);
        }

        UpdateEffectiveNodes();

        NotifyChanged();
    }

    /// <summary>Set whether the node or component is selected.</summary>
    public void SetSelected(RedObject? redObject, bool selected, bool activated = true)
    {
        if (redObject is Node node)
            SetSelected(node, selected, activated);
        else if (redObject is Component component)
            SetSelected(component, selected, activated);
        else
            System.Diagnostics.Debug.Assert(false, "SceneSelection.SetSelected received unexpected object");
    }

    private void ClearInternal()
    {
        _objects.Clear();
        _nodesAndScenes.Clear();
        _nodes.Clear();
        _components.Clear();

        _activeNodeOrScene = null;
        _activeNode = null;
        _activeObject = null;

        _effectiveNodesAndScenes.Clear();
        _effectiveNodes.Clear();
    }

    private void NotifyChanged()
    {
        _revision = Math.Max(1u, unchecked(_revision + 1));
        OnChanged?.Invoke(this);
    }

    private void UpdateActiveObject(
        WeakReference<Node> node, WeakReference<Component>? component, bool forceUpdate)
    {
        if (ActiveNodeOrScene is null || forceUpdate)
            _activeNodeOrScene = node;

        if (ActiveNode is null || forceUpdate)
        {
            node.TryGetTarget(out var nodeTarget);
            if (nodeTarget!.GetParent() is not null)
                _activeNode = node;
        }

        if (ActiveObject is null || forceUpdate)
        {
            if (component is not null)
            {
                component.TryGetTarget(out var componentTarget);
                _activeObject = CreateWeakReference<RedObject>(componentTarget!);
            }
            else
            {
                node.TryGetTarget(out var nodeTarget);
                _activeObject = CreateWeakReference<RedObject>(nodeTarget!);
            }
        }
    }

    private void UpdateEffectiveNodes()
    {
        _effectiveNodesAndScenes.Clear();
        _effectiveNodes.Clear();

        foreach (var nodeReference in _nodes)
        {
            if (nodeReference.TryGetTarget(out var node))
            {
                var weakNode = CreateWeakReference(node);
                if (node.GetParent() is not null)
                    _effectiveNodesAndScenes.Add(weakNode);
                _effectiveNodes.Add(weakNode);
            }
        }
        foreach (var componentReference in _components)
        {
            if (componentReference.TryGetTarget(out var component))
            {
                var node = component.TryGetNode();
                if (node is not null)
                {
                    var weakNode = CreateWeakReference(node);
                    if (node.GetParent() is not null)
                        _effectiveNodesAndScenes.Add(weakNode);
                    _effectiveNodes.Add(weakNode);
                }
            }
        }

        if (_activeNodeOrScene is null || !_effectiveNodesAndScenes.Contains(_activeNodeOrScene))
            _activeNodeOrScene = GetFirst(_effectiveNodesAndScenes);

        if (_activeNode is null || !_effectiveNodes.Contains(_activeNode))
            _activeNode = GetFirst(_effectiveNodes);

        if (_activeObject is null || !_objects.Contains(_activeObject))
            _activeObject = GetFirst(_objects);
    }

    private static T? GetTarget<T>(WeakReference<T>? weakReference) where T : class
    {
        return weakReference is not null && weakReference.TryGetTarget(out var target) ? target : null;
    }

    private static WeakReference<T> CreateWeakReference<T>(T target) where T : class
    {
        var result = new WeakReference<T>(target);
        WeakReferenceTargetComparer<T>.Instance.Register(result, target);
        return result;
    }

    private static WeakReference<T>? GetFirst<T>(HashSet<WeakReference<T>> set) where T : class
    {
        foreach (var value in set)
            return value;
        return null;
    }

    private static bool ContainsTarget<T>(HashSet<WeakReference<T>> set, T target) where T : class
    {
        foreach (var value in set)
        {
            if (value.TryGetTarget(out var candidate) && ReferenceEquals(candidate, target))
                return true;
        }
        return false;
    }

    private static void RemoveTarget<T>(HashSet<WeakReference<T>> set, T target) where T : class
    {
        foreach (var value in set)
        {
            if (value.TryGetTarget(out var candidate) && ReferenceEquals(candidate, target))
            {
                set.Remove(value);
                return;
            }
        }
    }

    private sealed class WeakReferenceTargetComparer<T> : IEqualityComparer<WeakReference<T>> where T : class
    {
        public static readonly WeakReferenceTargetComparer<T> Instance = new();

        public bool Equals(WeakReference<T>? lhs, WeakReference<T>? rhs)
        {
            if (ReferenceEquals(lhs, rhs))
                return true;
            return lhs is not null && rhs is not null
                && lhs.TryGetTarget(out var lhsTarget)
                && rhs.TryGetTarget(out var rhsTarget)
                && ReferenceEquals(lhsTarget, rhsTarget);
        }

        public int GetHashCode(WeakReference<T> weakReference)
        {
            return _hashCodes.GetValue(weakReference, static value =>
            {
                var hashCode = value.TryGetTarget(out var target)
                    ? RuntimeHelpers.GetHashCode(target)
                    : RuntimeHelpers.GetHashCode(value);
                return new HashCodeBox(hashCode);
            }).Value;
        }

        public void Register(WeakReference<T> weakReference, T target)
        {
            _hashCodes.Add(weakReference, new HashCodeBox(RuntimeHelpers.GetHashCode(target)));
        }

        private sealed class HashCodeBox(int value)
        {
            public int Value { get; } = value;
        }

        private readonly ConditionalWeakTable<WeakReference<T>, HashCodeBox> _hashCodes = new();
    }

    private readonly HashSet<WeakReference<RedObject>> _objects =
        new(WeakReferenceTargetComparer<RedObject>.Instance);
    private readonly HashSet<WeakReference<Node>> _nodesAndScenes =
        new(WeakReferenceTargetComparer<Node>.Instance);
    private readonly HashSet<WeakReference<Node>> _nodes =
        new(WeakReferenceTargetComparer<Node>.Instance);
    private readonly HashSet<WeakReference<Component>> _components =
        new(WeakReferenceTargetComparer<Component>.Instance);

    private WeakReference<Node>? _activeNodeOrScene;
    private WeakReference<Node>? _activeNode;
    private WeakReference<RedObject>? _activeObject;

    private readonly HashSet<WeakReference<Node>> _effectiveNodesAndScenes =
        new(WeakReferenceTargetComparer<Node>.Instance);
    private readonly HashSet<WeakReference<Node>> _effectiveNodes =
        new(WeakReferenceTargetComparer<Node>.Instance);
    private uint _revision = 1;
}
