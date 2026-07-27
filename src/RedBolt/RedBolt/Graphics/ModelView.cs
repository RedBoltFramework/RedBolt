using System.Collections;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace RedBolt;

using ModelVertexMorphVector = List<ModelVertexMorph>;

/// <summary>Model vertex format, unpacked for easy editing.</summary>
public struct ModelVertexFormat : IEquatable<ModelVertexFormat>, ICloneable
{
    public static readonly VertexElementType Undefined = VertexElementType.MaxVertexElementTypes;
    public const int MaxColors = 4;
    public const int MaxUVs = 4;

    /// <summary>Vertex element formats.</summary>
    public VertexElementType Position;
    public VertexElementType Normal;
    public VertexElementType Tangent;
    public VertexElementType Binormal;
    public VertexElementType BlendIndices;
    public VertexElementType BlendWeights;
    public VertexElementType[] Color;
    public VertexElementType[] Uv;

    public ModelVertexFormat()
    {
        Position = Undefined;
        Normal = Undefined;
        Tangent = Undefined;
        Binormal = Undefined;
        BlendIndices = Undefined;
        BlendWeights = Undefined;
        Color = [Undefined, Undefined, Undefined, Undefined];
        Uv = [Undefined, Undefined, Undefined, Undefined];
    }

    public readonly ModelVertexFormat Clone()
    {
        var clone = this;
        clone.Color = (VertexElementType[])Color.Clone();
        clone.Uv = (VertexElementType[])Uv.Clone();
        return clone;
    }

    readonly object ICloneable.Clone() => Clone();

    /// <summary>Convert to vertex elements.</summary>
    public List<VertexElement> ToVertexElements()
    {
        var result = new List<VertexElement>();
        AddElement(result, Position, VertexElementSemantic.Position);
        AddElement(result, Normal, VertexElementSemantic.Normal);
        AddElement(result, Binormal, VertexElementSemantic.Binormal);
        AddElement(result, Tangent, VertexElementSemantic.Tangent);
        AddElement(result, BlendWeights, VertexElementSemantic.BlendWeights);
        AddElement(result, BlendIndices, VertexElementSemantic.BlendIndices);

        for (byte i = 0; i < MaxUVs; ++i)
            AddElement(result, Uv[i], VertexElementSemantic.TexCoord, i);
        for (byte i = 0; i < MaxColors; ++i)
            AddElement(result, Color[i], VertexElementSemantic.Color, i);

        VertexBuffer.UpdateOffsets(result);
        return result;
    }

    /// <summary>Merge non-undefined elements from another format.</summary>
    public void MergeFrom(ModelVertexFormat rhs)
    {
        Merge(ref Position, rhs.Position);
        Merge(ref Normal, rhs.Normal);
        Merge(ref Tangent, rhs.Tangent);
        Merge(ref Binormal, rhs.Binormal);
        Merge(ref BlendIndices, rhs.BlendIndices);
        Merge(ref BlendWeights, rhs.BlendWeights);
        for (var i = 0; i < MaxColors; ++i)
            Merge(ref Color[i], rhs.Color[i]);
        for (var i = 0; i < MaxUVs; ++i)
            Merge(ref Uv[i], rhs.Uv[i]);
    }

    /// <summary>Return format hash.</summary>
    public uint ToHash()
    {
        uint hash = 0;
        CombineHash(ref hash, (uint)Position);
        CombineHash(ref hash, (uint)Normal);
        CombineHash(ref hash, (uint)Tangent);
        CombineHash(ref hash, (uint)Binormal);
        CombineHash(ref hash, (uint)BlendIndices);
        CombineHash(ref hash, (uint)BlendWeights);
        foreach (var type in Color)
            CombineHash(ref hash, (uint)type);
        foreach (var type in Uv)
            CombineHash(ref hash, (uint)type);
        return hash;
    }

    public bool Equals(ModelVertexFormat rhs)
    {
        return Position == rhs.Position
            && Normal == rhs.Normal
            && Tangent == rhs.Tangent
            && Binormal == rhs.Binormal
            && BlendIndices == rhs.BlendIndices
            && BlendWeights == rhs.BlendWeights
            && Color.SequenceEqual(rhs.Color)
            && Uv.SequenceEqual(rhs.Uv);
    }

    public override bool Equals(object? obj) => obj is ModelVertexFormat rhs && Equals(rhs);

    public override int GetHashCode() => unchecked((int)ToHash());

    public static bool operator ==(ModelVertexFormat lhs, ModelVertexFormat rhs) => lhs.Equals(rhs);

    public static bool operator !=(ModelVertexFormat lhs, ModelVertexFormat rhs) => !lhs.Equals(rhs);

    private static void AddElement(List<VertexElement> elements, VertexElementType type,
        VertexElementSemantic semantic, byte index = 0)
    {
        if (type != Undefined)
            elements.Add(new VertexElement(type, semantic, index));
    }

    private static void Merge(ref VertexElementType lhs, VertexElementType rhs)
    {
        if (rhs != Undefined)
            lhs = rhs;
    }

    private static void CombineHash(ref uint result, uint hash)
    {
        result ^= hash + 0x9e3779b9u + (result << 6) + (result >> 2);
    }
}

/// <summary>Model vertex, unpacked for easy editing.</summary>
public struct ModelVertex : IEquatable<ModelVertex>, ICloneable
{
    public const int MaxBones = 4;
    public const int MaxColors = ModelVertexFormat.MaxColors;
    public const int MaxUVs = ModelVertexFormat.MaxUVs;

    /// <summary>Vertex elements corresponding to the full ModelVertex.</summary>
    public static readonly VertexElement[] VertexElements =
    [
        new(VertexElementType.Vector4, VertexElementSemantic.Position),
        new(VertexElementType.Vector4, VertexElementSemantic.Normal),
        new(VertexElementType.Vector4, VertexElementSemantic.Tangent),
        new(VertexElementType.Vector4, VertexElementSemantic.Binormal),
        new(VertexElementType.Vector4, VertexElementSemantic.BlendIndices),
        new(VertexElementType.Vector4, VertexElementSemantic.BlendWeights),
        new(VertexElementType.Vector4, VertexElementSemantic.Color, 0),
        new(VertexElementType.Vector4, VertexElementSemantic.Color, 1),
        new(VertexElementType.Vector4, VertexElementSemantic.Color, 2),
        new(VertexElementType.Vector4, VertexElementSemantic.Color, 3),
        new(VertexElementType.Vector4, VertexElementSemantic.TexCoord, 0),
        new(VertexElementType.Vector4, VertexElementSemantic.TexCoord, 1),
        new(VertexElementType.Vector4, VertexElementSemantic.TexCoord, 2),
        new(VertexElementType.Vector4, VertexElementSemantic.TexCoord, 3)
    ];

    public Vector4f Position;
    public Vector4f Normal;
    public Vector4f Tangent;
    public Vector4f Binormal;
    public Vector4f BlendIndices;
    public Vector4f BlendWeights;
    public Vector4f[] Color;
    public Vector4f[] Uv;

    public ModelVertex()
    {
        Position = Vector4f.Zero;
        Normal = Vector4f.Zero;
        Tangent = Vector4f.Zero;
        Binormal = Vector4f.Zero;
        BlendIndices = Vector4f.Zero;
        BlendWeights = Vector4f.Zero;
        Color = new Vector4f[MaxColors];
        Uv = new Vector4f[MaxUVs];
    }

    public readonly ModelVertex Clone()
    {
        var clone = this;
        clone.Color = (Vector4f[])Color.Clone();
        clone.Uv = (Vector4f[])Uv.Clone();
        return clone;
    }

    readonly object ICloneable.Clone() => Clone();

    /// <summary>Set position from 3-vector.</summary>
    public void SetPosition(Vector3f position) => Position = position.ToVector4(1.0f);

    /// <summary>Set normal from 3-vector.</summary>
    public void SetNormal(Vector3f normal) => Normal = normal.ToVector4();

    /// <summary>Set color for given channel.</summary>
    public void SetColor(uint index, Color color) => Color[checked((int)index)] = color.ToVector4();

    /// <summary>Return position as 3-vector.</summary>
    public Vector3f GetPosition() => Position.ToVector3();

    /// <summary>Return normal as 3-vector.</summary>
    public Vector3f GetNormal() => Normal.ToVector3();

    /// <summary>Return tangent as 3-vector.</summary>
    public Vector3f GetTangent() => Tangent.ToVector3();

    /// <summary>Return color from given channel.</summary>
    public Color GetColor(uint index = 0) => new(Color[checked((int)index)]);

    /// <summary>Return blend indices and weights.</summary>
    public KeyValuePair<uint, float>[] GetBlendIndicesAndWeights()
    {
        var result = new KeyValuePair<uint, float>[MaxBones];
        for (var i = 0; i < MaxBones; ++i)
        {
            var index = BlendIndices[i];
            result[i] = index is >= 0 and <= 16777216
                ? new KeyValuePair<uint, float>((uint)index, BlendWeights[i])
                : new KeyValuePair<uint, float>(0, 0.0f);
        }
        return result;
    }

    /// <summary>Return whether the vertex has normal.</summary>
    public bool HasNormal() => Normal != Vector4f.Zero;

    /// <summary>Return whether the vertex has tangent.</summary>
    public bool HasTangent() => Tangent != Vector4f.Zero;

