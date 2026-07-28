//
// Copyright (c) 2015 Xamarin Inc.
// Copyright (c) 2022 the rbfx project.
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

namespace RedBolt.Actions;

using System;
using System.Collections.Generic;

using Microsoft.Extensions.Logging;

using RedBolt.Core;
using RedBolt.Logging;

/// <summary>Action manager.</summary>
public class ActionManager : RedObject
{
    private sealed class HashElement
    {
        public int ActionIndex;
        public readonly List<ActionState?> ActionStates = [];
        public ActionState? CurrentActionState;
        public bool CurrentActionSalvaged;
        public bool Paused;
        public WeakReference<RedObject>? Target;
    }

    private sealed class EmptyState : FiniteTimeActionState
    {
        public EmptyState(FiniteTimeAction action, RedObject? target)
            : base(action, target)
        {
        }

        /// <summary>Gets a value indicating whether this instance is done.</summary>
        public override bool IsDone => true;

        /// <summary>Called every frame with it's delta time.</summary>
        protected internal override void Step(float dt)
        {
        }
    }

    private sealed class EmptyAction : FiniteTimeAction
    {
        public EmptyAction()
        {
            _state = new EmptyState(this, null);
        }

        /// <summary>Get action duration.</summary>
        public override float Duration => 1.1920929E-07f;

        /// <summary>Create reversed action.</summary>
        public override FiniteTimeAction Reverse()
        {
            // Empty action is immutable so we can reuse it.
            return this;
        }

        /// <summary>Create new action state from the action.</summary>
        protected internal override ActionState StartAction(RedObject? target) => _state;

        private readonly EmptyState _state;
    }

    public ActionManager()
        : this(true)
    {
    }

    public ActionManager(bool autoupdate)
    {
        _emptyAction = new EmptyAction();
        ActionLibrary.RegisterActionLibrary(Context);
        if (autoupdate)
            SubscribeToEvent<CoreEvents.UpdateEvent>(HandleUpdate);
    }

    public override void Dispose()
    {
        CancelAllActions();
        _emptyAction.Dispose();
        base.Dispose();
    }

    public void CompleteAllActions()
    {
        if (_targets.Count == 0)
            return;

        _tmpKeysArray.Clear();

        foreach (var element in _targets)
        {
            if (element.Target is not null)
                _tmpKeysArray.Add(element.Target);
        }

        foreach (var targetReference in _tmpKeysArray)
        {
            if (targetReference.TryGetTarget(out var target))
                CompleteAllActionsOnTarget(target);
            else
            {
                foreach (var element in _targets)
                {
                    if (ReferenceEquals(element.Target, targetReference))
                    {
                        foreach (var actionState in element.ActionStates)
                            (actionState as IDisposable)?.Dispose();
                        element.ActionStates.Clear();
                        _targets.Remove(element);
                        break;
                    }
                }
            }
        }
    }

    public void CancelAllActions()
    {
        if (_targets.Count == 0)
            return;

        _tmpKeysArray.Clear();

        foreach (var element in _targets)
        {
            if (element.Target is not null)
                _tmpKeysArray.Add(element.Target);
        }

        foreach (var targetReference in _tmpKeysArray)
        {
            if (targetReference.TryGetTarget(out var target))
                CancelAllActionsFromTarget(target);
            else
            {
                foreach (var element in _targets)
                {
                    if (ReferenceEquals(element.Target, targetReference))
                    {
                        foreach (var actionState in element.ActionStates)
                            (actionState as IDisposable)?.Dispose();
                        element.ActionStates.Clear();
                        _targets.Remove(element);
                        break;
                    }
                }
            }
        }
    }

