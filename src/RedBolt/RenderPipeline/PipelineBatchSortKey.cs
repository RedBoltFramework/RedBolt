namespace RedBolt;

/// <summary>
/// Scene batch sorted by pipeline state, material and geometry. Also sorted front to back.
/// </summary>
public struct PipelineBatchByState : IComparable<PipelineBatchByState>
{
    public const ulong PixelLightBits = 8;
    public const ulong LightmapBits = 8;
    public const ulong MaterialBits = 16;
    public const ulong PipelineStateBits = 24;
    public const ulong RenderOrderBits = 8;

    public const ulong PixelLightMask = (1ul << (int)PixelLightBits) - 1;
    public const ulong LightmapMask = (1ul << (int)LightmapBits) - 1;
    public const ulong MaterialMask = (1ul << (int)MaterialBits) - 1;
    public const ulong PipelineStateMask = (1ul << (int)PipelineStateBits) - 1;
    public const ulong RenderOrderMask = (1ul << (int)RenderOrderBits) - 1;

    public const ulong PixelLightOffset = 0;
    public const ulong LightmapOffset = PixelLightOffset + PixelLightBits;
    public const ulong MaterialOffset = LightmapOffset + LightmapBits;
    public const ulong PipelineStateOffset = MaterialOffset + MaterialBits;
    public const ulong RenderOrderOffset = PipelineStateOffset + PipelineStateBits;

    public const ulong ReservedBits = 16;
    public const ulong VertexLightsBits = 24;
    public const ulong GeometryBits = 24;

    public const ulong ReservedMask = (1ul << (int)ReservedBits) - 1;
    public const ulong VertexLightsMask = (1ul << (int)VertexLightsBits) - 1;
    public const ulong GeometryMask = (1ul << (int)GeometryBits) - 1;

    public const ulong ReservedOffset = 0;
    public const ulong VertexLightsOffset = ReservedOffset + ReservedBits;
    public const ulong GeometryOffset = VertexLightsOffset + VertexLightsBits;

    public ulong PrimaryKey;
    public ulong SecondaryKey;
    public PipelineBatch? PipelineBatch;

    public PipelineBatchByState(PipelineBatch pipelineBatch)
    {
        PipelineBatch = pipelineBatch;
        PrimaryKey = 0;
        SecondaryKey = 0;

        PrimaryKey |= ((ulong)pipelineBatch.Material!.GetRenderOrder() & RenderOrderMask) << (int)RenderOrderOffset;
        PrimaryKey |= ((ulong)pipelineBatch.PipelineState!.GetObjectID() & PipelineStateMask) << (int)PipelineStateOffset;
        PrimaryKey |= ((ulong)pipelineBatch.Material.GetObjectID() & MaterialMask) << (int)MaterialOffset;
        PrimaryKey |= ((ulong)pipelineBatch.LightmapIndex & LightmapMask) << (int)LightmapOffset;
        PrimaryKey |= ((ulong)pipelineBatch.PixelLightIndex & PixelLightMask) << (int)PixelLightOffset;

        SecondaryKey |= ((ulong)pipelineBatch.Geometry!.GetObjectID() & GeometryMask) << (int)GeometryOffset;
        SecondaryKey |= ((ulong)pipelineBatch.VertexLightsHash & VertexLightsMask) << (int)VertexLightsOffset;
    }

    public int CompareTo(PipelineBatchByState other)
    {
        if (PrimaryKey != other.PrimaryKey)
            return PrimaryKey < other.PrimaryKey ? -1 : 1;
        if (SecondaryKey != other.SecondaryKey)
            return SecondaryKey < other.SecondaryKey ? -1 : 1;
        return 0;
    }
}

/// <summary>
/// Pipeline batch sorted by render order and back to front.
/// </summary>
public struct PipelineBatchBackToFront : IComparable<PipelineBatchBackToFront>
{
    public byte RenderOrder;
    public float Distance;
    public PipelineBatch? PipelineBatch;

    public PipelineBatchBackToFront(PipelineBatch pipelineBatch)
    {
        RenderOrder = pipelineBatch.Material!.GetRenderOrder();
        Distance = pipelineBatch.Distance;
        PipelineBatch = pipelineBatch;
    }

    public int CompareTo(PipelineBatchBackToFront other)
    {
        if (RenderOrder != other.RenderOrder)
            return RenderOrder < other.RenderOrder ? -1 : 1;
        if (Distance > other.Distance)
            return -1;
        if (other.Distance > Distance)
            return 1;
        return 0;
    }
}

/// <summary>
/// Group of batches to be rendered.
/// </summary>
public struct PipelineBatchGroup<T>
{
    public List<T>? Batches;
    public BatchRenderFlag Flags;
    public int StartInstance;
    public int NumInstances;
    public IntRect ScissorRect;
}
