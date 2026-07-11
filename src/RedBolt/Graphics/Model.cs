using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RedBolt;

public sealed class Model : ResourceWithMetadata
{
    private readonly List<List<Geometry>> _geometries = [];
    private readonly List<Vector3f> _geometryCenters = [];
    private BoundingBox _boundingBox = new(-0.5f, 0.5f);

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        IncludeFields = true,
        IgnoreReadOnlyProperties = true
    };

    private const uint LegacyVersion = 1;
    private const uint MorphWeightVersion = 2;

    static Model()
    {
        SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    }

    public Model(string name)
        : base(name)
    {
    }

    internal Model(string name, uint vertexCount)
        : this(name)
    {
        SetNumGeometries(1);
        SetNumGeometryLodLevels(0, 1);
        var geometry = new Geometry();
        geometry.SetDrawRange(PrimitiveType.TriangleList, 0, vertexCount, 0, vertexCount);
        SetGeometry(0, 0, geometry);
    }

    public IReadOnlyList<IReadOnlyList<Geometry>> Geometries => _geometries;

    public IReadOnlyList<Vector3f> GeometryCenters => _geometryCenters;

    public BoundingBox BoundingBox => _boundingBox;

    public uint NumGeometries => (uint)_geometries.Count;

    public uint VertexCount => _geometries.Count == 0 || _geometries[0].Count == 0 ? 0u : _geometries[0][0].VertexCount;

    public override bool BeginLoad(Stream source)
    {
        ArgumentNullException.ThrowIfNull(source);

        using var memory = new MemoryStream();
        source.CopyTo(memory);
        var bytes = memory.ToArray();
        if (bytes.Length >= 4)
        {
            var fileId = Encoding.ASCII.GetString(bytes, 0, 4);
            if (fileId is "UMDL" or "UMD2" or "UMD3")
                return LoadUrhoModel(new MemoryBuffer(bytes));
        }

        return LoadSerializedModel(bytes);
    }

    private bool LoadSerializedModel(byte[] bytes)
    {
        var modelData = JsonSerializer.Deserialize<SerializedModel>(bytes, SerializerOptions);
        if (modelData?.Geometries is null || modelData.Geometries.Count == 0)
            return false;

        _geometries.Clear();
        _geometryCenters.Clear();

        SetNumGeometries((uint)modelData.Geometries.Count);
        _boundingBox = modelData.BoundingBox ?? new BoundingBox();

        for (var i = 0; i < modelData.Geometries.Count; ++i)
        {
            var geometryData = modelData.Geometries[i];
            if (geometryData.Positions is null || geometryData.Positions.Count == 0 || geometryData.Indices is null || geometryData.Indices.Count == 0)
                return false;

            var vertexBuffer = new VertexBuffer();
            vertexBuffer.SetSize((uint)geometryData.Positions.Count, 12u);
            vertexBuffer.SetShadowed(true);
            vertexBuffer.SetData(ToVertexBytes(geometryData.Positions));

            var indexBuffer = new IndexBuffer();
            indexBuffer.SetSize((uint)geometryData.Indices.Count, geometryData.Positions.Count > ushort.MaxValue, dynamic: false);
            indexBuffer.SetShadowed(true);
            indexBuffer.SetUnpackedData(geometryData.Indices);

            var geometry = new Geometry();
            geometry.SetVertexBuffers([vertexBuffer]);
            geometry.SetIndexBuffer(indexBuffer);
            geometry.SetDrawRange(geometryData.PrimitiveType, 0u, (uint)geometryData.Indices.Count, 0u, (uint)geometryData.Positions.Count);
            SetNumGeometryLodLevels((uint)i, 1u);
            SetGeometry((uint)i, 0u, geometry);
            SetGeometryCenter((uint)i, geometryData.Center ?? CalculateCenter(geometryData.Positions));

            if (!_boundingBox.Defined)
                _boundingBox = new BoundingBox(geometryData.Positions);
        }

        if (!_boundingBox.Defined)
            _boundingBox = new BoundingBox(modelData.Geometries.SelectMany(static item => item.Positions ?? (IEnumerable<Vector3f>)Array.Empty<Vector3f>()).ToArray());

        MemoryUse = (uint)modelData.Geometries.Sum(static item => ((item.Positions?.Count ?? 0) * 12) + ((item.Indices?.Count ?? 0) * 4));
        return true;
    }

    private bool LoadUrhoModel(Deserializer source)
    {
        _geometries.Clear();
        _geometryCenters.Clear();
        _boundingBox = new BoundingBox();

        var fileId = source.ReadFileID();
        if (fileId is not ("UMDL" or "UMD2" or "UMD3"))
            return false;

        var version = fileId == "UMD3" ? source.ReadUInt() : LegacyVersion;
        var hasVertexDeclarations = fileId != "UMDL";

        var vertexBuffers = new List<VertexBuffer>();
        var numVertexBuffers = source.ReadUInt();
        for (var i = 0u; i < numVertexBuffers; ++i)
        {
            var vertexCount = source.ReadUInt();
            var elements = hasVertexDeclarations
                ? ReadVertexElements(source)
                : VertexBuffer.GetElements(source.ReadUInt());

            _ = source.ReadUInt();
            _ = source.ReadUInt();

            var vertexBuffer = new VertexBuffer();
            vertexBuffer.SetShadowed(true);
            vertexBuffer.SetSize(vertexCount, elements);
            var dataSize = checked((int)(vertexCount * vertexBuffer.GetVertexSize()));
            var data = new byte[dataSize];
            _ = source.Read(data);
            vertexBuffer.SetData(data);
            vertexBuffers.Add(vertexBuffer);
        }

        var indexBuffers = new List<IndexBuffer>();
        var numIndexBuffers = source.ReadUInt();
        for (var i = 0u; i < numIndexBuffers; ++i)
        {
            var indexCount = source.ReadUInt();
            var indexSize = source.ReadUInt();
            if (indexSize is not (2u or 4u))
                return false;

            var indexBuffer = new IndexBuffer();
            indexBuffer.SetShadowed(true);
            indexBuffer.SetSize(indexCount, indexSize > sizeof(ushort), dynamic: false);

            var indices = new uint[indexCount];
            for (var index = 0u; index < indexCount; ++index)
                indices[index] = indexSize == 2u ? source.ReadUShort() : source.ReadUInt();

            indexBuffer.SetUnpackedData(indices);
            indexBuffers.Add(indexBuffer);
        }

        var numGeometries = source.ReadUInt();
        SetNumGeometries(numGeometries);
        for (var geometryIndex = 0u; geometryIndex < numGeometries; ++geometryIndex)
        {
            var boneMappingCount = source.ReadUInt();
            for (var mappingIndex = 0u; mappingIndex < boneMappingCount; ++mappingIndex)
                _ = source.ReadUInt();

            var numLodLevels = source.ReadUInt();
            if (!SetNumGeometryLodLevels(geometryIndex, numLodLevels))
                return false;

            for (var lodLevel = 0u; lodLevel < numLodLevels; ++lodLevel)
            {
                var lodDistance = source.ReadFloat();
                var primitiveType = (PrimitiveType)source.ReadUInt();
                var vertexBufferRef = source.ReadUInt();
                var indexBufferRef = source.ReadUInt();
                var indexStart = source.ReadUInt();
                var indexCount = source.ReadUInt();

                if (vertexBufferRef >= vertexBuffers.Count || indexBufferRef >= indexBuffers.Count)
                    return false;

                var vertexBuffer = vertexBuffers[(int)vertexBufferRef];
                var indexBuffer = indexBuffers[(int)indexBufferRef];
                uint vertexStart = 0;
                var vertexCount = vertexBuffer.GetVertexCount();
                if (indexBuffer.GetUsedVertexRange(indexStart, indexCount, out var minVertex, out var usedVertexCount))
                {
                    vertexStart = minVertex;
                    vertexCount = usedVertexCount;
                }

                var geometry = new Geometry();
                geometry.SetVertexBuffers([vertexBuffer]);
                geometry.SetIndexBuffer(indexBuffer);
                geometry.SetLodDistance(lodDistance);
                geometry.SetDrawRange(primitiveType, indexStart, indexCount, vertexStart, vertexCount);
                if (!SetGeometry(geometryIndex, lodLevel, geometry))
                    return false;
            }
        }

        var numMorphs = source.ReadUInt();
        for (var morphIndex = 0u; morphIndex < numMorphs; ++morphIndex)
        {
            _ = source.ReadString();
            if (version >= MorphWeightVersion)
                _ = source.ReadFloat();

            var numBuffers = source.ReadUInt();
            for (var bufferIndex = 0u; bufferIndex < numBuffers; ++bufferIndex)
            {
                _ = source.ReadUInt();
                var elementMask = (VertexMask)source.ReadUInt();
                var vertexCount = source.ReadUInt();
                var vertexSize = sizeof(uint);
                if ((elementMask & VertexMask.Position) == VertexMask.Position)
                    vertexSize += 3 * sizeof(float);
                if ((elementMask & VertexMask.Normal) == VertexMask.Normal)
                    vertexSize += 3 * sizeof(float);
                if ((elementMask & VertexMask.Tangent) == VertexMask.Tangent)
                    vertexSize += 3 * sizeof(float);

                var bytesToSkip = checked((int)vertexCount * vertexSize);
                var scratch = new byte[bytesToSkip];
                _ = source.Read(scratch);
            }
        }

        if (!SkipSkeleton(source))
            return false;

        _boundingBox = source.ReadBoundingBox();
        for (var geometryIndex = 0u; geometryIndex < numGeometries && !source.IsEof(); ++geometryIndex)
            SetGeometryCenter(geometryIndex, source.ReadVector3());
        while (_geometryCenters.Count < _geometries.Count)
            _geometryCenters.Add(Vector3f.Zero);

        long totalBytes = 0;
        foreach (var buffer in vertexBuffers)
            totalBytes += buffer.GetData().Count;
        foreach (var buffer in indexBuffers)
            totalBytes += buffer.GetIndexCount() * buffer.GetIndexSize();
        MemoryUse = (uint)totalBytes;
        return true;
    }

    private static List<VertexElement> ReadVertexElements(Deserializer source)
    {
        var numElements = source.ReadUInt();
        var result = new List<VertexElement>((int)numElements);
        for (var i = 0u; i < numElements; ++i)
        {
            var elementDesc = source.ReadUInt();
            result.Add(new VertexElement(
                (VertexElementType)(elementDesc & 0xffu),
                (VertexElementSemantic)((elementDesc >> 8) & 0xffu),
                (byte)((elementDesc >> 16) & 0xffu)));
        }

        return result;
    }

    private static bool SkipSkeleton(Deserializer source)
    {
        if (source.IsEof())
            return false;

        var numBones = source.ReadUInt();
        for (var boneIndex = 0u; boneIndex < numBones; ++boneIndex)
        {
            _ = source.ReadString();
            _ = source.ReadUInt();
            _ = source.ReadVector3();
            _ = source.ReadQuaternion();
            _ = source.ReadVector3();

            var offsetMatrix = new byte[sizeof(float) * 12];
            _ = source.Read(offsetMatrix);

            var collisionMask = source.ReadUByte();
            if ((collisionMask & 0x1) != 0)
                _ = source.ReadFloat();
            if ((collisionMask & 0x2) != 0)
                _ = source.ReadBoundingBox();
        }

        return true;
    }

    public void SetBoundingBox(BoundingBox box)
    {
        _boundingBox = box;
    }

    public BoundingBox GetBoundingBox() => _boundingBox;

    public IReadOnlyList<IReadOnlyList<Geometry>> GetGeometries() => Geometries;

    public IReadOnlyList<Vector3f> GetGeometryCenters() => GeometryCenters;

    public uint GetNumGeometries() => NumGeometries;

    public void SetNumGeometries(uint num)
    {
        while (_geometries.Count < num)
        {
            _geometries.Add([]);
            _geometryCenters.Add(Vector3f.Zero);
        }

        while (_geometries.Count > num)
        {
            _geometries.RemoveAt(_geometries.Count - 1);
            _geometryCenters.RemoveAt(_geometryCenters.Count - 1);
        }
    }

    public bool SetNumGeometryLodLevels(uint index, uint num)
    {
        if (index >= _geometries.Count)
            return false;

        var lodLevels = _geometries[(int)index];
        while (lodLevels.Count < num)
            lodLevels.Add(new Geometry());

        while (lodLevels.Count > num)
            lodLevels.RemoveAt(lodLevels.Count - 1);

        return true;
    }

    public bool SetGeometry(uint index, uint lodLevel, Geometry geometry)
    {
        ArgumentNullException.ThrowIfNull(geometry);

        if (index >= _geometries.Count)
            return false;

        var lodLevels = _geometries[(int)index];
        if (lodLevel >= lodLevels.Count)
            return false;

        lodLevels[(int)lodLevel] = geometry;
        return true;
    }

    public Geometry? GetGeometry(uint index, uint lodLevel = 0)
    {
        if (index >= _geometries.Count)
            return null;

        var lodLevels = _geometries[(int)index];
        if (lodLevels.Count == 0)
            return null;

        return lodLevels[(int)Math.Min(lodLevel, (uint)lodLevels.Count - 1)];
    }

    public uint GetNumGeometryLodLevels(uint index)
    {
        return index < _geometries.Count ? (uint)_geometries[(int)index].Count : 0u;
    }

    public bool SetGeometryCenter(uint index, Vector3f center)
    {
        if (index >= _geometryCenters.Count)
            return false;

        _geometryCenters[(int)index] = center;
        return true;
    }

    public Vector3f GetGeometryCenter(uint index)
    {
        return index < _geometryCenters.Count ? _geometryCenters[(int)index] : Vector3f.Zero;
    }

    private static Vector3f CalculateCenter(IReadOnlyList<Vector3f> positions)
    {
        if (positions.Count == 0)
            return Vector3f.Zero;

        var sum = Vector3f.Zero;
        foreach (var position in positions)
            sum += position;
        return sum / positions.Count;
    }

    private static byte[] ToVertexBytes(IReadOnlyList<Vector3f> positions)
    {
        var data = new byte[positions.Count * 12];
        var offset = 0;
        foreach (var position in positions)
        {
            BitConverter.GetBytes(position.X).CopyTo(data, offset + 0);
            BitConverter.GetBytes(position.Y).CopyTo(data, offset + 4);
            BitConverter.GetBytes(position.Z).CopyTo(data, offset + 8);
            offset += 12;
        }

        return data;
    }

    private sealed class SerializedModel
    {
        public BoundingBox? BoundingBox { get; set; }
        public List<SerializedGeometry>? Geometries { get; set; }
    }

    private sealed class SerializedGeometry
    {
        public PrimitiveType PrimitiveType { get; set; } = PrimitiveType.TriangleList;
        public List<Vector3f>? Positions { get; set; }
        public List<uint>? Indices { get; set; }
        public Vector3f? Center { get; set; }
    }
}
