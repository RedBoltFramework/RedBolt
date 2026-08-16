// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Utility;

using System;
using System.Collections.Generic;
using System.Linq;

using Tg = RedBolt.Bindings.TinyGltf;

using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.Math;

internal sealed partial class GLTFHierarchyAnalyzer
{
    private void ImportAnimations()
    {
        for (var animationIndex = 0; animationIndex < _model.Animations.Count; ++animationIndex)
        {
            var animation = new GLTFAnimation { Index = animationIndex };
            _animations.Add(animation);
            ImportAnimation(animation);
        }
    }

    private void ImportAnimation(GLTFAnimation animation)
    {
        var sourceAnimation = _model.Animations[animation.Index];
        animation.Name = sourceAnimation.Name.ToString();
        foreach (var sourceChannel in sourceAnimation.Channels)
        {
            var targetPath = sourceChannel.TargetPath.ToString();
            var targetNode = GetEffectiveTargetNode(sourceChannel);
            var parentSkeletonIndex = GetNearestParentSkeleton(targetNode);
            var groupKey = new GLTFAnimationGroupKey(parentSkeletonIndex);
            if (!animation.AnimationGroups.TryGetValue(groupKey, out var animationGroup))
                animation.AnimationGroups[groupKey] = animationGroup = new GLTFAnimationTrackGroup();
            if (sourceChannel.Sampler >= sourceAnimation.Samplers.Count)
                throw new InvalidOperationException($"Unknown animation sampler #{sourceChannel.Sampler} is referenced");
            var sourceSampler = sourceAnimation.Samplers[sourceChannel.Sampler];
            var interpolation = GetInterpolationMode(sourceSampler);
            _base.CheckAccessor(sourceSampler.Input);
            _base.CheckAccessor(sourceSampler.Output);
            var channelKeys = _bufferReader.ReadAccessorChecked<float>(_model.Accessors[sourceSampler.Input]);
            var channelValuesAccessor = _model.Accessors[sourceSampler.Output];

            // Handle bones and nodes separately, mainly because bones have unique names and nodes don't
            if (targetPath == "weights")
            {
                var numMorphs = GetNumMorphsForNode(sourceChannel.TargetNode);
                if (numMorphs == 0)
                    throw new InvalidOperationException($"Animation #{animation.Index} weights channel targets node #{targetNode.Index} without morphs");
                var nodePath = GetNodePathRelativeToSkeleton(targetNode, parentSkeletonIndex);
                if (targetNode.SkinnedMeshNodes.Count != 0 && !targetNode.SkinnedMeshNodes.Contains(sourceChannel.TargetNode))
                    throw new InvalidOperationException($"Cannot connect morph weights animation to skinned mesh at node #{sourceChannel.TargetNode}");
                var componentIndex = targetNode.SkinnedMeshNodes.Count == 0 ? 0 : targetNode.SkinnedMeshNodes.IndexOf(sourceChannel.TargetNode);
                var weightsValues = _bufferReader.ReadAccessorChecked<float>(channelValuesAccessor);
                for (var morphIndex = 0; morphIndex < numMorphs; ++morphIndex)
                {
                    var trackPath = $"{nodePath}/@AnimatedModel#{componentIndex}/Morphs/{morphIndex}";
                    if (!animationGroup.AttributeTracksByPath.TryGetValue(trackPath, out var track))
                        animationGroup.AttributeTracksByPath[trackPath] = track = new GLTFAttributeTrack();
                    track.Interpolation = interpolation;
                    track.Keys = channelKeys;
                    if (interpolation == KeyFrameInterpolation.TangentSpline)
                    {
                        var morphWeightInTangents = ReadVerticalSlice(weightsValues, morphIndex * 3, numMorphs * 3);
                        var morphWeightValues = ReadVerticalSlice(weightsValues, morphIndex * 3 + 1, numMorphs * 3);
                        var morphWeightOutTangents = ReadVerticalSlice(weightsValues, morphIndex * 3 + 2, numMorphs * 3);
                        track.Values.AddRange(morphWeightValues.Select(value => new Variant(value)));
                        track.InTangents.AddRange(morphWeightValues.Select(value => new Variant(value)));
                        track.OutTangents.AddRange(morphWeightValues.Select(value => new Variant(value)));
                    }
                    else
                        track.Values.AddRange(ReadVerticalSlice(weightsValues, morphIndex, numMorphs).Select(value => new Variant(value)));
                }
            }
            else if (targetNode.SkeletonIndex is not null)
            {
                if (targetNode.UniqueBoneName is null)
                    throw new InvalidOperationException("Cannot connect animation track to node");
                if (!animationGroup.BoneTracksByBoneName.TryGetValue(targetNode.UniqueBoneName, out var track))
                    animationGroup.BoneTracksByBoneName[targetNode.UniqueBoneName] = track = new GLTFBoneTrack();
                var newChannel = ReadAnimationChannel(targetPath);
                if ((track.ChannelMask & newChannel) != 0)
                    throw new InvalidOperationException($"Duplicate animation for '{targetPath}' in animation #{animation.Index}");
                track.ChannelMask |= newChannel;
                if (newChannel == AnimationChannel.Position)
                {
                    var positionValues = _bufferReader.ReadVector3Accessor(channelValuesAccessor);
                    if (interpolation == KeyFrameInterpolation.TangentSpline) positionValues = ReadVerticalSlice(positionValues, 1, 3);
                    ApplyInlineTransformToPosition(positionValues);
                    if (positionValues.Count != channelKeys.Count)
                        throw new InvalidOperationException($"Animation #{animation.Index} channel input and output are mismatched");
                    track.Positions.RemoveAllKeyFrames();
                    for (var i = 0; i < positionValues.Count; ++i) track.Positions.AddKeyFrame((channelKeys[i], positionValues[i]));
                }
                else if (newChannel == AnimationChannel.Rotation)
                {
                    var rotationValues = _bufferReader.ReadQuaternionAccessor(channelValuesAccessor);
                    if (interpolation == KeyFrameInterpolation.TangentSpline) rotationValues = ReadVerticalSlice(rotationValues, 1, 3);
                    ApplyInlineTransformToRotation(rotationValues);
                    if (rotationValues.Count != channelKeys.Count)
                        throw new InvalidOperationException($"Animation #{animation.Index} channel input and output are mismatched");
                    track.Rotations.RemoveAllKeyFrames();
                    for (var i = 0; i < rotationValues.Count; ++i) track.Rotations.AddKeyFrame((channelKeys[i], rotationValues[i]));
                }
                else if (newChannel == AnimationChannel.Scale)
                {
                    var scaleValues = _bufferReader.ReadVector3Accessor(channelValuesAccessor);
                    if (interpolation == KeyFrameInterpolation.TangentSpline) scaleValues = ReadVerticalSlice(scaleValues, 1, 3);
                    if (scaleValues.Count != channelKeys.Count)
                        throw new InvalidOperationException($"Animation #{animation.Index} channel input and output are mismatched");
                    track.Scales.RemoveAllKeyFrames();
                    for (var i = 0; i < scaleValues.Count; ++i) track.Scales.AddKeyFrame((channelKeys[i], scaleValues[i]));
                }
            }
            else
            {
                var newChannel = ReadAnimationChannel(targetPath);
                var nodePath = GetNodePathRelativeToSkeleton(targetNode, parentSkeletonIndex);
                var trackPath = nodePath + "/" + ReadAttributeTrackName(newChannel);
                if (animationGroup.AttributeTracksByPath.ContainsKey(trackPath))
                    throw new InvalidOperationException($"Duplicate animation track '{trackPath}'");
                var track = new GLTFAttributeTrack { Interpolation = interpolation, Keys = channelKeys };
                animationGroup.AttributeTracksByPath.Add(trackPath, track);
                if (newChannel == AnimationChannel.Position)
                {
                    var positionValues = _bufferReader.ReadVector3Accessor(channelValuesAccessor);
                    ApplyInlineTransformToPosition(positionValues);
                    track.Values.AddRange(positionValues.Select(value => new Variant(value)));
                }
                else if (newChannel == AnimationChannel.Rotation)
                {
                    var rotationValues = _bufferReader.ReadQuaternionAccessor(channelValuesAccessor);
                    ApplyInlineTransformToRotation(rotationValues);
                    track.Values.AddRange(rotationValues.Select(value => new Variant(value)));
                }
                else if (newChannel == AnimationChannel.Scale)
                    track.Values.AddRange(_bufferReader.ReadVector3Accessor(channelValuesAccessor).Select(value => new Variant(value)));
                if (interpolation == KeyFrameInterpolation.TangentSpline)
                {
                    track.InTangents = ReadVerticalSlice(track.Values, 0, 3);
                    track.OutTangents = ReadVerticalSlice(track.Values, 2, 3);
                    track.Values = ReadVerticalSlice(track.Values, 1, 3);
                }
                if (track.Values.Count != channelKeys.Count)
                    throw new InvalidOperationException($"Animation #{animation.Index} channel input and output are mismatched");
            }
        }
    }

