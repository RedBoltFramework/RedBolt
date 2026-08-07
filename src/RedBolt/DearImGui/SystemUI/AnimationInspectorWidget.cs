//
// Copyright (c) 2022-2022 the rbfx project.
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

namespace RedBolt.SystemUI;

using System.Collections.Generic;

using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.Resource;

/// <summary>SystemUI widget used to edit animation.</summary>
public class AnimationInspectorWidget : ResourceInspectorWidget
{
    public AnimationInspectorWidget(IReadOnlyList<Resource> resources)
        : base(resources, _properties)
    {
    }

    public override void Dispose()
    {
        base.Dispose();
    }

    public override bool CanSave() => false;

    private static readonly PropertyDesc[] _properties =
    [
        new()
        {
            Name = "Length",
            DefaultValue = new Variant(0.0f),
            Getter = static resource => new Variant(((Animation)resource).Length),
            Setter = static (Resource resource, in Variant value) =>
                ((Animation)resource).SetLength(value.GetFloat()),
            Hint = "Length in seconds",
        },
    ];
}
