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

using RedBolt.Engine;
using RedBolt.IO;

/// <summary>Transformer execution inputs (should be serializable on its own).</summary>
public class AssetTransformerInput : ICloneable
{
    public AssetTransformerInput()
    {
        Flavor = new ApplicationFlavor();
    }

    public AssetTransformerInput(bool isPostTransform, in ApplicationFlavor flavor,
        string resourceName, string inputFileName, uint inputFileTime)
    {
        IsPostTransform = isPostTransform;
        Flavor = flavor.Clone();
        OriginalResourceName = resourceName;
        OriginalInputFileName = inputFileName;
        ResourceName = resourceName;
        InputFileName = inputFileName;
        InputFileTime = inputFileTime;
    }

    public AssetTransformerInput(AssetTransformerInput other, string tempPath,
        string outputFileName, string outputResourceName)
    {
        IsPostTransform = other.IsPostTransform;
        Flavor = other.Flavor.Clone();
        OriginalResourceName = other.OriginalResourceName;
        OriginalInputFileName = other.OriginalInputFileName;
        ResourceName = other.ResourceName;
        InputFileName = other.InputFileName;
        InputFileTime = other.InputFileTime;
        TempPath = other.TempPath;
        OutputFileName = other.OutputFileName;
        OutputResourceName = other.OutputResourceName;

        TempPath = tempPath;
        OutputFileName = outputFileName;
        OutputResourceName = outputResourceName;
    }

    public void SerializeInBlock(Archive archive)
    {
        ArchiveSerialization.SerializeValue(archive, "isPostTransform", ref IsPostTransform);

        ArchiveSerialization.SerializeMap(archive, "flavor", Flavor.Components,
            ArchiveSerialization.SerializeValue, SerializeFlavorComponent);
        ArchiveSerialization.SerializeValue(archive, "originalResourceName", ref OriginalResourceName);
        ArchiveSerialization.SerializeValue(archive, "originalInputFileName", ref OriginalInputFileName);

        ArchiveSerialization.SerializeValue(archive, "resourceName", ref ResourceName);
        ArchiveSerialization.SerializeValue(archive, "inputFileName", ref InputFileName);
        ArchiveSerialization.SerializeValue(archive, "inputFileTime", ref InputFileTime);

        ArchiveSerialization.SerializeValue(archive, "tempPath", ref TempPath);
        ArchiveSerialization.SerializeValue(archive, "outputFileName", ref OutputFileName);
        ArchiveSerialization.SerializeValue(archive, "outputResourceName", ref OutputResourceName);
    }

    public static AssetTransformerInput FromBase64(string base64)
    {
        using var archive = new Base64InputArchive(base64);
        var result = new AssetTransformerInput();
        SerializeValue(archive, "input", result);
        return result;
    }

    public string ToBase64()
    {
        using var archive = new Base64OutputArchive();
        SerializeValue(archive, "input", this);
        return archive.GetBase64();
    }

    public AssetTransformerInput Clone()
    {
        return new AssetTransformerInput(this, TempPath, OutputFileName, OutputResourceName);
    }

    object ICloneable.Clone() => Clone();

    /// <summary>Whether this is post-transform pass.</summary>
    public bool IsPostTransform;
    /// <summary>Flavor of the transformer.</summary>
    public ApplicationFlavor Flavor;
    /// <summary>Original resource name. May be different from resource name for nested transformers.</summary>
    public string OriginalResourceName = string.Empty;
    /// <summary>Original absolute file name. May be different from file name for nested transformers.</summary>
    public string OriginalInputFileName = string.Empty;

    /// <summary>Resource name that can be used to access resource via cache.</summary>
    public string ResourceName = string.Empty;
    /// <summary>Absolute file name of the asset.</summary>
    public string InputFileName = string.Empty;
    /// <summary>Modification time of the input file.</summary>
    public uint InputFileTime;
    /// <summary>Absolute path to the temporary directory used to store output files.</summary>
    public string TempPath = string.Empty;
    /// <summary>Absolute file name to the file counterpart in writeable directory.</summary>
    public string OutputFileName = string.Empty;
    /// <summary>Resource name to the file counterpart in writeable directory.</summary>
    public string OutputResourceName = string.Empty;

    private static void SerializeFlavorComponent(
        Archive archive, string name, ref ApplicationFlavorComponent value)
    {
        value ??= new ApplicationFlavorComponent();
        ArchiveSerialization.SerializeSet(archive, name, value, ArchiveSerialization.SerializeValue);
    }

    private static void SerializeValue(Archive archive, string name, AssetTransformerInput value)
    {
        using var block = archive.OpenUnorderedBlock(name);
        value.SerializeInBlock(archive);
    }
}

/// <summary>Transformer execution result (should be serializable on its own).</summary>
public class AssetTransformerOutput : ICloneable
{
    public void SerializeInBlock(Archive archive)
    {
        ArchiveSerialization.SerializeValue(archive, "sourceModified", ref SourceModified);
        ArchiveSerialization.SerializeVector(
            archive, "outputResourceNames", OutputResourceNames, ArchiveSerialization.SerializeValue);
        ArchiveSerialization.SerializeSet(
            archive, "modifiedResourceNames", ModifiedResourceNames, ArchiveSerialization.SerializeValue);
        ArchiveSerialization.SerializeSet(
            archive, "appliedTransformers", AppliedTransformers, ArchiveSerialization.SerializeValue);
        ArchiveSerialization.SerializeMap(archive, "dependencyModificationTimes", DependencyModificationTimes,
            ArchiveSerialization.SerializeValue, ArchiveSerialization.SerializeValue);
    }

    public static AssetTransformerOutput FromBase64(string base64)
    {
        using var archive = new Base64InputArchive(base64);
        var result = new AssetTransformerOutput();
        SerializeValue(archive, "output", result);
        return result;
    }

    public string ToBase64()
    {
        using var archive = new Base64OutputArchive();
        SerializeValue(archive, "output", this);
        return archive.GetBase64();
    }

    public AssetTransformerOutput Clone()
    {
        return new AssetTransformerOutput
        {
            SourceModified = SourceModified,
            OutputResourceNames = [.. OutputResourceNames],
            ModifiedResourceNames = new HashSet<string>(ModifiedResourceNames, StringComparer.Ordinal),
            AppliedTransformers = new HashSet<string>(AppliedTransformers, StringComparer.Ordinal),
            DependencyModificationTimes = new Dictionary<string, uint>(DependencyModificationTimes, StringComparer.Ordinal),
        };
    }

    object ICloneable.Clone() => Clone();

    /// <summary>Whether the source file was modified.</summary>
    public bool SourceModified;
    /// <summary>Resource names of the output files. Do not add source files here!</summary>
    public List<string> OutputResourceNames = [];
    /// <summary>Resource names of additionally modified files (outside of temporary folder).</summary>
    public HashSet<string> ModifiedResourceNames = new(StringComparer.Ordinal);
    /// <summary>Types of transformers that were applied to the asset.</summary>
    public HashSet<string> AppliedTransformers = new(StringComparer.Ordinal);
    /// <summary>Other files that were used to generate the output.</summary>
    public Dictionary<string, uint> DependencyModificationTimes = new(StringComparer.Ordinal);

    private static void SerializeValue(Archive archive, string name, AssetTransformerOutput value)
    {
        using var block = archive.OpenUnorderedBlock(name);
        value.SerializeInBlock(archive);
    }
}
