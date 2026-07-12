// Copyright (c) 2008-2020 the Urho3D project.
// Copyright (c) 2020-2025 the rbfx project.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

using VariantAnimationKeyFrame = RedBolt.VariantCurvePoint;

namespace RedBolt;

/// <summary>Skeletal animation keyframe.</summary>
/// <remarks>TODO: port: C# structs cannot inherit another struct, so Transform fields are duplicated here.</remarks>
public struct AnimationKeyFrame
{
    /// <summary>Keyframe time.</summary>
    public float Time;

    public Vector3f Position;
    public Quaternion Rotation;
    public Vector3f Scale;

    public AnimationKeyFrame()
    {
        Position = Vector3f.Zero;
        Rotation = Quaternion.Identity;
        Scale = Vector3f.One;
    }

    public AnimationKeyFrame(float time, Vector3f position, Quaternion rotation, Vector3f scale)
    {
        Position = position;
        Rotation = rotation;
        Scale = scale;
        Time = time;
    }

    public AnimationKeyFrame(float time, Vector3f position)
        : this(time, position, Quaternion.Identity, Vector3f.One)
    {
    }

    public AnimationKeyFrame(float time, Vector3f position, Quaternion rotation)
        : this(time, position, rotation, Vector3f.One)
    {
    }
}

/// <summary>Skeletal animation track, stores keyframes of a single bone.</summary>
public sealed class AnimationTrack : KeyFrameSet<AnimationKeyFrame>
{
    /// <summary>Bone or scene node name.</summary>
    public string Name = string.Empty;
    /// <summary>Name hash.</summary>
    public StringHash NameHash;
    /// <summary>Bitmask of included data (position, rotation, scale).</summary>
    public AnimationChannel ChannelMask;
    /// <summary>Weight of the position channel.</summary>
    public float PositionWeight = 1.0f;
    /// <summary>Weight of the rotation channel.</summary>
    public float RotationWeight = 1.0f;
    /// <summary>Weight of the scale channel.</summary>
    public float ScaleWeight = 1.0f;

    public AnimationTrack()
        : base(static keyFrame => keyFrame.Time)
    {
    }

    /// <summary>Sample value at given time.</summary>
    public void Sample(float time, float duration, bool isLooped, ref uint frameIndex, ref Transform value)
    {
        GetKeyFrames(time, duration, isLooped, ref frameIndex, out var nextFrameIndex, out var blendFactor);

        var keyFrame = KeyFrames[checked((int)frameIndex)];
        var nextKeyFrame = KeyFrames[checked((int)nextFrameIndex)];

        if (blendFactor >= MathDefs.Epsilon)
        {
            if ((ChannelMask & AnimationChannel.Position) != 0)
                value.Position = keyFrame.Position.Lerp(nextKeyFrame.Position, blendFactor);
            if ((ChannelMask & AnimationChannel.Rotation) != 0)
                value.Rotation = keyFrame.Rotation.Slerp(nextKeyFrame.Rotation, blendFactor);
            if ((ChannelMask & AnimationChannel.Scale) != 0)
                value.Scale = keyFrame.Scale.Lerp(nextKeyFrame.Scale, blendFactor);
        }
        else
        {
            if ((ChannelMask & AnimationChannel.Position) != 0)
                value.Position = keyFrame.Position;
            if ((ChannelMask & AnimationChannel.Rotation) != 0)
                value.Rotation = keyFrame.Rotation;
            if ((ChannelMask & AnimationChannel.Scale) != 0)
                value.Scale = keyFrame.Scale;
        }
    }

