namespace RedBolt;

public sealed class CustomGeometryVertex
{
    public Vector3f Position;
    public Vector3f Normal;
    public uint Color;
    public Vector2f TexCoord;
    public Vector4f Tangent;
}

public sealed class CustomGeometry : Drawable
{
    private const uint MaskPosition = 1u;
    private const uint MaskNormal = 1u << 1;
    private const uint MaskColor = 1u << 2;
    private const uint MaskTexCoord = 1u << 3;
    private const uint MaskTangent = 1u << 4;
    private readonly List<PrimitiveType> _primitiveTypes = [];
    private readonly List<List<CustomGeometryVertex>> _vertices = [];
    private readonly List<Geometry> _geometries = [];
    private readonly List<Material?> _materials = [];
    private readonly VertexBuffer _vertexBuffer = new();
    private uint _elementMask = MaskPosition;
    private uint _geometryIndex;

    public CustomGeometry()
        : base(DrawableFlags.Geometry)
    {
        _vertexBuffer.SetShadowed(true);
        SetNumGeometries(1u);
        SetLocalBoundingBox(CreateUndefinedBoundingBox());
    }

    public bool Dynamic { get; private set; }

    public uint ElementMask => _elementMask;

    public void Clear()
    {
        _elementMask = MaskPosition;
        _geometryIndex = 0u;
        _primitiveTypes.Clear();
        _vertices.Clear();
        _geometries.Clear();
        _materials.Clear();
        SetLocalBoundingBox(CreateUndefinedBoundingBox());
    }

    public void SetNumGeometries(uint num)
    {
        while (_geometries.Count < num)
        {
            _geometries.Add(new Geometry());
            _primitiveTypes.Add(PrimitiveType.TriangleList);
            _vertices.Add([]);
            _materials.Add(null);
        }

        while (_geometries.Count > num)
        {
            var index = _geometries.Count - 1;
            _geometries.RemoveAt(index);
            _primitiveTypes.RemoveAt(index);
            _vertices.RemoveAt(index);
            _materials.RemoveAt(index);
        }

        if (_geometryIndex >= _geometries.Count)
            _geometryIndex = _geometries.Count == 0 ? 0u : (uint)_geometries.Count - 1u;
    }

    public void SetDynamic(bool enable)
    {
        Dynamic = enable;
    }

    public void BeginGeometry(uint index, PrimitiveType type)
    {
        if (index >= _geometries.Count)
            return;

        _geometryIndex = index;
        _primitiveTypes[(int)index] = type;
        _vertices[(int)index].Clear();
        if (index == 0u)
            _elementMask = MaskPosition;
    }

    public void DefineVertex(Vector3f position)
    {
        if (_geometryIndex >= _vertices.Count)
            return;

        _vertices[(int)_geometryIndex].Add(new CustomGeometryVertex { Position = position });
    }

    public void DefineNormal(Vector3f normal)
    {
        if (!TryGetLastVertex(out var vertex))
            return;

        vertex.Normal = normal;
        _elementMask |= MaskNormal;
    }

    public void DefineColor(Color color)
    {
        DefineColor(color.ToUInt());
    }

    public void DefineColor(uint color)
    {
        if (!TryGetLastVertex(out var vertex))
            return;

        vertex.Color = color;
        _elementMask |= MaskColor;
    }

    public void DefineTexCoord(Vector2f texCoord)
    {
        if (!TryGetLastVertex(out var vertex))
            return;

        vertex.TexCoord = texCoord;
        _elementMask |= MaskTexCoord;
    }

    public void DefineTangent(Vector4f tangent)
    {
        if (!TryGetLastVertex(out var vertex))
            return;

        vertex.Tangent = tangent;
        _elementMask |= MaskTangent;
    }

    public void DefineGeometry(uint index, PrimitiveType type, uint numVertices, bool hasNormals, bool hasColors, bool hasTexCoords, bool hasTangents)
    {
        if (index >= _geometries.Count)
            return;

        _geometryIndex = index;
        _primitiveTypes[(int)index] = type;
        _vertices[(int)index].Clear();
        for (var i = 0u; i < numVertices; ++i)
            _vertices[(int)index].Add(new CustomGeometryVertex());

        if (index == 0u)
            _elementMask = MaskPosition;
        if (hasNormals)
            _elementMask |= MaskNormal;
        if (hasColors)
            _elementMask |= MaskColor;
        if (hasTexCoords)
            _elementMask |= MaskTexCoord;
        if (hasTangents)
            _elementMask |= MaskTangent;
    }

