using RedBolt.Core;
using RedBolt.IO;
using RedBolt.Resource;

namespace RedBolt.Scene;

/// <summary>Placeholder for allowing unregistered components to be loaded &amp; saved along with scenes.</summary>
public class UnknownComponent : Component
{
    /// <summary>Construct.</summary>
    public UnknownComponent()
    {
    }

    /// <summary>Register object factory.</summary>
    public static void RegisterObject(Context context)
    {
        context.AddFactoryReflection<UnknownComponent>();
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
