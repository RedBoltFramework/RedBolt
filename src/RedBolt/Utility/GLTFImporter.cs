// Copyright (c) 2017-2025 the rbfx project.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Utility;

using System;
using System.Collections.Generic;
using System.Linq;

using Tg = RedBolt.Bindings.TinyGltf;

using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.IO;
using RedBolt.Logging;
using RedBolt.Math;

public struct GLTFImporterSettings : ICloneable
{
    public GLTFImporterSettings() { }

    public string AssetName = "Asset";

    public bool MirrorX;
    public float Scale = 1.0f;
    public Quaternion Rotation = new();

    public bool FadeTransparency;

    public bool CleanupBoneNames = true;
    public bool CleanupRootNodes = true;
    public bool CombineLODs = true;
    public List<string> SkipTags = [];
    public bool KeepNamesOnMerge;
    public bool AddEmptyNodesToSkeleton;

    public float OffsetMatrixError = 0.00002f;
    public float KeyFrameTimeError = MathDefs.Epsilon;

    public Dictionary<string, string> NodeRenames = [];

    public bool GpuResources;

    /// <summary>Settings that affect only preview scene.</summary>
    public struct PreviewSettings
    {
        public PreviewSettings() { }

        /// <summary>Whether to add directional light source if scene doesn't contain any light sources.</summary>
        public bool AddLights = true;

        /// <summary>Whether to add skybox background. Doesn't affect object reflections!</summary>
        public bool AddSkybox = true;
        public string SkyboxMaterial = "Materials/DefaultSkybox.xml";

        /// <summary>Whether to add cubemap for object reflections.</summary>
        public bool AddReflectionProbe = true;
        public string ReflectionProbeCubemap = "Textures/DefaultSkybox.xml";

        public bool HighRenderQuality = true;
    }

    public PreviewSettings Preview = new();

    public GLTFImporterSettings Clone() => new()
    {
        AssetName = AssetName,
        MirrorX = MirrorX,
        Scale = Scale,
        Rotation = Rotation,
        FadeTransparency = FadeTransparency,
        CleanupBoneNames = CleanupBoneNames,
        CleanupRootNodes = CleanupRootNodes,
        CombineLODs = CombineLODs,
        SkipTags = [.. SkipTags],
        KeepNamesOnMerge = KeepNamesOnMerge,
        AddEmptyNodesToSkeleton = AddEmptyNodesToSkeleton,
        OffsetMatrixError = OffsetMatrixError,
        KeyFrameTimeError = KeyFrameTimeError,
        NodeRenames = new Dictionary<string, string>(NodeRenames),
        GpuResources = GpuResources,
        Preview = new PreviewSettings
        {
            AddLights = Preview.AddLights,
            AddSkybox = Preview.AddSkybox,
            SkyboxMaterial = Preview.SkyboxMaterial,
            AddReflectionProbe = Preview.AddReflectionProbe,
            ReflectionProbeCubemap = Preview.ReflectionProbeCubemap,
            HighRenderQuality = Preview.HighRenderQuality
        }
    };

    object ICloneable.Clone() => Clone();

