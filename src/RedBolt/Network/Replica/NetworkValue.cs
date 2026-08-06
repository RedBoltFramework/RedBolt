//
// Copyright (c) 2017-2020 the rbfx project.
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

namespace RedBolt.Replica;

using System.Diagnostics;

using RedBolt.Math;

public interface INetworkInterpolationTraits<T>
    where T : struct
{
    T Interpolate(T lhs, T rhs, float blendFactor, float snapThreshold);
}

public interface INetworkValueTraits<TInternal, TReturn> : INetworkInterpolationTraits<TInternal>
    where TInternal : struct
    where TReturn : struct
{
    TReturn DefaultValue { get; }
    TReturn Extract(TInternal value);
    TReturn Extrapolate(TInternal value, float extrapolationFactor);
    void UpdateCorrection(ref TReturn inverseCorrection, TReturn correctValue, TReturn oldValue);
    void SmoothCorrection(ref TReturn inverseCorrection, float blendFactor);
    void ApplyCorrection(TReturn inverseCorrection, ref TReturn value);
}

/// <summary>Value with derivative that can be extrapolated.</summary>
public struct ValueWithDerivative<T>
    where T : struct
{
    public ValueWithDerivative(T value, T derivative)
    {
        Value = value;
        Derivative = derivative;
    }

    public T Value;
    public T Derivative;
}

/// <summary>Quaternion with angular velocity derivative.</summary>
public struct QuaternionValueWithDerivative
{
    public QuaternionValueWithDerivative(Quaternion value, Vector3f derivative)
    {
        Value = value;
        Derivative = derivative;
    }

    public Quaternion Value;
    public Vector3f Derivative;
}

/// <summary>Default value manipulation used by <see cref="NetworkValue{T}"/>.</summary>
public class NetworkValueTraits<T> : INetworkValueTraits<T, T>
    where T : struct
{
    public T DefaultValue => NetworkValueMath.Default<T>();

    public virtual T Interpolate(T lhs, T rhs, float blendFactor, float snapThreshold)
    {
        if (lhs is Quaternion lhsQuaternion && rhs is Quaternion rhsQuaternion)
            return NetworkValueMath.Cast<T>(lhsQuaternion.Slerp(rhsQuaternion, blendFactor));

        if (NetworkValueMath.GetDistanceSquared(lhs, rhs) >= snapThreshold * snapThreshold)
            return blendFactor < 0.5f ? lhs : rhs;
        return NetworkValueMath.Lerp(lhs, rhs, blendFactor);
    }

    public virtual T Extract(T value) => value;

    public virtual T Extrapolate(T value, float extrapolationFactor) => value;

    public virtual void UpdateCorrection(ref T inverseCorrection, T correctValue, T oldValue)
    {
        if (inverseCorrection is Quaternion inverseQuaternion
            && correctValue is Quaternion correctQuaternion
            && oldValue is Quaternion oldQuaternion)
        {
            inverseCorrection = NetworkValueMath.Cast<T>(
                oldQuaternion * correctQuaternion.Inverse * inverseQuaternion);
            return;
        }

        inverseCorrection = NetworkValueMath.Subtract(
            inverseCorrection, NetworkValueMath.Subtract(correctValue, oldValue));
    }

    public virtual void SmoothCorrection(ref T inverseCorrection, float blendFactor)
    {
        if (inverseCorrection is Quaternion inverseQuaternion)
        {
            inverseCorrection = NetworkValueMath.Cast<T>(
                inverseQuaternion.Slerp(Quaternion.IDENTITY, blendFactor));
            return;
        }

        inverseCorrection = NetworkValueMath.Lerp(inverseCorrection, DefaultValue, blendFactor);
    }

    public virtual void ApplyCorrection(T inverseCorrection, ref T value)
    {
        if (inverseCorrection is Quaternion inverseQuaternion && value is Quaternion quaternion)
        {
            value = NetworkValueMath.Cast<T>(inverseQuaternion * quaternion);
            return;
        }

        value = NetworkValueMath.Add(value, inverseCorrection);
    }
}

