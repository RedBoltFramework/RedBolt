// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Scene;

[Flags]
public enum UpdateEvent : uint
{
    /// <summary>Bitmask for not using any events.</summary>
    UseNoEvent = 0x0,

    /// <summary>Bitmask for using the scene update event.</summary>
    UseUpdate = 0x1,

    /// <summary>Bitmask for using the scene post-update event.</summary>
    UsePostUpdate = 0x2,

    /// <summary>Bitmask for using the physics update event.</summary>
    UseFixedUpdate = 0x4,

    /// <summary>Bitmask for using the physics post-update event.</summary>
    UseFixedPostUpdate = 0x8,

    /// <summary>Bitmask for using the world origin update event.</summary>
    UseWorldOriginUpdate = 0x10,

    /// <summary>Bitmask for using the post world origin update event.</summary>
    UseWorldOriginPostUpdate = 0x20
}
