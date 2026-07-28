//
// Copyright (c) 2008-2022 the Urho3D project.
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

using Microsoft.Extensions.Logging;
using System.Diagnostics;
using SystemMath = global::System.Math;
using System.Runtime.InteropServices;

using RedBolt.Container;
using RedBolt.Core;
using RedBolt.Input;
using RedBolt.Logging;
using RedBolt.Math;
using RedBolt.Resource;
using RedBolt.Scene;
using SceneType = global::RedBolt.Scene.Scene;

namespace RedBolt.Graphics;

/// <summary>
/// Component that drives an AnimatedModel's animations.
/// </summary>
[Reflected(ObjectCategory.Category_Logic)]
public partial class AnimationController : AnimationStateSource
{
    private const int InvalidAnimationIndex = -1;
    private static readonly StringVector AnimationParametersNames = new()
    {
        "Animation Count",
        "   Animation",
        "   Is Looped",
        "   Remove On Completion",
        "   Layer",
        "   Blend Mode",
        "   Start Bone",
        "   Auto Fade Out Time",
        "   Time",
        "   Min Time",
        "   Max Time",
        "   Speed",
        "   Remove On Zero Weight",
        "   Weight",
        "   Target Weight",
        "   Target Weight Delay"
    };

    /// <summary>
    /// Construct.
    /// </summary>
    public AnimationController()
    {
    }

    public override void Dispose()
    {
        foreach (var value in _animatedAttributes.Values)
            value.Dispose();
        _animatedAttributes.Clear();
        _animatedNodes.Clear();
        _pendingTriggers.Clear();
        _tempAnimations.Clear();
        _animations.Clear();
        base.Dispose();
    }

    /// <summary>
    /// Apply attribute changes that can not be applied immediately. Called after scene load or a network update.
    /// </summary>
    public override void ApplyAttributes()
    {
        ConnectToAnimatedModel();
    }

    /// <summary>
    /// Handle enabled/disabled state change.
    /// </summary>
    public override void OnSetEnabled()
    {
        var scene = Scene;
        if (scene is null)
            return;

        if (IsEnabledEffective)
            SubscribeToEvent<SceneEvents.ScenePostUpdateEvent>(scene, HandleScenePostUpdate);
        else
            UnsubscribeFromEvent<SceneEvents.ScenePostUpdateEvent>(scene);
    }

    /// <summary>
    /// Mark that animation state tracks are dirty and should be reconnected.
    /// Should be called on every substantial change in animated structure.
    /// </summary>
    public override void MarkAnimationStateTracksDirty()
    {
        for (var i = 0; i < _animationStates.Count; ++i)
            _animationStates[i].MarkTracksDirty();
    }

    /// <summary>
    /// Update the animations. Is called from HandleScenePostUpdate().
    /// </summary>
    public virtual void Update(float timeStep)
    {
        var renderer = GetSubsystem<Renderer>();
        if (renderer is not null)
        {
            ref var stats = ref renderer.GetMutableFrameStats();
            ++stats.Animations;
            if (_revisionDirty)
                ++stats.ChangedAnimations;
        }

        // Update revision
        if (_revisionDirty)
        {
            ++_revision;
            _revisionDirty = false;
        }

        // Update individual animations
        _pendingTriggers.Clear();
        for (var i = 0; i < _animations.Count; ++i)
        {
            var instance = _animations[i];
            if (instance.Params.GetAnimation() is null)
                continue;

            UpdateInstance(instance.Params, timeStep, true);
            if (!instance.Params.Removed)
                UpdateState(instance.State!, instance.Params);
            else
                _animationStatesDirty = true;
        }

        for (var i = _animations.Count - 1; i >= 0; --i)
        {
            if (!_animations[i].Params.Removed)
                continue;

            _animations.RemoveAt(i);
        }

        // Sort animation states if necessary
        if (_animationStatesDirty)
            SortAnimationStates();

        // Update animation tracks if necessary
        for (var i = 0; i < _animationStates.Count; ++i)
        {
            if (_animationStates[i].AreTracksDirty())
                UpdateAnimationStateTracks(_animationStates[i]);
        }

        // Reset skeleton if necessary
        if (_resetSkeleton)
            TryGetNode()?.GetComponent<AnimatedModel>()?.ResetBones();

        // Node and attribute animations need to be applied manually
        CommitNodeAndAttributeAnimations();
    }