    public void SerializeInBlock(Archive archive)
    {
        ArchiveSerialization.SerializeOptionalValue(archive, "assetName", ref AssetName, string.Empty, ArchiveSerialization.SerializeValue);
        ArchiveSerialization.SerializeOptionalValue(archive, "mirrorX", ref MirrorX, false, ArchiveSerialization.SerializeValue);
        ArchiveSerialization.SerializeOptionalValue(archive, "scale", ref Scale, 0.0f, ArchiveSerialization.SerializeValue);
        ArchiveSerialization.SerializeOptionalValue(archive, "rotation", ref Rotation, new Quaternion(), ArchiveSerialization.SerializeValue);
        ArchiveSerialization.SerializeOptionalValue(archive, "fadeTransparency", ref FadeTransparency, false, ArchiveSerialization.SerializeValue);
        ArchiveSerialization.SerializeOptionalValue(archive, "cleanupBoneNames", ref CleanupBoneNames, false, ArchiveSerialization.SerializeValue);
        ArchiveSerialization.SerializeOptionalValue(archive, "cleanupRootNodes", ref CleanupRootNodes, false, ArchiveSerialization.SerializeValue);
        ArchiveSerialization.SerializeOptionalValue(archive, "combineLODs", ref CombineLODs, false, ArchiveSerialization.SerializeValue);
        SerializeOptionalStringVector(archive, "skipTag", SkipTags, []);
        ArchiveSerialization.SerializeOptionalValue(archive, "keepNamesOnMerge", ref KeepNamesOnMerge, false, ArchiveSerialization.SerializeValue);
        ArchiveSerialization.SerializeOptionalValue(archive, "addEmptyNodesToSkeleton", ref AddEmptyNodesToSkeleton, false, ArchiveSerialization.SerializeValue);
        ArchiveSerialization.SerializeOptionalValue(archive, "offsetMatrixError", ref OffsetMatrixError, 0.0f, ArchiveSerialization.SerializeValue);
        ArchiveSerialization.SerializeOptionalValue(archive, "keyFrameTimeError", ref KeyFrameTimeError, 0.0f, ArchiveSerialization.SerializeValue);
        SerializeOptionalStringMap(archive, "nodeRenames", NodeRenames, []);
        ArchiveSerialization.SerializeOptionalValue(archive, "gpuResources", ref GpuResources, false, ArchiveSerialization.SerializeValue);
        ArchiveSerialization.SerializeOptionalValue(archive, "addLights", ref Preview.AddLights, false, ArchiveSerialization.SerializeValue);
        ArchiveSerialization.SerializeOptionalValue(archive, "addSkybox", ref Preview.AddSkybox, false, ArchiveSerialization.SerializeValue);
        ArchiveSerialization.SerializeOptionalValue(archive, "skyboxMaterial", ref Preview.SkyboxMaterial, string.Empty, ArchiveSerialization.SerializeValue);
        ArchiveSerialization.SerializeOptionalValue(archive, "addReflectionProbe", ref Preview.AddReflectionProbe, false, ArchiveSerialization.SerializeValue);
        ArchiveSerialization.SerializeOptionalValue(archive, "reflectionProbeCubemap", ref Preview.ReflectionProbeCubemap, string.Empty, ArchiveSerialization.SerializeValue);
        ArchiveSerialization.SerializeOptionalValue(archive, "highRenderQuality", ref Preview.HighRenderQuality, false, ArchiveSerialization.SerializeValue);
    }

    private static void SerializeOptionalStringVector(Archive archive, string name, List<string> value,
        List<string> defaultValue)
    {
        if (!archive.IsUnorderedAccessSupportedInCurrentBlock)
        {
            ArchiveSerialization.SerializeStringVector(archive, name, value);
            return;
        }
        var initialized = archive.IsInput ? archive.HasElementOrBlock(name) : !value.SequenceEqual(defaultValue);
        if (initialized)
            ArchiveSerialization.SerializeStringVector(archive, name, value);
        else if (archive.IsInput)
        {
            value.Clear();
            value.AddRange(defaultValue);
        }
    }

    private static void SerializeOptionalStringMap(Archive archive, string name,
        Dictionary<string, string> value, Dictionary<string, string> defaultValue)
    {
        if (!archive.IsUnorderedAccessSupportedInCurrentBlock)
        {
            SerializeStringMap(archive, name, value);
            return;
        }
        var initialized = archive.IsInput
            ? archive.HasElementOrBlock(name)
            : value.Count != defaultValue.Count || value.Any(pair =>
                !defaultValue.TryGetValue(pair.Key, out var defaultItem) || pair.Value != defaultItem);
        if (!initialized && archive.IsUnorderedAccessSupportedInCurrentBlock)
        {
            if (archive.IsInput)
            {
                value.Clear();
                foreach (var pair in defaultValue)
                    value.Add(pair.Key, pair.Value);
            }
            return;
        }

        SerializeStringMap(archive, name, value);
    }

    private static void SerializeStringMap(Archive archive, string name, Dictionary<string, string> value)
    {
        using var block = archive.OpenArrayBlock(name, value.Count);
        if (archive.IsInput)
        {
            value.Clear();
            for (var i = 0; i < block.SizeHint; ++i)
            {
                using var itemBlock = archive.OpenUnorderedBlock("element");
                var key = string.Empty;
                var item = string.Empty;
                archive.Serialize("key", ref key);
                archive.Serialize("value", ref item);
                value[key] = item;
            }
        }
        else
        {
            foreach (var pair in value)
            {
                using var itemBlock = archive.OpenUnorderedBlock("element");
                var key = pair.Key;
                var item = pair.Value;
                archive.Serialize("key", ref key);
                archive.Serialize("value", ref item);
            }
        }
    }
}

public class GLTFImporterCallback
{
    public virtual void OnModelLoaded(ModelView modelView) { }
    public virtual void OnAnimationLoaded(Animation animation) { }

    /// <summary>Creates skin and empty mesh for given nodes (searched globally by name), if not present.</summary>
    public virtual List<string> GetArtificialSkinNodes() => [];
}

