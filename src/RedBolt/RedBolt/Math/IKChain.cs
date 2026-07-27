using System.Collections.Generic;

namespace RedBolt;

/// <summary>Base class for generic IK chain.</summary>
public class IKChain : ICloneable
{
    public IKChain()
    {
    }

    public IKChain(IKChain chain)
    {
        _isFirstSegmentIncomplete = chain._isFirstSegmentIncomplete;
        _nodes.AddRange(chain._nodes);
        _segments.AddRange(chain._segments);
        _totalLength = chain._totalLength;
    }

    public object Clone() => new IKChain(this);

    public void Clear()
    {
        _nodes.Clear();
        _segments.Clear();
    }

    public void AddNode(IKNode node)
    {
        _nodes.Add(node);

        if (_segments.Count == 0)
        {
            var segment = new IKNodeSegment { BeginNode = node, EndNode = node };
            _segments.Add(segment);
            _isFirstSegmentIncomplete = true;
        }
        else if (_isFirstSegmentIncomplete)
        {
            var last = _segments.Count - 1;
            var segment = _segments[last];
            segment.EndNode = node;
            _segments[last] = segment;
            _isFirstSegmentIncomplete = false;
        }
        else
        {
            var segment = new IKNodeSegment { BeginNode = _segments[^1].EndNode, EndNode = node };
            _segments.Add(segment);
        }
    }

    public void UpdateLengths()
    {
        _totalLength = 0.0f;
        for (var i = 0; i < _segments.Count; ++i)
        {
            var segment = _segments[i];
            segment.UpdateLength();
            _segments[i] = segment;
            _totalLength += segment.Length;
        }
    }

    public IKNodeSegment? FindSegment(IKNode node)
    {
        foreach (var segment in _segments)
        {
            if (segment.BeginNode == node || segment.EndNode == node)
                return segment;
        }
        return null;
    }

    public List<IKNodeSegment> GetSegments() => _segments;

    public List<IKNode> GetNodes() => _nodes;

    protected void StorePreviousTransforms()
    {
        if (_segments.Count == 0)
            return;

        foreach (var segment in _segments)
        {
            segment.BeginNode!.PreviousPosition = segment.BeginNode.Position;
            segment.BeginNode.PreviousRotation = segment.BeginNode.Rotation;
        }

        var lastNode = _segments[^1].EndNode!;
        lastNode.PreviousPosition = lastNode.Position;
        lastNode.PreviousRotation = lastNode.Rotation;
    }

    protected void RestorePreviousTransforms()
    {
        foreach (var segment in _segments)
        {
            segment.BeginNode!.Position = segment.BeginNode.PreviousPosition;
            segment.BeginNode.Rotation = segment.BeginNode.PreviousRotation;
        }

        var lastNode = _segments[^1].EndNode!;
        lastNode.Position = lastNode.PreviousPosition;
        lastNode.Rotation = lastNode.PreviousRotation;
    }

    protected void UpdateSegmentRotations(IKSettings settings)
    {
        for (var i = 0; i < _segments.Count; ++i)
        {
            var segment = _segments[i];
            segment.UpdateRotationInNodes(settings.ContinuousRotations, i == _segments.Count - 1);
            _segments[i] = segment;
        }
    }

    protected bool _isFirstSegmentIncomplete;
    protected readonly List<IKNode> _nodes = new();
    protected readonly List<IKNodeSegment> _segments = new();
    protected float _totalLength;
}
