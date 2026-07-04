namespace RedBolt;

public sealed class CustomGeometryVertex
{
    public Vector3 Position;
    public Vector3 Normal;
    public uint Color;
    public Vector2 TexCoord;
    public Vector4 Tangent;
}

public sealed class CustomGeometry : Drawable
{
    private const uint MaskPosition = 1u;
    private const uint MaskNormal = 1u << 1;
    private const uint MaskColor = 1u << 2;
    private const uint MaskTexCoord = 1u << 3;
    private const uint MaskTangent = 1u << 4;
    private static readonly uint MaterialType = StringHash.Calculate("Material");

    private readonly List<PrimitiveType> primitiveTypes = [];
    private readonly List<List<CustomGeometryVertex>> vertices = [];
    private readonly List<Geometry> geometries = [];
    private readonly List<Material?> materials = [];
    private readonly VertexBuffer vertexBuffer = new();
    private uint elementMask = MaskPosition;
    private uint geometryIndex;
    private ResourceRefList materialsAttr = new(MaterialType);
    private bool materialsAttrExplicit;

    public CustomGeometry()
        : base(DrawableFlags.Geometry)
    {
        vertexBuffer.SetShadowed(true);
        SetNumGeometries(1u);
        SetLocalBoundingBox(CreateUndefinedBoundingBox());
    }

    public bool Dynamic { get; private set; }

    public uint ElementMask => elementMask;

    public void Clear()
    {
        elementMask = MaskPosition;
        geometryIndex = 0u;
        primitiveTypes.Clear();
        vertices.Clear();
        geometries.Clear();
        materials.Clear();
        materialsAttr = new ResourceRefList(MaterialType);
        materialsAttrExplicit = false;
        SetLocalBoundingBox(CreateUndefinedBoundingBox());
    }

    public void SetNumGeometries(uint num)
    {
        while (geometries.Count < num)
        {
            geometries.Add(new Geometry());
            primitiveTypes.Add(PrimitiveType.TriangleList);
            vertices.Add([]);
            materials.Add(null);
        }

        while (geometries.Count > num)
        {
            var index = geometries.Count - 1;
            geometries.RemoveAt(index);
            primitiveTypes.RemoveAt(index);
            vertices.RemoveAt(index);
            materials.RemoveAt(index);
        }

        if (geometryIndex >= geometries.Count)
            geometryIndex = geometries.Count == 0 ? 0u : (uint)geometries.Count - 1u;
    }

    public void SetDynamic(bool enable)
    {
        Dynamic = enable;
    }

    public bool BeginGeometry(uint index, PrimitiveType type)
    {
        if (index >= geometries.Count)
            return false;

        geometryIndex = index;
        primitiveTypes[(int)index] = type;
        vertices[(int)index].Clear();
        if (index == 0u)
            elementMask = MaskPosition;
        return true;
    }

    public void DefineVertex(Vector3 position)
    {
        if (geometryIndex >= vertices.Count)
            return;

        vertices[(int)geometryIndex].Add(new CustomGeometryVertex { Position = position });
    }

    public void DefineNormal(Vector3 normal)
    {
        if (!TryGetLastVertex(out var vertex))
            return;

        vertex.Normal = normal;
        elementMask |= MaskNormal;
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
        elementMask |= MaskColor;
    }

    public void DefineTexCoord(Vector2 texCoord)
    {
        if (!TryGetLastVertex(out var vertex))
            return;

        vertex.TexCoord = texCoord;
        elementMask |= MaskTexCoord;
    }

    public void DefineTangent(Vector4 tangent)
    {
        if (!TryGetLastVertex(out var vertex))
            return;

        vertex.Tangent = tangent;
        elementMask |= MaskTangent;
    }

    public bool DefineGeometry(uint index, PrimitiveType type, uint numVertices, bool hasNormals, bool hasColors, bool hasTexCoords, bool hasTangents)
    {
        if (index >= geometries.Count)
            return false;

        geometryIndex = index;
        primitiveTypes[(int)index] = type;
        vertices[(int)index].Clear();
        for (var i = 0u; i < numVertices; ++i)
            vertices[(int)index].Add(new CustomGeometryVertex());

        if (index == 0u)
            elementMask = MaskPosition;
        if (hasNormals)
            elementMask |= MaskNormal;
        if (hasColors)
            elementMask |= MaskColor;
        if (hasTexCoords)
            elementMask |= MaskTexCoord;
        if (hasTangents)
            elementMask |= MaskTangent;
        return true;
    }

    public void Commit()
    {
        var totalVertices = 0u;
        var box = new BoundingBox();
        box.Clear();
        foreach (var geometryVertices in vertices)
        {
            totalVertices += (uint)geometryVertices.Count;
            foreach (var vertex in geometryVertices)
                box.Merge(vertex.Position);
        }

        SetLocalBoundingBox(box);
        var vertexSize = GetVertexSize(elementMask);
        vertexBuffer.SetSize(totalVertices, vertexSize, Dynamic);
        vertexBuffer.SetData(GetPackedVertexData());

        var vertexStart = 0u;
        for (var i = 0; i < geometries.Count; ++i)
        {
            var vertexCount = (uint)vertices[i].Count;
            geometries[i].SetVertexBuffer(0u, vertexBuffer);
            geometries[i].SetDrawRange(primitiveTypes[i], 0u, 0u, vertexStart, vertexCount);
            vertexStart += vertexCount;
        }
    }

