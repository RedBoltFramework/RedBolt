namespace RedBolt;

public sealed class StaticModelGroup : StaticModel
{
    private readonly List<Node> instanceNodes = [];
    private readonly List<Matrix3x4> worldTransforms = [];
    private readonly List<uint> nodeIdsAttribute = [0u];
    private uint numWorldTransforms;
    private bool nodesDirty;
    private bool nodeIdsDirty;

    public void AddInstanceNode(Node? node)
    {
        if (node is null || instanceNodes.Contains(node))
            return;

        instanceNodes.Add(node);
        UpdateNumTransforms();
    }

    public void RemoveInstanceNode(Node? node)
    {
        if (node is null || !instanceNodes.Remove(node))
            return;

        UpdateNumTransforms();
    }

    public void RemoveAllInstanceNodes()
    {
        if (instanceNodes.Count == 0)
            return;

        instanceNodes.Clear();
        UpdateNumTransforms();
    }

    public uint GetNumInstanceNodes() => (uint)instanceNodes.Count;

    public Node? GetInstanceNode(uint index) => index < instanceNodes.Count ? instanceNodes[(int)index] : null;

    public IReadOnlyList<Matrix3x4> GetWorldTransforms()
    {
        _ = GetWorldBoundingBox();
        return worldTransforms.Take((int)numWorldTransforms).ToArray();
    }

    public uint GetNumWorldTransforms()
    {
        _ = GetWorldBoundingBox();
        return numWorldTransforms;
    }

    public void SetNodeIDsAttr(IEnumerable<uint>? values)
    {
        nodeIdsAttribute.Clear();
        var source = values?.ToArray() ?? [];
        if (source.Length == 0)
        {
            nodeIdsAttribute.Add(0u);
        }
        else
        {
            var count = source[0] > int.MaxValue ? 0u : source[0];
            nodeIdsAttribute.Add(count);
            for (var i = 0u; i < count; ++i)
            {
                var sourceIndex = i + 1u;
                nodeIdsAttribute.Add(sourceIndex < source.Length ? source[(int)sourceIndex] : 0u);
            }
        }

        nodesDirty = true;
        nodeIdsDirty = false;
    }

    public IReadOnlyList<uint> GetNodeIDsAttr()
    {
        if (nodeIdsDirty)
            UpdateNodeIDs();

        return nodeIdsAttribute.ToArray();
    }

    public override void ApplyAttributes()
    {
        if (!nodesDirty)
            return;

        instanceNodes.Clear();
        if (IsAttached)
        {
            for (var i = 1; i < nodeIdsAttribute.Count; ++i)
            {
                var node = Scene.GetNode(nodeIdsAttribute[i]);
                if (node is not null)
                    instanceNodes.Add(node);
            }
        }

        nodesDirty = false;
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
        foreach (var node in instanceNodes)
        {
            if (!node.IsEnabled)
                continue;

            var transform = node.GetWorldTransform();
            worldTransforms[(int)index++] = transform;
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

        numWorldTransforms = index;
        SetWorldBoundingBox(worldBox);
    }

    internal IEnumerable<RayQueryResult> CreateRayQueryResults(Ray ray, float maxDistance)
    {
        _ = GetWorldBoundingBox();
        for (var i = 0; i < numWorldTransforms; ++i)
        {
            var distance = ray.HitDistance(GetBoundingBox().Transformed(worldTransforms[i]));
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
        while (worldTransforms.Count < instanceNodes.Count)
            worldTransforms.Add(Matrix3x4.Identity);
        while (worldTransforms.Count > instanceNodes.Count)
            worldTransforms.RemoveAt(worldTransforms.Count - 1);

        numWorldTransforms = 0;
        nodeIdsDirty = markNodeIdsDirty;
        MarkForUpdate();
    }

    private void UpdateNodeIDs()
    {
        nodeIdsAttribute.Clear();
        nodeIdsAttribute.Add((uint)instanceNodes.Count);
        foreach (var node in instanceNodes)
            nodeIdsAttribute.Add(node.Id);
        nodeIdsDirty = false;
    }
}
