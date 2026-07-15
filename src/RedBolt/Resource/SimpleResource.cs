using System.Text;

namespace RedBolt;

public enum InternalResourceFormat
{
    Unknown,
    Json,
    Xml,
    Binary
}

public abstract class SimpleResource : Resource
{
    // C++: DefaultBinaryMagic is a global 'const BinaryMagic' in Resource.cpp, not a member of SimpleResource.
    public static readonly byte[] DefaultBinaryMagic = [0, (byte)'B', (byte)'I', (byte)'N'];
    private InternalResourceFormat? _loadFormat;
    private InternalResourceFormat? _saveFormat;

    protected SimpleResource(string name)
        : base(name)
    {
    }

    public abstract override void SerializeInBlock(Archive archive);

    public void SetSaveFormatHint(InternalResourceFormat format)
    {
        _saveFormat = format == InternalResourceFormat.Unknown ? null : format;
    }

    // C++: BeginLoad creates JSONFile/XMLFile/BinaryFile objects as intermediaries, calls Load() on them,
    // then creates archives from the loaded intermediary files and calls SerializeValue(archive, GetRootBlockName(), *this).
    // JSON/Binary deserialization happens in BeginLoad, not deferred to EndLoad.
    public override bool BeginLoad(Stream source)
    {
        ArgumentNullException.ThrowIfNull(source);

        try
        {
            var data = ReadAllBytes(source);
            _loadFormat = PeekResourceFormat(data, GetBinaryMagic());
            switch (_loadFormat)
            {
                case InternalResourceFormat.Json:
                {
                    var archive = new JSONInputArchive(Context.Instance, Encoding.UTF8.GetString(data));
                    using (archive.OpenUnorderedBlock(GetRootBlockName()))
                        SerializeInBlock(archive);
                    archive.Flush();
                    return true;
                }

                case InternalResourceFormat.Xml:
                {
                    var xmlText = Encoding.UTF8.GetString(data);
                    var xmlFile = new XMLFile(Name);
                    if (!xmlFile.FromString(xmlText))
                        return false;

                    if (xmlFile.Root.Name == GetRootBlockName())
                    {
                        var archive = new XMLInputArchive(Context.Instance, xmlText);
                        using (archive.OpenUnorderedBlock(GetRootBlockName()))
                            SerializeInBlock(archive);
                        archive.Flush();
                    }
                    else if (!LoadLegacyXML(xmlFile.Root))
                        return false;

                    return true;
                }

                case InternalResourceFormat.Binary:
                {
                    var payload = data.AsMemory(GetBinaryMagic().Length);
                    var buffer = new MemoryBuffer(payload);
                    var archive = new BinaryInputArchive(Context.Instance, buffer);
                    using (archive.OpenUnorderedBlock(GetRootBlockName()))
                        SerializeInBlock(archive);
                    archive.Flush();
                    return true;
                }

                default:
                    return false;
            }
        }
        catch
        {
            return false;
        }
    }

    public override bool EndLoad() => true;

    public override bool Save(Stream destination)
    {
        return Save(destination, _saveFormat ?? GetDefaultInternalFormat());
    }

    public bool Save(Stream destination, InternalResourceFormat format)
    {
        ArgumentNullException.ThrowIfNull(destination);

        try
        {
            switch (format)
            {
                case InternalResourceFormat.Json:
                {
                    var archive = new JSONOutputArchive(Context.Instance);
                    using (archive.OpenUnorderedBlock(GetRootBlockName()))
                        SerializeInBlock(archive);
                    var bytes = Encoding.UTF8.GetBytes(archive.ToJsonString());
                    destination.Write(bytes);
                    return true;
                }

                case InternalResourceFormat.Xml:
                {
                    var archive = new XMLOutputArchive(Context.Instance);
                    using (archive.OpenUnorderedBlock(GetRootBlockName()))
                        SerializeInBlock(archive);
                    var bytes = Encoding.UTF8.GetBytes(archive.ToXmlString());
                    destination.Write(bytes);
                    return true;
                }

                case InternalResourceFormat.Binary:
                {
                    destination.Write(GetBinaryMagic());
                    var buffer = new VectorBuffer();
                    var archive = new BinaryOutputArchive(Context.Instance, buffer);
                    using (archive.OpenUnorderedBlock(GetRootBlockName()))
                        SerializeInBlock(archive);
                    archive.Flush();
                    destination.Write(buffer.Buffer[..checked((int)buffer.Size)]);
                    return true;
                }

                default:
                    return false;
            }
        }
        catch
        {
            return false;
        }
    }

    // C++: SaveFile opens a VFS file and writes via Save(*file, format) directly — no intermediate MemoryStream.
    public bool SaveFile(string fileName, InternalResourceFormat format)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        using var stream = new MemoryStream();
        if (!Save(stream, format))
            return false;

        var virtualFileSystem = GetSubsystem<VirtualFileSystem>()!;
        var identifier = new FileIdentifier(fileName);
        using var rootMount = identifier.Scheme == "file"
            ? new MountPointGuard(virtualFileSystem.MountRoot())
            : null;
        var file = virtualFileSystem.OpenFile(identifier, File.FileMode.Write);
        if (file is null)
            return false;

        try
        {
            var data = stream.ToArray();
            return file.Write(data) == data.Length;
        }
        finally
        {
            file.Close();
        }
    }

    protected virtual byte[] GetBinaryMagic() => DefaultBinaryMagic;

    protected virtual string GetRootBlockName() => "resource";

    protected virtual InternalResourceFormat GetDefaultInternalFormat() => InternalResourceFormat.Json;

    protected virtual bool LoadLegacyXML(XMLElement source) => false;

    private static byte[] ReadAllBytes(Stream source)
    {
        if (source is MemoryStream memoryStream && memoryStream.TryGetBuffer(out var segment))
            return segment.AsSpan((int)memoryStream.Position, checked((int)(memoryStream.Length - memoryStream.Position))).ToArray();

        using var copy = new MemoryStream();
        source.CopyTo(copy);
        return copy.ToArray();
    }

    // C++: PeekResourceFormat is a free function in Resource.cpp, not a member of SimpleResource.
    // C++: operates on Deserializer, not byte spans.
    private static InternalResourceFormat PeekResourceFormat(ReadOnlySpan<byte> data, ReadOnlySpan<byte> binaryMagic)
    {
        if (data.StartsWith(binaryMagic))
            return InternalResourceFormat.Binary;

        foreach (var value in data)
        {
            if (char.IsWhiteSpace((char)value))
                continue;
            return value switch
            {
                (byte)'{' or (byte)'[' => InternalResourceFormat.Json,
                (byte)'<' => InternalResourceFormat.Xml,
                _ => InternalResourceFormat.Unknown
            };
        }

        return InternalResourceFormat.Unknown;
    }
}
