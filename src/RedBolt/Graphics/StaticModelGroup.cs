namespace RedBolt;

public sealed class StaticModelGroup : StaticModel
{
    private readonly List<Node> _instanceNodes = [];
    private readonly List<Matrix3x4> _worldTransforms = [];
    private readonly List<uint> _nodeIdsAttribute = [0u];
    private uint _numWorldTransforms;
    private bool _nodesDirty;
    private bool _nodeIdsDirty;

    public void AddInstanceNode(Node? node)
    {
        if (node is null || _instanceNodes.Contains(node))
            return;

        _instanceNodes.Add(node);
        UpdateNumTransforms();
    }

    public void RemoveInstanceNode(Node? node)
    {
        if (node is null || !_instanceNodes.Remove(node))
            return;

        UpdateNumTransforms();
    }

    public void RemoveAllInstanceNodes()
    {
        if (_instanceNodes.Count == 0)
            return;

        _instanceNodes.Clear();
        UpdateNumTransforms();
    }

    public uint GetNumInstanceNodes() => (uint)_instanceNodes.Count;

    public Node? GetInstanceNode(uint index) => index < _instanceNodes.Count ? _instanceNodes[(int)index] : null;

    public IReadOnlyList<Matrix3x4> GetWorldTransforms()
    {
        _ = GetWorldBoundingBox();
        return _worldTransforms.Take((int)_numWorldTransforms).ToArray();
    }

    public uint GetNumWorldTransforms()
    {
        _ = GetWorldBoundingBox();
        return _numWorldTransforms;
    }

    public void SetNodeIDsAttr(IEnumerable<uint>? values)
    {
        _nodeIdsAttribute.Clear();
        var source = values?.ToArray() ?? [];
        if (source.Length == 0)
        {
            _nodeIdsAttribute.Add(0u);
        }
        else
        {
            var count = source[0] > int.MaxValue ? 0u : source[0];
            _nodeIdsAttribute.Add(count);
            for (var i = 0u; i < count; ++i)
            {
                var sourceIndex = i + 1u;
                _nodeIdsAttribute.Add(sourceIndex < source.Length ? source[(int)sourceIndex] : 0u);
            }
        }

        _nodesDirty = true;
        _nodeIdsDirty = false;
    }

    public IReadOnlyList<uint> GetNodeIDsAttr()
    {
        if (_nodeIdsDirty)
            UpdateNodeIDs();

        return _nodeIdsAttribute.ToArray();
    }

    public override void ApplyAttributes()
    {
        if (!_nodesDirty)
            return;

        _instanceNodes.Clear();
        if (IsAttached)
        {
            for (var i = 1; i < _nodeIdsAttribute.Count; ++i)
            {
                var node = Scene.GetNode(_nodeIdsAttribute[i]);
                if (node is not null)
                    _instanceNodes.Add(node);
            }
        }

        _nodesDirty = false;
        UpdateNumTransforms(markNodeIdsDirty: false);
    }

    protected internal override void OnMarkedDirty(Node node)
    {
        base.OnMarkedDirty(node);
        MarkForUpdate();
    }

    protected internal override void OnNodeSetEnabled(Node node)
    {
        MarkForUpdate();
    }

    protected override void OnWorldBoundingBoxUpdate()
    {
        var index = 0u;
        var worldBox = new BoundingBox();
        var hasWorldBox = false;
        foreach (var node in _instanceNodes)
        {
            if (!node.IsEnabled)
                continue;

            var transform = node.GetWorldTransform();
            _worldTransforms[(int)index++] = transform;
            var instanceBox = GetBoundingBox().Transformed(transform);
            if (!hasWorldBox)
            {
                worldBox = instanceBox;
                hasWorldBox = true;
            }
            else
            {
                worldBox.Merge(instanceBox);
            }
        }

        _numWorldTransforms = index;
        SetWorldBoundingBox(worldBox);
    }

    internal IEnumerable<RayQueryResult> CreateRayQueryResults(Ray ray, float maxDistance)
    {
        _ = GetWorldBoundingBox();
        for (var i = 0; i < _numWorldTransforms; ++i)
        {
            var distance = ray.HitDistance(GetBoundingBox().Transformed(_worldTransforms[i]));
            if (float.IsPositiveInfinity(distance) || distance > maxDistance)
                continue;

            yield return new RayQueryResult
            {
                Drawable = this,
                Node = IsAttached ? Node : null,
                Distance = distance,
                Position = ray.Origin + ray.Direction * distance,
                Normal = -ray.Direction,
                TextureUV = Vector2.Zero,
                SubObject = (uint)i
            };
        }
    }

    private void UpdateNumTransforms(bool markNodeIdsDirty = true)
    {
        while (_worldTransforms.Count < _instanceNodes.Count)
            _worldTransforms.Add(Matrix3x4.Identity);
        while (_worldTransforms.Count > _instanceNodes.Count)
            _worldTransforms.RemoveAt(_worldTransforms.Count - 1);

        _numWorldTransforms = 0;
        _nodeIdsDirty = markNodeIdsDirty;
        MarkForUpdate();
    }

    private void UpdateNodeIDs()
    {
        _nodeIdsAttribute.Clear();
        _nodeIdsAttribute.Add((uint)_instanceNodes.Count);
        foreach (var node in _instanceNodes)
            _nodeIdsAttribute.Add(node.Id);
        _nodeIdsDirty = false;
    }
}