    public void CompleteAllActionsOnTarget(RedObject? target)
    {
        if (target is null)
            return;

        HashElement? element = null;
        foreach (var candidate in _targets)
        {
            if (candidate.Target is not null
                && candidate.Target.TryGetTarget(out var candidateTarget)
                && ReferenceEquals(candidateTarget, target))
            {
                element = candidate;
                break;
            }
        }

        if (element is not null)
        {
            if (element.ActionStates.Contains(element.CurrentActionState) && !element.CurrentActionSalvaged)
                element.CurrentActionSalvaged = true;

            foreach (var action in element.ActionStates)
            {
                if (action is null)
                    continue;
                // Do a zero step to be sure that the action is initialized.
                action.Step(0.0f);
                var duration = float.MaxValue;
                if (action.Action is FiniteTimeAction finiteTimeAction)
                {
                    duration = finiteTimeAction.Duration;
                    if (duration >= float.MaxValue)
                        duration = 0.0f;
                }
                // Do a step beyond duration to complete the action.
                action.Step(duration * 2.0f);
            }
            foreach (var action in element.ActionStates)
            {
                if (!ReferenceEquals(action, element.CurrentActionState))
                    (action as IDisposable)?.Dispose();
            }
            element.ActionStates.Clear();
            if (ReferenceEquals(_currentTarget, target))
            {
                _currentTargetSalvaged = true;
            }
            else
            {
                _targets.Remove(element);
            }
        }
    }

    public void CancelAllActionsFromTarget(RedObject? target)
    {
        if (target is null)
            return;

        HashElement? element = null;
        foreach (var candidate in _targets)
        {
            if (candidate.Target is not null
                && candidate.Target.TryGetTarget(out var candidateTarget)
                && ReferenceEquals(candidateTarget, target))
            {
                element = candidate;
                break;
            }
        }

        if (element is not null)
        {
            if (element.ActionStates.Contains(element.CurrentActionState) && !element.CurrentActionSalvaged)
                element.CurrentActionSalvaged = true;

            foreach (var action in element.ActionStates)
            {
                if (!ReferenceEquals(action, element.CurrentActionState))
                    (action as IDisposable)?.Dispose();
            }
            element.ActionStates.Clear();
            if (ReferenceEquals(_currentTarget, target))
            {
                _currentTargetSalvaged = true;
            }
            else
            {
                _targets.Remove(element);
            }
        }
    }

    public void CancelAction(ActionState? actionState)
    {
        if (actionState is null || actionState.OriginalTarget is not { } target)
            return;

        HashElement? element = null;
        foreach (var candidate in _targets)
        {
            if (candidate.Target is not null
                && candidate.Target.TryGetTarget(out var candidateTarget)
                && ReferenceEquals(candidateTarget, target))
            {
                element = candidate;
                break;
            }
        }

        if (element is not null)
        {
            for (var index = element.ActionStates.Count - 1; index >= 0; --index)
            {
                if (ReferenceEquals(element.ActionStates[index], actionState))
                {
                    element.ActionStates.RemoveAt(index);
                    if (!ReferenceEquals(element.CurrentActionState, actionState))
                        (actionState as IDisposable)?.Dispose();
                }
            }
        }
    }

    public int GetNumActions(RedObject? target)
    {
        if (target is null)
            return 0;
        foreach (var element in _targets)
        {
            if (element.Target is not null
                && element.Target.TryGetTarget(out var candidateTarget)
                && ReferenceEquals(candidateTarget, target))
            {
                return element.ActionStates.Count;
            }
        }
        return 0;
    }

    /// <summary>Add action to the action manager.</summary>
    public ActionState? AddAction(BaseAction? action, RedObject? target, bool paused = false)
    {
        if (action is null)
        {
            Log.GetLogger().LogError("Action parameter is nullptr");
            return null;
        }
        if (target is null)
        {
            Log.GetLogger().LogError("Target parameter is nullptr");
            return null;
        }

        HashElement? element = null;
        foreach (var candidate in _targets)
        {
            if (candidate.Target is not null
                && candidate.Target.TryGetTarget(out var candidateTarget)
                && ReferenceEquals(candidateTarget, target))
            {
                element = candidate;
                break;
            }
        }
        // Check if element is missing or if we have a record with conflicting pointer (happens in unit tests).
        if (element is null)
        {
            element = new HashElement();
            _targets.Add(element);
            // Hack to handle expired target with reused pointer.
            element.ActionStates.Clear();
            element.Paused = paused;
            element.Target = new WeakReference<RedObject>(target);
        }

        foreach (var existingState in element.ActionStates)
        {
            if (existingState is not null && ReferenceEquals(existingState.Action, action))
            {
                Log.GetLogger().LogError("Action is already running for this target.");
                return null;
            }
        }
        var state = action.StartAction(target);
        element.ActionStates.Add(state);

        return state;
    }

