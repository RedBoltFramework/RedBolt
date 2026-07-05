namespace RedBolt;

public sealed class VertexBuffer
{
    private static readonly VertexElement[] LegacyVertexElements =
    [
        new(VertexElementType.Vector3, VertexElementSemantic.Position),
        new(VertexElementType.Vector3, VertexElementSemantic.Normal),
        new(VertexElementType.UByte4Norm, VertexElementSemantic.Color),
        new(VertexElementType.Vector2, VertexElementSemantic.TexCoord, 0),
        new(VertexElementType.Vector2, VertexElementSemantic.TexCoord, 1),
        new(VertexElementType.Vector3, VertexElementSemantic.TexCoord, 0),
        new(VertexElementType.Vector3, VertexElementSemantic.TexCoord, 1),
        new(VertexElementType.Vector4, VertexElementSemantic.Tangent),
        new(VertexElementType.Vector4, VertexElementSemantic.BlendWeights),
        new(VertexElementType.UByte4, VertexElementSemantic.BlendIndices),
        new(VertexElementType.Vector4, VertexElementSemantic.TexCoord, 4, 1),
        new(VertexElementType.Vector4, VertexElementSemantic.TexCoord, 5, 1),
        new(VertexElementType.Vector4, VertexElementSemantic.TexCoord, 6, 1),
        new(VertexElementType.Int, VertexElementSemantic.ObjectIndex)
    ];

    private static readonly uint[] TypeSizes =
    [
        sizeof(int),
        sizeof(float),
        2u * sizeof(float),
        3u * sizeof(float),
        4u * sizeof(float),
        sizeof(uint),
        sizeof(uint)
    ];

    private byte[] data = [];
    private List<VertexElement> elements = [];
    private uint elementMask;

    public bool IsDynamic { get; private set; }

    public bool Shadowed { get; private set; }

    public IReadOnlyList<byte> Data => data;

    public IReadOnlyList<VertexElement> Elements => elements;

    public uint ElementMask => elementMask;

    public uint VertexCount { get; private set; }

    public uint VertexSize { get; private set; }

    public IReadOnlyList<byte> GetData() => data;

    public IReadOnlyList<VertexElement> GetElements() => elements;

    public uint GetElementMask() => elementMask;

    public uint GetVertexCount() => VertexCount;

    public uint GetVertexSize() => VertexSize;

    public uint GetElementOffset(VertexElementSemantic semantic, byte index = 0)
    {
        foreach (var element in elements)
        {
            if (element.Semantic == semantic && element.Index == index)
                return element.Offset;
        }

        return uint.MaxValue;
    }

    public bool IsDynamicBuffer() => IsDynamic;

    public void SetData(IEnumerable<byte> data)
    {
        ArgumentNullException.ThrowIfNull(data);
        SetData(data.ToArray());
    }

    public void SetData(ReadOnlySpan<byte> data)
    {
        this.data = data.ToArray();
    }

    public void SetData(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        this.data = data.ToArray();
    }

    public void SetShadowed(bool enable)
    {
        Shadowed = enable;
    }

    public void SetSize(uint vertexCount, uint vertexSize, bool dynamic = false)
    {
        elements = [];
        elementMask = 0;
        VertexCount = vertexCount;
        VertexSize = vertexSize;
        IsDynamic = dynamic;
        var size = checked((int)(vertexCount * vertexSize));
        if (data.Length != size)
            data = new byte[size];
    }

    public void SetSize(uint vertexCount, IEnumerable<VertexElement> elements, bool dynamic = false)
    {
        ArgumentNullException.ThrowIfNull(elements);

        var elementList = elements.ToList();
        UpdateOffsets(elementList);
        this.elements = elementList;
        elementMask = CalculateElementMask(elementList);
        VertexCount = vertexCount;
        VertexSize = GetVertexSize(elementList);
        IsDynamic = dynamic;
        var size = checked((int)(vertexCount * VertexSize));
        if (data.Length != size)
            data = new byte[size];
    }

    public void SetSize(uint vertexCount, VertexMask elementMask, bool dynamic = false)
    {
        SetSize(vertexCount, GetElements((uint)elementMask), dynamic);
    }

    public static List<VertexElement> GetElements(uint elementMask)
    {
        var result = new List<VertexElement>();
        for (var i = 0; i < LegacyVertexElements.Length; ++i)
        {
            if ((elementMask & (1u << i)) != 0)
                result.Add(LegacyVertexElements[i]);
        }

        return result;
    }

    public static uint GetVertexSize(IEnumerable<VertexElement> elements)
    {
        ArgumentNullException.ThrowIfNull(elements);
        return elements.Aggregate(0u, static (sum, element) => sum + TypeSizes[(int)element.Type]);
    }

    public static uint GetVertexSize(VertexMask elementMask) => GetVertexSize(GetElements((uint)elementMask));

    public static void UpdateOffsets(IList<VertexElement> elements)
    {
        ArgumentNullException.ThrowIfNull(elements);

        uint offset = 0;
        for (var i = 0; i < elements.Count; ++i)
        {
            var element = elements[i];
            element.Offset = offset;
            elements[i] = element;
            offset += TypeSizes[(int)element.Type];
        }
    }

    private static uint CalculateElementMask(IEnumerable<VertexElement> elements)
    {
        uint mask = 0;
        foreach (var element in elements)
        {
            for (var i = 0; i < LegacyVertexElements.Length; ++i)
            {
                var legacy = LegacyVertexElements[i];
                if (legacy.Type == element.Type && legacy.Semantic == element.Semantic && legacy.Index == element.Index)
                    mask |= 1u << i;
            }
        }

        return mask;
    }
}
