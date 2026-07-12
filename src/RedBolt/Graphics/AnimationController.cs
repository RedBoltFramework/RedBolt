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

namespace RedBolt;

/// <summary>
/// Component that drives an AnimatedModel's animations.
/// </summary>
public class AnimationController : AnimationStateSource
{
    private const uint InvalidAnimationIndex = uint.MaxValue;

    /// <summary>
    /// Construct.
    /// </summary>
    public AnimationController()
    {
        RegisterAttribute(
            "Reset Skeleton",
            typeof(bool),
            self => ((AnimationController)self).IsSkeletonReset(),
            (self, value) => ((AnimationController)self).SetSkeletonReset(value is bool resetSkeleton && resetSkeleton),
            false);
        RegisterAttribute(
            "Animations",
            typeof(VariantVector),
            self => ((AnimationController)self).GetAnimationsAttr(),
            (self, value) => ((AnimationController)self).SetAnimationsAttr(value as VariantVector ?? new VariantVector()),
            new VariantVector());
    }

    /// <summary>
    /// Register object factory.
    /// </summary>
    public static void RegisterObject(Context context)
    {
        // TODO: port: context->AddFactoryReflection<AnimationController>(Category_Logic);
        // TODO: port: URHO3D_ACTION_STATIC_LABEL("Update Pose", UpdatePose, "");
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

            _animationStates.Remove(_animations[i].State!);
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
        {
            // TODO: port: AnimatedModel.ResetBones() once managed AnimatedModel is available.
        }

        // Node and attribute animations need to be applied manually
        CommitNodeAndAttributeAnimations();
    }

