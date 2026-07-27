using RedBolt.Core;
using RedBolt.IO;
namespace RedBolt.RenderAPI;

public struct ShaderBytecode : ICloneable
{
    public const uint Version = 1;

    public ShaderType Type;
    public string Mime;
    public byte[] Bytecode;
    public List<VertexShaderAttribute> VertexAttributes;

    public ShaderBytecode()
    {
        Mime = string.Empty;
        Bytecode = [];
        VertexAttributes = [];
    }

    public ShaderBytecode(ShaderType type)
        : this()
    {
        Type = type;
    }

    private ShaderBytecode(in ShaderBytecode other)
    {
        Type = other.Type;
        Mime = other.Mime ?? string.Empty;
        Bytecode = [.. other.Bytecode ?? []];
        VertexAttributes = [.. other.VertexAttributes ?? []];
    }

    public void SerializeInBlock(Archive archive)
    {
        ArgumentNullException.ThrowIfNull(archive);
        Mime ??= string.Empty;
        Bytecode ??= [];
        VertexAttributes ??= [];

        var version = archive.SerializeVersion(Version);
        if (version != Version)
            throw new InvalidOperationException("Compiled shader version is outdated, cached shader is ignored");

        ArchiveSerialization.SerializeEnum(archive, "type", ref Type, Enum.GetNames<ShaderType>());
        ArchiveSerialization.SerializeValue(archive, "mime", ref Mime);

        var bytecode = new Variant(Bytecode);
        try
        {
            ArchiveSerialization.SerializeVariantAsType(archive, "bytecode", ref bytecode, VariantType.Buffer);
            if (archive.IsInput)
                Bytecode = bytecode.GetBuffer();
        }
        finally
        {
            bytecode.Dispose();
        }

        var count = archive.IsInput ? 0 : VertexAttributes.Count;
        using var attributesBlock = archive.OpenArrayBlock("vertexAttributes", count);
        if (archive.IsInput)
            VertexAttributes.Clear();

        for (var i = 0; i < attributesBlock.SizeHint; ++i)
        {
            var attribute = archive.IsInput ? default : VertexAttributes[i];
            using var itemBlock = archive.OpenUnorderedBlock("vertexAttribute");
            SerializeAttribute(archive, ref attribute);
            if (archive.IsInput)
                VertexAttributes.Add(attribute);
        }
    }

    public bool SaveToFile(Serializer destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        var value = this;
        return ArchiveSerialization.ConsumeArchiveException(() =>
        {
            using var archive = new BinaryOutputArchive(Context.Instance, destination);
            using (archive.OpenUnorderedBlock("compiledShaderVariation"))
                value.SerializeInBlock(archive);
            archive.Flush();
        }, false);
    }

    public bool LoadFromFile(Deserializer source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var value = this;
        var loaded = ArchiveSerialization.ConsumeArchiveException(() =>
        {
            using var archive = new BinaryInputArchive(Context.Instance, source);
            using (archive.OpenUnorderedBlock("compiledShaderVariation"))
                value.SerializeInBlock(archive);
        }, false);

        this = loaded ? value : new ShaderBytecode();
        return loaded;
    }

    public bool IsEmpty() => Bytecode is null || Bytecode.Length == 0;

    public object Clone() => new ShaderBytecode(this);

    private static void SerializeAttribute(Archive archive, ref VertexShaderAttribute value)
    {
        var semantic = value.Semantic;
        var semanticIndex = archive.IsInput ? 0u : checked((uint)value.SemanticIndex);
        var inputIndex = archive.IsInput ? 0u : checked((uint)value.InputIndex);
        ArchiveSerialization.SerializeEnum(archive, "semantic", ref semantic, Enum.GetNames<VertexElementSemantic>());
        ArchiveSerialization.SerializeValue(archive, "semanticIndex", ref semanticIndex);
        ArchiveSerialization.SerializeValue(archive, "inputIndex", ref inputIndex);
        if (archive.IsInput)
            value = new VertexShaderAttribute(semantic, checked((int)semanticIndex), checked((int)inputIndex));
    }
}
