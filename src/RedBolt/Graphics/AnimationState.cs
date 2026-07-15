// Copyright (c) 2026 RedBoltFramework
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
/// Transform track applied to a scene node that is not used as an AnimatedModel bone.
/// </summary>
public struct NodeAnimationStateTrack
{
    public AnimationTrack? Track;
    public Node? Node;
    public uint KeyFrame;
}

/// <summary>
/// Output that aggregates transform tracks targeted at the same node.
/// </summary>
public struct NodeAnimationOutput
{
    public AnimationChannel Dirty;
    public Transform LocalToParent;
}

/// <summary>
/// Transform track applied to an AnimatedModel bone.
/// </summary>
public struct ModelAnimationStateTrack
{
    public AnimationTrack? Track;
    public Node? Node;
    public uint KeyFrame;
    public uint BoneIndex;
    public Bone? Bone;
}

/// <summary>
/// Output that aggregates transform tracks targeted at the same bone.
/// </summary>
public struct ModelAnimationOutput
{
    public AnimationChannel Dirty;
    public Transform LocalToParent;
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
/// Reference to an attribute or sub-attribute.
/// </summary>
public struct AnimatedAttributeReference : IEquatable<AnimatedAttributeReference>
{
    // TODO: port: the C++ weak Serializable reference is retained as a managed reference until the animation target APIs are ported.
    public Serializable? Serializable;
    public uint AttributeIndex;
    public AnimatedAttributeType AttributeType;
    public uint SubAttributeKey;

    /// <summary>
    /// Set value for the referenced attribute.
    /// </summary>
    public void SetValue(Variant value)
    {
        switch (AttributeType)
        {
            case AnimatedAttributeType.Default:
                Serializable?.SetAttribute(AttributeIndex, value.GetObject());
                break;

            case AnimatedAttributeType.NodeVariables:
                if (Serializable is Node node)
                    node.SetVarByHash(new StringHash(SubAttributeKey), value.GetObject());
                break;

            case AnimatedAttributeType.AnimatedModelMorphs:
                // TODO: port: AnimatedModel.SetMorphWeight once managed AnimatedModel is available.
                break;
        }
    }

    /// <summary>
    /// Return a hash suitable for use as a dictionary key.
    /// </summary>
    public uint ToHash() => unchecked((uint)HashCode.Combine(Serializable, AttributeIndex, SubAttributeKey));

    public bool Equals(AnimatedAttributeReference other)
        => ReferenceEquals(Serializable, other.Serializable)
            && AttributeIndex == other.AttributeIndex
            && SubAttributeKey == other.SubAttributeKey;

    public override bool Equals(object? obj) => obj is AnimatedAttributeReference other && Equals(other);

    public override int GetHashCode() => unchecked((int)ToHash());

    public static bool operator ==(AnimatedAttributeReference lhs, AnimatedAttributeReference rhs) => lhs.Equals(rhs);

    public static bool operator !=(AnimatedAttributeReference lhs, AnimatedAttributeReference rhs) => !lhs.Equals(rhs);
}

/// <summary>
/// Value track applied to a specific attribute or sub-attribute.
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
public sealed class AnimationState
{
    private readonly AnimationController? _controller;
    private AnimatedModel? _model;
    private Animation? _animation;
    private bool _tracksDirty = true;
    private uint _animationRevision;
    private bool _looped;
    private float _weight;
    private float _time;
    private AnimationBlendMode _blendingMode;
    private string _startBone = string.Empty;
    private readonly List<ModelAnimationStateTrack> _modelTracks = [];
    private readonly List<NodeAnimationStateTrack> _nodeTracks = [];
    private readonly List<AttributeAnimationStateTrack> _attributeTracks = [];

    public AnimationState(AnimationController? controller = null)
    {
        _controller = controller;
    }

    /// <summary>
    /// Connect to an AnimatedModel.
    /// </summary>
    public void ConnectToAnimatedModel(AnimatedModel? model)
    {
        _model = model;
    }

    /// <summary>
    /// Initialize static properties of the state and dirty tracks if changed.
    /// </summary>
    public void Initialize(Animation? animation, string startBone, AnimationBlendMode blendMode)
    {
        if (!ReferenceEquals(_animation, animation) || _animationRevision != (animation?.GetRevision() ?? 0u))
        {
            _animation = animation;
            _animationRevision = animation?.GetRevision() ?? 0u;
            _tracksDirty = true;
        }

        _startBone = startBone;
        _blendingMode = blendMode;
    }

    /// <summary>
    /// Update dynamic properties of the state.
    /// </summary>
    public void Update(bool looped, float time, float weight)
    {
        _looped = looped;
        _time = time;
        _weight = weight;
    }

    public bool AreTracksDirty() => _tracksDirty;

    public void MarkTracksDirty() => _tracksDirty = true;

    public void ClearAllTracks()
    {
        _modelTracks.Clear();
        _nodeTracks.Clear();
        _attributeTracks.Clear();
        _tracksDirty = true;
    }

    public void AddModelTrack(ModelAnimationStateTrack track) => _modelTracks.Add(track);

    public void AddNodeTrack(NodeAnimationStateTrack track) => _nodeTracks.Add(track);

    public void AddAttributeTrack(AttributeAnimationStateTrack track) => _attributeTracks.Add(track);

    public void OnTracksReady() => _tracksDirty = false;

    public void SetLooped(bool looped) => _looped = looped;

    public void SetWeight(float weight) => _weight = weight;

    /// <summary>
    /// Set time position. Does not fire animation triggers.
    /// </summary>
    public void SetTime(float time) => _time = time;

    public Animation? GetAnimation() => _animation;

    public AnimatedModel? GetModel() => _model;

    public string GetStartBone() => _startBone;

    public bool IsEnabled() => _weight > 0.0f;

    public bool IsLooped() => _looped;

    public float GetWeight() => _weight;

    public AnimationBlendMode GetBlendMode() => _blendingMode;

    public float GetTime() => _time;

    public float GetLength() => _animation?.Length ?? 0.0f;

    public void CalculateModelTracks(IList<ModelAnimationOutput> output)
    {
        if (_animation is null || !IsEnabled())
            return;

        for (var i = 0; i < _modelTracks.Count; ++i)
        {
            var stateTrack = _modelTracks[i];

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
                        Quaternion.Identity.Slerp(delta, rotationWeight) * output.LocalToParent.Rotation;
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

    private void CalculateAttributeTrack(
        ref Variant output, VariantAnimationTrack track, ref uint frame, float baseWeight)
    {
        if (track.KeyFrames.Count == 0)
            return;

        var weight = baseWeight * track.Weight;
        var isFullWeight = MathDefs.Equals(weight, 1.0f);
        var baseValue = track.KeyFrames[0].Value;
        var sampledValue = track.Sample(_time, _animation!.Length, _looped, ref frame);

        if (_blendingMode == AnimationBlendMode.Additive)
        {
            if (!output.IsEmpty)
                output = BlendAdditive(output, sampledValue, baseValue, weight);
        }
        else
        {
            if (!output.IsEmpty && !isFullWeight && track.Interpolation != KeyFrameInterpolation.None)
                output = output.Lerp(sampledValue, weight);
            else
                output = sampledValue;
        }
    }

    private static Variant BlendAdditive(Variant oldValue, Variant newValue, Variant baseValue, float weight)
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
            _ => oldValue
        };
    }
}
