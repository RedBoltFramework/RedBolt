using Microsoft.Extensions.Logging;

namespace RedBolt;

public enum PrimitiveType
{
    TriangleList,
    LineList,
    PointList,
    TriangleStrip,
    LineStrip,
    TriangleFan
}

public enum GeometryType
{
    Static,
    Skinned,
    Instanced,
    Billboard,
    DirectionalBillboard,
    TrailFaceCamera,
    TrailBone,
    MaxGeometryTypes,
    StaticNoInstancing
}

/// <summary>Defines one or more vertex buffers, an index buffer and a draw range.</summary>
public sealed class Geometry : PipelineStateTracker
{
    /// <summary>Vertex buffers.</summary>
    private readonly List<VertexBuffer?> _vertexBuffers = [null];

    /// <summary>Vertex buffers dependencies.</summary>
    private readonly List<PipelineStateSubscription> _vertexBuffersDependencies = [new()];

    /// <summary>Index buffer.</summary>
    public IndexBuffer? IndexBuffer { get; private set; }

    /// <summary>Index buffer dependency.</summary>
    private PipelineStateSubscription _indexBufferDependency = new();

    /// <summary>Primitive type.</summary>
    public PrimitiveType PrimitiveType { get; private set; } = PrimitiveType.TriangleList;

    /// <summary>Start index.</summary>
    public uint IndexStart { get; private set; }

    /// <summary>Number of indices.</summary>
    public uint IndexCount { get; private set; }

    /// <summary>First used vertex.</summary>
    public uint VertexStart { get; private set; }

    /// <summary>Number of used vertices.</summary>
    public uint VertexCount { get; private set; }

    /// <summary>LOD distance.</summary>
    private float _lodDistance;

    /// <summary>Raw vertex data elements.</summary>
    private List<VertexElement> _rawElements = [];

    /// <summary>Raw vertex data override.</summary>
    private byte[]? _rawVertexData;

    /// <summary>Raw index data override.</summary>
    private byte[]? _rawIndexData;

    /// <summary>Raw vertex data override size.</summary>
    private uint _rawVertexSize;

    /// <summary>Raw index data override size.</summary>
    private uint _rawIndexSize;

    /// <summary>Whether the geometry uses headless buffers.</summary>
    private bool _isHeadless;

    /// <summary>Construct with one empty vertex buffer.</summary>
    public Geometry()
    {
        SetNumVertexBuffers(1);
    }

    /// <summary>Register object with the engine.</summary>
    public static void RegisterObject(Context context)
    {
        context.AddFactoryReflection<Geometry>();
    }

    /// <summary>Set number of vertex buffers.</summary>
    public bool SetNumVertexBuffers(uint count)
    {
        if (count >= RenderApiLimits.MaxVertexStreams)
        {
            Log.Logger.LogError("Too many vertex streams");
            return false;
        }

        foreach (var dependency in _vertexBuffersDependencies)
            dependency.Dispose();

        _vertexBuffersDependencies.Clear();
        _vertexBuffers.Clear();
        for (var i = 0u; i < count; ++i)
        {
            _vertexBuffersDependencies.Add(new PipelineStateSubscription());
            _vertexBuffers.Add(null);
        }

        return true;
    }

    /// <summary>Set a vertex buffer by index.</summary>
    public bool SetVertexBuffer(uint index, VertexBuffer? buffer)
    {
        if (index >= _vertexBuffers.Count)
        {
            Log.Logger.LogError("Stream index out of bounds");
            return false;
        }

        var dependency = CreateDependency(buffer);
        _vertexBuffersDependencies[(int)index].Dispose();
        _vertexBuffersDependencies[(int)index] = dependency;
        _vertexBuffers[(int)index] = buffer;

        UpdateHeadlessStatus();
        return true;
    }

    /// <summary>Set all vertex buffers at once.</summary>
    public void SetVertexBuffers(IEnumerable<VertexBuffer?> buffers)
    {
        ArgumentNullException.ThrowIfNull(buffers);

        var newBuffers = buffers.ToList();
        foreach (var dependency in _vertexBuffersDependencies)
            dependency.Dispose();

        _vertexBuffersDependencies.Clear();
        _vertexBuffers.Clear();
        foreach (var buffer in newBuffers)
        {
            _vertexBuffersDependencies.Add(CreateDependency(buffer));
            _vertexBuffers.Add(buffer);
        }

        UpdateHeadlessStatus();
    }

