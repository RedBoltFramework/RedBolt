// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Scene;

/// <summary>Reaction to world origin updated.</summary>
public enum WorldOriginUpdateMode
{
    /// <summary>This node is moved automatically. Children nodes are ignored.</summary>
    Move,

    /// <summary>This node and its children are ignored.</summary>
    Ignore,

    /// <summary>This node is ignored. Children nodes are checked instead.</summary>
    Recurse
}
