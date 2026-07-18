using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace RedBolt;

public struct CustomGeometryVertex
{
    public Vector3f Position;
    public Vector3f Normal;
    public uint Color;
    public Vector2f TexCoord;
    public Vector4f Tangent;
}

public sealed class CustomGeometry : Drawable
{
    private const uint MaskPosition = (uint)VertexMask.Position;
    private const uint MaskNormal = (uint)VertexMask.Normal;
    private const uint MaskColor = (uint)VertexMask.Color;
    private const uint MaskTexCoord = (uint)VertexMask.TexCoord1;
    private const uint MaskTangent = (uint)VertexMask.Tangent;
    private readonly List<PrimitiveType> _primitiveTypes = [];
    private readonly List<List<CustomGeometryVertex>> _vertices = [];
    private readonly List<Geometry> _geometries = [];
    private readonly VertexBuffer _vertexBuffer = new();
    private uint _elementMask = MaskPosition;
    private uint _geometryIndex;
    private ResourceRefList _materialsAttr = new(nameof(Material));
    private bool _dynamic;

    public CustomGeometry()
        : base(DrawableFlags.Geometry)
    {
        _vertexBuffer.SetDebugName("CustomGeometry Geometry");
        _vertexBuffer.SetShadowed(true);
        SetNumGeometries(1u);
    }

    public override void Dispose()
    {
        _materialsAttr.Names.Clear();
        for (var i = _geometries.Count - 1; i >= 0; --i)
            _geometries[i].Dispose();
        _geometries.Clear();
        _vertexBuffer.Dispose();
        _vertices.Clear();
        _primitiveTypes.Clear();
        for (var i = _batches.Count - 1; i >= 0; --i)
        {
            var batch = _batches[i];
            batch.Dispose();
        }
        _batches.Clear();
        base.Dispose();
    }

    public void Clear()
    {
        _elementMask = MaskPosition;
        for (var i = _batches.Count - 1; i >= 0; --i)
        {
            var batch = _batches[i];
            batch.Dispose();
        }
        _batches.Clear();
        for (var i = _geometries.Count - 1; i >= 0; --i)
            _geometries[i].Dispose();
        _geometries.Clear();
        _primitiveTypes.Clear();
        _vertices.Clear();
    }

    public void SetNumGeometries(uint num)
    {
        while (_batches.Count < num)
            _batches.Add(new SourceBatch());
        while (_geometries.Count < num)
            _geometries.Add(null!);
        while (_primitiveTypes.Count < num)
            _primitiveTypes.Add(PrimitiveType.TriangleList);
        while (_vertices.Count < num)
            _vertices.Add([]);

        while (_batches.Count > num)
        {
            var index = _batches.Count - 1;
            var batch = _batches[index];
            batch.Dispose();
            _batches.RemoveAt(index);
        }
        while (_geometries.Count > num)
        {
            var index = _geometries.Count - 1;
            _geometries[index].Dispose();
            _geometries.RemoveAt(index);
        }
        while (_primitiveTypes.Count > num)
            _primitiveTypes.RemoveAt(_primitiveTypes.Count - 1);
        while (_vertices.Count > num)
            _vertices.RemoveAt(_vertices.Count - 1);

        for (var i = 0; i < _geometries.Count; ++i)
        {
            _geometries[i] ??= new Geometry();
            var batch = _batches[i];
            batch.Geometry = _geometries[i].GetVertexCount() != 0 ? _geometries[i] : null;
            _batches[i] = batch;
        }
    }

    public void SetDynamic(bool enable)
    {
        _dynamic = enable;
    }

    public void BeginGeometry(uint index, PrimitiveType type)
    {
        if (index > _geometries.Count)
            return;

        _geometryIndex = index;
        _primitiveTypes[(int)index] = type;
        _vertices[(int)index].Clear();
        if (index == 0u)
            _elementMask = MaskPosition;
    }

    public void DefineVertex(Vector3f position)
    {
        if (_vertices.Count < _geometryIndex)
            return;

        _vertices[(int)_geometryIndex].Add(new CustomGeometryVertex { Position = position });
    }

    public void DefineNormal(Vector3f normal)
    {
        if (_vertices.Count < _geometryIndex || _vertices[(int)_geometryIndex].Count == 0)
            return;

        ref var vertex = ref CollectionsMarshal.AsSpan(_vertices[(int)_geometryIndex])[^1];
        vertex.Normal = normal;
        _elementMask |= MaskNormal;
    }

