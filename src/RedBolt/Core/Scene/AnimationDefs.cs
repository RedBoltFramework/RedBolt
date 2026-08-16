// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Scene;

using RedBolt.Graphics;

/// <summary>Animation wrap mode.</summary>
public enum WrapMode
{
    /// <summary>Loop mode.</summary>
    Loop = 0,
    /// <summary>Play once, when animation finished it will be removed.</summary>
    Once,
    /// <summary>Clamp mode.</summary>
    Clamp
}