/// <summary>Utility class to load GLTF file and save it as Urho resources.</summary>
/// <remarks>
/// Temporarily loads resources into resource cache, removes them from the cache on destruction.
/// It's better to use this utility from separate executable.
/// </remarks>
public sealed partial class GLTFImporter : RedObject, IDisposable
{
    public GLTFImporter(GLTFImporterSettings settings)
    {
        _settings = settings.Clone();
    }

    public override void Dispose()
    {
        _impl?.Dispose();
        _impl = null;
        base.Dispose();
    }

    /// <summary>Load primary GLTF file into memory without any processing.</summary>
    public bool LoadFile(string fileName) => LoadFileInternal(() => GLTFImporterFunctions.LoadGLTF(fileName));

    public bool LoadFileBinary(ReadOnlySpan<byte> data)
    {
        try
        {
            if (_model is not null || _impl is not null)
                throw new InvalidOperationException("Primary source model is already loaded");
            _model = GLTFImporterFunctions.LoadGLTFBinary(data);
            GLTFImporterFunctions.RenameNodes(_model, _settings.NodeRenames);
            return true;
        }
        catch (InvalidOperationException e)
        {
            Log.GetLogger().Error("{}", e.Message);
            return false;
        }
    }

    /// <summary>Load and merge secondary GLTF file.</summary>
    /// <remarks>Merge functionality is limited, unsupported content of secondary file is ignored.</remarks>
    public bool MergeFile(string fileName, string assetName)
    {
        try
        {
            if (_impl is not null)
                throw new InvalidOperationException("Source GLTF model is already processed");
            if (_model is null)
                throw new InvalidOperationException("Primary source model is not loaded");
            string? overrideName = _settings.KeepNamesOnMerge ? null : assetName;
            var secondaryModel = GLTFImporterFunctions.LoadGLTF(fileName);
            GLTFImporterFunctions.RenameNodes(secondaryModel, _settings.NodeRenames);
            GLTFImporterFunctions.MergeModels(_model, secondaryModel, overrideName);
            return true;
        }
        catch (InvalidOperationException e)
        {
            Log.GetLogger().Error("{}", e.Message);
            return false;
        }
    }

    /// <summary>Process loaded GLTF files and import resources. Injects resources into resource cache!</summary>
    public bool Process(string outputPath, string resourceNamePrefix, GLTFImporterCallback? callback)
    {
        try
        {
            if (_model is null)
                throw new InvalidOperationException("Source GLTF model is not loaded");
            if (_impl is not null)
                throw new InvalidOperationException("Source GLTF model is already processed");
            callback ??= _defaultCallback;
            GLTFImporterFunctions.PreprocessModel(_model, _settings, callback);
            _impl = new Impl(_settings, _model, outputPath, resourceNamePrefix, callback);
            _model = null;
            return true;
        }
        catch (InvalidOperationException e)
        {
            Log.GetLogger().Error("{}", e.Message);
            return false;
        }
    }

    /// <summary>Save generated resources.</summary>
    public bool SaveResources()
    {
        try
        {
            if (_impl is null)
                throw new InvalidOperationException("Imported asserts weren't cooked");
            _impl.SaveResources();
            return true;
        }
        catch (InvalidOperationException e)
        {
            Log.GetLogger().Error("{}", e.Message);
            return false;
        }
    }

    /// <summary>Return saved resources and their absolute names.</summary>
    public IReadOnlyDictionary<string, string> GetSavedResources()
    {
        if (_impl is not null)
            return _impl.GetResourceNames();
        Log.GetLogger().Error("Imported asserts weren't cooked");
        return EmptyResourceNames;
    }

    /// <summary>Convert GLTF transform to the engine format.</summary>
    public Transform ConvertTransform(Transform sourceTransform)
    {
        if (_impl is not null)
            return _impl.ConvertTransform(sourceTransform);
        Log.GetLogger().Error("Imported asserts weren't cooked");
        return sourceTransform;
    }

    private bool LoadFileInternal(Func<Tg.Model> getModel)
    {
        try
        {
            if (_model is not null || _impl is not null)
                throw new InvalidOperationException("Primary source model is already loaded");
            _model = getModel();
            GLTFImporterFunctions.RenameNodes(_model, _settings.NodeRenames);
            return true;
        }
        catch (InvalidOperationException e)
        {
            Log.GetLogger().Error("{}", e.Message);
            return false;
        }
    }

    private static readonly IReadOnlyDictionary<string, string> EmptyResourceNames =
        new Dictionary<string, string>();

    private readonly GLTFImporterSettings _settings;
    private Tg.Model? _model;
    private Impl? _impl;
    private readonly GLTFImporterCallback _defaultCallback = new();
}
