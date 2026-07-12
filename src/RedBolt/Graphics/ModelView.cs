using System.Collections;

namespace RedBolt;

using ModelVertexMorphVector = List<ModelVertexMorph>;

/// <summary>Model vertex format, unpacked for easy editing.</summary>
public struct ModelVertexFormat : IEquatable<ModelVertexFormat>
{
    public static readonly VertexElementType Undefined = (VertexElementType)byte.MaxValue;
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
        var hash = new HashCode();
        hash.Add(Position);
        hash.Add(Normal);
        hash.Add(Tangent);
        hash.Add(Binormal);
        hash.Add(BlendIndices);
        hash.Add(BlendWeights);
        foreach (var type in Color)
            hash.Add(type);
        foreach (var type in Uv)
            hash.Add(type);
        return unchecked((uint)hash.ToHashCode());
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
}

/// <summary>Model vertex, unpacked for easy editing.</summary>
public struct ModelVertex : IEquatable<ModelVertex>
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
        Position = Vector4f.ZERO;
        Normal = Vector4f.ZERO;
        Tangent = Vector4f.ZERO;
        Binormal = Vector4f.ZERO;
        BlendIndices = Vector4f.ZERO;
        BlendWeights = Vector4f.ZERO;
        Color = new Vector4f[MaxColors];
        Uv = new Vector4f[MaxUVs];
    }

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
    public bool HasNormal() => Normal != Vector4f.ZERO;

    /// <summary>Return whether the vertex has tangent.</summary>
    public bool HasTangent() => Tangent != Vector4f.ZERO;

    /// <summary>Return whether the vertex has binormal.</summary>
    public bool HasBinormal() => Binormal != Vector4f.ZERO;

    /// <summary>Return whether the vertex has tangent and binormal combined.</summary>
    public bool HasTangentBinormalCombined() => Tangent != Vector4f.ZERO && Tangent.W != 0;

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
                Binormal = Vector4f.ZERO;
        }
        else
        {
            Tangent = Vector4f.ZERO;
            Binormal = Vector4f.ZERO;
        }
    }

    /// <summary>Prune vertex elements not represented in the format.</summary>
    public void PruneElements(ModelVertexFormat format)
    {
        if (format.Position == ModelVertexFormat.Undefined) Position = Vector4f.ZERO;
        if (format.Normal == ModelVertexFormat.Undefined) Normal = Vector4f.ZERO;
        if (format.Tangent == ModelVertexFormat.Undefined) Tangent = Vector4f.ZERO;
        if (format.Binormal == ModelVertexFormat.Undefined) Binormal = Vector4f.ZERO;
        if (format.BlendIndices == ModelVertexFormat.Undefined) BlendIndices = Vector4f.ZERO;
        if (format.BlendWeights == ModelVertexFormat.Undefined) BlendWeights = Vector4f.ZERO;
        for (var i = 0; i < MaxColors; ++i)
        {
            if (format.Color[i] == ModelVertexFormat.Undefined)
                Color[i] = Vector4f.ZERO;
        }
        for (var i = 0; i < MaxUVs; ++i)
        {
            if (format.Uv[i] == ModelVertexFormat.Undefined)
                Uv[i] = Vector4f.ZERO;
        }
    }

    public bool Equals(ModelVertex rhs)
    {
        return Position == rhs.Position && Normal == rhs.Normal && Tangent == rhs.Tangent
            && Binormal == rhs.Binormal && BlendIndices == rhs.BlendIndices && BlendWeights == rhs.BlendWeights
            && Color.SequenceEqual(rhs.Color) && Uv.SequenceEqual(rhs.Uv);
    }

    public override bool Equals(object? obj) => obj is ModelVertex rhs && Equals(rhs);

    public override int GetHashCode() => HashCode.Combine(Position, Normal, Tangent, Binormal, BlendIndices, BlendWeights);

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

    public bool HasPosition() => PositionDelta != Vector3f.ZERO;
    public bool HasNormal() => NormalDelta != Vector3f.ZERO;
    public bool HasTangent() => TangentDelta != Vector3f.ZERO;
    public bool IsEmpty() => !HasPosition() && !HasNormal() && !HasTangent();

    public bool Equals(ModelVertexMorph rhs) => PositionDelta == rhs.PositionDelta
        && NormalDelta == rhs.NormalDelta && TangentDelta == rhs.TangentDelta;

    public override bool Equals(object? obj) => obj is ModelVertexMorph rhs && Equals(rhs);

    public override int GetHashCode() => HashCode.Combine(PositionDelta, NormalDelta, TangentDelta);

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
public struct GeometryLODView : IEquatable<GeometryLODView>
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
            return Vector3f.ZERO;
        var center = Vector3f.ZERO;
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
            vertex.Normal = Vector4f.ZERO;
            vertex.Tangent = Vector4f.ZERO;
            Vertices[i] = vertex;
        }
        foreach (var morphs in Morphs.Values)
        {
            for (var i = 0; i < morphs.Count; ++i)
            {
                var morph = morphs[i];
                morph.NormalDelta = Vector3f.ZERO;
                morph.TangentDelta = Vector3f.ZERO;
                morphs[i] = morph;
            }
        }
    }

    public void RecalculateFlatNormals()
    {
        if (!IsTriangleGeometry())
            return;
        InvalidateNormalsAndTangents();
        var newVertices = new List<ModelVertex>();
        var sourceVertices = Vertices;
        ForEachTriangle((i0, i1, i2) =>
        {
            var v0 = sourceVertices[(int)i0];
            var v1 = sourceVertices[(int)i1];
            var v2 = sourceVertices[(int)i2];
            var normal = (v1.GetPosition() - v0.GetPosition()).CrossProduct(v2.GetPosition() - v0.GetPosition()).Normalized;
            v0.Normal = normal.ToVector4();
            v1.Normal = normal.ToVector4();
            v2.Normal = normal.ToVector4();
            newVertices.Add(v0);
            newVertices.Add(v1);
            newVertices.Add(v2);
        });
        PrimitiveType = PrimitiveType.TriangleList;
        Vertices = newVertices;
        Indices = Enumerable.Range(0, Vertices.Count).Select(static index => (uint)index).ToList();
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
        // TODO: port: Tangent::CalculateTangents requires the native tangent helper and managed vertex-buffer packing.
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

    public bool Equals(GeometryLODView rhs) => PrimitiveType == rhs.PrimitiveType
        && Vertices.SequenceEqual(rhs.Vertices) && Indices.SequenceEqual(rhs.Indices)
        && LodDistance == rhs.LodDistance && VertexFormat == rhs.VertexFormat
        && Morphs.Count == rhs.Morphs.Count && Morphs.All(pair => rhs.Morphs.TryGetValue(pair.Key, out var value) && pair.Value.SequenceEqual(value));

    public override bool Equals(object? obj) => obj is GeometryLODView rhs && Equals(rhs);
    public override int GetHashCode() => HashCode.Combine(PrimitiveType, LodDistance, VertexFormat);
    public static bool operator ==(GeometryLODView lhs, GeometryLODView rhs) => lhs.Equals(rhs);
    public static bool operator !=(GeometryLODView lhs, GeometryLODView rhs) => !lhs.Equals(rhs);
}

