// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Editor.Assets;

using System;
using System.Collections.Generic;
using System.IO;

using Microsoft.Extensions.Logging;

using RedBolt.Core;
using RedBolt.Editor.Project;
using RedBolt.Graphics;
using RedBolt.IO;
using RedBolt.Logging;
using RedBolt.Math;
using RedBolt.Resource;
using RedBolt.Utility;
using ProjectSubsystem = RedBolt.Editor.Project.Project;
using SystemFile = global::System.IO.File;

/// <summary>Asset transformer that imports GLTF models.</summary>
[Reflected(ObjectCategory.Category_Transformer)]
public partial class ModelImporter : AssetTransformer
{
    public ModelImporter()
    {
        _defaultParams.ImporterSettings.SkipTags = [.. DefaultSkipTags];
        _callback = new ImporterCallback(this);
    }

    public override void Dispose()
    {
        _defaultParams.Dispose();
        base.Dispose();
    }

    public override bool IsApplicable(AssetTransformerInput input)
    {
        var toolManager = GetToolManager();

        if (IsFileNameGLTF(input.ResourceName))
            return true;

        if (IsFileNameFBX(input.ResourceName))
        {
            if (!toolManager.HasFBX2glTF)
            {
                if (!_fbx2glTFNotFoundLogged)
                {
                    Log.GetLogger().LogError(
                        "FBX2glTF is not found, cannot import FBX files. See Settings/Editor/ExternalTools.");
                }
                _fbx2glTFNotFoundLogged = true;
                return false;
            }

            return true;
        }

        if (IsFileNameBlend(input.ResourceName))
        {
            if (!toolManager.HasBlender)
            {
                if (!_blenderNotFoundLogged)
                {
                    Log.GetLogger().LogError(
                        "Blender is not found, cannot import Blender files. See Settings/Editor/ExternalTools.");
                }
                _blenderNotFoundLogged = true;
                return false;
            }

            return true;
        }
        return false;
    }

    public override bool Execute(AssetTransformerInput input, AssetTransformerOutput output,
        IReadOnlyList<AssetTransformer> transformers)
    {
        var paramsFileName = GetParametersFileName(input.InputFileName);
        using var parameters = new TransformerParams();
        if (LoadParameters(parameters, paramsFileName))
            AddDependency(input, output, paramsFileName);
        parameters.ImporterSettings.AssetName = Path.GetFileName(input.OriginalInputFileName);

        using var handle = LoadData(input.InputFileName, parameters, input.TempPath);
        if (handle is null)
            return false;

        return ImportGLTF(handle, parameters, input, output, transformers);
    }

    protected struct ResetRootMotionInfo
    {
        public float Factor;
        public Vector3f PositionWeight;
        public float RotationSwingWeight;
        public float RotationTwistWeight;
        public float ScaleWeight;

        public void SerializeInBlock(Archive archive)
        {
            ArchiveSerialization.SerializeOptionalValue(
                archive, "factor", ref Factor, 0.0f, ArchiveSerialization.SerializeValue);
            ArchiveSerialization.SerializeOptionalValue(
                archive, "positionWeight", ref PositionWeight, new Vector3f(), ArchiveSerialization.SerializeValue);
            ArchiveSerialization.SerializeOptionalValue(
                archive, "rotationSwingWeight", ref RotationSwingWeight, 0.0f,
                ArchiveSerialization.SerializeValue);
            ArchiveSerialization.SerializeOptionalValue(
                archive, "rotationTwistWeight", ref RotationTwistWeight, 0.0f,
                ArchiveSerialization.SerializeValue);
            ArchiveSerialization.SerializeOptionalValue(
                archive, "scaleWeight", ref ScaleWeight, 0.0f, ArchiveSerialization.SerializeValue);
        }
    }

    protected sealed class TransformerParams : IArchiveSerializable, ICloneable, IDisposable
    {
        public GLTFImporterSettings ImporterSettings = new();

        public bool RepairLooping;

        public bool BlenderApplyModifiers = true;
        public bool BlenderDeformingBonesOnly = true;
        public bool LightmapUVGenerate;
        public float LightmapUVTexelsPerUnit = 10.0f;
        public uint LightmapUVChannel = 1;

