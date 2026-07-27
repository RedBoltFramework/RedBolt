using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using RedBolt.Container;
using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.Input;
namespace RedBolt.Resource;

/// <summary>Base class for resources that support arbitrary metadata stored. Metadata serialization shall be implemented in derived classes.</summary>
public class ResourceWithMetadata : Resource
{
    /// <summary>Construct.</summary>
    public ResourceWithMetadata()
    {
    }

    /// <summary>Destruct.</summary>
    public override void Dispose()
    {
        _metadataKeys.Clear();
        foreach (var value in _metadata.Values)
        {
            var ownedValue = value;
            ownedValue.Dispose();
        }
        _metadata.Clear();
        base.Dispose();
    }

    /// <summary>Add new metadata variable or overwrite old value.</summary>
    public void AddMetadata(string name, Variant value)
    {
        var nameHash = new StringHash(name);
        var replacement = value.Clone();
        if (_metadata.TryGetValue(nameHash, out var previousValue))
            previousValue.Dispose();
        else
            _metadataKeys.Add(name);
        _metadata[nameHash] = replacement;
    }

    /// <summary>Remove metadata variable.</summary>
    public void RemoveMetadata(string name)
    {
        if (_metadata.Remove(new StringHash(name), out var value))
            value.Dispose();
        _metadataKeys.Remove(name);
    }

    /// <summary>Remove all metadata variables.</summary>
    public void RemoveAllMetadata()
    {
        foreach (var value in _metadata.Values)
        {
            var ownedValue = value;
            ownedValue.Dispose();
        }
        _metadata.Clear();
        _metadataKeys.Clear();
    }

    /// <summary>Return all metadata keys.</summary>
    public IReadOnlyList<string> GetMetadataKeys() => _metadataKeys;

    /// <summary>Return metadata variable.</summary>
    public ref readonly Variant GetMetadata(string name)
    {
        ref var value = ref CollectionsMarshal.GetValueRefOrNullRef(_metadata, new StringHash(name));
        if (!Unsafe.IsNullRef(ref value))
            return ref value;
        return ref Variant.Empty;
    }

    /// <summary>Return whether the resource has metadata.</summary>
    public bool HasMetadata()
    {
        return _metadata.Count != 0;
    }

    /// <summary>Copy metadata from another resource.</summary>
    public void CopyMetadata(ResourceWithMetadata source)
    {
        if (ReferenceEquals(source, this))
            return;

        RemoveAllMetadata();
        foreach (var pair in source._metadata)
            _metadata.Add(pair.Key, pair.Value.Clone());
        _metadataKeys.AddRange(source._metadataKeys);
    }

    /// <summary>Load metadata from &lt;metadata&gt; children of XML element.</summary>
    protected void LoadMetadataFromXML(XMLElement source)
    {
        RemoveAllMetadata();
        for (var element = source.GetChild("metadata"); element.NotNull; element = element.GetNext("metadata"))
        {
            using var value = element.GetVariant();
            AddMetadata(element.GetAttribute("name"), value);
        }
    }

    /// <summary>Load metadata from JSON array.</summary>
    protected void LoadMetadataFromJSON(IReadOnlyList<JSONValue> array)
    {
        RemoveAllMetadata();
        for (var i = 0; i < array.Count; ++i)
        {
            var value = array[i];
            using var variant = value.GetVariant();
            AddMetadata(value.Get("name").GetString(), variant);
        }
    }

    /// <summary>Save as &lt;metadata&gt; children of XML element.</summary>
    protected void SaveMetadataToXML(XMLElement destination)
    {
        for (var i = 0; i < _metadataKeys.Count; ++i)
        {
            var element = destination.CreateChild("metadata");
            element.SetString("name", _metadataKeys[i]);
            element.SetVariant(GetMetadata(_metadataKeys[i]));
        }
    }

    /// <summary>Animation metadata variables.</summary>
    private readonly Dictionary<StringHash, Variant> _metadata = [];
    /// <summary>Animation metadata keys.</summary>
    private readonly List<string> _metadataKeys = [];
}
