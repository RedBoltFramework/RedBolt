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
                    var jsonFile = new JSONFile(Name);
                    if (!jsonFile.BeginLoad(new MemoryStream(data, writable: false)))
                        return false;
                    return jsonFile.LoadObjectCallback(archive =>
                    {
                        using var block = archive.OpenUnorderedBlock(GetRootBlockName());
                        SerializeInBlock(archive);
                    });
                }

                case InternalResourceFormat.Xml:
                {
                    var xmlText = Encoding.UTF8.GetString(data);
                    var xmlFile = new XMLFile(Name);
                    if (!xmlFile.FromString(xmlText))
                        return false;

                    if (xmlFile.Root.Name == GetRootBlockName())
                    {
                        if (!xmlFile.LoadObjectCallback(archive =>
                            {
                                using var block = archive.OpenUnorderedBlock(GetRootBlockName());
                                SerializeInBlock(archive);
                            }))
                            return false;
                    }
                    else if (!LoadLegacyXML(xmlFile.Root))
                        return false;

                    return true;
                }

                case InternalResourceFormat.Binary:
                {
                    var binaryFile = new BinaryFile(Name);
                    using var payload = new MemoryStream(data, GetBinaryMagic().Length,
                        data.Length - GetBinaryMagic().Length, writable: false);
                    if (!binaryFile.BeginLoad(payload))
                        return false;
                    return binaryFile.LoadObjectCallback(archive =>
                    {
                        using var block = archive.OpenUnorderedBlock(GetRootBlockName());
                        SerializeInBlock(archive);
                    });
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