        public List<string> AppendFiles = [];
        public Dictionary<string, ResetRootMotionInfo> ResetRootMotion = new(StringComparer.Ordinal);
        public Dictionary<string, StringVariantMap> ResourceMetadata = new(StringComparer.Ordinal);
        public List<string> ArtificialSkinNodes = [];

        public void SerializeInBlock(Archive archive)
        {
            ImporterSettings.SerializeInBlock(archive);

            ArchiveSerialization.SerializeOptionalValue(
                archive, "repairLooping", ref RepairLooping, false, ArchiveSerialization.SerializeValue);
            ArchiveSerialization.SerializeOptionalValue(
                archive, "blenderApplyModifiers", ref BlenderApplyModifiers, true,
                ArchiveSerialization.SerializeValue);
            ArchiveSerialization.SerializeOptionalValue(
                archive, "blenderDeformingBonesOnly", ref BlenderDeformingBonesOnly, true,
                ArchiveSerialization.SerializeValue);
            ArchiveSerialization.SerializeOptionalValue(
                archive, "lightmapUVGenerate", ref LightmapUVGenerate, false,
                ArchiveSerialization.SerializeValue);
            ArchiveSerialization.SerializeOptionalValue(
                archive, "lightmapUVTexelsPerUnit", ref LightmapUVTexelsPerUnit, 10.0f,
                ArchiveSerialization.SerializeValue);
            ArchiveSerialization.SerializeOptionalValue(
                archive, "lightmapUVChannel", ref LightmapUVChannel, 1u,
                ArchiveSerialization.SerializeValue);

            SerializeOptionalStringVector(archive, "appendFiles", AppendFiles);

            SerializeOptionalResetRootMotionMap(archive, "resetRootMotion", ResetRootMotion);
            SerializeOptionalResourceMetadataMap(archive, "resourceMetadata", ResourceMetadata);
            SerializeOptionalStringVector(archive, "artificialSkinNodes", ArtificialSkinNodes);
        }

        public TransformerParams Clone()
        {
            var result = new TransformerParams();
            result.CopyFrom(this);
            return result;
        }

        object ICloneable.Clone() => Clone();

        public void CopyFrom(TransformerParams other)
        {
            if (ReferenceEquals(this, other))
                return;

            DisposeResourceMetadata();

            ImporterSettings = other.ImporterSettings.Clone();
            RepairLooping = other.RepairLooping;
            BlenderApplyModifiers = other.BlenderApplyModifiers;
            BlenderDeformingBonesOnly = other.BlenderDeformingBonesOnly;
            LightmapUVGenerate = other.LightmapUVGenerate;
            LightmapUVTexelsPerUnit = other.LightmapUVTexelsPerUnit;
            LightmapUVChannel = other.LightmapUVChannel;
            AppendFiles = [.. other.AppendFiles];
            ResetRootMotion = new Dictionary<string, ResetRootMotionInfo>(
                other.ResetRootMotion, other.ResetRootMotion.Comparer);
            ResourceMetadata = new Dictionary<string, StringVariantMap>(
                other.ResourceMetadata.Count, other.ResourceMetadata.Comparer);
            foreach (var (name, metadata) in other.ResourceMetadata)
                ResourceMetadata.Add(name, new StringVariantMap(metadata));
            ArtificialSkinNodes = [.. other.ArtificialSkinNodes];
        }

        public void Dispose()
        {
            DisposeResourceMetadata();
        }

        private void DisposeResourceMetadata()
        {
            foreach (var metadata in ResourceMetadata.Values)
                metadata.Dispose();
            ResourceMetadata.Clear();
        }

        private static void SerializeOptionalStringVector(Archive archive, string name, List<string> values)
        {
            var serialize = !archive.IsUnorderedAccessSupportedInCurrentBlock
                || (archive.IsInput ? archive.HasElementOrBlock(name) : values.Count != 0);
            if (serialize)
                ArchiveSerialization.SerializeStringVector(archive, name, values);
            else if (archive.IsInput)
                values.Clear();
        }

        private static void SerializeOptionalResetRootMotionMap(Archive archive, string name,
            Dictionary<string, ResetRootMotionInfo> values)
        {
            var serialize = !archive.IsUnorderedAccessSupportedInCurrentBlock
                || (archive.IsInput ? archive.HasElementOrBlock(name) : values.Count != 0);
            if (serialize)
            {
                ArchiveSerialization.SerializeMap(
                    archive, name, values, ArchiveSerialization.SerializeValue, SerializeResetRootMotionInfo);
            }
            else if (archive.IsInput)
                values.Clear();
        }