    /// <summary>Return whether the vertex has binormal.</summary>
    public bool HasBinormal() => Binormal != Vector4f.Zero;

    /// <summary>Return whether the vertex has tangent and binormal combined.</summary>
    public bool HasTangentBinormalCombined() => Tangent != Vector4f.Zero && Tangent.W != 0;

    /// <summary>Return vertex as an array of Vector4f elements.</summary>
    public Vector4f[] Data() =>
    [
        Position, Normal, Tangent, Binormal, BlendIndices, BlendWeights,
        Color[0], Color[1], Color[2], Color[3], Uv[0], Uv[1], Uv[2], Uv[3]
    ];

    /// <summary>Replace given semantics from another vertex.</summary>
    public bool ReplaceElement(ModelVertex source, VertexElement element)
    {
        switch (element.Semantic)
        {
            case VertexElementSemantic.Position:
                Position = source.Position;
                return true;
            case VertexElementSemantic.Normal:
                Normal = source.Normal;
                return true;
            case VertexElementSemantic.Binormal:
                Binormal = source.Binormal;
                return true;
            case VertexElementSemantic.Tangent:
                Tangent = source.Tangent;
                return true;
            case VertexElementSemantic.TexCoord:
                if (element.Index >= MaxUVs)
                    return false;
                Uv[element.Index] = source.Uv[element.Index];
                return true;
            case VertexElementSemantic.Color:
                if (element.Index >= MaxColors)
                    return false;
                Color[element.Index] = source.Color[element.Index];
                return true;
            case VertexElementSemantic.BlendWeights:
                BlendWeights = source.BlendWeights;
                return true;
            case VertexElementSemantic.BlendIndices:
                BlendIndices = source.BlendIndices;
                return true;
            default:
                return false;
        }
    }

    /// <summary>Repair missing vertex elements if possible.</summary>
    public void Repair()
    {
        Normal.W = 0;
        Binormal.W = 0;
        if (HasNormal())
        {
            if (HasTangent())
            {
                if (HasTangentBinormalCombined() && !HasBinormal())
                {
                    var normal = Tangent.ToVector3();
                    var tangent = Normal.ToVector3();
                    var binormal = Tangent.W * normal.CrossProduct(tangent);
                    Binormal = binormal.Normalized.ToVector4();
                }
                else if (HasBinormal() && !HasTangentBinormalCombined())
                {
                    var normal = Tangent.ToVector3();
                    var tangent = Normal.ToVector3();
                    var binormal = Binormal.ToVector3();
                    Tangent.W = normal.CrossProduct(tangent).DotProduct(binormal) >= 0 ? 1.0f : -1.0f;
                }
            }
            else
                Binormal = Vector4f.Zero;
        }
        else
        {
            Tangent = Vector4f.Zero;
            Binormal = Vector4f.Zero;
        }
    }

    /// <summary>Prune vertex elements not represented in the format.</summary>
    public void PruneElements(ModelVertexFormat format)
    {
        if (format.Position == ModelVertexFormat.Undefined) Position = Vector4f.Zero;
        if (format.Normal == ModelVertexFormat.Undefined) Normal = Vector4f.Zero;
        if (format.Tangent == ModelVertexFormat.Undefined) Tangent = Vector4f.Zero;
        if (format.Binormal == ModelVertexFormat.Undefined) Binormal = Vector4f.Zero;
        if (format.BlendIndices == ModelVertexFormat.Undefined) BlendIndices = Vector4f.Zero;
        if (format.BlendWeights == ModelVertexFormat.Undefined) BlendWeights = Vector4f.Zero;
        if (format.Position == ModelVertexFormat.Undefined) Position = Vector4f.Zero;
        for (var i = 0; i < MaxColors; ++i)
        {
            if (format.Color[i] == ModelVertexFormat.Undefined)
                Color[i] = Vector4f.Zero;
        }
        for (var i = 0; i < MaxUVs; ++i)
        {
            if (format.Uv[i] == ModelVertexFormat.Undefined)
                Uv[i] = Vector4f.Zero;
        }
    }

    public bool Equals(ModelVertex rhs)
    {
        for (var i = 0; i < MaxColors; ++i)
        {
            if (!Color[i].ApproximatelyEquivalent(rhs.Color[i]))
                return false;
        }
        for (var i = 0; i < MaxUVs; ++i)
        {
            if (!Uv[i].ApproximatelyEquivalent(rhs.Uv[i]))
                return false;
        }
        return Position.ApproximatelyEquivalent(rhs.Position) && Normal.ApproximatelyEquivalent(rhs.Normal)
            && Tangent.ApproximatelyEquivalent(rhs.Tangent)
            && BlendIndices.ApproximatelyEquivalent(rhs.BlendIndices)
            && BlendWeights.ApproximatelyEquivalent(rhs.BlendWeights)
            && Binormal.ApproximatelyEquivalent(rhs.Binormal);
    }

    public override bool Equals(object? obj) => obj is ModelVertex rhs && Equals(rhs);

    public override int GetHashCode() => 0;

    public static bool operator ==(ModelVertex lhs, ModelVertex rhs) => lhs.Equals(rhs);

    public static bool operator !=(ModelVertex lhs, ModelVertex rhs) => !lhs.Equals(rhs);
}

/// <summary>Morph of ModelVertex.</summary>
public struct ModelVertexMorph : IEquatable<ModelVertexMorph>
{
    public uint Index;
    public Vector3f PositionDelta;
    public Vector3f NormalDelta;
    public Vector3f TangentDelta;

    public bool HasPosition() => PositionDelta != Vector3f.Zero;
    public bool HasNormal() => NormalDelta != Vector3f.Zero;
    public bool HasTangent() => TangentDelta != Vector3f.Zero;
    public bool IsEmpty() => !HasPosition() && !HasNormal() && !HasTangent();

    public bool Equals(ModelVertexMorph rhs) => PositionDelta.ApproximatelyEquivalent(rhs.PositionDelta)
        && NormalDelta.ApproximatelyEquivalent(rhs.NormalDelta)
        && TangentDelta.ApproximatelyEquivalent(rhs.TangentDelta);

    public override bool Equals(object? obj) => obj is ModelVertexMorph rhs && Equals(rhs);

    public override int GetHashCode() => 0;

    public static bool operator ==(ModelVertexMorph lhs, ModelVertexMorph rhs) => lhs.Equals(rhs);

    public static bool operator !=(ModelVertexMorph lhs, ModelVertexMorph rhs) => !lhs.Equals(rhs);
}

/// <summary>Normalize a model vertex morph vector.</summary>
public static class ModelViewFunctions
{
    public static void NormalizeModelVertexMorphVector(ModelVertexMorphVector morphVector)
    {
        morphVector.Sort(static (lhs, rhs) => lhs.Index.CompareTo(rhs.Index));
        for (var i = morphVector.Count - 1; i > 0; --i)
        {
            if (morphVector[i - 1].Index == morphVector[i].Index)
                morphVector.RemoveAt(i);
        }
        morphVector.RemoveAll(static morph => morph.IsEmpty());
    }
}

/// <summary>Level of detail of Model geometry, unpacked for easy editing.</summary>
public struct GeometryLODView : IEquatable<GeometryLODView>, ICloneable
{
    public PrimitiveType PrimitiveType;
    public List<ModelVertex> Vertices;
    public List<uint> Indices;
    public float LodDistance;
    public ModelVertexFormat VertexFormat;
    public Dictionary<uint, ModelVertexMorphVector> Morphs;

    public GeometryLODView()
    {
        PrimitiveType = PrimitiveType.TriangleList;
        Vertices = [];
        Indices = [];
        LodDistance = 0;
        VertexFormat = new ModelVertexFormat();
        Morphs = [];
    }

    public readonly GeometryLODView Clone()
    {
        var clone = this;
        clone.Vertices = Vertices.Select(static vertex => vertex.Clone()).ToList();
        clone.Indices = [.. Indices];
        clone.VertexFormat = VertexFormat.Clone();
        clone.Morphs = Morphs.ToDictionary(static pair => pair.Key,
            static pair => new ModelVertexMorphVector(pair.Value));
        return clone;
    }

    readonly object ICloneable.Clone() => Clone();

    /// <summary>Return number of primitives.</summary>
    public uint GetNumPrimitives() => PrimitiveType switch
    {
        PrimitiveType.TriangleList => (uint)(Indices.Count / 3),
        PrimitiveType.LineList => (uint)(Indices.Count / 2),
        PrimitiveType.PointList => (uint)Indices.Count,
        PrimitiveType.TriangleStrip or PrimitiveType.TriangleFan => Indices.Count >= 3 ? (uint)Indices.Count - 2 : 0,
        PrimitiveType.LineStrip => Indices.Count >= 2 ? (uint)Indices.Count - 1 : 0,
        _ => 0
    };

    public bool IsTriangleGeometry() => PrimitiveType is PrimitiveType.TriangleList or PrimitiveType.TriangleStrip or PrimitiveType.TriangleFan;
    public bool IsLineGeometry() => PrimitiveType is PrimitiveType.LineList or PrimitiveType.LineStrip;
    public bool IsPointGeometry() => PrimitiveType == PrimitiveType.PointList;

    /// <summary>Calculate center of vertices' bounding box.</summary>
    public Vector3f CalculateCenter()
    {
        if (Vertices.Count == 0)
            return Vector3f.Zero;
        var center = Vector3f.Zero;
        foreach (var vertex in Vertices)
            center += vertex.Position.ToVector3();
        return center / Vertices.Count;
    }

