// Copyright (c) 2025-2025 the rbfx project.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Utility;

using System;
using System.Collections.Generic;
using System.Diagnostics;

using Microsoft.Extensions.Logging;

using RedBolt.Container;
using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.IK;
using RedBolt.IO;
using RedBolt.Logging;
using RedBolt.Math;
using RedBolt.Resource;
using RedBolt.Scene;

/// <summary>Single task for RetargetAnimationsTransformer.</summary>
public class RetargetAnimationTask : ICloneable
{
    public Model? SourceModel;
    public Animation? SourceAnimation;
    public Model? TargetModel;
    public Animation? TargetInitialPose;
    public string TargetAnimationName = string.Empty;
    public Dictionary<string, string> SourceToTargetBones = new(StringComparer.Ordinal);
    public Dictionary<string, string> TargetToSourceBones = new(StringComparer.Ordinal);
    public List<List<string>> IKChains = [];

    public RetargetAnimationTask Clone()
    {
        return new RetargetAnimationTask
        {
            SourceModel = SourceModel,
            SourceAnimation = SourceAnimation,
            TargetModel = TargetModel,
            TargetInitialPose = TargetInitialPose,
            TargetAnimationName = TargetAnimationName,
            SourceToTargetBones = new Dictionary<string, string>(
                SourceToTargetBones, SourceToTargetBones.Comparer),
            TargetToSourceBones = new Dictionary<string, string>(
                TargetToSourceBones, TargetToSourceBones.Comparer),
            IKChains = CloneIKChains(IKChains),
        };
    }

    object ICloneable.Clone() => Clone();

    private static List<List<string>> CloneIKChains(List<List<string>> chains)
    {
        var result = new List<List<string>>(chains.Count);
        foreach (var chain in chains)
            result.Add([.. chain]);
        return result;
    }
}

/// <summary>Asset transformer that re-targets animation from one model to another.</summary>
[Reflected(ObjectCategory.Category_Transformer)]
public partial class RetargetAnimationsTransformer : BaseAssetPostTransformer
{
    public RetargetAnimationsTransformer()
    {
    }