public class ValueWithDerivativeNetworkValueTraits<T> : INetworkValueTraits<ValueWithDerivative<T>, T>
    where T : struct
{
    public ValueWithDerivativeNetworkValueTraits()
        : this(new NetworkValueTraits<T>())
    {
    }

    public ValueWithDerivativeNetworkValueTraits(INetworkValueTraits<T, T> valueTraits)
    {
        _valueTraits = valueTraits;
    }

    public T DefaultValue => _valueTraits.DefaultValue;

    public ValueWithDerivative<T> Interpolate(
        ValueWithDerivative<T> lhs, ValueWithDerivative<T> rhs, float blendFactor, float snapThreshold)
    {
        if (NetworkValueMath.GetDistanceSquared(lhs.Value, rhs.Value) >= snapThreshold * snapThreshold)
            return blendFactor < 0.5f ? lhs : rhs;

        return new ValueWithDerivative<T>(
            NetworkValueMath.Lerp(lhs.Value, rhs.Value, blendFactor),
            NetworkValueMath.Lerp(lhs.Derivative, rhs.Derivative, blendFactor));
    }

    public T Extract(ValueWithDerivative<T> value) => value.Value;

    public T Extrapolate(ValueWithDerivative<T> value, float extrapolationFactor) =>
        NetworkValueMath.Add(value.Value, NetworkValueMath.Scale(value.Derivative, extrapolationFactor));

    public void UpdateCorrection(ref T inverseCorrection, T correctValue, T oldValue) =>
        _valueTraits.UpdateCorrection(ref inverseCorrection, correctValue, oldValue);

    public void SmoothCorrection(ref T inverseCorrection, float blendFactor) =>
        _valueTraits.SmoothCorrection(ref inverseCorrection, blendFactor);

    public void ApplyCorrection(T inverseCorrection, ref T value) =>
        _valueTraits.ApplyCorrection(inverseCorrection, ref value);

    private readonly INetworkValueTraits<T, T> _valueTraits;
}

public class QuaternionValueWithDerivativeNetworkValueTraits
    : INetworkValueTraits<QuaternionValueWithDerivative, Quaternion>
{
    public Quaternion DefaultValue => Quaternion.IDENTITY;

    public QuaternionValueWithDerivative Interpolate(QuaternionValueWithDerivative lhs,
        QuaternionValueWithDerivative rhs, float blendFactor, float snapThreshold) => new(
        lhs.Value.Slerp(rhs.Value, blendFactor),
        lhs.Derivative.Lerp(rhs.Derivative, blendFactor));

    public Quaternion Extract(QuaternionValueWithDerivative value) => value.Value;

    public Quaternion Extrapolate(QuaternionValueWithDerivative value, float extrapolationFactor) =>
        Quaternion.FromAngularVelocity(value.Derivative * extrapolationFactor) * value.Value;

    public void UpdateCorrection(ref Quaternion inverseCorrection, Quaternion correctValue, Quaternion oldValue)
    {
        inverseCorrection = oldValue * correctValue.Inverse * inverseCorrection;
    }

    public void SmoothCorrection(ref Quaternion inverseCorrection, float blendFactor)
    {
        inverseCorrection = inverseCorrection.Slerp(Quaternion.IDENTITY, blendFactor);
    }

    public void ApplyCorrection(Quaternion inverseCorrection, ref Quaternion value)
    {
        value = inverseCorrection * value;
    }
}

public abstract class NetworkValueBase
{
    protected struct InterpolationBase
    {
        public NetworkFrame FirstFrame;
        public uint FirstIndex;
        public NetworkFrame SecondFrame;
        public uint SecondIndex;
        public float BlendFactor;
    }

    protected bool IsInitializedBase => _initialized;
    protected uint Capacity => (uint)_hasFrameByIndex.Length;
    protected NetworkFrame FirstFrameBase => _lastFrame - Capacity + 1;
    protected NetworkFrame LastFrameBase => _lastFrame;

    protected void ResizeBase(uint capacity)
    {
        Debug.Assert(capacity > 0);
        _hasFrameByIndex = new bool[checked((int)capacity)];
    }

    protected uint? FrameToIndex(NetworkFrame frame)
    {
        var capacity = Capacity;
        var behind = _lastFrame - frame;
        if (behind >= 0 && (ulong)behind < capacity)
            return (_lastIndex + capacity - (uint)behind) % capacity;
        return null;
    }