    public void Update(float dt)
    {
        if (_targets.Count == 0)
            return;

        var count = _targets.Count;

        _tmpKeysArray.Clear();
        foreach (var element in _targets)
        {
            if (element.Target is not null)
                _tmpKeysArray.Add(element.Target);
        }

        for (var i = 0; i < count; i++)
        {
            if (i >= _tmpKeysArray.Count)
                continue;

            var keyReference = _tmpKeysArray[i];
            HashElement? element = null;
            foreach (var candidate in _targets)
            {
                if (ReferenceEquals(candidate.Target, keyReference))
                {
                    element = candidate;
                    break;
                }
            }
            if (element is null)
                continue;

            if (!keyReference.TryGetTarget(out var target))
            {
                foreach (var actionState in element.ActionStates)
                    (actionState as IDisposable)?.Dispose();
                element.ActionStates.Clear();
                _targets.Remove(element);
                continue;
            }

            _currentTarget = target;
            _currentTargetSalvaged = false;

            if (!element.Paused)
            {
                // The 'actions' may change while inside this loop.
                for (element.ActionIndex = 0;
                     element.ActionIndex < element.ActionStates.Count;
                     element.ActionIndex++)
                {
                    element.CurrentActionState = element.ActionStates[element.ActionIndex];
                    if (element.CurrentActionState is null)
                        continue;
                    if (element.Target is null || !element.Target.TryGetTarget(out _))
                        break;

                    element.CurrentActionSalvaged = false;

                    element.CurrentActionState.Step(dt);

                    if (element.CurrentActionSalvaged)
                    {
                        // The currentAction told the node to remove it. To prevent the action from
                        // accidentally deallocating itself before finishing its step, we retained
                        // it. Now that step is done, it's safe to release it.

                        // currentTarget->currentAction->release();
                        (element.CurrentActionState as IDisposable)?.Dispose();
                        element.CurrentActionState = null;
                    }
                    else if (element.CurrentActionState.IsDone)
                    {
                        element.CurrentActionState.Stop();

                        var actionState = element.CurrentActionState;
                        // Make currentAction nil to prevent removeAction from salvaging it.
                        element.CurrentActionState = null;
                        CancelAction(actionState);
                    }

                    if (element.CurrentActionState is not null
                        && !element.ActionStates.Contains(element.CurrentActionState))
                    {
                        (element.CurrentActionState as IDisposable)?.Dispose();
                    }
                    element.CurrentActionState = null;
                }
            }

            // only delete currentTarget if no actions were scheduled during the cycle (issue #481)
            if (_currentTargetSalvaged && element.ActionStates.Count == 0)
                _targets.Remove(element);
        }

        _currentTarget = null;
    }

    public FiniteTimeAction GetEmptyAction() => _emptyAction;

    private void HandleUpdate(ref CoreEvents.UpdateEvent eventData)
    {
        // Then execute user-defined update function
        Update(eventData.TimeStep);
    }

    // Current target strong pointer to keep target alive while manager operates on actions.
    private RedObject? _currentTarget;
    private bool _currentTargetSalvaged;
    private readonly List<HashElement> _targets = [];
    private readonly List<WeakReference<RedObject>> _tmpKeysArray = [];
    private readonly FiniteTimeAction _emptyAction;
}

public static class ActionLibrary
{
    /// <summary>Register Particle Graph library objects.</summary>
    public static void RegisterActionLibrary(Context context)
    {
        context.RegisterAssembly(typeof(ActionLibrary).Assembly);
    }
}
