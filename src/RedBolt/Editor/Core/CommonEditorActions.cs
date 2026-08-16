// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Editor.Core;

using System;
using System.Collections.Generic;
using System.Diagnostics;

using RedBolt.Core;
using RedBolt.Math;
using RedBolt.Scene;
using RedBolt.Utility;

/// <summary>Empty action.</summary>
public class EmptyEditorAction : EditorAction
{
    /// <summary>Implement EditorAction.</summary>
    public override bool RemoveOnUndo() => true;

    public override void Redo()
    {
    }

    public override void Undo()
    {
    }
}

/// <summary>Composite action.</summary>
public class CompositeEditorAction : EditorAction
{
    /// <summary>Add action to composite.</summary>
    public void AddAction(EditorAction action) => _actions.Add(action);

    public void EmplaceAction<T>(T action)
        where T : EditorAction
    {
        _actions.Add(action);
    }

    /// <summary>Implement EditorAction.</summary>
    public override bool CanRedo()
    {
        foreach (var action in _actions)
        {
            if (!action.CanRedo())
                return false;
        }
        return true;
    }

    public override bool CanUndo()
    {
        foreach (var action in _actions)
        {
            if (!action.CanUndo())
                return false;
        }
        return true;
    }

    public override void Redo()
    {
        foreach (var action in _actions)
            action.Redo();
    }

    public override void Undo()
    {
        for (var i = _actions.Count - 1; i >= 0; --i)
            _actions[i].Undo();
    }

    private readonly List<EditorAction> _actions = [];
}

/// <summary>Create or remove node.</summary>
public class CreateRemoveNodeAction : EditorAction
{
    public CreateRemoveNodeAction(Node node, bool removed)
    {
        _removed = removed;
        var scene = node.Scene;
        _scene = scene is not null ? new WeakReference<Scene>(scene) : null;
        _data = new PackedNodeData(node);
    }

    /// <summary>Implement EditorAction.</summary>
    public override bool CanUndoRedo()
    {
        return _scene is not null && _scene.TryGetTarget(out _);
    }

    public override void Redo()
    {
        if (_removed)
            RemoveNode();
        else
            AddNode();
    }

    public override void Undo()
    {
        if (_removed)
            AddNode();
        else
            RemoveNode();
    }

    private void AddNode()
    {
        if (_scene is null || !_scene.TryGetTarget(out var scene))
            return;

        var node = _data.SpawnExact(scene);
        if (node is null)
            throw new UndoException($"Cannot create node with id {_data.Id}");
    }

    private void RemoveNode()
    {
        if (_scene is null || !_scene.TryGetTarget(out var scene))
            return;

        var node = scene.GetNode(_data.Id);
        if (node is not null)
            node.Remove();
        else
            throw new UndoException($"Cannot remove node with id {_data.Id}");
    }

    private readonly bool _removed;
    private readonly WeakReference<Scene>? _scene;
    private readonly PackedNodeData _data;
}

/// <summary>Create or remove component.</summary>
public class CreateRemoveComponentAction : EditorAction
{
    public CreateRemoveComponentAction(Component component, bool removed)
    {
        _removed = removed;
        var scene = component.Scene;
        _scene = scene is not null ? new WeakReference<Scene>(scene) : null;
        _data = new PackedComponentData(component);
    }

    /// <summary>Implement EditorAction.</summary>
    public override bool CanUndoRedo()
    {
        return _scene is not null && _scene.TryGetTarget(out _);
    }

    public override void Redo()
    {
        if (_removed)
            RemoveComponent();
        else
            AddComponent();
    }

    public override void Undo()
    {
        if (_removed)
            AddComponent();
        else
            RemoveComponent();
    }

    private void AddComponent()
    {
        if (_scene is null || !_scene.TryGetTarget(out var scene))
            return;

        var component = _data.SpawnExact(scene);
        if (component is null)
            throw new UndoException($"Cannot create component with id {_data.Id}");
    }

    private void RemoveComponent()
    {
        if (_scene is null || !_scene.TryGetTarget(out var scene))
            return;

        var component = scene.GetComponent(_data.Id);
        if (component is not null)
            component.Remove();
        else
            throw new UndoException($"Cannot remove component with id {_data.Id}");
    }

