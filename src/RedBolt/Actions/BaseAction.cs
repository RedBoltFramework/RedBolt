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

using RedBolt.Core;
using RedBolt.IO;
using RedBolt.Scene;

/// <summary>Base action state.</summary>
[Reflected]
public partial class BaseAction : Serializable
{
    private sealed class NoActionState : ActionState
    {
        public NoActionState(BaseAction action, RedObject? target)
            : base(action, target)
        {
        }
    }

    /// <summary>Construct.</summary>
    public BaseAction()
    {
    }

    /// <summary>Destruct.</summary>
    public override void Dispose()
    {
        base.Dispose();
    }

    /// <summary>Get action from argument or empty action.</summary>
    public BaseAction GetOrDefault(BaseAction? action)
    {
        if (action is not null)
            return action;
        return GetSubsystem<ActionManager>().GetEmptyAction();
    }

    /// <summary>Serialize content from/to archive. May throw ArchiveException.</summary>
    public override void SerializeInBlock(Archive archive)
    {
    }

    /// <summary>Create new action state from the action.</summary>
    protected internal virtual ActionState StartAction(RedObject? target) => new NoActionState(this, target);
}