    /// <summary>Set the index buffer.</summary>
    public void SetIndexBuffer(IndexBuffer? buffer)
    {
        var dependency = CreateDependency(IndexBuffer);
        _indexBufferDependency.Dispose();
        _indexBufferDependency = dependency;
        IndexBuffer = buffer;

        UpdateHeadlessStatus();
    }

    /// <summary>Set the draw range.</summary>
    public bool SetDrawRange(PrimitiveType type, uint indexStart, uint indexCount, bool getUsedVertexRange = true)
    {
        if (IndexBuffer is null && _rawIndexData is null)
        {
            Log.Logger.LogError("Null index buffer and no raw index data, can not define indexed draw range");
            return false;
        }

        if (IndexBuffer is not null && unchecked(indexStart + indexCount) > IndexBuffer.GetIndexCount())
        {
            Log.Logger.LogError(
                "Illegal draw range {IndexStart} to {LastIndex}, index buffer has {IndexCount} indices",
                indexStart,
                unchecked(indexStart + indexCount - 1),
                IndexBuffer.GetIndexCount());
            return false;
        }

        PrimitiveType = type;
        IndexStart = indexStart;
        IndexCount = indexCount;

        // Get min.vertex index and num of vertices from index buffer. If it fails, use full range as fallback
        if (indexCount != 0)
        {
            VertexStart = 0;
            VertexCount = _vertexBuffers.Count != 0 && _vertexBuffers[0] is not null
                ? _vertexBuffers[0]!.GetVertexCount()
                : 0;

            if (getUsedVertexRange && IndexBuffer is not null)
            {
                if (IndexBuffer.GetUsedVertexRange(IndexStart, IndexCount, out var vertexStart, out var vertexCount))
                {
                    VertexStart = vertexStart;
                    VertexCount = vertexCount;
                }
            }
        }
        else
        {
            VertexStart = 0;
            VertexCount = 0;
        }

        MarkPipelineStateHashDirty();
        return true;
    }

    /// <summary>Set the draw range.</summary>
    public bool SetDrawRange(PrimitiveType type, uint indexStart, uint indexCount, uint vertexStart, uint vertexCount,
        bool checkIllegal = true)
    {
        if (IndexBuffer is not null)
        {
            // We can allow setting an illegal draw range now if the caller guarantees to resize / fill the buffer later
            if (checkIllegal && unchecked(indexStart + indexCount) > IndexBuffer.GetIndexCount())
            {
                Log.Logger.LogError(
                    "Illegal draw range {IndexStart} to {LastIndex}, index buffer has {IndexCount} indices",
                    indexStart,
                    unchecked(indexStart + indexCount - 1),
                    IndexBuffer.GetIndexCount());
                return false;
            }
        }
        else if (_rawIndexData is null)
        {
            indexStart = 0;
            indexCount = 0;
        }

        PrimitiveType = type;
        IndexStart = indexStart;
        IndexCount = indexCount;
        VertexStart = vertexStart;
        VertexCount = vertexCount;

        _ = RecalculatePipelineStateHash();
        return true;
    }

    /// <summary>Set the LOD distance.</summary>
    public void SetLodDistance(float distance)
    {
        if (distance < 0.0f)
            distance = 0.0f;

        _lodDistance = distance;
    }

    /// <summary>LOD distance.</summary>
    public float LodDistance
    {
        get => _lodDistance;
        set => SetLodDistance(value);
    }

    /// <summary>Override raw vertex data to be returned for CPU-side operations.</summary>
    public void SetRawVertexData(byte[] data, IEnumerable<VertexElement> elements)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(elements);