    /// <summary>
    /// Smoothly replace existing animations with animations from external source.
    /// If layers are specified, only these layers will be affected.
    /// </summary>
    public void ReplaceAnimations(
        ReadOnlySpan<AnimationParameters> newAnimations, float elapsedTime, float fadeTime,
        ReadOnlySpan<int> layers = default)
    {
        // Upload new parameters and correct them according to elapsed time, some states may be removed
        var numNewAnimations = newAnimations.Length;
        _tempAnimations.Clear();
        for (var i = 0; i < numNewAnimations; ++i)
        {
            var instance = new AnimationInstance
            {
                Params = newAnimations[i].Clone(),
                State = null
            };
            UpdateInstance(instance.Params, elapsedTime, false);
            _tempAnimations.Add(instance);
        }

        // Try to match old animation states for all remaining states
        for (var i = 0; i < numNewAnimations; ++i)
        {
            var newParams = _tempAnimations[i].Params;
            if (newParams.Removed)
                continue;

            AnimationInstance? oldInstance = null;
            for (var j = 0; j < _animations.Count; ++j)
            {
                var candidate = _animations[j];
                if (!candidate.Params.Merged && candidate.Params.IsMergeableWith(newParams))
                {
                    oldInstance = candidate;
                    break;
                }
            }

            if (oldInstance is not null)
            {
                _tempAnimations[i].State = oldInstance.State;
                oldInstance.Params.Merged = true;

                // Keep original weight for smooth transition when merged
                newParams.WeightValue = oldInstance.Params.WeightValue;
                // Keep original delay if weight didn't change, extend delay up to minimum value otherwise
                var weightChanged = !MathDefs.Equals(newParams.TargetWeight, oldInstance.Params.TargetWeight, MathDefs.LargeEpsilon);
                newParams.TargetWeightDelay = weightChanged
                    ? SystemMath.Max(newParams.TargetWeightDelay, fadeTime)
                    : oldInstance.Params.TargetWeightDelay;
            }
        }

        // Remove all merged states from the animations, and fade out unmerged animations
        _animations.RemoveAll(static instance => instance.Params.Merged);
        for (var i = 0; i < _animations.Count; ++i)
        {
            var instance = _animations[i];
            if (layers.IsEmpty || layers.IndexOf(instance.Params.LayerIndex) >= 0)
                instance.Params.RemoveDelayed(fadeTime);
        }

        // Append new animations
        for (var i = 0; i < _tempAnimations.Count; ++i)
        {
            var instance = _tempAnimations[i];
            if (instance.Params.Removed)
                continue;

            if (instance.State is null)
            {
                PlayNew(instance.Params, fadeTime);
            }
            else
            {
                EnsureStateInitialized(instance.State, instance.Params);
                _animations.Add(instance.Clone());
            }
        }

        _animationStatesDirty = true;
        _revisionDirty = true;
    }

    /// <summary>
    /// Manage played animations on low level.
    /// </summary>
    public void AddAnimation(AnimationParameters parameters)
    {
        var instanceIndex = 0;
        for (var i = 0; i < _animations.Count; ++i)
        {
            if (ReferenceEquals(_animations[i].Params.GetAnimation(), parameters.GetAnimation()))
                ++instanceIndex;
        }

        var instance = new AnimationInstance
        {
            Params = parameters.Clone(),
            State = new AnimationState(this)
        };
        instance.Params.InstanceIndex = instanceIndex;

        var model = GetComponent<AnimatedModel>();
        if (model is not null)
            instance.State.ConnectToAnimatedModel(model);

        EnsureStateInitialized(instance.State!, instance.Params);
        _animations.Add(instance);

        _animationStatesDirty = true;
        _revisionDirty = true;
    }

    public void UpdateAnimation(int index, AnimationParameters parameters)
    {
        if (index < 0 || index >= _animations.Count)
        {
            Debug.Assert(false, "Out-of-bounds animation index in AnimationController::UpdateAnimation");
            return;
        }

        var instance = _animations[index];
        if (!ReferenceEquals(instance.Params.GetAnimation(), parameters.GetAnimation())
            || instance.Params.InstanceIndex != parameters.InstanceIndex)
        {
            Debug.Assert(false, "Animation and instance index cannot be changed by AnimationController::UpdateAnimation");
            return;
        }

        EnsureStateInitialized(instance.State!, parameters);
        if (instance.Params.LayerIndex != parameters.LayerIndex)
            _animationStatesDirty = true;

        if (!instance.Params.Equals(parameters))
        {
            instance.Params = parameters.Clone();
            _revisionDirty = true;
        }
    }