    protected uint FrameToIndexUnchecked(NetworkFrame frame)
    {
        var index = FrameToIndex(frame);
        Debug.Assert(index.HasValue);
        return index.GetValueOrDefault();
    }

    protected uint? AllocatedFrameToIndex(NetworkFrame frame)
    {
        var index = FrameToIndex(frame);
        return index.HasValue && _hasFrameByIndex[index.Value] ? index : null;
    }

    protected bool AllocateFrame(NetworkFrame frame, Action<uint, bool> compressCallback)
    {
        Debug.Assert(_hasFrameByIndex.Length != 0);

        if (!_initialized)
        {
            _initialized = true;
            _lastFrame = frame;
            _lastIndex = 0;
            _hasFrameByIndex[_lastIndex] = true;
            return true;
        }

        if (frame > _lastFrame)
        {
            var capacity = Capacity;
            var offset = unchecked((int)(frame - _lastFrame));

            var hasCompressedData = false;
            var lastCompressedOffset = System.Math.Min(capacity - 1, unchecked((uint)offset));
            for (var i = 0u; i <= lastCompressedOffset; ++i)
            {
                var index = (_lastIndex + i + 1) % capacity;
                if (_hasFrameByIndex[index])
                {
                    compressCallback(index, false);
                    hasCompressedData = true;
                }
            }

            _lastFrame = frame;
            _lastIndex = unchecked(_lastIndex + (uint)offset) % capacity;

            var firstSkippedCandidate = _lastFrame - offset + 1;
            var firstSkippedFrame = firstSkippedCandidate > FirstFrameBase ? firstSkippedCandidate : FirstFrameBase;
            for (var skippedFrame = firstSkippedFrame; skippedFrame != _lastFrame; ++skippedFrame)
                _hasFrameByIndex[FrameToIndexUnchecked(skippedFrame)] = false;

            if (hasCompressedData)
            {
                var index = (_lastIndex + 1) % capacity;
                _hasFrameByIndex[index] = true;
                compressCallback(index, true);
            }

            _hasFrameByIndex[_lastIndex] = true;
            return true;
        }

        var pastIndex = FrameToIndex(frame);
        if (pastIndex.HasValue)
        {
            _hasFrameByIndex[pastIndex.Value] = true;
            return true;
        }

        return false;
    }

    protected bool HasFrame(NetworkFrame frame) => AllocatedFrameToIndex(frame).HasValue;

    protected NetworkFrame? FindClosestAllocatedFrameBase(
        NetworkFrame frame, bool searchPast, bool searchFuture)
    {
        if (HasFrame(frame))
            return frame;

        var firstFrame = FirstFrameBase;
        if (searchPast && frame > firstFrame)
        {
            var lastCheckedFrame = _lastFrame < frame - 1 ? _lastFrame : frame - 1;
            for (var pastFrame = lastCheckedFrame; pastFrame != firstFrame - 1; --pastFrame)
            {
                if (HasFrame(pastFrame))
                    return pastFrame;
            }
        }

        if (searchFuture && frame < _lastFrame)
        {
            var firstCheckedFrame = firstFrame > frame + 1 ? firstFrame : frame + 1;
            for (var futureFrame = firstCheckedFrame; futureFrame != _lastFrame + 1; ++futureFrame)
            {
                if (HasFrame(futureFrame))
                    return futureFrame;
            }
        }

        return null;
    }

    protected NetworkFrame GetClosestAllocatedFrame(NetworkFrame frame)
    {
        Debug.Assert(_initialized);
        return FindClosestAllocatedFrameBase(frame, true, true) ?? _lastFrame;
    }

