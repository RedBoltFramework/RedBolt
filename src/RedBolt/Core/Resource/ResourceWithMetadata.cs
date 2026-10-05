// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Resource;

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using RedBolt.Container;
using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.Input;

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
        _metadata.Dispose();
        base.Dispose();
    }

    /// <summary>Set metadata, overwrite all old values.</summary>
    public void SetMetadata(StringVariantMap metadata)
    {
        if (ReferenceEquals(metadata, _metadata))
            return;

        _metadata.Clear();
        foreach (var (key, value) in metadata)
            _metadata.Add(key, value);
    }

    /// <summary>Return all metadata.</summary>
    public StringVariantMap GetMetadata() => _metadata;

    /// <summary>Add new metadata variable or overwrite old value.</summary>
    public void AddMetadata(string name, Variant value)
    {
        _metadata[name] = value;
    }

    /// <summary>Remove metadata variable.</summary>
    public void RemoveMetadata(string name)
    {
        _metadata.Remove(name);
    }

    /// <summary>Remove all metadata variables.</summary>
    public void RemoveAllMetadata()
    {
        _metadata.Clear();
    }

    /// <summary>Return metadata variable.</summary>
    public ref readonly Variant GetMetadata(string name)
    {
        ref var value = ref CollectionsMarshal.GetValueRefOrNullRef(_metadata, name);
        if (!Unsafe.IsNullRef(ref value))
            return ref value;
        return ref Variant.Empty;
    }

    /// <summary>Return whether the resource has metadata.</summary>
    public bool HasMetadata()
    {
        return _metadata.Count != 0;
    }

    /// <summary>Read metadata from &lt;metadata&gt; children of XML element.</summary>
    protected static StringVariantMap CreateMetadataFromXML(XMLElement source)
    {
        var result = new StringVariantMap();
        try
        {
            for (var element = source.GetChild("metadata"); element.NotNull; element = element.GetNext("metadata"))
            {
                using var value = element.GetVariant();
                result.TryAdd(element.GetAttribute("name"), value);
            }
        }
        catch
        {
            result.Dispose();
            throw;
        }
        return result;
    }

    /// <summary>Save as &lt;metadata&gt; children of XML element.</summary>
    protected void SaveMetadataToXML(XMLElement destination)
    {
        var keys = new string[_metadata.Count];
        _metadata.Keys.CopyTo(keys, 0);
        Array.Sort(keys, StringComparer.Ordinal);
        for (var i = 0; i < keys.Length; ++i)
        {
            var element = destination.CreateChild("metadata");
            element.SetString("name", keys[i]);
            element.SetVariant(GetMetadata(keys[i]));
        }
    }

    /// <summary>Metadata variables.</summary>
    private readonly StringVariantMap _metadata = new();
}
