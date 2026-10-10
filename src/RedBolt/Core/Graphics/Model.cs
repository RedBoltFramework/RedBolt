// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.Graphics;

using Microsoft.Extensions.Logging;
using SystemMath = global::System.Math;

using RedBolt.Container;
using RedBolt.Core;
using RedBolt.IO;
using FileMode = global::RedBolt.IO.FileMode;
using RedBolt.Logging;
using RedBolt.Math;
using RedBolt.RenderAPI;
using RedBolt.Resource;

/// <summary>Vertex buffer morph data.</summary>
public sealed class VertexBufferMorph : ICloneable
{
    /// <summary>Vertex elements present in the packed data.</summary>
    public VertexMask ElementMask;

    /// <summary>Number of morphed vertices.</summary>
    public uint VertexCount;

    /// <summary>Morphed vertices data size in bytes.</summary>
    public uint DataSize;

    /// <summary>Packed index and vertex-element data.</summary>
    public byte[] MorphData = [];

    public object Clone()
    {
        return new VertexBufferMorph
        {
            ElementMask = ElementMask,
            VertexCount = VertexCount,
            DataSize = DataSize,
            MorphData = MorphData
        };
    }
}

/// <summary>Definition of a model vertex morph.</summary>
public sealed class ModelMorph : ICloneable
{
    /// <summary>Morph name.</summary>
    public string Name = string.Empty;

    /// <summary>Morph name hash.</summary>
    public StringHash NameHash;

    /// <summary>Current morph weight.</summary>
    public float Weight;

    /// <summary>Morph data per vertex buffer.</summary>
    public Dictionary<uint, VertexBufferMorph> Buffers = [];

    public object Clone()
    {
        var clone = new ModelMorph
        {
            Name = Name,
            NameHash = NameHash,
            Weight = Weight
        };
        foreach (var (bufferIndex, buffer) in Buffers)
            clone.Buffers[bufferIndex] = (VertexBufferMorph)buffer.Clone();
        return clone;
    }
}

/// <summary>Description of vertex buffer data for asynchronous loading.</summary>
public sealed class VertexBufferDesc : ICloneable
{
    /// <summary>Vertex count.</summary>
    public uint VertexCount;

    /// <summary>Vertex declaration.</summary>
    public List<VertexElement> VertexElements = [];

    /// <summary>Vertex data size.</summary>
    public uint DataSize;

    /// <summary>Vertex data.</summary>
    public byte[]? Data;

    public object Clone()
    {
        return new VertexBufferDesc
        {
            VertexCount = VertexCount,
            VertexElements = [.. VertexElements],
            DataSize = DataSize,
            Data = Data
        };
    }
}

/// <summary>Description of index buffer data for asynchronous loading.</summary>
public sealed class IndexBufferDesc : ICloneable
{
    /// <summary>Index count.</summary>
    public uint IndexCount;

    /// <summary>Index size.</summary>
    public uint IndexSize;

    /// <summary>Index data size.</summary>
    public uint DataSize;

    /// <summary>Index data.</summary>
    public byte[]? Data;

    public object Clone()
    {
        return new IndexBufferDesc
        {
            IndexCount = IndexCount,
            IndexSize = IndexSize,
            DataSize = DataSize,
            Data = Data
        };
    }
}

/// <summary>Description of a geometry for asynchronous loading.</summary>
public sealed class GeometryDesc : ICloneable
{
    /// <summary>Primitive type.</summary>
    public PrimitiveType Type;

    /// <summary>Vertex buffer ref.</summary>
    public uint VertexBufferRef;

    /// <summary>Index buffer ref.</summary>
    public uint IndexBufferRef;

    /// <summary>Index start.</summary>
    public uint IndexStart;

    /// <summary>Index count.</summary>
    public uint IndexCount;

    public object Clone()
    {
        return new GeometryDesc
        {
            Type = Type,
            VertexBufferRef = VertexBufferRef,
            IndexBufferRef = IndexBufferRef,
            IndexStart = IndexStart,
            IndexCount = IndexCount
        };
    }
}

