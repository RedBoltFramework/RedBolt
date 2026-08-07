//
// Copyright (c) 2017-2020 the rbfx project.
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
//

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
