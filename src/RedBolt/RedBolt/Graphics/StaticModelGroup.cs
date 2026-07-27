namespace RedBolt;

/// <summary>Renders several object instances while culling and receiving light as one unit. Can be used as a CPU-side optimization, but note that also regular StaticModels will use instanced rendering if possible.</summary>
public class StaticModelGroup : StaticModel
{
    private static readonly StringVector InstanceNodesStructureElementNames = new()
    {
        "Instance Count",
        "   NodeID"
    };

    private static readonly Matrix3x4[] IdentityWorldTransform = [Matrix3x4.Identity];

    /// <summary>Construct.</summary>
    public StaticModelGroup()
    {
        // Initialize the default node IDs attribute
        UpdateNodeIDs();

        using (var metadata = new Variant(InstanceNodesStructureElementNames))
        {
            RegisterAttribute(
                "Instance Nodes",
                typeof(VariantVector),
                self => ((StaticModelGroup)self).GetNodeIDsAttr(),
                (self, value) => ((StaticModelGroup)self).SetNodeIDsAttr((VariantVector)value!),
                new VariantVector(),
                AttributeMode.Default | AttributeMode.NodeIdVector)
                .SetMetadata(AttributeMetadata.VectorStructElements, metadata);
        }
    }

    /// <summary>Destruct.</summary>
    public override void Dispose()
    {
        _nodeIdsAttr.Dispose();
        _worldTransforms = [];
        for (var i = 0; i < _instanceNodes.Count; ++i)
        {
            if (_instanceNodes[i].TryGetTarget(out var instanceNode))
                instanceNode.RemoveListener(this);
        }
        _instanceNodes.Clear();
        base.Dispose();
    }

    /// <summary>Register object factory. StaticModel must be registered first.</summary>
    public new static void RegisterObject(Context context)
    {
        var reflection = context.AddFactoryReflection<StaticModelGroup>(ObjectCategory.Category_Geometry);
        reflection.SetObjectFactory(static () => new StaticModelGroup());
    }

    /// <summary>Apply attribute changes that can not be applied immediately. Called after scene load or a network update.</summary>
    public override void ApplyAttributes()
    {
        if (!_nodesDirty)
            return;

        // Remove all old instance nodes before searching for new
        for (var i = 0; i < _instanceNodes.Count; ++i)
        {
            if (_instanceNodes[i].TryGetTarget(out var instanceNode))
                instanceNode.RemoveListener(this);
        }

        _instanceNodes.Clear();

        var scene = Scene;
        if (scene is not null)
        {
            // The first index stores the number of IDs redundantly. This is for editing
            for (var i = 1; i < _nodeIdsAttr.Count; ++i)
            {
                var node = scene.GetNode(_nodeIdsAttr[i].GetUInt());
                if (node is not null)
                {
                    node.AddListener(this);
                    _instanceNodes.Add(new WeakReference<Node>(node));
                }
            }
        }

        _worldTransforms = new Matrix3x4[_instanceNodes.Count];
        _numWorldTransforms = 0; // Correct amount will be found during world bounding box update
        _nodesDirty = false;

        OnMarkedDirty(Node);
    }

    /// <summary>Process octree raycast. May be called from a worker thread.</summary>
    public override void ProcessRayQuery(RayOctreeQuery query, List<RayQueryResult> results)
    {
        // If no bones or no bone-level testing, use the Drawable test
        var level = query.Level;
        if (level < RayQueryLevel.RayAabb)
        {
            base.ProcessRayQuery(query, results);
            return;
        }

        // Check ray hit distance to AABB before proceeding with more accurate tests
        // GetWorldBoundingBox() updates the world transforms
        if (query.Ray.HitDistance(GetWorldBoundingBox()) >= query.MaxDistance)
            return;

        for (var i = 0u; i < _numWorldTransforms; ++i)
        {
            // Initial test using AABB
            var distanceAndNormal = query.Ray.HitDistanceAndNormal(GetBoundingBox().Transformed(_worldTransforms[i]));
            var distance = distanceAndNormal.Distance;
            var normal = distanceAndNormal.Normal;

            // Then proceed to OBB and triangle-level tests if necessary
            if (level >= RayQueryLevel.RayObb && distance < query.MaxDistance)
            {
                var inverse = _worldTransforms[i].Inverse;
                var localRay = query.Ray.Transformed(inverse);
                distance = localRay.HitDistance(GetBoundingBox());

                if (level == RayQueryLevel.RayTriangle && distance < query.MaxDistance)
                {
                    distance = float.PositiveInfinity;

                    for (var j = 0; j < _batches.Count; ++j)
                    {
                        var geometry = _batches[j].Geometry;
                        if (geometry is not null)
                        {
                            var geometryDistance = geometry.GetHitDistance(localRay, out var geometryNormal);
                            if (geometryDistance < query.MaxDistance && geometryDistance < distance)
                            {
                                distance = geometryDistance;
                                normal = (_worldTransforms[i].Matrix3 * geometryNormal).Normalized;
                            }
                        }
                    }
                }
            }

            if (distance < query.MaxDistance)
            {
                results.Add(new RayQueryResult
                {
                    Position = query.Ray.Origin + distance * query.Ray.Direction,
                    Normal = normal,
                    Distance = distance,
                    Drawable = this,
                    Node = Node,
                    SubObject = i
                });
            }
        }
    }