    /// <summary>Calculate number of morphs in the model.</summary>
    public uint CalculateNumMorphs()
    {
        uint result = 0;
        foreach (var index in Morphs.Keys)
            result = Math.Max(result, index + 1);
        return result;
    }

    /// <summary>All equivalent views should be literally equal after normalization.</summary>
    public void Normalize()
    {
        for (var i = 0; i < Vertices.Count; ++i)
        {
            var normalizedVertex = Vertices[i];
            normalizedVertex.PruneElements(VertexFormat);
            Vertices[i] = normalizedVertex;
        }
        if (Indices.Count == 0)
        {
            for (uint i = 0; i < Vertices.Count; ++i)
                Indices.Add(i);
        }
        foreach (var morph in Morphs.Values)
            ModelViewFunctions.NormalizeModelVertexMorphVector(morph);
    }

    public void InvalidateNormalsAndTangents()
    {
        for (var i = 0; i < Vertices.Count; ++i)
        {
            var vertex = Vertices[i];
            vertex.Normal = Vector4f.Zero;
            vertex.Tangent = Vector4f.Zero;
            Vertices[i] = vertex;
        }
        foreach (var morphs in Morphs.Values)
        {
            for (var i = 0; i < morphs.Count; ++i)
            {
                var morph = morphs[i];
                morph.NormalDelta = Vector3f.Zero;
                morph.TangentDelta = Vector3f.Zero;
                morphs[i] = morph;
            }
        }
    }

    public void RecalculateFlatNormals()
    {
        if (!IsTriangleGeometry())
        {
            System.Diagnostics.Debug.Assert(false);
            return;
        }
        InvalidateNormalsAndTangents();
        var newVertices = new List<ModelVertex>();
        var oldToNewVertex = new Dictionary<uint, List<uint>>();
        var sourceVertices = Vertices;
        ForEachTriangle((i0, i1, i2) =>
        {
            var v0 = sourceVertices[(int)i0].Clone();
            var v1 = sourceVertices[(int)i1].Clone();
            var v2 = sourceVertices[(int)i2].Clone();
            var normal = (v1.GetPosition() - v0.GetPosition()).CrossProduct(v2.GetPosition() - v0.GetPosition()).Normalized;
            v0.Normal = normal.ToVector4();
            v1.Normal = normal.ToVector4();
            v2.Normal = normal.ToVector4();
            var newIndex = (uint)newVertices.Count;
            newVertices.Add(v0);
            newVertices.Add(v1);
            newVertices.Add(v2);
            if (!oldToNewVertex.TryGetValue(i0, out var newIndices0))
                oldToNewVertex.Add(i0, newIndices0 = []);
            newIndices0.Add(newIndex);
            if (!oldToNewVertex.TryGetValue(i1, out var newIndices1))
                oldToNewVertex.Add(i1, newIndices1 = []);
            newIndices1.Add(newIndex + 1);
            if (!oldToNewVertex.TryGetValue(i2, out var newIndices2))
                oldToNewVertex.Add(i2, newIndices2 = []);
            newIndices2.Add(newIndex + 2);
        });
        PrimitiveType = PrimitiveType.TriangleList;
        Vertices = newVertices;
        Indices = new List<uint>(Vertices.Count);
        for (uint i = 0; i < Vertices.Count; ++i)
            Indices.Add(i);

        foreach (var morphIndex in Morphs.Keys.ToArray())
        {
            var newMorphVector = new ModelVertexMorphVector();
            foreach (var vertexMorph in Morphs[morphIndex])
            {
                if (!oldToNewVertex.TryGetValue(vertexMorph.Index, out var newIndices))
                    continue;
                foreach (var newIndex in newIndices)
                {
                    var newVertexMorph = vertexMorph;
                    newVertexMorph.Index = newIndex;
                    newMorphVector.Add(newVertexMorph);
                }
            }
            Morphs[morphIndex] = newMorphVector;
        }
    }

    public void RecalculateSmoothNormals()
    {
        if (!IsTriangleGeometry())
            return;
        InvalidateNormalsAndTangents();
        var normals = new Vector3f[Vertices.Count];
        var sourceVertices = Vertices;
        ForEachTriangle((i0, i1, i2) =>
        {
            var p0 = sourceVertices[(int)i0].GetPosition();
            var p1 = sourceVertices[(int)i1].GetPosition();
            var p2 = sourceVertices[(int)i2].GetPosition();
            var normal = (p1 - p0).CrossProduct(p2 - p0);
            normals[(int)i0] += normal;
            normals[(int)i1] += normal;
            normals[(int)i2] += normal;
        });
        for (var i = 0; i < Vertices.Count; ++i)
        {
            var vertex = Vertices[i];
            vertex.Normal = normals[i].Normalized.ToVector4();
            Vertices[i] = vertex;
        }
    }

    public void RecalculateTangents()
    {
        if (!IsTriangleGeometry())
        {
            System.Diagnostics.Debug.Assert(false);
            return;
        }

        var vertexElements = ModelVertex.VertexElements;
        var elementCount = vertexElements.Length;
        var unpackedData = new Vector4f[checked(Vertices.Count * elementCount)];
        for (var i = 0; i < Vertices.Count; ++i)
            Vertices[i].Data().CopyTo(unpackedData, i * elementCount);

        using var vertexBuffer = new VertexBuffer(DeviceObjectFlag.Headless);
        vertexBuffer.SetShadowed(true);
        vertexBuffer.SetSize((uint)Vertices.Count, vertexElements);
        vertexBuffer.SetUnpackedData(unpackedData);

        var vertexData = vertexBuffer.GetShadowData();

        var indexData = new byte[checked(Indices.Count * sizeof(uint))];
        Buffer.BlockCopy(Indices.ToArray(), 0, indexData, 0, indexData.Length);
        var actualVertexElements = vertexBuffer.GetElements();
        var normalOffset = VertexBuffer.GetElementOffset(actualVertexElements, VertexElementType.Vector4,
            VertexElementSemantic.Normal);
        var texCoordOffset = VertexBuffer.GetElementOffset(actualVertexElements, VertexElementType.Vector4,
            VertexElementSemantic.TexCoord);
        var tangentOffset = VertexBuffer.GetElementOffset(actualVertexElements, VertexElementType.Vector4,
            VertexElementSemantic.Tangent);
        Tangent.GenerateTangents(vertexData, vertexBuffer.GetVertexSize(), indexData, sizeof(uint), 0,
            (uint)Indices.Count, normalOffset, texCoordOffset, tangentOffset);

        var updatedData = vertexBuffer.GetUnpackedData();
        for (var i = 0; i < Vertices.Count; ++i)
        {
            var vertex = Vertices[i];
            vertex.Tangent = updatedData[i * elementCount + 2];
            Vertices[i] = vertex;
        }
    }

    /// <summary>Iterate all triangles in primitive.</summary>
    public void ForEachTriangle(Action<uint, uint, uint> callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        if (!IsTriangleGeometry())
            return;
        var count = GetNumPrimitives();
        switch (PrimitiveType)
        {
            case PrimitiveType.TriangleList:
                for (uint i = 0; i < count; ++i)
                    callback(Indices[(int)(i * 3)], Indices[(int)(i * 3 + 1)], Indices[(int)(i * 3 + 2)]);
                break;
            case PrimitiveType.TriangleStrip:
                for (uint i = 0; i < count; ++i)
                {
                    if (i % 2 == 0)
                        callback(Indices[(int)i], Indices[(int)i + 1], Indices[(int)i + 2]);
                    else
                        callback(Indices[(int)i], Indices[(int)i + 2], Indices[(int)i + 1]);
                }
                break;
            case PrimitiveType.TriangleFan:
                for (uint i = 0; i < count; ++i)
                    callback(Indices[0], Indices[(int)i + 1], Indices[(int)i + 2]);
                break;
        }
    }

    public bool Equals(GeometryLODView rhs) => Vertices.SequenceEqual(rhs.Vertices) && Indices.SequenceEqual(rhs.Indices)
        && LodDistance == rhs.LodDistance && VertexFormat == rhs.VertexFormat
        && Morphs.Count == rhs.Morphs.Count && Morphs.All(pair => rhs.Morphs.TryGetValue(pair.Key, out var value) && pair.Value.SequenceEqual(value));

    public override bool Equals(object? obj) => obj is GeometryLODView rhs && Equals(rhs);
    public override int GetHashCode() => HashCode.Combine(LodDistance, VertexFormat);
    public static bool operator ==(GeometryLODView lhs, GeometryLODView rhs) => lhs.Equals(rhs);
    public static bool operator !=(GeometryLODView lhs, GeometryLODView rhs) => !lhs.Equals(rhs);

}

/// <summary>Model geometry, unpacked for easy editing.</summary>
public struct GeometryView : IEquatable<GeometryView>, ICloneable
{
    public List<GeometryLODView> Lods;
    public string Material;
    public bool Exported;

    public GeometryView()
    {
        Lods = [];
        Material = string.Empty;
        Exported = true;
    }

    public readonly GeometryView Clone()
    {
        var clone = this;
        clone.Lods = Lods.Select(static lod => lod.Clone()).ToList();
        return clone;
    }

    readonly object ICloneable.Clone() => Clone();

    public uint CalculateNumMorphs()
    {
        uint result = 0;
        foreach (var lod in Lods)
            result = Math.Max(result, lod.CalculateNumMorphs());
        return result;
    }

