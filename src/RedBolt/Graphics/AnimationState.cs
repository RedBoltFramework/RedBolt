// Copyright (c) 2017-2026 the rbfx project.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt;

/// <summary>
/// Animation blending mode.
/// </summary>
public enum AnimationBlendMode
{
    /// <summary>Lerp blending.</summary>
    Lerp,

    /// <summary>Additive blending based on difference from bind pose.</summary>
    Additive
}

/// <summary>
/// Transform track applied to the Node which is not used as AnimatedModel bone.
/// </summary>
public struct NodeAnimationStateTrack
{
    public AnimationTrack? Track;
    private WeakReference<Node>? _node;
    public Node? Node
    {
        readonly get => _node is not null && _node.TryGetTarget(out var node) ? node : null;
        set => _node = value is null ? null : new WeakReference<Node>(value);
    }
    public uint KeyFrame;
}

/// <summary>
/// Output that aggregates all NodeAnimationStateTrack-s targeted at the same node.
/// </summary>
public struct NodeAnimationOutput
{
    public AnimationChannel Dirty;
    public Transform LocalToParent;
}

/// <summary>
/// Transform track applied to the Bone of AnimatedModel.
/// TODO(animation): Handle Animation reload when tracks are playing?
/// TODO(animation): Do we want per-bone weights?
/// </summary>
public struct ModelAnimationStateTrack
{
    public AnimationTrack? Track;
    private WeakReference<Node>? _node;
    public Node? Node
    {
        readonly get => _node is not null && _node.TryGetTarget(out var node) ? node : null;
        set => _node = value is null ? null : new WeakReference<Node>(value);
    }
    public uint KeyFrame;
    public uint BoneIndex;
    public Bone? Bone;
}

/// <summary>
/// Output that aggregates all ModelAnimationStateTrack-s targeted at the same bone.
/// </summary>
public struct ModelAnimationOutput
{
    public AnimationChannel Dirty;
    public Transform LocalToParent;
    // Unused by AnimationState, but it's just convinient to have here.
    public Matrix3x4 LocalToComponent;
}

/// <summary>
/// Custom attribute type, used to support sub-attribute animation in special cases.
/// </summary>
public enum AnimatedAttributeType
{
    Default,
    NodeVariables,
    AnimatedModelMorphs
}

/// <summary>
/// Reference to attribute or sub-attribute;
/// </summary>
public struct AnimatedAttributeReference : IEquatable<AnimatedAttributeReference>
{
    private WeakReference<Serializable>? _serializable;
    public Serializable? Serializable
    {
        readonly get => _serializable is not null && _serializable.TryGetTarget(out var serializable) ? serializable : null;
        set => _serializable = value is null ? null : new WeakReference<Serializable>(value);
    }
    public uint AttributeIndex;
    public AnimatedAttributeType AttributeType;
    public uint SubAttributeKey;

    /// <summary>
    /// Set value for attribute. Reference must be valid.
    /// </summary>
    public readonly void SetValue(in Variant value)
    {
        var serializable = Serializable!;
        switch (AttributeType)
        {
            case AnimatedAttributeType.Default:
                serializable.SetAttribute(AttributeIndex, value.GetObject());
                break;

            case AnimatedAttributeType.NodeVariables:
                System.Diagnostics.Debug.Assert(serializable is Node);
                ((Node)serializable).SetVarByHash(new StringHash(SubAttributeKey), value.GetObject());
                break;

            case AnimatedAttributeType.AnimatedModelMorphs:
                System.Diagnostics.Debug.Assert(serializable is AnimatedModel);
                ((AnimatedModel)serializable).SetMorphWeight(checked((int)SubAttributeKey), value.GetFloat());
                break;
        }
    }

    /// <summary>
    /// Can be used as key in hash map.
    /// </summary>
    public readonly uint ToHash()
    {
        var result = 0u;
        CombineHash(ref result, Serializable is { } serializable
            ? unchecked((uint)System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(serializable))
            : 0u);
        CombineHash(ref result, AttributeIndex);
        CombineHash(ref result, SubAttributeKey);
        return result;
    }

