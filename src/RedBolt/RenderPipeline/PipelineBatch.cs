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