    public void Normalize()
    {
        for (var i = 0; i < Lods.Count; ++i)
        {
            var normalizedLod = Lods[i];
            normalizedLod.Normalize();
            Lods[i] = normalizedLod;
        }
    }

    public bool Equals(GeometryView rhs) => Material == rhs.Material && Lods.SequenceEqual(rhs.Lods);
    public override bool Equals(object? obj) => obj is GeometryView rhs && Equals(rhs);
    public override int GetHashCode() => HashCode.Combine(Material, Lods.Count);
    public static bool operator ==(GeometryView lhs, GeometryView rhs) => lhs.Equals(rhs);
    public static bool operator !=(GeometryView lhs, GeometryView rhs) => !lhs.Equals(rhs);
}

/// <summary>Bone of Model skeleton, unpacked for easy editing.</summary>
public struct BoneView : IEquatable<BoneView>
{
    public string Name;
    public int ParentIndex;
    public Vector3f Position;
    public Quaternion Rotation;
    public Vector3f Scale;
    public Matrix3x4 OffsetMatrix;
    public BoneCollisionShape ShapeFlags;
    public float BoundingSphereRadius;
    public BoundingBox LocalBoundingBox;

    public BoneView()
    {
        Name = string.Empty;
        ParentIndex = -1;
        Position = Vector3f.Zero;
        Rotation = Quaternion.IDENTITY;
        Scale = Vector3f.One;
        OffsetMatrix = Matrix3x4.Identity;
        ShapeFlags = BoneCollisionShape.None;
        BoundingSphereRadius = 0;
        LocalBoundingBox = new BoundingBox();
    }

    /// <summary>Set initial bone transform. Does not change offset matrix.</summary>
    public void SetInitialTransform(Vector3f position, Quaternion rotation, Vector3f scale)
    {
        Position = position;
        Rotation = rotation;
        Scale = scale;
    }

    /// <summary>Recalculate offset matrix from initial bone transform.</summary>
    public void RecalculateOffsetMatrix() => OffsetMatrix = new Matrix3x4(Position, Rotation, Scale).Inverse;

    /// <summary>Reset bounding volume.</summary>
    public void ResetBoundingVolume() => ShapeFlags = BoneCollisionShape.None;

    /// <summary>Reset bounding volume to local bounding box.</summary>
    public void SetLocalBoundingBox(BoundingBox boundingBox)
    {
        ShapeFlags = BoneCollisionShape.Box;
        LocalBoundingBox = boundingBox;
    }

    /// <summary>Reset bounding volume to local bounding sphere.</summary>
    public void SetLocalBoundingSphere(float radius)
    {
        ShapeFlags = BoneCollisionShape.Sphere;
        BoundingSphereRadius = radius;
    }

    public bool Equals(BoneView rhs) => Name == rhs.Name && ParentIndex == rhs.ParentIndex
        && Position.ApproximatelyEquivalent(rhs.Position) && Rotation.ApproximatelyEquivalent(rhs.Rotation)
        && Scale.ApproximatelyEquivalent(rhs.Scale) && OffsetMatrix.ApproximatelyEquivalent(rhs.OffsetMatrix)
        && ShapeFlags == rhs.ShapeFlags
        && MathDefs.Equals(BoundingSphereRadius, rhs.BoundingSphereRadius)
        && LocalBoundingBox.Min.ApproximatelyEquivalent(rhs.LocalBoundingBox.Min)
        && LocalBoundingBox.Max.ApproximatelyEquivalent(rhs.LocalBoundingBox.Max);
    public override bool Equals(object? obj) => obj is BoneView rhs && Equals(rhs);
    public override int GetHashCode() => HashCode.Combine(Name, ParentIndex, ShapeFlags);
    public static bool operator ==(BoneView lhs, BoneView rhs) => lhs.Equals(rhs);
    public static bool operator !=(BoneView lhs, BoneView rhs) => !lhs.Equals(rhs);
}

/// <summary>Represents metadata of model morph.</summary>
public struct ModelMorphView
{
    public string Name;
    public float InitialWeight;
}

/// <summary>Flags that control how ModelView is exported as Model.</summary>
[Flags]
public enum ModelViewExportFlag
{
    None = 0,
    Inplace = 1 << 0,
    Headless = 1 << 1
}

/// <summary>Represents Model in editable form.</summary>
public sealed class ModelView : RedBolt.Object
{
    private sealed class VertexBufferBuilder
    {
        public ModelVertexFormat VertexFormat = new();
        public readonly List<GeometryLODView> VertexSources = [];
        public uint NumVertices;

        // TODO: Remove suboptimal copying of morphs
        public readonly List<ModelVertexMorphVector> Morphs = [];
        public uint MorphRangeStart;
        public uint MorphRangeCount;

        public VertexBuffer? Buffer;
    }

    private sealed class IndexBufferBuilder
    {
        public bool LargeIndices;
        public readonly List<(GeometryLODView Geometry, uint BaseIndex)> IndexSources = [];
        public uint NumIndices;

        public IndexBuffer? Buffer;
    }

    private readonly record struct GeometryRange(uint VertexBuffer, uint StartVertex, uint StartIndex);

    private string _name = string.Empty;
    private List<GeometryView> _geometries = [];
    private List<BoneView> _bones = [];
    private List<ModelMorphView> _morphs = [];
    private readonly Dictionary<string, Variant> _metadata = [];

    public ModelView()
    {
    }

    public void Clear()
    {
        _geometries.Clear();
        _bones.Clear();
        foreach (var value in _metadata.Values)
            value.Dispose();
        _metadata.Clear();
    }

    public override void Dispose()
    {
        foreach (var value in _metadata.Values)
            value.Dispose();
        _metadata.Clear();
        _morphs.Clear();
        _bones.Clear();
        _geometries.Clear();
        base.Dispose();
    }