        _rawVertexData = data;
        _rawVertexSize = VertexBuffer.GetVertexSize(elements);
        _rawElements = elements.ToList();
    }

    /// <summary>Override raw vertex data to be returned for CPU-side operations using a legacy vertex bitmask.</summary>
    public void SetRawVertexData(byte[] data, uint elementMask)
    {
        ArgumentNullException.ThrowIfNull(data);

        _rawVertexData = data;
        _rawVertexSize = VertexBuffer.GetVertexSize((VertexMask)elementMask);
        _rawElements = VertexBuffer.GetElements(elementMask);
    }

    /// <summary>Override raw index data to be returned for CPU-side operations.</summary>
    public void SetRawIndexData(byte[] data, uint indexSize)
    {
        ArgumentNullException.ThrowIfNull(data);

        _rawIndexData = data;
        _rawIndexSize = indexSize;
    }

    /// <summary>Return all vertex buffers.</summary>
    public IReadOnlyList<VertexBuffer?> VertexBuffers => _vertexBuffers;

    /// <summary>Return all vertex buffers.</summary>
    public IReadOnlyList<VertexBuffer?> GetVertexBuffers() => _vertexBuffers;

    /// <summary>Return number of vertex buffers.</summary>
    public uint GetNumVertexBuffers() => (uint)_vertexBuffers.Count;

    /// <summary>Return vertex buffer by index.</summary>
    public VertexBuffer? GetVertexBuffer(uint index)
    {
        return index < _vertexBuffers.Count ? _vertexBuffers[(int)index] : null;
    }

    /// <summary>Return the index buffer.</summary>
    public IndexBuffer? GetIndexBuffer() => IndexBuffer;

    /// <summary>Return primitive type.</summary>
    public PrimitiveType GetPrimitiveType() => PrimitiveType;

    /// <summary>Return start index.</summary>
    public uint GetIndexStart() => IndexStart;

    /// <summary>Return number of indices.</summary>
    public uint GetIndexCount() => IndexCount;

    /// <summary>Return first used vertex.</summary>
    public uint GetVertexStart() => VertexStart;

    /// <summary>Return number of used vertices.</summary>
    public uint GetVertexCount() => VertexCount;

    /// <summary>Return LOD distance.</summary>
    public float GetLodDistance() => LodDistance;

    /// <summary>Return index or vertex count depending on whether the index buffer is used.</summary>
    public uint EffectiveIndexCount => IndexBuffer is not null ? IndexCount : VertexCount;

    /// <summary>Return index or vertex count depending on whether the index buffer is used.</summary>
    public uint GetEffectiveIndexCount() => EffectiveIndexCount;

    /// <summary>Return number of primitives.</summary>
    public uint GetPrimitiveCount()
    {
        var indexCount = GetEffectiveIndexCount();
        return PrimitiveType switch
        {
            PrimitiveType.TriangleList => indexCount / 3,
            PrimitiveType.LineList => indexCount / 2,
            PrimitiveType.PointList => indexCount,
            PrimitiveType.TriangleStrip or PrimitiveType.TriangleFan => indexCount >= 2 ? indexCount - 2 : 0,
            PrimitiveType.LineStrip => indexCount >= 1 ? indexCount - 1 : 0,
            _ => 0
        };
    }

    /// <summary>Return raw vertex and index data for CPU operations, or null data if not available.</summary>
    public void GetRawData(out IReadOnlyList<byte>? vertexData, out uint vertexSize, out IReadOnlyList<byte>? indexData,
        out uint indexSize, out IReadOnlyList<VertexElement>? elements)
    {
        if (_rawVertexData is not null)
        {
            vertexData = _rawVertexData;
            vertexSize = _rawVertexSize;
            elements = _rawElements;
        }
        else if (_vertexBuffers.Count != 0 && _vertexBuffers[0] is not null)
        {
            vertexData = _vertexBuffers[0]!.GetData();
            vertexSize = _vertexBuffers[0]!.GetVertexSize();
            elements = _vertexBuffers[0]!.GetElements();
        }
        else
        {
            vertexData = null;
            vertexSize = 0;
            elements = null;
        }

        if (_rawIndexData is not null)
        {
            indexData = _rawIndexData;
            indexSize = _rawIndexSize;
        }
        else
        {
            if (IndexBuffer is { Shadowed: true } indexBuffer)
            {
                var unpackedData = indexBuffer.GetUnpackedData();
                indexSize = indexBuffer.GetIndexSize();
                var packedIndexData = new byte[checked(unpackedData.Count * (int)indexSize)];
                for (var i = 0; i < unpackedData.Count; ++i)
                {
                    var offset = checked(i * (int)indexSize);
                    if (indexSize == sizeof(ushort))
                        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(
                            packedIndexData.AsSpan(offset, sizeof(ushort)), checked((ushort)unpackedData[i]));
                    else
                        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(
                            packedIndexData.AsSpan(offset, sizeof(uint)), unpackedData[i]);
                }
                indexData = packedIndexData;
            }
            else
            {
                indexData = null;
                indexSize = 0;
            }
        }
    }

    /// <summary>Return raw vertex and index data for CPU operations using managed shared arrays.</summary>
    public void GetRawDataShared(out byte[]? vertexData, out uint vertexSize, out byte[]? indexData, out uint indexSize,
        out IReadOnlyList<VertexElement>? elements)
    {
        GetRawData(out var vertexDataList, out vertexSize, out var indexDataList, out indexSize, out elements);
        vertexData = vertexDataList as byte[];
        indexData = indexDataList as byte[];
    }

    /// <summary>Return ray hit distance or infinity if no hit.</summary>
    public float GetHitDistance(Ray ray, Vector3f? outNormal = null, Vector2f? outUv = null)
    {
        GetRawData(out var vertexDataList, out var vertexSize, out var rawIndexDataList, out var indexSize,
            out var elements);

        if (vertexDataList is not byte[] vertexData || elements is null)
            return float.PositiveInfinity;

        var positionElement = VertexBuffer.GetElement(elements, VertexElementType.Vector3,
            VertexElementSemantic.Position);
        if (positionElement is null || positionElement.Value.Offset != 0)
            return float.PositiveInfinity;

        var uvElement = VertexBuffer.GetElement(elements, VertexElementType.Vector2, VertexElementSemantic.TexCoord);
        if (outUv is not null && uvElement is null)
        {
            Log.Logger.LogWarning("Illegal GetHitDistance call: UV return requested on vertex buffer without UV coords");
            outUv = Vector2f.Zero;
        }

        var indexData = rawIndexDataList as byte[];
        var unpackedIndices = indexData is null && IndexBuffer is { Shadowed: true }
            ? IndexBuffer.GetUnpackedData(IndexStart, IndexCount).ToArray()
            : null;
        var hasIndexData = indexData is not null || unpackedIndices is not null;
        if (IndexBuffer is not null && !hasIndexData)
            return float.PositiveInfinity;

        var nearest = float.PositiveInfinity;
        var nearestBary = Vector3f.Zero;
        var nearestIndices = Vector3i.Zero;

        static float ReadFloat(byte[] data, int offset) =>
            System.Buffers.Binary.BinaryPrimitives.ReadSingleLittleEndian(data.AsSpan(offset, sizeof(float)));

        static Vector3f ReadVector3(byte[] data, int offset) =>
            new(ReadFloat(data, offset), ReadFloat(data, offset + sizeof(float)),
                ReadFloat(data, offset + 2 * sizeof(float)));

        static Vector2f ReadVector2(byte[] data, int offset) =>
            new(ReadFloat(data, offset), ReadFloat(data, offset + sizeof(float)));

        Vector3f ReadPosition(uint vertex) => ReadVector3(vertexData,
            checked((int)(vertex * vertexSize + positionElement.Value.Offset)));

        Vector2f ReadUv(uint vertex) => ReadVector2(vertexData,
            checked((int)(vertex * vertexSize + uvElement!.Value.Offset)));

        uint ReadIndex(uint index)
        {
            if (unpackedIndices is not null)
                return unpackedIndices[checked((int)index)];

            return indexSize == sizeof(ushort)
                ? System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(
                    indexData!.AsSpan(checked((int)(index * indexSize)), sizeof(ushort)))
                : System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(
                    indexData!.AsSpan(checked((int)(index * indexSize)), sizeof(uint)));
        }

        var triangleCount = hasIndexData ? IndexCount / 3 : VertexCount / 3;
        for (var triangle = 0u; triangle < triangleCount; ++triangle)
        {
            var index = triangle * 3;
            var vertex0 = hasIndexData ? ReadIndex(IndexStart + index) : VertexStart + index;
            var vertex1 = hasIndexData ? ReadIndex(IndexStart + index + 1) : VertexStart + index + 1;
            var vertex2 = hasIndexData ? ReadIndex(IndexStart + index + 2) : VertexStart + index + 2;
            var distance = ray.HitDistance(ReadPosition(vertex0), ReadPosition(vertex1), ReadPosition(vertex2),
                out _, out var barycentric);
            if (distance < nearest)
            {
                nearest = distance;
                nearestBary = barycentric;
                nearestIndices = new Vector3i(checked((int)vertex0), checked((int)vertex1), checked((int)vertex2));
            }
        }

        if (outUv is not null)
        {
            if (nearest == float.PositiveInfinity)
                outUv = Vector2f.Zero;
            else
            {
                var uv0 = ReadUv(checked((uint)nearestIndices.X));
                var uv1 = ReadUv(checked((uint)nearestIndices.Y));
                var uv2 = ReadUv(checked((uint)nearestIndices.Z));
                outUv = uv0 * nearestBary.X + uv1 * nearestBary.Y + uv2 * nearestBary.Z;
            }
        }

        return nearest;
    }

    /// <summary>Return whether or not the ray is inside geometry.</summary>
    public bool IsInside(Ray ray)
    {
        GetRawData(out var vertexDataList, out var vertexSize, out var rawIndexDataList, out var indexSize,
            out var elements);

        if (vertexDataList is not byte[] vertexData || elements is null)
            return false;

        var positionElement = VertexBuffer.GetElement(elements, VertexElementType.Vector3,
            VertexElementSemantic.Position);
        if (positionElement is null || positionElement.Value.Offset != 0)
            return false;

        var indexData = rawIndexDataList as byte[];
        var unpackedIndices = indexData is null && IndexBuffer is { Shadowed: true }
            ? IndexBuffer.GetUnpackedData(IndexStart, IndexCount).ToArray()
            : null;
        var hasIndexData = indexData is not null || unpackedIndices is not null;
        if (IndexBuffer is not null && !hasIndexData)
            return false;

        static float ReadFloat(byte[] data, int offset) =>
            System.Buffers.Binary.BinaryPrimitives.ReadSingleLittleEndian(data.AsSpan(offset, sizeof(float)));

        static Vector3f ReadVector3(byte[] data, int offset) =>
            new(ReadFloat(data, offset), ReadFloat(data, offset + sizeof(float)),
                ReadFloat(data, offset + 2 * sizeof(float)));

        Vector3f ReadPosition(uint vertex) => ReadVector3(vertexData,
            checked((int)(vertex * vertexSize + positionElement.Value.Offset)));

        uint ReadIndex(uint index)
        {
            if (unpackedIndices is not null)
                return unpackedIndices[checked((int)index)];

            return indexSize == sizeof(ushort)
                ? System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(
                    indexData!.AsSpan(checked((int)(index * indexSize)), sizeof(ushort)))
                : System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(
                    indexData!.AsSpan(checked((int)(index * indexSize)), sizeof(uint)));
        }

        var currentFrontFace = float.PositiveInfinity;
        var currentBackFace = float.PositiveInfinity;
        var triangleCount = hasIndexData ? IndexCount / 3 : VertexCount / 3;
        for (var triangle = 0u; triangle < triangleCount; ++triangle)
        {
            var index = triangle * 3;
            var vertex0 = hasIndexData ? ReadIndex(IndexStart + index) : VertexStart + index;
            var vertex1 = hasIndexData ? ReadIndex(IndexStart + index + 1) : VertexStart + index + 1;
            var vertex2 = hasIndexData ? ReadIndex(IndexStart + index + 2) : VertexStart + index + 2;
            var position0 = ReadPosition(vertex0);
            var position1 = ReadPosition(vertex1);
            var position2 = ReadPosition(vertex2);
            var frontFaceDistance = ray.HitDistance(position0, position1, position2, out _, out _);
            var backFaceDistance = ray.HitDistance(position2, position1, position0, out _, out _);
            currentFrontFace = MathF.Min(frontFaceDistance > 0.0f ? frontFaceDistance : float.PositiveInfinity,
                currentFrontFace);
            currentBackFace = MathF.Min(backFaceDistance > 0.0f ? backFaceDistance : float.PositiveInfinity,
                currentBackFace);
        }

        if (currentFrontFace != float.PositiveInfinity || currentBackFace != float.PositiveInfinity)
            return currentBackFace < currentFrontFace;

        return false;
    }

    /// <summary>Return whether has empty draw range.</summary>
    public bool IsEmpty => IndexCount == 0 && VertexCount == 0;

    /// <summary>Return whether the geometry can be rendered using instancing buffer.</summary>
    public bool IsInstanced(GeometryType geometryType)
    {
        return (geometryType == GeometryType.Static || geometryType == GeometryType.Instanced)
            && IndexBuffer is not null;
    }

    /// <summary>Return whether the geometry uses headless buffers.</summary>
    public bool IsHeadless() => _isHeadless;

    /// <summary>Recalculate hash.</summary>
    protected override uint RecalculatePipelineStateHash()
    {
        var hash = 0u;
        CombineHash(ref hash, (uint)_vertexBuffers.Count);
        foreach (var vertexBuffer in _vertexBuffers)
            CombineHash(ref hash, vertexBuffer is not null ? vertexBuffer.GetPipelineStateHash() : 0);
        CombineHash(ref hash, IndexBuffer is not null ? IndexBuffer.GetPipelineStateHash() : 0);
        CombineHash(ref hash, (uint)PrimitiveType);
        return hash;
    }

    /// <summary>Update "headless" status.</summary>
    private void UpdateHeadlessStatus()
    {
        // TODO: port: Managed VertexBuffer and IndexBuffer do not expose RenderDevice; all existing buffers are CPU-only.
        _isHeadless = IndexBuffer is not null;
        foreach (var vertexBuffer in _vertexBuffers)
            _isHeadless = _isHeadless || vertexBuffer is not null;
    }

    private static void CombineHash(ref uint result, uint hash)
    {
        result ^= hash + 0x9e3779b9u + (result << 6) + (result >> 2);
    }
}
