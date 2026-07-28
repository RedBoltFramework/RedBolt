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

/// <summary>Finite time action.</summary>
[Reflected]
public partial class FiniteTimeAction : BaseAction
{
    private sealed class NoAction : FiniteTimeAction
    {
        /// <summary>Construct.</summary>
        public NoAction(FiniteTimeAction reversed)
        {
            _reversed = reversed;
        }

        /// <summary>Destruct.</summary>
        public override void Dispose()
        {
            base.Dispose();
        }

        /// <summary>Create reversed action.</summary>
        public override FiniteTimeAction Reverse() => _reversed;

        private readonly FiniteTimeAction _reversed;
    }

    /// <summary>Construct.</summary>
    public FiniteTimeAction()
    {
    }

    /// <summary>Set action duration.</summary>
    public void SetDuration(float duration)
    {
        // Prevent division by 0
        if (duration < 1.1920929E-07f)
            duration = 1.1920929E-07f;

        _duration = duration;
    }

    /// <summary>Get action duration.</summary>
    public virtual float Duration => _duration;

    /// <summary>Serialize content from/to archive. May throw ArchiveException.</summary>
    public override void SerializeInBlock(Archive archive)
    {
        base.SerializeInBlock(archive);
        ArchiveSerialization.SerializeOptionalValue(
            archive, "duration", ref _duration, 1.1920929E-07f, ArchiveSerialization.SerializeValue);
    }

    /// <summary>Get action from argument or empty action.</summary>
    public FiniteTimeAction GetOrDefault(FiniteTimeAction? action)
    {
        if (action is not null)
            return action;
        return GetSubsystem<ActionManager>().GetEmptyAction();
    }

    /// <summary>Create reversed action.</summary>
    public virtual FiniteTimeAction Reverse() => new NoAction(this);

    private float _duration = 1.1920929E-07f;
}
