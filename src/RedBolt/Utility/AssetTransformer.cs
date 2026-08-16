// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Utility;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

using Microsoft.Extensions.Logging;

using RedBolt.Core;
using RedBolt.Engine;
using RedBolt.IO;
using RedBolt.Logging;
using RedBolt.Scene;

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

/// <summary>Interface of a script that can be used to transform assets.</summary>
/// <remarks>
/// Flavor is used for hierarchical filtering.
/// Transformer with a flavor is not used unless user requested this (or more specific) flavor.
/// </remarks>
[Reflected]
public partial class AssetTransformer : Serializable
{
    public AssetTransformer()
    {
    }

    /// <summary>Return whether the transformer array is applied to the given asset in any way.</summary>
    /// <remarks>It's not checked whether transformers are part of post-transform pass.</remarks>
    public static bool IsApplicable(AssetTransformerInput input, IReadOnlyList<AssetTransformer> transformers)
    {
        foreach (var transformer in transformers)
        {
            if (transformer.IsApplicable(input))
                return true;
        }
        return false;
    }

    /// <summary>Execute transformer array on the asset.</summary>
    public static bool ExecuteTransformers(AssetTransformerInput input, AssetTransformerOutput output,
        IReadOnlyList<AssetTransformer> transformers, bool isNestedExecution)
    {
        Debug.Assert(transformers.Count != 0);
        foreach (var transformer in transformers)
        {
            if (isNestedExecution && !transformer.IsExecutedOnOutput())
                continue;

            if (transformer.IsApplicable(input))
            {
                if (!transformer.Execute(input, output, transformers))
                    return false;
                output.AppliedTransformers.Add(transformer.GetTypeName());
            }
        }
        return true;
    }

    /// <summary>Execute transformer array on the asset and copy results in the output path.</summary>
    public static bool ExecuteTransformersAndStore(AssetTransformerInput input, string outputPath,
        AssetTransformerOutput output, IReadOnlyList<AssetTransformer> transformers)
    {
        Debug.Assert(transformers.Count != 0);

        var tempFolderPath = input.TempPath.Replace('\\', '/');
        if (tempFolderPath.Length > 0 && tempFolderPath[^1] != '/')
            tempFolderPath += '/';
        try
        {
            try
            {
                Directory.CreateDirectory(tempFolderPath);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }

            if (!ExecuteTransformers(input, output, transformers, false))
                return false;

            var copiedFiles = new List<string>();
            if (Directory.Exists(tempFolderPath))
            {
                var outputFolderPath = outputPath.Replace('\\', '/');
                if (outputFolderPath.Length > 0 && outputFolderPath[^1] != '/')
                    outputFolderPath += '/';

                foreach (var sourceFileName in Directory.EnumerateFiles(
                    tempFolderPath, "*", SearchOption.AllDirectories))
                {
                    var relativeFileName = Path.GetRelativePath(tempFolderPath, sourceFileName).Replace('\\', '/');
                    var pathComponents = relativeFileName.Split('/');
                    var isHidden = false;
                    var currentPath = tempFolderPath;
                    foreach (var pathComponent in pathComponents)
                    {
                        currentPath = Path.Combine(currentPath, pathComponent);
#if REDBOLT_PLATFORM_WINDOWS
                        if ((System.IO.File.GetAttributes(currentPath) & FileAttributes.Hidden) != 0)
#else
                        if (pathComponent.StartsWith(".", StringComparison.Ordinal))
#endif
                        {
                            isHidden = true;
                            break;
                        }
                    }
                    if (isHidden)
                        continue;

                    var destinationFileName = outputFolderPath + relativeFileName;
                    try
                    {
                        var destinationPath = Path.GetDirectoryName(destinationFileName);
                        if (!string.IsNullOrEmpty(destinationPath))
                            Directory.CreateDirectory(destinationPath);
                        System.IO.File.Copy(sourceFileName, destinationFileName, true);
                        copiedFiles.Add(destinationFileName);
                    }
                    catch (IOException)
                    {
                    }
                    catch (UnauthorizedAccessException)
                    {
                    }
                }
            }

            foreach (var fileName in copiedFiles)
                output.OutputResourceNames.Add(fileName[outputPath.Length..]);

            return true;
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempFolderPath))
                    Directory.Delete(tempFolderPath, true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    /// <summary>Return whether the transformer can be applied to the given asset. Should be as fast as possible.</summary>
    public virtual bool IsApplicable(AssetTransformerInput input) => false;

    /// <summary>Execute this transformer on the asset. Return true if any action was performed.</summary>
    public virtual bool Execute(AssetTransformerInput input, AssetTransformerOutput output,
        IReadOnlyList<AssetTransformer> transformers)
    {
        return false;
    }

    /// <summary>Return whether the importer of this type should be invoked at most once.</summary>
    public virtual bool IsSingleInstanced() => true;

    /// <summary>Return whether to execute this transformer on the output of the other transformer.</summary>
    public virtual bool IsExecutedOnOutput() => false;

    /// <summary>Return whether this transformer should be executed in post-processing pass.</summary>
    public virtual bool IsPostTransform() => false;

    /// <summary>Manage requirement flavor of the transformer.</summary>
    public void SetFlavor(in ApplicationFlavorPattern value)
    {
        _flavor = value.Clone();
    }

    /// <summary>Manage requirement flavor of the transformer.</summary>
    public ref readonly ApplicationFlavorPattern GetFlavor() => ref _flavor;

    /// <summary>Helper to add file dependency.</summary>
    protected void AddDependency(AssetTransformerInput input, AssetTransformerOutput output, string fileName)
    {
        var dataFolder = input.OriginalInputFileName[
            ..(input.OriginalInputFileName.Length - input.OriginalResourceName.Length)];

        if (!fileName.StartsWith(dataFolder, StringComparison.Ordinal))
        {
            Log.GetLogger().LogError(
                "Dependency file '{FileName}' is not in the same folder as the input file '{InputFileName}'",
                fileName, input.OriginalInputFileName);
            return;
        }

        var relativeFileName = fileName[dataFolder.Length..];
        var modificationTime = 0u;
        try
        {
            var modifiedTime = System.IO.File.GetLastWriteTimeUtc(fileName);
#if REDBOLT_PLATFORM_WINDOWS
            {
                var creationTime = System.IO.File.GetCreationTimeUtc(fileName);
                if (creationTime > modifiedTime)
                    modifiedTime = creationTime;
            }
#endif

            var seconds = new DateTimeOffset(modifiedTime).ToUnixTimeSeconds();
            modificationTime = seconds > 0 ? unchecked((uint)seconds) : 0u;
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
        output.DependencyModificationTimes[relativeFileName] = modificationTime;
    }

    private ApplicationFlavorPattern _flavor = new();
}

/// <summary>Dependency between two transformer classes.</summary>
/// <remarks>Note: this dependency is global within a project regardless of transformer scope!</remarks>
public class AssetTransformerDependency : ICloneable
{
    public string Class = string.Empty;
    public string DependsOn = string.Empty;

    // TODO: Extract common code to Resource
    public void SerializeInBlock(Archive archive)
    {
        ArchiveSerialization.SerializeValue(archive, "Class", ref Class);
        ArchiveSerialization.SerializeValue(archive, "DependsOn", ref DependsOn);
    }

    public AssetTransformerDependency Clone()
    {
        return new AssetTransformerDependency
        {
            Class = Class,
            DependsOn = DependsOn,
        };
    }

    object ICloneable.Clone() => Clone();
}