    public bool Equals(AnimatedAttributeReference other)
        => ReferenceEquals(Serializable, other.Serializable)
            && AttributeIndex == other.AttributeIndex
            && SubAttributeKey == other.SubAttributeKey;

    public override bool Equals(object? obj) => obj is AnimatedAttributeReference other && Equals(other);

    public override int GetHashCode() => unchecked((int)ToHash());

    public static bool operator ==(AnimatedAttributeReference lhs, AnimatedAttributeReference rhs) => lhs.Equals(rhs);

    public static bool operator !=(AnimatedAttributeReference lhs, AnimatedAttributeReference rhs) => !lhs.Equals(rhs);

    private static void CombineHash(ref uint result, uint hash)
    {
        result ^= hash + 0x9e3779b9u + (result << 6) + (result >> 2);
    }
}

/// <summary>
/// Value track applied to the specific attribute or sub-attribute.
/// </summary>
public struct AttributeAnimationStateTrack
{
    public VariantAnimationTrack? Track;
    public AnimatedAttributeReference Attribute;
    public uint KeyFrame;
}

/// <summary>
/// Animation instance.
/// </summary>
public sealed class AnimationState : IDisposable
{
    /// <summary>Owner controller.</summary>
    private WeakReference<AnimationController>? _controller;
    /// <summary>Connected animated model.</summary>
    private WeakReference<AnimatedModel>? _model;
    /// <summary>Animation.</summary>
    private Animation? _animation;
    /// <summary>Whether the animation state tracks are dirty and should be updated.</summary>
    private bool _tracksDirty = true;
    /// <summary>Revision of Animation object. Used to detect changes in animation.</summary>
    private uint _animationRevision;

    /// <summary>Dynamic properties of AnimationState.</summary>
    private bool _looped;
    private float _weight;
    private float _time;
    private AnimationBlendMode _blendingMode;
    private string _startBone = string.Empty;

    /// <summary>Tracks that are actually applied to the objects.</summary>
    private readonly List<ModelAnimationStateTrack> _modelTracks = [];
    private readonly List<NodeAnimationStateTrack> _nodeTracks = [];
    private readonly List<AttributeAnimationStateTrack> _attributeTracks = [];

    /// <summary>
    /// Construct.
    /// </summary>
    public AnimationState(AnimationController? controller = null)
    {
        _controller = controller is null ? null : new WeakReference<AnimationController>(controller);
    }

    /// <summary>
    /// Destruct.
    /// </summary>
    public void Dispose()
    {
        _attributeTracks.Clear();
        _nodeTracks.Clear();
        _modelTracks.Clear();
        _startBone = string.Empty;
        _animation = null;
        _model = null;
        _controller = null;
    }

    /// <summary>
    /// Connect to AnimatedModel.
    /// </summary>
    public void ConnectToAnimatedModel(AnimatedModel? model)
    {
        _model = model is null ? null : new WeakReference<AnimatedModel>(model);
    }

    /// <summary>
    /// Initialize static properties of the state and dirty tracks if changed.
    /// </summary>
    public void Initialize(Animation? animation, string startBone, AnimationBlendMode blendMode)
    {
        if (!ReferenceEquals(_animation, animation) || _startBone != startBone || _blendingMode != blendMode)
        {
            _animation = animation;
            _startBone = startBone;
            _blendingMode = blendMode;
            MarkTracksDirty();
        }
    }

    /// <summary>
    /// Update dynamic properies of the state.
    /// </summary>
    public void Update(bool looped, float time, float weight)
    {
        SetLooped(looped);
        SetTime(time);
        SetWeight(weight);
    }

    /// <summary>
    /// Modify tracks. For internal use only.
    /// </summary>
    public bool AreTracksDirty() => _tracksDirty || (_animation is not null && _animation.GetRevision() != _animationRevision);

    public void MarkTracksDirty() => _tracksDirty = true;

    public void ClearAllTracks()
    {
        _modelTracks.Clear();
        _nodeTracks.Clear();
        _attributeTracks.Clear();
    }

    public void AddModelTrack(ModelAnimationStateTrack track) => _modelTracks.Add(track);

    public void AddNodeTrack(NodeAnimationStateTrack track) => _nodeTracks.Add(track);