    public void RemoveAnimation(int index)
    {
        if (index < 0 || index >= _animations.Count)
        {
            Debug.Assert(false, "Out-of-bounds animation index in AnimationController::UpdateAnimation");
            return;
        }

        _animations.RemoveAt(index);
        _animationStatesDirty = true;
        _revisionDirty = true;
    }

    public void GetAnimationParameters(IList<AnimationParameters> result)
    {
        ArgumentNullException.ThrowIfNull(result);
        result.Clear();
        for (var i = 0; i < _animations.Count; ++i)
            result.Add(_animations[i].Params.Clone());
    }

    public List<AnimationParameters> GetAnimationParameters()
    {
        var result = new List<AnimationParameters>(_animations.Count);
        GetAnimationParameters(result);
        return result;
    }

    public int GetNumAnimations() => _animations.Count;

    public int GetAnimationLayer(int index)
    {
        return index >= 0 && index < _animations.Count ? _animations[index].Params.LayerIndex : 0;
    }

    public AnimationParameters GetAnimationParameters(int index)
    {
        return index >= 0 && index < _animations.Count
            ? _animations[index].Params
            : AnimationParameters.Empty;
    }

    public uint GetRevision() => _revision;

    public void UpdatePose()
    {
        Update(0.0f);
    }

    /// <summary>
    /// Manage played animations on high level.
    /// </summary>
    public int FindLastAnimation(Animation? animation, int layer = InvalidAnimationIndex)
    {
        for (var i = _animations.Count - 1; i >= 0; --i)
        {
            var parameters = _animations[i].Params;
            if (animation is not null && !ReferenceEquals(parameters.GetAnimation(), animation))
                continue;
            if (layer != InvalidAnimationIndex && parameters.LayerIndex != layer)
                continue;
            return i;
        }

        return InvalidAnimationIndex;
    }

    public AnimationParameters? GetLastAnimationParameters(Animation? animation, int layer = InvalidAnimationIndex)
    {
        var index = FindLastAnimation(animation, layer);
        return index != InvalidAnimationIndex ? _animations[index].Params : null;
    }

    public AnimationParameters? GetMutableLastAnimationParameters(Animation? animation, int layer = InvalidAnimationIndex)
    {
        var index = FindLastAnimation(animation, layer);
        return index != InvalidAnimationIndex ? _animations[index].Params : null;
    }

    public bool IsPlaying(Animation? animation) => FindLastAnimation(animation) != InvalidAnimationIndex;

    public int PlayNew(AnimationParameters parameters, float fadeInTime = 0.0f)
    {
        var adjustedParameters = parameters.Clone();
        if (fadeInTime > 0.0f)
        {
            adjustedParameters.WeightValue = 0.0f;
            adjustedParameters.TargetWeightDelay = fadeInTime;
        }

        AddAnimation(adjustedParameters);
        return _animations.Count - 1;
    }

    public int PlayNewExclusive(AnimationParameters parameters, float fadeInTime = 0.0f)
    {
        var index = PlayNew(parameters, fadeInTime);
        StopLayerExcept(parameters.LayerIndex, index, fadeInTime);
        return index;
    }

    public int PlayExisting(AnimationParameters parameters, float fadeInTime = 0.0f)
    {
        var index = FindLastAnimation(parameters.GetAnimation(), parameters.LayerIndex);
        if (index == InvalidAnimationIndex)
        {
            PlayNew(parameters, fadeInTime);
            return _animations.Count - 1;
        }

        var existingParameters = GetAnimationParameters(index).Clone();
        existingParameters.SpeedValue = parameters.SpeedValue;
        existingParameters.RemoveOnZeroWeight = parameters.RemoveOnZeroWeight;
        if (existingParameters.TargetWeight != parameters.TargetWeight)
        {
            existingParameters.TargetWeight = parameters.TargetWeight;
            existingParameters.TargetWeightDelay = fadeInTime;
        }
        UpdateAnimation(index, existingParameters);
        return index;
    }

    public int PlayExistingExclusive(AnimationParameters parameters, float fadeInTime = 0.0f)
    {
        var index = PlayExisting(parameters, fadeInTime);
        StopLayerExcept(parameters.LayerIndex, index, fadeInTime);
        return index;
    }

