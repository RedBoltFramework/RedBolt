using System.Text;
using Microsoft.Extensions.Logging;

namespace RedBolt;

public enum InternalResourceFormat
{
    Unknown,
    Json,
    Xml,
    Binary
}

/// <summary>Base class for simple resource that uses Archive serialization.</summary>
public abstract class SimpleResource : Resource
{
    public static readonly byte[] DefaultBinaryMagic = [0, (byte)'B', (byte)'I', (byte)'N'];

    /// <summary>Construct.</summary>
    public SimpleResource()
    {
    }

    public abstract override void SerializeInBlock(Archive archive);

    public void SetSaveFormatHint(InternalResourceFormat format)
    {
        _saveFormat = format == InternalResourceFormat.Unknown ? null : format;
    }

    public override bool BeginLoad(Deserializer source)
    {
        ArgumentNullException.ThrowIfNull(source);

        try
        {
            var format = PeekResourceFormat(source, GetBinaryMagic());
            switch (format)
            {
                case InternalResourceFormat.Json:
                {
                    using var jsonFile = new JSONFile();
                    if (!jsonFile.Load(source))
                        return false;
                    var success = jsonFile.LoadObjectCallback(archive =>
                    {
                        using var block = archive.OpenUnorderedBlock(GetRootBlockName());
                        SerializeInBlock(archive);
                    });
                    if (success)
                        _loadFormat = format;
                    return success;
                }

                case InternalResourceFormat.Xml:
                {
                    using var xmlFile = new XMLFile();
                    if (!xmlFile.Load(source))
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

                    _loadFormat = format;
                    return true;
                }

                case InternalResourceFormat.Binary:
                {
                    source.SeekRelative(GetBinaryMagic().Length);
                    using var archive = new BinaryInputArchive(Context.Instance, source);
                    using (var block = archive.OpenUnorderedBlock(GetRootBlockName()))
                    {
                        SerializeInBlock(archive);
                    }
                    _loadFormat = format;
                    return true;
                }

                default:
                    Log.GetLogger().LogError("Unknown resource format");
                    return false;
            }
        }
        catch (InvalidOperationException exception)
        {
            Log.GetLogger().LogError(exception, "Cannot load SimpleResource");
            return false;
        }
    }

    public override bool EndLoad() => true;

    public override bool Save(Serializer destination)
    {
        return Save(destination, _loadFormat ?? GetDefaultInternalFormat());
    }

    public bool Save(Serializer destination, InternalResourceFormat format)
    {
        ArgumentNullException.ThrowIfNull(destination);

        try
        {
            switch (format)
            {
                case InternalResourceFormat.Json:
                {
                    using var jsonFile = new JSONFile();
                    using var archive = new JSONOutputArchive(Context.Instance, jsonFile.Root, jsonFile);
                    using (archive.OpenUnorderedBlock(GetRootBlockName()))
                        SerializeInBlock(archive);
                    return jsonFile.Save(destination);
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
        catch (InvalidOperationException exception)
        {
            Log.GetLogger().LogError(exception, "Cannot save SimpleResource");
            return false;
        }
    }

    public bool SaveFile(FileIdentifier fileName, InternalResourceFormat format)
    {
        var virtualFileSystem = GetSubsystem<VirtualFileSystem>()!;
        using var file = virtualFileSystem.OpenFile(fileName, FileMode.Write);
        return file is not null && Save(file, format);
    }

    public override bool SaveFile(FileIdentifier fileName)
    {
        return SaveFile(fileName, _loadFormat ?? GetDefaultInternalFormat());
    }

    protected virtual byte[] GetBinaryMagic() => DefaultBinaryMagic;

    protected virtual string GetRootBlockName() => "resource";

    protected virtual InternalResourceFormat GetDefaultInternalFormat() => InternalResourceFormat.Json;

    protected virtual bool LoadLegacyXML(XMLElement source) => false;

    private static InternalResourceFormat PeekResourceFormat(Deserializer source, ReadOnlySpan<byte> binaryMagic)
    {
        var basePosition = source.Position;
        try
        {
            Span<byte> magic = stackalloc byte[DefaultBinaryMagic.Length];
            var count = checked((int)source.Read(magic));
            var data = magic[..count];

            if (count == DefaultBinaryMagic.Length && data.SequenceEqual(binaryMagic))
                return InternalResourceFormat.Binary;

            var marker = data.IndexOf((byte)'<');
            if (count == DefaultBinaryMagic.Length && marker >= 0 && IsBlank(data[..marker]))
                return InternalResourceFormat.Xml;

            marker = data.IndexOf((byte)'{');
            if (count >= 2 && marker >= 0 && IsBlank(data[..marker]))
                return InternalResourceFormat.Json;

            if (!IsBlank(data))
                return InternalResourceFormat.Unknown;

            Log.GetLogger().LogWarning(
                "File starts with whitespace, peeking more data to determine format. It may cause performance penalty.");
            while (!source.IsEof())
            {
                var value = source.ReadUByte();
                if (value == '<')
                    return InternalResourceFormat.Xml;
                if (value == '{')
                    return InternalResourceFormat.Json;
                if (!char.IsWhiteSpace((char)value))
                    return InternalResourceFormat.Unknown;
            }
            return InternalResourceFormat.Unknown;
        }
        finally
        {
            source.Seek(basePosition);
        }

        static bool IsBlank(ReadOnlySpan<byte> data)
        {
            foreach (var value in data)
            {
                if (!char.IsWhiteSpace((char)value))
                    return false;
            }
            return true;
        }
    }

    private InternalResourceFormat? _loadFormat;
    private InternalResourceFormat? _saveFormat;
}
