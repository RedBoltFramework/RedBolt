// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Utility;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

using RedBolt.Core;
using RedBolt.Graphics;

/// <summary>Utility to import animations.</summary>
internal sealed class GLTFAnimationImporter
{
    public GLTFAnimationImporter(GLTFImporterBase importerBase, GLTFHierarchyAnalyzer hierarchyAnalyzer,
        GLTFModelImporter modelImporter)
    {
        _base = importerBase;
        _hierarchyAnalyzer = hierarchyAnalyzer;
        _modelImporter = modelImporter;
        ImportAnimations();
    }

    public void SaveResources()
    {
        foreach (var animation in _animations.Values)
            _base.SaveResource(animation);
    }

    public Animation? FindAnimation(int animationIndex, int? groupIndex) =>
        _animations.GetValueOrDefault((animationIndex, groupIndex));

    public bool HasAnimations() => _animations.Count != 0;
    public bool HasSceneAnimations() => _hasSceneAnimations;

    private void ImportAnimations()
    {
        for (var animationIndex = 0; animationIndex < _base.GetModel().Animations.Count; ++animationIndex)
        {
            var sourceAnimation = _hierarchyAnalyzer.GetAnimation(animationIndex);
            if (GLTFImporterFunctions.IsNameSkipped(sourceAnimation.Name, _base.GetSettings().SkipTags))
                continue;
            foreach (var (groupKey, group) in sourceAnimation.AnimationGroups)
            {
                var groupIndex = groupKey.Value;
                var animationNameHint = GetAnimationGroupName(sourceAnimation, groupIndex);
                var animationName = _base.GetResourceName(animationNameHint, "Animations/", "Animation", ".ani");
                var animation = ImportAnimation(animationName, groupIndex, group);
                if (groupIndex is not null)
                {
                    var skeleton = _hierarchyAnalyzer.GetSkeleton(groupIndex.Value);
                    if (skeleton.RootNode.SkinnedMeshNodes.Count != 0)
                    {
                        var skinnedMeshNode = _hierarchyAnalyzer.GetNode(skeleton.RootNode.SkinnedMeshNodes[0]);
                        var model = _modelImporter.GetModel(skinnedMeshNode.Mesh!.Value, skinnedMeshNode.Skin!.Value);
                        animation.AddMetadata(AnimationMetadata.Model, model.GetName());
                    }
                }
                _base.AddToResourceCache(animation);
                _animations[(animationIndex, groupIndex)] = animation;
                if (groupIndex is null)
                    _hasSceneAnimations = true;
            }
        }
    }

    private Animation ImportAnimation(string animationName, int? groupIndex, GLTFAnimationTrackGroup sourceGroup)
    {
        var animation = new Animation();
        animation.SetName(animationName);
        foreach (var (boneName, boneTrack) in sourceGroup.BoneTracksByBoneName)
        {
            var hasPositions = (boneTrack.ChannelMask & AnimationChannel.Position) != 0;
            var hasRotations = (boneTrack.ChannelMask & AnimationChannel.Rotation) != 0;
            var hasScales = (boneTrack.ChannelMask & AnimationChannel.Scale) != 0;
            if (boneTrack.Positions.IsEmpty && hasPositions)
                throw new InvalidOperationException($"Position array is empty for animation '{animationName}'");
            if (boneTrack.Rotations.IsEmpty && hasRotations)
                throw new InvalidOperationException($"Rotation array is empty for animation '{animationName}'");
            if (boneTrack.Scales.IsEmpty && hasScales)
                throw new InvalidOperationException($"Scale array is empty for animation '{animationName}'");
            var track = animation.CreateTrack(boneName);
            track.CreateMerged(boneTrack.ChannelMask, CollectionsMarshal.AsSpan(boneTrack.Positions.KeyFrames),
                CollectionsMarshal.AsSpan(boneTrack.Rotations.KeyFrames),
                CollectionsMarshal.AsSpan(boneTrack.Scales.KeyFrames), _base.GetSettings().KeyFrameTimeError);
        }
        foreach (var (attributePath, attributeTrack) in sourceGroup.AttributeTracksByPath)
        {
            var track = animation.CreateVariantTrack(attributePath);
            track.Interpolation = attributeTrack.Interpolation;
            for (var i = 0; i < attributeTrack.Keys.Count; ++i)
            {
                track.KeyFrames.Add(new VariantCurvePoint
                    { Time = attributeTrack.Keys[i], Value = attributeTrack.Values[i] });
                if (track.Interpolation == KeyFrameInterpolation.TangentSpline)
                {
                    track.InTangents.Add(attributeTrack.InTangents[i]);
                    track.OutTangents.Add(attributeTrack.OutTangents[i]);
                }
            }
        }
        if (groupIndex is not null)
            FillAnimationTrackParents(animation, _hierarchyAnalyzer.GetSkeleton(groupIndex.Value));
        animation.SetLength(GetAnimationLength(animation));
        animation.SetAnimationName(Path.GetFileName(animationName));
        _base.GetCallback().OnAnimationLoaded(animation);
        return animation;
    }

