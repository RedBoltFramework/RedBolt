using System.Text;

namespace RedBolt;

public sealed class ShaderBytecode : IEquatable<ShaderBytecode>
{
    /// <summary>
    /// Version of the shader bytecode format. Increment when serialization format changes.
    /// </summary>
    public const uint Version = 1;

    public ShaderBytecode()
    {
    }

    public ShaderBytecode(ShaderType type)
    {
        Type = type;
    }

    public ShaderBytecode(ShaderBytecode? other)
    {
        if (other is null)
            return;

        Type = other.Type;
        Mime = other.Mime;
        Bytecode = [.. other.Bytecode];
        VertexAttributes.AddRange(other.VertexAttributes);
    }

    public ShaderType Type { get; set; }

    public string Mime { get; set; } = string.Empty;

    public byte[] Bytecode { get; set; } = [];

    /// <summary>
    /// Vertex input layout, if applicable.
    /// </summary>
    public List<VertexShaderAttribute> VertexAttributes { get; } = [];

    public bool IsEmpty() => Bytecode.Length == 0;

    public void SerializeInBlock(Archive archive)
    {
        ArgumentNullException.ThrowIfNull(archive);
        var type = Type;
        var mime = Mime;
        ArchiveSerialization.SerializeEnum(archive, "type", ref type, Enum.GetNames<ShaderType>());
        ArchiveSerialization.SerializeValue(archive, "mime", ref mime);
        var bytecode = new Variant(Bytecode);
        ArchiveSerialization.SerializeVariantAsType(archive, "bytecode", ref bytecode, VariantType.Buffer);

        var count = archive.IsInput ? 0u : (uint)VertexAttributes.Count;
        using var attributesBlock = archive.OpenArrayBlock("vertexAttributes", count);
        if (archive.IsInput)
        {
            Type = type;
            Mime = mime;
            Bytecode = bytecode.GetBuffer();
            VertexAttributes.Clear();
            for (var i = 0u; i < attributesBlock.SizeHint; ++i)
            {
                using var itemBlock = archive.OpenUnorderedBlock("attribute");
                VertexAttributes.Add(SerializeAttribute(archive, default));
            }
        }
        else
        {
            foreach (var attribute in VertexAttributes)
            {
                using var itemBlock = archive.OpenUnorderedBlock("attribute");
                SerializeAttribute(archive, attribute);
            }
        }
    }

    private static VertexShaderAttribute SerializeAttribute(Archive archive, VertexShaderAttribute value)
    {
        var semantic = value.Semantic;
        var semanticIndex = value.SemanticIndex;
        var inputIndex = value.InputIndex;
        ArchiveSerialization.SerializeEnum(archive, "semantic", ref semantic, Enum.GetNames<VertexElementSemantic>());
        ArchiveSerialization.SerializeValue(archive, "semanticIndex", ref semanticIndex);
        ArchiveSerialization.SerializeValue(archive, "inputIndex", ref inputIndex);
        return new VertexShaderAttribute(semantic, semanticIndex, inputIndex);
    }

    public bool SaveToFile(Stream destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        try
        {
            using var writer = new BinaryWriter(destination, Encoding.UTF8, leaveOpen: true);
            writer.Write(Version);
            writer.Write((int)Type);
            writer.Write(Mime);
            writer.Write(Bytecode.Length);
            writer.Write(Bytecode);
            writer.Write(VertexAttributes.Count);
            foreach (var attribute in VertexAttributes)
            {
                writer.Write((byte)attribute.Semantic);
                writer.Write(attribute.SemanticIndex);
                writer.Write(attribute.InputIndex);
            }

            writer.Flush();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public bool LoadFromFile(Stream source)
    {
        ArgumentNullException.ThrowIfNull(source);
        try
        {
            using var reader = new BinaryReader(source, Encoding.UTF8, leaveOpen: true);
            var version = reader.ReadUInt32();
            if (version != Version)
                throw new InvalidDataException("Compiled shader version is outdated.");

            Type = (ShaderType)reader.ReadInt32();
            Mime = reader.ReadString();
            Bytecode = reader.ReadBytes(reader.ReadInt32());

            VertexAttributes.Clear();
            var attributeCount = reader.ReadInt32();
            for (var i = 0; i < attributeCount; ++i)
            {
                VertexAttributes.Add(new VertexShaderAttribute(
                    (VertexElementSemantic)reader.ReadByte(),
                    reader.ReadUInt32(),
                    reader.ReadUInt32()));
            }

            return true;
        }
        catch
        {
            Type = default;
            Mime = string.Empty;
            Bytecode = [];
            VertexAttributes.Clear();
            return false;
        }
    }

    public bool Equals(ShaderBytecode? other)
    {
        if (other is null)
            return false;

        return Type == other.Type
            && string.Equals(Mime, other.Mime, StringComparison.Ordinal)
            && Bytecode.SequenceEqual(other.Bytecode)
            && VertexAttributes.SequenceEqual(other.VertexAttributes);
    }

    public override bool Equals(object? obj) => Equals(obj as ShaderBytecode);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Type);
        hash.Add(Mime, StringComparer.Ordinal);
        foreach (var value in Bytecode)
            hash.Add(value);
        foreach (var attribute in VertexAttributes)
            hash.Add(attribute);
        return hash.ToHashCode();
    }
}
