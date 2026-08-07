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
