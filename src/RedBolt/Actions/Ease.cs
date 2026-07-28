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
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR rhs
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR rhsWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR rhs DEALINGS IN
// THE SOFTWARE.
//

namespace RedBolt.Actions;

using RedBolt.Core;
using RedBolt.IO;

file sealed class ActionEaseState : FiniteTimeActionState
{
    public ActionEaseState(ActionEase action, RedObject? target)
        : base(action, target)
    {
        _action = action;
        _innerAction = StartAction(action.InnerAction, target);
    }

    /// <summary>Destruct.</summary>
    public override void Dispose()
    {
        _innerAction?.Dispose();
        base.Dispose();
    }

    /// <summary>Called every frame with it's delta time.</summary>
    public override void Update(float dt)
    {
        if (_innerAction is not null)
        {
            _innerAction.Update(_action.Ease(dt));
        }
    }

    private readonly ActionEase _action;
    private readonly FiniteTimeActionState? _innerAction;
}

/// <summary>Base action state.</summary>
[Reflected]
public partial class ActionEase : FiniteTimeAction
{
    /// <summary>Construct.</summary>
    public ActionEase()
    {
    }

    /// <summary>Get action duration.</summary>
    public override float Duration
    {
        get
        {
            if (_innerAction is not null)
            {
                return _innerAction.Duration;
            }
            return 1.1920929E-07f;
        }
    }

    /// <summary>Set inner action.</summary>
    public void SetInnerAction(FiniteTimeAction? action)
    {
        _innerAction = GetOrDefault(action);
        SetDuration(_innerAction.Duration);
    }

    /// <summary>Get inner action.</summary>
    public FiniteTimeAction? InnerAction => _innerAction;

    /// <summary>Serialize content from/to archive. May throw ArchiveException.</summary>
    public override void SerializeInBlock(Archive archive)
    {
        // Skipping FiniteTimeAction::SerializeInBlock on purpose to skip duration serialization
        ArchiveSerialization.SerializeValue(archive, "innerAction", ref _innerAction);
    }

    /// <summary>Create reversed action.</summary>
    public override FiniteTimeAction Reverse()
    {
        var result = new ActionEase();
        if (_innerAction is not null)
        {
            result.SetInnerAction(_innerAction.Reverse());
        }
        return result;
    }

    /// <summary>Apply easing function to the time argument.</summary>
    public virtual float Ease(float time) => time;

    private FiniteTimeAction? _innerAction;
}
