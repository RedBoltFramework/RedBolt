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

public class CalculateAnimationVelocityParams : IArchiveSerializable, ICloneable
{
    public List<string> TargetBones = [];
    public float GroundThreshold = 0.005f;
    public float VelocityThreshold = MathDefs.LargeEpsilon;
    public float SampleRate;

    public void SerializeInBlock(Archive archive)
    {
        var serializeTargetBones = !archive.IsUnorderedAccessSupportedInCurrentBlock
            || (archive.IsInput ? archive.HasElementOrBlock("targetBones") : TargetBones.Count != 0);
        if (serializeTargetBones)
            ArchiveSerialization.SerializeStringVector(archive, "targetBones", TargetBones);
        else if (archive.IsInput)
            TargetBones.Clear();

        var defaults = new CalculateAnimationVelocityParams();
        ArchiveSerialization.SerializeOptionalValue(
            archive, "groundThreshold", ref GroundThreshold, defaults.GroundThreshold,
            ArchiveSerialization.SerializeValue);
        ArchiveSerialization.SerializeOptionalValue(
            archive, "velocityThreshold", ref VelocityThreshold, defaults.VelocityThreshold,
            ArchiveSerialization.SerializeValue);
        ArchiveSerialization.SerializeOptionalValue(
            archive, "sampleRate", ref SampleRate, defaults.SampleRate,
            ArchiveSerialization.SerializeValue);
    }

    public CalculateAnimationVelocityParams Clone()
    {
        return new CalculateAnimationVelocityParams
        {
            TargetBones = [.. TargetBones],
            GroundThreshold = GroundThreshold,
            VelocityThreshold = VelocityThreshold,
            SampleRate = SampleRate,
        };
    }

    object ICloneable.Clone() => Clone();
}

/// <summary>Single task for CalculateAnimationVelocityTransformer.</summary>
public class CalculateAnimationVelocityTask : ICloneable
{
    public Model? Model;
    public Animation? Animation;
    public CalculateAnimationVelocityParams Params = new();

    public CalculateAnimationVelocityTask Clone()
    {
        return new CalculateAnimationVelocityTask
        {
            Model = Model,
            Animation = Animation,
            Params = Params.Clone(),
        };
    }

    object ICloneable.Clone() => Clone();
}

/// <summary>Asset transformer that extracts velocity from movement animations.</summary>
[Reflected(ObjectCategory.Category_Transformer)]
public partial class CalculateAnimationVelocityTransformer : BaseAssetPostTransformer
{
    public const float DefaultThreshold = 0.005f;

    public CalculateAnimationVelocityTransformer()
    {
    }

    public override void Dispose()
    {
        base.Dispose();
    }

    public Vector3f? CalculateVelocity(CalculateAnimationVelocityTask task)
    {
        // Don't run this transformer on animations that are too short.
        if (task.Animation!.Length < MathDefs.LargeEpsilon)
            return null;

        var trackSet = ExtractAnimationTracks(task);

        var velocities = new List<Vector3f>();
        foreach (var track in trackSet.Tracks)
        {
            var velocity = EvaluateVelocity(track, trackSet.SampleRate, task.Params);
            if (velocity.HasValue)
                velocities.Add(velocity.Value);
        }

        if (velocities.Count == 0)
            return null;

        var averageVelocity = Vector3f.Zero;
        foreach (var velocity in velocities)
            averageVelocity += velocity;
        averageVelocity /= velocities.Count;

        if (averageVelocity.Length > task.Params.VelocityThreshold)
            return averageVelocity;
        else
            return Vector3f.Zero;
    }