    public void Fade(Animation? animation, float targetWeight, float fadeTime)
    {
        var index = FindLastAnimation(animation);
        if (index == InvalidAnimationIndex)
            return;

        var parameters = GetAnimationParameters(index).Clone();
        parameters.TargetWeight = targetWeight;
        parameters.TargetWeightDelay = fadeTime;
        UpdateAnimation(index, parameters);
    }

    public void Stop(Animation? animation, float fadeTime = 0.0f)
    {
        var index = FindLastAnimation(animation);
        if (index == InvalidAnimationIndex)
            return;

        var parameters = GetAnimationParameters(index).Clone();
        if (parameters.RemoveDelayed(fadeTime))
            UpdateAnimation(index, parameters);
    }

    public void StopLayer(int layer, float fadeTime = 0.0f)
    {
        for (var index = 0; index < _animations.Count; ++index)
        {
            if (_animations[index].Params.LayerIndex != layer)
                continue;

            var parameters = GetAnimationParameters(index).Clone();
            if (parameters.RemoveDelayed(fadeTime))
                UpdateAnimation(index, parameters);
        }
    }

    public void StopLayerExcept(int layer, int exceptionIndex, float fadeTime = 0.0f)
    {
        for (var index = 0; index < _animations.Count; ++index)
        {
            if (_animations[index].Params.LayerIndex != layer || index == exceptionIndex)
                continue;

            var parameters = GetAnimationParameters(index).Clone();
            if (parameters.RemoveDelayed(fadeTime))
                UpdateAnimation(index, parameters);
        }
    }

    public void StopAll(float fadeTime = 0.0f)
    {
        for (var index = 0; index < _animations.Count; ++index)
        {
            var parameters = GetAnimationParameters(index).Clone();
            if (parameters.RemoveDelayed(fadeTime))
                UpdateAnimation(index, parameters);
        }
    }

    public bool UpdateAnimationTime(Animation? animation, float time)
    {
        var index = FindLastAnimation(animation);
        if (index == InvalidAnimationIndex)
            return false;

        var parameters = GetAnimationParameters(index).Clone();
        parameters.SetTime(time);
        UpdateAnimation(index, parameters);
        return true;
    }

    public bool UpdateAnimationWeight(Animation? animation, float weight, float fadeTime = 0.0f)
    {
        var index = FindLastAnimation(animation);
        if (index == InvalidAnimationIndex)
            return false;

        var parameters = GetAnimationParameters(index).Clone();
        if (fadeTime == 0.0f)
            parameters.WeightValue = weight;
        parameters.TargetWeight = weight;
        parameters.TargetWeightDelay = fadeTime;
        UpdateAnimation(index, parameters);
        return true;
    }

    public bool UpdateAnimationSpeed(Animation? animation, float speed)
    {
        var index = FindLastAnimation(animation);
        if (index == InvalidAnimationIndex)
            return false;

        var parameters = GetAnimationParameters(index).Clone();
        parameters.SpeedValue = speed;
        UpdateAnimation(index, parameters);
        return true;
    }

    /// <summary>
    /// Deprecated.
    /// </summary>
    public bool Play(string name, int layer, bool looped, float fadeTime = 0.0f)
    {
        var animation = GetAnimationByName(name);
        if (animation is null)
            return false;

        var parameters = new AnimationParameters(animation).Layer(layer);
        parameters.RemoveOnZeroWeight = true;
        parameters.IsLooped = looped;
        PlayExisting(parameters, fadeTime);
        return true;
    }

    public bool PlayExclusive(string name, int layer, bool looped, float fadeTime = 0.0f)
    {
        var animation = GetAnimationByName(name);
        if (animation is null)
            return false;

        var parameters = new AnimationParameters(animation).Layer(layer);
        parameters.RemoveOnZeroWeight = true;
        parameters.IsLooped = looped;
        PlayExistingExclusive(parameters, fadeTime);
        return true;
    }

    public bool Fade(string name, float targetWeight, float fadeTime = 0.0f)
    {
        var animation = GetAnimationByName(name);
        if (animation is null)
            return false;

        Fade(animation, targetWeight, fadeTime);
        return true;
    }

    public bool Stop(string name, float fadeTime = 0.0f)
    {
        var animation = GetAnimationByName(name);
        if (animation is null)
            return false;

        Stop(animation, fadeTime);
        return true;
    }

    public bool SetTime(string name, float time)
    {
        var animation = GetAnimationByName(name);
        return animation is not null && UpdateAnimationTime(animation, time);
    }

    public bool SetWeight(string name, float weight)
    {
        var animation = GetAnimationByName(name);
        return animation is not null && UpdateAnimationWeight(animation, weight);
    }

