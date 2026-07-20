using System.Text;

namespace RedBolt;

public enum SceneResourceFormat { Json, Xml, Binary }

public sealed class SceneResource : Resource
{
    private byte[]? _pendingData;
    private InternalResourceFormat? _loadFormat;
    private InternalResourceFormat? _saveFormat;

    public delegate void ReloadBeginHandler(SceneResource resource, ref bool cancelReload);
    public delegate void ReloadEndHandler(SceneResource resource, bool success);

    public event ReloadBeginHandler? OnReloadBegin;
    public event ReloadEndHandler? OnReloadEnd;

    public SceneResource(string name) : base(name) => Scene = new Scene();

    public Scene Scene { get; }
    public bool IsPrefab { get; private set; }

    public void SetPrefab(bool value) => IsPrefab = value;
    public Scene GetScene() => Scene;
    public void SetSaveFormatHint(InternalResourceFormat format) => _saveFormat = format == InternalResourceFormat.Unknown ? null : format;

    public override bool BeginLoad(Stream source)
    {
        ArgumentNullException.ThrowIfNull(source);
        using var memory = new MemoryStream(); source.CopyTo(memory); _pendingData = memory.ToArray();
        _loadFormat = PeekFormat(_pendingData);
        return _loadFormat != InternalResourceFormat.Unknown;
    }

    public override bool EndLoad()
    {
        if (_pendingData is null || _loadFormat is null) return false;
        try
        {
            var cancelReload = false;
            OnReloadBegin?.Invoke(this, ref cancelReload);
            if (cancelReload)
            {
                OnReloadEnd?.Invoke(this, success: false);
                return true;
            }

            using var binaryBuffer = _loadFormat == InternalResourceFormat.Binary
                ? new MemoryBuffer(_pendingData.AsMemory(4))
                : null;
            using Archive archive = _loadFormat switch
            {
                InternalResourceFormat.Json => new JSONInputArchive(Context.Instance, Encoding.UTF8.GetString(_pendingData)),
                InternalResourceFormat.Xml => new XMLInputArchive(Context.Instance, Encoding.UTF8.GetString(_pendingData)),
                InternalResourceFormat.Binary => new BinaryInputArchive(Context.Instance, binaryBuffer!),
                _ => throw new InvalidOperationException("Unknown scene format.")
            };
            using (archive.OpenUnorderedBlock(GetXmlRootName()))
                SerializeScene(archive, Scene, asPrefab: false);
            archive.Flush();
            OnReloadEnd?.Invoke(this, success: true);
            return true;
        }
        catch { return false; }
        finally { _pendingData = null; }
    }

    public override bool Save(Stream destination) => Save(destination, _saveFormat ?? _loadFormat ?? InternalResourceFormat.Xml, IsPrefab);

    public bool Save(Stream destination, SceneResourceFormat format, bool asPrefab = false)
        => Save(destination, format switch { SceneResourceFormat.Json => InternalResourceFormat.Json, SceneResourceFormat.Xml => InternalResourceFormat.Xml, _ => InternalResourceFormat.Binary }, asPrefab);

    public bool Save(Stream destination, InternalResourceFormat format, bool asPrefab = false)
    {
        ArgumentNullException.ThrowIfNull(destination);
        try
        {
            switch (format)
            {
                case InternalResourceFormat.Json:
                {
                    using var archive = new JSONOutputArchive(Context.Instance);
                    using (archive.OpenUnorderedBlock(GetXmlRootName()))
                        SerializeScene(archive, Scene, asPrefab);
                    var bytes = Encoding.UTF8.GetBytes(archive.ToJsonString()); destination.Write(bytes); break;
                }
                case InternalResourceFormat.Xml:
                {
                    using var archive = new XMLOutputArchive(Context.Instance);
                    using (archive.OpenUnorderedBlock(GetXmlRootName()))
                        SerializeScene(archive, Scene, asPrefab);
                    var bytes = Encoding.UTF8.GetBytes(archive.ToXmlString()); destination.Write(bytes); break;
                }
                case InternalResourceFormat.Binary:
                {
                    destination.Write(SimpleResource.DefaultBinaryMagic);
                    using var buffer = new VectorBuffer();
                    using var archive = new BinaryOutputArchive(Context.Instance, buffer);
                    using (archive.OpenUnorderedBlock(GetXmlRootName()))
                        SerializeScene(archive, Scene, asPrefab);
                    archive.Flush(); destination.Write(buffer.Buffer[..checked((int)buffer.Size)]); break;
                }
                default: return false;
            }
            _saveFormat = format;
            return true;
        }
        catch { return false; }
    }

    public bool SaveFile(string fileName, SceneResourceFormat format, bool asPrefab = false)
    {
        return SaveFile(fileName, format switch
        {
            SceneResourceFormat.Json => InternalResourceFormat.Json,
            SceneResourceFormat.Xml => InternalResourceFormat.Xml,
            _ => InternalResourceFormat.Binary
        }, asPrefab);
    }

    public bool SaveFile(string fileName, InternalResourceFormat format, bool asPrefab = false)
    {
        var virtualFileSystem = GetSubsystem<VirtualFileSystem>()!;
        var file = virtualFileSystem.OpenFile(new FileIdentifier(fileName), FileMode.Write);
        if (file is null)
            return false;

        try
        {
            using var buffer = new MemoryStream();
            if (!Save(buffer, format, asPrefab))
                return false;

            var data = buffer.ToArray();
            return file.Write(data) == data.Length;
        }
        finally
        {
            file.Close();
        }
    }

    public static string GetXmlRootName() => "resource";

    private static void SerializeScene(Archive archive, Scene scene, bool asPrefab = false)
    {
        if (asPrefab)
        {
            using var prefab = archive.IsInput ? new NodePrefab() : scene.Root.GeneratePrefab();
            prefab.SerializeInBlock(archive, PrefabArchiveFlag.SerializeTemporary);
            if (archive.IsInput)
            {
                var reader = new PrefabReaderFromMemory(prefab);
                scene.Root.Load(reader);
            }
            return;
        }

        var saveFlags = archive.IsHumanReadable
            ? PrefabSaveFlag.EnumsAsStrings
            : PrefabSaveFlag.CompactAttributeNames;
        scene.SerializeInBlock(archive, false, saveFlags, PrefabLoadFlag.None);
    }

    private static InternalResourceFormat PeekFormat(ReadOnlySpan<byte> data)
    {
        if (data.StartsWith(SimpleResource.DefaultBinaryMagic)) return InternalResourceFormat.Binary;
        foreach (var value in data)
        {
            if (char.IsWhiteSpace((char)value)) continue;
            return value switch { (byte)'{' => InternalResourceFormat.Json, (byte)'<' => InternalResourceFormat.Xml, _ => InternalResourceFormat.Unknown };
        }
        return InternalResourceFormat.Unknown;
    }
}
