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
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR rhs
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR rhsWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR rhs DEALINGS IN
// THE SOFTWARE.
//

namespace RedBolt.Actions;

using System;

using RedBolt.Core;

public class ActionState
{
    protected ActionState(BaseAction action, RedObject? target)
    {
        _action = action;
        _target = target is not null ? new WeakReference<RedObject>(target) : null;
        _originalTarget = target is not null ? new WeakReference<RedObject>(target) : null;
    }

    /// <summary>Called once per frame.</summary>
    /// <remarks>
    /// Time value is between 0 and 1.
    /// For example:
    /// 0 means that the action just started.
    /// 0.5 means that the action is in the middle.
    /// 1 means that the action is over.
    /// </remarks>
    public virtual void Update(float time)
    {
    }

    /// <summary>Gets the target.</summary>
    /// <remarks>
    /// Will be set with the 'StartAction' method of the corresponding Action.
    /// When the 'Stop' method is called, Target will be set to null.
    /// </remarks>
    public RedObject? Target => _target is not null && _target.TryGetTarget(out var target) ? target : null;

    /// <summary>Gets the original target.</summary>
    public RedObject? OriginalTarget =>
        _originalTarget is not null && _originalTarget.TryGetTarget(out var target) ? target : null;

    /// <summary>Gets the action.</summary>
    public BaseAction Action => _action;

    /// <summary>Gets a value indicating whether this instance is done.</summary>
    public virtual bool IsDone => true;

    /// <summary>Called after the action has finished.</summary>
    /// <remarks>
    /// It will set the 'Target' to null.
    /// IMPORTANT: You should never call this method manually. Instead, use: "target.StopAction(actionState);"
    /// </remarks>
    public virtual void Stop()
    {
        _target = null;
    }

    /// <summary>Called every frame with it's delta time.</summary>
    /// <remarks>DON'T override unless you know what you are doing.</remarks>
    protected internal virtual void Step(float dt)
    {
    }

    /// <summary>Call StartAction on an action.</summary>
    protected ActionState? StartAction(BaseAction? action, RedObject? target) =>
        action is not null ? action.StartAction(target) : null;

    private readonly BaseAction _action;
    /// <summary>Active target reference. Set to null when action is complete.</summary>
    private WeakReference<RedObject>? _target;
    /// <summary>Original target reference. To track target references in ActionManager.</summary>
    private readonly WeakReference<RedObject>? _originalTarget;
}