    /// <summary>Import and export from/to native Model.</summary>
    public bool ImportModel(Model? model)
    {
        Clear();

        // Read name
        _name = model!.Name;

        // Read metadata
        foreach (var key in model.GetMetadataKeys())
            _metadata.Add(key, model.GetMetadata(key).Clone());

        var modelVertexBuffers = model.GetVertexBuffers();
        var modelGeometries = model.GetGeometries();
        var modelMorphs = model.GetMorphs();

        // Read morphs metadata
        if (_morphs.Count > modelMorphs.Count)
            _morphs.RemoveRange(modelMorphs.Count, _morphs.Count - modelMorphs.Count);
        while (_morphs.Count < modelMorphs.Count)
            _morphs.Add(new ModelMorphView());
        for (var morphIndex = 0; morphIndex < modelMorphs.Count; ++morphIndex)
        {
            _morphs[morphIndex] = new ModelMorphView
            {
                Name = modelMorphs[morphIndex].Name,
                InitialWeight = modelMorphs[morphIndex].Weight
            };
        }

        // Read geometries
        for (var geometryIndex = 0; geometryIndex < modelGeometries.Count; ++geometryIndex)
        {
            var geometryView = new GeometryView();
            var modelGeometryLods = modelGeometries[geometryIndex];
            for (var lodIndex = 0; lodIndex < modelGeometryLods.Count; ++lodIndex)
            {
                var modelGeometry = modelGeometryLods[lodIndex]!;
                var geometry = new GeometryLODView
                {
                    LodDistance = modelGeometry.GetLodDistance(),
                    PrimitiveType = modelGeometry.GetPrimitiveType()
                };

                // Copy indices
                if (modelGeometry.GetIndexBuffer() is { } modelIndexBuffer)
                {
                    var numIndices = modelGeometry.GetIndexCount();
                    if (!CheckIndexCount(geometry.PrimitiveType, numIndices))
                    {
                        Log.GetLogger().LogError("Incorrect number of geometry indices");
                        return false;
                    }

                    geometry.Indices.AddRange(modelIndexBuffer.GetUnpackedData(
                        modelGeometry.GetIndexStart(), modelGeometry.GetIndexCount()));
                }
                else
                {
                    var numIndices = modelGeometry.GetVertexCount();
                    if (!CheckIndexCount(geometry.PrimitiveType, numIndices))
                    {
                        Log.GetLogger().LogError("Incorrect number of geometry vertices");
                        return false;
                    }

                    for (var i = 0u; i < numIndices; ++i)
                        geometry.Indices.Add(i);
                }

                // Adjust indices
                for (var i = 0; i < geometry.Indices.Count; ++i)
                    geometry.Indices[i] -= modelGeometry.GetVertexStart();

                // Copy vertices and read vertex format
                var vertexStart = modelGeometry.GetVertexStart();
                var vertexCount = modelGeometry.GetVertexCount();
                for (var i = 0u; i < vertexCount; ++i)
                    geometry.Vertices.Add(new ModelVertex());

                foreach (var modelVertexBuffer in modelGeometry.GetVertexBuffers())
                {
                    if (modelVertexBuffer is null)
                        continue;
                    if (!CheckVertexElements(modelVertexBuffer.GetElements()))
                    {
                        Log.GetLogger().LogError("Unsupported vertex elements are present");
                        return false;
                    }

                    var vertexBufferData = GetVertexBufferData(modelVertexBuffer, vertexStart, vertexCount);
                    var vertexElements = modelVertexBuffer.GetElements();
                    for (var i = 0; i < vertexBufferData.Count; ++i)
                    {
                        var vertex = geometry.Vertices[i];
                        foreach (var element in vertexElements)
                            vertex.ReplaceElement(vertexBufferData[i], element);
                        geometry.Vertices[i] = vertex;
                    }

                    var vertexFormat = ParseVertexElements(vertexElements);
                    geometry.VertexFormat.MergeFrom(vertexFormat);

                    // Read morphs for this vertex buffer
                    for (var morphIndex = 0; morphIndex < modelMorphs.Count; ++morphIndex)
                    {
                        var modelMorph = modelMorphs[morphIndex];
                        var vertexBufferIndex = -1;
                        for (var i = 0; i < modelVertexBuffers.Count; ++i)
                        {
                            if (ReferenceEquals(modelVertexBuffers[i], modelVertexBuffer))
                            {
                                vertexBufferIndex = i;
                                break;
                            }
                        }
                        if (vertexBufferIndex < 0 || !modelMorph.Buffers.TryGetValue((uint)vertexBufferIndex, out var vertexBufferMorph))
                            continue;

                        if (!geometry.Morphs.TryGetValue((uint)morphIndex, out var morphData))
                            geometry.Morphs[(uint)morphIndex] = morphData = [];
                        for (var i = 0u; i < vertexBufferMorph.VertexCount; ++i)
                        {
                            var vertexMorph = ReadVertexMorph(vertexBufferMorph, i);
                            if (vertexMorph.Index >= vertexStart && vertexMorph.Index < vertexStart + vertexCount)
                                morphData.Add(vertexMorph);
                        }
                    }
                }

                // Cleanup morphs
                foreach (var morphIndex in geometry.Morphs.Keys.ToArray())
                {
                    var morphVector = geometry.Morphs[morphIndex];
                    morphVector.Sort((lhs, rhs) => lhs.Index.CompareTo(rhs.Index));
                    var newMorphVector = new ModelVertexMorphVector();
                    foreach (var vertexMorph in morphVector)
                    {
                        if (newMorphVector.Count != 0 && newMorphVector[^1].Index == vertexMorph.Index)
                        {
                            var previous = newMorphVector[^1];
                            if (vertexMorph.HasPosition())
                                previous.PositionDelta = vertexMorph.PositionDelta;
                            if (vertexMorph.HasNormal())
                                previous.NormalDelta = vertexMorph.NormalDelta;
                            if (vertexMorph.HasTangent())
                                previous.TangentDelta = vertexMorph.TangentDelta;
                            newMorphVector[^1] = previous;
                            continue;
                        }
                        newMorphVector.Add(vertexMorph);
                    }

                    for (var i = 0; i < newMorphVector.Count; ++i)
                    {
                        var vertexMorph = newMorphVector[i];
                        vertexMorph.Index -= vertexStart;
                        newMorphVector[i] = vertexMorph;
                    }

                    if (newMorphVector.Count == 0)
                        geometry.Morphs.Remove(morphIndex);
                    else
                        geometry.Morphs[morphIndex] = newMorphVector;
                }

                geometryView.Lods.Add(geometry);
            }
            _geometries.Add(geometryView);
        }

        // Read bones
        var modelBones = model.GetSkeleton().GetBones();
        var hasRootBone = false;
        for (var boneIndex = 0; boneIndex < modelBones.Count; ++boneIndex)
        {
            var modelBone = modelBones[boneIndex];
            var isRootBone = modelBone.ParentIndex == boneIndex;
            if (isRootBone)
            {
                if (hasRootBone)
                {
                    Log.GetLogger().LogError("Multiple root bones are present");
                    return false;
                }
                hasRootBone = true;
            }

            _bones.Add(new BoneView
            {
                Name = modelBone.Name,
                ParentIndex = isRootBone ? -1 : modelBone.ParentIndex,
                Position = modelBone.InitialPosition,
                Rotation = modelBone.InitialRotation,
                Scale = modelBone.InitialScale,
                OffsetMatrix = modelBone.OffsetMatrix,
                ShapeFlags = modelBone.CollisionMask,
                BoundingSphereRadius = modelBone.Radius,
                LocalBoundingBox = modelBone.BoundingBox
            });
        }

        return true;
    }

    /// <summary>Export into a native Model.</summary>
    public void ExportModel(Model model, ModelViewExportFlag flags = ModelViewExportFlag.None)
    {
        var inplace = (flags & ModelViewExportFlag.Inplace) != 0;

        // Prepare buffer layout
        var vertexBufferBuilders = new List<VertexBufferBuilder>();
        var indexBufferBuilder = new IndexBufferBuilder();
        var geometryRanges = new Dictionary<(int Geometry, int Lod), GeometryRange>();
        CreateGeometryBufferBuilders(_geometries, vertexBufferBuilders, indexBufferBuilder, geometryRanges, inplace);

        // Create buffers or connect to existing buffers
        if (inplace)
            ConnectToModelBuffers(model, vertexBufferBuilders, indexBufferBuilder);
        else
            AllocateNewBuffers(vertexBufferBuilders, indexBufferBuilder,
                (flags & ModelViewExportFlag.Headless) != 0);

        for (var i = 0; i < vertexBufferBuilders.Count; ++i)
            vertexBufferBuilders[i].Buffer?.SetDebugName($"Model '{_name}' Vertex Buffer {i}");
        indexBufferBuilder.Buffer?.SetDebugName($"Model '{_name}' Index Buffer");

        // Copy data
        foreach (var vertexBufferBuilder in vertexBufferBuilders)
            ComposeVertexBufferData(vertexBufferBuilder);
        ComposeIndexBufferData(indexBufferBuilder);

        // Initialize morph info
        foreach (var vertexBufferBuilder in vertexBufferBuilders)
            CalculateMorphRange(vertexBufferBuilder);

        // Extract vertex buffers info
        var vertexBuffers = vertexBufferBuilders.Select(builder => builder.Buffer!).ToList();
        var morphRangeStarts = vertexBufferBuilders.Select(builder => builder.MorphRangeStart).ToList();
        var morphRangeCounts = vertexBufferBuilders.Select(builder => builder.MorphRangeCount).ToList();

        // Inplace mode: preserve unused vertex buffers
        if (inplace)
        {
            vertexBuffers = model.GetVertexBuffers().ToList();
            while (morphRangeStarts.Count < vertexBuffers.Count)
                morphRangeStarts.Add(0);
            while (morphRangeCounts.Count < vertexBuffers.Count)
                morphRangeCounts.Add(0);
        }

        // Create morphs
        var morphs = _morphs.Select(morph => new ModelMorph
        {
            Name = morph.Name,
            NameHash = new StringHash(morph.Name),
            Weight = morph.InitialWeight
        }).ToList();

        for (var i = 0; i < vertexBufferBuilders.Count; ++i)
        {
            var builderMorphs = vertexBufferBuilders[i].Morphs;
            while (morphs.Count < builderMorphs.Count)
                morphs.Add(new ModelMorph());
            for (var j = 0; j < builderMorphs.Count; ++j)
            {
                var vertexBufferMorph = CreateVertexBufferMorph(builderMorphs[j]);
                if (vertexBufferMorph.VertexCount > 0)
                    morphs[j].Buffers[(uint)i] = vertexBufferMorph;
            }
        }

        // Create model
        model.SetName(_name);
        foreach (var pair in _metadata)
            model.AddMetadata(pair.Key, pair.Value);
        model.SetBoundingBox(CalculateBoundingBox());
        model.SetVertexBuffers(vertexBuffers, morphRangeStarts, morphRangeCounts);
        if (indexBufferBuilder.Buffer is not null)
            model.SetIndexBuffers([indexBufferBuilder.Buffer]);
        model.SetMorphs(morphs);

        // Write geometries
        var exportedGeometries = Enumerable.Range(0, _geometries.Count).Where(i => _geometries[i].Exported).ToList();
        model.SetNumGeometries((uint)exportedGeometries.Count);
        for (var geometryIndex = 0; geometryIndex < exportedGeometries.Count; ++geometryIndex)
        {
            var sourceGeometryIndex = exportedGeometries[geometryIndex];
            var sourceGeometry = _geometries[sourceGeometryIndex];
            if (sourceGeometry.Lods.Count == 0)
                continue;

            model.SetGeometryCenter((uint)geometryIndex, sourceGeometry.Lods[0].CalculateCenter());
            model.SetNumGeometryLodLevels((uint)geometryIndex, (uint)sourceGeometry.Lods.Count);
            for (var lodIndex = 0; lodIndex < sourceGeometry.Lods.Count; ++lodIndex)
            {
                var sourceGeometryLod = sourceGeometry.Lods[lodIndex];
                var geometry = new Geometry();
                geometry.SetLodDistance(sourceGeometryLod.LodDistance);
                if (geometryRanges.TryGetValue((sourceGeometryIndex, lodIndex), out var range))
                {
                    geometry.SetNumVertexBuffers(1);
                    geometry.SetVertexBuffer(0, vertexBufferBuilders[(int)range.VertexBuffer].Buffer);
                    geometry.SetIndexBuffer(indexBufferBuilder.Buffer);
                    geometry.SetDrawRange(sourceGeometryLod.PrimitiveType, range.StartIndex,
                        (uint)sourceGeometryLod.Indices.Count, range.StartVertex,
                        (uint)sourceGeometryLod.Vertices.Count);
                }
                model.SetGeometry((uint)geometryIndex, (uint)lodIndex, geometry);
            }
        }

        // Write bones
        var skeleton = new Skeleton();
        skeleton.SetNumBones(_bones.Count);
        var skeletonBones = skeleton.GetModifiableBones();
        for (var boneIndex = 0; boneIndex < _bones.Count; ++boneIndex)
        {
            var sourceBone = _bones[boneIndex];
            var isRootBone = sourceBone.ParentIndex == -1;
            var bone = skeletonBones[boneIndex];
            bone.Name = sourceBone.Name;
            bone.NameHash = new StringHash(sourceBone.Name);
            bone.ParentIndex = isRootBone ? boneIndex : sourceBone.ParentIndex;
            bone.InitialPosition = sourceBone.Position;
            bone.InitialRotation = sourceBone.Rotation;
            bone.InitialScale = sourceBone.Scale;
            bone.OffsetMatrix = sourceBone.OffsetMatrix;
            bone.CollisionMask = sourceBone.ShapeFlags;
            bone.Radius = sourceBone.BoundingSphereRadius;
            bone.BoundingBox = sourceBone.LocalBoundingBox;
            if (isRootBone)
                skeleton.SetRootBoneIndex(boneIndex);
        }
        skeleton.UpdateBoneOrder();
        model.SetSkeleton(skeleton);
    }

