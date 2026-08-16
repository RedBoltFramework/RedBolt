// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Utility;

using System;
using System.Collections.Generic;

using Microsoft.Extensions.Logging;

using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.IO;
using RedBolt.Logging;
using RedBolt.Math;
using RedBolt.Resource;
using RedBolt.Scene;

public class GenerateWorldSpaceTracksParams : IArchiveSerializable, ICloneable
{
    public bool FillRotations = true;
    public bool DeltaRotation = true;
    public float SampleRate;

    public HashSet<string> Bones = new(StringComparer.Ordinal);
    public Dictionary<string, Vector3f> BendTargetOffsets = new(StringComparer.Ordinal);

    public void SerializeInBlock(Archive archive)
    {
        var defaults = new GenerateWorldSpaceTracksParams();

        ArchiveSerialization.SerializeOptionalValue(
            archive, "fillRotations", ref FillRotations, defaults.FillRotations,
            ArchiveSerialization.SerializeValue);
        ArchiveSerialization.SerializeOptionalValue(
            archive, "deltaRotation", ref DeltaRotation, defaults.DeltaRotation,
            ArchiveSerialization.SerializeValue);
        ArchiveSerialization.SerializeOptionalValue(
            archive, "sampleRate", ref SampleRate, defaults.SampleRate,
            ArchiveSerialization.SerializeValue);

        SerializeOptionalSet(archive, "bones", Bones);
        SerializeOptionalMap(archive, "bendTargetOffsets", BendTargetOffsets);
    }

    public GenerateWorldSpaceTracksParams Clone()
    {
        return new GenerateWorldSpaceTracksParams
        {
            FillRotations = FillRotations,
            DeltaRotation = DeltaRotation,
            SampleRate = SampleRate,
            Bones = new HashSet<string>(Bones, Bones.Comparer),
            BendTargetOffsets = new Dictionary<string, Vector3f>(BendTargetOffsets, BendTargetOffsets.Comparer),
        };
    }

    object ICloneable.Clone() => Clone();

    private static void SerializeOptionalSet(Archive archive, string name, HashSet<string> values)
    {
        var serialize = !archive.IsUnorderedAccessSupportedInCurrentBlock
            || (archive.IsInput ? archive.HasElementOrBlock(name) : values.Count != 0);
        if (serialize)
            ArchiveSerialization.SerializeSet(archive, name, values, ArchiveSerialization.SerializeValue);
        else if (archive.IsInput)
            values.Clear();
    }

    private static void SerializeOptionalMap(
        Archive archive, string name, Dictionary<string, Vector3f> values)
    {
        var serialize = !archive.IsUnorderedAccessSupportedInCurrentBlock
            || (archive.IsInput ? archive.HasElementOrBlock(name) : values.Count != 0);
        if (serialize)
        {
            ArchiveSerialization.SerializeMap(
                archive, name, values, ArchiveSerialization.SerializeValue, ArchiveSerialization.SerializeValue);
        }
        else if (archive.IsInput)
            values.Clear();
    }
}

/// <summary>Single task for GenerateWorldSpaceTracksTransformer.</summary>
public class GenerateWorldSpaceTracksTask : ICloneable
{
    public Model? Model;
    public Animation? SourceAnimation;
    public Animation? TargetAnimation;
    public GenerateWorldSpaceTracksParams Params = new();

    public GenerateWorldSpaceTracksTask Clone()
    {
        return new GenerateWorldSpaceTracksTask
        {
            Model = Model,
            SourceAnimation = SourceAnimation,
            TargetAnimation = TargetAnimation,
            Params = Params.Clone(),
        };
    }

    object ICloneable.Clone() => Clone();
}

/// <summary>Asset transformer that generates world-space tracks for animation. Useful for IK animation.</summary>
[Reflected(ObjectCategory.Category_Transformer)]
public partial class GenerateWorldSpaceTracksTransformer : BaseAssetPostTransformer
{
    public GenerateWorldSpaceTracksTransformer()
    {
    }

    public override void Dispose()
    {
        base.Dispose();
    }