        private static void SerializeOptionalResourceMetadataMap(Archive archive, string name,
            Dictionary<string, StringVariantMap> values)
        {
            var serialize = !archive.IsUnorderedAccessSupportedInCurrentBlock
                || (archive.IsInput ? archive.HasElementOrBlock(name) : values.Count != 0);
            if (serialize)
            {
                if (archive.IsInput)
                {
                    foreach (var metadata in values.Values)
                        metadata.Dispose();
                    values.Clear();
                }
                ArchiveSerialization.SerializeMap(
                    archive, name, values, ArchiveSerialization.SerializeValue, SerializeStringVariantMap);
            }
            else if (archive.IsInput)
            {
                foreach (var metadata in values.Values)
                    metadata.Dispose();
                values.Clear();
            }
        }

        private static void SerializeResetRootMotionInfo(
            Archive archive, string name, ref ResetRootMotionInfo value)
        {
            using var block = archive.OpenUnorderedBlock(name);
            value.SerializeInBlock(archive);
        }

        private static void SerializeStringVariantMap(
            Archive archive, string name, ref StringVariantMap value)
        {
            value ??= new StringVariantMap();
            ArchiveSerialization.SerializeValue(archive, name, value);
        }
    }

    /// <summary>Implement GLTFImporterCallback.</summary>
    /// <remarks>C# uses a forwarding callback because it doesn't support the native secondary base class.</remarks>
    protected virtual void OnModelLoaded(ModelView modelView)
    {
        // Lightmap UV generation is contributed by the optional RedBolt.Glow tools assembly.
        // Legacy fields stay serialized here so pipelines and ModelImporter.json files remain loadable.
    }

    protected virtual void OnAnimationLoaded(Animation animation)
    {
        AppendResourceMetadata(animation);

        if (_currentParams!.ResetRootMotion.TryGetValue(animation.AnimationName, out var resetRootMotion))
            ResetRootMotion(animation, resetRootMotion);

        var isAnimationLooped = IsAnimationLooped(animation);
        var frameStep = GetFrameStep(animation);

        // TODO: It would be better to add a keyframe at the end of the animation
        if (!isAnimationLooped && _currentParams.RepairLooping && frameStep.HasValue)
        {
            animation.SetLength(animation.Length + frameStep.Value);
            animation.AddMetadata(AnimationMetadata.Looped, true);
        }
        else
        {
            animation.AddMetadata(AnimationMetadata.Looped, isAnimationLooped);
        }

        if (frameStep.HasValue)
        {
            var frameRate = MathDefs.RoundToInt(1.0f / frameStep.Value);
            var frameRateError = MathF.Abs(frameRate - 1.0f / frameStep.Value);

            animation.AddMetadata(AnimationMetadata.FrameStep, frameStep.Value);
            if (frameRateError < 0.01f)
                animation.AddMetadata(AnimationMetadata.FrameRate, frameRate);
        }
    }

    protected virtual List<string> GetArtificialSkinNodes()
    {
        return [.. _currentParams!.ArtificialSkinNodes];
    }

