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
// ID corresponding to the temporal order of undo actions.
using EditorActionFrame = System.UInt64;

using RedBolt.Bindings.DearImGui;
using RedBolt.Core;
using RedBolt.Input;

/// <summary>Exception thrown when UndoManager stack is desynchronized with editor state.</summary>
public class UndoException : InvalidOperationException
{
    public UndoException(string message)
        : base(message)
    {
    }
}

/// <summary>Abstract undoable and redoable action.</summary>
public abstract class EditorAction
{
    /// <summary>Return whether the action should be completely removed from stack on undo.</summary>
    /// <remarks>Useful for injecting callback on undoing. Don't change any important state if true!</remarks>
    public virtual bool RemoveOnUndo() => false;

    /// <summary>Return whether the action is incomplete, e.g. "redo" state is not saved. Useful for heavy actions.</summary>
    public virtual bool IsComplete() => true;

    /// <summary>Return if action is transparent, i.e. it can be pushed to stack or ignored without desynchronization.</summary>
    public virtual bool IsTransparent() => false;

    /// <summary>Action is pushed to the stack.</summary>
    public virtual void OnPushed(EditorActionFrame frame)
    {
    }

    /// <summary>Complete action if needed. Called after merge attempt but before stack modification.</summary>
    /// <remarks>Called with force=false periodically.</remarks>
    public virtual void Complete(bool force)
    {
    }

    /// <summary>Return whether the action can be redone and undone.</summary>
    public virtual bool CanUndoRedo() => true;

    /// <summary>Return whether the action can be redone.</summary>
    public virtual bool CanRedo() => CanUndoRedo();

    /// <summary>Redo this action. May fail if external state has unexpectedly changed.</summary>
    public abstract void Redo();

    /// <summary>Return whether the action can be undone.</summary>
    public virtual bool CanUndo() => CanUndoRedo();

    /// <summary>Undo this action. May fail if external state has unexpectedly changed.</summary>
    public abstract void Undo();

    /// <summary>Try to merge this action with another. Return true if successfully merged.</summary>
    public virtual bool MergeWith(EditorAction other) => false;
}

/// <summary>Base class for action wrappers.</summary>
public class BaseEditorActionWrapper : EditorAction
{
    public BaseEditorActionWrapper(EditorAction action)
    {
        _action = action;
    }

    /// <summary>Implement EditorAction.</summary>
    public override bool RemoveOnUndo() => _action.RemoveOnUndo();

    public override bool IsComplete() => _action.IsComplete();

    public override bool IsTransparent() => _action.IsTransparent();

    public override void OnPushed(EditorActionFrame frame) => _action.OnPushed(frame);

    public override void Complete(bool force) => _action.Complete(force);

    public override bool CanRedo() => _action.CanRedo();

    public override void Redo() => _action.Redo();

    public override bool CanUndo() => _action.CanUndo();

    public override void Undo() => _action.Undo();

    public override bool MergeWith(EditorAction other)
    {
        if (GetType() != other.GetType())
            return false;

        var otherWrapper = (BaseEditorActionWrapper)other;
        return _action.MergeWith(otherWrapper._action);
    }

    protected EditorAction _action;
}

/// <summary>Manages undo stack and actions.</summary>
public class UndoManager : RedObject
{
    public UndoManager()
    {
        SubscribeToEvent<InputEvents.InputEndEvent>((ref InputEvents.InputEndEvent _) => Update());
    }

    /// <summary>Force new frame. Call it on any resource save.</summary>
    public void NewFrame()
    {
        ++_frame;
    }

    /// <summary>Push new action. May be merged with the top of the stack. Drops redo stack.</summary>
    public EditorActionFrame PushAction(EditorAction action)
    {
        ClearCanUndoRedo();
        action.OnPushed(_frame);

        if (!action.IsTransparent())
            _redoStack.Clear();

        if (NeedNewGroup())
            _undoStack.Add(new ActionGroup { Frame = _frame });

        // Try merge first
        var group = _undoStack[^1];
        if (group.Actions.Count != 0)
        {
            if (group.Actions[^1].MergeWith(action))
                return _frame;
        }

        group.Actions.Add(action);

        CommitIncompleteAction(true);
        if (!action.IsComplete())
        {
            _incompleteAction = action;
            _incompleteActionTimer.Reset();
        }

        return _frame;
    }