/// <summary>Model geometry, unpacked for easy editing.</summary>
public struct GeometryView : IEquatable<GeometryView>
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
    public uint ParentIndex;
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
        ParentIndex = uint.MaxValue;
        Position = Vector3f.ZERO;
        Rotation = Quaternion.IDENTITY;
        Scale = Vector3f.ONE;
        OffsetMatrix = Matrix3x4.IDENTITY;
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
        && Position == rhs.Position && Rotation == rhs.Rotation && Scale == rhs.Scale
        && OffsetMatrix == rhs.OffsetMatrix && ShapeFlags == rhs.ShapeFlags
        && MathDefs.Equals(BoundingSphereRadius, rhs.BoundingSphereRadius)
        && LocalBoundingBox.Equals(rhs.LocalBoundingBox);
    public override bool Equals(object? obj) => obj is BoneView rhs && Equals(rhs);
    public override int GetHashCode() => HashCode.Combine(Name, ParentIndex, Position, Rotation, Scale);
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
public sealed class ModelView : Object
{
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
        _name = string.Empty;
        _geometries.Clear();
        _bones.Clear();
        _morphs.Clear();
        _metadata.Clear();
    }

    /// <summary>Import and export from/to native Model.</summary>
    public bool ImportModel(Model? model)
    {
        // TODO: port: ModelView::ImportModel requires native Model vertex-buffer, index-buffer, morph, and skeleton access.
        return false;
    }

    /// <summary>Export into a native Model.</summary>
    public void ExportModel(Model model, ModelViewExportFlag flags = ModelViewExportFlag.None)
    {
        // TODO: port: ModelView::ExportModel requires native Model buffer composition and morph/skeleton APIs.
        ArgumentNullException.ThrowIfNull(model);
    }

    /// <summary>Export into a new native Model.</summary>
    public Model ExportModel(ModelViewExportFlag flags = ModelViewExportFlag.None, string name = "")
    {
        var model = new Model(string.IsNullOrEmpty(name) ? _name : name);
        ExportModel(model, flags & ~ModelViewExportFlag.Inplace);
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
            result.Merge(Vector3f.ZERO);
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
                    var weightSum = vertex.BlendWeights.DotProduct(Vector4f.ONE);
                    if (!MathDefs.Equals(weightSum, 1.0f))
                    {
                        if (weightSum < MathDefs.Epsilon)
                        {
                            vertex.BlendIndices = Vector4f.ZERO;
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

    /// <summary>Set contents.</summary>
    public void SetName(string name) => _name = name;
    public void SetGeometries(List<GeometryView> geometries) => _geometries = geometries;
    public void SetBones(List<BoneView> bones) => _bones = bones;
    public void SetMorphs(List<ModelMorphView> morphs) => _morphs = morphs;
    public void SetMorph(uint index, ModelMorphView morph)
    {
        while (_morphs.Count <= index)
            _morphs.Add(new ModelMorphView());
        _morphs[(int)index] = morph;
    }

    public void AddMetadata(string key, Variant variant) => _metadata[key] = variant;

    /// <summary>Return contents.</summary>
    public string GetName() => _name;
    public List<GeometryView> GetGeometries() => _geometries;
    public List<BoneView> GetBones() => _bones;
    public List<ModelMorphView> GetMorphs() => _morphs;
    public Variant GetMetadata(string key) => _metadata.TryGetValue(key, out var value) ? value : new Variant(VariantType.None);
}
