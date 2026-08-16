// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Scene;

[Flags]
public enum SceneLookupFlag
{
    None = 0x0,

    /// <summary>Whether to do recursive search in the scene subtree.</summary>
    Recursive = 0x1,

    //IgnoreTemporary = 0x2,

    /// <summary>Used for lazy node lookup. Whether to validate the existing node name.</summary>
    ValidateName = 0x4,

    /// <summary>Used for lazy node lookup. Whether to validate that the existing node is a child of the queried node.</summary>
    ValidateRelation = 0x8,
}
