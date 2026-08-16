// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Scene;

using Microsoft.Extensions.Logging;
using System.Diagnostics;

using RedBolt.Core;
using RedBolt.IO;
using FileMode = global::RedBolt.IO.FileMode;
using RedBolt.Logging;
using RedBolt.Resource;
using ResourceType = global::RedBolt.Resource.Resource;

/// <summary>Scene resource.</summary>
/// <remarks>
/// Can be used to load and save <see cref="Scene"/>, since <see cref="Scene"/> is not a resource.
/// Be careful when using a cached <see cref="SceneResource"/>, since the underlying scene may be active.
/// </remarks>
[Reflected]
public partial class SceneResource : ResourceType
{
    public delegate void ReloadBeginHandler(SceneResource resource, ref bool cancelReload);
    public delegate void ReloadEndHandler(SceneResource resource, bool success);

    public event ReloadBeginHandler? OnReloadBegin;
    public event ReloadEndHandler? OnReloadEnd;

    public SceneResource()
    {
        Scene = new Scene();
    }

    public override void Dispose()
    {
        _loadXmlFile?.Dispose();
        _loadXmlFile = null;
        _loadJsonFile?.Dispose();
        _loadJsonFile = null;
        _loadBinaryFile?.Dispose();
        _loadBinaryFile = null;
        Scene.Dispose();
        base.Dispose();
    }


    public void SetPrefab(bool value) => _isPrefab = value;

    public void SetSaveFormatHint(InternalResourceFormat format)
    {
        _saveFormat = format != InternalResourceFormat.Unknown ? format : null;
    }

    public bool Save(Serializer destination, InternalResourceFormat format, bool asPrefab = false)
    {
        if (asPrefab)
        {
            // Save as prefab is rare, we can afford to be suboptimal here.
            using var prefab = new PrefabResource();
            Scene.GeneratePrefab(prefab.GetMutableScenePrefab());
            prefab.NormalizeIds();
            return prefab.Save(destination, format);
        }

        try
        {
            switch (format)
            {
                case InternalResourceFormat.Json:
                {
                    using var jsonFile = new JSONFile();
                    using var archive = new JSONOutputArchive(Context.Instance, jsonFile.Root, jsonFile);
                    using (archive.OpenUnorderedBlock(GetXmlRootName()))
                        Scene.SerializeInBlock(archive, false, PrefabSaveFlag.EnumsAsStrings, PrefabLoadFlag.None);
                    return jsonFile.Save(destination);
                }
                case InternalResourceFormat.Xml:
                {
                    using var xmlFile = new XMLFile();
                    using var xmlRoot = xmlFile.GetOrCreateRoot(GetXmlRootName());
                    using var archive = new XMLOutputArchive(Context.Instance, xmlRoot, xmlFile);
                    using (archive.OpenUnorderedBlock(GetXmlRootName()))
                        Scene.SerializeInBlock(archive, false, PrefabSaveFlag.EnumsAsStrings, PrefabLoadFlag.None);
                    return xmlFile.Save(destination);
                }
                case InternalResourceFormat.Binary:
                {
                    destination.Write(SimpleResource.DefaultBinaryMagic);

                    using var archive = new BinaryOutputArchive(Context.Instance, destination);
                    using (archive.OpenUnorderedBlock(GetXmlRootName()))
                        Scene.SerializeInBlock(archive, false, PrefabSaveFlag.CompactAttributeNames, PrefabLoadFlag.None);
                    return true;
                }
                default:
                {
                    Debug.Assert(false);
                    return false;
                }
            }
        }
        catch (ArchiveException exception)
        {
            Log.GetLogger().LogError(exception, "Cannot save SimpleResource");
            return false;
        }
    }

    public bool SaveFile(FileIdentifier fileName, InternalResourceFormat format, bool asPrefab = false)
    {
        var virtualFileSystem = GetSubsystem<VirtualFileSystem>()!;
        using var file = virtualFileSystem.OpenFile(fileName, FileMode.Write);
        return file is not null && Save(file, format, asPrefab);
    }