    private readonly bool _removed;
    private readonly WeakReference<Scene>? _scene;
    private readonly PackedComponentData _data;
}

/// <summary>Change node transform.</summary>
public class ChangeNodeTransformAction : EditorAction
{
    public ChangeNodeTransformAction(Node node, Transform oldTransform)
    {
        var scene = node.Scene;
        _scene = scene is not null ? new WeakReference<Scene>(scene) : null;
        _nodes = new Dictionary<uint, NodeData>
        {
            [node.Id] = new NodeData
            {
                OldTransform = oldTransform,
                NewTransform = node.GetTransform()
            }
        };
    }

    /// <summary>Implement EditorAction.</summary>
    public override bool CanUndoRedo()
    {
        return _scene is not null && _scene.TryGetTarget(out _);
    }

    public override void Redo()
    {
        if (_scene is null || !_scene.TryGetTarget(out var scene))
            return;

        foreach (var pair in _nodes)
        {
            var node = scene.GetNode(pair.Key);
            if (node is not null)
                node.SetTransform(pair.Value.NewTransform);
            else
                throw new UndoException($"Cannot find node with id {pair.Key}");
        }
    }

    public override void Undo()
    {
        if (_scene is null || !_scene.TryGetTarget(out var scene))
            return;

        foreach (var pair in _nodes)
        {
            var node = scene.GetNode(pair.Key);
            if (node is not null)
                node.SetTransform(pair.Value.OldTransform);
            else
                throw new UndoException($"Cannot find node with id {pair.Key}");
        }
    }

    public override bool MergeWith(EditorAction other)
    {
        if (other is not ChangeNodeTransformAction otherAction)
            return false;

        var scene = _scene is not null && _scene.TryGetTarget(out var sceneTarget) ? sceneTarget : null;
        var otherScene = otherAction._scene is not null && otherAction._scene.TryGetTarget(out var otherSceneTarget)
            ? otherSceneTarget
            : null;
        if (!ReferenceEquals(scene, otherScene))
            return false;

        foreach (var pair in otherAction._nodes)
        {
            if (_nodes.TryGetValue(pair.Key, out var nodeData))
            {
                nodeData.NewTransform = pair.Value.NewTransform;
                _nodes[pair.Key] = nodeData;
            }
            else
                _nodes[pair.Key] = pair.Value;
        }
        return true;
    }

    private struct NodeData
    {
        public Transform OldTransform;
        public Transform NewTransform;
    }

    private readonly WeakReference<Scene>? _scene;
    private readonly Dictionary<uint, NodeData> _nodes;
}

/// <summary>Change attribute values of nodes.</summary>
public class ChangeNodeAttributesAction : EditorAction
{
    public ChangeNodeAttributesAction(
        Scene scene,
        string attributeName,
        IEnumerable<Node> nodes,
        VariantVector oldValues,
        VariantVector newValues)
    {
        _scene = new WeakReference<Scene>(scene);
        _attributeName = attributeName;
        _oldValues = new VariantVector(oldValues);
        _newValues = new VariantVector(newValues);

        foreach (var node in nodes)
            _nodeIds.Add(node.Id);

        Debug.Assert(_nodeIds.Count == _oldValues.Count);
        Debug.Assert(_nodeIds.Count == _newValues.Count);
    }

    /// <summary>Implement EditorAction.</summary>
    public override bool CanUndoRedo()
    {
        if (!_scene.TryGetTarget(out var scene))
            return false;

        foreach (var nodeId in _nodeIds)
        {
            if (scene.GetNode(nodeId) is null)
                return false;
        }
        return true;
    }

    public override void Redo()
    {
        SetAttributeValues(_newValues);
    }

    public override void Undo()
    {
        SetAttributeValues(_oldValues);
    }

    private void SetAttributeValues(VariantVector values)
    {
        if (!_scene.TryGetTarget(out var scene))
            return;

        for (var index = 0; index < _nodeIds.Count; ++index)
        {
            var node = scene.GetNode(_nodeIds[index]);
            if (node is not null)
            {
                node.SetAttribute(_attributeName, values[index]);
                node.ApplyAttributes();
            }
        }
    }