    protected InterpolationBase GetValidFrameInterpolation(NetworkTime time)
    {
        var frame = time.Frame;
        var thisOrPastFrame = FindClosestAllocatedFrameBase(frame, true, false);

        if (thisOrPastFrame == frame && time.Fraction < MathDefs.LargeEpsilon)
        {
            var index = FrameToIndexUnchecked(frame);
            return new InterpolationBase
            {
                FirstFrame = frame,
                FirstIndex = index,
                SecondFrame = frame,
                SecondIndex = index
            };
        }

        var nextOrFutureFrame = FindClosestAllocatedFrameBase(frame + 1, false, true);
        if (thisOrPastFrame.HasValue && nextOrFutureFrame.HasValue)
        {
            var firstIndex = FrameToIndexUnchecked(thisOrPastFrame.Value);
            var secondIndex = FrameToIndexUnchecked(nextOrFutureFrame.Value);
            var extraPastFrames = unchecked((int)(frame - thisOrPastFrame.Value));
            var extraFutureFrames = unchecked((int)(nextOrFutureFrame.Value - frame - 1));
            var adjustedFactor =
                (extraPastFrames + time.Fraction) / (extraPastFrames + extraFutureFrames + 1);
            return new InterpolationBase
            {
                FirstFrame = thisOrPastFrame.Value,
                FirstIndex = firstIndex,
                SecondFrame = nextOrFutureFrame.Value,
                SecondIndex = secondIndex,
                BlendFactor = adjustedFactor
            };
        }

        var closestFrame = thisOrPastFrame ?? nextOrFutureFrame ?? _lastFrame;
        var closestIndex = FrameToIndexUnchecked(closestFrame);
        return new InterpolationBase
        {
            FirstFrame = closestFrame,
            FirstIndex = closestIndex,
            SecondFrame = closestFrame,
            SecondIndex = closestIndex
        };
    }

    private bool _initialized;
    private NetworkFrame _lastFrame;
    private uint _lastIndex;
    private bool[] _hasFrameByIndex = [];
}

/// <summary>Value stored at multiple points of time in a ring buffer.</summary>
public class NetworkValue<T> : NetworkValueBase
    where T : struct
{
    public NetworkValue()
        : this(new NetworkValueTraits<T>())
    {
    }

    public NetworkValue(INetworkInterpolationTraits<T> traits)
    {
        _traits = traits;
    }

    public bool IsInitialized => IsInitializedBase;
    public NetworkFrame LastFrame => LastFrameBase;

    public void Resize(uint capacity)
    {
        ResizeBase(capacity);
        _values = new T[checked((int)capacity)];
    }

    public void Set(NetworkFrame frame, T value)
    {
        var compressedValue = default(T);
        void Compress(uint index, bool restore)
        {
            if (!restore)
                compressedValue = _values[index];
            else
                _values[index] = compressedValue;
        }

        if (AllocateFrame(frame, Compress))
            _values[FrameToIndexUnchecked(frame)] = value;
    }

    public bool Has(NetworkFrame frame) => AllocatedFrameToIndex(frame).HasValue;

    public T? GetRaw(NetworkFrame frame)
    {
        var index = AllocatedFrameToIndex(frame);
        return index.HasValue ? _values[index.Value] : null;
    }

    public ref readonly T GetRawUnchecked(NetworkFrame frame) =>
        ref _values[FrameToIndexUnchecked(frame)];

    public (T Value, NetworkFrame Frame)? GetRawOrPrior(NetworkFrame frame)
    {
        var closestFrame = FindClosestAllocatedFrameBase(frame, true, false);
        return closestFrame.HasValue
            ? (_values[FrameToIndexUnchecked(closestFrame.Value)], closestFrame.Value)
            : null;
    }

    public T GetClosestRaw(NetworkFrame frame)
    {
        var closestFrame = GetClosestAllocatedFrame(frame);
        return _values[FrameToIndexUnchecked(closestFrame)];
    }

    public NetworkFrame? FindClosestAllocatedFrame(
        NetworkFrame frame, bool searchPast, bool searchFuture) =>
        FindClosestAllocatedFrameBase(frame, searchPast, searchFuture);

    public T SampleValid(NetworkTime time, float snapThreshold = MathDefs.LargeValue) =>
        CalculateInterpolatedValue(time, snapThreshold).Value;

    public T? SamplePrecise(NetworkTime time, float snapThreshold = MathDefs.LargeValue)
    {
        var result = CalculateInterpolatedValue(time, snapThreshold);
        return result.IsPrecise ? result.Value : null;
    }

    private (T Value, bool IsPrecise) CalculateInterpolatedValue(NetworkTime time, float snapThreshold)
    {
        var interpolation = GetValidFrameInterpolation(time);
        var value = interpolation.FirstIndex == interpolation.SecondIndex
            ? _values[interpolation.FirstIndex]
            : _traits.Interpolate(_values[interpolation.FirstIndex], _values[interpolation.SecondIndex],
                interpolation.BlendFactor, snapThreshold);

        var isPrecise = time.Frame <= interpolation.SecondFrame;
        return (value, isPrecise);
    }

    private readonly INetworkInterpolationTraits<T> _traits;
    private T[] _values = [];
}

