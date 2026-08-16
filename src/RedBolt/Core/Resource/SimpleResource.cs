// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Resource;

using Microsoft.Extensions.Logging;

using RedBolt.Core;
using RedBolt.IO;
using FileMode = global::RedBolt.IO.FileMode;
using RedBolt.Logging;

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
            var format = ResourceFormat.PeekResourceFormat(source, GetBinaryMagic());
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

                    var xmlRoot = xmlFile.GetRoot();
                    if (xmlRoot.Name == GetRootBlockName())
                    {
                        if (!xmlFile.LoadObjectCallback(archive =>
                            {
                                using var block = archive.OpenUnorderedBlock(GetRootBlockName());
                                SerializeInBlock(archive);
                            }))
                            return false;
                    }
                    else if (!LoadLegacyXML(xmlRoot))
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
                    using var xmlFile = new XMLFile();
                    using var archive = new XMLOutputArchive(
                        Context.Instance, xmlFile.GetOrCreateRoot(GetRootBlockName()), xmlFile);
                    using (archive.OpenUnorderedBlock(GetRootBlockName()))
                        SerializeInBlock(archive);
                    return xmlFile.Save(destination);
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

    private InternalResourceFormat? _loadFormat;
    private InternalResourceFormat? _saveFormat;
}
