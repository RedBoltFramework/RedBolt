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

using System;

using RedBolt.Core;
using RedBolt.Math;
using RedBolt.Scene;

/// <summary>Attribute action state.</summary>
public class AttributeActionState : FiniteTimeActionState
{
    /// <summary>Construct.</summary>
    public AttributeActionState(FiniteTimeAction action, RedObject? target, AttributeInfo? attribute)
        : base(action, target)
    {
        _attribute = attribute;
    }

    /// <summary>Destruct.</summary>
    public override void Dispose()
    {
        base.Dispose();
    }

    /// <summary>Called every frame with it's delta time and attribute value.</summary>
    protected virtual void Update(float dt, ref Variant value)
    {
    }

    protected void Get(ref Variant value)
    {
        if (_attribute is not null)
        {
            var serializable = (Serializable)Target!;
            value.Dispose();
            _attribute.Accessor!.Get(serializable, ref value);
            return;
        }
        value.Clear();
    }

    protected void Set(Variant value)
    {
        if (_attribute is not null)
        {
            var serializable = (Serializable)Target!;
            _attribute.Accessor!.Set(serializable, value);
        }
    }

    /// <summary>Get attribute value or default value.</summary>
    protected T Get<T>()
    {
        var tmp = default(Variant);
        try
        {
            if (_attribute is not null)
                _attribute.Accessor!.Get((Serializable)Target!, ref tmp);
            return tmp.Get<T>();
        }
        finally
        {
            tmp.Dispose();
        }
    }

    public override void Update(float dt)
    {
        if (_attribute is null)
            return;

        var dst = default(Variant);
        try
        {
            Get(ref dst);
            Update(dt, ref dst);
            Set(dst);
        }
        finally
        {
            dst.Dispose();
        }
    }

    protected AttributeInfo? _attribute;
}

public class SetAttributeState : AttributeActionState
{
    public SetAttributeState(FiniteTimeAction action, RedObject? target, AttributeInfo? attribute, Variant value)
        : base(action, target, attribute)
    {
        _value = value.Clone();
    }

    public override void Dispose()
    {
        _value.Dispose();
        base.Dispose();
    }

    protected override void Update(float time, ref Variant value)
    {
        if (!_triggered)
        {
            value.Dispose();
            value = _value.Clone();
            _triggered = true;
        }
    }

    private Variant _value;
    private bool _triggered;
}

public class AttributeBlinkState : AttributeActionState
{
    public AttributeBlinkState(
        FiniteTimeAction action, RedObject? target, AttributeInfo? attribute, Variant from,
        Variant to, int times)
        : base(action, target, attribute)
    {
        _from = from.Clone();
        _to = to.Clone();
        _times = Math.Max(1, times);
        Get(ref _originalState);
    }

    public override void Dispose()
    {
        _to.Dispose();
        _from.Dispose();
        _originalState.Dispose();
        base.Dispose();
    }

    protected override void Update(float time, ref Variant value)
    {
        var slice = 1.0f / _times;
        var m = MathDefs.Mod(time, slice);
        value.Dispose();
        value = (m > slice / 2 ? _from : _to).Clone();
    }

    public override void Stop()
    {
        Set(_originalState);
    }

    private int _times;
    private Variant _originalState;
    private Variant _from;
    private Variant _to;
}