/// <summary>Continuously samples a <see cref="NetworkValue{T}"/> with extrapolation and correction smoothing.</summary>
public class NetworkValueSampler<T> : NetworkValueSampler<T, T>
    where T : struct
{
    public NetworkValueSampler()
        : base(new NetworkValueTraits<T>())
    {
    }

    public NetworkValueSampler(INetworkValueTraits<T, T> traits)
        : base(traits)
    {
    }
}

public class NetworkValueSampler<TInternal, TReturn>
    where TInternal : struct
    where TReturn : struct
{
    public NetworkValueSampler(INetworkValueTraits<TInternal, TReturn> traits)
    {
        _traits = traits;
        _valueCorrection = traits.DefaultValue;
    }

    public void Setup(uint maxExtrapolation, float smoothingConstant, float snapThreshold)
    {
        _maxExtrapolation = maxExtrapolation;
        _smoothingConstant = smoothingConstant;
        _snapThreshold = snapThreshold;
    }

    public void UpdatePreviousValue(NetworkTime time, TReturn value)
    {
        _previousValue = new TimeAndValue { Time = time, Value = value };
    }

    public TReturn? UpdateAndSample(NetworkValue<TInternal> value, NetworkTime time, float timeStep)
    {
        if (!value.IsInitialized)
            return null;

        UpdateCorrection(value, timeStep);
        UpdateCache(value, time.Frame);

        var sampledValue = CalculateValueFromCache(value, time);
        _previousValue = new TimeAndValue { Time = time, Value = sampledValue };

        _traits.ApplyCorrection(_valueCorrection, ref sampledValue);
        return sampledValue;
    }

    private float GetExtrapolationFactor(NetworkTime time, NetworkFrame baseFrame, uint maxExtrapolation)
    {
        var factor = (time.Frame - baseFrame) + time.Fraction;
        return System.Math.Min(factor, maxExtrapolation);
    }

    private void UpdateCorrection(NetworkValue<TInternal> value, float timeStep)
    {
        if (!_previousValue.HasValue)
            return;

        _traits.SmoothCorrection(ref _valueCorrection,
            MathDefs.InverseExponentialDecay(_smoothingConstant * timeStep));

        var previous = _previousValue.Value;
        UpdateCache(value, previous.Time.Frame);
        var newPreviousValue = CalculateValueFromCache(value, previous.Time);
        _traits.UpdateCorrection(ref _valueCorrection, newPreviousValue, previous.Value);
    }

    private void UpdateCache(NetworkValue<TInternal> value, NetworkFrame frame)
    {
        if (_interpolationCache.HasValue && _interpolationCache.Value.BaseFrame == frame)
            return;

        var nextValue = value.SamplePrecise(new NetworkTime(frame + 1), _snapThreshold);
        if (nextValue.HasValue)
        {
            var baseValue = _interpolationCache.HasValue
                && _interpolationCache.Value.BaseFrame + 1 == frame
                    ? _interpolationCache.Value.NextValue
                    : value.SampleValid(new NetworkTime(frame), _snapThreshold);

            _interpolationCache = new InterpolationCache
            {
                BaseFrame = frame,
                BaseValue = baseValue,
                NextValue = nextValue.Value
            };
            _extrapolationFrame = frame + 1;
        }
        else
        {
            _extrapolationFrame = value.LastFrame;
            Debug.Assert(_extrapolationFrame.Value < frame + 1);
        }
    }

    private TReturn CalculateValueFromCache(NetworkValue<TInternal> value, NetworkTime time)
    {
        if (_interpolationCache.HasValue && _interpolationCache.Value.BaseFrame == time.Frame)
        {
            var cache = _interpolationCache.Value;
            var interpolated = _traits.Interpolate(
                cache.BaseValue, cache.NextValue, time.Fraction, _snapThreshold);
            return _traits.Extract(interpolated);
        }

        Debug.Assert(_extrapolationFrame.HasValue);
        var baseValue = value.GetRaw(_extrapolationFrame.GetValueOrDefault()).GetValueOrDefault();
        var factor = GetExtrapolationFactor(time, _extrapolationFrame.GetValueOrDefault(), _maxExtrapolation);
        return _traits.Extrapolate(baseValue, factor);
    }

    private struct InterpolationCache
    {
        public NetworkFrame BaseFrame;
        public TInternal BaseValue;
        public TInternal NextValue;
    }

    private struct TimeAndValue
    {
        public NetworkTime Time;
        public TReturn Value;
    }

    private readonly INetworkValueTraits<TInternal, TReturn> _traits;
    private uint _maxExtrapolation;
    private float _smoothingConstant;
    private float _snapThreshold = MathDefs.LargeValue;

    private InterpolationCache? _interpolationCache;
    private TimeAndValue? _previousValue;
    private NetworkFrame? _extrapolationFrame;
    private TReturn _valueCorrection;
}