    /// <summary>Implement BaseAssetPostTransformer.</summary>
    public override string GetParametersFileName() => "CalculateAnimationVelocity.json";

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
            var matches = GetResourcesByPattern(baseResourceName, taskTemplate.Animation);
            foreach (var match in matches)
            {
                var task = taskTemplate.Clone();
                taskDescriptions.Add(task);
                task.Animation = match.FileName;
            }
        }

        var tasks = new List<CalculateAnimationVelocityTask>();
        foreach (var taskDescription in taskDescriptions)
        {
            var generateTask = new CalculateAnimationVelocityTask
            {
                Model = cache.GetTempResource<Model>(baseResourceName + taskDescription.Model),
                Animation = cache.GetTempResource<Animation>(baseResourceName + taskDescription.Animation),
                Params = taskDescription.CloneParameters(),
            };

            if (generateTask.Model is null)
            {
                Log.GetLogger().LogError("Base model '{Model}' is not found", taskDescription.Model);
                continue;
            }
            if (generateTask.Animation is null)
            {
                Log.GetLogger().LogError("Animation '{Animation}' is not found", taskDescription.Animation);
                continue;
            }

            tasks.Add(generateTask);
        }

        foreach (var task in tasks)
        {
            var velocity = CalculateVelocity(task);
            if (velocity.HasValue)
            {
                using var metadata = new Variant(velocity.Value);
                task.Animation!.AddMetadata("Velocity", metadata);
                task.Animation.SaveFile(FileIdentifier.FromUri(task.Animation.GetAbsoluteFileName()));
                output.ModifiedResourceNames.Add(task.Animation.GetName());
            }
            else
                Log.GetLogger().LogDebug("Cannot calculate velocity for animation '{Animation}'", task.Animation!.GetName());
        }

        return true;
    }

    private class TaskDescription : CalculateAnimationVelocityParams
    {
        public string Model = string.Empty;
        public string Animation = string.Empty;

        public new TaskDescription Clone()
        {
            return new TaskDescription
            {
                TargetBones = [.. TargetBones],
                GroundThreshold = GroundThreshold,
                VelocityThreshold = VelocityThreshold,
                SampleRate = SampleRate,
                Model = Model,
                Animation = Animation,
            };
        }

        public CalculateAnimationVelocityParams CloneParameters()
        {
            return new CalculateAnimationVelocityParams
            {
                TargetBones = [.. TargetBones],
                GroundThreshold = GroundThreshold,
                VelocityThreshold = VelocityThreshold,
                SampleRate = SampleRate,
            };
        }

        public new void SerializeInBlock(Archive archive)
        {
            base.SerializeInBlock(archive);

            ArchiveSerialization.SerializeOptionalValue(
                archive, "model", ref Model, string.Empty, ArchiveSerialization.SerializeValue);
            ArchiveSerialization.SerializeOptionalValue(
                archive, "animation", ref Animation, string.Empty, ArchiveSerialization.SerializeValue);
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

    private class ExtractedTrackSet
    {
        public readonly List<List<Vector3f>> Tracks = [];
        public float SampleRate;
    }

    private static ExtractedTrackSet ExtractAnimationTracks(CalculateAnimationVelocityTask task)
    {
        using var scene = new Scene();
        scene.CreateComponent<Octree>();
        var node = scene.CreateChild();

        var animatedModel = node.CreateComponent<AnimatedModel>();
        animatedModel.SetModel(task.Model);
        animatedModel.ApplyAnimation();

        var targetNodes = new List<Node>();
        foreach (var boneName in task.Params.TargetBones)
        {
            var boneNode = node.GetChild(boneName, true);
            if (boneNode is not null)
                targetNodes.Add(boneNode);
        }
        var result = new ExtractedTrackSet();
        foreach (var targetNode in targetNodes)
            result.Tracks.Add([]);

        var animationController = node.CreateComponent<AnimationController>();
        animationController.Update(0.0f);
        animationController.PlayNew(new AnimationParameters(task.Animation).Looped());

        var animationLength = task.Animation!.Length;
        var animationFrameRate = task.Animation.GetMetadata("FrameRate").GetFloat();

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
            animationController.UpdateAnimationTime(task.Animation, frameTime);
            animationController.Update(0.0f);
            animatedModel.ApplyAnimation();

            for (var trackIndex = 0; trackIndex < targetNodes.Count; ++trackIndex)
            {
                var targetNode = targetNodes[trackIndex];
                var track = result.Tracks[trackIndex];
                track.Add(targetNode.GetWorldPosition());
            }
        }

        result.SampleRate = sampleRate;
        return result;
    }

    private static Vector3f? EvaluateVelocity(
        IReadOnlyList<Vector3f> track, float sampleRate, CalculateAnimationVelocityParams parameters)
    {
        if (track.Count <= 3)
            return null;

        // Find range of Y values that are considered to be on the ground.
        var minY = track[0].Y;
        var maxY = track[0].Y;
        for (var i = 1; i < track.Count; ++i)
        {
            minY = MathF.Min(minY, track[i].Y);
            maxY = MathF.Max(maxY, track[i].Y);
        }
        var groundRangeY = new FloatRange(minY, MathF.Min(minY + parameters.GroundThreshold, maxY));

        // Find the frames that are on the ground.
        var isGrounded = new bool[track.Count];
        for (var i = 0; i < track.Count; ++i)
            isGrounded[i] = groundRangeY.ContainsInclusive(track[i].Y);

        // Find groups of consecutive frames that are on the ground.
        var groups = new List<UintRange>();
        var groupIndices = new uint?[track.Count];
        for (var i = 0; i < track.Count; ++i)
        {
            if (!isGrounded[i])
                continue;

            if (i == 0 || !isGrounded[i - 1])
                groups.Add(new UintRange(checked((uint)i), checked((uint)i)));

            groupIndices[i] = checked((uint)groups.Count - 1);
            var group = groups[^1];
            group.Second = checked((uint)i);
            groups[^1] = group;
        }
        if (groups.Count == 0)
            return null;

        // Since animation is considered looped, merge the first and last groups if they are on the ground.
        if (groups.Count > 1 && isGrounded[0] && isGrounded[^1])
        {
            var lastGroupIndex = checked((uint)groups.Count - 1);
            for (var i = 0; i < groupIndices.Length; ++i)
            {
                if (groupIndices[i] == lastGroupIndex)
                    groupIndices[i] = 0;
            }

            var firstGroup = groups[0];
            firstGroup.First = groups[^1].First;
            groups[0] = firstGroup;
            groups.RemoveAt(groups.Count - 1);
        }

        // Find the longest group of frames that are on the ground.
        var groupLengths = new uint[groups.Count];
        for (var i = 0; i < track.Count; ++i)
        {
            var groupIndex = groupIndices[i];
            if (groupIndex.HasValue)
                ++groupLengths[groupIndex.Value];
        }
        var longestGroupIndex = 0;
        for (var i = 1; i < groupLengths.Length; ++i)
        {
            if (groupLengths[i] > groupLengths[longestGroupIndex])
                longestGroupIndex = i;
        }
        var longestGroupLength = groupLengths[longestGroupIndex];
        if (longestGroupLength < 2)
            return null;

        // Find the delta position
        var firstGroundPosition = track[(int)groups[longestGroupIndex].First];
        var lastGroundPosition = track[(int)groups[longestGroupIndex].Second];
        var groundTime = (longestGroupLength - 1) / sampleRate;

        // Tracks are relative to the moving object, while we need velocity relative to the ground. Invert result.
        return -(lastGroundPosition - firstGroundPosition) / groundTime;
    }

    private static string GetPath(string fullPath)
    {
        var slash = Math.Max(fullPath.LastIndexOf('/'), fullPath.LastIndexOf('\\'));
        return slash >= 0 ? fullPath[..(slash + 1)] : string.Empty;
    }
}