    public override bool MergeWith(EditorAction other)
    {
        if (other is not ChangeNodeAttributesAction otherAction)
            return false;

        var scene = _scene.TryGetTarget(out var sceneTarget) ? sceneTarget : null;
        var otherScene = otherAction._scene.TryGetTarget(out var otherSceneTarget) ? otherSceneTarget : null;
        if (!ReferenceEquals(scene, otherScene) || _nodeIds.Count != otherAction._nodeIds.Count)
            return false;

        for (var index = 0; index < _nodeIds.Count; ++index)
        {
            if (_nodeIds[index] != otherAction._nodeIds[index])
                return false;
        }

        var newValues = new VariantVector(otherAction._newValues);
        _newValues.Dispose();
        _newValues = newValues;
        return true;
    }

    private readonly WeakReference<Scene> _scene;
    private readonly string _attributeName;
    private readonly List<uint> _nodeIds = [];
    private readonly VariantVector _oldValues;
    private VariantVector _newValues;
}

/// <summary>Change attribute values of a components.</summary>
public class ChangeComponentAttributesAction : EditorAction
{
    public ChangeComponentAttributesAction(
        Scene scene,
        string attributeName,
        IEnumerable<Component> components,
        VariantVector oldValues,
        VariantVector newValues)
    {
        _scene = new WeakReference<Scene>(scene);
        _attributeName = attributeName;
        _oldValues = new VariantVector(oldValues);
        _newValues = new VariantVector(newValues);

        foreach (var component in components)
            _componentIds.Add(component.Id);

        Debug.Assert(_componentIds.Count == _oldValues.Count);
        Debug.Assert(_componentIds.Count == _newValues.Count);
    }

    /// <summary>Implement EditorAction.</summary>
    public override bool CanUndoRedo()
    {
        if (!_scene.TryGetTarget(out var scene))
            return false;

        foreach (var componentId in _componentIds)
        {
            if (scene.GetComponent(componentId) is null)
                return false;
        }
        return true;
    }

    public override void Redo()
    {
        SetAttributeValues(_newValues);
    }

    public override void Undo()
    {
        SetAttributeValues(_oldValues);
    }

    private void SetAttributeValues(VariantVector values)
    {
        if (!_scene.TryGetTarget(out var scene))
            return;

        for (var index = 0; index < _componentIds.Count; ++index)
        {
            var component = scene.GetComponent(_componentIds[index]);
            if (component is not null)
            {
                component.SetAttribute(_attributeName, values[index]);
                component.ApplyAttributes();
            }
        }
    }

    public override bool MergeWith(EditorAction other)
    {
        if (other is not ChangeComponentAttributesAction otherAction)
            return false;

        var scene = _scene.TryGetTarget(out var sceneTarget) ? sceneTarget : null;
        var otherScene = otherAction._scene.TryGetTarget(out var otherSceneTarget) ? otherSceneTarget : null;
        if (!ReferenceEquals(scene, otherScene) || _componentIds.Count != otherAction._componentIds.Count)
            return false;

        for (var index = 0; index < _componentIds.Count; ++index)
        {
            if (_componentIds[index] != otherAction._componentIds[index])
                return false;
        }

        var newValues = new VariantVector(otherAction._newValues);
        _newValues.Dispose();
        _newValues = newValues;
        return true;
    }

    private readonly WeakReference<Scene> _scene;
    private readonly string _attributeName;
    private readonly List<uint> _componentIds = [];
    private readonly VariantVector _oldValues;
    private VariantVector _newValues;
}

/// <summary>Reorder node in the parent node.</summary>
public class ReorderNodeAction : EditorAction
{
    public ReorderNodeAction(Node node, int oldIndex, int newIndex)
    {
        var scene = node.Scene;
        _scene = scene is not null ? new WeakReference<Scene>(scene) : null;
        _parentNodeId = node.Parent!.Id;
        _nodeId = node.Id;
        _oldIndex = oldIndex;
        _newIndex = newIndex;
    }