    public void SetMaterial(Material? material)
    {
        for (var i = 0; i < materials.Count; ++i)
            materials[i] = material;
        materialsAttrExplicit = false;
    }

    public bool SetMaterial(uint index, Material? material)
    {
        if (index >= materials.Count)
            return false;

        materials[(int)index] = material;
        materialsAttrExplicit = false;
        return true;
    }

    public uint GetNumGeometries() => (uint)geometries.Count;

    public uint GetNumVertices(uint index) => index < vertices.Count ? (uint)vertices[(int)index].Count : 0u;

    public bool IsDynamic() => Dynamic;

    public Material? GetMaterial(uint index = 0u) => index < materials.Count ? materials[(int)index] : null;

    public List<List<CustomGeometryVertex>> GetVertices() => vertices;

    public CustomGeometryVertex? GetVertex(uint geometryIndex, uint vertexNum)
    {
        return geometryIndex < vertices.Count && vertexNum < vertices[(int)geometryIndex].Count
            ? vertices[(int)geometryIndex][(int)vertexNum]
            : null;
    }

    public Geometry? GetLodGeometry(uint batchIndex, uint level) => batchIndex < geometries.Count ? geometries[(int)batchIndex] : null;

    public uint GetNumOccluderTriangles()
    {
        var triangles = 0u;
        for (var i = 0; i < geometries.Count; ++i)
        {
            if (materials[i] is { Occlusion: false })
                continue;
            triangles += geometries[i].GetPrimitiveCount();
        }

        return triangles;
    }

    public IReadOnlyList<byte> GetGeometryDataAttr()
    {
        var buffer = new VectorBuffer();
        buffer.WriteVLE((uint)geometries.Count);
        buffer.WriteUInt(elementMask);
        for (var i = 0; i < geometries.Count; ++i)
        {
            buffer.WriteVLE((uint)vertices[i].Count);
            buffer.WriteUByte((byte)primitiveTypes[i]);
            foreach (var vertex in vertices[i])
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
        elementMask = buffer.ReadUInt() | MaskPosition;
        for (var i = 0; i < geometries.Count; ++i)
        {
            var numVertices = buffer.ReadVLE();
            primitiveTypes[i] = (PrimitiveType)buffer.ReadUByte();
            vertices[i].Clear();
            for (var j = 0u; j < numVertices; ++j)
                vertices[i].Add(ReadVertex(buffer));
        }

        Commit();
    }

    public void SetMaterialsAttr(ResourceRefList? value)
    {
        materialsAttr = value is null ? new ResourceRefList(MaterialType) : new ResourceRefList(value.Type, value.Names);
        materialsAttrExplicit = true;
    }

    public ResourceRefList GetMaterialsAttr()
    {
        if (materialsAttrExplicit)
            return new ResourceRefList(materialsAttr.Type, materialsAttr.Names);

        return new ResourceRefList(MaterialType, materials.Select(static material => material?.Name ?? string.Empty));
    }

    public VertexBuffer GetVertexBuffer() => vertexBuffer;

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
            TextureUV = Vector2.Zero,
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
        if (geometryIndex >= vertices.Count || vertices[(int)geometryIndex].Count == 0)
            return false;

        vertex = vertices[(int)geometryIndex][^1];
        return true;
    }

    private byte[] GetPackedVertexData()
    {
        var buffer = new VectorBuffer();
        foreach (var geometryVertices in vertices)
        {
            foreach (var vertex in geometryVertices)
                WriteVertex(buffer, vertex);
        }

        return buffer.GetBuffer();
    }

    private void WriteVertex(VectorBuffer buffer, CustomGeometryVertex vertex)
    {
        if ((elementMask & MaskPosition) != 0u)
            buffer.WriteVector3(vertex.Position);
        if ((elementMask & MaskNormal) != 0u)
            buffer.WriteVector3(vertex.Normal);
        if ((elementMask & MaskColor) != 0u)
            buffer.WriteUInt(vertex.Color);
        if ((elementMask & MaskTexCoord) != 0u)
            buffer.WriteVector2(vertex.TexCoord);
        if ((elementMask & MaskTangent) != 0u)
            buffer.WriteVector4(vertex.Tangent);
    }

    private CustomGeometryVertex ReadVertex(MemoryBuffer buffer)
    {
        var vertex = new CustomGeometryVertex();
        if ((elementMask & MaskPosition) != 0u)
            vertex.Position = buffer.ReadVector3();
        if ((elementMask & MaskNormal) != 0u)
            vertex.Normal = buffer.ReadVector3();
        if ((elementMask & MaskColor) != 0u)
            vertex.Color = buffer.ReadUInt();
        if ((elementMask & MaskTexCoord) != 0u)
            vertex.TexCoord = buffer.ReadVector2();
        if ((elementMask & MaskTangent) != 0u)
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
