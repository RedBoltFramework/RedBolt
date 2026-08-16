// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.SystemUI;

using RedBolt.Core;

/// <summary>SystemUI base class for a widget.</summary>
public abstract class BaseWidget : RedObject
{
    public BaseWidget()
    {
    }

    public override void Dispose()
    {
        base.Dispose();
    }

    public abstract void RenderContent();
}