public readonly struct InterpolatedValueSpan<T>
    where T : struct
{
    internal InterpolatedValueSpan(T[] values, int firstOffset, int secondOffset,
        int size, float blendFactor, float snapThreshold, INetworkInterpolationTraits<T> traits)
    {
        _values = values;
        _firstOffset = firstOffset;
        _secondOffset = secondOffset;
        _size = size;
        _blendFactor = blendFactor;
        _snapThreshold = snapThreshold;
        _traits = traits;
    }

    public T this[int index] => _firstOffset == _secondOffset
        ? _values[_firstOffset + index]
        : _traits.Interpolate(_values[_firstOffset + index], _values[_secondOffset + index],
            _blendFactor, _snapThreshold);

    public int Count => _size;

    private readonly T[] _values;
    private readonly int _firstOffset;
    private readonly int _secondOffset;
    private readonly int _size;
    private readonly float _blendFactor;
    private readonly float _snapThreshold;
    private readonly INetworkInterpolationTraits<T> _traits;
}

/// <summary>Network value whose frames contain arrays of elements.</summary>
public class NetworkValueVector<T> : NetworkValueBase
    where T : struct
{
    public NetworkValueVector()
        : this(new NetworkValueTraits<T>())
    {
    }

    public NetworkValueVector(INetworkInterpolationTraits<T> traits)
    {
        _traits = traits;
    }

    public void Resize(uint size, uint capacity)
    {
        ResizeBase(capacity);
        _size = System.Math.Max(1u, size);
        _values = new T[checked((int)(_size * capacity))];
        _compressedValues = new T[checked((int)_size)];
    }

    public uint Size => _size;

    public Span<T> SetUninitialized(NetworkFrame frame)
    {
        void Compress(uint index, bool restore)
        {
            var values = _values.AsSpan(checked((int)(index * _size)), checked((int)_size));
            if (!restore)
                values.CopyTo(_compressedValues);
            else
                _compressedValues.CopyTo(values);
        }

        if (AllocateFrame(frame, Compress))
        {
            var index = FrameToIndexUnchecked(frame);
            return _values.AsSpan(checked((int)(index * _size)), checked((int)_size));
        }
        return [];
    }

    public void Set(NetworkFrame frame, ReadOnlySpan<T> value)
    {
        var destination = SetUninitialized(frame);
        if (!destination.IsEmpty)
            value[..System.Math.Min(value.Length, destination.Length)].CopyTo(destination);
    }

    public ReadOnlyMemory<T>? GetRaw(NetworkFrame frame)
    {
        var index = AllocatedFrameToIndex(frame);
        if (!index.HasValue)
            return null;
        return GetMemoryForIndex(index.Value);
    }

    public ReadOnlyMemory<T> GetClosestRaw(NetworkFrame frame)
    {
        var closestFrame = GetClosestAllocatedFrame(frame);
        return GetMemoryForIndex(FrameToIndexUnchecked(closestFrame));
    }

    public InterpolatedValueSpan<T> SampleValid(
        NetworkTime time, float snapThreshold = MathDefs.LargeValue)
    {
        var interpolation = GetValidFrameInterpolation(time);
        return new InterpolatedValueSpan<T>(_values,
            checked((int)(interpolation.FirstIndex * _size)),
            checked((int)(interpolation.SecondIndex * _size)),
            checked((int)_size), interpolation.BlendFactor, snapThreshold, _traits);
    }

    private ReadOnlyMemory<T> GetMemoryForIndex(uint index) =>
        _values.AsMemory(checked((int)(index * _size)), checked((int)_size));

    private readonly INetworkInterpolationTraits<T> _traits;
    private uint _size;
    private T[] _values = [];
    private T[] _compressedValues = [];
}

