// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

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
