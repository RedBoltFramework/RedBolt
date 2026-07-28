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

using System;

using RedBolt.Core;

/// <summary>Finite time action state.</summary>
public class FiniteTimeActionState : ActionState, IDisposable
{
    /// <summary>Construct.</summary>
    public FiniteTimeActionState(FiniteTimeAction action, RedObject? target)
        : base(action, target)
    {
        _duration = action.Duration;
    }

    /// <summary>Destruct.</summary>
    public virtual void Dispose()
    {
    }

    /// <summary>Gets a value indicating whether this instance is done.</summary>
    public override bool IsDone => _elapsed >= _duration;

    /// <summary>Called every frame with it's delta time.</summary>
    protected internal override void Step(float dt)
    {
        if (_firstTick)
        {
            _firstTick = false;
            _elapsed = 0.0f;
        }
        else
        {
            _elapsed += dt;
        }

        Update(Math.Clamp(_elapsed / Math.Max(_duration, 1.1920929E-07f), 0.0f, 1.0f));
    }

    /// <summary>Get action duration.</summary>
    public float Duration => _duration;

    /// <summary>Get action elapsed time.</summary>
    public float Elapsed => _elapsed;

    /// <summary>Call StartAction on an action.</summary>
    protected FiniteTimeActionState? StartAction(FiniteTimeAction? action, RedObject? target)
    {
        if (action is not null)
            return base.StartAction(action, target) as FiniteTimeActionState;
        return null;
    }

    private readonly float _duration;
    private float _elapsed;
    private bool _firstTick = true;
}