    /// <summary>Export into a new native Model.</summary>
    public Model ExportModel(ModelViewExportFlag flags = ModelViewExportFlag.None, string name = "")
    {
        if ((flags & ModelViewExportFlag.Inplace) != 0)
        {
            Log.GetLogger().LogWarning("ModelViewExportFlag.Inplace is ignored: Model is not provided to ModelView.ExportModel");
            flags &= ~ModelViewExportFlag.Inplace;
        }

        var model = new Model();
        ExportModel(model, flags);
        if (!string.IsNullOrEmpty(name))
            model.SetName(name);
        return model;
    }

    /// <summary>Export material references.</summary>
    public ResourceRefList ExportMaterialList()
    {
        var names = new List<string>();
        foreach (var geometry in _geometries)
        {
            if (geometry.Exported)
                names.Add(geometry.Material);
        }
        return new ResourceRefList("Material", names);
    }

    /// <summary>Calculate bounding box.</summary>
    public BoundingBox CalculateBoundingBox(bool exportedOnly = true)
    {
        var result = new BoundingBox();
        foreach (var geometry in _geometries)
        {
            if (exportedOnly && !geometry.Exported)
                continue;
            foreach (var lod in geometry.Lods)
            {
                foreach (var vertex in lod.Vertices)
                    result.Merge(vertex.Position.ToVector3());
            }
        }
        if (!result.Defined)
            result.Merge(Vector3f.Zero);
        return result;
    }

    /// <summary>All equivalent views should be literally equal after normalization.</summary>
    public void Normalize()
    {
        for (var i = 0; i < _geometries.Count; ++i)
        {
            var geometry = _geometries[i];
            geometry.Normalize();
            _geometries[i] = geometry;
        }
        uint numMorphs = 0;
        foreach (var geometry in _geometries)
            numMorphs = Math.Max(numMorphs, geometry.CalculateNumMorphs());
        while (_morphs.Count < numMorphs)
            _morphs.Add(new ModelMorphView());
    }

    /// <summary>Mirror geometries along X axis.</summary>
    public void MirrorGeometriesX()
    {
        for (var geometryIndex = 0; geometryIndex < _geometries.Count; ++geometryIndex)
        {
            var geometry = _geometries[geometryIndex];
            for (var lodIndex = 0; lodIndex < geometry.Lods.Count; ++lodIndex)
            {
                var lod = geometry.Lods[lodIndex];
                for (var i = 0; i < lod.Vertices.Count; ++i)
                {
                    var vertex = lod.Vertices[i];
                    vertex.Position.X = -vertex.Position.X;
                    vertex.Normal.X = -vertex.Normal.X;
                    vertex.Tangent.X = -vertex.Tangent.X;
                    lod.Vertices[i] = vertex;
                }
                foreach (var morphs in lod.Morphs.Values)
                {
                    for (var i = 0; i < morphs.Count; ++i)
                    {
                        var morph = morphs[i];
                        morph.PositionDelta.X = -morph.PositionDelta.X;
                        morph.NormalDelta.X = -morph.NormalDelta.X;
                        morph.TangentDelta.X = -morph.TangentDelta.X;
                        morphs[i] = morph;
                    }
                }
                if (lod.IsTriangleGeometry())
                {
                    switch (lod.PrimitiveType)
                    {
                        case PrimitiveType.TriangleList:
                            for (var i = 0u; i < lod.GetNumPrimitives(); ++i)
                                (lod.Indices[(int)(i * 3 + 1)], lod.Indices[(int)(i * 3 + 2)]) =
                                    (lod.Indices[(int)(i * 3 + 2)], lod.Indices[(int)(i * 3 + 1)]);
                            break;
                        case PrimitiveType.TriangleStrip:
                            if (lod.GetNumPrimitives() > 0 && lod.GetNumPrimitives() % 2 == 0)
                                lod.Indices.Add(lod.Indices[^1]);
                            lod.Indices.Reverse();
                            break;
                        case PrimitiveType.TriangleFan:
                            if (lod.GetNumPrimitives() >= 1)
                                lod.Indices.Reverse(1, lod.Indices.Count - 1);
                            break;
                    }
                }
                geometry.Lods[lodIndex] = lod;
            }
            _geometries[geometryIndex] = geometry;
        }
    }

    /// <summary>Scale geometries.</summary>
    public void ScaleGeometries(float scale)
    {
        for (var geometryIndex = 0; geometryIndex < _geometries.Count; ++geometryIndex)
        {
            var geometry = _geometries[geometryIndex];
            for (var lodIndex = 0; lodIndex < geometry.Lods.Count; ++lodIndex)
            {
                var lod = geometry.Lods[lodIndex];
                for (var i = 0; i < lod.Vertices.Count; ++i)
                {
                    var vertex = lod.Vertices[i];
                    vertex.Position = (scale * vertex.GetPosition()).ToVector4(vertex.Position.W);
                    lod.Vertices[i] = vertex;
                }
                foreach (var morphs in lod.Morphs.Values)
                {
                    for (var i = 0; i < morphs.Count; ++i)
                    {
                        var morph = morphs[i];
                        morph.PositionDelta *= scale;
                        morphs[i] = morph;
                    }
                }
                geometry.Lods[lodIndex] = lod;
            }
            _geometries[geometryIndex] = geometry;
        }
    }

    /// <summary>Calculate normals for geometries without normals in vertex format.</summary>
    public void CalculateMissingNormals(bool flatNormals = false)
    {
        for (var geometryIndex = 0; geometryIndex < _geometries.Count; ++geometryIndex)
        {
            var geometry = _geometries[geometryIndex];
            for (var lodIndex = 0; lodIndex < geometry.Lods.Count; ++lodIndex)
            {
                var lod = geometry.Lods[lodIndex];
                if (!lod.IsTriangleGeometry() || lod.VertexFormat.Normal != ModelVertexFormat.Undefined)
                    continue;
                lod.VertexFormat.Normal = VertexElementType.Vector3;
                lod.VertexFormat.Tangent = ModelVertexFormat.Undefined;
                if (flatNormals)
                    lod.RecalculateFlatNormals();
                else
                    lod.RecalculateSmoothNormals();
                geometry.Lods[lodIndex] = lod;
            }
            _geometries[geometryIndex] = geometry;
        }
    }

    /// <summary>Calculate tangents for geometries without tangents in vertex format.</summary>
    public void CalculateMissingTangents()
    {
        for (var geometryIndex = 0; geometryIndex < _geometries.Count; ++geometryIndex)
        {
            var geometry = _geometries[geometryIndex];
            for (var lodIndex = 0; lodIndex < geometry.Lods.Count; ++lodIndex)
            {
                var lod = geometry.Lods[lodIndex];
                if (!lod.IsTriangleGeometry() || lod.VertexFormat.Tangent != ModelVertexFormat.Undefined)
                    continue;
                lod.VertexFormat.Tangent = VertexElementType.Vector4;
                lod.RecalculateTangents();
                geometry.Lods[lodIndex] = lod;
            }
            _geometries[geometryIndex] = geometry;
        }
    }

    /// <summary>Normalize bone weights and cleanup invalid bones.</summary>
    public void RepairBoneWeights()
    {
        if (_bones.Count == 0)
            return;
        foreach (var geometry in _geometries)
        {
            foreach (var lod in geometry.Lods)
            {
                for (var vertexIndex = 0; vertexIndex < lod.Vertices.Count; ++vertexIndex)
                {
                    var vertex = lod.Vertices[vertexIndex];
                    for (var i = 0; i < ModelVertex.MaxBones; ++i)
                    {
                        var index = vertex.BlendIndices[i];
                        var weight = vertex.BlendWeights[i];
                        if (index < 0 || index >= _bones.Count || weight < 0)
                        {
                            vertex.BlendIndices[i] = 0;
                            vertex.BlendWeights[i] = 0;
                        }
                    }
                    var weightSum = vertex.BlendWeights.DotProduct(Vector4f.One);
                    if (!MathDefs.Equals(weightSum, 1.0f))
                    {
                        if (weightSum < MathDefs.Epsilon)
                        {
                            vertex.BlendIndices = Vector4f.Zero;
                            vertex.BlendWeights = new Vector4f(1, 0, 0, 0);
                        }
                        else
                            vertex.BlendWeights /= weightSum;
                    }
                    lod.Vertices[vertexIndex] = vertex;
                }
            }
        }
    }