    public void AddAttributeTrack(AttributeAnimationStateTrack track) => _attributeTracks.Add(track);

    public void OnTracksReady()
    {
        _tracksDirty = false;
        _animationRevision = _animation?.GetRevision() ?? 0u;
        GetModel()?.MarkAnimationDirty();
    }

    /// <summary>
    /// Set looping enabled/disabled.
    /// </summary>
    public void SetLooped(bool looped)
    {
        if (_looped != looped)
        {
            _looped = looped;
            GetModel()?.MarkAnimationDirty();
        }
    }

    /// <summary>
    /// Set blending weight.
    /// </summary>
    public void SetWeight(float weight)
    {
        if (_animation is null)
            return;

        weight = Math.Clamp(weight, 0.0f, 1.0f);
        if (weight != _weight)
        {
            _weight = weight;
            GetModel()?.MarkAnimationDirty();
        }
    }

    /// <summary>
    /// Set time position. Does not fire animation triggers.
    /// </summary>
    public void SetTime(float time)
    {
        if (_animation is null)
            return;

        time = Math.Clamp(time, 0.0f, _animation.Length);
        if (time != _time)
        {
            _time = time;
            GetModel()?.MarkAnimationDirty();
        }
    }

    /// <summary>
    /// Return animation.
    /// </summary>
    public Animation? GetAnimation() => _animation;

    /// <summary>
    /// Return connected animated model.
    /// </summary>
    public AnimatedModel? GetModel() => _model is not null && _model.TryGetTarget(out var model) ? model : null;

    /// <summary>
    /// Return name of start bone.
    /// </summary>
    public string GetStartBone() => _startBone;

    /// <summary>
    /// Return whether weight is nonzero.
    /// </summary>
    public bool IsEnabled() => _weight > 0.0f;

    /// <summary>
    /// Return whether looped.
    /// </summary>
    public bool IsLooped() => _looped;

    /// <summary>
    /// Return blending weight.
    /// </summary>
    public float GetWeight() => _weight;

    /// <summary>
    /// Return blending mode.
    /// </summary>
    public AnimationBlendMode GetBlendMode() => _blendingMode;

    /// <summary>
    /// Return time position.
    /// </summary>
    public float GetTime() => _time;

    /// <summary>
    /// Return animation length.
    /// </summary>
    public float GetLength() => _animation?.Length ?? 0.0f;

    /// <summary>
    /// Calculate animation for the model skeleton.
    /// </summary>
    public void CalculateModelTracks(IList<ModelAnimationOutput> output)
    {
        if (_animation is null || !IsEnabled())
            return;

        for (var i = 0; i < _modelTracks.Count; ++i)
        {
            var stateTrack = _modelTracks[i];
            System.Diagnostics.Debug.Assert(output.Count > checked((int)stateTrack.BoneIndex));

            // Do not apply if the bone has animation disabled
            if (!stateTrack.Bone!.Animated)
                continue;

            var trackOutput = output[(int)stateTrack.BoneIndex];
            var keyFrame = stateTrack.KeyFrame;
            CalculateTransformTrack(ref trackOutput, stateTrack.Track!, ref keyFrame, _weight);
            stateTrack.KeyFrame = keyFrame;
            _modelTracks[i] = stateTrack;
            output[(int)stateTrack.BoneIndex] = trackOutput;
        }
    }

    /// <summary>
    /// Apply animation to a scene node hierarchy.
    /// </summary>
    public void CalculateNodeTracks(IDictionary<Node, NodeAnimationOutput> output)
    {
        if (_animation is null || !IsEnabled())
            return;

        for (var i = 0; i < _nodeTracks.Count; ++i)
        {
            var stateTrack = _nodeTracks[i];
            var node = stateTrack.Node!;
            if (!output.TryGetValue(node, out var trackOutput))
                trackOutput = new NodeAnimationOutput { LocalToParent = Transform.Identity };

            var keyFrame = stateTrack.KeyFrame;
            CalculateTransformTrack(ref trackOutput, stateTrack.Track!, ref keyFrame, _weight);
            stateTrack.KeyFrame = keyFrame;
            _nodeTracks[i] = stateTrack;
            output[node] = trackOutput;
        }
    }