    protected void ResetRootMotion(Animation animation, in ResetRootMotionInfo info)
    {
        var rootTrack = GetRootAnimationTrack(animation);
        if (rootTrack is null)
            return;

        var firstFrame = rootTrack.KeyFrames[0];
        var lastFrame = rootTrack.KeyFrames[^1];
        if (firstFrame.Time == lastFrame.Time)
            return;

        // Copy the track
        var originalTrackName = rootTrack.Name + "_Original";
        if (animation.GetTrack(originalTrackName) is not null)
        {
            Log.GetLogger().LogWarning(
                "Cannot create backup track '{OriginalTrackName}' for root motion, skipping", originalTrackName);
            return;
        }

        animation.AddMetadata(AnimationMetadata.RootTrack, rootTrack.Name);
        animation.AddMetadata(AnimationMetadata.OriginalRootTrack, originalTrackName);

        var originalRootTrack = animation.CreateTrack(originalTrackName);
        originalRootTrack.ChannelMask = rootTrack.ChannelMask;
        originalRootTrack.KeyFrames.AddRange(rootTrack.KeyFrames);

        // Calculate approximate velocity
        var positionDelta = lastFrame.Position - firstFrame.Position;
        var rotationDelta = lastFrame.Rotation * firstFrame.Rotation.Inverse;
        var scaleDelta = lastFrame.Scale - firstFrame.Scale;

        var frameDuration = lastFrame.Time - firstFrame.Time;
        var linearVelocity = positionDelta / frameDuration;
        var angularVelocity = rotationDelta.AngularVelocity / frameDuration;
        var scaleVelocity = scaleDelta / frameDuration;

        animation.AddMetadata(AnimationMetadata.RootLinearVelocity, linearVelocity);
        animation.AddMetadata(AnimationMetadata.RootAngularVelocity, angularVelocity);
        animation.AddMetadata(AnimationMetadata.RootScaleVelocity, scaleVelocity);

        // Calculate the reset transform
        var resetTime = MathDefs.Lerp(firstFrame.Time, lastFrame.Time, info.Factor);
        var frameIndex = 0u;
        var resetTransform = new Transform();
        rootTrack.Sample(resetTime, lastFrame.Time, false, ref frameIndex, ref resetTransform);

        for (var i = 0; i < rootTrack.KeyFrames.Count; ++i)
        {
            var frame = rootTrack.KeyFrames[i];
            // Preserve native reference semantics: the first and last frames may have been modified
            // by an earlier iteration of this loop.
            var firstTransform = ToTransform(rootTrack.KeyFrames[0]);
            var lastTransform = ToTransform(rootTrack.KeyFrames[^1]);
            var interpolatedTransform = firstTransform.Lerp(lastTransform, frame.Time);
            var deltaTransform = ToTransform(frame) * interpolatedTransform.Inverse();
            var filteredTransform = deltaTransform * resetTransform;

            var (deltaSwing, deltaTwist) = filteredTransform.Rotation.ToSwingTwist(rotationDelta.Axis);

            frame.Position = VectorLerp(resetTransform.Position, filteredTransform.Position, info.PositionWeight);
            frame.Rotation = Quaternion.IDENTITY.Slerp(deltaSwing, info.RotationSwingWeight)
                * Quaternion.IDENTITY.Slerp(deltaTwist, info.RotationTwistWeight) * resetTransform.Rotation;
            frame.Scale = resetTransform.Scale.Lerp(filteredTransform.Scale, info.ScaleWeight);
            rootTrack.KeyFrames[i] = frame;
        }
    }

    protected void AppendResourceMetadata(ResourceWithMetadata resource)
    {
        if (!_currentParams!.ResourceMetadata.TryGetValue(Path.GetFileName(resource.Name), out var metadata))
            return;

        foreach (var (name, value) in metadata)
            resource.AddMetadata(name, value);
    }

    private sealed class GLTFFileInfo : IDisposable
    {
        public GLTFFileInfo(string fileName, bool deleteOnDispose = false)
        {
            FileName = fileName;
            _deleteOnDispose = deleteOnDispose;
        }

        public void Dispose()
        {
            if (_deleteOnDispose)
                DeleteFile(FileName);
        }

        public string FileName { get; }

        private readonly bool _deleteOnDispose;
    }