    /// <summary>Calculate distance and prepare batches for rendering. May be called from worker thread(s), possibly re-entrantly.</summary>
    public override void UpdateBatches(FrameInfo frame)
    {
        // Getting the world bounding box ensures the transforms are updated
        var worldBoundingBox = GetWorldBoundingBox();
        var worldTransform = Node.GetWorldTransform();
        var camera = frame.Camera!;
        SetDistance(camera.GetDistance(worldBoundingBox.Center));

        if (_batches.Count > 1)
        {
            for (var i = 0; i < _batches.Count; ++i)
            {
                var batch = _batches[i];
                batch.Distance = camera.GetDistance(worldTransform * _geometryData[i].Center);
                batch.WorldTransform = _numWorldTransforms != 0 ? _worldTransforms : IdentityWorldTransform;
                batch.NumWorldTransforms = _numWorldTransforms;
                _batches[i] = batch;
            }
        }
        else if (_batches.Count == 1)
        {
            var batch = _batches[0];
            batch.Distance = GetDistance();
            batch.WorldTransform = _numWorldTransforms != 0 ? _worldTransforms : IdentityWorldTransform;
            batch.NumWorldTransforms = _numWorldTransforms;
            _batches[0] = batch;
        }

        var scale = worldBoundingBox.Size.DotProduct(GraphicsDefs.DotScale);
        var newLodDistance = camera.GetLodDistance(GetDistance(), scale, LodBias);

        if (newLodDistance != GetLodDistance())
        {
            SetLodDistance(newLodDistance);
            CalculateLodLevels();
        }
    }

    /// <summary>Return number of occlusion geometry triangles.</summary>
    public override uint GetNumOccluderTriangles()
    {
        // Make sure instance transforms are up-to-date
        _ = GetWorldBoundingBox();

        var triangles = 0u;

        for (var i = 0; i < _batches.Count; ++i)
        {
            var geometry = GetLodGeometry((uint)i, _occlusionLodLevel);
            if (geometry is null)
                continue;

            // Check that the material is suitable for occlusion (default material always is)
            var material = _batches[i].Material;
            if (material is not null && !material.GetOcclusion())
                continue;

            triangles += _numWorldTransforms * geometry.GetIndexCount() / 3u;
        }

        return triangles;
    }

    /// <summary>Draw to occlusion buffer. Return true if did not run out of triangles.</summary>
    public override bool DrawOcclusion(OcclusionBuffer buffer)
    {
        // Make sure instance transforms are up-to-date
        _ = GetWorldBoundingBox();

        for (var i = 0u; i < _numWorldTransforms; ++i)
        {
            for (var j = 0; j < _batches.Count; ++j)
            {
                var geometry = GetLodGeometry((uint)j, _occlusionLodLevel);
                if (geometry is null)
                    continue;

                // Check that the material is suitable for occlusion (default material always is) and set culling mode
                var material = _batches[j].Material;
                if (material is not null)
                {
                    if (!material.GetOcclusion())
                        continue;
                    buffer.SetCullMode(material.GetCullMode());
                }
                else
                    buffer.SetCullMode(CullMode.CounterClockwise);

                geometry.GetRawDataShared(out var vertexData, out var vertexSize, out var indexData, out var indexSize,
                    out var elements);
                // Check for valid geometry data
                if (vertexData is null || indexData is null || elements is null)
                    continue;
                var positionElement = VertexBuffer.GetElement(elements, VertexElementType.Vector3,
                    VertexElementSemantic.Position);
                if (positionElement is null || positionElement.Value.Offset != 0)
                    continue;

                var indexStart = geometry.GetIndexStart();
                var indexCount = geometry.GetIndexCount();

                // Draw and check for running out of triangles
                if (!buffer.AddTriangles(_worldTransforms[i], vertexData, vertexSize, indexData, indexSize, indexStart,
                        indexCount))
                    return false;
            }
        }

        return true;
    }

    /// <summary>Add an instance scene node. It does not need any drawable components of its own.</summary>
    public void AddInstanceNode(Node? node)
    {
        if (node is null)
            return;

        for (var i = 0; i < _instanceNodes.Count; ++i)
        {
            if (_instanceNodes[i].TryGetTarget(out var instanceNode) && ReferenceEquals(instanceNode, node))
                return;
        }

        // Add as a listener for the instance node, so that we know to dirty the transforms when the node moves or is enabled/disabled
        node.AddListener(this);
        _instanceNodes.Add(new WeakReference<Node>(node));
        UpdateNumTransforms();
    }

    /// <summary>Remove an instance scene node.</summary>
    public void RemoveInstanceNode(Node? node)
    {
        if (node is null)
            return;

        for (var i = 0; i < _instanceNodes.Count; ++i)
        {
            if (_instanceNodes[i].TryGetTarget(out var instanceNode) && ReferenceEquals(instanceNode, node))
            {
                node.RemoveListener(this);
                _instanceNodes.RemoveAt(i);
                UpdateNumTransforms();
                return;
            }
        }
    }