    /// <summary>Recalculate bounding boxes for bones.</summary>
    public void RecalculateBoneBoundingBoxes(bool exportedOnly = true)
    {
        if (_bones.Count == 0)
            return;
        for (var i = 0; i < _bones.Count; ++i)
        {
            var bone = _bones[i];
            bone.SetLocalBoundingBox(new BoundingBox());
            _bones[i] = bone;
        }
        foreach (var geometry in _geometries)
        {
            if (exportedOnly && !geometry.Exported)
                continue;
            foreach (var lod in geometry.Lods)
            {
                foreach (var vertex in lod.Vertices)
                {
                    foreach (var (boneIndex, boneWeight) in vertex.GetBlendIndicesAndWeights())
                    {
                        if (boneIndex >= _bones.Count || boneWeight < MathDefs.LargeEpsilon)
                            continue;
                        var bone = _bones[(int)boneIndex];
                        bone.LocalBoundingBox.Merge(bone.OffsetMatrix * vertex.GetPosition());
                        _bones[(int)boneIndex] = bone;
                    }
                }
            }
        }
        for (var i = 0; i < _bones.Count; ++i)
        {
            var bone = _bones[i];
            if (!bone.LocalBoundingBox.Defined)
                bone.ResetBoundingVolume();
            _bones[i] = bone;
        }
    }

    /// <summary>Read vertex buffer data.</summary>
    private static List<ModelVertex> GetVertexBufferData(VertexBuffer vertexBuffer, uint start, uint count)
    {
        var unpackedData = vertexBuffer.GetUnpackedData(start, count);
        var shuffledData = new Vector4f[checked((int)(count * (uint)ModelVertex.VertexElements.Length))];
        VertexBuffer.ShuffleUnpackedVertexData(count, unpackedData.ToArray(), vertexBuffer.GetElements(),
            shuffledData, ModelVertex.VertexElements);

        var result = new List<ModelVertex>(checked((int)count));
        for (var i = 0; i < count; ++i)
        {
            var vertex = new ModelVertex();
            var data = shuffledData.AsSpan(checked((int)(i * (uint)ModelVertex.VertexElements.Length)),
                ModelVertex.VertexElements.Length);
            vertex.Position = data[0];
            vertex.Normal = data[1];
            vertex.Tangent = data[2];
            vertex.Binormal = data[3];
            vertex.BlendIndices = data[4];
            vertex.BlendWeights = data[5];
            for (var j = 0; j < ModelVertex.MaxColors; ++j)
                vertex.Color[j] = data[6 + j];
            for (var j = 0; j < ModelVertex.MaxUVs; ++j)
                vertex.Uv[j] = data[10 + j];
            result.Add(vertex);
        }
        return result;
    }

    /// <summary>Parse vertex elements into simpler format.</summary>
    private static ModelVertexFormat ParseVertexElements(IReadOnlyList<VertexElement> elements)
    {
        var result = new ModelVertexFormat();
        foreach (var element in elements)
        {
            switch (element.Semantic)
            {
                case VertexElementSemantic.Position:
                    result.Position = element.Type;
                    break;
                case VertexElementSemantic.Normal:
                    result.Normal = element.Type;
                    break;
                case VertexElementSemantic.Binormal:
                    result.Binormal = element.Type;
                    break;
                case VertexElementSemantic.Tangent:
                    result.Tangent = element.Type;
                    break;
                case VertexElementSemantic.TexCoord when element.Index < ModelVertex.MaxUVs:
                    result.Uv[element.Index] = element.Type;
                    break;
                case VertexElementSemantic.Color when element.Index < ModelVertex.MaxColors:
                    result.Color[element.Index] = element.Type;
                    break;
                case VertexElementSemantic.BlendWeights:
                    result.BlendWeights = element.Type;
                    break;
                case VertexElementSemantic.BlendIndices:
                    result.BlendIndices = element.Type;
                    break;
            }
        }
        return result;
    }

    /// <summary>Check if vertex elements can be imported into ModelVertex.</summary>
    private static bool CheckVertexElements(IReadOnlyList<VertexElement> elements)
    {
        foreach (var element in elements)
        {
            if (element.Semantic == VertexElementSemantic.ObjectIndex)
                return false;
            if (element.Semantic == VertexElementSemantic.Color)
            {
                if (element.Index >= ModelVertex.MaxColors)
                    return false;
            }
            else if (element.Semantic == VertexElementSemantic.TexCoord)
            {
                if (element.Index >= ModelVertex.MaxUVs)
                    return false;
            }
            else if (element.Index > 0)
                return false;
        }
        return true;
    }

    private static bool CheckIndexCount(PrimitiveType primitiveType, uint count)
    {
        return primitiveType switch
        {
            PrimitiveType.TriangleList => count % 3 == 0,
            PrimitiveType.LineList => count % 2 == 0,
            PrimitiveType.PointList => true,
            PrimitiveType.TriangleStrip or PrimitiveType.TriangleFan => count == 0 || count >= 3,
            PrimitiveType.LineStrip => count == 0 || count >= 2,
            _ => false
        };
    }

    private static VertexBufferMorph CreateVertexBufferMorph(ModelVertexMorphVector morphVector)
    {
        morphVector = [.. morphVector];
        ModelViewFunctions.NormalizeModelVertexMorphVector(morphVector);
        var hasPosition = morphVector.Any(morph => morph.HasPosition());
        var hasNormal = morphVector.Any(morph => morph.HasNormal());
        var hasTangent = morphVector.Any(morph => morph.HasTangent());
        var result = new VertexBufferMorph();
        if (!hasPosition && !hasNormal && !hasTangent)
            return result;

        var stride = sizeof(uint);
        if (hasPosition)
        {
            result.ElementMask |= VertexMask.Position;
            stride += sizeof(float) * 3;
        }
        if (hasNormal)
        {
            result.ElementMask |= VertexMask.Normal;
            stride += sizeof(float) * 3;
        }
        if (hasTangent)
        {
            result.ElementMask |= VertexMask.Tangent;
            stride += sizeof(float) * 3;
        }

        result.VertexCount = (uint)morphVector.Count;
        result.DataSize = checked(result.VertexCount * (uint)stride);
        result.MorphData = new byte[checked((int)result.DataSize)];
        for (var i = 0; i < morphVector.Count; ++i)
        {
            var source = morphVector[i];
            var destination = result.MorphData.AsSpan(i * stride, stride);
            BinaryPrimitives.WriteUInt32LittleEndian(destination, source.Index);
            var offset = sizeof(uint);
            if (hasPosition)
            {
                MemoryMarshal.Write(destination[offset..], in source.PositionDelta);
                offset += sizeof(float) * 3;
            }
            if (hasNormal)
            {
                MemoryMarshal.Write(destination[offset..], in source.NormalDelta);
                offset += sizeof(float) * 3;
            }
            if (hasTangent)
                MemoryMarshal.Write(destination[offset..], in source.TangentDelta);
        }
        return result;
    }

    private static ModelVertexMorph ReadVertexMorph(VertexBufferMorph vertexBufferMorph, uint index)
    {
        var stride = sizeof(uint);
        if ((vertexBufferMorph.ElementMask & VertexMask.Position) != 0)
            stride += sizeof(float) * 3;
        if ((vertexBufferMorph.ElementMask & VertexMask.Normal) != 0)
            stride += sizeof(float) * 3;
        if ((vertexBufferMorph.ElementMask & VertexMask.Tangent) != 0)
            stride += sizeof(float) * 3;

        var source = vertexBufferMorph.MorphData.AsSpan(checked((int)index * stride), stride);
        var result = new ModelVertexMorph { Index = BinaryPrimitives.ReadUInt32LittleEndian(source) };
        var offset = sizeof(uint);
        if ((vertexBufferMorph.ElementMask & VertexMask.Position) != 0)
        {
            result.PositionDelta = MemoryMarshal.Read<Vector3f>(source[offset..]);
            offset += sizeof(float) * 3;
        }
        if ((vertexBufferMorph.ElementMask & VertexMask.Normal) != 0)
        {
            result.NormalDelta = MemoryMarshal.Read<Vector3f>(source[offset..]);
            offset += sizeof(float) * 3;
        }
        if ((vertexBufferMorph.ElementMask & VertexMask.Tangent) != 0)
            result.TangentDelta = MemoryMarshal.Read<Vector3f>(source[offset..]);
        return result;
    }

    private static uint GetMatchingVertexBuffer(List<VertexBufferBuilder> vertexBuffers, ModelVertexFormat vertexFormat)
    {
        for (var i = 0; i < vertexBuffers.Count; ++i)
        {
            if (vertexBuffers[i].VertexFormat == vertexFormat)
                return (uint)i;
        }
        vertexBuffers.Add(new VertexBufferBuilder { VertexFormat = vertexFormat.Clone() });
        return (uint)vertexBuffers.Count - 1;
    }