    /// <summary>Return whether the track is looped, i.e. the first and the last keyframes have the same value.</summary>
    public bool IsLooped(
        float positionThreshold = 0.001f, float rotationThreshold = 0.001f, float scaleThreshold = 0.001f)
    {
        if (KeyFrames.Count == 0)
            return true;

        var firstTransform = KeyFrames[0];
        var lastTransform = KeyFrames[^1];

        if ((ChannelMask & AnimationChannel.Position) != 0
            && !firstTransform.Position.ApproximatelyEquivalent(lastTransform.Position, positionThreshold))
            return false;
        if ((ChannelMask & AnimationChannel.Rotation) != 0
            && !firstTransform.Rotation.ApproximatelyEquivalent(lastTransform.Rotation, rotationThreshold))
            return false;
        if ((ChannelMask & AnimationChannel.Scale) != 0
            && !firstTransform.Scale.ApproximatelyEquivalent(lastTransform.Scale, scaleThreshold))
            return false;

        return true;
    }

    /// <summary>Create track from separate position, rotation and scale tracks. Keyframe times are merged within epsilon.</summary>
    public void CreateMerged(AnimationChannel channels,
        ReadOnlySpan<(float Time, Vector3f Value)> positionTrack,
        ReadOnlySpan<(float Time, Quaternion Value)> rotationTrack,
        ReadOnlySpan<(float Time, Vector3f Value)> scaleTrack,
        float epsilon)
    {
        ChannelMask = channels;
        KeyFrames.Clear();

        var hasPositions = (channels & AnimationChannel.Position) != 0;
        var hasRotations = (channels & AnimationChannel.Rotation) != 0;
        var hasScales = (channels & AnimationChannel.Scale) != 0;

        var keys = new List<float>();
        if (hasPositions)
            AppendKeys(keys, positionTrack);
        if (hasRotations)
            AppendKeys(keys, rotationTrack);
        if (hasScales)
            AppendKeys(keys, scaleTrack);
        keys.Sort();
        EraseEquivalentKeys(keys, epsilon);

        var numKeyFrames = keys.Count;
        for (var i = 0; i < numKeyFrames; ++i)
            KeyFrames.Add(new AnimationKeyFrame { Time = keys[i] });

        if (hasPositions)
        {
            var remappedPositions = RemapVectorValues(keys, positionTrack);
            for (var i = 0; i < numKeyFrames; ++i)
            {
                var keyFrame = KeyFrames[i];
                keyFrame.Position = remappedPositions[i];
                KeyFrames[i] = keyFrame;
            }
        }

        if (hasRotations)
        {
            var remappedRotations = RemapQuaternionValues(keys, rotationTrack);
            for (var i = 0; i < numKeyFrames; ++i)
            {
                var keyFrame = KeyFrames[i];
                keyFrame.Rotation = remappedRotations[i];
                KeyFrames[i] = keyFrame;
            }
        }

        if (hasScales)
        {
            var remappedScales = RemapVectorValues(keys, scaleTrack);
            for (var i = 0; i < numKeyFrames; ++i)
            {
                var keyFrame = KeyFrames[i];
                keyFrame.Scale = remappedScales[i];
                KeyFrames[i] = keyFrame;
            }
        }
    }

    private static void AppendKeys(List<float> keys, ReadOnlySpan<(float Time, Vector3f Value)> keyFrames)
    {
        foreach (var keyFrame in keyFrames)
            keys.Add(keyFrame.Time);
    }

    private static void AppendKeys(List<float> keys, ReadOnlySpan<(float Time, Quaternion Value)> keyFrames)
    {
        foreach (var keyFrame in keyFrames)
            keys.Add(keyFrame.Time);
    }

    private static void EraseEquivalentKeys(List<float> keys, float epsilon)
    {
        var lastValidIndex = 0;
        for (var i = 1; i < keys.Count; ++i)
        {
            if (keys[i] - keys[lastValidIndex] < epsilon)
                keys[i] = -MathDefs.LargeValue;
            else
                lastValidIndex = i;
        }

        for (var i = keys.Count - 1; i >= 0; --i)
        {
            if (keys[i] < 0.0f)
                keys.RemoveAt(i);
        }
    }

