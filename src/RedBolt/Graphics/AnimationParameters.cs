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
/// State and parameters of playing Animation.
/// </summary>
public sealed class AnimationParameters : IEquatable<AnimationParameters?>, ICloneable
{
    public AnimationParameters()
    {
        _time = new WrappedScalar<float>(0.0f, 0.0f, MathDefs.LargeValue);
    }

    public AnimationParameters(Animation? animation)
    {
        _animation = animation;
        _animationName = animation?.AnimationNameHash ?? StringHash.Empty;
        _time = new WrappedScalar<float>(0.0f, 0.0f, animation?.Length ?? MathDefs.LargeValue);
    }

    public AnimationParameters(Animation? animation, float minTime, float maxTime)
    {
        _animation = animation;
        _animationName = animation?.AnimationNameHash ?? StringHash.Empty;
        _time = new WrappedScalar<float>(0.0f, minTime, maxTime);
    }

    public AnimationParameters(AnimationParameters rhs)
    {
        ArgumentNullException.ThrowIfNull(rhs);

        InstanceIndex = rhs.InstanceIndex;
        IsLooped = rhs.IsLooped;
        RemoveOnCompletion = rhs.RemoveOnCompletion;
        LayerIndex = rhs.LayerIndex;
        BlendMode = rhs.BlendMode;
        StartBoneName = rhs.StartBoneName;
        AutoFadeOutTime = rhs.AutoFadeOutTime;
        SpeedValue = rhs.SpeedValue;
        RemoveOnZeroWeight = rhs.RemoveOnZeroWeight;
        WeightValue = rhs.WeightValue;
        TargetWeight = rhs.TargetWeight;
        TargetWeightDelay = rhs.TargetWeightDelay;
        Removed = rhs.Removed;
        Merged = rhs.Merged;
        _animation = rhs._animation;
        _animationName = rhs._animationName;
        _time = new WrappedScalar<float>(rhs._time.Value(), rhs._time.Min(), rhs._time.Max());
    }

    public object Clone() => new AnimationParameters(this);

    private static Animation? GetAnimation(string animationName)
    {
        var context = Context.Instance;
        return context.GetSubsystem<ResourceCache>()!.GetResource<Animation>(animationName);
    }

    public AnimationParameters(string animationName)
        : this(GetAnimation(animationName))
    {
    }

    public AnimationParameters(string animationName, float minTime, float maxTime)
        : this(GetAnimation(animationName), minTime, maxTime)
    {
    }

    /// <summary>
    /// Helper utility to fade animation out and remove it later.
    /// </summary>
    public bool RemoveDelayed(float fadeTime)
    {
        var alreadyRemoved = Math.Abs(TargetWeight) < MathDefs.LargeEpsilon;
        var effectiveDelay = alreadyRemoved ? Math.Min(TargetWeightDelay, fadeTime) : fadeTime;
        var changed = !RemoveOnZeroWeight || TargetWeightDelay != effectiveDelay || TargetWeight != 0.0f;

        RemoveOnZeroWeight = true;
        TargetWeightDelay = effectiveDelay;
        TargetWeight = 0.0f;
        return changed;
    }

    /// <summary>
    /// Factory helper.
    /// </summary>
    public AnimationParameters Looped(bool value = true)
    {
        IsLooped = value;
        return this;
    }

    public AnimationParameters StartBone(string startBone)
    {
        StartBoneName = startBone;
        return this;
    }

    public AnimationParameters Layer(uint layer)
    {
        LayerIndex = layer;
        return this;
    }

    public AnimationParameters Time(float time)
    {
        _time.Set(time);
        return this;
    }

    public AnimationParameters TimeRange(float minTime, float maxTime)
    {
        _time = new WrappedScalar<float>(_time.Value(), minTime, maxTime);
        return this;
    }

    public AnimationParameters Additive(bool value = true)
    {
        BlendMode = value ? AnimationBlendMode.Additive : AnimationBlendMode.Lerp;
        return this;
    }

    public AnimationParameters Weight(float weight)
    {
        WeightValue = weight;
        TargetWeight = weight;
        return this;
    }

    public AnimationParameters Speed(float speed)
    {
        SpeedValue = speed;
        return this;
    }

