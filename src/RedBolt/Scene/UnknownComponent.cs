namespace RedBolt;

public sealed class UnknownComponent : Component
{
    public string OriginalTypeName { get; private set; } = string.Empty;

    public UnknownComponent()
    {
        RegisterAttribute(
            "Original Type Name",
            typeof(string),
            self => ((UnknownComponent)self).GetOriginalTypeName(),
            (self, value) => ((UnknownComponent)self).SetOriginalTypeName(value as string ?? string.Empty),
            string.Empty,
            AttributeMode.Default | AttributeMode.NoEdit);
    }

    public void SetOriginalTypeName(string typeName)
    {
        OriginalTypeName = typeName ?? string.Empty;
    }

    public string GetOriginalTypeName() => OriginalTypeName;

    public new bool Load(Stream source)
    {
        ArgumentNullException.ThrowIfNull(source);
        SetTemporary(true);
        return true;
    }

    public override bool LoadXML(XMLElement source)
    {
        ArgumentNullException.ThrowIfNull(source);
        SetTemporary(true);
        return true;
    }

    public override bool LoadJSON(JSONValue source)
    {
        ArgumentNullException.ThrowIfNull(source);
        SetTemporary(true);
        return true;
    }
}
