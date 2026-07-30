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
using System.Diagnostics;

using RedBolt.Core;

/// <summary>Internal helper class for invoking action handler functions.</summary>
public abstract class ActionCallHandler : IDisposable
{
    /// <summary>Construct with specified receiver and userdata.</summary>
    public ActionCallHandler(RedObject? receiver, object? userData = null)
    {
        _receiver = receiver;
        _userData = userData;
    }

    /// <summary>Destruct.</summary>
    public virtual void Dispose()
    {
    }

    /// <summary>Invoke event handler function.</summary>
    public abstract void Invoke(RedObject? target);

    /// <summary>Return event receiver.</summary>
    public RedObject? Receiver => _receiver;

    /// <summary>Return userdata.</summary>
    public object? UserData => _userData;

    /// <summary>Event receiver.</summary>
    protected RedObject? _receiver;
    /// <summary>Userdata.</summary>
    protected object? _userData;
}

/// <summary>Template implementation of the action handler invoke helper (stores a function pointer of specific class).</summary>
public class ActionCallHandlerImpl<T> : ActionCallHandler
    where T : RedObject
{
    public delegate void HandlerFunctionPtr(T receiver, RedObject? target);

    /// <summary>Construct with receiver and function pointers and userdata.</summary>
    public ActionCallHandlerImpl(T? receiver, HandlerFunctionPtr function, object? userData = null)
        : base(receiver, userData)
    {
        _function = function;
        Debug.Assert(_receiver is not null);
        Debug.Assert(_function is not null);
    }

    /// <summary>Invoke event handler function.</summary>
    public override void Invoke(RedObject? target)
    {
        var receiver = (T)_receiver!;
        _function(receiver, target);
    }

    /// <summary>Class-specific pointer to handler function.</summary>
    private readonly HandlerFunctionPtr _function;
}

/// <summary>Call function on each action tick.</summary>
/// <remarks>This action is not serializable.</remarks>
[Reflected]
public partial class CallFunc : ActionInstant
{
    private sealed class CallFuncState : FiniteTimeActionState
    {
        public CallFuncState(CallFunc action, RedObject? target)
            : base(action, target)
        {
            _callHandler = action.CallHandler;
        }

        public override void Update(float time)
        {
            if (_callHandler is not null)
                _callHandler.Invoke(Target);
        }

        private readonly ActionCallHandler? _callHandler;
    }

    /// <summary>Construct.</summary>
    public CallFunc()
    {
    }

    /// <summary>Get call handler</summary>
    public ActionCallHandler? CallHandler => _actionCallHandler;

    /// <summary>Set call handler</summary>
    public void SetCallHandler(ActionCallHandler? handler)
    {
        _actionCallHandler = handler;
    }

    /// <summary>Create new action state from the action.</summary>
    protected internal override ActionState StartAction(RedObject? target)
    {
        return new CallFuncState(this, target);
    }

    protected ActionCallHandler? _actionCallHandler;
}

/// <summary>Send typed event on target object.</summary>
public class SendEvent<T> : ActionInstant
    where T : struct
{
    private sealed class SendEventState : FiniteTimeActionState
    {
        public SendEventState(SendEvent<T> action, RedObject? target)
            : base(action, target)
        {
            _eventData = action.EventData;
        }

        public override void Update(float time)
        {
            Target!.SendEvent(ref _eventData);
        }

        private T _eventData;
    }

    /// <summary>Construct.</summary>
    public SendEvent()
    {
    }

    /// <summary>Get event args.</summary>
    public T EventData => _eventData;

    /// <summary>Set event args.</summary>
    public void SetEventData(in T eventData)
    {
        _eventData = eventData;
    }

    /// <summary>Create new action state from the action.</summary>
    protected internal override ActionState StartAction(RedObject? target)
    {
        return new SendEventState(this, target);
    }

    /// <summary>Event arguments.</summary>
    private T _eventData;
}
