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