    /// <summary>
    /// Smoothly replace existing animations with animations from external source.
    /// If layers are specified, only these layers will be affected.
    /// </summary>
    public void ReplaceAnimations(
        ReadOnlySpan<AnimationParameters> newAnimations, float elapsedTime, float fadeTime,
        ReadOnlySpan<uint> layers = default)
    {
        // Upload new parameters and correct them according to elapsed time, some states may be removed
        var numNewAnimations = newAnimations.Length;
        _tempAnimations.Clear();
        for (var i = 0; i < numNewAnimations; ++i)
        {
            var instance = new AnimationInstance
            {
                Params = new AnimationParameters(newAnimations[i]),
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
                    ? Math.Max(newParams.TargetWeightDelay, fadeTime)
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
                _animations.Add(instance);
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
        var instanceIndex = 0u;
        for (var i = 0; i < _animations.Count; ++i)
        {
            if (ReferenceEquals(_animations[i].Params.GetAnimation(), parameters.GetAnimation()))
                ++instanceIndex;
        }

        var instance = new AnimationInstance
        {
            Params = new AnimationParameters(parameters),
            State = new AnimationState(this)
        };
        instance.Params.InstanceIndex = instanceIndex;

        EnsureStateInitialized(instance.State!, instance.Params);
        _animations.Add(instance);
        _animationStates.Add(instance.State!);

        _animationStatesDirty = true;
        _revisionDirty = true;
    }

    public void UpdateAnimation(uint index, AnimationParameters parameters)
    {
        if (index >= _animations.Count)
        {
            // TODO: port: URHO3D_ASSERTLOG(0, "Out-of-bounds animation index in AnimationController::UpdateAnimation");
            return;
        }

        var instance = _animations[(int)index];
        if (!ReferenceEquals(instance.Params.GetAnimation(), parameters.GetAnimation())
            || instance.Params.InstanceIndex != parameters.InstanceIndex)
        {
            // TODO: port: URHO3D_ASSERTLOG(0, "Animation and instance index cannot be changed by AnimationController::UpdateAnimation");
            return;
        }

        EnsureStateInitialized(instance.State!, parameters);
        if (instance.Params.LayerIndex != parameters.LayerIndex)
            _animationStatesDirty = true;

        if (!instance.Params.Equals(parameters))
        {
            instance.Params = new AnimationParameters(parameters);
            _revisionDirty = true;
        }
    }

    public void RemoveAnimation(uint index)
    {
        if (index >= _animations.Count)
        {
            // TODO: port: URHO3D_ASSERTLOG(0, "Out-of-bounds animation index in AnimationController::UpdateAnimation");
            return;
        }

        _animationStates.Remove(_animations[(int)index].State!);
        _animations.RemoveAt((int)index);
        _animationStatesDirty = true;
        _revisionDirty = true;
    }

    public void GetAnimationParameters(IList<AnimationParameters> result)
    {
        ArgumentNullException.ThrowIfNull(result);
        result.Clear();
        for (var i = 0; i < _animations.Count; ++i)
            result.Add(new AnimationParameters(_animations[i].Params));
    }

    public List<AnimationParameters> GetAnimationParameters()
    {
        var result = new List<AnimationParameters>(_animations.Count);
        GetAnimationParameters(result);
        return result;
    }

    public uint GetNumAnimations() => checked((uint)_animations.Count);

    public uint GetAnimationLayer(uint index)
    {
        return index < _animations.Count ? _animations[(int)index].Params.LayerIndex : 0u;
    }

    public AnimationParameters GetAnimationParameters(uint index)
    {
        return index < _animations.Count
            ? new AnimationParameters(_animations[(int)index].Params)
            : new AnimationParameters(AnimationParameters.Empty);
    }

    public uint GetRevision() => _revision;

    public void UpdatePose()
    {
        Update(0.0f);
    }

    /// <summary>
    /// Manage played animations on high level.
    /// </summary>
    public uint FindLastAnimation(Animation? animation, uint layer = InvalidAnimationIndex)
    {
        for (var i = _animations.Count - 1; i >= 0; --i)
        {
            var parameters = _animations[i].Params;
            if (animation is not null && !ReferenceEquals(parameters.GetAnimation(), animation))
                continue;
            if (layer != InvalidAnimationIndex && parameters.LayerIndex != layer)
                continue;
            return (uint)i;
        }

        return InvalidAnimationIndex;
    }

    public AnimationParameters? GetLastAnimationParameters(Animation? animation, uint layer = InvalidAnimationIndex)
    {
        var index = FindLastAnimation(animation, layer);
        return index != InvalidAnimationIndex ? _animations[(int)index].Params : null;
    }

    public AnimationParameters? GetMutableLastAnimationParameters(Animation? animation, uint layer = InvalidAnimationIndex)
    {
        var index = FindLastAnimation(animation, layer);
        return index != InvalidAnimationIndex ? _animations[(int)index].Params : null;
    }

    public bool IsPlaying(Animation? animation) => FindLastAnimation(animation) != InvalidAnimationIndex;

    public uint PlayNew(AnimationParameters parameters, float fadeInTime = 0.0f)
    {
        var adjustedParameters = new AnimationParameters(parameters);
        if (fadeInTime > 0.0f)
        {
            adjustedParameters.WeightValue = 0.0f;
            adjustedParameters.TargetWeightDelay = fadeInTime;
        }

        AddAnimation(adjustedParameters);
        return checked((uint)(_animations.Count - 1));
    }

    public uint PlayNewExclusive(AnimationParameters parameters, float fadeInTime = 0.0f)
    {
        var index = PlayNew(parameters, fadeInTime);
        StopLayerExcept(parameters.LayerIndex, index, fadeInTime);
        return index;
    }

    public uint PlayExisting(AnimationParameters parameters, float fadeInTime = 0.0f)
    {
        var index = FindLastAnimation(parameters.GetAnimation(), parameters.LayerIndex);
        if (index == InvalidAnimationIndex)
        {
            PlayNew(parameters, fadeInTime);
            return checked((uint)(_animations.Count - 1));
        }

        var existingParameters = GetAnimationParameters(index);
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

    public uint PlayExistingExclusive(AnimationParameters parameters, float fadeInTime = 0.0f)
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

        var parameters = GetAnimationParameters(index);
        parameters.TargetWeight = targetWeight;
        parameters.TargetWeightDelay = fadeTime;
        UpdateAnimation(index, parameters);
    }

    public void Stop(Animation? animation, float fadeTime = 0.0f)
    {
        var index = FindLastAnimation(animation);
        if (index == InvalidAnimationIndex)
            return;

        var parameters = GetAnimationParameters(index);
        if (parameters.RemoveDelayed(fadeTime))
            UpdateAnimation(index, parameters);
    }

    public void StopLayer(uint layer, float fadeTime = 0.0f)
    {
        for (var index = 0u; index < _animations.Count; ++index)
        {
            if (_animations[(int)index].Params.LayerIndex != layer)
                continue;

            var parameters = GetAnimationParameters(index);
            if (parameters.RemoveDelayed(fadeTime))
                UpdateAnimation(index, parameters);
        }
    }

    public void StopLayerExcept(uint layer, uint exceptionIndex, float fadeTime = 0.0f)
    {
        for (var index = 0u; index < _animations.Count; ++index)
        {
            if (_animations[(int)index].Params.LayerIndex != layer || index == exceptionIndex)
                continue;

            var parameters = GetAnimationParameters(index);
            if (parameters.RemoveDelayed(fadeTime))
                UpdateAnimation(index, parameters);
        }
    }

    public void StopAll(float fadeTime = 0.0f)
    {
        for (var index = 0u; index < _animations.Count; ++index)
        {
            var parameters = GetAnimationParameters(index);
            if (parameters.RemoveDelayed(fadeTime))
                UpdateAnimation(index, parameters);
        }
    }

    public bool UpdateAnimationTime(Animation? animation, float time)
    {
        var index = FindLastAnimation(animation);
        if (index == InvalidAnimationIndex)
            return false;

        var parameters = GetAnimationParameters(index);
        parameters.SetTime(time);
        UpdateAnimation(index, parameters);
        return true;
    }

    public bool UpdateAnimationWeight(Animation? animation, float weight, float fadeTime = 0.0f)
    {
        var index = FindLastAnimation(animation);
        if (index == InvalidAnimationIndex)
            return false;

        var parameters = GetAnimationParameters(index);
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

        var parameters = GetAnimationParameters(index);
        parameters.SpeedValue = speed;
        UpdateAnimation(index, parameters);
        return true;
    }

    /// <summary>
    /// Deprecated.
    /// </summary>
    public bool Play(string name, byte layer, bool looped, float fadeTime = 0.0f)
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

    public bool PlayExclusive(string name, byte layer, bool looped, float fadeTime = 0.0f)
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

    public bool IsSkeletonReset() => _resetSkeleton;

    public void SetSkeletonReset(bool resetSkeleton) => _resetSkeleton = resetSkeleton;

    /// <summary>
    /// Set animation parameters attribute.
    /// </summary>
    public void SetAnimationsAttr(VariantVector value)
    {
        _animations.Clear();
        _animationStates.Clear();
        _revisionDirty = true;

        if (value.Count == 0 || value[0].Type != VariantType.Int)
            return;

        var requestedCount = Math.Max(0, value[0].GetInt());
        var availableCount = (value.Count - 1) / checked((int)AnimationParameters.NumVariants);
        var loadedCount = Math.Min(requestedCount, availableCount);

        for (var i = 0; i < loadedCount; ++i)
        {
            var variants = new Variant[AnimationParameters.NumVariants];
            var offset = 1 + i * checked((int)AnimationParameters.NumVariants);
            for (var j = 0; j < variants.Length; ++j)
                variants[j] = value[offset + j];
            AddAnimation(AnimationParameters.FromVariantSpan(variants));
        }

        for (var i = loadedCount; i < requestedCount; ++i)
            AddAnimation(new AnimationParameters((Animation?)null));
    }

    /// <summary>
    /// Return animation parameters attribute.
    /// </summary>
    public VariantVector GetAnimationsAttr()
    {
        var result = new VariantVector
        {
            new Variant(checked((uint)_animations.Count))
        };

        for (var i = 0; i < _animations.Count; ++i)
        {
            var variants = new Variant[AnimationParameters.NumVariants];
            _animations[i].Params.ToVariantSpan(variants);
            for (var j = 0; j < variants.Length; ++j)
                result.Add(variants[j]);
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
    protected internal override void OnSceneSet(Scene? previousScene, Scene? scene)
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
        _tempAnimations.AddRange(_animations);
        _tempAnimations.Sort(static (lhs, rhs) => lhs.Params.LayerIndex.CompareTo(rhs.Params.LayerIndex));

        _animationStates.Clear();
        for (var i = 0; i < _tempAnimations.Count; ++i)
            _animationStates.Add(_tempAnimations[i].State!);

        _animationStatesDirty = false;
    }

    private void UpdateAnimationStateTracks(AnimationState state)
    {
        // TODO: port: UpdateAnimationStateTracks requires managed AnimatedModel, skeleton, and attribute reflection paths.
    }

    private void ConnectToAnimatedModel()
    {
        // TODO: port: AnimatedModel.ConnectToAnimationStateSource and model state connection are not available yet.
    }

    private AnimatedAttributeReference ParseAnimatablePath(string path, Node? startNode)
    {
        // TODO: port: ParseAnimatablePath requires the managed animatable attribute path resolver.
        return default;
    }

    private Node? GetTrackNodeByNameHash(StringHash trackNameHash, Node? startBone)
    {
        // TODO: port: track-node lookup requires the managed AnimatedModel bone hierarchy.
        return null;
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
            foreach (var trigger in animation.Triggers)
            {
                if (delta.ContainsExcludingBegin(Math.Min(trigger.Time, length)))
                    _pendingTriggers.Add((animation, trigger));
            }
        }

        return isCompleted ? scaledOvertime / parameters.SpeedValue : null;
    }

    private void ApplyInstanceAutoFadeOut(AnimationParameters parameters, float overtime)
    {
        if (parameters.TargetWeight == 0.0f || parameters.AutoFadeOutTime == 0.0f)
            return;

        parameters.TargetWeight = 0.0f;
        parameters.TargetWeightDelay = Math.Max(0.0f, parameters.AutoFadeOutTime - overtime);
        parameters.RemoveOnZeroWeight = true;

        var fraction = Math.Min(1.0f, overtime / parameters.AutoFadeOutTime);
        parameters.WeightValue = MathDefs.Lerp(parameters.WeightValue, 0.0f, fraction);
    }

    private void UpdateInstanceWeight(AnimationParameters parameters, float timeStep)
    {
        if (parameters.TargetWeightDelay <= 0.0f)
        {
            parameters.WeightValue = parameters.TargetWeight;
            return;
        }

        var fraction = Math.Min(1.0f, timeStep / parameters.TargetWeightDelay);
        parameters.WeightValue = MathDefs.Lerp(parameters.WeightValue, parameters.TargetWeight, fraction);
        parameters.TargetWeightDelay = Math.Max(0.0f, parameters.TargetWeightDelay - timeStep);
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
        // TODO: port: node and attribute animation commits require managed animatable targets.
    }

    private void SendTriggerEvents()
    {
        // TODO: port: trigger event dispatch will be completed with the managed DrawableEvents integration.
    }

    /// <summary>
    /// Currently playing animations.
    /// </summary>
    private sealed class AnimationInstance
    {
        public required AnimationParameters Params;
        public AnimationState? State;
    }

    private readonly List<AnimationInstance> _animations = [];
    private readonly List<AnimationInstance> _tempAnimations = [];
    private readonly List<(Animation Animation, AnimationTriggerPoint Trigger)> _pendingTriggers = [];
    private uint _revision;
    private bool _animationStatesDirty;
    private bool _revisionDirty;
    private readonly Dictionary<Node, NodeAnimationOutput> _animatedNodes = [];
    private readonly Dictionary<AnimatedAttributeReference, Variant> _animatedAttributes = [];
}