    public Animation? RetargetAnimation(RetargetAnimationTask task)
    {
        if (task.SourceAnimation is null || task.SourceModel is null || task.TargetModel is null)
        {
            Log.GetLogger().LogError("Input resources are not available during retargeting");
            return null;
        }

        var targetAnimation = new Animation();
        targetAnimation.SetLength(task.SourceAnimation.Length);
        targetAnimation.SetName(task.TargetAnimationName);
        targetAnimation.SetAnimationName(task.SourceAnimation.AnimationName);
        targetAnimation.CopyMetadata(task.SourceAnimation);

        var disposeTargetAnimation = true;
        try
        {
            // Prepare scene and models
            using var scene = new Scene();
            scene.CreateComponent<Octree>();

            var sourceNode = scene.CreateChild("Source");
            var targetNode = scene.CreateChild("Target");

            var sourceAnimatedModel = sourceNode.CreateComponent<AnimatedModel>();
            var targetAnimatedModel = targetNode.CreateComponent<AnimatedModel>();
            sourceAnimatedModel.SetModel(task.SourceModel);
            targetAnimatedModel.SetModel(task.TargetModel);

            if (task.TargetInitialPose is not null)
            {
                // Override initial transform if requested
                foreach (var bone in targetAnimatedModel.GetSkeleton().GetModifiableBones())
                {
                    var track = task.TargetInitialPose.GetTrack(bone.NameHash);
                    if (track is not null && track.KeyFrames.Count != 0)
                    {
                        var keyFrame = track.KeyFrames[0];
                        if ((track.ChannelMask & AnimationChannel.Position) != 0)
                            bone.InitialPosition = keyFrame.Position;
                        if ((track.ChannelMask & AnimationChannel.Rotation) != 0)
                            bone.InitialRotation = keyFrame.Rotation;
                        if ((track.ChannelMask & AnimationChannel.Scale) != 0)
                            bone.InitialScale = keyFrame.Scale;
                    }
                }
            }

            var sourceController = sourceNode.CreateComponent<AnimationController>();
            sourceController.PlayNew(new AnimationParameters(task.SourceAnimation));
            sourceController.SetSkeletonReset(true);
            sourceController.Update(0.0f);
            sourceAnimatedModel.ApplyAnimation();

            var targetController = targetNode.CreateComponent<AnimationController>();
            targetController.PlayNew(new AnimationParameters(targetAnimation));
            targetController.SetSkeletonReset(true);
            targetController.Update(0.0f);
            targetAnimatedModel.ApplyAnimation();

            // Deduce total scale from root bone
            var sourceBaseScale = GetBaseScale(sourceAnimatedModel.GetSkeleton());
            var targetBaseScale = GetBaseScale(targetAnimatedModel.GetSkeleton());
            var positionScale = sourceBaseScale > MathDefs.LargeEpsilon && targetBaseScale > MathDefs.LargeEpsilon
                ? targetBaseScale / sourceBaseScale
                : 0.0f;

            // Retarget bones one by one
            foreach (var targetBoneIndex in targetAnimatedModel.GetSkeleton().GetBonesOrder())
            {
                var targetBone = targetAnimatedModel.GetSkeleton().GetBones()[targetBoneIndex];
                var sourceBoneName = GetMappedBoneName(targetBone.Name, task.TargetToSourceBones);
                if (sourceBoneName is null)
                    continue;

                var sourceBone = sourceAnimatedModel.GetSkeleton().GetBone(sourceBoneName);
                if (sourceBone is null)
                    continue;

                var sourceTrack = task.SourceAnimation.GetTrack(sourceBone.Name);
                if (sourceTrack is null)
                    continue;

                Debug.Assert(targetBone.Node is not null);
                Debug.Assert(sourceBone.Node is not null);

                var hasPosition = (sourceTrack.ChannelMask & AnimationChannel.Position) != 0;
                var hasRotation = (sourceTrack.ChannelMask & AnimationChannel.Rotation) != 0;
                var hasScale = (sourceTrack.ChannelMask & AnimationChannel.Scale) != 0;
                var isRootBone = targetBone.ParentIndex == targetBoneIndex;

                var targetKeyFrames = new List<AnimationKeyFrame>();
                foreach (var sourceKeyFrame in sourceTrack.KeyFrames)
                {
                    sourceController.UpdateAnimationTime(task.SourceAnimation, sourceKeyFrame.Time);
                    sourceController.Update(0.0f);
                    sourceAnimatedModel.ApplyAnimation();

                    targetController.UpdateAnimationTime(targetAnimation, sourceKeyFrame.Time);
                    targetController.Update(0.0f);
                    targetAnimatedModel.ApplyAnimation();

                    var targetKeyFrame = new AnimationKeyFrame
                    {
                        Time = sourceKeyFrame.Time,
                    };

                    var sourceParentWorld = sourceBone.Node!.GetParent()!.GetWorldTransform();
                    var targetParentWorld = targetBone.Node!.GetParent()!.GetWorldTransform();
                    var parentWorldDelta = sourceParentWorld.Inverse * targetParentWorld;

                    var sourceBindLocal = new Matrix3x4(
                        sourceBone.InitialPosition, sourceBone.InitialRotation, sourceBone.InitialScale);
                    var sourceAnimationLocal = new Matrix3x4(
                        hasPosition ? sourceKeyFrame.Position : sourceBone.InitialPosition,
                        hasRotation ? sourceKeyFrame.Rotation : sourceBone.InitialRotation,
                        hasScale ? sourceKeyFrame.Scale : sourceBone.InitialScale);
                    var sourceLocalDelta = sourceAnimationLocal * sourceBindLocal.Inverse;

                    var targetBindLocal = new Matrix3x4(
                        targetBone.InitialPosition, targetBone.InitialRotation, targetBone.InitialScale);

                    var targetAnimationLocal =
                        parentWorldDelta.Inverse * sourceLocalDelta * parentWorldDelta * targetBindLocal;

                    var targetAnimationLocalTransform = Transform.FromMatrix3x4(targetAnimationLocal);

                    // Scale position for root bones, discard position for other bones
                    targetKeyFrame.Position = isRootBone
                        ? sourceKeyFrame.Position * positionScale
                        : targetBone.InitialPosition;

                    // Rotation is properly re-targeted
                    targetKeyFrame.Rotation = targetAnimationLocalTransform.Rotation;

                    // Scale is never re-targeted
                    targetKeyFrame.Scale = sourceKeyFrame.Scale;
                    targetKeyFrames.Add(targetKeyFrame);
                }

                // Create target track and populate keyframes
                var targetTrack = targetAnimation.CreateTrack(targetBone.Name);
                targetTrack.ChannelMask = sourceTrack.ChannelMask;
                targetTrack.KeyFrames.AddRange(targetKeyFrames);
            }

            // Resolve IK chains to stabilize animations
            if (task.IKChains.Count != 0)
            {
                var ikSolver = targetNode.CreateComponent<IKSolver>();
                ikSolver.SolveFromOriginal = false;

                var frameRate = targetAnimation.GetMetadata(AnimationMetadata.FrameRate).GetFloat();
                var numChains = task.IKChains.Count;

                var chains = new List<IKChainData>(numChains);
                try
                {
                    for (var i = 0; i < numChains; ++i)
                    {
                        var boneNames = task.IKChains[i];
                        if (boneNames.Count < 3)
                        {
                            Log.GetLogger().LogError("IK chain should have at least 3 bones");
                            return null;
                        }

                        var chainData = new IKChainData();
                        chains.Add(chainData);

                        var chainTargetName = $"__RetargetAnimation_IK_Target_{i}__";
                        var chainComponent = targetNode.CreateComponent<IKChainSolver>();
                        var numBones = boneNames.Count;
                        var sourceEffectorBone = sourceAnimatedModel.GetSkeleton().GetBone(boneNames[^1]);
                        if (sourceEffectorBone is null)
                        {
                            Log.GetLogger().LogError(
                                "IK effector bone '{BoneName}' is not found in source skeleton", boneNames[^1]);
                            return null;
                        }

                        chainData.SolverComponent = chainComponent;
                        chainData.TargetNode = targetNode.CreateChild(chainTargetName);
                        chainData.EffectorNode = sourceEffectorBone.Node;

                        var boneNamesRemapped = new StringVector();
                        for (var j = 0; j < numBones; ++j)
                        {
                            var boneName = GetMappedBoneName(boneNames[j], task.SourceToTargetBones);
                            if (boneName is null)
                            {
                                Log.GetLogger().LogError(
                                    "Bone '{BoneName}' cannot be used in IK chain because is missing from target skeleton",
                                    boneNames[j]);
                                return null;
                            }

                            var bone = targetAnimatedModel.GetSkeleton().GetBone(boneName);
                            if (bone is null)
                            {
                                Log.GetLogger().LogError(
                                    "Bone '{SourceBoneName}' -> '{TargetBoneName}' is not found", boneNames[j], boneName);
                                return null;
                            }

                            chainData.Joints.Add((bone.Node, new AnimationTrack()));
                            boneNamesRemapped.Add(boneName);
                        }

                        chainComponent.TargetName = chainTargetName;
                        chainComponent.SetBoneNames(boneNamesRemapped);
                    }

                    // Rebuild solvers before animation
                    ikSolver.Solve(0.0f);

                    var numFrames = (int)MathF.Ceiling(
                        targetAnimation.Length * frameRate - MathDefs.LargeEpsilon);
                    for (var i = 0; i < numFrames; ++i)
                    {
                        var frameTime = MathF.Min(i / frameRate, targetAnimation.Length);

                        sourceController.UpdateAnimationTime(task.SourceAnimation, frameTime);
                        sourceController.Update(0.0f);
                        sourceAnimatedModel.ApplyAnimation();

                        foreach (var chainData in chains)
                        {
                            chainData.TargetNode!.SetWorldPosition(
                                chainData.EffectorNode!.GetWorldPosition() * positionScale);
                        }

                        targetController.UpdateAnimationTime(targetAnimation, frameTime);
                        targetController.Update(0.0f);
                        targetAnimatedModel.ApplyAnimation();
                        ikSolver.Solve(0.0f);

                        foreach (var chainData in chains)
                        {
                            foreach (var (jointNode, track) in chainData.Joints)
                            {
                                var keyFrame = new AnimationKeyFrame
                                {
                                    Time = frameTime,
                                    Rotation = jointNode!.GetRotation(),
                                };
                                track.AddKeyFrame(keyFrame);
                            }
                        }
                    }

                    // Replace tracks in target animation with IK-created rotation tracks (KISS)
                    foreach (var chainData in chains)
                    {
                        foreach (var (jointNode, ikTrack) in chainData.Joints)
                        {
                            var boneName = jointNode!.Name;
                            var targetTrack = targetAnimation.GetTrack(boneName)
                                ?? targetAnimation.CreateTrack(boneName);

                            // IK solver only provides rotation keyframes for now.
                            targetTrack.ChannelMask = AnimationChannel.Rotation;
                            targetTrack.KeyFrames.Clear();
                            targetTrack.KeyFrames.AddRange(ikTrack.KeyFrames);
                        }
                    }
                }
                finally
                {
                    foreach (var chainData in chains)
                    {
                        foreach (var (_, track) in chainData.Joints)
                            track.Dispose();
                    }
                }
            }

            disposeTargetAnimation = false;
            return targetAnimation;
        }
        finally
        {
            if (disposeTargetAnimation)
                targetAnimation.Dispose();
        }
    }

