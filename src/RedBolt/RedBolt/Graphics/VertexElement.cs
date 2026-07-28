namespace RedBolt.Graphics;

using RedBolt.Math;
using RedBolt.RenderAPI;

/// <summary>Legacy vertex element bitmasks.</summary>
[Flags]
public enum VertexMask : uint
{
    None = 0x0,
    Position = 0x1,
    Normal = 0x2,
    Color = 0x4,
    TexCoord1 = 0x8,
    TexCoord2 = 0x10,
    CubeTexCoord1 = 0x20,
    CubeTexCoord2 = 0x40,
    Tangent = 0x80,
    BlendWeights = 0x100,
    BlendIndices = 0x200,
    InstanceMatrix1 = 0x400,
    InstanceMatrix2 = 0x800,
    InstanceMatrix3 = 0x1000,
    // Custom 32-bit integer object index. Due to API limitations, not supported on D3D9
    ObjectIndex = 0x2000
}

/// <summary>Vertex element description for arbitrary vertex declarations.</summary>
public struct VertexElement : IEquatable<VertexElement>
{
    /// <summary>Default-construct.</summary>
    public VertexElement()
    {
        Type = VertexElementType.Vector3;
        Semantic = VertexElementSemantic.Position;
        Index = 0;
        StepRate = 0;
        Offset = 0;
    }

    /// <summary>Construct with type, semantic, index and whether is per-instance data.</summary>
    public VertexElement(
        VertexElementType type,
        VertexElementSemantic semantic,
        byte index = 0,
        uint stepRate = 0)
    {
        Type = type;
        Semantic = semantic;
        Index = index;
        StepRate = stepRate;
        Offset = 0;
    }

    /// <summary>Data type of element.</summary>
    public VertexElementType Type;

    /// <summary>Semantic of element.</summary>
    public VertexElementSemantic Semantic;

    /// <summary>Semantic index of element, for example multi-texcoords.</summary>
    public byte Index;

    /// <summary>Instancing data steprate.</summary>
    public uint StepRate;

    /// <summary>Offset of element from vertex start. Filled by VertexBuffer once the vertex declaration is built.</summary>
    public uint Offset;

    /// <summary>Test for equality with another vertex element. Offset is intentionally not compared, as it's relevant only when an element exists within a vertex buffer.</summary>
    public readonly bool Equals(VertexElement other)
    {
        return Type == other.Type
            && Semantic == other.Semantic
            && Index == other.Index
            && StepRate == other.StepRate;
    }

    /// <summary>Return hash value of the vertex element.</summary>
    public readonly uint ToHash()
    {
        var hash = 0u;
        CombineHash(ref hash, (uint)Type);
        CombineHash(ref hash, (uint)Semantic);
        CombineHash(ref hash, Index);
        CombineHash(ref hash, StepRate);
        CombineHash(ref hash, Offset);
        return hash;
    }

    public override readonly bool Equals(object? obj) => obj is VertexElement other && Equals(other);

    public override readonly int GetHashCode() => unchecked((int)ToHash());

    public static bool operator ==(VertexElement left, VertexElement right) => left.Equals(right);

    public static bool operator !=(VertexElement left, VertexElement right) => !left.Equals(right);

    private static void CombineHash(ref uint result, uint hash)
    {
        result ^= hash + 0x9e3779b9u + (result << 6) + (result >> 2);
    }
}
