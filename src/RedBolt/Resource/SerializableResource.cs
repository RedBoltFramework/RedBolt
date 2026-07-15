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
                serializable.SerializeInBlock(archive);
            Value = serializable;
        }
        else if (Value is not null)
        {
            var typeName = Value.GetTypeName();
            ArchiveSerialization.SerializeValue(archive, "type", ref typeName);
            using (archive.OpenUnorderedBlock("value"))
                Value.SerializeInBlock(archive);
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

}
