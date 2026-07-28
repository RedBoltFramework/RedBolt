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

using RedBolt.Core;
using RedBolt.IO;

/// <summary>Set of actions to be executed in parallel.</summary>
[Reflected]
public partial class Parallel : FiniteTimeAction
{
    private struct StateAndDuration
    {
        public FiniteTimeActionState State;
        public float TimeScale;
    }

    private sealed class ParallelState : FiniteTimeActionState
    {
        public ParallelState(Parallel action, RedObject? target)
            : base(action, target)
        {
            var numActions = action.NumActions;
            _innerActionStates = new List<StateAndDuration>(numActions);
            var totalDuration = action.Duration;
            for (var i = 0; i < numActions; ++i)
            {
                var innerAction = action.GetAction(i);
                var innerDuration = innerAction!.Duration;
                var state = StartAction(innerAction, target);
                if (state is not null)
                {
                    _innerActionStates.Add(new StateAndDuration
                    {
                        State = state,
                        TimeScale = totalDuration / innerDuration,
                    });
                }
            }
        }

        public override void Dispose()
        {
            for (var index = _innerActionStates.Count - 1; index >= 0; --index)
                _innerActionStates[index].State.Dispose();
            base.Dispose();
        }

        public override void Update(float time)
        {
            foreach (var state in _innerActionStates)
                state.State.Update(Math.Clamp(time * state.TimeScale, 0.0f, 1.0f));
        }

        public override void Stop()
        {
            foreach (var state in _innerActionStates)
                state.State.Stop();
            base.Stop();
        }

        private readonly List<StateAndDuration> _innerActionStates;
    }

    /// <summary>Construct.</summary>
    public Parallel()
    {
    }

    /// <summary>Set number of actions.</summary>
    public void SetNumActions(int num)
    {
        if (num < _actions.Count)
            _actions.RemoveRange(num, _actions.Count - num);
        else
            SetAction(num, null);
    }

    /// <summary>Set number of actions.</summary>
    public int NumActions => _actions.Count;

    /// <summary>Set action by index.</summary>
    public void SetAction(int index, FiniteTimeAction? action)
    {
        if (index < _actions.Count)
        {
            if (_actions.Capacity < index + 1)
                _actions.Capacity = index + 1;
            var empty = GetOrDefault(null);
            while (index < _actions.Count)
                _actions[index] = empty;
        }
        _actions[index] = GetOrDefault(action);
    }

    /// <summary>Add action.</summary>
    public void AddAction(FiniteTimeAction? action)
    {
        _actions.Add(action);
    }

    /// <summary>Get action duration.</summary>
    public override float Duration
    {
        get
        {
            var duration = 1.1920929E-07f;
            foreach (var action in _actions)
                duration = Math.Max(duration, action!.Duration);
            return duration;
        }
    }

    /// <summary>Get action by index.</summary>
    public FiniteTimeAction? GetAction(int index)
    {
        if (index >= _actions.Count)
            return GetSubsystem<ActionManager>().GetEmptyAction();
        return _actions[index];
    }

    /// <summary>Create reversed action.</summary>
    public override FiniteTimeAction Reverse()
    {
        var result = new Parallel();

        result._actions.Capacity = _actions.Count;
        foreach (var action in _actions)
            result._actions.Add(action!.Reverse());
        return result;
    }

    /// <summary>Serialize content from/to archive. May throw ArchiveException.</summary>
    public override void SerializeInBlock(Archive archive)
    {
        base.SerializeInBlock(archive);
        using var block = archive.OpenArrayBlock("actions", _actions.Count);
        if (archive.IsInput)
        {
            _actions.Clear();
            for (var index = 0; index < block.SizeHint; ++index)
                _actions.Add(null);
        }

        for (var index = 0; index < _actions.Count; ++index)
        {
            var action = _actions[index];
            ArchiveSerialization.SerializeValue(archive, "action", ref action);
            _actions[index] = action;
        }
    }

    /// <summary>Create new action state from the action.</summary>
    protected internal override ActionState StartAction(RedObject? target)
    {
        return new ParallelState(this, target);
    }

    private readonly List<FiniteTimeAction?> _actions = [];
}