    /// <summary>Implement EditorAction.</summary>
    public override bool CanUndoRedo()
    {
        return _scene is not null && _scene.TryGetTarget(out var scene)
            && scene.GetNode(_parentNodeId) is not null && scene.GetNode(_nodeId) is not null;
    }

    public override void Redo()
    {
        Reorder(_newIndex);
    }

    public override void Undo()
    {
        Reorder(_oldIndex);
    }

    private void Reorder(int index)
    {
        var scene = _scene is not null && _scene.TryGetTarget(out var sceneTarget) ? sceneTarget : null;
        var parentNode = scene!.GetNode(_parentNodeId);
        var node = scene.GetNode(_nodeId);
        if (parentNode is null)
            throw new UndoException($"Cannot find parent node with id {_parentNodeId}");
        else if (node is null)
            throw new UndoException($"Cannot find node with id {_nodeId}");
        else
            parentNode.ReorderChild(node, index);
    }

    public override bool MergeWith(EditorAction other)
    {
        if (other is not ReorderNodeAction otherAction)
            return false;

        var scene = _scene is not null && _scene.TryGetTarget(out var sceneTarget) ? sceneTarget : null;
        var otherScene = otherAction._scene is not null && otherAction._scene.TryGetTarget(out var otherSceneTarget)
            ? otherSceneTarget
            : null;
        if (!ReferenceEquals(scene, otherScene) || _nodeId != otherAction._nodeId
            || _parentNodeId != otherAction._parentNodeId)
        {
            return false;
        }

        _newIndex = otherAction._newIndex;
        return true;
    }

    private readonly WeakReference<Scene>? _scene;
    private readonly uint _parentNodeId;
    private readonly uint _nodeId;
    private readonly int _oldIndex;
    private int _newIndex;
}

/// <summary>Reorder component in the node.</summary>
public class ReorderComponentAction : EditorAction
{
    public ReorderComponentAction(Component component, int oldIndex, int newIndex)
    {
        var scene = component.Scene;
        _scene = scene is not null ? new WeakReference<Scene>(scene) : null;
        _nodeId = component.Node.Id;
        _componentId = component.Id;
        _oldIndex = oldIndex;
        _newIndex = newIndex;
    }

    /// <summary>Implement EditorAction.</summary>
    public override bool CanUndoRedo()
    {
        return _scene is not null && _scene.TryGetTarget(out var scene)
            && scene.GetNode(_nodeId) is not null && scene.GetComponent(_componentId) is not null;
    }

    public override void Redo()
    {
        Reorder(_newIndex);
    }

    public override void Undo()
    {
        Reorder(_oldIndex);
    }

    private void Reorder(int index)
    {
        var scene = _scene is not null && _scene.TryGetTarget(out var sceneTarget) ? sceneTarget : null;
        var node = scene!.GetNode(_nodeId);
        var component = scene.GetComponent(_componentId);
        if (node is null)
            throw new UndoException($"Cannot find node with id {_nodeId}");
        else if (component is null)
            throw new UndoException($"Cannot find component with id {_componentId}");
        else
            node.ReorderComponent(component, index);
    }

    public override bool MergeWith(EditorAction other)
    {
        if (other is not ReorderComponentAction otherAction)
            return false;

        var scene = _scene is not null && _scene.TryGetTarget(out var sceneTarget) ? sceneTarget : null;
        var otherScene = otherAction._scene is not null && otherAction._scene.TryGetTarget(out var otherSceneTarget)
            ? otherSceneTarget
            : null;
        if (!ReferenceEquals(scene, otherScene) || _nodeId != otherAction._nodeId
            || _componentId != otherAction._componentId)
        {
            return false;
        }

        _newIndex = otherAction._newIndex;
        return true;
    }

    private readonly WeakReference<Scene>? _scene;
    private readonly uint _nodeId;
    private readonly uint _componentId;
    private readonly int _oldIndex;
    private int _newIndex;
}

