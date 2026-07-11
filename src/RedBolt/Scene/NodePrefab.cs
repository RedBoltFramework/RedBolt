namespace RedBolt;

public sealed class AttributePrefab
{
    public string Name { get; set; } = string.Empty;
    public Variant Value { get; set; }

    public void SerializeInBlock(Archive archive)
    {
        var name = Name;
        ArchiveSerialization.SerializeValue(archive, "name", ref name);
        Name = name;
        var value = Value;
        using (archive.OpenUnorderedBlock("value")) ArchiveSerialization.SerializeVariantInBlock(archive, ref value);
        Value = value;
    }
}

public sealed class SerializablePrefab
{
    public string TypeName { get; set; } = string.Empty;
    public uint Id { get; set; }
    public bool Temporary { get; set; }
    public List<AttributePrefab> Attributes { get; } = [];

    public void SerializeInBlock(Archive archive)
    {
        var type = TypeName; var id = Id; var temporary = Temporary;
        ArchiveSerialization.SerializeValue(archive, "type", ref type);
        ArchiveSerialization.SerializeValue(archive, "id", ref id);
        ArchiveSerialization.SerializeValue(archive, "temporary", ref temporary);
        TypeName = type; Id = id; Temporary = temporary;
        var count = archive.IsInput ? 0u : (uint)Attributes.Count;
        using var block = archive.OpenArrayBlock("attributes", count);
        if (archive.IsInput)
        {
            Attributes.Clear();
            for (var i = 0u; i < block.SizeHint; ++i)
            {
                using var item = archive.OpenUnorderedBlock("attribute");
                var attribute = new AttributePrefab(); attribute.SerializeInBlock(archive); Attributes.Add(attribute);
            }
        }
        else foreach (var attribute in Attributes)
        {
            using var item = archive.OpenUnorderedBlock("attribute"); attribute.SerializeInBlock(archive);
        }
    }
}

public sealed class NodePrefab
{
    public SerializablePrefab Node { get; } = new();
    public List<SerializablePrefab> Components { get; } = [];
    public List<NodePrefab> Children { get; } = [];

    public void SerializeInBlock(Archive archive)
    {
        using (archive.OpenUnorderedBlock("node")) Node.SerializeInBlock(archive);
        SerializeList(archive, "components", "component", Components, static (value, ar) => value.SerializeInBlock(ar));
        SerializeList(archive, "children", "child", Children, static (value, ar) => value.SerializeInBlock(ar));
    }

    private static void SerializeList<T>(Archive archive, string name, string itemName, List<T> values, Action<T, Archive> serialize)
        where T : new()
    {
        var count = archive.IsInput ? 0u : (uint)values.Count;
        using var block = archive.OpenArrayBlock(name, count);
        if (archive.IsInput)
        {
            values.Clear();
            for (var i = 0u; i < block.SizeHint; ++i)
            {
                using var item = archive.OpenUnorderedBlock(itemName);
                var value = new T(); serialize(value, archive); values.Add(value);
            }
        }
        else foreach (var value in values)
        {
            using var item = archive.OpenUnorderedBlock(itemName); serialize(value, archive);
        }
    }
}
