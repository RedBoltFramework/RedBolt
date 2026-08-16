// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Math;

using System;
using System.Collections.Generic;

using RedBolt.Logging;

/// <summary>Uniformly bending IK chain.</summary>
public class IKSpineChain : IKChain, ICloneable
{
    public delegate float WeightFunction(float x);

    public static float DefaultWeightFunction(float x) => 1.0f;

    public IKSpineChain()
    {
    }

    public IKSpineChain(IKSpineChain chain)
        : base(chain)
    {
        _weights.AddRange(chain._weights);
    }

    public new object Clone() => new IKSpineChain(this);

    public void Solve(Vector3f target, Vector3f baseDirection, float maxRotation, IKSettings settings,
        WeightFunction? weightFun = null)
    {
        if (_nodes.Count < 2)
            return;

        StorePreviousTransforms();
        UpdateSegmentWeights(weightFun ?? DefaultWeightFunction);

        var basePosition = _nodes[0].Position;
        var bendDirection = GetProjectionAndOffset(target, basePosition, baseDirection).Offset.Normalized;

        //    Target
        //   /|
        //  / |
        // o--> Base Direction (= x axis)
        // ^
        // Base Position
        var projectedTarget = ProjectTarget(target, basePosition, baseDirection);
        var angularTolerance = settings.Tolerance / _totalLength * MathDefs.RadToDeg;
        var totalAngle = FindBestAngle(projectedTarget, maxRotation, angularTolerance);
        EvaluateSegmentPositions(totalAngle, baseDirection, bendDirection);

        for (var i = 0; i < _segments.Count; ++i)
        {
            var segment = _segments[i];
            segment.UpdateRotationInNodes(true, i == _segments.Count - 1);
            _segments[i] = segment;
        }
    }

    public void Twist(float angle, IKSettings settings)
    {
        if (_segments.Count < 2)
            return;

        var accumulatedAngle = 0.0f;
        for (var i = 0; i < _segments.Count; ++i)
        {
            accumulatedAngle += angle * _weights[i];
            var segment = _segments[i];
            segment.Twist(accumulatedAngle, i == _segments.Count - 1);
            _segments[i] = segment;
        }
    }

    private void UpdateSegmentWeights(WeightFunction weightFun)
    {
        _weights.Clear();
        for (var i = 0; i < _segments.Count; ++i)
            _weights.Add(0.0f);

        var totalWeight = 0.0f;
        for (var i = 0; i < _segments.Count; ++i)
        {
            var fract = (float)i / (_segments.Count - 1);
            var weight = _segments[i].Length * weightFun(fract);
            totalWeight += weight;
            _weights[i] = weight;
        }

        if (totalWeight > 0.0f)
        {
            for (var i = 0; i < _segments.Count; ++i)
                _weights[i] /= totalWeight;
        }
        else
            _weights[0] = 1.0f;
    }

    private void EnumerateProjectedPositions<T>(float totalRotation, ref T callback)
        where T : struct, IProjectedPositionCallback
    {
        var position = Vector2f.Zero;
        var angle = 0.0f;
        for (var i = 0; i < _segments.Count; ++i)
        {
            angle += totalRotation * _weights[i];
            position += new Vector2f(MathDefs.Cos(angle), MathDefs.Sin(angle)) * _segments[i].Length;
            callback.Invoke(position);
        }
    }

    private (float Projection, Vector3f Offset) GetProjectionAndOffset(Vector3f target, Vector3f basePosition, Vector3f baseDirection)
    {
        var targetOffset = target - basePosition;
        var projection = targetOffset.ProjectOntoAxis(baseDirection);
        var normalOffset = targetOffset - baseDirection * projection;
        return (projection, normalOffset);
    }

    private Vector2f ProjectTarget(Vector3f target, Vector3f basePosition, Vector3f baseDirection)
    {
        var projection = GetProjectionAndOffset(target, basePosition, baseDirection);
        return new Vector2f(projection.Projection, projection.Offset.Length);
    }