    private static void FillAnimationTrackParents(Animation animation, GLTFSkeleton skeleton)
    {
        var animatedNodes = new List<(GLTFNode Node, GLTFNode? Parent)>();
        EnumerateNodesAndParents(animatedNodes, skeleton);
        if (animatedNodes.Count == 0)
            return;
        StripUnreferencedNodes(animatedNodes, GetAnimationTrackNames(animation), skeleton.RootNode.SkeletonIndex!.Value);
        using var nodeToParentNames = new StringVariantMap();
        foreach (var (node, parentNode) in animatedNodes)
            nodeToParentNames[node.UniqueBoneName!] = parentNode?.UniqueBoneName ?? string.Empty;
        using var metadata = new Variant(nodeToParentNames);
        animation.AddMetadata(AnimationMetadata.ParentTracks, metadata);
    }

    private static void EnumerateNodesAndParents(List<(GLTFNode Node, GLTFNode? Parent)> result, GLTFSkeleton skeleton)
    {
        if (skeleton.RootNode.SkeletonIndex is null)
            return;
        var skeletonIndex = skeleton.RootNode.SkeletonIndex.Value;
        GLTFHierarchyAnalyzer.ForEachSkeletonNode(skeleton.RootNode, skeletonIndex, node =>
            result.Add((node, node.Parent?.SkeletonIndex == skeletonIndex ? node.Parent : null)));
    }

    private static HashSet<string> GetAnimationTrackNames(Animation animation) =>
        animation.Tracks.Values.Select(track => track.Name).ToHashSet();

    private static bool IsNodeBoneTracked(GLTFNode node, HashSet<string> trackNames) =>
        node.UniqueBoneName is not null && trackNames.Contains(node.UniqueBoneName);

    private static void StripUnreferencedNodes(List<(GLTFNode Node, GLTFNode? Parent)> result,
        HashSet<string> trackNames, int skeletonIndex)
    {
        for (var i = result.Count - 1; i >= 0; --i)
        {
            var (node, parentNode) = result[i];
            if (!IsNodeBoneTracked(node, trackNames))
            {
                result.RemoveAt(i);
                continue;
            }
            while (parentNode is not null && !IsNodeBoneTracked(parentNode, trackNames))
            {
                parentNode = parentNode.Parent;
                if (parentNode is not null && parentNode.SkeletonIndex != skeletonIndex)
                    parentNode = null;
            }
            result[i] = (node, parentNode);
        }
    }

    private static string GetAnimationGroupName(GLTFAnimation animation, int? groupIndex)
    {
        var namePrefix = animation.Name.Length != 0 ? animation.Name : $"Animation_{animation.Index}";
        if (animation.AnimationGroups.Count <= 1)
            return namePrefix;
        return namePrefix + (groupIndex is not null ? $"_{groupIndex}" : "_R");
    }

    private static float? GetTrackLength<T>(IReadOnlyList<T> keyFrames, Func<T, float> getTime) =>
        keyFrames.Count == 0 ? null : getTime(keyFrames[^1]);

    private static float GetAnimationLength(Animation animation)
    {
        var length = 0.0f;
        foreach (var track in animation.Tracks.Values)
            length = Math.Max(length, GetTrackLength(track.KeyFrames, keyFrame => keyFrame.Time) ?? 0.0f);
        foreach (var track in animation.VariantTracks.Values)
            length = Math.Max(length, GetTrackLength(track.KeyFrames, keyFrame => keyFrame.Time) ?? 0.0f);
        return length;
    }

    private readonly GLTFImporterBase _base;
    private readonly GLTFHierarchyAnalyzer _hierarchyAnalyzer;
    private readonly GLTFModelImporter _modelImporter;
    private readonly Dictionary<(int, int?), Animation> _animations = [];
    private bool _hasSceneAnimations;
}