    private static List<Vector3f> RemapVectorValues(List<float> destinationKeys,
        ReadOnlySpan<(float Time, Vector3f Value)> sourceKeyFrames)
    {
        var result = new List<Vector3f>(destinationKeys.Count);
        if (sourceKeyFrames.Length == 0)
        {
            result.AddRange(new Vector3f[destinationKeys.Count]);
            return result;
        }

        for (var i = 0; i < destinationKeys.Count; ++i)
        {
            var destinationKey = destinationKeys[i];
            var secondIndex = LowerBound(sourceKeyFrames, destinationKey);
            secondIndex = Math.Min(sourceKeyFrames.Length - 1, secondIndex);
            var firstIndex = secondIndex;
            if (secondIndex < sourceKeyFrames.Length && sourceKeyFrames[secondIndex].Time >= destinationKey)
                firstIndex = secondIndex == 0 ? 0 : secondIndex - 1;

            var firstFrame = sourceKeyFrames[firstIndex];
            var secondFrame = sourceKeyFrames[secondIndex];
            result.Add(firstIndex == secondIndex
                ? firstFrame.Value
                : firstFrame.Value.Lerp(secondFrame.Value,
                    MathDefs.InverseLerp(firstFrame.Time, secondFrame.Time, destinationKey)));
        }

        return result;
    }

    private static List<Quaternion> RemapQuaternionValues(List<float> destinationKeys,
        ReadOnlySpan<(float Time, Quaternion Value)> sourceKeyFrames)
    {
        var result = new List<Quaternion>(destinationKeys.Count);
        if (sourceKeyFrames.Length == 0)
        {
            for (var i = 0; i < destinationKeys.Count; ++i)
                result.Add(Quaternion.Identity);
            return result;
        }

        for (var i = 0; i < destinationKeys.Count; ++i)
        {
            var destinationKey = destinationKeys[i];
            var secondIndex = LowerBound(sourceKeyFrames, destinationKey);
            secondIndex = Math.Min(sourceKeyFrames.Length - 1, secondIndex);
            var firstIndex = secondIndex;
            if (secondIndex < sourceKeyFrames.Length && sourceKeyFrames[secondIndex].Time >= destinationKey)
                firstIndex = secondIndex == 0 ? 0 : secondIndex - 1;

            var firstFrame = sourceKeyFrames[firstIndex];
            var secondFrame = sourceKeyFrames[secondIndex];
            result.Add(firstIndex == secondIndex
                ? firstFrame.Value
                : firstFrame.Value.Slerp(secondFrame.Value,
                    MathDefs.InverseLerp(firstFrame.Time, secondFrame.Time, destinationKey)));
        }

        return result;
    }

    private static int LowerBound(ReadOnlySpan<(float Time, Vector3f Value)> keyFrames, float time)
    {
        var first = 0;
        var count = keyFrames.Length;
        while (count > 0)
        {
            var step = count / 2;
            var middle = first + step;
            if (keyFrames[middle].Time < time)
            {
                first = middle + 1;
                count -= step + 1;
            }
            else
                count = step;
        }

        return first;
    }

    private static int LowerBound(ReadOnlySpan<(float Time, Quaternion Value)> keyFrames, float time)
    {
        var first = 0;
        var count = keyFrames.Length;
        while (count > 0)
        {
            var step = count / 2;
            var middle = first + step;
            if (keyFrames[middle].Time < time)
            {
                first = middle + 1;
                count -= step + 1;
            }
            else
                count = step;
        }

        return first;
    }
}

/// <summary>Generic variant animation keyframe.</summary>
/// <summary>Generic animation track, stores keyframes of single animatable entity.</summary>
public sealed class VariantAnimationTrack : VariantCurve
{
    /// <summary>Weight of the track.</summary>
    public float Weight = 1.0f;

    /// <summary>Return whether the track is looped, i.e. the first and the last keyframes have the same value.</summary>
    public bool IsLooped()
    {
        if (KeyFrames.Count == 0)
            return true;

        var firstValue = KeyFrames[0].Value;
        var lastValue = KeyFrames[^1].Value;
        return firstValue == lastValue;
    }
}