    public AnimationParameters AutoFadeOut(float fadeOut)
    {
        AutoFadeOutTime = fadeOut;
        return this;
    }

    public AnimationParameters KeepOnCompletion(bool value = true)
    {
        RemoveOnCompletion = !value;
        return this;
    }

    public AnimationParameters KeepOnZeroWeight(bool value = true)
    {
        RemoveOnZeroWeight = !value;
        return this;
    }

    /// <summary>
    /// Getters for read-only properties. Use the constructor to set Animation to the parameters.
    /// </summary>
    public Animation? GetAnimation() => _animation;

    public StringHash GetAnimationName() => _animationName;

    public WrappedScalar<float> GetAnimationTime() => _time;

    /// <summary>
    /// Time operations.
    /// </summary>
    public float GetTime() => _time.Value();

    public void SetTime(float time) => _time.Set(time);

    public void SetTimeRange(float minTime, float maxTime)
    {
        _time = new WrappedScalar<float>(_time.Value(), minTime, maxTime);
    }

    public void SetDefaultTimeRange()
    {
        _time = new WrappedScalar<float>(_time.Value(), 0.0f, _animation?.Length ?? MathDefs.LargeValue);
    }

    /// <summary>
    /// Update the animation.
    /// </summary>
    public WrappedScalarRange<float> Update(float scaledTimeStep)
    {
        return IsLooped
            ? _time.UpdateWrapped(scaledTimeStep)
            : _time.UpdateClamped(scaledTimeStep, true);
    }

    public uint InstanceIndex;

    /// <summary>
    /// Static animation parameters that change rarely.
    /// </summary>
    public bool IsLooped;
    public bool RemoveOnCompletion = true;
    public uint LayerIndex;
    public AnimationBlendMode BlendMode;
    public string StartBoneName = string.Empty;
    public float AutoFadeOutTime;

    /// <summary>
    /// Dynamic animation parameters that change often.
    /// </summary>
    public float SpeedValue = 1.0f;
    public bool RemoveOnZeroWeight;
    public float WeightValue = 1.0f;
    public float TargetWeight = 1.0f;
    public float TargetWeightDelay;

    /// <summary>
    /// Internal flags for easier algorithms. Never stored between API calls.
    /// </summary>
    public bool Removed;
    public bool Merged;

    /// <summary>
    /// Number of serialized animation parameter variants.
    /// </summary>
    public const uint NumVariants = 15;

    // C# omits the Context* parameter.
    public static AnimationParameters FromVariantSpan(ReadOnlySpan<Variant> variants)
    {
        var result = new AnimationParameters();
        var index = 0;

        var animationName = variants[index++].GetResourceRef().Name;
        result._animation = GetAnimation(animationName);
        result._animationName = result._animation?.AnimationNameHash ?? StringHash.Empty;

        result.IsLooped = variants[index++].GetBool();
        result.RemoveOnCompletion = variants[index++].GetBool();
        result.LayerIndex = variants[index++].GetUInt();
        result.BlendMode = (AnimationBlendMode)variants[index++].GetUInt();
        result.StartBoneName = variants[index++].GetString();
        result.AutoFadeOutTime = variants[index++].GetFloat();

        var time = variants[index++].GetFloat();
        var minTime = variants[index++].GetFloat();
        var maxTime = variants[index++].GetFloat();
        var effectiveMaxTime = maxTime == MathDefs.LargeValue && result._animation is not null
            ? result._animation.Length
            : maxTime;
        result._time = new WrappedScalar<float>(time, minTime, effectiveMaxTime);

        result.SpeedValue = variants[index++].GetFloat();
        result.RemoveOnZeroWeight = variants[index++].GetBool();
        result.WeightValue = variants[index++].GetFloat();
        result.TargetWeight = variants[index++].GetFloat();
        result.TargetWeightDelay = variants[index++].GetFloat();

        return result;
    }