    public bool SetSpeed(string name, float speed)
    {
        var animation = GetAnimationByName(name);
        return animation is not null && UpdateAnimationSpeed(animation, speed);
    }

    public bool IsPlaying(string name)
    {
        var animation = GetAnimationByName(name);
        return animation is not null && IsPlaying(animation);
    }

    public float GetTime(string name)
    {
        var animation = GetAnimationByName(name);
        var parameters = GetLastAnimationParameters(animation);
        return parameters?.GetTime() ?? 0.0f;
    }

    public float GetWeight(string name)
    {
        var animation = GetAnimationByName(name);
        var parameters = GetLastAnimationParameters(animation);
        return parameters?.WeightValue ?? 0.0f;
    }

    public float GetSpeed(string name)
    {
        var animation = GetAnimationByName(name);
        var parameters = GetLastAnimationParameters(animation);
        return parameters?.SpeedValue ?? 0.0f;
    }

    [Serialized("Reset Skeleton", DefaultSource = nameof(GetResetSkeletonAttributeDefault), Setter = nameof(SetSkeletonReset))]
    public bool IsSkeletonReset() => _resetSkeleton;

    public void SetSkeletonReset(bool resetSkeleton) => _resetSkeleton = resetSkeleton;

    /// <summary>
    /// Set animation parameters attribute.
    /// </summary>
    public void SetAnimationsAttr(VariantVector value)
    {
        _animations.Clear();
        _revisionDirty = true;

        if (value.Count == 0 || value[0].Type != VariantType.Int)
            return;

        var requestedCount = SystemMath.Max(0, value[0].GetInt());
        var availableCount = (value.Count - 1) / AnimationParameters.NumVariants;
        var loadedCount = SystemMath.Min(requestedCount, availableCount);

        var valueSpan = CollectionsMarshal.AsSpan(value);
        for (var i = 0; i < loadedCount; ++i)
        {
            var offset = 1 + i * AnimationParameters.NumVariants;
            var variants = valueSpan.Slice(offset, AnimationParameters.NumVariants);
            AddAnimation(AnimationParameters.FromVariantSpan(variants));
        }

        for (var i = loadedCount; i < requestedCount; ++i)
            AddAnimation(new AnimationParameters((Animation?)null));
    }

    /// <summary>
    /// Return animation parameters attribute.
    /// </summary>
    [Serialized("Animations", Configure = nameof(ConfigureAnimationsAttribute), DefaultSource = nameof(GetAnimationsAttributeDefault), Setter = nameof(SetAnimationsAttr))]
    public VariantVector GetAnimationsAttr()
    {
        var numAnimations = _animations.Count;
        var numVariants = AnimationParameters.NumVariants;
        var resultSize = checked(1 + numAnimations * numVariants);

        var result = new VariantVector();
        for (var i = 0; i < resultSize; ++i)
            result.Add(default);
        result[0] = new Variant(numAnimations);

        var valueSpan = CollectionsMarshal.AsSpan(result);
        for (var i = 0; i < numAnimations; ++i)
        {
            var variants = valueSpan.Slice(1 + i * numVariants, numVariants);
            _animations[i].Params.ToVariantSpan(variants);
        }

        return result;
    }

    /// <summary>
    /// Handle node being assigned.
    /// </summary>
    protected override void OnNodeSet(Node? previousNode, Node? currentNode)
    {
        ConnectToAnimatedModel();
    }

    /// <summary>
    /// Handle scene being assigned.
    /// </summary>
    protected internal override void OnSceneSet(SceneType? previousScene, SceneType? scene)
    {
        if (scene is not null && IsEnabledEffective)
            SubscribeToEvent<SceneEvents.ScenePostUpdateEvent>(scene, HandleScenePostUpdate);
        else if (scene is null)
            UnsubscribeFromEvent<SceneEvents.ScenePostUpdateEvent>();
    }

    private void HandleScenePostUpdate(ref SceneEvents.ScenePostUpdateEvent eventData)
    {
        Update(eventData.TimeStep);
    }

    private void SortAnimationStates()
    {
        _tempAnimations.Clear();
        for (var i = 0; i < _animations.Count; ++i)
            _tempAnimations.Add(_animations[i].Clone());
        _tempAnimations.Sort(static (lhs, rhs) => lhs.Params.LayerIndex.CompareTo(rhs.Params.LayerIndex));

        _animationStates.Clear();
        for (var i = 0; i < _tempAnimations.Count; ++i)
            _animationStates.Add(_tempAnimations[i].State!);

        _animationStatesDirty = false;
    }