    /// <summary>Implement BaseAssetPostTransformer.</summary>
    public override string GetParametersFileName() => "RetargetAnimations.json";

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

        var tasks = new List<RetargetAnimationTask>();
        foreach (var taskDescription in taskDescriptions)
        {
            var retargetTask = new RetargetAnimationTask
            {
                SourceModel = cache.GetTempResource<Model>(baseResourceName + taskDescription.SourceModel),
                SourceAnimation = cache.GetTempResource<Animation>(baseResourceName + taskDescription.SourceAnimation),
                TargetModel = cache.GetTempResource<Model>(baseResourceName + taskDescription.TargetModel),
                TargetInitialPose =
                    cache.GetTempResource<Animation>(baseResourceName + taskDescription.TargetInitialPose),
                TargetAnimationName = baseResourceName + taskDescription.TargetAnimation,
                SourceToTargetBones = new Dictionary<string, string>(
                    taskDescription.BoneMapping, taskDescription.BoneMapping.Comparer),
                TargetToSourceBones = InvertMap(taskDescription.BoneMapping),
                IKChains = CloneIKChains(taskDescription.IKChains),
            };

            if (retargetTask.SourceModel is null)
            {
                Log.GetLogger().LogError("Source model '{SourceModel}' is not found", taskDescription.SourceModel);
                continue;
            }
            if (retargetTask.SourceAnimation is null)
            {
                Log.GetLogger().LogError(
                    "Source animation '{SourceAnimation}' is not found", taskDescription.SourceAnimation);
                continue;
            }
            if (retargetTask.TargetModel is null)
            {
                Log.GetLogger().LogError("Target model '{TargetModel}' is not found", taskDescription.TargetModel);
                continue;
            }
            if (retargetTask.TargetInitialPose is null && !string.IsNullOrEmpty(taskDescription.TargetInitialPose))
            {
                Log.GetLogger().LogError(
                    "Initial target pose animation '{TargetInitialPose}' is not found",
                    taskDescription.TargetInitialPose);
                continue;
            }

            tasks.Add(retargetTask.Clone());
        }