    private Vector2f EvaluateProjectedEnd(float totalRotation)
    {
        var callback = new ProjectedEndCallback();
        EnumerateProjectedPositions(totalRotation, ref callback);
        return callback.Position;
    }

    private float EvaluateError(float totalRotation, Vector2f target)
    {
        var endPosition = EvaluateProjectedEnd(totalRotation);
        return (endPosition - target).LengthSquared;
    }

    private float FindBestAngle(Vector2f projectedTarget, float maxRotation, float angularTolerance)
    {
        var function = new ErrorFunction(this, projectedTarget);
        return SolveBisect(ref function, 0.0f, maxRotation, angularTolerance);
    }

    private void EvaluateSegmentPositions(float totalRotation, Vector3f baseDirection, Vector3f bendDirection)
    {
        var callback = new SegmentPositionCallback(this, _segments[0].BeginNode!.Position, baseDirection, bendDirection);
        EnumerateProjectedPositions(totalRotation, ref callback);
    }

    private readonly List<float> _weights = new();

    private interface IProjectedPositionCallback
    {
        void Invoke(Vector2f position);
    }

    private interface IErrorFunction
    {
        float Invoke(float value);
    }

    private struct ProjectedEndCallback : IProjectedPositionCallback
    {
        public Vector2f Position;

        public void Invoke(Vector2f position)
        {
            Position = position;
        }
    }

    private struct SegmentPositionCallback : IProjectedPositionCallback
    {
        public SegmentPositionCallback(
            IKSpineChain chain, Vector3f basePosition, Vector3f baseDirection, Vector3f bendDirection)
        {
            _chain = chain;
            _basePosition = basePosition;
            _baseDirection = baseDirection;
            _bendDirection = bendDirection;
        }

        public void Invoke(Vector2f position)
        {
            var segment = _chain._segments[_segmentIndex++];
            segment.EndNode!.Position = _basePosition + _baseDirection * position.X + _bendDirection * position.Y;
            _chain._segments[_segmentIndex - 1] = segment;
        }

        private readonly IKSpineChain _chain;
        private readonly Vector3f _basePosition;
        private readonly Vector3f _baseDirection;
        private readonly Vector3f _bendDirection;
        private int _segmentIndex;
    }

    private readonly struct ErrorFunction : IErrorFunction
    {
        public ErrorFunction(IKSpineChain chain, Vector2f target)
        {
            _chain = chain;
            _target = target;
        }

        public float Invoke(float value)
        {
            return _chain.EvaluateError(value, _target);
        }

        private readonly IKSpineChain _chain;
        private readonly Vector2f _target;
    }

    /// <summary>Solve error function f(x) for minimum value using bisection method.</summary>
    private static float SolveBisect<T>(
        ref T function, float minValue, float maxValue, float tolerance, uint maxIterations = 100)
        where T : struct, IErrorFunction
    {
        var approximateNumSteps = MathF.Ceiling(MathF.Log(tolerance / (maxValue - minValue + MathDefs.Epsilon)) / MathF.Log(0.5f));
        var numSteps = (uint)MathDefs.Clamp(approximateNumSteps, 1.0f, maxIterations);
        var beginValue = minValue;
        var beginError = (double)function.Invoke(beginValue);
        var endValue = maxValue;
        var endError = (double)function.Invoke(endValue);
        for (uint i = 0; i < numSteps; ++i)
        {
            var middleValue = (beginValue + endValue) / 2.0f;
            var middleError = (double)function.Invoke(middleValue);
            if (middleError >= beginError && middleError >= endError)
                break;
            if (beginError >= middleError && beginError >= endError)
            {
                beginValue = middleValue;
                beginError = middleError;
            }
            else
            {
                endValue = middleValue;
                endError = middleError;
            }
            if (endValue - beginValue < 2.0f * tolerance)
                break;
        }
        return (beginValue + endValue) / 2.0f;
    }
}
