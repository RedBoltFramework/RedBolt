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
                    using var jsonFile = new JSONFile(Name);
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
                    using var xmlFile = new XMLFile(Name);
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
                    using var binaryFile = new BinaryFile(Name);
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
                    using var archive = new JSONOutputArchive(Context.Instance);
                    using (archive.OpenUnorderedBlock(GetRootBlockName()))
                        SerializeInBlock(archive);
                    var bytes = Encoding.UTF8.GetBytes(archive.ToJsonString());
                    destination.Write(bytes);
                    return true;
                }

                case InternalResourceFormat.Xml:
                {
                    using var archive = new XMLOutputArchive(Context.Instance);
                    using (archive.OpenUnorderedBlock(GetRootBlockName()))
                        SerializeInBlock(archive);
                    var bytes = Encoding.UTF8.GetBytes(archive.ToXmlString());
                    destination.Write(bytes);
                    return true;
                }

                case InternalResourceFormat.Binary:
                {
                    destination.Write(GetBinaryMagic());
                    using var buffer = new VectorBuffer();
                    using var archive = new BinaryOutputArchive(Context.Instance, buffer);
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

    public bool SaveFile(string fileName, InternalResourceFormat format)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        var virtualFileSystem = GetSubsystem<VirtualFileSystem>()!;
        var identifier = new FileIdentifier(fileName);
        using var rootMount = identifier.Scheme == "file"
            ? new MountPointGuard(virtualFileSystem.MountRoot())
            : null;
        var file = virtualFileSystem.OpenFile(identifier, FileMode.Write);
        if (file is not File outputFile || outputFile.Handle is null)
            return false;

        try
        {
            return Save(outputFile.Handle, format);
        }
        finally
        {
            outputFile.Close();
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

    private static InternalResourceFormat PeekResourceFormat(ReadOnlySpan<byte> data, ReadOnlySpan<byte> binaryMagic)
    {
        if (data.StartsWith(binaryMagic))
            return InternalResourceFormat.Binary;

        static bool IsBlank(byte value) => char.IsWhiteSpace((char)value);
        static bool IsAllBlank(ReadOnlySpan<byte> values)
        {
            foreach (var value in values)
            {
                if (!IsBlank(value))
                    return false;
            }
            return true;
        }

        var peekCount = Math.Min(4, data.Length);
        if (peekCount == 4)
        {
            for (var i = 0; i < peekCount; ++i)
            {
                if (data[i] == (byte)'<' && IsAllBlank(data[..i]))
                    return InternalResourceFormat.Xml;
            }
        }

        if (peekCount >= 2)
        {
            for (var i = 0; i < peekCount; ++i)
            {
                if (data[i] == (byte)'{' && IsAllBlank(data[..i]))
                    return InternalResourceFormat.Json;
            }
        }

        var allBlank = true;
        for (var i = 0; i < peekCount; ++i)
        {
            if (!IsBlank(data[i]))
            {
                allBlank = false;
                break;
            }
        }

        if (!allBlank)
            return InternalResourceFormat.Unknown;

        for (var i = peekCount; i < data.Length; ++i)
        {
            if (data[i] == (byte)'<')
                return InternalResourceFormat.Xml;
            if (data[i] == (byte)'{')
                return InternalResourceFormat.Json;
            if (!IsBlank(data[i]))
                return InternalResourceFormat.Unknown;
        }

        return InternalResourceFormat.Unknown;
    }
}