    private bool ImportGLTF(GLTFFileInfo fileHandle, TransformerParams parameters,
        AssetTransformerInput input, AssetTransformerOutput output, IReadOnlyList<AssetTransformer> transformers)
    {
        _currentParams = parameters;
        try
        {
            using var importer = new GLTFImporter(parameters.ImporterSettings);

            var outputPath = AddTrailingSlash(input.OutputFileName);
            var resourceNamePrefix = AddTrailingSlash(input.OutputResourceName);
            if (!importer.LoadFile(fileHandle.FileName))
            {
                Log.GetLogger().LogError(
                    "Failed to load asset {ResourceName} as GLTF model", input.ResourceName);
                return false;
            }

            foreach (var secondaryFileName in parameters.AppendFiles)
            {
                var secondaryFilePath = GetPath(input.OriginalInputFileName) + secondaryFileName;
                using var secondaryFileHandle = LoadData(secondaryFilePath, parameters, input.TempPath);
                if (secondaryFileHandle is null)
                {
                    Log.GetLogger().LogWarning(
                        "Failed to load secondary file {SecondaryFilePath} for asset {ResourceName}",
                        secondaryFilePath, input.ResourceName);
                    continue;
                }

                AddDependency(input, output, secondaryFilePath);

                if (!importer.MergeFile(secondaryFileHandle.FileName, Path.GetFileName(secondaryFilePath)))
                {
                    Log.GetLogger().LogWarning(
                        "Failed to merge secondary file {SecondaryFilePath} into asset {ResourceName}",
                        secondaryFilePath, input.ResourceName);
                    continue;
                }
            }

            if (!importer.Process(outputPath, resourceNamePrefix, _callback))
            {
                Log.GetLogger().LogError("Failed to process asset {ResourceName}", input.ResourceName);
                return false;
            }

            DeleteDirectory(input.OutputFileName);
            DeleteFile(input.OutputFileName);

            if (!importer.SaveResources())
            {
                Log.GetLogger().LogError(
                    "Failed to save output files for asset {ResourceName}", input.ResourceName);
                return false;
            }

            foreach (var (resourceName, fileName) in importer.GetSavedResources())
            {
                var nestedOutput = new AssetTransformerOutput();
                var nestedInput = input.Clone();
                nestedInput.ResourceName = resourceName;
                nestedInput.InputFileName = fileName;
                nestedInput.OutputFileName = fileName;
                if (!AssetTransformer.ExecuteTransformers(nestedInput, nestedOutput, transformers, true))
                {
                    Log.GetLogger().LogError(
                        "Failed to apply nested transformer for asset {ResourceName}", resourceName);
                    return false;
                }

                output.AppliedTransformers.UnionWith(nestedOutput.AppliedTransformers);
            }
            return true;
        }
        finally
        {
            _currentParams = null;
        }
    }

    private string GetParametersFileName(string fileName)
    {
        return fileName + ".d/ModelImporter.json";
    }

    private bool LoadParameters(TransformerParams parameters, string paramsFileName)
    {
        using var baseFile = new JSONFile();
        baseFile.SaveObject("params", _defaultParams);

        using var overrideFile = new JSONFile();
        if (overrideFile.LoadFile(FileIdentifier.FromUri(paramsFileName)))
        {
            baseFile.Root.ReplaceRecursively(overrideFile.Root);
            if (baseFile.LoadObject("params", parameters))
                return true;
        }

        parameters.CopyFrom(_defaultParams);
        return false;
    }

    private GLTFFileInfo? LoadData(string fileName, TransformerParams parameters, string tempPath)
    {
        if (IsFileNameGLTF(fileName, false))
            return LoadDataNative(fileName, parameters);
        else if (IsFileNameFBX(fileName, false))
            return LoadDataFromFBX(fileName, parameters, tempPath);
        else if (IsFileNameBlend(fileName, false))
            return LoadDataFromBlend(fileName, parameters, tempPath);
        return null;
    }

    private GLTFFileInfo? LoadDataNative(string fileName, TransformerParams parameters)
    {
        if (!SystemFile.Exists(fileName))
            return null;

        return new GLTFFileInfo(fileName);
    }

    private GLTFFileInfo? LoadDataFromFBX(string fileName, TransformerParams parameters, string tempPath)
    {
        var toolManager = GetToolManager();

        if (!SystemFile.Exists(fileName))
            return null;

        // fbx2gltf generates extra files that we don't want to copy, create another temporary directory for those
        using var fbxTempFolderHolder = new TemporaryDir(RemoveTrailingSlash(tempPath) + ".2");

        var tempGltfFile = $"{tempPath}{ProcessUtils.GenerateUUID()}.glb";
        string[] arguments =
        [
            "--binary", "--input", fileName, "--output", tempGltfFile,
            "--fbx-temp-dir", fbxTempFolderHolder.Path,
        ];

        if (ProcessUtils.SystemRun(toolManager.FBX2glTF, arguments, out var commandOutput) != 0)
        {
            Log.GetLogger().LogError("{CommandOutput}", commandOutput);
            return null;
        }
        Log.GetLogger().LogDebug("FBX2glTF output:\n{CommandOutput}", commandOutput);

        return new GLTFFileInfo(tempGltfFile, true);
    }

