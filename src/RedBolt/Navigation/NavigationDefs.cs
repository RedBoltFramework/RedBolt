// Copyright (c) 2024-2024 the rbfx project.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Navigation;

public enum NavmeshPartitionType
{
    Watershed = 0,
    Monotone
}

public static class NavigationDefs
{
    /// <summary>Special area ID. Evaluates to RC_WALKABLE_AREA or 0 depending on polygon slope.</summary>
    public const byte DeduceAreaId = 0xff;
}
