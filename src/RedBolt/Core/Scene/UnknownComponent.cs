// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Scene;

using RedBolt.Core;
using RedBolt.IO;
using RedBolt.Resource;

/// <summary>Placeholder for allowing unregistered components to be loaded &amp; saved along with scenes.</summary>
[Reflected]
public partial class UnknownComponent : Component
{
    /// <summary>Construct.</summary>
    public UnknownComponent()
    {
    }


    /// <summary>Load from binary data. Return true if successful.</summary>
    public override bool Load(Deserializer source)
    {
        SetTemporary(true);
        return true;
    }

    /// <summary>Load from XML data. Return true if successful.</summary>
    public override bool LoadXML(XMLElement source)
    {
        SetTemporary(true);
        return true;
    }

    /// <summary>Load from JSON data. Return true if successful.</summary>
    public override bool LoadJSON(JSONValue source)
    {
        SetTemporary(true);
        return true;
    }
}