    private GLTFFileInfo? LoadDataFromBlend(string fileName, TransformerParams parameters, string tempPath)
    {
        var toolManager = GetToolManager();

        if (!SystemFile.Exists(fileName))
            return null;

        var tempGltfFile = tempPath + "model.glb";

        // This script is passed as command line argument, so it must be a single line and use single quotes
        var script = "import bpy;"
            + "bpy.ops.export_scene.gltf("
            + $"  filepath='{tempGltfFile}', "
            + "  export_format='GLB', "
            + $"  export_apply={(parameters.BlenderApplyModifiers ? "True" : "False")}, "
            + $"  export_def_bones={(parameters.BlenderDeformingBonesOnly ? "True" : "False")}"
            + ");";

        string[] arguments = ["-b", fileName, "--python-expr", script];

        if (ProcessUtils.SystemRun(toolManager.Blender, arguments, out var commandOutput) != 0)
        {
            Log.GetLogger().LogError("{CommandOutput}", commandOutput);
            return null;
        }
        Log.GetLogger().LogDebug("Blender output:\n{CommandOutput}", commandOutput);

        return new GLTFFileInfo(tempGltfFile, true);
    }

    private ToolManager GetToolManager()
    {
        return GetSubsystem<ProjectSubsystem>().ToolManager;
    }

    private static readonly StringVector DefaultSkipTags = ["[skip]"];