    private void UpdateAnimationStateTracks(AnimationState state)
    {
        var animation = state.GetAnimation();
        state.ClearAllTracks();

        if (animation is null)
        {
            state.OnTracksReady();
            return;
        }

        var startBoneName = state.GetStartBone();
        var startNode = !string.IsNullOrEmpty(startBoneName) ? Node.GetChild(startBoneName, true) : null;
        startNode ??= Node;

        var model = GetComponent<AnimatedModel>();

        foreach (var track in animation.Tracks.Values)
        {
            var trackBoneIndex = model is not null ? model.GetSkeleton().GetBoneIndex(track.NameHash) : -1;
            var trackBone = trackBoneIndex != -1 ? model!.GetSkeleton().GetBone(trackBoneIndex) : null;
            if (trackBone?.Node is { } boneNode &&
                (ReferenceEquals(startNode, Node) || boneNode.IsChildOf(startNode)))
            {
                state.AddModelTrack(new ModelAnimationStateTrack
                {
                    Track = track,
                    Node = boneNode,
                    BoneIndex = trackBoneIndex,
                    Bone = trackBone
                });
                continue;
            }

            var trackNode = GetTrackNodeByNameHash(track.NameHash, startNode);
            if (trackNode is not null)
            {
                state.AddNodeTrack(new NodeAnimationStateTrack
                {
                    Track = track,
                    Node = trackNode
                });
            }
        }

        foreach (var track in animation.VariantTracks.Values)
        {
            var attribute = ParseAnimatablePath(track.Name, startNode);
            if (attribute.Serializable is not null)
            {
                state.AddAttributeTrack(new AttributeAnimationStateTrack
                {
                    Track = track,
                    Attribute = attribute
                });
            }
        }

        state.OnTracksReady();
    }

    private void ConnectToAnimatedModel()
    {
        var model = TryGetNode()?.GetComponent<AnimatedModel>();
        if (model is not null)
        {
            model.ConnectToAnimationStateSource(this);
            foreach (var animation in _animations)
                animation.State?.ConnectToAnimatedModel(model);
        }
    }

    private AnimatedAttributeReference ParseAnimatablePath(string path, Node? startNode)
    {
        if (string.IsNullOrEmpty(path))
        {
            Log.GetLogger().LogWarning("Variant animation track name must not be empty");
            return default;
        }

        var animatedNode = startNode;
        var attributePath = path;

        // Resolve path to node if necessary
        if (path[0] != '@')
        {
            var separator = path.IndexOf("/@", StringComparison.Ordinal);
            if (separator < 0)
            {
                Log.GetLogger().LogWarning("Path must end with attribute reference like /@StaticModel/Model");
                return default;
            }

            var nodePath = path[..separator];
            animatedNode = startNode!.FindChild(nodePath);
            if (animatedNode is null)
            {
                Log.GetLogger().LogWarning("Path to node '{Path}' cannot be resolved", nodePath);
                return default;
            }

            attributePath = path[(separator + 1)..];
        }

        var result = new AnimatedAttributeReference();

        // Special cases:
        // 1) if Node variables are referenced, individual variables are supported
        // 2) if AnimatedModel morphs are referenced, individual morphs are supported
        const string variablesPath = "@/Variables/";
        const string animatedModelPath = "@AnimatedModel";
        if (attributePath.StartsWith(variablesPath, StringComparison.Ordinal))
        {
            var variableNameHash = new StringHash(attributePath[variablesPath.Length..]);
            result.AttributeType = AnimatedAttributeType.NodeVariables;
            result.SubAttributeKey = variableNameHash.Value;
            attributePath = attributePath[..(variablesPath.Length - 1)];
        }
        else if (attributePath.StartsWith(animatedModelPath, StringComparison.Ordinal))
        {
            var separator1 = attributePath.IndexOf('/');
            if (separator1 >= 0)
            {
                var separator2 = attributePath.IndexOf('/', separator1 + 1);
                if (separator2 >= 0)
                {
                    result.AttributeType = AnimatedAttributeType.AnimatedModelMorphs;
                    uint.TryParse(attributePath[(separator2 + 1)..], out result.SubAttributeKey);
                    attributePath = attributePath[..separator2];
                }
            }
        }

        var attribute = animatedNode!.FindComponentAttribute(attributePath);
        if (attribute.Serializable is null)
        {
            Log.GetLogger().LogWarning("Path to attribute '{Path}' cannot be resolved", attributePath);
            return default;
        }

        result.Serializable = attribute.Serializable;
        result.AttributeIndex = attribute.AttributeIndex;
        return result;
    }

