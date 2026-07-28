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
using RedBolt.IO;

/// <summary>Finite time action.</summary>
[Reflected]
public partial class ActionInstant : FiniteTimeAction
{
    /// <summary>Construct.</summary>
    public ActionInstant()
    {
    }

    /// <summary>Get action duration.</summary>
    public override float Duration => 1.1920929E-07f;

    /// <summary>Serialize content from/to archive. May throw ArchiveException.</summary>
    public override void SerializeInBlock(Archive archive)
    {
        // Skip FiniteTimeAction::SerializeInBlock because duration is always 0
    }

    /// <summary>Create reversed action.</summary>
    public override FiniteTimeAction Reverse()
    {
        return new ActionInstant();
    }

    /// <summary>Create new action state from the action.</summary>
    protected internal override ActionState StartAction(RedObject? target)
    {
        return new ActionInstantState(this, target);
    }
}