    /// <summary>
    /// Apply animation to attributes.
    /// </summary>
    public void CalculateAttributeTracks(IDictionary<AnimatedAttributeReference, Variant> output)
    {
        if (_animation is null || !IsEnabled())
            return;

        for (var i = 0; i < _attributeTracks.Count; ++i)
        {
            var stateTrack = _attributeTracks[i];
            output.TryGetValue(stateTrack.Attribute, out var trackOutput);
            var keyFrame = stateTrack.KeyFrame;
            CalculateAttributeTrack(ref trackOutput, stateTrack.Track!, ref keyFrame, _weight);
            stateTrack.KeyFrame = keyFrame;
            _attributeTracks[i] = stateTrack;
            output[stateTrack.Attribute] = trackOutput;
        }
    }

    /// <summary>
    /// Apply value of transformation track to the output.
    /// </summary>
    private void CalculateTransformTrack(
        ref NodeAnimationOutput output, AnimationTrack track, ref uint frame, float baseWeight)
    {
        if (track.KeyFrames.Count == 0)
            return;

        var positionWeight = baseWeight * track.PositionWeight;
        var rotationWeight = baseWeight * track.RotationWeight;
        var scaleWeight = baseWeight * track.ScaleWeight;

        var isFullPositionWeight = MathDefs.Equals(positionWeight, 1.0f);
        var isFullRotationWeight = MathDefs.Equals(rotationWeight, 1.0f);
        var isFullScaleWeight = MathDefs.Equals(scaleWeight, 1.0f);

        var baseValue = track.KeyFrames[0];

        var sampledValue = Transform.Identity;
        track.Sample(_time, _animation!.Length, _looped, ref frame, ref sampledValue);

        if (_blendingMode == AnimationBlendMode.Additive)
        {
            // In additive mode, check for output being already initialzed
            if ((track.ChannelMask & output.Dirty & AnimationChannel.Position) != 0)
            {
                var delta = sampledValue.Position - baseValue.Position;
                output.LocalToParent.Position += delta * positionWeight;
            }

            if ((track.ChannelMask & output.Dirty & AnimationChannel.Rotation) != 0)
            {
                var delta = sampledValue.Rotation * baseValue.Rotation.Inverse;
                if (isFullRotationWeight)
                    output.LocalToParent.Rotation = delta * output.LocalToParent.Rotation;
                else
                {
                    output.LocalToParent.Rotation =
                        Quaternion.IDENTITY.Slerp(delta, rotationWeight) * output.LocalToParent.Rotation;
                }
            }

            if ((track.ChannelMask & output.Dirty & AnimationChannel.Scale) != 0)
            {
                var delta = sampledValue.Scale - baseValue.Scale;
                output.LocalToParent.Scale += delta * scaleWeight;
            }
        }
        else
        {
            // In interpolation mode, disable interpolation if output is not initialzed yet
            if ((track.ChannelMask & AnimationChannel.Position) != 0)
            {
                if (!isFullPositionWeight && (output.Dirty & AnimationChannel.Position) != 0)
                {
                    output.LocalToParent.Position =
                        output.LocalToParent.Position.Lerp(sampledValue.Position, positionWeight);
                }
                else
                {
                    output.Dirty |= AnimationChannel.Position;
                    output.LocalToParent.Position = sampledValue.Position;
                }
            }

            if ((track.ChannelMask & AnimationChannel.Rotation) != 0)
            {
                if (!isFullRotationWeight && (output.Dirty & AnimationChannel.Rotation) != 0)
                {
                    output.LocalToParent.Rotation =
                        output.LocalToParent.Rotation.Slerp(sampledValue.Rotation, rotationWeight);
                }
                else
                {
                    output.Dirty |= AnimationChannel.Rotation;
                    output.LocalToParent.Rotation = sampledValue.Rotation;
                }
            }

            if ((track.ChannelMask & AnimationChannel.Scale) != 0)
            {
                if (!isFullScaleWeight && (output.Dirty & AnimationChannel.Scale) != 0)
                    output.LocalToParent.Scale = output.LocalToParent.Scale.Lerp(sampledValue.Scale, scaleWeight);
                else
                {
                    output.Dirty |= AnimationChannel.Scale;
                    output.LocalToParent.Scale = sampledValue.Scale;
                }
            }
        }
    }