    private int GetNumMorphsForNode(int nodeIndex)
    {
        var node = _nodeByIndex[nodeIndex];
        if (node.Mesh is null)
            throw new InvalidOperationException($"Animation weights channel targets node #{node.Index} without mesh");
        _base.CheckMesh(node.Mesh.Value);
        return _numMorphsInMesh[node.Mesh.Value];
    }

    private GLTFNode GetEffectiveTargetNode(Tg.AnimationChannel channel)
    {
        _base.CheckNode(channel.TargetNode);
        return channel.TargetPath.ToString() == "weights" && _skinnedMeshNodeRemapping.TryGetValue(channel.TargetNode, out var nodeIndex)
            ? _nodeByIndex[nodeIndex] : _nodeByIndex[channel.TargetNode];
    }

    private static bool IsUniquelyNamedSibling(GLTFNode node)
    {
        if (node.Name.Length == 0) return false;
        if (node.Parent is null) return true;
        return node.Parent.Children.All(child => child == node || child.GetEffectiveName() != node.GetEffectiveName());
    }

    private string GetNodePathRelativeToSkeleton(GLTFNode node, int? skeletonIndex)
    {
        var path = GetPathIncludingSelf(node);
        var skeletonRoot = skeletonIndex is not null ? _skeletons[skeletonIndex.Value].RootNode : null;
        if (skeletonRoot is not null && !path.Contains(skeletonRoot))
            throw new InvalidOperationException("Skeleton doesn't contain required node");
        var startIndex = skeletonRoot is not null ? path.IndexOf(skeletonRoot) + 1 : 0;
        return string.Join('/', path.Skip(startIndex).Select(pathNode =>
            IsUniquelyNamedSibling(pathNode) ? pathNode.GetEffectiveName() : $"#{GetChildIndex(pathNode)}"));
    }

