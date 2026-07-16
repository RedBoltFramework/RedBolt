namespace RedBolt;

/// <summary>
/// Self-sufficient batch that can be sorted and rendered by RenderPipeline.
/// </summary>
public class PipelineBatch
{
    public Drawable? Drawable;
    public Geometry? Geometry;
    public Material? Material;
    public PipelineState? PipelineState;
    public object? UserData;
    public uint DrawableIndex;
    public uint PixelLightIndex = uint.MaxValue;
    public uint VertexLightsHash;
    public uint SourceBatchIndex;
    public uint LightmapIndex;
    public float Distance;
    public GeometryType GeometryType;

    public SourceBatch GetSourceBatch()
    {
        return SourceBatchIndex != uint.MaxValue ? Drawable!.GetBatches()[(int)SourceBatchIndex] : default;
    }

    public PipelineBatch()
    {
    }

    public PipelineBatch(Drawable drawable, uint sourceBatchIndex, object? userData = null)
    {
        Drawable = drawable;
        Geometry = null;
        Material = null;
        PipelineState = null;
        UserData = userData;
        DrawableIndex = drawable.GetDrawableIndex();
        PixelLightIndex = uint.MaxValue;
        VertexLightsHash = 0;
        SourceBatchIndex = sourceBatchIndex;
        LightmapIndex = 0;
        Distance = 0;
        GeometryType = default;

        var sourceBatch = GetSourceBatch();
        Geometry = sourceBatch.Geometry;
        Material = sourceBatch.Material;
        LightmapIndex = sourceBatch.LightmapIndex;
        Distance = sourceBatch.Distance;
        GeometryType = sourceBatch.GeometryType;
    }
}

/// <summary>
/// Information needed to fully create PipelineBatch.
/// </summary>
public class PipelineBatchDesc : PipelineBatch
{
    public Pass? Pass;
    public uint DrawableHash;
    /// <summary>
    /// Light that contributes to pipeline state.
    /// For scene batches: per-pixel forward light applied to object.
    /// For shadow batches: owner shadow split.
    /// </summary>
    public LightProcessor? PixelLightForPipelineState;
    public uint PixelLightForPipelineStateIndex;
    public uint PixelLightForPipelineStateHash;

    public PipelineBatchDesc()
    {
    }

    public PipelineBatchDesc(Drawable drawable, uint sourceBatchIndex, Pass pass, object? userData = null)
        : base(drawable, sourceBatchIndex, userData)
    {
        Pass = pass;
        DrawableHash = drawable.GetPipelineStateHash();
    }

    public void InitializeShadowBatch(LightProcessor light, uint lightIndex, uint lightHash)
    {
        PixelLightForPipelineState = light;
        PixelLightForPipelineStateIndex = lightIndex;
        PixelLightForPipelineStateHash = lightHash;
    }

    public void InitializeLitBatch(LightProcessor light, uint lightIndex, uint lightHash)
    {
        PixelLightIndex = lightIndex;
        PixelLightForPipelineState = light;
        PixelLightForPipelineStateIndex = lightIndex;
        PixelLightForPipelineStateHash = lightHash;
    }

    public BatchStateCreateKey GetKey()
    {
        return new BatchStateCreateKey(
            DrawableHash,
            PixelLightForPipelineStateHash,
            GeometryType,
            Geometry!,
            Material!,
            Pass!,
            Drawable,
            PixelLightForPipelineState,
            PixelLightForPipelineStateIndex);
    }
}
