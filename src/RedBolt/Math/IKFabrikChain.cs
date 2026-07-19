using System;

namespace RedBolt;

/// <summary>Generic unconstrained FABRIK chain.</summary>
public class IKFabrikChain : IKChain, ICloneable
{
    public IKFabrikChain()
    {
    }

    public IKFabrikChain(IKFabrikChain chain)
        : base(chain)
    {
    }

    public new object Clone() => new IKFabrikChain(this);

    public void Solve(Vector3f target, IKSettings settings)
    {
        if (_segments.Count == 0)
            return;

        var targetNode = _segments[^1].EndNode!;
        if ((targetNode.Position - target).Length < settings.Tolerance)
            return;

        StorePreviousTransforms();
        // Don't do more than one attempt for now
        TrySolve(target, settings);
        UpdateSegmentRotations(settings);
    }

    private void SolveIteration(Vector3f target, IKSettings settings, bool backward)
    {
        var nextPosition = target;
        if (backward)
        {
            for (var i = _segments.Count - 1; i >= 0; --i)
                nextPosition = IterateSegment(_segments[i], nextPosition, true);
        }
        else
        {
            for (var i = 0; i < _segments.Count; ++i)
                nextPosition = IterateSegment(_segments[i], nextPosition, false);
        }
    }

    private bool TrySolve(Vector3f target, IKSettings settings)
    {
        var targetNode = _segments[^1].EndNode!;
        float? previousError = null;
        for (uint i = 0; i < settings.MaxIterations; ++i)
        {
            var startPosition = _segments[0].BeginNode!.Position;
            SolveIteration(target, settings, true);
            SolveIteration(startPosition, settings, false);
            var error = (targetNode.Position - target).Length;
            if (previousError.HasValue && error >= previousError.Value)
                return false;
            previousError = error;
            if ((targetNode.Position - target).Length < settings.Tolerance)
                break;
        }
        return true;
    }

    private static Vector3f IterateSegment(IKNodeSegment segment, Vector3f target, bool backward)
    {
        var targetNode = backward ? segment.EndNode! : segment.BeginNode!;
        var adjustedNode = backward ? segment.BeginNode! : segment.EndNode!;
        targetNode.Position = target;
        var direction = (adjustedNode.Position - targetNode.Position).Normalized;
        adjustedNode.Position = direction * segment.Length + targetNode.Position;
        return adjustedNode.Position;
    }
}
