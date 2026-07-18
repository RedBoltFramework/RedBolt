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

/// <summary>Geometry type for vertex shader geometry variations.</summary>
public enum GeometryType
{
    Static = 0,
    Skinned = 1,
    Instanced = 2,
    Billboard = 3,
    DirectionalBillboard = 4,
    TrailFaceCamera = 5,
    TrailBone = 6,
    MaxGeometryTypes = 7,
    // This is not a real geometry type for VS, but used to mark objects that do not desire to be instanced
    StaticNoInstancing = 7
}

/// <summary>Defines one or more vertex buffers, an index buffer and a draw range.</summary>
/// <remarks>
/// </remarks>
public sealed class Geometry : PipelineStateTracker
{
    private readonly IDFamily<Geometry> _idFamily = new();

    /// <summary>Vertex buffers.</summary>
    private readonly List<VertexBuffer?> _vertexBuffers = [];

    /// <summary>Vertex buffers dependencies.</summary>
    private readonly List<PipelineStateSubscription> _vertexBuffersDependencies = [];

    /// <summary>Index buffer.</summary>
    public IndexBuffer? IndexBuffer { get; private set; }

    /// <summary>Index buffer dependency.</summary>
    private readonly PipelineStateSubscription _indexBufferDependency = new();

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

    /// <summary>Destruct.</summary>
    public override void Dispose()
    {
        _rawIndexData = null;
        _rawVertexData = null;
        _rawElements.Clear();
        _indexBufferDependency.Dispose();
        IndexBuffer = null;
        for (var i = _vertexBuffersDependencies.Count - 1; i >= 0; --i)
            _vertexBuffersDependencies[i].Dispose();
        _vertexBuffersDependencies.Clear();
        _vertexBuffers.Clear();
        _idFamily.Dispose();
        base.Dispose();
    }

    /// <summary>Return unique object ID or 0 if not assigned.</summary>
    public uint GetObjectID() => _idFamily.GetObjectID();

    /// <summary>Return upper bound of all used object IDs within family.</summary>
    public static uint GetNextFreeObjectID() => IDFamily<Geometry>.GetNextFreeObjectID();

    /// <summary>Acquire unique object ID. Ignored if unique ID is already acquired.</summary>
    public void AcquireObjectID() => _idFamily.AcquireObjectID();

    /// <summary>Release unique object ID. Ignored if unique ID is already released.</summary>
    public void ReleaseObjectID() => _idFamily.ReleaseObjectID();

    /// <summary>Set number of vertex buffers.</summary>
    public bool SetNumVertexBuffers(uint count)
    {
        if (count >= RenderApiLimits.MaxVertexStreams)
        {
            Log.Logger.LogError("Too many vertex streams");
            return false;
        }

        var num = (int)count;
        while (_vertexBuffersDependencies.Count > num)
        {
            var index = _vertexBuffersDependencies.Count - 1;
            _vertexBuffersDependencies[index].Dispose();
            _vertexBuffersDependencies.RemoveAt(index);
        }
        while (_vertexBuffersDependencies.Count < num)
            _vertexBuffersDependencies.Add(new PipelineStateSubscription());

        while (_vertexBuffers.Count > num)
            _vertexBuffers.RemoveAt(_vertexBuffers.Count - 1);
        while (_vertexBuffers.Count < num)
            _vertexBuffers.Add(null);

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

        using (var dependency = CreateDependency(buffer))
            _vertexBuffersDependencies[(int)index].MoveAssign(dependency);
        _vertexBuffers[(int)index] = buffer;

        UpdateHeadlessStatus();
        return true;
    }

    /// <summary>Set all vertex buffers at once.</summary>
    public void SetVertexBuffers(IEnumerable<VertexBuffer?> buffers)
    {
        ArgumentNullException.ThrowIfNull(buffers);

        var newBuffers = buffers.ToList();
        while (_vertexBuffersDependencies.Count > newBuffers.Count)
        {
            var index = _vertexBuffersDependencies.Count - 1;
            _vertexBuffersDependencies[index].Dispose();
            _vertexBuffersDependencies.RemoveAt(index);
        }
        while (_vertexBuffersDependencies.Count < newBuffers.Count)
            _vertexBuffersDependencies.Add(new PipelineStateSubscription());
        for (var i = 0; i < newBuffers.Count; ++i)
        {
            using (var dependency = CreateDependency(newBuffers[i]))
                _vertexBuffersDependencies[i].MoveAssign(dependency);
        }

        _vertexBuffers.Clear();
        _vertexBuffers.AddRange(newBuffers);

        UpdateHeadlessStatus();
    }