    public void Commit()
    {
        var totalVertices = 0u;
        var box = new BoundingBox();
        box.Clear();
        foreach (var geometryVertices in _vertices)
        {
            totalVertices += (uint)geometryVertices.Count;
            foreach (var vertex in geometryVertices)
                box.Merge(vertex.Position);
        }

        SetLocalBoundingBox(box);
        var vertexSize = GetVertexSize(_elementMask);
        _vertexBuffer.SetSize(totalVertices, vertexSize, Dynamic);
        _vertexBuffer.SetData(GetPackedVertexData());

        var vertexStart = 0u;
        for (var i = 0; i < _geometries.Count; ++i)
        {
            var vertexCount = (uint)_vertices[i].Count;
            _geometries[i].SetVertexBuffer(0u, _vertexBuffer);
            _geometries[i].SetDrawRange(_primitiveTypes[i], 0u, 0u, vertexStart, vertexCount);
            vertexStart += vertexCount;
        }
    }

    public void SetMaterial(Material? material)
    {
        for (var i = 0; i < _materials.Count; ++i)
            _materials[i] = material;
    }

    public bool SetMaterial(uint index, Material? material)
    {
        if (index >= _materials.Count)
            return false;

        _materials[(int)index] = material;
        return true;
    }

    public uint GetNumGeometries() => (uint)_geometries.Count;

    public uint GetNumVertices(uint index) => index < _vertices.Count ? (uint)_vertices[(int)index].Count : 0u;

    public bool IsDynamic() => Dynamic;

    public Material? GetMaterial(uint index = 0u) => index < _materials.Count ? _materials[(int)index] : null;

    public List<List<CustomGeometryVertex>> GetVertices() => _vertices;

    public CustomGeometryVertex? GetVertex(uint geometryIndex, uint vertexNum)
    {
        return geometryIndex < _vertices.Count && vertexNum < _vertices[(int)geometryIndex].Count
            ? _vertices[(int)geometryIndex][(int)vertexNum]
            : null;
    }

    public Geometry? GetLodGeometry(uint batchIndex, uint level) => batchIndex < _geometries.Count ? _geometries[(int)batchIndex] : null;

    public uint GetNumOccluderTriangles()
    {
        var triangles = 0u;
        for (var i = 0; i < _geometries.Count; ++i)
        {
            if (_materials[i] is { Occlusion: false })
                continue;
            triangles += _geometries[i].GetVertexCount() / 3u;
        }

        return triangles;
    }

    public IReadOnlyList<byte> GetGeometryDataAttr()
    {
        var buffer = new VectorBuffer();
        buffer.WriteVLE((uint)_geometries.Count);
        buffer.WriteUInt(_elementMask);
        for (var i = 0; i < _geometries.Count; ++i)
        {
            buffer.WriteVLE((uint)_vertices[i].Count);
            buffer.WriteUByte((byte)_primitiveTypes[i]);
            foreach (var vertex in _vertices[i])
                WriteVertex(buffer, vertex);
        }

        return buffer.GetBuffer();
    }

    public void SetGeometryDataAttr(IEnumerable<byte>? value)
    {
        var data = value?.ToArray() ?? [];
        if (data.Length == 0)
            return;

        var buffer = new MemoryBuffer(data);
        SetNumGeometries(buffer.ReadVLE());
        _elementMask = buffer.ReadUInt() | MaskPosition;
        for (var i = 0; i < _geometries.Count; ++i)
        {
            var numVertices = buffer.ReadVLE();
            _primitiveTypes[i] = (PrimitiveType)buffer.ReadUByte();
            _vertices[i].Clear();
            for (var j = 0u; j < numVertices; ++j)
                _vertices[i].Add(ReadVertex(buffer));
        }

        Commit();
    }

    public void SetMaterialsAttr(ResourceRefList value)
    {
        var cache = GetSubsystem<ResourceCache>()!;
        var count = Math.Min(value.Names.Count, _materials.Count);
        for (var i = 0; i < count; ++i)
        {
            var name = value.Names[i];
            _materials[i] = string.IsNullOrEmpty(name) ? null : cache.GetResourceOrNull<Material>(name);
        }

        for (var i = count; i < _materials.Count; ++i)
            _materials[i] = null;
    }

    public ResourceRefList GetMaterialsAttr() => Resource.GetResourceRefList(_materials);

    public VertexBuffer GetVertexBuffer() => _vertexBuffer;

