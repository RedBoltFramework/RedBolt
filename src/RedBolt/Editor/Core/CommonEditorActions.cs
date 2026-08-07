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