internal static class NetworkValueMath
{
    public static T Cast<T>(object value)
        where T : struct => (T)value;

    public static T Default<T>()
        where T : struct => typeof(T) == typeof(Quaternion)
            ? Cast<T>(Quaternion.IDENTITY)
            : default;

    public static float GetDistanceSquared<T>(T lhs, T rhs)
        where T : struct => (lhs, rhs) switch
    {
        (float left, float right) => (left - right) * (left - right),
        (Vector2f left, Vector2f right) => (left - right).LengthSquared,
        (Vector3f left, Vector3f right) => (left - right).LengthSquared,
        (Quaternion left, Quaternion right) => 1.0f - System.Math.Abs(left.DotProduct(right)),
        _ => 0.0f
    };

    public static T Lerp<T>(T lhs, T rhs, float blendFactor)
        where T : struct => (lhs, rhs) switch
    {
        (float left, float right) => Cast<T>(MathDefs.Lerp(left, right, blendFactor)),
        (double left, double right) => Cast<T>(MathDefs.Lerp(left, right, blendFactor)),
        (Vector2f left, Vector2f right) => Cast<T>(left.Lerp(right, blendFactor)),
        (Vector2d left, Vector2d right) => Cast<T>(left.Lerp(right, blendFactor)),
        (Vector3f left, Vector3f right) => Cast<T>(left.Lerp(right, blendFactor)),
        (Vector3d left, Vector3d right) => Cast<T>(left.Lerp(right, blendFactor)),
        (Vector4f left, Vector4f right) => Cast<T>(left.Lerp(right, blendFactor)),
        (Quaternion left, Quaternion right) => Cast<T>(left.Slerp(right, blendFactor)),
        (Color left, Color right) => Cast<T>(left.Lerp(right, blendFactor)),
        (Transform left, Transform right) => Cast<T>(left.Lerp(right, blendFactor)),
        (DoubleTransform left, DoubleTransform right) => Cast<T>(left.Lerp(right, blendFactor)),
        _ => throw new NotSupportedException($"No network interpolation is defined for {typeof(T)}.")
    };

    public static T Add<T>(T lhs, T rhs)
        where T : struct => (lhs, rhs) switch
    {
        (float left, float right) => Cast<T>(left + right),
        (double left, double right) => Cast<T>(left + right),
        (Vector2f left, Vector2f right) => Cast<T>(left + right),
        (Vector2d left, Vector2d right) => Cast<T>(left + right),
        (Vector3f left, Vector3f right) => Cast<T>(left + right),
        (Vector3d left, Vector3d right) => Cast<T>(left + right),
        (Vector4f left, Vector4f right) => Cast<T>(left + right),
        _ => throw new NotSupportedException($"No network addition is defined for {typeof(T)}.")
    };

    public static T Subtract<T>(T lhs, T rhs)
        where T : struct => (lhs, rhs) switch
    {
        (float left, float right) => Cast<T>(left - right),
        (double left, double right) => Cast<T>(left - right),
        (Vector2f left, Vector2f right) => Cast<T>(left - right),
        (Vector2d left, Vector2d right) => Cast<T>(left - right),
        (Vector3f left, Vector3f right) => Cast<T>(left - right),
        (Vector3d left, Vector3d right) => Cast<T>(left - right),
        (Vector4f left, Vector4f right) => Cast<T>(left - right),
        _ => throw new NotSupportedException($"No network subtraction is defined for {typeof(T)}.")
    };

    public static T Scale<T>(T value, float factor)
        where T : struct => value switch
    {
        float scalar => Cast<T>(scalar * factor),
        double scalar => Cast<T>(scalar * factor),
        Vector2f vector => Cast<T>(vector * factor),
        Vector2d vector => Cast<T>(vector * factor),
        Vector3f vector => Cast<T>(vector * factor),
        Vector3d vector => Cast<T>(vector * factor),
        Vector4f vector => Cast<T>(vector * factor),
        _ => throw new NotSupportedException($"No network scaling is defined for {typeof(T)}.")
    };
}