    private void CalculateTransformTrack(
        ref ModelAnimationOutput output, AnimationTrack track, ref uint frame, float baseWeight)
    {
        var nodeOutput = new NodeAnimationOutput
        {
            Dirty = output.Dirty,
            LocalToParent = output.LocalToParent
        };
        CalculateTransformTrack(ref nodeOutput, track, ref frame, baseWeight);
        output.Dirty = nodeOutput.Dirty;
        output.LocalToParent = nodeOutput.LocalToParent;
    }

    /// <summary>
    /// Apply single attribute track to target object. Key frame hint is updated on call.
    /// </summary>
    private void CalculateAttributeTrack(
        ref Variant output, VariantAnimationTrack track, ref uint frame, float baseWeight)
    {
        if (track.KeyFrames.Count == 0)
            return;

        var weight = baseWeight * track.Weight;
        var isFullWeight = MathDefs.Equals(weight, 1.0f);
        var baseValue = track.KeyFrames[0].Value;
        var sampledValue = track.Sample(_time, _animation!.Length, _looped, ref frame);
        try
        {
            Variant replacement;
            if (_blendingMode == AnimationBlendMode.Additive)
            {
                if (output.IsEmpty)
                    return;
                replacement = BlendAdditive(output, sampledValue, baseValue, weight);
            }
            else
            {
                replacement = !output.IsEmpty && !isFullWeight && track.Interpolation != KeyFrameInterpolation.None
                    ? output.Lerp(sampledValue, weight)
                    : sampledValue.Clone();
            }

            output.Dispose();
            output = replacement;
        }
        finally
        {
            sampledValue.Dispose();
        }
    }

    private static Variant BlendAdditive(in Variant oldValue, in Variant newValue, in Variant baseValue, float weight)
    {
        return newValue.Type switch
        {
            VariantType.Float => new Variant(oldValue.GetFloat() + (newValue.GetFloat() - baseValue.GetFloat()) * weight),
            VariantType.Double => new Variant(oldValue.GetDouble() + (newValue.GetDouble() - baseValue.GetDouble()) * weight),
            VariantType.Int => new Variant((int)(oldValue.GetInt() + (newValue.GetInt() - baseValue.GetInt()) * weight)),
            VariantType.Int64 => new Variant((long)(oldValue.GetInt64() + (newValue.GetInt64() - baseValue.GetInt64()) * weight)),
            VariantType.Vector2 => new Variant(oldValue.GetVector2() + (newValue.GetVector2() - baseValue.GetVector2()) * weight),
            VariantType.Vector3 => new Variant(oldValue.GetVector3() + (newValue.GetVector3() - baseValue.GetVector3()) * weight),
            VariantType.Vector4 => new Variant(oldValue.GetVector4() + (newValue.GetVector4() - baseValue.GetVector4()) * weight),
            VariantType.Quaternion => new Variant(oldValue.GetQuaternion() * Quaternion.IDENTITY.Slerp(
                newValue.GetQuaternion() * baseValue.GetQuaternion().Inverse, weight)),
            VariantType.Color => new Variant(oldValue.GetColor() + (newValue.GetColor() - baseValue.GetColor()) * weight),
            VariantType.Vector2i => new Variant(oldValue.GetIntVector2() + new Vector2i(
                (int)MathF.Round((newValue.GetIntVector2().X - baseValue.GetIntVector2().X) * weight),
                (int)MathF.Round((newValue.GetIntVector2().Y - baseValue.GetIntVector2().Y) * weight))),
            VariantType.Vector3i => new Variant(oldValue.GetIntVector3() + new Vector3i(
                (int)MathF.Round((newValue.GetIntVector3().X - baseValue.GetIntVector3().X) * weight),
                (int)MathF.Round((newValue.GetIntVector3().Y - baseValue.GetIntVector3().Y) * weight),
                (int)MathF.Round((newValue.GetIntVector3().Z - baseValue.GetIntVector3().Z) * weight))),
            _ => oldValue.Clone()
        };
    }
}