    /// <summary>Remove all instance scene nodes.</summary>
    public void RemoveAllInstanceNodes()
    {
        for (var i = 0; i < _instanceNodes.Count; ++i)
        {
            if (_instanceNodes[i].TryGetTarget(out var instanceNode))
                instanceNode.RemoveListener(this);
        }

        _instanceNodes.Clear();
        UpdateNumTransforms();
    }

    /// <summary>Return number of instance nodes.</summary>
    public uint GetNumInstanceNodes() => checked((uint)_instanceNodes.Count);

    /// <summary>Return instance node by index.</summary>
    public Node? GetInstanceNode(uint index)
    {
        return index < _instanceNodes.Count && _instanceNodes[(int)index].TryGetTarget(out var node) ? node : null;
    }

    /// <summary>Set node IDs attribute.</summary>
    public void SetNodeIDsAttr(VariantVector value)
    {
        // Just remember the node IDs. They need to go through the SceneResolver, and we actually find the nodes during
        // ApplyAttributes()
        if (value.Count != 0)
        {
            _nodeIdsAttr.Clear();

            var index = 0;
            var numInstances = value[index++].GetUInt();
            // Prevent crash on entering negative value in the editor
            if (numInstances > (uint)MathDefs.MaxInt)
                numInstances = 0;

            _nodeIdsAttr.Add(numInstances);
            while (numInstances-- != 0)
            {
                // If vector contains less IDs than should, fill the rest with zeroes
                if (index < value.Count)
                    _nodeIdsAttr.Add(value[index++].GetUInt());
                else
                    _nodeIdsAttr.Add(0u);
            }
        }
        else
        {
            _nodeIdsAttr.Clear();
            _nodeIdsAttr.Add(0u);
        }

        _nodesDirty = true;
        _nodeIdsDirty = false;
    }

    /// <summary>Return node IDs attribute.</summary>
    public VariantVector GetNodeIDsAttr()
    {
        if (_nodeIdsDirty)
            UpdateNodeIDs();

        return _nodeIdsAttr;
    }

    /// <summary>Handle scene node enabled status changing.</summary>
    protected internal override void OnNodeSetEnabled(Node node)
    {
        base.OnMarkedDirty(node);
    }

    /// <summary>Recalculate the world-space bounding box.</summary>
    protected override void OnWorldBoundingBoxUpdate()
    {
        // Update transforms and bounding box at the same time to have to go through the objects only once
        var index = 0u;

        var worldBox = new BoundingBox();
        var hasWorldBox = false;

        for (var i = 0; i < _instanceNodes.Count; ++i)
        {
            if (!_instanceNodes[i].TryGetTarget(out var node) || !node.IsEnabled)
                continue;

            var worldTransform = node.GetWorldTransform();
            _worldTransforms[index++] = worldTransform;
            var instanceBox = GetBoundingBox().Transformed(worldTransform);
            if (!hasWorldBox)
            {
                worldBox = instanceBox;
                hasWorldBox = true;
            }
            else
                worldBox.Merge(instanceBox);
        }

        SetWorldBoundingBox(worldBox);

        // Store the amount of valid instances we found instead of resizing worldTransforms_. This is because this function may be
        // called from multiple worker threads simultaneously
        _numWorldTransforms = index;
    }

    /// <summary>Ensure proper size of world transforms when nodes are added/removed. Also mark node IDs dirty.</summary>
    private void UpdateNumTransforms()
    {
        _worldTransforms = new Matrix3x4[_instanceNodes.Count];
        _numWorldTransforms = 0; // Correct amount will be during world bounding box update
        _nodeIdsDirty = true;

        OnMarkedDirty(Node);
    }

    /// <summary>Update node IDs attribute from the actual nodes.</summary>
    private void UpdateNodeIDs()
    {
        var numInstances = checked((uint)_instanceNodes.Count);

        _nodeIdsAttr.Clear();
        _nodeIdsAttr.Add(numInstances);

        for (var i = 0u; i < numInstances; ++i)
            _nodeIdsAttr.Add(_instanceNodes[(int)i].TryGetTarget(out var node) ? node.Id : 0u);

        _nodeIdsDirty = false;
    }

    /// <summary>Instance nodes.</summary>
    private readonly List<WeakReference<Node>> _instanceNodes = [];
    /// <summary>World transforms of valid (existing and visible) instances.</summary>
    private Matrix3x4[] _worldTransforms = [];
    /// <summary>IDs of instance nodes for serialization.</summary>
    private readonly VariantVector _nodeIdsAttr = [];
    /// <summary>Number of valid instance node transforms.</summary>
    private uint _numWorldTransforms;
    /// <summary>Whether node IDs have been set and nodes should be searched for during ApplyAttributes.</summary>
    private bool _nodesDirty;
    /// <summary>Whether nodes have been manipulated by the API and node ID attribute should be refreshed.</summary>
    private bool _nodeIdsDirty;
}
