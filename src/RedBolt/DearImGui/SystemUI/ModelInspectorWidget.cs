// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.SystemUI;

using System.Collections.Generic;

using RedBolt.Resource;

/// <summary>SystemUI widget used to edit models.</summary>
public class ModelInspectorWidget : ResourceInspectorWidget
{
    public ModelInspectorWidget(IReadOnlyList<Resource> resources)
        : base(resources, _properties)
    {
    }

    public override void Dispose()
    {
        base.Dispose();
    }

    public override bool CanSave() => false;

    private static readonly PropertyDesc[] _properties = [];
}