/// <summary>Reparent node.</summary>
public class ReparentNodeAction : EditorAction
{
    public ReparentNodeAction(Node node, Node newParent)
    {
        var scene = node.Scene;
        _scene = scene is not null ? new WeakReference<Scene>(scene) : null;
        _nodeId = node.Id;
        _oldParentId = node.Parent!.Id;
        _oldIndex = node.GetIndexInParent();
        _newParentId = newParent.Id;
    }

    /// <summary>Implement EditorAction.</summary>
    public override bool CanUndoRedo()
    {
        return _scene is not null && _scene.TryGetTarget(out var scene)
            && scene.GetNode(_nodeId) is not null && scene.GetNode(_oldParentId) is not null
            && scene.GetNode(_newParentId) is not null;
    }

    public override void Redo()
    {
        Reparent(_newParentId, null);
    }

    public override void Undo()
    {
        Reparent(_oldParentId, _oldIndex);
    }

    private void Reparent(uint parentId, int? index)
    {
        var scene = _scene is not null && _scene.TryGetTarget(out var sceneTarget) ? sceneTarget : null;
        var node = scene!.GetNode(_nodeId);
        var parent = scene.GetNode(parentId);
        if (node is null)
            throw new UndoException($"Cannot find node with id {_nodeId}");
        else if (parent is null)
            throw new UndoException($"Cannot find parent node with id {parentId}");
        else
        {
            node.SetParent(parent);
            if (index.HasValue)
                parent.ReorderChild(node, index.Value);
        }
    }

    public override bool MergeWith(EditorAction other)
    {
        if (other is not ReparentNodeAction otherAction)
            return false;

        var scene = _scene is not null && _scene.TryGetTarget(out var sceneTarget) ? sceneTarget : null;
        var otherScene = otherAction._scene is not null && otherAction._scene.TryGetTarget(out var otherSceneTarget)
            ? otherSceneTarget
            : null;
        if (!ReferenceEquals(scene, otherScene) || _nodeId != otherAction._nodeId)
            return false;

        _newParentId = otherAction._newParentId;
        return true;
    }

    private readonly WeakReference<Scene>? _scene;
    private readonly uint _nodeId;
    private readonly uint _oldParentId;
    private readonly int _oldIndex;
    private uint _newParentId;
}

/// <summary>Change entire Component.</summary>
public class ChangeComponentAction : EditorAction
{
    public ChangeComponentAction(Scene? scene, PackedComponentData oldData, Component newData)
    {
        _scene = scene is not null ? new WeakReference<Scene>(scene) : null;
        _oldData = oldData.Clone();
        _newData = new PackedComponentData(newData);
    }

    public ChangeComponentAction(Scene? scene, PackedComponentData oldData, PackedComponentData newData)
    {
        _scene = scene is not null ? new WeakReference<Scene>(scene) : null;
        _oldData = oldData.Clone();
        _newData = newData.Clone();
    }

    /// <summary>Implement EditorAction.</summary>
    public override bool CanUndoRedo()
    {
        return _scene is not null && _scene.TryGetTarget(out var scene)
            && scene.GetComponent(_oldData.Id) is not null;
    }

    public override void Redo()
    {
        UpdateComponent(_oldData.Id, _newData);
    }

    public override void Undo()
    {
        UpdateComponent(_oldData.Id, _oldData);
    }

    private void UpdateComponent(uint componentId, PackedComponentData data)
    {
        var scene = _scene is not null && _scene.TryGetTarget(out var sceneTarget) ? sceneTarget : null;
        var component = scene!.GetComponent(componentId);
        data.Update(component!);
    }

    public override bool MergeWith(EditorAction other)
    {
        if (other is not ChangeComponentAction otherAction)
            return false;

        var scene = _scene is not null && _scene.TryGetTarget(out var sceneTarget) ? sceneTarget : null;
        var otherScene = otherAction._scene is not null && otherAction._scene.TryGetTarget(out var otherSceneTarget)
            ? otherSceneTarget
            : null;
        if (!ReferenceEquals(scene, otherScene))
            return false;

        _newData = otherAction._newData.Clone();
        return true;
    }

    private readonly WeakReference<Scene>? _scene;
    private readonly PackedComponentData _oldData;
    private PackedComponentData _newData;
}