/// <summary>3D model resource.</summary>
[Reflected]
public sealed partial class Model : ResourceWithMetadata, ICloneable
{
    private const uint LegacyVersion = 1;
    private const uint MorphWeightVersion = 2;
    private const uint CurrentVersion = 2;

    private BoundingBox _boundingBox = new(-0.5f, 0.5f);
    private Skeleton _skeleton = new();
    private readonly List<VertexBuffer> _vertexBuffers = [];
    private readonly List<IndexBuffer> _indexBuffers = [];
    private readonly List<List<Geometry?>> _geometries = [];
    private readonly List<List<uint>> _geometryBoneMappings = [];
    private readonly List<Vector3f> _geometryCenters = [];
    private readonly List<ModelMorph> _morphs = [];
    private readonly List<uint> _morphRangeStarts = [];
    private readonly List<uint> _morphRangeCounts = [];
    private readonly List<VertexBufferDesc> _loadVertexBuffers = [];
    private readonly List<IndexBufferDesc> _loadIndexBuffers = [];
    private readonly List<List<GeometryDesc>> _loadGeometries = [];

    public Model()
    {
    }


    /// <summary>Destruct.</summary>
    public override void Dispose()
    {
        _skeleton.Dispose();
        base.Dispose();
    }

    public override bool BeginLoad(Deserializer source)
    {
        ArgumentNullException.ThrowIfNull(source);

        return LoadUrhoModel(source);
    }

