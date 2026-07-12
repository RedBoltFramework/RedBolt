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
        // TODO: port: AnimatedAttributeReference::SetValue requires AnimatedModel and sub-attribute reflection APIs not available yet.
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
    // TODO: port: AnimationController and AnimatedModel are not available yet; preserve the C++ weak references as opaque managed references.
    private readonly object? _controller;
    private object? _model;
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

    public AnimationState(object? controller = null)
    {
        _controller = controller;
    }

    /// <summary>
    /// Connect to an AnimatedModel.
    /// </summary>
    public void ConnectToAnimatedModel(object? model)
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

    public object? GetModel() => _model;

    public string GetStartBone() => _startBone;

    public bool IsEnabled() => _weight > 0.0f;

    public bool IsLooped() => _looped;

    public float GetWeight() => _weight;

    public AnimationBlendMode GetBlendMode() => _blendingMode;

    public float GetTime() => _time;

    public float GetLength() => _animation?.Length ?? 0.0f;

    public void CalculateModelTracks(IList<ModelAnimationOutput> output)
    {
        // TODO: port: AnimatedModel skeleton access and animation output blending are not available yet.
    }

    public void CalculateNodeTracks(IDictionary<Node, NodeAnimationOutput> output)
    {
        // TODO: port: node animation output blending will be completed with AnimatedModel animation support.
    }

    public void CalculateAttributeTracks(IDictionary<AnimatedAttributeReference, Variant> output)
    {
        // TODO: port: attribute animation target reflection is not available yet.
    }

    private void CalculateTransformTrack(
        ref NodeAnimationOutput output, AnimationTrack track, ref uint frame, float baseWeight)
    {
        // TODO: port: complete when animation output blending is available.
    }

    private void CalculateAttributeTrack(
        ref Variant output, VariantAnimationTrack track, ref uint frame, float baseWeight)
    {
        // TODO: port: complete when Variant animation target reflection is available.
    }
}