    public void ToVariantSpan(Span<Variant> variants)
    {
        var index = 0;
        variants[index++] = new Variant(new ResourceRef(new StringHash("Animation"), _animation?.Name ?? string.Empty));
        variants[index++] = new Variant(IsLooped);
        variants[index++] = new Variant(RemoveOnCompletion);
        variants[index++] = new Variant(LayerIndex);
        variants[index++] = new Variant((uint)BlendMode);
        variants[index++] = new Variant(StartBoneName);
        variants[index++] = new Variant(AutoFadeOutTime);
        variants[index++] = new Variant(_time.Value());
        variants[index++] = new Variant(_time.Min());
        variants[index++] = new Variant(_time.Max());
        variants[index++] = new Variant(SpeedValue);
        variants[index++] = new Variant(RemoveOnZeroWeight);
        variants[index++] = new Variant(WeightValue);
        variants[index++] = new Variant(TargetWeight);
        variants[index++] = new Variant(TargetWeightDelay);
    }

    public static AnimationParameters Deserialize(Animation? animation, Deserializer source)
    {
        var result = new AnimationParameters(animation);
        var flags = (AnimationParameterMask)source.ReadVLE();

        if ((flags & AnimationParameterMask.InstanceIndex) != 0)
            result.InstanceIndex = source.ReadVLE();

        result.IsLooped = (flags & AnimationParameterMask.Looped) != 0;
        result.RemoveOnCompletion = (flags & AnimationParameterMask.RemoveOnCompletion) != 0;

        if ((flags & AnimationParameterMask.Layer) != 0)
            result.LayerIndex = source.ReadVLE();

        result.BlendMode = (flags & AnimationParameterMask.Additive) != 0
            ? AnimationBlendMode.Additive
            : AnimationBlendMode.Lerp;

        if ((flags & AnimationParameterMask.StartBone) != 0)
            result.StartBoneName = source.ReadString();
        if ((flags & AnimationParameterMask.AutoFadeOutTime) != 0)
            result.AutoFadeOutTime = source.ReadFloat();

        var time = 0.0f;
        var minTime = 0.0f;
        var maxTime = animation?.Length ?? 0.0f;
        if ((flags & AnimationParameterMask.Time) != 0)
            time = source.ReadFloat();
        if ((flags & AnimationParameterMask.MinTime) != 0)
            minTime = source.ReadFloat();
        if ((flags & AnimationParameterMask.MaxTime) != 0)
            maxTime = source.ReadFloat();
        result._time = new WrappedScalar<float>(time, minTime, maxTime);

        if ((flags & AnimationParameterMask.Speed) != 0)
            result.SpeedValue = source.ReadFloat();

        result.RemoveOnZeroWeight = (flags & AnimationParameterMask.RemoveOnZeroWeight) != 0;
        if ((flags & AnimationParameterMask.Weight) != 0)
            result.WeightValue = source.ReadFloat();
        if ((flags & AnimationParameterMask.TargetWeight) != 0)
            result.TargetWeight = source.ReadFloat();
        if ((flags & AnimationParameterMask.TargetWeightDelay) != 0)
            result.TargetWeightDelay = source.ReadFloat();

        return result;
    }

