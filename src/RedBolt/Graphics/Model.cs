using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RedBolt;

/// <summary>Vertex buffer morph data.</summary>
public sealed class VertexBufferMorph
{
    /// <summary>Vertex elements present in the packed data.</summary>
    public VertexMask ElementMask;

    /// <summary>Number of morphed vertices.</summary>
    public uint VertexCount;

    /// <summary>Morphed vertices data size in bytes.</summary>
    public uint DataSize;

    /// <summary>Packed index and vertex-element data.</summary>
    public byte[] MorphData = [];
}

/// <summary>Definition of a model vertex morph.</summary>
public sealed class ModelMorph
{
    /// <summary>Morph name.</summary>
    public string Name = string.Empty;

    /// <summary>Morph name hash.</summary>
    public StringHash NameHash;

    /// <summary>Current morph weight.</summary>
    public float Weight;

    /// <summary>Morph data per vertex buffer.</summary>
    public Dictionary<uint, VertexBufferMorph> Buffers = [];
}

public sealed class Model : ResourceWithMetadata, ICloneable
{
    private readonly List<VertexBuffer> _vertexBuffers = [];
    private readonly List<IndexBuffer> _indexBuffers = [];
    private readonly List<List<Geometry>> _geometries = [];
    private readonly List<Vector3f> _geometryCenters = [];
    private readonly List<List<uint>> _geometryBoneMappings = [];
    private readonly List<ModelMorph> _morphs = [];
    private readonly List<uint> _morphRangeStarts = [];
    private readonly List<uint> _morphRangeCounts = [];
    private readonly Skeleton _skeleton = new();
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
            _vertexBuffers.Add(vertexBuffer);

            var indexBuffer = new IndexBuffer();
            indexBuffer.SetSize((uint)geometryData.Indices.Count, geometryData.Positions.Count > ushort.MaxValue, dynamic: false);
            indexBuffer.SetShadowed(true);
            indexBuffer.SetUnpackedData(geometryData.Indices);
            _indexBuffers.Add(indexBuffer);

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
        _geometryBoneMappings.Clear();
        _vertexBuffers.Clear();
        _indexBuffers.Clear();
        _morphs.Clear();
        _morphRangeStarts.Clear();
        _morphRangeCounts.Clear();
        _skeleton.ClearBones();
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

            _morphRangeStarts.Add(source.ReadUInt());
            _morphRangeCounts.Add(source.ReadUInt());

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
            var boneMapping = new List<uint>(checked((int)boneMappingCount));
            for (var mappingIndex = 0u; mappingIndex < boneMappingCount; ++mappingIndex)
                boneMapping.Add(source.ReadUInt());
            _geometryBoneMappings.Add(boneMapping);

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
            var morph = new ModelMorph
            {
                Name = source.ReadString()
            };
            morph.NameHash = new StringHash(morph.Name);
            if (version >= MorphWeightVersion)
                morph.Weight = source.ReadFloat();

            var numBuffers = source.ReadUInt();
            for (var bufferIndex = 0u; bufferIndex < numBuffers; ++bufferIndex)
            {
                var vertexBufferIndex = source.ReadUInt();
                var morphBuffer = new VertexBufferMorph
                {
                    ElementMask = (VertexMask)source.ReadUInt(),
                    VertexCount = source.ReadUInt()
                };
                var vertexSize = sizeof(uint);
                if ((morphBuffer.ElementMask & VertexMask.Position) == VertexMask.Position)
                    vertexSize += 3 * sizeof(float);
                if ((morphBuffer.ElementMask & VertexMask.Normal) == VertexMask.Normal)
                    vertexSize += 3 * sizeof(float);
                if ((morphBuffer.ElementMask & VertexMask.Tangent) == VertexMask.Tangent)
                    vertexSize += 3 * sizeof(float);

                morphBuffer.DataSize = checked(morphBuffer.VertexCount * (uint)vertexSize);
                morphBuffer.MorphData = new byte[checked((int)morphBuffer.DataSize)];
                _ = source.Read(morphBuffer.MorphData);
                morph.Buffers[vertexBufferIndex] = morphBuffer;
            }

