//
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
using RedBolt.Math;

/// <summary>Repeat inner action several times.</summary>
[Reflected]
public partial class Repeat : FiniteTimeAction
{
    private sealed class RepeatState : FiniteTimeActionState
    {
        private readonly FiniteTimeAction _innerAction;
        private FiniteTimeActionState _innerState;
        private readonly int _times;
        private int _total;
        private float _nextDt;
        private readonly float _slice;

        public RepeatState(Repeat action, RedObject? target)
            : base(action, target)
        {
            _innerAction = action.InnerAction!;
            _times = action.Times;

            _slice = _nextDt = _innerAction.Duration / action.Duration;

            _innerState = StartAction(_innerAction, target)!;
            _total = 0;
        }

        public override void Dispose()
        {
            _innerState.Dispose();
            base.Dispose();
        }

        public override bool IsDone => _total >= _times;

        public override void Update(float time)
        {
            if (time >= _nextDt)
            {
                while (time > _nextDt && _total < _times)
                {
                    _innerState.Update(1.0f);
                    _innerState.Stop();
                    _total++;

                    var innerState = StartAction(_innerAction, OriginalTarget)!;
                    _innerState.Dispose();
                    _innerState = innerState;
                    _nextDt = (_total + 1.0f) * _slice;
                }
                if (_total == _times)
                {
                    _innerState.Update(1.0f);
                    _innerState.Stop();
                }
                else
                {
                    _innerState.Update((time - (_nextDt - _slice)) / _slice);
                }
            }
            else
            {
                _innerState.Update(MathDefs.Mod(time * _times, 1.0f));
            }
        }

        public override void Stop()
        {
            _innerState.Stop();
            base.Stop();
        }
    }

    /// <summary>Construct.</summary>
    public Repeat()
    {
        _innerAction = GetOrDefault(null);
    }

    /// <summary>Get action duration.</summary>
    public override float Duration => _innerAction!.Duration * _times;

    /// <summary>Set inner action.</summary>
    public void SetInnerAction(FiniteTimeAction? action)
    {
        _innerAction = GetOrDefault(action);
        SetDuration(Duration);
    }

    /// <summary>Get inner action.</summary>
    public FiniteTimeAction? InnerAction => _innerAction;

    /// <summary>Set number of repetitions.</summary>
    public void SetTimes(int times)
    {
        _times = times;
        SetDuration(Duration);
    }

    /// <summary>Get number of repetitions.</summary>
    public int Times => _times;

    /// <summary>Create reversed action.</summary>
    public override FiniteTimeAction Reverse()
    {
        var result = new Repeat();
        result.SetTimes(_times);
        result.SetInnerAction(_innerAction!.Reverse());
        return result;
    }

    /// <summary>Serialize content from/to archive. May throw ArchiveException.</summary>
    public override void SerializeInBlock(Archive archive)
    {
        // Skipping FiniteTimeAction::SerializeInBlock on purpose to skip duration serialization
        ArchiveSerialization.SerializeValue(archive, "innerAction", ref _innerAction);
    }

    /// <summary>Create new action state from the action.</summary>
    protected internal override ActionState StartAction(RedObject? target)
    {
        return new RepeatState(this, target);
    }

    private int _times;
    private FiniteTimeAction? _innerAction;
}

/// <summary>Repeat inner action forever.</summary>
[Reflected]
public partial class RepeatForever : FiniteTimeAction
{
    private sealed class RepeatForeverState : FiniteTimeActionState
    {
        private readonly FiniteTimeAction _innerAction;
        private FiniteTimeActionState _innerState;

        public RepeatForeverState(RepeatForever action, RedObject? target)
            : base(action, target)
        {
            _innerAction = action.InnerAction!;
            _innerState = StartAction(_innerAction, target)!;
        }

        public override void Dispose()
        {
            _innerState.Dispose();
            base.Dispose();
        }

        public override bool IsDone => false;

        protected internal override void Step(float dt)
        {
            _innerState.Step(dt);
            if (_innerState.IsDone)
            {
                var innerDt = _innerState.Duration - _innerState.Elapsed;
                var innerState = StartAction(_innerAction, OriginalTarget)!;
                _innerState.Dispose();
                _innerState = innerState;
                _innerState.Step(0);
                _innerState.Step(innerDt);
            }
        }

        public override void Stop()
        {
            _innerState.Stop();
            base.Stop();
        }
    }

    /// <summary>Construct.</summary>
    public RepeatForever()
    {
        _innerAction = GetOrDefault(null);
    }

    /// <summary>Get action duration.</summary>
    public override float Duration => float.MaxValue;

    /// <summary>Set inner action.</summary>
    public void SetInnerAction(FiniteTimeAction? action)
    {
        _innerAction = GetOrDefault(action);
    }

    /// <summary>Get inner action.</summary>
    public FiniteTimeAction? InnerAction => _innerAction;

    /// <summary>Create reversed action.</summary>
    public override FiniteTimeAction Reverse()
    {
        var result = new RepeatForever();
        result.SetInnerAction(_innerAction!.Reverse());
        return result;
    }

    /// <summary>Serialize content from/to archive. May throw ArchiveException.</summary>
    public override void SerializeInBlock(Archive archive)
    {
        // Skipping FiniteTimeAction::SerializeInBlock on purpose to skip duration serialization
        ArchiveSerialization.SerializeValue(archive, "innerAction", ref _innerAction);
    }

    /// <summary>Create new action state from the action.</summary>
    protected internal override ActionState StartAction(RedObject? target)
    {
        return new RepeatForeverState(this, target);
    }

    private FiniteTimeAction? _innerAction;
}
