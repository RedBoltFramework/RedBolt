using RedBolt.Graphics;
using RedBolt.RenderAPI;

namespace RedBolt.RenderPipeline;

/// <summary>
/// Self-sufficient batch that can be sorted and rendered by RenderPipeline.
/// </summary>
public struct PipelineBatch
{
    public Drawable? Drawable { get; set; }
    public Geometry? Geometry { get; set; }
    public Material? Material { get; set; }
    public PipelineState? PipelineState { get; set; }
    public object? UserData { get; set; }
    public int DrawableIndex { get; set; }
    public int PixelLightIndex { get; set; }
    public uint VertexLightsHash { get; set; }
    public int SourceBatchIndex { get; set; }
    public int LightmapIndex { get; set; }
    public float Distance { get; set; }
    public GeometryType GeometryType { get; set; }

    public SourceBatch GetSourceBatch()
    {
        return SourceBatchIndex != int.MaxValue ? Drawable!.GetBatches()[SourceBatchIndex] : default;
    }

    public PipelineBatch()
    {
        PixelLightIndex = int.MaxValue;
    }

    public PipelineBatch(Drawable drawable, int sourceBatchIndex, object? userData = null)
    {
        Drawable = drawable;
        UserData = userData;
        var drawableIndex = drawable.GetDrawableIndex();
        DrawableIndex = drawableIndex == uint.MaxValue ? int.MaxValue : checked((int)drawableIndex);
        PixelLightIndex = int.MaxValue;
        SourceBatchIndex = sourceBatchIndex;

        var sourceBatch = GetSourceBatch();
        Geometry = sourceBatch.Geometry;
        Material = sourceBatch.Material;
        LightmapIndex = checked((int)sourceBatch.LightmapIndex);
        Distance = sourceBatch.Distance;
        GeometryType = sourceBatch.GeometryType;
    }

    public PipelineBatch(PipelineBatchDesc desc)
    {
        Drawable = desc.Drawable;
        Geometry = desc.Geometry;
        Material = desc.Material;
        PipelineState = desc.PipelineState;
        UserData = desc.UserData;
        DrawableIndex = desc.DrawableIndex;
        PixelLightIndex = desc.PixelLightIndex;
        VertexLightsHash = desc.VertexLightsHash;
        SourceBatchIndex = desc.SourceBatchIndex;
        LightmapIndex = desc.LightmapIndex;
        Distance = desc.Distance;
        GeometryType = desc.GeometryType;
    }
}

/// <summary>
/// Information needed to fully create PipelineBatch.
/// </summary>
public struct PipelineBatchDesc
{
    public Drawable? Drawable { get; set; }
    public Geometry? Geometry { get; set; }
    public Material? Material { get; set; }
    public PipelineState? PipelineState { get; set; }
    public object? UserData { get; set; }
    public int DrawableIndex { get; set; }
    public int PixelLightIndex { get; set; }
    public uint VertexLightsHash { get; set; }
    public int SourceBatchIndex { get; set; }
    public int LightmapIndex { get; set; }
    public float Distance { get; set; }
    public GeometryType GeometryType { get; set; }

    public Pass? Pass { get; set; }
    public uint DrawableHash { get; set; }
    /// <summary>
    /// Light that contributes to pipeline state.
    /// For scene batches: per-pixel forward light applied to object.
    /// For shadow batches: owner shadow split.
    /// </summary>
    public LightProcessor? PixelLightForPipelineState { get; set; }
    public int PixelLightForPipelineStateIndex { get; set; }
    public uint PixelLightForPipelineStateHash { get; set; }

    public SourceBatch GetSourceBatch()
    {
        return SourceBatchIndex != int.MaxValue ? Drawable!.GetBatches()[SourceBatchIndex] : default;
    }

    public PipelineBatchDesc()
    {
        PixelLightIndex = int.MaxValue;
    }

    public PipelineBatchDesc(Drawable drawable, int sourceBatchIndex, Pass? pass, object? userData = null)
    {
        Drawable = drawable;
        UserData = userData;
        var drawableIndex = drawable.GetDrawableIndex();
        DrawableIndex = drawableIndex == uint.MaxValue ? int.MaxValue : checked((int)drawableIndex);
        PixelLightIndex = int.MaxValue;
        SourceBatchIndex = sourceBatchIndex;

        var sourceBatch = GetSourceBatch();
        Geometry = sourceBatch.Geometry;
        Material = sourceBatch.Material;
        LightmapIndex = checked((int)sourceBatch.LightmapIndex);
        Distance = sourceBatch.Distance;
        GeometryType = sourceBatch.GeometryType;

        Pass = pass;
        DrawableHash = drawable.GetPipelineStateHash();
    }

    public void InitializeShadowBatch(LightProcessor light, int lightIndex, uint lightHash)
    {
        PixelLightForPipelineState = light;
        PixelLightForPipelineStateIndex = lightIndex;
        PixelLightForPipelineStateHash = lightHash;
    }

    public void InitializeLitBatch(LightProcessor? light, int lightIndex, uint lightHash)
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