    public void DefineColor(Color color)
    {
        if (_vertices.Count < _geometryIndex || _vertices[(int)_geometryIndex].Count == 0)
            return;

        ref var vertex = ref CollectionsMarshal.AsSpan(_vertices[(int)_geometryIndex])[^1];
        vertex.Color = color.ToUInt();
        _elementMask |= MaskColor;
    }

    public void DefineTexCoord(Vector2f texCoord)
    {
        if (_vertices.Count < _geometryIndex || _vertices[(int)_geometryIndex].Count == 0)
            return;

        ref var vertex = ref CollectionsMarshal.AsSpan(_vertices[(int)_geometryIndex])[^1];
        vertex.TexCoord = texCoord;
        _elementMask |= MaskTexCoord;
    }

    public void DefineTangent(Vector4f tangent)
    {
        if (_vertices.Count < _geometryIndex || _vertices[(int)_geometryIndex].Count == 0)
            return;

        ref var vertex = ref CollectionsMarshal.AsSpan(_vertices[(int)_geometryIndex])[^1];
        vertex.Tangent = tangent;
        _elementMask |= MaskTangent;
    }

    public void DefineGeometry(uint index, PrimitiveType type, uint numVertices, bool hasNormals, bool hasColors, bool hasTexCoords, bool hasTangents)
    {
        if (index > _geometries.Count)
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
        if (_vertexBuffer.GetVertexCount() != totalVertices || (uint)_vertexBuffer.GetElementMask() != _elementMask ||
            _vertexBuffer.IsDynamic != _dynamic)
            _vertexBuffer.SetSize(totalVertices, _elementMask, _dynamic);

        if (totalVertices != 0)
        {
            var destination = _vertexBuffer.Map();
            if (!destination.IsEmpty)
            {
                var offset = 0;
                foreach (var geometryVertices in _vertices)
                {
                    foreach (var vertex in geometryVertices)
                        WriteVertex(destination, ref offset, vertex);
                }
                _vertexBuffer.Unmap();

                var vertexStart = 0u;
                for (var i = 0; i < _geometries.Count; ++i)
                {
                    var vertexCount = (uint)_vertices[i].Count;
                    _geometries[i].SetVertexBuffer(0u, _vertexBuffer);
                    _geometries[i].SetDrawRange(_primitiveTypes[i], 0u, 0u, vertexStart, vertexCount);
                    vertexStart += vertexCount;
                }
            }
        }
        _vertexBuffer.ClearDataLost();
        SetNumGeometries((uint)_geometries.Count);
    }

    public void SetMaterial(Material? material)
    {
        for (var i = 0; i < _batches.Count; ++i)
        {
            var batch = _batches[i];
            batch.Material = material;
            _batches[i] = batch;
        }
    }

    public bool SetMaterial(uint index, Material? material)
    {
        if (index >= _batches.Count)
            return false;

        var batch = _batches[(int)index];
        batch.Material = material;
        _batches[(int)index] = batch;
        return true;
    }

    public uint GetNumGeometries() => (uint)_geometries.Count;

    public uint GetNumVertices(uint index) => index < _vertices.Count ? (uint)_vertices[(int)index].Count : 0u;

    public bool IsDynamic() => _dynamic;

    public Material? GetMaterial(uint index = 0u) => index < _batches.Count ? _batches[(int)index].Material : null;

    public List<List<CustomGeometryVertex>> GetVertices() => _vertices;

    public ref CustomGeometryVertex GetVertex(uint geometryIndex, uint vertexNum)
    {
        if (geometryIndex < _vertices.Count && vertexNum < _vertices[(int)geometryIndex].Count)
            return ref CollectionsMarshal.AsSpan(_vertices[(int)geometryIndex])[(int)vertexNum];
        return ref Unsafe.NullRef<CustomGeometryVertex>();
    }

    public override Geometry? GetLodGeometry(uint batchIndex, uint level) => batchIndex < _geometries.Count ? _geometries[(int)batchIndex] : null;

    public override uint GetNumOccluderTriangles()
    {
        var triangles = 0u;
        for (var i = 0; i < _batches.Count; ++i)
        {
            var geometry = GetLodGeometry((uint)i, 0);
            if (geometry is null)
                continue;

            var material = _batches[i].Material;
            if (material is not null && !material.GetOcclusion())
                continue;

            triangles += geometry.GetVertexCount() / 3u;
        }

        return triangles;
    }