/// <summary>Change subtree of nodes.</summary>
public class ChangeNodeSubtreeAction : EditorAction
{
    public ChangeNodeSubtreeAction(Scene? scene, PackedNodeData oldData, Node? newData)
    {
        _scene = scene is not null ? new WeakReference<Scene>(scene) : null;
        _oldData = oldData.Clone();
        _newData = newData is not null ? new PackedNodeData(newData) : new PackedNodeData();
        _newRemoved = newData is null;
    }

    public ChangeNodeSubtreeAction(Scene? scene, PackedNodeData oldData, PackedNodeData newData)
    {
        _scene = scene is not null ? new WeakReference<Scene>(scene) : null;
        _oldData = oldData.Clone();
        _newData = newData.Clone();
        _newRemoved = false;
    }

    /// <summary>Implement EditorAction.</summary>
    public override bool CanUndoRedo()
    {
        return _scene is not null && _scene.TryGetTarget(out _);
    }

    public override void Redo()
    {
        UpdateSubtree(_oldData.Id, _newRemoved ? null : _newData);
    }

    public override void Undo()
    {
        UpdateSubtree(_oldData.Id, _oldData);
    }

    private void UpdateSubtree(uint nodeId, PackedNodeData? data)
    {
        var scene = _scene is not null && _scene.TryGetTarget(out var sceneTarget) ? sceneTarget : null;
        var oldNode = scene!.GetNode(nodeId);
        if (oldNode is not null)
            oldNode.Remove();
        if (data is not null)
            data.SpawnExact(scene);
    }

    public override bool MergeWith(EditorAction other)
    {
        if (other is not ChangeNodeSubtreeAction otherAction)
            return false;

        var scene = _scene is not null && _scene.TryGetTarget(out var sceneTarget) ? sceneTarget : null;
        var otherScene = otherAction._scene is not null && otherAction._scene.TryGetTarget(out var otherSceneTarget)
            ? otherSceneTarget
            : null;
        if (!ReferenceEquals(scene, otherScene) || _newData.Id != otherAction._newData.Id)
            return false;

        _newData = otherAction._newData.Clone();
        _newRemoved = otherAction._newRemoved;
        return true;
    }

    private readonly WeakReference<Scene>? _scene;
    private readonly PackedNodeData _oldData;
    private PackedNodeData _newData;
    private bool _newRemoved;
}

/// <summary>Change entire scene.</summary>
public class ChangeSceneAction : EditorAction
{
    public ChangeSceneAction(Scene? scene, PackedSceneData oldData)
    {
        _scene = scene is not null ? new WeakReference<Scene>(scene) : null;
        _oldData = oldData.Clone();
        _newData = PackedSceneData.FromScene(scene!);
    }

    public ChangeSceneAction(Scene? scene, PackedSceneData oldData, PackedSceneData newData)
    {
        _scene = scene is not null ? new WeakReference<Scene>(scene) : null;
        _oldData = oldData.Clone();
        _newData = newData.Clone();
    }

    /// <summary>Implement EditorAction.</summary>
    public override bool CanUndoRedo()
    {
        return _scene is not null && _scene.TryGetTarget(out _);
    }

    public override void Redo()
    {
        UpdateScene(_newData);
    }

    public override void Undo()
    {
        UpdateScene(_oldData);
    }

    private void UpdateScene(PackedSceneData data)
    {
        var scene = _scene is not null && _scene.TryGetTarget(out var sceneTarget) ? sceneTarget : null;
        data.ToScene(scene!);
    }

    public override bool MergeWith(EditorAction other)
    {
        if (other is not ChangeSceneAction otherAction)
            return false;

        var scene = _scene is not null && _scene.TryGetTarget(out var sceneTarget) ? sceneTarget : null;
        var otherScene = otherAction._scene is not null && otherAction._scene.TryGetTarget(out var otherSceneTarget)
            ? otherSceneTarget
            : null;
        if (!ReferenceEquals(scene, otherScene))
            return false;

        _newData = otherAction._newData.Clone();
        return true;
    }

    private readonly WeakReference<Scene>? _scene;
    private readonly PackedSceneData _oldData;
    private PackedSceneData _newData;
}
