// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Math;

using System.Numerics;

/// <summary>
/// Range between two WrappedScalar values.
/// Preserves direction of change.
/// </summary>
public struct WrappedScalarRange<T>
    where T : unmanaged, INumber<T>
{
    public WrappedScalarRange(T value, T minValue, T maxValue)
    {
        _min = minValue;
        _max = maxValue;
        _begin = value;
        _end = value;
        _numWraps = 0;
    }

    public WrappedScalarRange(T beginValue, T endValue, T minValue, T maxValue, int numWraps)
    {
        _min = minValue;
        _max = maxValue;
        _begin = beginValue;
        _end = endValue;
        _numWraps = numWraps;
    }

    public T Begin() => _begin;
    public T End() => _end;
    public T Min() => _min;
    public T Max() => _max;
    public bool IsEmpty() => _begin == _end;

    public bool ContainsInclusive(T value) => ContainsInternal(value, true, true);
    public bool ContainsExclusive(T value) => ContainsInternal(value, false, false);
    public bool ContainsExcludingBegin(T value) => ContainsInternal(value, false, true);
    public bool ContainsExcludingEnd(T value) => ContainsInternal(value, true, false);

    public static bool operator ==(WrappedScalarRange<T> lhs, WrappedScalarRange<T> rhs)
    {
        return lhs._min == rhs._min
            && lhs._max == rhs._max
            && lhs._begin == rhs._begin
            && lhs._end == rhs._end
            && lhs._numWraps == rhs._numWraps;
    }

    public static bool operator !=(WrappedScalarRange<T> lhs, WrappedScalarRange<T> rhs) => !(lhs == rhs);

    public override bool Equals(object? obj) => obj is WrappedScalarRange<T> rhs && this == rhs;

    public override int GetHashCode() => HashCode.Combine(_min, _max, _begin, _end, _numWraps);

    private bool ContainsInternal(T value, bool includeBegin, bool includeEnd)
    {
        if (value < _min || value > _max)
            return false;

        var lowest = T.Min(_begin, _end);
        var highest = T.Max(_begin, _end);

        if (_numWraps == 0)
        {
            // min  begin    end   max
            // |    |--------|     |
            //   or
            // min  end      begin max
            // |    |--------|     |
            return value >= lowest && value <= highest
                && (includeBegin || value != _begin)
                && (includeEnd || value != _end);
        }
        else if ((_numWraps == 1 && _end < _begin) || (_numWraps == -1 && _end > _begin))
        {
            // min  end    begin   max
            // |----|      |-------|
            //   or
            // min  begin  end     max
            // |----|      |-------|
            return (value <= lowest || value >= highest)
                && (includeBegin || value != _begin)
                && (includeEnd || value != _end);
        }
        else
        {
            // min  end    begin   max
            // |----|======|-------|
            return true;
        }
    }

    private T _min;
    private T _max;
    private T _begin;
    private T _end;
    private int _numWraps;
}

/// <summary>
/// Wrapped value between min and max boundaries.
/// </summary>
public struct WrappedScalar<T>
    where T : unmanaged, INumber<T>
{
    public WrappedScalar()
    {
    }

    public WrappedScalar(T value, T minValue, T maxValue)
    {
        _min = T.Min(minValue, maxValue);
        _max = T.Max(minValue, maxValue);
        _value = T.Min(T.Max(value, _min), _max);
    }

    /// <summary>
    /// Reset value.
    /// </summary>
    public void Set(T value)
    {
        _value = T.Min(T.Max(value, _min), _max);
    }

    /// <summary>
    /// Add delta to the scalar, wrapping value at the boundaries. Return the range of the change.
    /// </summary>
    public WrappedScalarRange<T> UpdateWrapped(T delta)
    {
        if (delta == T.Zero || double.CreateChecked(_max - _min) < MathDefs.LargeEpsilon)
            return new WrappedScalarRange<T>(_value, _min, _max);

        var numWraps = 0;
        var oldValue = _value;
        while (delta > T.Zero)
        {
            _value += delta;
            if (_value < _max)
                delta = T.Zero;
            else
            {
                delta = _value - _max;
                _value = _min;
                ++numWraps;
            }
        }

        while (delta < T.Zero)
        {
            _value += delta;
            if (_value > _min)
                delta = T.Zero;
            else
            {
                delta = _value - _min;
                _value = _max;
                --numWraps;
            }
        }

        return new WrappedScalarRange<T>(oldValue, _value, _min, _max, numWraps);
    }

    /// <summary>
    /// Add delta to the scalar, clamping value at the boundaries. Return the range of the change. Optionally returns out-of-bounds range instead of clamped one.
    /// </summary>
    public WrappedScalarRange<T> UpdateClamped(T delta, bool returnOutOfBounds = false)
    {
        var oldValue = _value;
        _value = T.Min(T.Max(_value + delta, _min), _max);
        return new WrappedScalarRange<T>(oldValue, returnOutOfBounds ? oldValue + delta : _value, _min, _max, 0);
    }

    /// <summary>
    /// Clamp boundaries.
    /// </summary>
    public WrappedScalar<T> MinMaxClamped(T minValue, T maxValue)
    {
        return new WrappedScalar<T>(
            _value,
            T.Max(minValue, _min),
            T.Min(maxValue, _max));
    }

    public T Value() => _value;
    public T Min() => _min;
    public T Max() => _max;

    public static bool operator ==(WrappedScalar<T> lhs, WrappedScalar<T> rhs)
    {
        return lhs._min == rhs._min
            && lhs._max == rhs._max
            && lhs._value == rhs._value;
    }

    public static bool operator !=(WrappedScalar<T> lhs, WrappedScalar<T> rhs) => !(lhs == rhs);

    public override bool Equals(object? obj) => obj is WrappedScalar<T> rhs && this == rhs;

    public override int GetHashCode() => HashCode.Combine(_min, _max, _value);

    private T _min = T.CreateChecked(-MathDefs.LargeValue);
    private T _max = T.CreateChecked(MathDefs.LargeValue);
    private T _value = T.Zero;
}