            _morphs.Add(morph);
        }

        if (!_skeleton.Load(source))
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
        _vertexBuffers.AddRange(vertexBuffers);
        _indexBuffers.AddRange(indexBuffers);
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

    public void SetBoundingBox(BoundingBox box)
    {
        _boundingBox = box;
    }

    public BoundingBox GetBoundingBox() => _boundingBox;

    public bool SetVertexBuffers(IReadOnlyList<VertexBuffer?> buffers, IReadOnlyList<uint>? morphRangeStarts = null,
        IReadOnlyList<uint>? morphRangeCounts = null)
    {
        for (var i = 0; i < buffers.Count; ++i)
        {
            var buffer = buffers[i];
            if (buffer is null)
            {
                Log.Logger.LogError("Null model vertex buffers specified");
                return false;
            }
            if (!buffer.Shadowed)
            {
                Log.Logger.LogError("Model vertex buffers must be shadowed");
                return false;
            }
        }

        _vertexBuffers.Clear();
        foreach (var buffer in buffers)
            _vertexBuffers.Add(buffer!);

        _morphRangeStarts.Clear();
        _morphRangeCounts.Clear();
        for (var i = 0; i < buffers.Count; ++i)
        {
            _morphRangeStarts.Add(morphRangeStarts is not null && i < morphRangeStarts.Count ? morphRangeStarts[i] : 0u);
            _morphRangeCounts.Add(morphRangeCounts is not null && i < morphRangeCounts.Count ? morphRangeCounts[i] : 0u);
        }

        return true;
    }

    public bool SetIndexBuffers(IReadOnlyList<IndexBuffer?> buffers)
    {
        for (var i = 0; i < buffers.Count; ++i)
        {
            var buffer = buffers[i];
            if (buffer is null)
            {
                Log.Logger.LogError("Null model index buffers specified");
                return false;
            }
            if (!buffer.Shadowed)
            {
                Log.Logger.LogError("Model index buffers must be shadowed");
                return false;
            }
        }

        _indexBuffers.Clear();
        foreach (var buffer in buffers)
            _indexBuffers.Add(buffer!);
        return true;
    }

    public IReadOnlyList<VertexBuffer> GetVertexBuffers() => _vertexBuffers;

    public IReadOnlyList<IndexBuffer> GetIndexBuffers() => _indexBuffers;

    public void SetSkeleton(Skeleton skeleton)
    {
        _skeleton.Define(skeleton);
    }

    public Skeleton GetSkeleton() => _skeleton;

    public void SetGeometryBoneMappings(IReadOnlyList<IReadOnlyList<uint>> geometryBoneMappings)
    {
        _geometryBoneMappings.Clear();
        foreach (var geometryBoneMapping in geometryBoneMappings)
            _geometryBoneMappings.Add([.. geometryBoneMapping]);
    }

    public IReadOnlyList<IReadOnlyList<uint>> GetGeometryBoneMappings() => _geometryBoneMappings;

    public void SetMorphs(IReadOnlyList<ModelMorph> morphs)
    {
        _morphs.Clear();
        _morphs.AddRange(morphs);
    }

    public Model Clone(string cloneName = "")
    {
        var clone = new Model(cloneName)
        {
            _boundingBox = _boundingBox
        };
        clone._skeleton.Define(_skeleton);
        foreach (var mapping in _geometryBoneMappings)
            clone._geometryBoneMappings.Add([.. mapping]);
        clone._geometryCenters.AddRange(_geometryCenters);
        clone._morphRangeStarts.AddRange(_morphRangeStarts);
        clone._morphRangeCounts.AddRange(_morphRangeCounts);

        foreach (var morph in _morphs)
        {
            var cloneMorph = new ModelMorph
            {
                Name = morph.Name,
                NameHash = morph.NameHash,
                Weight = morph.Weight
            };
            foreach (var (bufferIndex, bufferMorph) in morph.Buffers)
            {
                cloneMorph.Buffers[bufferIndex] = new VertexBufferMorph
                {
                    ElementMask = bufferMorph.ElementMask,
                    VertexCount = bufferMorph.VertexCount,
                    DataSize = bufferMorph.DataSize,
                    MorphData = [.. bufferMorph.MorphData]
                };
            }
            clone._morphs.Add(cloneMorph);
        }

        var vertexBufferMapping = new Dictionary<VertexBuffer, VertexBuffer>();
        foreach (var originalBuffer in _vertexBuffers)
        {
            var cloneBuffer = new VertexBuffer();
            cloneBuffer.SetDebugName(originalBuffer.GetDebugName());
            cloneBuffer.SetShadowed(originalBuffer.Shadowed);
            cloneBuffer.SetSize(originalBuffer.VertexCount, originalBuffer.Elements, originalBuffer.IsDynamic);
            if (originalBuffer.Shadowed)
                cloneBuffer.SetData(originalBuffer.GetRawBuffer()!.GetShadowData());
            else
                Log.Logger.LogError("Failed to read original vertex buffer");
            vertexBufferMapping[originalBuffer] = cloneBuffer;
            clone._vertexBuffers.Add(cloneBuffer);
        }

        var indexBufferMapping = new Dictionary<IndexBuffer, IndexBuffer>();
        foreach (var originalBuffer in _indexBuffers)
        {
            var cloneBuffer = new IndexBuffer();
            cloneBuffer.SetDebugName(originalBuffer.GetDebugName());
            cloneBuffer.SetShadowed(originalBuffer.Shadowed);
            cloneBuffer.SetSize(originalBuffer.IndexCount, originalBuffer.IndexSize == sizeof(uint), originalBuffer.IsDynamic);
            if (originalBuffer.Shadowed)
            {
                var shadowData = originalBuffer.GetRawBuffer()!.GetShadowData();
                cloneBuffer.UpdateRange(shadowData, 0u, (uint)shadowData.Length);
            }
            else
                Log.Logger.LogError("Failed to read original index buffer");
            indexBufferMapping[originalBuffer] = cloneBuffer;
            clone._indexBuffers.Add(cloneBuffer);
        }

        foreach (var lodLevels in _geometries)
        {
            var cloneLodLevels = new List<Geometry>(lodLevels.Count);
            foreach (var originalGeometry in lodLevels)
            {
                var cloneGeometry = new Geometry();
                if (originalGeometry.GetIndexBuffer() is { } originalIndexBuffer)
                    cloneGeometry.SetIndexBuffer(indexBufferMapping[originalIndexBuffer]);
                for (var i = 0u; i < originalGeometry.GetNumVertexBuffers(); ++i)
                {
                    if (originalGeometry.GetVertexBuffer(i) is { } originalVertexBuffer)
                        cloneGeometry.SetVertexBuffer(i, vertexBufferMapping[originalVertexBuffer]);
                }
                cloneGeometry.SetDrawRange(originalGeometry.GetPrimitiveType(), originalGeometry.GetIndexStart(),
                    originalGeometry.GetIndexCount(), originalGeometry.GetVertexStart(), originalGeometry.GetVertexCount(), false);
                cloneGeometry.SetLodDistance(originalGeometry.GetLodDistance());
                cloneLodLevels.Add(cloneGeometry);
            }
            clone._geometries.Add(cloneLodLevels);
        }

        clone.MemoryUse = MemoryUse;
        return clone;
    }

    object ICloneable.Clone() => Clone();

    public IReadOnlyList<ModelMorph> GetMorphs() => _morphs;

    public uint GetNumMorphs() => (uint)_morphs.Count;

    public ModelMorph? GetMorph(uint index) => index < _morphs.Count ? _morphs[(int)index] : null;

    public ModelMorph? GetMorph(string name) => GetMorph(new StringHash(name));

    public ModelMorph? GetMorph(StringHash nameHash)
    {
        foreach (var morph in _morphs)
        {
            if (morph.NameHash == nameHash)
                return morph;
        }

        return null;
    }

    public uint GetMorphRangeStart(uint bufferIndex) => bufferIndex < _morphRangeStarts.Count ? _morphRangeStarts[(int)bufferIndex] : 0u;

    public uint GetMorphRangeCount(uint bufferIndex) => bufferIndex < _morphRangeCounts.Count ? _morphRangeCounts[(int)bufferIndex] : 0u;

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