    private static void CreateGeometryBufferBuilders(IReadOnlyList<GeometryView> geometries,
        List<VertexBufferBuilder> vertexBufferBuilders, IndexBufferBuilder indexBufferBuilder,
        Dictionary<(int Geometry, int Lod), GeometryRange> geometryRanges, bool isInplace)
    {
        for (var geometryIndex = 0; geometryIndex < geometries.Count; ++geometryIndex)
        {
            var sourceGeometry = geometries[geometryIndex];
            if (!sourceGeometry.Exported)
                continue;
            for (var lodIndex = 0; lodIndex < sourceGeometry.Lods.Count; ++lodIndex)
            {
                var sourceGeometryLod = sourceGeometry.Lods[lodIndex];
                // Keep empty geometries when building inplace to have predictable vertex buffer layout.
                if (!isInplace && (sourceGeometryLod.Vertices.Count == 0 || sourceGeometryLod.Indices.Count == 0))
                    continue;

                var vertexBufferIndex = GetMatchingVertexBuffer(vertexBufferBuilders, sourceGeometryLod.VertexFormat);
                var vertexBufferBuilder = vertexBufferBuilders[(int)vertexBufferIndex];
                var startVertex = vertexBufferBuilder.NumVertices;
                var startIndex = indexBufferBuilder.NumIndices;
                geometryRanges[(geometryIndex, lodIndex)] = new GeometryRange(vertexBufferIndex, startVertex, startIndex);
                vertexBufferBuilder.VertexSources.Add(sourceGeometryLod);
                indexBufferBuilder.IndexSources.Add((sourceGeometryLod, startVertex));
                vertexBufferBuilder.NumVertices += (uint)sourceGeometryLod.Vertices.Count;
                indexBufferBuilder.NumIndices += (uint)sourceGeometryLod.Indices.Count;
                var largestIndex = sourceGeometryLod.Indices.Count == 0 ? 0u : sourceGeometryLod.Indices.Max();
                indexBufferBuilder.LargeIndices |= startVertex + largestIndex >= 0xffff;

                foreach (var pair in sourceGeometryLod.Morphs)
                {
                    while (vertexBufferBuilder.Morphs.Count <= pair.Key)
                        vertexBufferBuilder.Morphs.Add([]);
                    var vertexBufferMorph = vertexBufferBuilder.Morphs[(int)pair.Key];
                    foreach (var sourceMorph in pair.Value)
                    {
                        var morph = sourceMorph;
                        morph.Index += startVertex;
                        vertexBufferMorph.Add(morph);
                    }
                }
            }
        }
    }

    private static void ConnectToModelBuffers(Model model, List<VertexBufferBuilder> vertexBufferBuilders,
        IndexBufferBuilder indexBufferBuilder)
    {
        var originalVertexBuffers = model.GetVertexBuffers();
        for (var i = 0; i < vertexBufferBuilders.Count; ++i)
        {
            var builder = vertexBufferBuilders[i];
            var original = i < originalVertexBuffers.Count ? originalVertexBuffers[i] : null;
            if (original is null)
            {
                Log.GetLogger().LogError("Cannot create Model inplace: Vertex Buffer {Index} is not found", i);
                return;
            }
            if (original.GetVertexCount() < builder.NumVertices)
            {
                Log.GetLogger().LogError("Cannot create Model inplace: Vertex Buffer {Index} has only {Actual} vertices and {Required} are required",
                    i, original.GetVertexCount(), builder.NumVertices);
                return;
            }
            if (!builder.VertexFormat.ToVertexElements().SequenceEqual(original.GetElements()))
            {
                Log.GetLogger().LogError("Cannot create Model inplace: Vertex Buffer {Index} elements don't match", i);
                return;
            }
            builder.Buffer = original;
        }

        var originalIndexBuffers = model.GetIndexBuffers();
        var originalIndexBuffer = originalIndexBuffers.Count != 0 ? originalIndexBuffers[0] : null;
        if (originalIndexBuffer is null)
        {
            Log.GetLogger().LogError("Cannot create Model inplace: Index Buffer is not found");
            return;
        }
        if (originalIndexBuffer.GetIndexCount() < indexBufferBuilder.NumIndices)
        {
            Log.GetLogger().LogError("Cannot create Model inplace: Index Buffer has only {Actual} indices and {Required} are required",
                originalIndexBuffer.GetIndexCount(), indexBufferBuilder.NumIndices);
            return;
        }
        if (indexBufferBuilder.LargeIndices && originalIndexBuffer.GetIndexSize() != 4)
        {
            Log.GetLogger().LogError("Cannot create Model inplace: Index Buffer index size does not match");
            return;
        }
        indexBufferBuilder.Buffer = originalIndexBuffer;
    }

    private static void AllocateNewBuffers(List<VertexBufferBuilder> vertexBufferBuilders,
        IndexBufferBuilder indexBufferBuilder, bool headless)
    {
        foreach (var builder in vertexBufferBuilders)
        {
            var vertexElements = builder.VertexFormat.ToVertexElements();
            if (vertexElements.Count == 0)
            {
                Log.GetLogger().LogError("No vertex elements in vertex buffer");
                continue;
            }
            var vertexBuffer = new VertexBuffer(headless ? DeviceObjectFlag.Headless : DeviceObjectFlag.None);
            vertexBuffer.SetShadowed(true);
            vertexBuffer.SetSize(builder.NumVertices, vertexElements);
            builder.Buffer = vertexBuffer;
        }

        if (indexBufferBuilder.NumIndices != 0)
        {
            var indexBuffer = new IndexBuffer(headless ? DeviceObjectFlag.Headless : DeviceObjectFlag.None);
            indexBuffer.SetShadowed(true);
            indexBuffer.SetSize(indexBufferBuilder.NumIndices, indexBufferBuilder.LargeIndices);
            indexBufferBuilder.Buffer = indexBuffer;
        }
    }

    private static void CalculateMorphRange(VertexBufferBuilder builder)
    {
        var minMorphVertex = uint.MaxValue;
        var maxMorphVertex = 0u;
        foreach (var morphData in builder.Morphs)
        {
            foreach (var vertexMorph in morphData)
            {
                minMorphVertex = Math.Min(minMorphVertex, vertexMorph.Index);
                maxMorphVertex = Math.Max(maxMorphVertex, vertexMorph.Index);
            }
        }
        if (minMorphVertex <= maxMorphVertex)
        {
            builder.MorphRangeStart = minMorphVertex;
            builder.MorphRangeCount = maxMorphVertex - minMorphVertex + 1;
        }
    }

    private static void ComposeVertexBufferData(VertexBufferBuilder builder)
    {
        if (builder.NumVertices == 0)
            return;
        if (builder.Buffer is null)
        {
            Log.GetLogger().LogError("Cannot map VertexBuffer");
            return;
        }

        var destinationElements = builder.Buffer.GetElements();
        var unpacked = new Vector4f[checked((int)(builder.NumVertices * (uint)destinationElements.Count))];
        var offset = 0u;
        foreach (var geometry in builder.VertexSources)
        {
            foreach (var vertex in geometry.Vertices)
            {
                var data = vertex.Data();
                for (var destinationIndex = 0; destinationIndex < destinationElements.Count; ++destinationIndex)
                {
                    var destinationElement = destinationElements[destinationIndex];
                    for (var sourceIndex = 0; sourceIndex < ModelVertex.VertexElements.Length; ++sourceIndex)
                    {
                        var sourceElement = ModelVertex.VertexElements[sourceIndex];
                        if (destinationElement.Semantic == sourceElement.Semantic && destinationElement.Index == sourceElement.Index)
                        {
                            unpacked[checked((int)(offset * (uint)destinationElements.Count + (uint)destinationIndex))] = data[sourceIndex];
                            break;
                        }
                    }
                }
                ++offset;
            }
        }
        builder.Buffer.SetUnpackedData(unpacked);
    }

    private static void ComposeIndexBufferData(IndexBufferBuilder builder)
    {
        if (builder.NumIndices == 0)
            return;
        if (builder.Buffer is null)
        {
            Log.GetLogger().LogError("Cannot map IndexBuffer");
            return;
        }

        var indices = new uint[checked((int)builder.NumIndices)];
        var offset = 0;
        foreach (var (geometry, baseIndex) in builder.IndexSources)
        {
            foreach (var index in geometry.Indices)
                indices[offset++] = index + baseIndex;
        }
        builder.Buffer.SetUnpackedData(indices);
    }

    /// <summary>Set contents.</summary>
    public void SetName(string name) => _name = name;
    public void SetGeometries(List<GeometryView> geometries) =>
        _geometries = geometries.Select(static geometry => geometry.Clone()).ToList();
    public void SetBones(List<BoneView> bones) => _bones = [.. bones];
    public void SetMorphs(List<ModelMorphView> morphs) => _morphs = [.. morphs];
    public void SetMorph(uint index, ModelMorphView morph)
    {
        while (_morphs.Count <= index)
            _morphs.Add(new ModelMorphView());
        _morphs[(int)index] = morph;
    }

    public void AddMetadata(string key, Variant variant)
    {
        var replacement = variant.Clone();
        if (_metadata.TryGetValue(key, out var previous))
            previous.Dispose();
        _metadata[key] = replacement;
    }

    /// <summary>Return contents.</summary>
    public string GetName() => _name;
    public List<GeometryView> GetGeometries() => _geometries;
    public List<BoneView> GetBones() => _bones;
    public List<ModelMorphView> GetMorphs() => _morphs;
    public ref readonly Variant GetMetadata(string key)
    {
        ref var value = ref CollectionsMarshal.GetValueRefOrNullRef(_metadata, key);
        if (!Unsafe.IsNullRef(ref value))
            return ref value;
        return ref Variant.Empty;
    }
}