    public void GenerateTracks(GenerateWorldSpaceTracksTask task)
    {
        using var scene = new Scene();
        scene.CreateComponent<Octree>();
        var node = scene.CreateChild();

        var animatedModel = node.CreateComponent<AnimatedModel>();
        animatedModel.SetModel(task.Model);
        animatedModel.ApplyAnimation();

        var tracks = GetTracks(animatedModel, task.Params);
        tracks.AddRange(GetBendTracks(animatedModel, task.Params));

        var animationController = node.CreateComponent<AnimationController>();
        animationController.Update(0.0f);
        animationController.PlayNew(new AnimationParameters(task.SourceAnimation).Looped());

        var animationLength = task.SourceAnimation!.Length;
        var animationFrameRate = task.SourceAnimation.GetMetadata("FrameRate").GetFloat();

        var sampleRate = task.Params.SampleRate != 0
            ? task.Params.SampleRate
            : animationFrameRate != 0.0f
                ? animationFrameRate
                : 30.0f;

        var numFramesEstimate = animationLength * sampleRate;
        var numFrames = (int)MathF.Ceiling(numFramesEstimate - MathDefs.LargeEpsilon);
        for (var i = 0; i < numFrames; ++i)
        {
            var frameTime = MathF.Min(i / sampleRate, animationLength);
            animationController.UpdateAnimationTime(task.SourceAnimation, frameTime);
            animationController.Update(0.0f);
            animatedModel.ApplyAnimation();

            foreach (var track in tracks)
            {
                track.Node.TryGetTarget(out var trackNode);
                var frame = new AnimationKeyFrame(frameTime, trackNode!.GetWorldPosition());
                if ((track.Track.ChannelMask & AnimationChannel.Rotation) != 0)
                {
                    frame.Rotation = trackNode.GetWorldRotation() * track.RotationOffset.Inverse;
                }
                track.Track.AddKeyFrame(frame);
            }
        }

        foreach (var track in tracks)
        {
            task.TargetAnimation!.RemoveTrack(track.Track.Name);
            var destTrack = task.TargetAnimation.CreateTrack(track.Track.Name);

            destTrack.ChannelMask = track.Track.ChannelMask;
            destTrack.KeyFrames.AddRange(track.Track.KeyFrames);
        }
    }

    /// <summary>Implement BaseAssetPostTransformer.</summary>
    public override string GetParametersFileName() => "GenerateWorldSpaceTracks.json";

    /// <summary>Implement BaseAssetPostTransformer.</summary>
    public override bool Execute(AssetTransformerInput input, AssetTransformerOutput output,
        IReadOnlyList<AssetTransformer> transformers)
    {
        var cache = GetSubsystem<ResourceCache>();

        var parameters = LoadParameters<TransformerParams>(input.InputFileName);
        var baseResourceName = GetPath(input.ResourceName);

        var taskDescriptions = new List<TaskDescription>();
        foreach (var task in parameters.Tasks)
            taskDescriptions.Add(task.Clone());
        foreach (var taskTemplate in parameters.TaskTemplates)
        {
            var matches = GetResourcesByPattern(baseResourceName, taskTemplate.SourceAnimation);
            foreach (var match in matches)
            {
                var task = taskTemplate.Clone();
                taskDescriptions.Add(task);
                task.SourceAnimation = match.FileName;
                task.TargetAnimation = GetMatchFileName(taskTemplate.TargetAnimation, match);
            }
        }

        var tasks = new List<GenerateWorldSpaceTracksTask>();
        foreach (var taskDescription in taskDescriptions)
        {
            var generateTask = new GenerateWorldSpaceTracksTask
            {
                Model = cache.GetTempResource<Model>(baseResourceName + taskDescription.Model),
                SourceAnimation = cache.GetTempResource<Animation>(baseResourceName + taskDescription.SourceAnimation),
                Params = taskDescription.CloneParameters(),
            };

            if (taskDescription.TargetAnimation == taskDescription.SourceAnimation)
                generateTask.TargetAnimation = generateTask.SourceAnimation;
            else
            {
                var targetResourceName = baseResourceName + taskDescription.TargetAnimation;
                var targetAnimation = generateTask.SourceAnimation!.Clone(targetResourceName);
                targetAnimation.SetAbsoluteFileName(string.Empty);
                targetAnimation.RemoveAllTracks();
                generateTask.TargetAnimation = targetAnimation;
            }

            if (generateTask.Model is null)
            {
                Log.GetLogger().LogError("Base model '{Model}' is not found", taskDescription.Model);
                continue;
            }
            if (generateTask.SourceAnimation is null)
            {
                Log.GetLogger().LogError(
                    "Source animation '{SourceAnimation}' is not found", taskDescription.SourceAnimation);
                continue;
            }

            tasks.Add(generateTask);
        }

        foreach (var task in tasks)
        {
            GenerateTracks(task);
            if (string.IsNullOrEmpty(task.TargetAnimation!.GetAbsoluteFileName()))
            {
                task.TargetAnimation.SaveFile(
                    FileIdentifier.FromUri(input.TempPath + task.TargetAnimation.GetName()));
            }
            else
            {
                task.TargetAnimation.SaveFile(
                    FileIdentifier.FromUri(task.TargetAnimation.GetAbsoluteFileName()));
                output.ModifiedResourceNames.Add(task.TargetAnimation.GetName());
            }
        }

        return true;
    }

    private class TaskDescription : GenerateWorldSpaceTracksParams
    {
        public string Model = string.Empty;
        public string SourceAnimation = string.Empty;
        public string TargetAnimation = string.Empty;

        public new TaskDescription Clone()
        {
            return new TaskDescription
            {
                FillRotations = FillRotations,
                DeltaRotation = DeltaRotation,
                SampleRate = SampleRate,
                Bones = new HashSet<string>(Bones, Bones.Comparer),
                BendTargetOffsets =
                    new Dictionary<string, Vector3f>(BendTargetOffsets, BendTargetOffsets.Comparer),
                Model = Model,
                SourceAnimation = SourceAnimation,
                TargetAnimation = TargetAnimation,
            };
        }