    private Node? GetTrackNodeByNameHash(StringHash trackNameHash, Node? startBone)
    {
        // Empty track name means that we animate the node itself
        if (trackNameHash == StringHash.Empty)
            return Node;

        // If track name hash matches start bone name hash then we animate the start bone
        if (trackNameHash == startBone!.GetNameHash())
            return startBone;

        return startBone.GetChild(trackNameHash, true);
    }

    private Animation? GetAnimationByName(string name)
    {
        return GetSubsystem<ResourceCache>()!.GetResource<Animation>(name);
    }

    private void UpdateInstance(AnimationParameters parameters, float timeStep, bool fireTriggers)
    {
        var overtime = UpdateInstanceTime(parameters, timeStep, fireTriggers);
        var isCompleted = overtime.HasValue;
        UpdateInstanceWeight(parameters, timeStep);

        if (isCompleted)
            ApplyInstanceAutoFadeOut(parameters, overtime!.Value);
        ApplyInstanceRemoval(parameters, isCompleted);
    }

    private float? UpdateInstanceTime(AnimationParameters parameters, float timeStep, bool fireTriggers)
    {
        var scaledTimeStep = timeStep * parameters.SpeedValue;
        if (scaledTimeStep == 0.0f)
            return null;

        var delta = parameters.Update(scaledTimeStep);
        var positiveOvertime = delta.End() - delta.Max();
        var negativeOvertime = delta.End() - delta.Min();
        var isCompleted = !parameters.IsLooped
            && (parameters.SpeedValue > 0.0f ? positiveOvertime >= 0.0f : negativeOvertime <= 0.0f);
        var scaledOvertime = isCompleted
            ? parameters.SpeedValue > 0.0f ? positiveOvertime : negativeOvertime
            : 0.0f;

        if (fireTriggers && parameters.GetAnimation() is { } animation)
        {
            var length = animation.Length;
            for (var triggerIndex = 0; triggerIndex < animation.NumTriggers; ++triggerIndex)
            {
                ref var trigger = ref animation.GetTrigger(triggerIndex);
                if (delta.ContainsExcludingBegin(SystemMath.Min(trigger.Time, length)))
                    _pendingTriggers.Add((animation, triggerIndex));
            }
        }

        return isCompleted ? scaledOvertime / parameters.SpeedValue : null;
    }

    private void ApplyInstanceAutoFadeOut(AnimationParameters parameters, float overtime)
    {
        if (parameters.TargetWeight == 0.0f || parameters.AutoFadeOutTime == 0.0f)
            return;

        parameters.TargetWeight = 0.0f;
        parameters.TargetWeightDelay = SystemMath.Max(0.0f, parameters.AutoFadeOutTime - overtime);
        parameters.RemoveOnZeroWeight = true;

        var fraction = SystemMath.Min(1.0f, overtime / parameters.AutoFadeOutTime);
        parameters.WeightValue = MathDefs.Lerp(parameters.WeightValue, 0.0f, fraction);
    }

    private void UpdateInstanceWeight(AnimationParameters parameters, float timeStep)
    {
        if (parameters.TargetWeightDelay <= 0.0f)
        {
            parameters.WeightValue = parameters.TargetWeight;
            return;
        }

        var fraction = SystemMath.Min(1.0f, timeStep / parameters.TargetWeightDelay);
        parameters.WeightValue = MathDefs.Lerp(parameters.WeightValue, parameters.TargetWeight, fraction);
        parameters.TargetWeightDelay = SystemMath.Max(0.0f, parameters.TargetWeightDelay - timeStep);
    }

    private void ApplyInstanceRemoval(AnimationParameters parameters, bool isCompleted)
    {
        if (parameters.RemoveOnCompletion && parameters.AutoFadeOutTime == 0.0f && isCompleted)
            parameters.Removed = true;
        if (parameters.RemoveOnZeroWeight && parameters.WeightValue < MathDefs.LargeEpsilon)
            parameters.Removed = true;
    }

    private void EnsureStateInitialized(AnimationState state, AnimationParameters parameters)
    {
        state.Initialize(parameters.GetAnimation(), parameters.StartBoneName, parameters.BlendMode);
    }

