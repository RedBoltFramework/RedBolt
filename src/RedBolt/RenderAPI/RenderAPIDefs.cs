namespace RedBolt;

public static class RenderApiLimits
{
    public const uint MaxRenderTargets = 8;
    public const uint MaxVertexStreams = 4;
    public const uint MaxNumImmutableSamplers = 16;
}

public enum DeviceObjectEvent
{
    Invalidate,
    Restore,
    Destroy
}

public enum PipelineStateType
{
    Graphics,
    Compute
}

public sealed class PipelineStateOutputDesc : IEquatable<PipelineStateOutputDesc>
{
    private readonly TextureFormat[] renderTargetFormats = new TextureFormat[RenderApiLimits.MaxRenderTargets];

    public TextureFormat DepthStencilFormat { get; set; } = TextureFormat.Unknown;

    public uint NumRenderTargets { get; private set; }

    public uint MultiSample { get; set; } = 1u;

    public ReadOnlySpan<TextureFormat> RenderTargetFormats => renderTargetFormats.AsSpan(0, checked((int)NumRenderTargets));

    public void ClearRenderTargetFormats()
    {
        Array.Clear(renderTargetFormats, 0, renderTargetFormats.Length);
        NumRenderTargets = 0u;
    }

    public void SetRenderTargetFormats(params TextureFormat[] formats)
    {
        ArgumentNullException.ThrowIfNull(formats);

        if (formats.Length > RenderApiLimits.MaxRenderTargets)
            throw new ArgumentOutOfRangeException(nameof(formats), $"At most {RenderApiLimits.MaxRenderTargets} render targets are supported.");

        Array.Clear(renderTargetFormats, 0, renderTargetFormats.Length);
        Array.Copy(formats, renderTargetFormats, formats.Length);
        NumRenderTargets = (uint)formats.Length;
    }

    public uint ToHash()
    {
        var hash = new HashCode();
        hash.Add(DepthStencilFormat);
        hash.Add(NumRenderTargets);
        hash.Add(MultiSample);
        for (var i = 0; i < NumRenderTargets; ++i)
            hash.Add(renderTargetFormats[i]);
        return unchecked((uint)hash.ToHashCode());
    }

    public bool Equals(PipelineStateOutputDesc? other)
    {
        if (other is null)
            return false;

        if (ReferenceEquals(this, other))
            return true;

        if (DepthStencilFormat != other.DepthStencilFormat || NumRenderTargets != other.NumRenderTargets || MultiSample != other.MultiSample)
            return false;

        for (var i = 0; i < NumRenderTargets; ++i)
        {
            if (renderTargetFormats[i] != other.renderTargetFormats[i])
                return false;
        }

        return true;
    }

    public override bool Equals(object? obj) => Equals(obj as PipelineStateOutputDesc);

    public override int GetHashCode() => unchecked((int)ToHash());
}
