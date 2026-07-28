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

using RedBolt.Core;
using RedBolt.IO;

/// <summary>Sequence of actions</summary>
[Reflected]
public partial class Sequence : FiniteTimeAction
{
    private sealed class SequenceState : FiniteTimeActionState
    {
        public SequenceState(Sequence action, RedObject? target)
            : base(action, target)
        {
            _innerActions[0] = action.FirstAction!;
            _innerActions[1] = action.SecondAction!;
            _hasInfiniteAction = _innerActions[0] is RepeatForever
                || _innerActions[1] is RepeatForever;
            _split = _innerActions[0]!.Duration / Duration;
        }

        public override void Dispose()
        {
            _innerActionStates[1]?.Dispose();
            _innerActionStates[0]?.Dispose();
            base.Dispose();
        }

        /// <summary>Gets a value indicating whether this instance is done.</summary>
        public override bool IsDone
        {
            get
            {
                if (_hasInfiniteAction && _innerActions[_last] is RepeatForever)
                    return false;
                return base.IsDone;
            }
        }

        public override void Update(float time)
        {
            var found = 0;
            var newTime = 0.0f;

            if (time < _split)
            {
                // action[0]
                found = 0;
                if (_split != 0)
                    newTime = time / _split;
                else
                    newTime = 1;
            }
            else
            {
                // action[1]
                found = 1;
                if (_split == 1)
                    newTime = 1;
                else
                    newTime = (time - _split) / (1 - _split);
            }

            if (found == 1)
            {
                if (_last == -1)
                {
                    // action[0] was skipped, execute it.
                    _innerActionStates[0] = StartAction(_innerActions[0], Target);
                    _innerActionStates[0]!.Update(1.0f);
                    _innerActionStates[0]!.Stop();
                }
                else if (_last == 0)
                {
                    _innerActionStates[0]!.Update(1.0f);
                    _innerActionStates[0]!.Stop();
                }
            }
            else if (found == 0 && _last == 1)
            {
                // Reverse mode ?
                // XXX: Bug. this case doesn't contemplate when _last==-1, found=0 and in "reverse mode"
                // since it will require a hack to know if an action is on reverse mode or not.
                // "step" should be overriden, and the "reverseMode" value propagated to inner Sequences.
                _innerActionStates[1]!.Update(0);
                _innerActionStates[1]!.Stop();
            }

            // Last action found and it is done.
            if (found == _last && _innerActionStates[found]!.IsDone)
                return;

            // Last action found and it is done
            if (found != _last)
            {
                var innerActionState = StartAction(_innerActions[found], Target);
                _innerActionStates[found]?.Dispose();
                _innerActionStates[found] = innerActionState;
            }

            _innerActionStates[found]!.Update(newTime);
            _last = found;
        }

        public override void Stop()
        {
            // Issue #1305
            if (_last != -1)
                _innerActionStates[_last]!.Stop();
        }

        protected internal override void Step(float dt)
        {
            if (_last > -1 && _innerActions[_last] is RepeatForever)
                _innerActionStates[_last]!.Step(dt);
            else
                base.Step(dt);
        }

        private readonly FiniteTimeActionState?[] _innerActionStates = new FiniteTimeActionState?[2];
        private readonly FiniteTimeAction?[] _innerActions = new FiniteTimeAction?[2];

        private readonly bool _hasInfiniteAction;
        private readonly float _split;
        private int _last = -1;
    }

    /// <summary>Construct.</summary>
    public Sequence()
    {
        _actions[0] = GetOrDefault(null);
        _actions[1] = GetOrDefault(null);
    }

    /// <summary>Set first action in sequence.</summary>
    public void SetFirstAction(FiniteTimeAction? action)
    {
        _actions[0] = GetOrDefault(action);
        SetDuration(Duration);
    }

    /// <summary>Set second action in sequence.</summary>
    public void SetSecondAction(FiniteTimeAction? action)
    {
        _actions[1] = GetOrDefault(action);
        SetDuration(Duration);
    }

    /// <summary>Get action duration.</summary>
    public override float Duration => _actions[0]!.Duration + _actions[1]!.Duration;

    /// <summary>Get first action.</summary>
    public FiniteTimeAction? FirstAction => _actions[0];

    /// <summary>Get second action.</summary>
    public FiniteTimeAction? SecondAction => _actions[1];

    /// <summary>Create reversed action.</summary>
    public override FiniteTimeAction Reverse()
    {
        var result = new Sequence();
        result.SetFirstAction(SecondAction);
        result.SetSecondAction(FirstAction);
        return result;
    }

    /// <summary>Serialize content from/to archive. May throw ArchiveException.</summary>
    public override void SerializeInBlock(Archive archive)
    {
        base.SerializeInBlock(archive);
        ArchiveSerialization.SerializeValue(archive, "first", ref _actions[0]);
        ArchiveSerialization.SerializeValue(archive, "second", ref _actions[1]);
    }

    /// <summary>Create new action state from the action.</summary>
    protected internal override ActionState StartAction(RedObject? target)
    {
        return new SequenceState(this, target);
    }

    private readonly FiniteTimeAction?[] _actions = new FiniteTimeAction?[2];
}