    private void ApplyInlineTransformToPosition(List<Vector3f> values)
    {
        for (var i = 0; i < values.Count; ++i) values[i] = _inlineTransform.ApplyToPosition(values[i]);
    }

    private void ApplyInlineTransformToRotation(List<Quaternion> values)
    {
        for (var i = 0; i < values.Count; ++i) values[i] = _inlineTransform.ApplyToRotation(values[i]);
    }

    private void ApplyInlineTransformToInverseBindMatrix(List<Matrix3x4> values)
    {
        for (var i = 0; i < values.Count; ++i) values[i] = _inlineTransform.ApplyToInverseBindMatrix(values[i]);
    }

    private int GetChildIndex(GLTFNode node)
    {
        var index = node.Parent is null ? _rootNodes.IndexOf(node) : node.Parent.Children.IndexOf(node);
        if (index < 0)
            throw new InvalidOperationException(node.Parent is null ? "Cannot get index of root node" : "Cannot find child in parent node");
        return index;
    }

    private static void MarkInSkin(GLTFNode node, int skin)
    {
        if (!node.ContainedInSkins.Contains(skin)) node.ContainedInSkins.Add(skin);
    }

    private static Matrix3x4 ReadMatrix3x4(Std.Vector<double> source)
    {
        if (source.Count != 16) throw new InvalidOperationException("Unexpected size of matrix object");
        var temp = new Matrix4(source.Select(value => (float)value).ToArray());
        return new Matrix3x4(temp.Transpose);
    }

    private static Vector3f ReadVector3(Std.Vector<double> source)
    {
        if (source.Count != 3) throw new InvalidOperationException("Unexpected size of matrix object");
        return new Vector3f((float)source[0], (float)source[1], (float)source[2]);
    }

    private static Quaternion ReadQuaternion(Std.Vector<double> source)
    {
        if (source.Count != 4) throw new InvalidOperationException("Unexpected size of matrix object");
        return GLTFImporterFunctions.RotationFromVector(new Vector4f((float)source[0], (float)source[1], (float)source[2], (float)source[3]));
    }

    private static AnimationChannel ReadAnimationChannel(string targetPath) => targetPath switch
    {
        "translation" => AnimationChannel.Position,
        "rotation" => AnimationChannel.Rotation,
        "scale" => AnimationChannel.Scale,
        _ => throw new InvalidOperationException($"Unknown animation channel '{targetPath}'")
    };

    private static string ReadAttributeTrackName(AnimationChannel channel) => channel switch
    {
        AnimationChannel.Position => "@/Position",
        AnimationChannel.Rotation => "@/Rotation",
        AnimationChannel.Scale => "@/Scale",
        _ => throw new InvalidOperationException($"Invalid animation channel '{channel}'")
    };

    private static int? GetNearestParentSkeleton(GLTFNode node)
    {
        for (var currentNode = node; currentNode is not null; currentNode = currentNode.Parent)
            if (currentNode.SkeletonIndex is not null) return currentNode.SkeletonIndex;
        return null;
    }

    private static List<T> ReadVerticalSlice<T>(List<T> source, int index, int count)
    {
        if (source.Count % count != 0 || index >= count)
            throw new InvalidOperationException("Invalid array slice specified");
        var result = new List<T>(source.Count / count);
        for (var i = 0; i < source.Count / count; ++i) result.Add(source[i * count + index]);
        return result;
    }

    private static KeyFrameInterpolation GetInterpolationMode(Tg.AnimationSampler sourceSampler) =>
        sourceSampler.Interpolation.ToString() switch
        {
            "STEP" => KeyFrameInterpolation.None,
            "LINEAR" => KeyFrameInterpolation.Linear,
            "CUBICSPLINE" => KeyFrameInterpolation.TangentSpline,
            _ => throw new InvalidOperationException($"Unsupported interpolation mode '{sourceSampler.Interpolation}'")
        };
}