    private void UpdateState(AnimationState state, AnimationParameters parameters)
    {
        state.Update(parameters.IsLooped, parameters.GetTime(), parameters.WeightValue);
    }

    private void CommitNodeAndAttributeAnimations()
    {
        foreach (var node in _animatedNodes.Keys)
        {
            var value = _animatedNodes[node];
            value.Dirty = AnimationChannel.None;
            _animatedNodes[node] = value;
        }

        foreach (var attribute in _animatedAttributes.Keys)
        {
            ref var value = ref CollectionsMarshal.GetValueRefOrNullRef(_animatedAttributes, attribute);
            value.Dispose();
        }

        for (var i = 0; i < _animationStates.Count; ++i)
        {
            _animationStates[i].CalculateNodeTracks(_animatedNodes);
            _animationStates[i].CalculateAttributeTracks(_animatedAttributes);
        }

        foreach (var pair in _animatedNodes)
        {
            var node = pair.Key;
            var value = pair.Value;
            if ((value.Dirty & AnimationChannel.Position) != 0)
                node.SetPosition(value.LocalToParent.Position);
            if ((value.Dirty & AnimationChannel.Rotation) != 0)
                node.SetRotation(value.LocalToParent.Rotation);
            if ((value.Dirty & AnimationChannel.Scale) != 0)
                node.SetScale(value.LocalToParent.Scale);
        }

        foreach (var pair in _animatedAttributes)
        {
            if (pair.Value.IsEmpty)
                continue;
            pair.Key.SetValue(pair.Value);
        }
    }

    private void SendTriggerEvents()
    {
        foreach (var pendingTrigger in _pendingTriggers)
        {
            ref var trigger = ref pendingTrigger.Animation.GetTrigger(pendingTrigger.TriggerIndex);
            var eventData = new DrawableEvents.AnimationTriggerEvent
            {
                Node = Node,
                Animation = pendingTrigger.Animation,
                Name = pendingTrigger.Animation.AnimationName,
                Time = trigger.Time,
                Data = trigger.Data.Clone()
            };
            try
            {
                Node.SendEvent(ref eventData);
            }
            finally
            {
                eventData.Data.Dispose();
            }
        }
    }

    /// <summary>
    /// Currently playing animations.
    /// </summary>
    private sealed class AnimationInstance : ICloneable
    {
        public required AnimationParameters Params;
        public AnimationState? State;

        public AnimationInstance Clone()
        {
            return new AnimationInstance
            {
                Params = Params.Clone(),
                State = State
            };
        }

        object ICloneable.Clone() => Clone();
    }

    private readonly List<AnimationInstance> _animations = [];
    private readonly List<AnimationInstance> _tempAnimations = [];
    private readonly List<(Animation Animation, int TriggerIndex)> _pendingTriggers = [];
    private uint _revision;
    private bool _animationStatesDirty;
    private bool _revisionDirty;
    private readonly Dictionary<Node, NodeAnimationOutput> _animatedNodes = [];
    private readonly Dictionary<AnimatedAttributeReference, Variant> _animatedAttributes = [];

    private static object? GetIsEnabledAttributeDefault() => true;

    private static object? GetResetSkeletonAttributeDefault() => false;

    private static object? GetAnimationsAttributeDefault() => new VariantVector();

    private static void ConfigureAnimationsAttribute(AttributeHandle attribute)
    {
        using var metadata = new Variant(AnimationParametersNames);
        attribute.SetMetadata(AttributeMetadata.VectorStructElements, metadata);
    }

    private static object? GetUpdatePoseAttributeDefault() => false;

    private static void ConfigureUpdatePoseAttribute(AttributeHandle attribute)
    {
        attribute.SetMetadata(AttributeMetadata.IsAction, true);
    }

    [Serialized("Update Pose", typeof(bool), Configure = nameof(ConfigureUpdatePoseAttribute), DefaultSource = nameof(GetUpdatePoseAttributeDefault), Mode = AttributeMode.Edit, Setter = nameof(SetSerializedUpdatePose))]
    private object? GetSerializedUpdatePose() => string.Empty;

    private void SetSerializedUpdatePose(object? value)
    {
        Serializable self = this;
        if (value is true)
            ((AnimationController)self).UpdatePose();
    }


    [Serialized("Is Enabled", DefaultSource = nameof(GetIsEnabledAttributeDefault))]
    private bool SerializedIsEnabled
    {
        get => Enabled;
        set => SetEnabled(value);
    }
}
