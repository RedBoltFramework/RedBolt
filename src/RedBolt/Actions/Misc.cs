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
using RedBolt.Scene;

/// <summary>Remove self from parent. The Target of the action should be either Node or UIElement.</summary>
[Reflected]
public partial class RemoveSelf : FiniteTimeAction
{
    private sealed class RemoveSelfState : FiniteTimeActionState
    {
        public RemoveSelfState(RemoveSelf action, RedObject? target)
            : base(action, target)
        {
        }

        public override void Update(float time)
        {
            var target = Target;
            if (target is null)
            {
                return;
            }
            if (target is Node node)
            {
                node.Remove();
            }
        }
    }

    /// <summary>Construct.</summary>
    public RemoveSelf()
    {
    }

    /// <summary>Create new action state from the action.</summary>
    protected internal override ActionState StartAction(RedObject? target)
    {
        return new RemoveSelfState(this, target);
    }
}

/// <summary>Show target. The Target should have "Is Visible" attribute.</summary>
[Reflected]
public partial class Show : AttributeAction
{
    /// <summary>Construct.</summary>
    public Show()
        : base(AttributeActionConstants.IsVisibleAttribute)
    {
    }

    /// <summary>Create new action state from the action.</summary>
    protected internal override ActionState StartAction(RedObject? target)
    {
        using var value = new Variant(true);
        return new SetAttributeState(this, target, GetAttribute(target), value);
    }
}

/// <summary>Hide target. The Target should have "Is Visible" attribute.</summary>
[Reflected]
public partial class Hide : AttributeAction
{
    /// <summary>Construct.</summary>
    public Hide()
        : base(AttributeActionConstants.IsVisibleAttribute)
    {
    }

    /// <summary>Create new action state from the action.</summary>
    protected internal override ActionState StartAction(RedObject? target)
    {
        using var value = new Variant(false);
        return new SetAttributeState(this, target, GetAttribute(target), value);
    }
}