    private static bool IsFileNameGLTF(string fileName, bool strict = true)
    {
        if (!strict && (fileName.EndsWith(".gltf_", StringComparison.OrdinalIgnoreCase)
            || fileName.EndsWith(".glb_", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return fileName.EndsWith(".gltf", StringComparison.OrdinalIgnoreCase)
            || fileName.EndsWith(".glb", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsFileNameFBX(string fileName, bool strict = true)
    {
        if (!strict && fileName.EndsWith(".fbx_", StringComparison.OrdinalIgnoreCase))
            return true;

        return fileName.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsFileNameBlend(string fileName, bool strict = true)
    {
        if (!strict && fileName.EndsWith(".blend_", StringComparison.OrdinalIgnoreCase))
            return true;

        return fileName.EndsWith(".blend", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAnimationLooped(Animation animation)
    {
        foreach (var track in animation.Tracks.Values)
        {
            if (!track.IsLooped())
                return false;
        }
        foreach (var track in animation.VariantTracks.Values)
        {
            if (!track.IsLooped())
                return false;
        }
        return true;
    }

    private static float? GetFrameStep(Animation animation)
    {
        float? frameStep = null;
        foreach (var track in animation.Tracks.Values)
        {
            if (GetTrackStep(track.KeyFrames) is { } trackStep)
                frameStep = MathF.Min(frameStep ?? MathDefs.LargeValue, trackStep);
        }
        foreach (var track in animation.VariantTracks.Values)
        {
            if (GetTrackStep(track.KeyFrames) is { } trackStep)
                frameStep = MathF.Min(frameStep ?? MathDefs.LargeValue, trackStep);
        }
        return frameStep;
    }

    private static float? GetTrackStep<T>(IReadOnlyList<T> keyFrames)
    {
        if (keyFrames.Count <= 1)
            return null;

        return keyFrames switch
        {
            IReadOnlyList<AnimationKeyFrame> animationFrames =>
                animationFrames[^1].Time - animationFrames[^2].Time,
            IReadOnlyList<VariantCurvePoint> variantFrames =>
                variantFrames[^1].Time - variantFrames[^2].Time,
            _ => null,
        };
    }

    private static AnimationTrack? GetRootAnimationTrack(Animation animation)
    {
        var parentTracks = animation.GetMetadata(AnimationMetadata.ParentTracks).GetStringVariantMap();

        var rootTracks = new List<string>();
        foreach (var (name, parent) in parentTracks)
        {
            if (parent.GetString().Length == 0)
                rootTracks.Add(name);
        }

        return rootTracks.Count == 1 ? animation.GetTrack(rootTracks[0]) : null;
    }

    private static Transform ToTransform(in AnimationKeyFrame frame)
    {
        return new Transform(frame.Position, frame.Rotation, frame.Scale);
    }

    private static Vector3f VectorLerp(in Vector3f lhs, in Vector3f rhs, in Vector3f t)
    {
        return lhs * (Vector3f.One - t) + rhs * t;
    }

    private static string AddTrailingSlash(string path)
    {
        return path.EndsWith('/') || path.EndsWith('\\') ? path : path + '/';
    }

    private static string RemoveTrailingSlash(string path)
    {
        return path.TrimEnd('/', '\\');
    }

    private static string GetPath(string fullPath)
    {
        var slash = Math.Max(fullPath.LastIndexOf('/'), fullPath.LastIndexOf('\\'));
        return slash >= 0 ? fullPath[..(slash + 1)] : string.Empty;
    }

    private static void DeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static void DeleteFile(string path)
    {
        try
        {
            if (SystemFile.Exists(path))
                SystemFile.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private sealed class ImporterCallback : GLTFImporterCallback
    {
        public ImporterCallback(ModelImporter owner)
        {
            _owner = owner;
        }

        public override void OnModelLoaded(ModelView modelView) => _owner.OnModelLoaded(modelView);

        public override void OnAnimationLoaded(Animation animation) => _owner.OnAnimationLoaded(animation);

        public override List<string> GetArtificialSkinNodes() => _owner.GetArtificialSkinNodes();

        private readonly ModelImporter _owner;
    }

    private static bool _fbx2glTFNotFoundLogged;
    private static bool _blenderNotFoundLogged;

    private readonly TransformerParams _defaultParams = new();
    private readonly ImporterCallback _callback;

    private TransformerParams? _currentParams;

    private static object? GetMirrorXDefault() => new TransformerParams().ImporterSettings.MirrorX;

    [Serialized("Mirror X", DefaultSource = nameof(GetMirrorXDefault))]
    private bool MirrorX
    {
        get => _defaultParams.ImporterSettings.MirrorX;
        set => _defaultParams.ImporterSettings.MirrorX = value;
    }

    private static object? GetScaleDefault() => new TransformerParams().ImporterSettings.Scale;

    [Serialized("Scale", DefaultSource = nameof(GetScaleDefault))]
    private float Scale
    {
        get => _defaultParams.ImporterSettings.Scale;
        set => _defaultParams.ImporterSettings.Scale = value;
    }

    private static object? GetRotationDefault() => new TransformerParams().ImporterSettings.Rotation;

    [Serialized("Rotation", DefaultSource = nameof(GetRotationDefault))]
    private Quaternion Rotation
    {
        get => _defaultParams.ImporterSettings.Rotation;
        set => _defaultParams.ImporterSettings.Rotation = value;
    }

    private static object? GetFadeTransparencyDefault() => new TransformerParams().ImporterSettings.FadeTransparency;

    [Serialized("Fade Transparency", DefaultSource = nameof(GetFadeTransparencyDefault))]
    private bool FadeTransparency
    {
        get => _defaultParams.ImporterSettings.FadeTransparency;
        set => _defaultParams.ImporterSettings.FadeTransparency = value;
    }

    private static object? GetCleanupBoneNamesDefault() => new TransformerParams().ImporterSettings.CleanupBoneNames;

    [Serialized("Cleanup Bone Names", DefaultSource = nameof(GetCleanupBoneNamesDefault))]
    private bool CleanupBoneNames
    {
        get => _defaultParams.ImporterSettings.CleanupBoneNames;
        set => _defaultParams.ImporterSettings.CleanupBoneNames = value;
    }

    private static object? GetCleanupRootNodesDefault() => new TransformerParams().ImporterSettings.CleanupRootNodes;

    [Serialized("Cleanup Root Nodes", DefaultSource = nameof(GetCleanupRootNodesDefault))]
    private bool CleanupRootNodes
    {
        get => _defaultParams.ImporterSettings.CleanupRootNodes;
        set => _defaultParams.ImporterSettings.CleanupRootNodes = value;
    }

    private static object? GetCombineLODsDefault() => new TransformerParams().ImporterSettings.CombineLODs;

    [Serialized("Combine LODs", DefaultSource = nameof(GetCombineLODsDefault))]
    private bool CombineLODs
    {
        get => _defaultParams.ImporterSettings.CombineLODs;
        set => _defaultParams.ImporterSettings.CombineLODs = value;
    }

    private static object? GetSkipTagsDefault() => new StringVector(DefaultSkipTags);

    [Serialized("Skip Tags", typeof(StringVector), DefaultSource = nameof(GetSkipTagsDefault),
        Setter = nameof(SetSkipTags))]
    private object? GetSkipTags() => new StringVector(_defaultParams.ImporterSettings.SkipTags);

    private void SetSkipTags(object? value)
    {
        _defaultParams.ImporterSettings.SkipTags = value is StringVector tags ? [.. tags] : [];
    }

    private static object? GetKeepNamesOnMergeDefault() =>
        new TransformerParams().ImporterSettings.KeepNamesOnMerge;

    [Serialized("Keep Names On Merge", DefaultSource = nameof(GetKeepNamesOnMergeDefault))]
    private bool KeepNamesOnMerge
    {
        get => _defaultParams.ImporterSettings.KeepNamesOnMerge;
        set => _defaultParams.ImporterSettings.KeepNamesOnMerge = value;
    }

    private static object? GetAddEmptyNodesToSkeletonDefault() =>
        new TransformerParams().ImporterSettings.AddEmptyNodesToSkeleton;

    [Serialized("Add Empty Nodes To Skeleton", DefaultSource = nameof(GetAddEmptyNodesToSkeletonDefault))]
    private bool AddEmptyNodesToSkeleton
    {
        get => _defaultParams.ImporterSettings.AddEmptyNodesToSkeleton;
        set => _defaultParams.ImporterSettings.AddEmptyNodesToSkeleton = value;
    }

    private static object? GetRepairLoopingDefault() => new TransformerParams().RepairLooping;

    [Serialized("Repair Looping", DefaultSource = nameof(GetRepairLoopingDefault))]
    private bool RepairLooping
    {
        get => _defaultParams.RepairLooping;
        set => _defaultParams.RepairLooping = value;
    }

    private static object? GetArtificialSkinNodesDefault() => new StringVector();

    [Serialized("Artificial Skin Nodes", typeof(StringVector), DefaultSource = nameof(GetArtificialSkinNodesDefault),
        Setter = nameof(SetArtificialSkinNodes))]
    private object? GetSerializedArtificialSkinNodes() => new StringVector(_defaultParams.ArtificialSkinNodes);

    private void SetArtificialSkinNodes(object? value)
    {
        _defaultParams.ArtificialSkinNodes = value is StringVector nodes ? [.. nodes] : [];
    }

    private static object? GetBlenderApplyModifiersDefault() => new TransformerParams().BlenderApplyModifiers;

    [Serialized("Blender: Apply Modifiers", DefaultSource = nameof(GetBlenderApplyModifiersDefault))]
    private bool BlenderApplyModifiers
    {
        get => _defaultParams.BlenderApplyModifiers;
        set => _defaultParams.BlenderApplyModifiers = value;
    }

    private static object? GetBlenderDeformingBonesOnlyDefault() =>
        new TransformerParams().BlenderDeformingBonesOnly;

    [Serialized("Blender: Deforming Bones Only", DefaultSource = nameof(GetBlenderDeformingBonesOnlyDefault))]
    private bool BlenderDeformingBonesOnly
    {
        get => _defaultParams.BlenderDeformingBonesOnly;
        set => _defaultParams.BlenderDeformingBonesOnly = value;
    }

    private static object? GetLightmapUVGenerateDefault() => new TransformerParams().LightmapUVGenerate;

    // Compatibility shim for asset pipelines created before Glow became an external module.
    [Serialized("LightMap UV: Generate", DefaultSource = nameof(GetLightmapUVGenerateDefault))]
    private bool LightmapUVGenerate
    {
        get => _defaultParams.LightmapUVGenerate;
        set => _defaultParams.LightmapUVGenerate = value;
    }

    private static object? GetLightmapUVTexelsPerUnitDefault() =>
        new TransformerParams().LightmapUVTexelsPerUnit;

    // Compatibility shim for asset pipelines created before Glow became an external module.
    [Serialized("LightMap UV: Texels per Unit", DefaultSource = nameof(GetLightmapUVTexelsPerUnitDefault))]
    private float LightmapUVTexelsPerUnit
    {
        get => _defaultParams.LightmapUVTexelsPerUnit;
        set => _defaultParams.LightmapUVTexelsPerUnit = value;
    }

    private static object? GetLightmapUVChannelDefault() => new TransformerParams().LightmapUVChannel;

    // Compatibility shim for asset pipelines created before Glow became an external module.
    [Serialized("LightMap UV: Channel", DefaultSource = nameof(GetLightmapUVChannelDefault))]
    private uint LightmapUVChannel
    {
        get => _defaultParams.LightmapUVChannel;
        set => _defaultParams.LightmapUVChannel = value;
    }
}