    internal IEnumerable<RayQueryResult> CreateRayQueryResults(Ray ray, RayQueryLevel level, float maxDistance)
    {
        if (level == RayQueryLevel.RayTriangleUv)
            yield break;

        var distance = level == RayQueryLevel.RayAabb ? ray.HitDistance(GetWorldBoundingBox()) : HitDistanceObb(ray);
        if (float.IsPositiveInfinity(distance) || distance > maxDistance)
            yield break;

        yield return new RayQueryResult
        {
            Drawable = this,
            Node = IsAttached ? Node : null,
            Distance = distance,
            Position = ray.Origin + ray.Direction * distance,
            Normal = -ray.Direction,
            TextureUV = Vector2f.Zero,
            SubObject = uint.MaxValue
        };
    }

    protected override void OnWorldBoundingBoxUpdate()
    {
        SetWorldBoundingBox(!BoundingBox.Defined
            ? CreateUndefinedBoundingBox()
            : IsAttached ? BoundingBox.Transformed(Node.GetWorldTransform()) : BoundingBox);
    }

    private bool TryGetLastVertex(out CustomGeometryVertex vertex)
    {
        vertex = null!;
        if (_geometryIndex >= _vertices.Count || _vertices[(int)_geometryIndex].Count == 0)
            return false;

        vertex = _vertices[(int)_geometryIndex][^1];
        return true;
    }

    private byte[] GetPackedVertexData()
    {
        var buffer = new VectorBuffer();
        foreach (var geometryVertices in _vertices)
        {
            foreach (var vertex in geometryVertices)
                WriteVertex(buffer, vertex);
        }

        return buffer.GetBuffer();
    }

    private void WriteVertex(VectorBuffer buffer, CustomGeometryVertex vertex)
    {
        if ((_elementMask & MaskPosition) != 0u)
            buffer.WriteVector3(vertex.Position);
        if ((_elementMask & MaskNormal) != 0u)
            buffer.WriteVector3(vertex.Normal);
        if ((_elementMask & MaskColor) != 0u)
            buffer.WriteUInt(vertex.Color);
        if ((_elementMask & MaskTexCoord) != 0u)
            buffer.WriteVector2(vertex.TexCoord);
        if ((_elementMask & MaskTangent) != 0u)
            buffer.WriteVector4(vertex.Tangent);
    }

    private CustomGeometryVertex ReadVertex(MemoryBuffer buffer)
    {
        var vertex = new CustomGeometryVertex();
        if ((_elementMask & MaskPosition) != 0u)
            vertex.Position = buffer.ReadVector3();
        if ((_elementMask & MaskNormal) != 0u)
            vertex.Normal = buffer.ReadVector3();
        if ((_elementMask & MaskColor) != 0u)
            vertex.Color = buffer.ReadUInt();
        if ((_elementMask & MaskTexCoord) != 0u)
            vertex.TexCoord = buffer.ReadVector2();
        if ((_elementMask & MaskTangent) != 0u)
            vertex.Tangent = buffer.ReadVector4();
        return vertex;
    }

    private float HitDistanceObb(Ray ray)
    {
        if (!IsAttached)
            return ray.HitDistance(BoundingBox);

        var localOrigin = Node.WorldToLocal(ray.Origin);
        var localEnd = Node.WorldToLocal(ray.Origin + ray.Direction);
        var localDirection = localEnd - localOrigin;
        if (localDirection.LengthSquared <= MathDefs.Epsilon * MathDefs.Epsilon)
            return float.PositiveInfinity;

        var localRay = new Ray(localOrigin, localDirection);
        var localDistance = localRay.HitDistance(BoundingBox);
        if (float.IsPositiveInfinity(localDistance))
            return localDistance;

        var localHit = localRay.Origin + localRay.Direction * localDistance;
        return (Node.LocalToWorld(localHit) - ray.Origin).Length;
    }

    private static uint GetVertexSize(uint mask)
    {
        var size = 0u;
        if ((mask & MaskPosition) != 0u)
            size += 12u;
        if ((mask & MaskNormal) != 0u)
            size += 12u;
        if ((mask & MaskColor) != 0u)
            size += 4u;
        if ((mask & MaskTexCoord) != 0u)
            size += 8u;
        if ((mask & MaskTangent) != 0u)
            size += 16u;
        return size;
    }

    private static BoundingBox CreateUndefinedBoundingBox()
    {
        var box = new BoundingBox();
        box.Clear();
        return box;
    }
}