    private bool LoadUrhoModel(Deserializer source)
    {
        // Check ID
        var fileId = source.ReadFileID();
        if (fileId is not ("UMDL" or "UMD2" or "UMD3"))
        {
            Log.GetLogger().LogError("{ModelName} is not a valid model file", Name);
            return false;
        }

        // Read version
        var version = fileId == "UMD3" ? source.ReadUInt() : LegacyVersion;
        var hasVertexDeclarations = fileId != "UMDL";

        _geometries.Clear();
        _geometryCenters.Clear();
        _geometryBoneMappings.Clear();
        _vertexBuffers.Clear();
        _indexBuffers.Clear();
        _morphs.Clear();
        _morphRangeStarts.Clear();
        _morphRangeCounts.Clear();
        _loadVertexBuffers.Clear();
        _loadIndexBuffers.Clear();
        _loadGeometries.Clear();

        var async = AsyncLoadState == AsyncLoadState.Loading;
        var numVertexBuffers = source.ReadUInt();
        for (var i = 0u; i < numVertexBuffers; ++i)
        {
            var desc = new VertexBufferDesc
            {
                VertexCount = source.ReadUInt(),
                VertexElements = hasVertexDeclarations
                    ? ReadVertexElements(source)
                    : VertexBuffer.GetElements(source.ReadUInt())
            };

            _morphRangeStarts.Add(source.ReadUInt());
            _morphRangeCounts.Add(source.ReadUInt());

            var vertexBuffer = new VertexBuffer();
            vertexBuffer.SetDebugName($"Model '{Name}' Vertex Buffer #{i}");
            desc.DataSize = desc.VertexCount * VertexBuffer.GetVertexSize(desc.VertexElements);
            var dataSize = checked((int)desc.DataSize);
            if (async)
            {
                desc.Data = new byte[dataSize];
                _ = source.Read(desc.Data);
            }
            else
            {
                vertexBuffer.SetShadowed(true);
                vertexBuffer.SetSize(desc.VertexCount, desc.VertexElements);
                _ = source.Read(vertexBuffer.Map());
                vertexBuffer.Unmap();
            }
            _vertexBuffers.Add(vertexBuffer);
            _loadVertexBuffers.Add(desc);
        }

        var numIndexBuffers = source.ReadUInt();
        for (var i = 0u; i < numIndexBuffers; ++i)
        {
            var desc = new IndexBufferDesc
            {
                IndexCount = source.ReadUInt(),
                IndexSize = source.ReadUInt()
            };

            var indexBuffer = new IndexBuffer();
            indexBuffer.SetDebugName($"Model '{Name}' Index Buffer #{i}");
            desc.DataSize = desc.IndexCount * desc.IndexSize;
            var dataSize = checked((int)desc.DataSize);
            if (async)
            {
                desc.Data = new byte[dataSize];
                _ = source.Read(desc.Data);
            }
            else
            {
                indexBuffer.SetShadowed(true);
                indexBuffer.SetSize(desc.IndexCount, desc.IndexSize > sizeof(ushort), dynamic: false);
                _ = source.Read(indexBuffer.GetRawBuffer().Map());
                indexBuffer.GetRawBuffer().Unmap();
            }
            _indexBuffers.Add(indexBuffer);
            _loadIndexBuffers.Add(desc);
        }

        var numGeometries = source.ReadUInt();
        SetNumGeometries(numGeometries);
        for (var geometryIndex = 0u; geometryIndex < numGeometries; ++geometryIndex)
        {
            var boneMappingCount = source.ReadUInt();
            var boneMapping = new List<uint>(checked((int)boneMappingCount));
            for (var mappingIndex = 0u; mappingIndex < boneMappingCount; ++mappingIndex)
                boneMapping.Add(source.ReadUInt());
            _geometryBoneMappings[(int)geometryIndex] = boneMapping;

            var numLodLevels = source.ReadUInt();
            var lodLevels = _geometries[(int)geometryIndex];
            lodLevels.Clear();
            for (var lodLevel = 0u; lodLevel < numLodLevels; ++lodLevel)
                lodLevels.Add(null);

            var geometryDescs = new List<GeometryDesc>(checked((int)numLodLevels));
            for (var lodLevel = 0u; lodLevel < numLodLevels; ++lodLevel)
            {
                var lodDistance = source.ReadFloat();
                var desc = new GeometryDesc
                {
                    Type = (PrimitiveType)source.ReadUInt(),
                    VertexBufferRef = source.ReadUInt(),
                    IndexBufferRef = source.ReadUInt(),
                    IndexStart = source.ReadUInt(),
                    IndexCount = source.ReadUInt()
                };

                if (desc.VertexBufferRef >= _vertexBuffers.Count)
                {
                    Log.GetLogger().LogError("Vertex buffer index out of bounds");
                    _loadVertexBuffers.Clear();
                    _loadIndexBuffers.Clear();
                    _loadGeometries.Clear();
                    return false;
                }
                if (desc.IndexBufferRef >= _indexBuffers.Count)
                {
                    Log.GetLogger().LogError("Index buffer index out of bounds");
                    _loadVertexBuffers.Clear();
                    _loadIndexBuffers.Clear();
                    _loadGeometries.Clear();
                    return false;
                }

                var geometry = new Geometry();
                geometry.SetLodDistance(lodDistance);
                if (!SetGeometry(geometryIndex, lodLevel, geometry))
                    return false;
                geometryDescs.Add(desc);
            }
            _loadGeometries.Add(geometryDescs);
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

        var virtualFileSystem = GetSubsystem<VirtualFileSystem>();
        if (virtualFileSystem is not null && !string.IsNullOrEmpty(Name))
        {
            var xmlName = Path.ChangeExtension(Name, ".xml");
            var xmlSource = virtualFileSystem.OpenFile(new FileIdentifier(xmlName), FileMode.Read);
            if (xmlSource is not null)
            {
                var xmlData = new byte[checked((int)xmlSource.Size)];
                var readBytes = xmlSource.Read(xmlData);
                xmlSource.Close();
                using var xmlFile = new XMLFile();
                if (readBytes == xmlData.Length && xmlFile.BeginLoad(new MemoryBuffer(xmlData)))
                {
                    using var metadata = CreateMetadataFromXML(xmlFile.GetRoot());
                    SetMetadata(metadata);
                }
            }
        }

        long totalBytes = 0;
        foreach (var desc in _loadVertexBuffers)
            totalBytes += desc.DataSize;
        foreach (var desc in _loadIndexBuffers)
            totalBytes += desc.DataSize;
        MemoryUse = checked((int)totalBytes);
        return true;
    }

    public override bool EndLoad()
    {
        for (var i = 0; i < _loadVertexBuffers.Count; ++i)
        {
            var desc = _loadVertexBuffers[i];
            if (desc.Data is not null)
            {
                var buffer = _vertexBuffers[i];
                buffer.SetShadowed(true);
                buffer.SetSize(desc.VertexCount, desc.VertexElements);
                desc.Data.AsSpan().CopyTo(buffer.Map());
                buffer.Unmap();
            }
        }

        for (var i = 0; i < _loadIndexBuffers.Count; ++i)
        {
            var desc = _loadIndexBuffers[i];
            if (desc.Data is not null)
            {
                var buffer = _indexBuffers[i];
                buffer.SetShadowed(true);
                buffer.SetSize(desc.IndexCount, desc.IndexSize > sizeof(ushort), dynamic: false);
                buffer.Update(desc.Data);
            }
        }

        for (var geometryIndex = 0; geometryIndex < _loadGeometries.Count; ++geometryIndex)
        {
            for (var lodLevel = 0; lodLevel < _loadGeometries[geometryIndex].Count; ++lodLevel)
            {
                var desc = _loadGeometries[geometryIndex][lodLevel];
                var geometry = _geometries[geometryIndex][lodLevel]!;
                geometry.SetVertexBuffer(0, _vertexBuffers[(int)desc.VertexBufferRef]);
                geometry.SetIndexBuffer(_indexBuffers[(int)desc.IndexBufferRef]);
                geometry.SetDrawRange(desc.Type, desc.IndexStart, desc.IndexCount);
            }
        }

        _loadVertexBuffers.Clear();
        _loadIndexBuffers.Clear();
        _loadGeometries.Clear();
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

    public override bool Save(Serializer destination)
    {
        ArgumentNullException.ThrowIfNull(destination);

        var output = new VectorBuffer();
        if (!output.WriteFileID("UMD3"))
            return false;
        output.WriteUInt(CurrentVersion);

        output.WriteUInt(checked((uint)_vertexBuffers.Count));
        for (var i = 0; i < _vertexBuffers.Count; ++i)
        {
            var buffer = _vertexBuffers[i];
            output.WriteUInt(buffer.GetVertexCount());
            var elements = buffer.GetElements();
            output.WriteUInt(checked((uint)elements.Count));
            foreach (var element in elements)
            {
                var elementDesc = (uint)element.Type | ((uint)element.Semantic << 8) | ((uint)element.Index << 16);
                output.WriteUInt(elementDesc);
            }
            output.WriteUInt(_morphRangeStarts[i]);
            output.WriteUInt(_morphRangeCounts[i]);
            output.Write(buffer.GetRawBuffer()!.GetShadowData());
        }

        output.WriteUInt(checked((uint)_indexBuffers.Count));
        foreach (var buffer in _indexBuffers)
        {
            output.WriteUInt(buffer.GetIndexCount());
            output.WriteUInt(buffer.GetIndexSize());
            output.Write(buffer.GetShadowData());
        }

        output.WriteUInt(checked((uint)_geometries.Count));
        for (var i = 0; i < _geometries.Count; ++i)
        {
            output.WriteUInt(checked((uint)_geometryBoneMappings[i].Count));
            foreach (var boneIndex in _geometryBoneMappings[i])
                output.WriteUInt(boneIndex);

            output.WriteUInt(checked((uint)_geometries[i].Count));
            foreach (var geometry in _geometries[i])
            {
                output.WriteFloat(geometry!.GetLodDistance());
                output.WriteUInt((uint)geometry.GetPrimitiveType());
                output.WriteUInt(LookupVertexBuffer(geometry.GetVertexBuffer(0)));
                output.WriteUInt(LookupIndexBuffer(geometry.GetIndexBuffer()));
                output.WriteUInt(geometry.GetIndexStart());
                output.WriteUInt(geometry.GetIndexCount());
            }
        }

        output.WriteUInt(checked((uint)_morphs.Count));
        foreach (var morph in _morphs)
        {
            output.WriteString(morph.Name);
            output.WriteFloat(morph.Weight);
            output.WriteUInt(checked((uint)morph.Buffers.Count));
            foreach (var (bufferIndex, buffer) in morph.Buffers)
            {
                output.WriteUInt(bufferIndex);
                output.WriteUInt((uint)buffer.ElementMask);
                output.WriteUInt(buffer.VertexCount);
                output.Write(buffer.MorphData);
            }
        }

        _skeleton.Save(output);
        output.WriteBoundingBox(_boundingBox);
        foreach (var center in _geometryCenters)
            output.WriteVector3(center);

        destination.Write(output.Buffer);

        if (HasMetadata())
        {
            // The metadata goes beside the destination file. A destination that is not a file (a memory buffer) gets none.
            using var destinationXmlFile = destination is global::RedBolt.IO.File
                ? GetSubsystem<VirtualFileSystem>()?.OpenFile(new FileIdentifier(Path.ChangeExtension(destination.GetName(), ".xml")), FileMode.Write)
                : null;
            if (destinationXmlFile is not null)
            {
                using var xml = new XMLFile();
                var root = xml.CreateRoot("model");
                SaveMetadataToXML(root);
                xml.Save(destinationXmlFile);
            }
            else
                Log.GetLogger().LogWarning("Can not save model metadata when not saving into a file");
        }

        return true;
    }

    private uint LookupVertexBuffer(VertexBuffer? buffer)
    {
        var index = buffer is null ? -1 : _vertexBuffers.IndexOf(buffer);
        return index >= 0 ? checked((uint)index) : 0u;
    }

    private uint LookupIndexBuffer(IndexBuffer? buffer)
    {
        var index = buffer is null ? -1 : _indexBuffers.IndexOf(buffer);
        return index >= 0 ? checked((uint)index) : 0u;
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
                Log.GetLogger().LogError("Null model vertex buffers specified");
                return false;
            }
            if (!buffer.IsShadowed())
            {
                Log.GetLogger().LogError("Model vertex buffers must be shadowed");
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
                Log.GetLogger().LogError("Null model index buffers specified");
                return false;
            }
            if (!buffer.IsShadowed())
            {
                Log.GetLogger().LogError("Model index buffers must be shadowed");
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
        var replacement = skeleton.Clone();
        _skeleton.Dispose();
        _skeleton = replacement;
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
        foreach (var morph in morphs)
            _morphs.Add((ModelMorph)morph.Clone());
    }

    public Model Clone(string cloneName = "")
    {
        var clone = new Model()
        {
            _boundingBox = _boundingBox
        };
        clone.SetName(cloneName);
        clone._skeleton.Dispose();
        clone._skeleton = _skeleton.Clone();
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
            cloneBuffer.SetShadowed(originalBuffer.IsShadowed());
            cloneBuffer.SetSize(originalBuffer.GetVertexCount(), originalBuffer.GetElements(), originalBuffer.IsDynamic);
            if (originalBuffer.IsShadowed())
                cloneBuffer.Update(originalBuffer.GetShadowData());
            else
                Log.GetLogger().LogError("Failed to read original vertex buffer");
            vertexBufferMapping[originalBuffer] = cloneBuffer;
            clone._vertexBuffers.Add(cloneBuffer);
        }

        var indexBufferMapping = new Dictionary<IndexBuffer, IndexBuffer>();
        foreach (var originalBuffer in _indexBuffers)
        {
            var cloneBuffer = new IndexBuffer();
            cloneBuffer.SetDebugName(originalBuffer.GetDebugName());
            cloneBuffer.SetShadowed(originalBuffer.IsShadowed());
            cloneBuffer.SetSize(originalBuffer.GetIndexCount(), originalBuffer.GetIndexSize() == sizeof(uint),
                originalBuffer.IsDynamic());
            if (originalBuffer.IsShadowed())
            {
                cloneBuffer.Update(originalBuffer.GetShadowData());
            }
            else
                Log.GetLogger().LogError("Failed to read original index buffer");
            indexBufferMapping[originalBuffer] = cloneBuffer;
            clone._indexBuffers.Add(cloneBuffer);
        }

        foreach (var lodLevels in _geometries)
        {
            var cloneLodLevels = new List<Geometry?>(lodLevels.Count);
            foreach (var originalGeometry in lodLevels)
            {
                if (originalGeometry is null)
                {
                    cloneLodLevels.Add(null);
                    continue;
                }
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

    /// <summary>Return morph range starts for each vertex buffer.</summary>
    public IReadOnlyList<uint> GetMorphRangeStarts() => _morphRangeStarts;

    /// <summary>Return morph range vertex counts for each vertex buffer.</summary>
    public IReadOnlyList<uint> GetMorphRangeCounts() => _morphRangeCounts;

    public IReadOnlyList<IReadOnlyList<Geometry?>> GetGeometries() => _geometries;

    public IReadOnlyList<Vector3f> GetGeometryCenters() => _geometryCenters;

    public uint GetNumGeometries() => (uint)_geometries.Count;

    public void SetNumGeometries(uint num)
    {
        while (_geometries.Count < num)
        {
            _geometries.Add([null]);
            _geometryBoneMappings.Add([]);
            _geometryCenters.Add(Vector3f.Zero);
        }

        while (_geometries.Count > num)
        {
            _geometries.RemoveAt(_geometries.Count - 1);
            _geometryBoneMappings.RemoveAt(_geometryBoneMappings.Count - 1);
            _geometryCenters.RemoveAt(_geometryCenters.Count - 1);
        }

        // For easier creation of from-scratch geometry, ensure that all geometries start with at least 1 LOD level (0 makes no sense)
        foreach (var geometry in _geometries)
        {
            if (geometry.Count == 0)
                geometry.Add(null);
        }
    }

    public bool SetNumGeometryLodLevels(uint index, uint num)
    {
        if (index >= _geometries.Count)
        {
            Log.GetLogger().LogError("Geometry index out of bounds");
            return false;
        }
        if (num == 0)
        {
            Log.GetLogger().LogError("Zero LOD levels not allowed");
            return false;
        }

        var lodLevels = _geometries[(int)index];
        while (lodLevels.Count < num)
            lodLevels.Add(null);

        while (lodLevels.Count > num)
            lodLevels.RemoveAt(lodLevels.Count - 1);

        return true;
    }

    public bool SetGeometry(uint index, uint lodLevel, Geometry? geometry)
    {
        if (index >= _geometries.Count)
        {
            Log.GetLogger().LogError("Geometry index out of bounds");
            return false;
        }

        var lodLevels = _geometries[(int)index];
        if (lodLevel >= lodLevels.Count)
        {
            Log.GetLogger().LogError("LOD level index out of bounds");
            return false;
        }

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

        return lodLevels[(int)SystemMath.Min(lodLevel, (uint)lodLevels.Count - 1)];
    }

    public uint GetNumGeometryLodLevels(uint index)
    {
        return index < _geometries.Count ? (uint)_geometries[(int)index].Count : 0u;
    }

    public bool SetGeometryCenter(uint index, Vector3f center)
    {
        if (index >= _geometryCenters.Count)
        {
            Log.GetLogger().LogError("Geometry index out of bounds");
            return false;
        }

        _geometryCenters[(int)index] = center;
        return true;
    }

    public Vector3f GetGeometryCenter(uint index)
    {
        return index < _geometryCenters.Count ? _geometryCenters[(int)index] : Vector3f.Zero;
    }

}
