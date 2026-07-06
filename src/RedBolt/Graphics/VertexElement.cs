namespace RedBolt;

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
    ObjectIndex = 0x2000
}

public struct VertexElement : IEquatable<VertexElement>
{
    public VertexElement(
        VertexElementType type,
        VertexElementSemantic semantic,
        byte index = 0,
        uint stepRate = 0,
        uint offset = 0)
    {
        Type = type;
        Semantic = semantic;
        Index = index;
        StepRate = stepRate;
        Offset = offset;
    }

    public VertexElementType Type { get; set; } = VertexElementType.Vector3;

    public VertexElementSemantic Semantic { get; set; } = VertexElementSemantic.Position;

    public byte Index { get; set; }

    public uint StepRate { get; set; }

    public uint Offset { get; set; }

    public readonly bool Equals(VertexElement other)
    {
        return Type == other.Type
            && Semantic == other.Semantic
            && Index == other.Index
            && StepRate == other.StepRate
            && Offset == other.Offset;
    }

    public override readonly bool Equals(object? obj) => obj is VertexElement other && Equals(other);

    public override readonly int GetHashCode() => HashCode.Combine(Type, Semantic, Index, StepRate, Offset);

    public static bool operator ==(VertexElement left, VertexElement right) => left.Equals(right);

    public static bool operator !=(VertexElement left, VertexElement right) => !left.Equals(right);
}
