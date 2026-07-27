using RedBolt.Container;
using RedBolt.Core;
using RedBolt.IO;
using RedBolt.Scene;
namespace RedBolt.Resource;

/// <summary>Base class for serializable resource that uses Archive serialization.</summary>
[Reflected]
public partial class SerializableResource : SimpleResource
{
    private static readonly byte[] SerializableBinaryMagic = [0, (byte)'S', (byte)'R', (byte)'L'];

    /// <summary>Construct.</summary>
    public SerializableResource()
    {
    }

    public static void RegisterObject(Context context)
    {
        context.RegisterAttributedType<SerializableResource>();
    }

    /// <summary>Destruct.</summary>
    public override void Dispose()
    {
        _value = null;
        base.Dispose();
    }

    /// <summary>Override of SerializeInBlock.</summary>
    public override void SerializeInBlock(Archive archive)
    {
        if (archive.IsInput)
        {
            var typeName = string.Empty;
            if (!archive.IsUnorderedAccessSupportedInCurrentBlock || archive.HasElementOrBlock("type"))
                ArchiveSerialization.SerializeValue(archive, "type", ref typeName);
            if (!string.IsNullOrEmpty(typeName))
            {
                using var valueBlock = archive.OpenUnorderedBlock("value");
                _value = Context.CreateObject(new StringHash(typeName)) as Serializable;
                if (_value is not null)
                {
                    _value.SerializeInBlock(archive);
                }
                else
                {
                    throw new InvalidOperationException($"Failed to create object of type '{typeName}'.");
                }
            }
        }
        else
        {
            if (_value is not null)
            {
                var typeName = _value.GetTypeName();
                ArchiveSerialization.SerializeValue(archive, "type", ref typeName);
                using var valueBlock = archive.OpenUnorderedBlock("value");
                _value.SerializeInBlock(archive);
            }
        }
    }

    /// <summary>Get resource value.</summary>
    public Serializable? GetValue()
    {
        return _value;
    }

    /// <summary>Set value of the resource.</summary>
    public void SetValue(Serializable? serializable)
    {
        _value = serializable;
    }

    /// <summary>Binary archive magic word. Should be 4 bytes.</summary>
    protected override byte[] GetBinaryMagic() => SerializableBinaryMagic;

    /// <summary>Root block name. Used for XML serialization only.</summary>
    protected override string GetRootBlockName() => "serializable";

    private Serializable? _value;
}