        foreach (var task in tasks)
        {
            using var targetAnimation = RetargetAnimation(task);
            if (targetAnimation is not null)
            {
                targetAnimation.SaveFile(
                    FileIdentifier.FromUri(input.TempPath + targetAnimation.GetName()));
            }
        }

        return true;
    }

    private class TaskDescription : IArchiveSerializable, ICloneable
    {
        public string SourceModel = string.Empty;
        public string SourceAnimation = string.Empty;
        public string TargetModel = string.Empty;
        public string TargetInitialPose = string.Empty;
        public string TargetAnimation = string.Empty;
        public Dictionary<string, string> BoneMapping = new(StringComparer.Ordinal);
        public List<List<string>> IKChains = [];

        public void SerializeInBlock(Archive archive)
        {
            ArchiveSerialization.SerializeOptionalValue(
                archive, "sourceModel", ref SourceModel, string.Empty, ArchiveSerialization.SerializeValue);
            ArchiveSerialization.SerializeOptionalValue(
                archive, "sourceAnimation", ref SourceAnimation, string.Empty, ArchiveSerialization.SerializeValue);
            ArchiveSerialization.SerializeOptionalValue(
                archive, "targetModel", ref TargetModel, string.Empty, ArchiveSerialization.SerializeValue);
            ArchiveSerialization.SerializeOptionalValue(
                archive, "targetInitialPose", ref TargetInitialPose, string.Empty, ArchiveSerialization.SerializeValue);
            ArchiveSerialization.SerializeOptionalValue(
                archive, "targetAnimation", ref TargetAnimation, string.Empty, ArchiveSerialization.SerializeValue);
            SerializeOptionalMap(archive, "boneMapping", BoneMapping);
            SerializeOptionalIKChains(archive, "ikChains", IKChains);
        }

        public TaskDescription Clone()
        {
            return new TaskDescription
            {
                SourceModel = SourceModel,
                SourceAnimation = SourceAnimation,
                TargetModel = TargetModel,
                TargetInitialPose = TargetInitialPose,
                TargetAnimation = TargetAnimation,
                BoneMapping = new Dictionary<string, string>(BoneMapping, BoneMapping.Comparer),
                IKChains = CloneIKChains(IKChains),
            };
        }

        object ICloneable.Clone() => Clone();
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
    }

    private class IKChainData
    {
        public IKChainSolver? SolverComponent;
        public Node? TargetNode;
        public Node? EffectorNode;
        public List<(Node? Node, AnimationTrack Track)> Joints = [];
    }

    private static Dictionary<string, string> InvertMap(Dictionary<string, string> original)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, value) in original)
            result[value] = key;
        return result;
    }

    private static string? GetMappedBoneName(string boneName, Dictionary<string, string> boneMapping)
    {
        if (boneMapping.Count == 0)
            return boneName;

        return boneMapping.TryGetValue(boneName, out var mappedBoneName) ? mappedBoneName : null;
    }

    private static float GetBaseScale(Skeleton skeleton)
    {
        var boneIndex = skeleton.GetBonesOrder()[0];
        return skeleton.GetBones()[boneIndex].InitialPosition.Length;
    }

    private static List<List<string>> CloneIKChains(List<List<string>> chains)
    {
        var result = new List<List<string>>(chains.Count);
        foreach (var chain in chains)
            result.Add([.. chain]);
        return result;
    }

    private static void SerializeOptionalMap(
        Archive archive, string name, Dictionary<string, string> values)
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

    private static void SerializeOptionalIKChains(
        Archive archive, string name, List<List<string>> values)
    {
        var serialize = !archive.IsUnorderedAccessSupportedInCurrentBlock
            || (archive.IsInput ? archive.HasElementOrBlock(name) : values.Count != 0);
        if (serialize)
            ArchiveSerialization.SerializeVector(archive, name, values, SerializeStringVector);
        else if (archive.IsInput)
            values.Clear();
    }

    private static void SerializeStringVector(
        Archive archive, string name, ref List<string> value)
    {
        value ??= [];
        ArchiveSerialization.SerializeStringVector(archive, name, value);
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

    private static string GetPath(string fullPath)
    {
        var slash = Math.Max(fullPath.LastIndexOf('/'), fullPath.LastIndexOf('\\'));
        return slash >= 0 ? fullPath[..(slash + 1)] : string.Empty;
    }
}