    /// <summary>Set the index buffer.</summary>
    public void SetIndexBuffer(IndexBuffer? buffer)
    {
        using (var dependency = CreateDependency(IndexBuffer))
            _indexBufferDependency.MoveAssign(dependency);
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
            VertexCount = _vertexBuffers[0] is not null ? _vertexBuffers[0]!.GetVertexCount() : 0;

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
            var rawBuffer = _vertexBuffers[0]!.GetRawBuffer();
            vertexData = rawBuffer?.IsShadowed() == true ? rawBuffer.GetShadowData() : null;
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
            var rawBuffer = IndexBuffer?.GetRawBuffer();
            if (rawBuffer?.IsShadowed() == true)
            {
                indexData = rawBuffer.GetShadowData();
                indexSize = IndexBuffer!.GetIndexSize();
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
    public float GetHitDistance(Ray ray)
    {
        return GetHitDistance(ray, false, false, out _, out _);
    }

    /// <summary>Return ray hit distance or infinity if no hit.</summary>
    public float GetHitDistance(Ray ray, out Vector3f outNormal)
    {
        return GetHitDistance(ray, true, false, out outNormal, out _);
    }

    /// <summary>Return ray hit distance or infinity if no hit.</summary>
    public float GetHitDistance(Ray ray, out Vector3f outNormal, out Vector2f outUv)
    {
        return GetHitDistance(ray, true, true, out outNormal, out outUv);
    }

    private float GetHitDistance(Ray ray, bool requestNormal, bool requestUv,
        out Vector3f outNormal, out Vector2f outUv)
    {
        outNormal = Vector3f.Zero;
        outUv = Vector2f.Zero;
        GetRawData(out var vertexDataList, out var vertexSize, out var rawIndexDataList, out var indexSize,
            out var elements);

        if (vertexDataList is not byte[] vertexData || elements is null)
            return float.PositiveInfinity;

        var positionElement = VertexBuffer.GetElement(elements, VertexElementType.Vector3,
            VertexElementSemantic.Position);
        if (positionElement is null || positionElement.Value.Offset != 0)
            return float.PositiveInfinity;

        var uvElement = VertexBuffer.GetElement(elements, VertexElementType.Vector2, VertexElementSemantic.TexCoord);
        if (requestUv && uvElement is null)
        {
            Log.Logger.LogWarning("Illegal GetHitDistance call: UV return requested on vertex buffer without UV coords");
            requestUv = false;
        }

        var indexData = rawIndexDataList as byte[];
        var hasIndexData = indexData is not null;

        var nearest = float.PositiveInfinity;
        var nearestNormal = Vector3f.Zero;
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
                out var normal, out var barycentric);
            if (distance < nearest)
            {
                nearest = distance;
                nearestNormal = normal;
                nearestBary = barycentric;
                nearestIndices = new Vector3i(checked((int)vertex0), checked((int)vertex1), checked((int)vertex2));
            }
        }

        if (requestNormal)
            outNormal = nearest == float.PositiveInfinity ? Vector3f.Zero : nearestNormal;

        if (requestUv)
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
        var hasIndexData = indexData is not null;

        static float ReadFloat(byte[] data, int offset) =>
            System.Buffers.Binary.BinaryPrimitives.ReadSingleLittleEndian(data.AsSpan(offset, sizeof(float)));

        static Vector3f ReadVector3(byte[] data, int offset) =>
            new(ReadFloat(data, offset), ReadFloat(data, offset + sizeof(float)),
                ReadFloat(data, offset + 2 * sizeof(float)));

        Vector3f ReadPosition(uint vertex) => ReadVector3(vertexData,
            checked((int)(vertex * vertexSize + positionElement.Value.Offset)));

        uint ReadIndex(uint index)
        {
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
            // A backwards face is just a regular one, with the vertices in the opposite order. This essentially checks backfaces by
            // checking reversed frontfaces
            currentBackFace = MathF.Min(backFaceDistance > 0.0f ? backFaceDistance : float.PositiveInfinity,
                currentBackFace);
        }

        // If the closest face is a backface, that means that the ray originates from the inside of the geometry
        // NOTE: there may be cases where both are equal, as in, no collision to either. This is prevented in the most likely case
        // (ray doesn't hit either) by this conditional
        if (currentFrontFace != float.PositiveInfinity || currentBackFace != float.PositiveInfinity)
            return currentBackFace < currentFrontFace;

        // It is still possible for two triangles to be equally distant from the triangle, however, this is extremely unlikely.
        // As such, it is safe to assume they are not
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
        _isHeadless = IndexBuffer?.GetRawBuffer()?.GetRenderDevice() is null && IndexBuffer is not null;
        foreach (var vertexBuffer in _vertexBuffers)
            _isHeadless = _isHeadless || (vertexBuffer is not null && vertexBuffer.GetRawBuffer()?.GetRenderDevice() is null);
    }

    private static void CombineHash(ref uint result, uint hash)
    {
        result ^= hash + 0x9e3779b9u + (result << 6) + (result >> 2);
    }
}