    /// <summary>Draw to occlusion buffer. Return true if did not run out of triangles.</summary>
    public override bool DrawOcclusion(OcclusionBuffer buffer)
    {
        var success = true;
        for (var i = 0; i < _batches.Count; ++i)
        {
            var geometry = GetLodGeometry((uint)i, 0);
            if (geometry is null)
                continue;

            var material = _batches[i].Material;
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
            if (vertexData is null || elements is null)
                continue;
            var positionElement = VertexBuffer.GetElement(elements, VertexElementType.Vector3,
                VertexElementSemantic.Position);
            if (positionElement is null || positionElement.Value.Offset != 0)
                continue;

            success = buffer.AddTriangles(Node.GetWorldTransform(), vertexData, vertexSize, geometry.GetVertexStart(),
                geometry.GetVertexCount());
            if (!success)
                break;
        }
        return success;
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
        _elementMask = buffer.ReadUInt();
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
        for (var i = 0; i < value.Names.Count; ++i)
            SetMaterial((uint)i, cache.GetResourceOrNull<Material>(value.Names[i]));
    }

    public ResourceRefList GetMaterialsAttr()
    {
        while (_materialsAttr.Names.Count < _batches.Count)
            _materialsAttr.Names.Add(string.Empty);
        while (_materialsAttr.Names.Count > _batches.Count)
            _materialsAttr.Names.RemoveAt(_materialsAttr.Names.Count - 1);
        for (var i = 0; i < _batches.Count; ++i)
            _materialsAttr.Names[i] = _batches[i].Material?.Name ?? string.Empty;
        return _materialsAttr;
    }

    public override void ProcessRayQuery(RayOctreeQuery query, List<RayQueryResult> results)
    {
        switch (query.Level)
        {
            case RayQueryLevel.RayAabb:
                base.ProcessRayQuery(query, results);
                break;

            case RayQueryLevel.RayObb:
            case RayQueryLevel.RayTriangle:
            {
                var worldTransform = Node.GetWorldTransform();
                var inverse = worldTransform.Inverse;
                var localRay = query.Ray.Transformed(inverse);
                var distanceAndNormal = localRay.HitDistanceAndNormal(BoundingBox);
                var distance = distanceAndNormal.Distance;
                var normal = worldTransform * distanceAndNormal.Normal.ToVector4(0.0f);

                if (query.Level == RayQueryLevel.RayTriangle && distance < query.MaxDistance)
                {
                    distance = float.PositiveInfinity;

                    for (var i = 0; i < _batches.Count; ++i)
                    {
                        var geometry = _batches[i].Geometry;
                        if (geometry is null)
                            continue;

                        var geometryDistance = geometry.GetHitDistance(localRay, out var geometryNormal);
                        if (geometryDistance < query.MaxDistance && geometryDistance < distance)
                        {
                            distance = geometryDistance;
                            normal = (Node.GetWorldTransform() * geometryNormal.ToVector4()).Normalized;
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
                        SubObject = MathDefs.MaxUnsigned
                    });
                }
                break;
            }

            case RayQueryLevel.RayTriangleUv:
                Log.Logger.LogWarning("RAY_TRIANGLE_UV query level is not supported for CustomGeometry component");
                break;
        }
    }

    protected override void OnWorldBoundingBoxUpdate()
    {
        SetWorldBoundingBox(!BoundingBox.Defined
            ? CreateUndefinedBoundingBox()
            : IsAttached ? BoundingBox.Transformed(Node.GetWorldTransform()) : BoundingBox);
    }

    private void WriteVertex(VectorBuffer buffer, in CustomGeometryVertex vertex)
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

    private void WriteVertex(Span<byte> destination, ref int offset, in CustomGeometryVertex vertex)
    {
        if ((_elementMask & MaskPosition) != 0u)
        {
            MemoryMarshal.Write(destination[offset..], in vertex.Position);
            offset += 12;
        }
        if ((_elementMask & MaskNormal) != 0u)
        {
            MemoryMarshal.Write(destination[offset..], in vertex.Normal);
            offset += 12;
        }
        if ((_elementMask & MaskColor) != 0u)
        {
            MemoryMarshal.Write(destination[offset..], in vertex.Color);
            offset += 4;
        }
        if ((_elementMask & MaskTexCoord) != 0u)
        {
            MemoryMarshal.Write(destination[offset..], in vertex.TexCoord);
            offset += 8;
        }
        if ((_elementMask & MaskTangent) != 0u)
        {
            MemoryMarshal.Write(destination[offset..], in vertex.Tangent);
            offset += 16;
        }
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

    private static BoundingBox CreateUndefinedBoundingBox()
    {
        var box = new BoundingBox();
        box.Clear();
        return box;
    }
}
