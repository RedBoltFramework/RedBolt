namespace RedBolt;

public sealed class StaticModelGroup : StaticModel
{
    // TODO: port - C++ code:
    // bool DrawOcclusion(OcclusionBuffer* buffer) override;

    // C++: ea::vector<WeakPtr<Node>> instanceNodes_;
    // C++: mutable VariantVector nodeIDsAttr_;

    private static readonly Matrix3x4[] _identityWorldTransform = [Matrix3x4.Identity];
    private readonly List<Node> _instanceNodes = [];
    private readonly List<Matrix3x4> _worldTransforms = [];
    private Matrix3x4[] _worldTransformsArray = [];
    private readonly List<uint> _nodeIdsAttribute = [0u];
    private uint _numWorldTransforms;
    private bool _nodesDirty;
    private bool _nodeIdsDirty;

    public void AddInstanceNode(Node? node)
    {
        if (node is null || _instanceNodes.Contains(node))
            return;

        node.AddListener(this);
        _instanceNodes.Add(node);
        UpdateNumTransforms();
    }

    public void RemoveInstanceNode(Node? node)
    {
        if (node is null || !_instanceNodes.Remove(node))
            return;

        node.RemoveListener(this);
        UpdateNumTransforms();
    }

    public void RemoveAllInstanceNodes()
    {
        foreach (var node in _instanceNodes)
            node.RemoveListener(this);
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

        foreach (var node in _instanceNodes)
            node.RemoveListener(this);
        _instanceNodes.Clear();
        if (Scene is { } scene)
        {
            for (var i = 1; i < _nodeIdsAttribute.Count; ++i)
            {
                var node = scene.GetNode(_nodeIdsAttribute[i]);
                if (node is not null)
                {
                    node.AddListener(this);
                    _instanceNodes.Add(node);
                }
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
        base.OnMarkedDirty(node);
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
        for (var i = 0u; i < index; ++i)
            _worldTransformsArray[(int)i] = _worldTransforms[(int)i];
        SetWorldBoundingBox(worldBox);
    }

    public void ProcessRayQuery(RayOctreeQuery query)
    {
        foreach (var result in CreateRayQueryResults(query.Ray, query.Level, query.MaxDistance))
            query.Result.Add(result);
    }

    public override uint GetNumOccluderTriangles()
    {
        _ = GetWorldBoundingBox();
        var triangles = 0u;
        for (var i = 0u; i < NumGeometries; ++i)
        {
            var geometry = GetLodGeometry(i, OcclusionLodLevel);
            if (geometry is null)
                continue;

            var material = GetMaterial(i);
            if (material is not null && !material.GetOcclusion())
                continue;

            triangles += _numWorldTransforms * geometry.GetIndexCount() / 3u;
        }

        return triangles;
    }

    public override void UpdateBatches(CommonFrameInfo frame)
    {
        var worldBoundingBox = GetWorldBoundingBox();
        var worldTransform = Node.GetWorldTransform();
        SetDistance(frame.Camera!.GetDistance(worldBoundingBox.Center));

        if (_batches.Count > 1)
        {
            var model = Model;
            for (var i = 0; i < _batches.Count; ++i)
            {
                var batch = _batches[i];
                var center = model?.GetGeometryCenter((uint)i) ?? Vector3f.Zero;
                batch.Distance = frame.Camera.GetDistance(worldTransform * center);
                batch.WorldTransform = _numWorldTransforms > 0 ? _worldTransformsArray : _identityWorldTransform;
                batch.NumWorldTransforms = _numWorldTransforms;
                _batches[i] = batch;
            }
        }
        else if (_batches.Count == 1)
        {
            var batch = _batches[0];
            batch.Distance = GetDistance();
            batch.WorldTransform = _numWorldTransforms > 0 ? _worldTransformsArray : _identityWorldTransform;
            batch.NumWorldTransforms = _numWorldTransforms;
            _batches[0] = batch;
        }

        var size = worldBoundingBox.Size;
        var scale = (size.X + size.Y + size.Z) / 3.0f;
        var newLodDistance = frame.Camera.GetLodDistance(GetDistance(), scale, LodBias);
        if (newLodDistance != GetLodDistance())
            SetLodDistance(newLodDistance);
    }

    internal IEnumerable<RayQueryResult> CreateRayQueryResults(Ray ray, RayQueryLevel level, float maxDistance)
    {
        _ = GetWorldBoundingBox();
        for (var i = 0; i < _numWorldTransforms; ++i)
        {
            var distance = ray.HitDistance(GetBoundingBox().Transformed(_worldTransforms[i]));
            if (level >= RayQueryLevel.RayObb && distance < maxDistance)
            {
                var localRay = ray.Transformed(_worldTransforms[i].Inverse);
                distance = localRay.HitDistance(GetBoundingBox());

                if (level == RayQueryLevel.RayTriangle && distance < maxDistance)
                {
                    distance = float.PositiveInfinity;
                    for (var j = 0u; j < NumGeometries; ++j)
                    {
                        var geometry = GetLodGeometry(j, 0);
                        if (geometry is null)
                            continue;

                        var geometryDistance = geometry.GetHitDistance(localRay);
                        if (geometryDistance < maxDistance && geometryDistance < distance)
                            distance = geometryDistance;
                    }
                }
            }

            if (float.IsPositiveInfinity(distance) || distance >= maxDistance)
                continue;

            yield return new RayQueryResult
            {
                Drawable = this,
                Node = IsAttached ? Node : null,
                Distance = distance,
                Position = ray.Origin + ray.Direction * distance,
                Normal = -ray.Direction,
                TextureUV = Vector2f.Zero,
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

        if (_worldTransformsArray.Length != _worldTransforms.Count)
            _worldTransformsArray = new Matrix3x4[_worldTransforms.Count];

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