    /// <summary>Implement <see cref="Resource"/>.</summary>
    public override bool BeginLoad(Deserializer source)
    {
        _loadBinaryFile?.Dispose();
        _loadBinaryFile = null;
        _loadJsonFile?.Dispose();
        _loadJsonFile = null;
        _loadXmlFile?.Dispose();
        _loadXmlFile = null;

        var format = ResourceFormat.PeekResourceFormat(source, SimpleResource.DefaultBinaryMagic);
        switch (format)
        {
            case InternalResourceFormat.Json:
            {
                _loadJsonFile = new JSONFile();
                _loadJsonFile.SetName(source.Name);
                if (!_loadJsonFile.Load(source))
                    return false;

                _loadFormat = format;
                return true;
            }
            case InternalResourceFormat.Xml:
            {
                _loadXmlFile = new XMLFile();
                _loadXmlFile.SetName(source.Name);
                if (!_loadXmlFile.Load(source))
                    return false;

                _loadFormat = format;
                return true;
            }
            case InternalResourceFormat.Binary:
            {
                _loadBinaryFile = new BinaryFile();
                _loadBinaryFile.SetName(source.Name);
                _loadBinaryFile.Load(source);

                _loadFormat = format;
                return true;
            }
            default:
            {
                Log.GetLogger().LogError("Unknown resource format");
                _loadFormat = InternalResourceFormat.Unknown;
                return false;
            }
        }
    }

    public override bool EndLoad()
    {
        if (_loadFormat is null || _loadFormat == InternalResourceFormat.Unknown)
            return false;

        try
        {
            var cancelReload = false;
            OnReloadBegin?.Invoke(this, ref cancelReload);

            if (!cancelReload)
            {
                switch (_loadFormat.Value)
                {
                    case InternalResourceFormat.Json:
                    {
                        using var archive = new JSONInputArchive(Context.Instance, _loadJsonFile!.Root, _loadJsonFile);
                        using var block = archive.OpenUnorderedBlock(GetXmlRootName());
                        Scene.SerializeInBlock(archive, false, PrefabSaveFlag.None, PrefabLoadFlag.None);
                        break;
                    }
                    case InternalResourceFormat.Xml:
                    {
                        using var xmlRoot = _loadXmlFile!.GetRoot();
                        if (xmlRoot.Name == GetXmlRootName())
                        {
                            using var archive = new XMLInputArchive(Context.Instance, xmlRoot, _loadXmlFile);
                            using var block = archive.OpenUnorderedBlock(GetXmlRootName());
                            Scene.SerializeInBlock(archive, false, PrefabSaveFlag.None, PrefabLoadFlag.None);
                        }
                        else
                        {
                            if (!Scene.LoadXML(xmlRoot))
                                throw new ArchiveException("Cannot load Scene from legacy XML format");
                        }
                        break;
                    }
                    case InternalResourceFormat.Binary:
                    {
                        using var readBuffer = new MemoryBuffer(_loadBinaryFile!.GetMutableBuffer());
                        readBuffer.SeekRelative(SimpleResource.DefaultBinaryMagic.Length);

                        using var archive = new BinaryInputArchive(Context.Instance, readBuffer);
                        using var block = archive.OpenUnorderedBlock(GetXmlRootName());
                        Scene.SerializeInBlock(archive, false, PrefabSaveFlag.None, PrefabLoadFlag.None);
                        break;
                    }
                    default:
                    {
                        throw new ArchiveException("Cannot load Scene from the file of unknown format");
                    }
                }
            }

            _loadJsonFile?.Dispose();
            _loadJsonFile = null;
            _loadBinaryFile?.Dispose();
            _loadBinaryFile = null;
            _loadXmlFile?.Dispose();
            _loadXmlFile = null;

            OnReloadEnd?.Invoke(this, !cancelReload);

            return true;
        }
        catch (ArchiveException exception)
        {
            Log.GetLogger().LogError(exception, "Cannot load SimpleResource");
            return false;
        }
    }

    public override bool Save(Serializer destination)
    {
        var format = _saveFormat ?? _loadFormat ?? InternalResourceFormat.Xml;
        return Save(destination, format, _isPrefab);
    }

    public override bool SaveFile(FileIdentifier fileName)
    {
        var format = _saveFormat ?? _loadFormat ?? InternalResourceFormat.Xml;
        return SaveFile(fileName, format, _isPrefab);
    }

    /// <summary>Return scene. It may be modified.</summary>
    public Scene Scene { get; }

    /// <summary>Get name of XML root element.</summary>
    public static string GetXmlRootName() => "resource";

    private InternalResourceFormat? _loadFormat;
    private InternalResourceFormat? _saveFormat;
    private bool _isPrefab;

    private BinaryFile? _loadBinaryFile;
    private JSONFile? _loadJsonFile;
    private XMLFile? _loadXmlFile;
}
