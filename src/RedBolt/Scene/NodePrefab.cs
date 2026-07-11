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

    public void Import(Serializable? serializable, PrefabSaveFlag flags = PrefabSaveFlag.None)
    {
        TypeName = serializable!.GetTypeName();
        Temporary = serializable.IsTemporary;

        Attributes.Clear();
        for (var attributeIndex = 0u; attributeIndex < serializable.Attributes.Count; ++attributeIndex)
        {
            var attribute = serializable.Attributes[(int)attributeIndex];

            // If temporary attribute saving is enabled, it overrides other flags.
            var shouldSave = attribute.ShouldSave
                || ((flags & PrefabSaveFlag.SaveTemporary) != 0 && (attribute.Mode & AttributeMode.Temporary) != 0);
            if (!shouldSave)
                continue;

            if ((attribute.Mode & AttributeMode.Prefab) == 0 && (flags & PrefabSaveFlag.Prefab) != 0)
                continue;

            var value = Variant.FromObject(serializable.OnGetAttribute(attribute));

            // Skip defaults if allowed
            if ((flags & PrefabSaveFlag.SaveDefaultValues) == 0)
            {
                var defaultValue = Variant.FromObject(serializable.GetAttributeDefault(attributeIndex));
                if (value == defaultValue)
                    continue;
            }

            // TODO: port compact attribute names once AttributePrefab supports hash identifiers.
            var attributePrefab = new AttributePrefab { Name = attribute.Name };
            if ((flags & PrefabSaveFlag.EnumsAsStrings) != 0 && attribute.EnumNames.Count != 0)
                value = new Variant(attribute.ConvertEnumToString(value.GetUInt()));

            attributePrefab.Value = value;
            Attributes.Add(attributePrefab);
        }
    }

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

    public void Clear()
    {
        Node.TypeName = string.Empty;
        Node.Id = 0;
        Node.Temporary = false;
        Node.Attributes.Clear();
        Components.Clear();
        Children.Clear();
    }

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
