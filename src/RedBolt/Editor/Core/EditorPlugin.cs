//
// Copyright (c) 2017-2020 the rbfx project.
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

namespace RedBolt.Editor.Core;

using System;

using RedBolt.Core;

/// <summary>Base class for any Editor plugin.</summary>
public abstract class EditorPlugin : RedObject
{
    public abstract string Name { get; }

    public abstract bool Apply(RedObject target);
}

public sealed class EditorPlugin<T> : EditorPlugin
    where T : RedObject
{
    public EditorPlugin(string name, Action<T> function)
    {
        _name = name;
        _function = function;
    }

    public override string Name => _name;

    public override bool Apply(RedObject target)
    {
        if (target is T derivedTarget)
        {
            _function(derivedTarget);
            return true;
        }
        return false;
    }

    private readonly string _name;
    private readonly Action<T> _function;
}