    public void Serialize(Serializer destination)
    {
        var flags = AnimationParameterMask.None;
        if (InstanceIndex != 0)
            flags |= AnimationParameterMask.InstanceIndex;
        if (IsLooped)
            flags |= AnimationParameterMask.Looped;
        if (RemoveOnCompletion)
            flags |= AnimationParameterMask.RemoveOnCompletion;
        if (LayerIndex != 0)
            flags |= AnimationParameterMask.Layer;
        if (BlendMode == AnimationBlendMode.Additive)
            flags |= AnimationParameterMask.Additive;
        if (string.IsNullOrEmpty(StartBoneName))
            flags |= AnimationParameterMask.StartBone;
        if (AutoFadeOutTime != 0.0f)
            flags |= AnimationParameterMask.AutoFadeOutTime;
        if (_time.Value() != 0.0f)
            flags |= AnimationParameterMask.Time;
        if (_time.Min() != 0.0f)
            flags |= AnimationParameterMask.MinTime;
        if (_animation is not null && _time.Max() != _animation.Length)
            flags |= AnimationParameterMask.MaxTime;
        if (SpeedValue != 1.0f)
            flags |= AnimationParameterMask.Speed;
        if (RemoveOnZeroWeight)
            flags |= AnimationParameterMask.RemoveOnZeroWeight;
        if (WeightValue != 1.0f)
            flags |= AnimationParameterMask.Weight;
        if (TargetWeight != 1.0f)
            flags |= AnimationParameterMask.TargetWeight;
        if (TargetWeightDelay != 0.0f)
            flags |= AnimationParameterMask.TargetWeightDelay;

        destination.WriteVLE((uint)flags);
        if ((flags & AnimationParameterMask.InstanceIndex) != 0)
            destination.WriteVLE(InstanceIndex);
        if ((flags & AnimationParameterMask.Layer) != 0)
            destination.WriteVLE(LayerIndex);
        if ((flags & AnimationParameterMask.StartBone) != 0)
            destination.WriteString(StartBoneName);
        if ((flags & AnimationParameterMask.AutoFadeOutTime) != 0)
            destination.WriteFloat(AutoFadeOutTime);
        if ((flags & AnimationParameterMask.Time) != 0)
            destination.WriteFloat(_time.Value());
        if ((flags & AnimationParameterMask.MinTime) != 0)
            destination.WriteFloat(_time.Min());
        if ((flags & AnimationParameterMask.MaxTime) != 0)
            destination.WriteFloat(_time.Max());
        if ((flags & AnimationParameterMask.Speed) != 0)
            destination.WriteFloat(SpeedValue);
        if ((flags & AnimationParameterMask.Weight) != 0)
            destination.WriteFloat(WeightValue);
        if ((flags & AnimationParameterMask.TargetWeight) != 0)
            destination.WriteFloat(TargetWeight);
        if ((flags & AnimationParameterMask.TargetWeightDelay) != 0)
            destination.WriteFloat(TargetWeightDelay);
    }

    public bool IsMergeableWith(AnimationParameters rhs)
    {
        return ReferenceEquals(_animation, rhs._animation) && InstanceIndex == rhs.InstanceIndex;
    }

    public bool Equals(AnimationParameters? rhs)
    {
        return rhs is not null
            && ReferenceEquals(_animation, rhs._animation)
            && _animationName == rhs._animationName
            && InstanceIndex == rhs.InstanceIndex
            && IsLooped == rhs.IsLooped
            && RemoveOnCompletion == rhs.RemoveOnCompletion
            && LayerIndex == rhs.LayerIndex
            && BlendMode == rhs.BlendMode
            && StartBoneName == rhs.StartBoneName
            && AutoFadeOutTime == rhs.AutoFadeOutTime
            && _time == rhs._time
            && SpeedValue == rhs.SpeedValue
            && RemoveOnZeroWeight == rhs.RemoveOnZeroWeight
            && WeightValue == rhs.WeightValue
            && TargetWeight == rhs.TargetWeight
            && TargetWeightDelay == rhs.TargetWeightDelay;
    }

    public override bool Equals(object? obj) => obj is AnimationParameters rhs && Equals(rhs);

    public override int GetHashCode() => HashCode.Combine(_animation, _animationName, InstanceIndex, IsLooped, LayerIndex, BlendMode, StartBoneName, WeightValue);

    public static bool operator ==(AnimationParameters? lhs, AnimationParameters? rhs) =>
        ReferenceEquals(lhs, rhs) || (lhs is not null && lhs.Equals(rhs));

    public static bool operator !=(AnimationParameters? lhs, AnimationParameters? rhs) => !(lhs == rhs);

    /// <summary>
    /// Empty AnimationParameters.
    /// </summary>
    public static readonly AnimationParameters Empty = new();

    private enum AnimationParameterMask : uint
    {
        None = 0,
        InstanceIndex = 1 << 0,
        Looped = 1 << 1,
        RemoveOnCompletion = 1 << 2,
        Layer = 1 << 3,
        Additive = 1 << 4,
        StartBone = 1 << 5,
        AutoFadeOutTime = 1 << 6,
        Time = 1 << 7,
        MinTime = 1 << 8,
        MaxTime = 1 << 9,
        Speed = 1 << 10,
        RemoveOnZeroWeight = 1 << 11,
        Weight = 1 << 12,
        TargetWeight = 1 << 13,
        TargetWeightDelay = 1 << 14
    }

    private Animation? _animation;
    private StringHash _animationName;
    private WrappedScalar<float> _time;
}
