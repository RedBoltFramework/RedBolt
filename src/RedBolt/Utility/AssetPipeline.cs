// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Utility;

using System;
using System.Collections.Generic;

using RedBolt.Core;
using RedBolt.IO;
using RedBolt.Resource;

/// <summary>Resource containing an array of AssetTransformer-s.</summary>
[Reflected]
public partial class AssetPipeline : Resource
{
    public AssetPipeline()
    {
    }

    public static bool CheckExtension(string fileName)
    {
        return fileName.EndsWith(".assetpipeline", StringComparison.OrdinalIgnoreCase)
            || fileName.EndsWith(".AssetPipeline.json", StringComparison.OrdinalIgnoreCase);
    }

    public void AddTransformer(AssetTransformer? transformer)
    {
        _transformers.Add(transformer);
    }

    public void RemoveTransformer(AssetTransformer? transformer)
    {
        var index = _transformers.FindIndex(candidate => ReferenceEquals(candidate, transformer));
        if (index != -1)
            _transformers.RemoveAt(index);
    }

    public void ReorderTransformer(AssetTransformer? transformer, int index)
    {
        if (index >= _transformers.Count)
            return;

        var iterator = _transformers.FindIndex(candidate => ReferenceEquals(candidate, transformer));
        if (iterator != -1)
        {
            _transformers.RemoveAt(iterator);
            _transformers.Insert(index, transformer);
        }
    }

    /// <summary>Implement Resource.</summary>
    public override void SerializeInBlock(Archive archive)
    {
        var serializeTransformers = !archive.IsUnorderedAccessSupportedInCurrentBlock
            || (archive.IsInput ? archive.HasElementOrBlock("Transformers") : _transformers.Count != 0);
        if (serializeTransformers)
        {
            ArchiveSerialization.SerializeVector(
                archive, "Transformers", _transformers, SerializeTransformer, "Transformer");
        }
        else if (archive.IsInput)
            _transformers.Clear();

        var serializeDependencies = !archive.IsUnorderedAccessSupportedInCurrentBlock
            || (archive.IsInput ? archive.HasElementOrBlock("Dependencies") : _dependencies.Count != 0);
        if (serializeDependencies)
        {
            ArchiveSerialization.SerializeVector(
                archive, "Dependencies", _dependencies, SerializeDependency);
        }
        else if (archive.IsInput)
            _dependencies.Clear();

        _transformers.RemoveAll(static transformer => transformer is null);
    }

    /// <summary>Implement Resource.</summary>
    public override bool BeginLoad(Deserializer source)
    {
        using var jsonFile = new JSONFile();
        if (jsonFile.Load(source))
            return jsonFile.LoadObject(this);
        return false;
    }

    /// <summary>Implement Resource.</summary>
    public override bool Save(Serializer destination)
    {
        using var jsonFile = new JSONFile();
        if (jsonFile.SaveObject(this))
            return jsonFile.Save(destination);
        return false;
    }

    public IReadOnlyList<AssetTransformer?> GetTransformers() => _transformers;

    public IReadOnlyList<AssetTransformerDependency> GetDependencies() => _dependencies;

    private readonly List<AssetTransformer?> _transformers = [];
    private readonly List<AssetTransformerDependency> _dependencies = [];

    private static void SerializeTransformer(Archive archive, string name, ref AssetTransformer? value)
    {
        ArchiveSerialization.SerializeSharedPtr(archive, name, ref value, true, true);
    }

    private static void SerializeDependency(Archive archive, string name, ref AssetTransformerDependency value)
    {
        value ??= new AssetTransformerDependency();
        using var block = archive.OpenUnorderedBlock(name);
        value.SerializeInBlock(archive);
    }
}