        public GenerateWorldSpaceTracksParams CloneParameters()
        {
            return new GenerateWorldSpaceTracksParams
            {
                FillRotations = FillRotations,
                DeltaRotation = DeltaRotation,
                SampleRate = SampleRate,
                Bones = new HashSet<string>(Bones, Bones.Comparer),
                BendTargetOffsets =
                    new Dictionary<string, Vector3f>(BendTargetOffsets, BendTargetOffsets.Comparer),
            };
        }

        public new void SerializeInBlock(Archive archive)
        {
            base.SerializeInBlock(archive);

            ArchiveSerialization.SerializeOptionalValue(
                archive, "model", ref Model, string.Empty, ArchiveSerialization.SerializeValue);
            ArchiveSerialization.SerializeOptionalValue(
                archive, "sourceAnimation", ref SourceAnimation, string.Empty, ArchiveSerialization.SerializeValue);
            ArchiveSerialization.SerializeOptionalValue(
                archive, "targetAnimation", ref TargetAnimation, string.Empty, ArchiveSerialization.SerializeValue);
        }
    }

    private class TransformerParams : IArchiveSerializable
    {
        public List<TaskDescription> Tasks = [];
        public List<TaskDescription> TaskTemplates = [];

        public void SerializeInBlock(Archive archive)
        {
            SerializeOptionalVector(archive, "tasks", Tasks);
            SerializeOptionalVector(archive, "taskTemplates", TaskTemplates);
        }

        private static void SerializeOptionalVector(
            Archive archive, string name, List<TaskDescription> values)
        {
            var serialize = !archive.IsUnorderedAccessSupportedInCurrentBlock
                || (archive.IsInput ? archive.HasElementOrBlock(name) : values.Count != 0);
            if (serialize)
                ArchiveSerialization.SerializeVector(archive, name, values, SerializeTaskDescription);
            else if (archive.IsInput)
                values.Clear();
        }

        private static void SerializeTaskDescription(
            Archive archive, string name, ref TaskDescription value)
        {
            value ??= new TaskDescription();
            using var block = archive.OpenUnorderedBlock(name);
            value.SerializeInBlock(archive);
        }
    }

    private class ExtractedTrack
    {
        public WeakReference<Node> Node = null!;
        public AnimationTrack Track = new();
        public Quaternion RotationOffset;
    }

    private static string GetGeneratedTrackName(string boneName, bool isBendTarget)
    {
        return boneName + (isBendTarget ? "_BendTarget" : "_Target");
    }

    private static bool IsBoneIgnored(string boneName, HashSet<string> filter)
    {
        if (string.IsNullOrEmpty(boneName) || (filter.Count != 0 && !filter.Contains(boneName)))
            return true;

        return boneName.EndsWith("_BendTarget", StringComparison.Ordinal)
            || boneName.EndsWith("_Target", StringComparison.Ordinal);
    }

    private static List<ExtractedTrack> GetTracks(
        AnimatedModel animatedModel, GenerateWorldSpaceTracksParams parameters)
    {
        var skeleton = animatedModel.GetSkeleton();
        var numBones = skeleton.GetNumBones();

        var tracks = new List<ExtractedTrack>();
        for (var i = 0; i < numBones; ++i)
        {
            var bone = skeleton.GetBones()[i];
            if (bone.Node is null || IsBoneIgnored(bone.Name, parameters.Bones))
                continue;

            var entry = new ExtractedTrack
            {
                Node = new WeakReference<Node>(bone.Node),
                RotationOffset = parameters.DeltaRotation ? bone.Node.GetWorldRotation() : Quaternion.IDENTITY,
            };
            entry.Track.Name = GetGeneratedTrackName(bone.Name, false);
            entry.Track.ChannelMask = AnimationChannel.Position;
            if (parameters.FillRotations)
                entry.Track.ChannelMask |= AnimationChannel.Rotation;
            tracks.Add(entry);
        }
        return tracks;
    }

    private static List<ExtractedTrack> GetBendTracks(
        AnimatedModel animatedModel, GenerateWorldSpaceTracksParams parameters)
    {
        var skeleton = animatedModel.GetSkeleton();

        var tracks = new List<ExtractedTrack>();
        foreach (var (boneName, offset) in parameters.BendTargetOffsets)
        {
            var bone = skeleton.GetBone(boneName);
            if (bone?.Node is null)
            {
                Log.GetLogger().LogError("Bone '{BoneName}' is not found for bend track", boneName);
                continue;
            }

            var probeNode = bone.Node.CreateChild();
            probeNode.Translate(offset, TransformSpace.World);

            var entry = new ExtractedTrack
            {
                Node = new WeakReference<Node>(probeNode),
            };
            entry.Track.Name = GetGeneratedTrackName(bone.Name, true);
            entry.Track.ChannelMask = AnimationChannel.Position;
            tracks.Add(entry);
        }
        return tracks;
    }

    private static string GetPath(string fullPath)
    {
        var slash = Math.Max(fullPath.LastIndexOf('/'), fullPath.LastIndexOf('\\'));
        return slash >= 0 ? fullPath[..(slash + 1)] : string.Empty;
    }
}