    /// <summary>Try to undo action. May fail if external state changed.</summary>
    public bool Undo()
    {
        try
        {
            if (_undoStack.Count == 0)
                return false;

            ClearCanUndoRedo();
            CommitIncompleteAction(true);

            var group = _undoStack[^1];
            if (!group.CanUndo())
                return false;

            for (var i = group.Actions.Count - 1; i >= 0; --i)
                group.Actions[i].Undo();

            group.Actions.RemoveAll(static action => action.RemoveOnUndo());

            if (group.Actions.Count != 0)
                _redoStack.Add(group);
            _undoStack.RemoveAt(_undoStack.Count - 1);
            return true;
        }
        catch (UndoException exception)
        {
            Debug.Fail($"Desynchronized on UndoManager::Undo: {exception.Message}");
            _redoStack.Clear();
            _undoStack.Clear();
            return false;
        }
    }

    /// <summary>Try to redo action. May fail if external state changed.</summary>
    public bool Redo()
    {
        try
        {
            if (_redoStack.Count == 0)
                return false;

            ClearCanUndoRedo();
            CommitIncompleteAction(true);

            var group = _redoStack[^1];
            if (!group.CanRedo())
                return false;

            foreach (var action in group.Actions)
                action.Redo();

            _undoStack.Add(group);
            _redoStack.RemoveAt(_redoStack.Count - 1);
            return true;
        }
        catch (UndoException exception)
        {
            Debug.Fail($"Desynchronized on UndoManager::Redo: {exception.Message}");
            _redoStack.Clear();
            _undoStack.Clear();
            return false;
        }
    }

    /// <summary>Return whether can undo.</summary>
    public bool CanUndo()
    {
        _canUndo ??= _undoStack.Count != 0 && _undoStack[^1].CanUndo();
        return _canUndo.Value;
    }

    /// <summary>Return whether can redo.</summary>
    public bool CanRedo()
    {
        _canRedo ??= _redoStack.Count != 0 && _redoStack[^1].CanRedo();
        return _canRedo.Value;
    }

    private sealed class ActionGroup
    {
        public EditorActionFrame Frame;
        public List<EditorAction> Actions = [];

        public bool CanRedo()
        {
            foreach (var action in Actions)
            {
                if (!action.CanRedo())
                    return false;
            }
            return true;
        }

        public bool CanUndo()
        {
            foreach (var action in Actions)
            {
                if (!action.CanUndo())
                    return false;
            }
            return true;
        }
    }

    private void ClearCanUndoRedo()
    {
        _canRedo = null;
        _canUndo = null;
    }

    private void Update()
    {
        var mouseDown = ImGui.IsMouseDown(ImGuiMouseButton.ImGuiMouseButtonLeft)
            || ImGui.IsMouseDown(ImGuiMouseButton.ImGuiMouseButtonRight)
            || ImGui.IsMouseDown(ImGuiMouseButton.ImGuiMouseButtonMiddle);
        if (!mouseDown)
            NewFrame();

        if (_incompleteAction is not null && NeedNewGroup()
            && _incompleteActionTimer.GetMSec(false) > _actionCompletionTimeoutMs)
        {
            CommitIncompleteAction(false);
        }
    }

    private bool NeedNewGroup() => _undoStack.Count == 0 || _undoStack[^1].Frame != _frame;

    private void CommitIncompleteAction(bool force)
    {
        if (_incompleteAction is null)
            return;

        _incompleteAction.Complete(force);
        if (_incompleteAction.IsComplete())
            _incompleteAction = null;
        else if (force)
            RedBolt.Logging.Log.GetLogger().Error("Incomplete action failed to complete when it was forced");
    }

    private readonly uint _actionCompletionTimeoutMs = 1000;

    private readonly List<ActionGroup> _undoStack = [];
    private readonly List<ActionGroup> _redoStack = [];
    private EditorActionFrame _frame;

    private EditorAction? _incompleteAction;
    private readonly Timer _incompleteActionTimer = new();

    private bool? _canUndo;
    private bool? _canRedo;
}
