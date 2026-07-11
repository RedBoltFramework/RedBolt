using System;
using System.Collections.Generic;

namespace RedBolt;

/// <summary>Uniformly bending IK chain.</summary>
public class IKSpineChain : IKChain
{
    public delegate float WeightFunction(float x);

    public static float DefaultWeightFunction(float x) => 1.0f;

    public void Solve(Vector3f target, Vector3f baseDirection, float maxRotation, IKSettings settings,
        WeightFunction? weightFun = null)
    {
        if (_nodes.Count < 2)
            return;

        StorePreviousTransforms();
        UpdateSegmentWeights(weightFun ?? DefaultWeightFunction);

        var basePosition = _nodes[0].Position;
        var bendDirection = GetProjectionAndOffset(target, basePosition, baseDirection).Offset.Normalized;
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

    private readonly List<float> _weights = new();

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

    private void EnumerateProjectedPositions(float totalRotation, Action<Vector2f> callback)
    {
        var position = Vector2f.Zero;
        var angle = 0.0f;
        for (var i = 0; i < _segments.Count; ++i)
        {
            angle += totalRotation * _weights[i];
            position += new Vector2f(MathDefs.Cos(angle), MathDefs.Sin(angle)) * _segments[i].Length;
            callback(position);
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
        var position = Vector2f.Zero;
        EnumerateProjectedPositions(totalRotation, projectedPosition => position = projectedPosition);
        return position;
    }

    private float EvaluateError(float totalRotation, Vector2f target)
    {
        var endPosition = EvaluateProjectedEnd(totalRotation);
        return (endPosition - target).LengthSquared;
    }

    private float FindBestAngle(Vector2f projectedTarget, float maxRotation, float angularTolerance)
    {
        return SolveBisect(angle => EvaluateError(angle, projectedTarget), 0.0f, maxRotation, angularTolerance);
    }

    private void EvaluateSegmentPositions(float totalRotation, Vector3f baseDirection, Vector3f bendDirection)
    {
        var basePosition = _segments[0].BeginNode!.Position;
        var segmentIndex = 0;
        EnumerateProjectedPositions(totalRotation, position =>
        {
            var segment = _segments[segmentIndex++];
            segment.EndNode!.Position = basePosition + baseDirection * position.X + bendDirection * position.Y;
            _segments[segmentIndex - 1] = segment;
        });
    }

    private static float SolveBisect(Func<float, float> function, float minValue, float maxValue, float tolerance, uint maxIterations = 100)
    {
        var approximateNumSteps = MathF.Ceiling(MathF.Log(tolerance / (maxValue - minValue + float.Epsilon)) / MathF.Log(0.5f));
        var numSteps = (uint)MathDefs.Clamp(approximateNumSteps, 1.0f, maxIterations);
        var beginValue = minValue;
        var beginError = (double)function(beginValue);
        var endValue = maxValue;
        var endError = (double)function(endValue);
        for (uint i = 0; i < numSteps; ++i)
        {
            var middleValue = (beginValue + endValue) / 2.0f;
            var middleError = (double)function(middleValue);
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
