namespace RedBolt;

public class SerializableResource : SimpleResource
{
    private static readonly byte[] SerializableBinaryMagic = [0, (byte)'S', (byte)'R', (byte)'L'];
    public SerializableResource(string name)
        : base(name)
    {
    }

    public Serializable? Value { get; private set; }

    public Serializable? GetValue() => Value;

    public void SetValue(Serializable? serializable)
    {
        Value = serializable;
    }

    public override void SerializeInBlock(Archive archive)
    {
        // C++: calls serializable->SerializeInBlock(archive) directly for entire object
        // C++: no attribute-level serialization; value_ DynamicCast from context_->CreateObject
        // C++: throws ArchiveException on creation failure
        if (archive.IsInput)
        {
            var typeName = string.Empty;
            if (!archive.IsUnorderedAccessSupportedInCurrentBlock || archive.HasElementOrBlock("type"))
                ArchiveSerialization.SerializeValue(archive, "type", ref typeName);
            if (string.IsNullOrEmpty(typeName))
            {
                Value = null;
                return;
            }

            var serializable = CreateSerializable(typeName);
            using (archive.OpenUnorderedBlock("value"))
                SerializeAttributes(archive, serializable);
            serializable.ApplyAttributes();
            Value = serializable;
        }
        else if (Value is not null)
        {
            var typeName = Value.GetTypeName();
            ArchiveSerialization.SerializeValue(archive, "type", ref typeName);
            using (archive.OpenUnorderedBlock("value"))
                SerializeAttributes(archive, Value);
        }
    }

    protected override byte[] GetBinaryMagic() => SerializableBinaryMagic;

    protected override string GetRootBlockName() => "serializable";

    private static Serializable CreateSerializable(string typeName)
    {
        if (Context.Instance.CreateObject(new StringHash(typeName)) is not Serializable serializable)
            throw new InvalidOperationException($"Failed to create object of type '{typeName}'.");

        return serializable;
    }

    private static void SerializeAttributes(Archive archive, Serializable serializable)
    {
        if (archive.IsInput)
        {
            if (archive.IsUnorderedAccessSupportedInCurrentBlock && !archive.HasElementOrBlock("attributes"))
                return;

            uint numAttributes = 0;
            using var attributesBlock = archive.OpenArrayBlock("attributes", numAttributes);
            for (var index = 0u; index < attributesBlock.SizeHint; ++index)
            {
                using var attributeBlock = archive.OpenUnorderedBlock("attribute");
                var name = string.Empty;
                ArchiveSerialization.SerializeValue(archive, "name", ref name);
                var attribute = FindAttribute(serializable, name);
                if (attribute is null || !attribute.ShouldLoad)
                    continue;

                var value = new Variant(attribute.DefaultValue.Type);
                using (archive.OpenUnorderedBlock("value"))
                    ArchiveSerialization.SerializeVariantInBlock(archive, ref value);
                serializable.SetAttribute(attribute.Name, value.GetObject());
            }
            return;
        }

        var savedAttributes = serializable.Attributes.Where(static attribute => attribute.ShouldSave).ToArray();
        var numSavedAttributes = checked((uint)savedAttributes.Length);
        using var savedAttributesBlock = archive.OpenArrayBlock("attributes", numSavedAttributes);
        foreach (var attribute in savedAttributes)
        {
            using var attributeBlock = archive.OpenUnorderedBlock("attribute");
            var name = attribute.Name;
            ArchiveSerialization.SerializeValue(archive, "name", ref name);
            using var valueBlock = archive.OpenUnorderedBlock("value");
            var value = Variant.FromObject(serializable.GetAttribute(attribute.Name));
            ArchiveSerialization.SerializeVariantInBlock(archive, ref value);
        }
    }

    private static AttributeInfo? FindAttribute(Serializable serializable, string name)
    {
        foreach (var attribute in serializable.Attributes)
        {
            if (string.Equals(attribute.Name, name, StringComparison.Ordinal))
                return attribute;
        }

        return null;
    }

}
