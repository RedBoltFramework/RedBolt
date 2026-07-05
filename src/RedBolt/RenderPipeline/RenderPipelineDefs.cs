namespace RedBolt;

public readonly record struct RenderPassToggle(string Name, bool Enabled);

public enum BatchCompositorSubpass
{
    Deferred,
    Base,
    Light,
    Ignored
}

[Flags]
public enum RenderBufferFlag
{
    None = 0,
    Persistent = 1 << 0,
    FixedTextureSize = 1 << 1,
    BilinearFiltering = 1 << 2,
    CubeMap = 1 << 3,
    NoMultiSampledAutoResolve = 1 << 4
}

public readonly record struct RenderBufferParams(TextureFormat TextureFormat, int MultiSampleLevel = 1, RenderBufferFlag Flags = RenderBufferFlag.None);

public enum RenderPipelineColorSpace
{
    GammaLDR,
    LinearLDR,
    LinearHDR,
    Optimized
}

public sealed class RenderBufferManagerSettings : IEquatable<RenderBufferManagerSettings>
{
    public bool InheritMultiSampleLevel { get; set; } = true;

    public int MultiSampleLevel { get; set; } = 1;

    public RenderPipelineColorSpace ColorSpace { get; set; }

    public bool FilteredColor { get; set; }

    public bool StencilBuffer { get; set; }

    public bool ReadableDepth { get; set; }

    public bool ColorUsableWithMultipleRenderTargets { get; set; }

    public uint CalculatePipelineStateHash()
    {
        var hash = new HashCode();
        hash.Add(ReadableDepth);
        return unchecked((uint)hash.ToHashCode());
    }

    public void Validate()
    {
        MultiSampleLevel = (int)Math.Clamp(MathDefs.ClosestPowerOfTwo((uint)Math.Max(MultiSampleLevel, 1)), 1u, 16u);
    }

    public bool Equals(RenderBufferManagerSettings? other)
    {
        if (other is null)
            return false;

        return InheritMultiSampleLevel == other.InheritMultiSampleLevel
            && MultiSampleLevel == other.MultiSampleLevel
            && ColorSpace == other.ColorSpace
            && FilteredColor == other.FilteredColor
            && StencilBuffer == other.StencilBuffer
            && ReadableDepth == other.ReadableDepth
            && ColorUsableWithMultipleRenderTargets == other.ColorUsableWithMultipleRenderTargets;
    }

    public override bool Equals(object? obj) => Equals(obj as RenderBufferManagerSettings);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(InheritMultiSampleLevel);
        hash.Add(MultiSampleLevel);
        hash.Add(ColorSpace);
        hash.Add(FilteredColor);
        hash.Add(StencilBuffer);
        hash.Add(ReadableDepth);
        hash.Add(ColorUsableWithMultipleRenderTargets);
        return hash.ToHashCode();
    }
}

public sealed class RenderBufferManagerFrameSettings
{
    public bool ReadableColor { get; set; }

    public bool SupportColorReadWrite { get; set; }
}

[Flags]
public enum RenderOutputFlag
{
    None = 0,
    NeedColorOutputReadAndWrite = 1 << 0,
    NeedColorOutputBilinear = 1 << 1
}

public sealed class RenderPipelineStats
{
    public uint NumLights { get; set; }

    public uint NumShadowedLights { get; set; }

    public uint NumGeometries { get; set; }

    public uint NumOccluders { get; set; }
}

public sealed class RenderPipelineSettings : IEquatable<RenderPipelineSettings>
{
    public RenderBufferManagerSettings RenderBufferManager { get; } = new();

    public bool DrawDebugGeometry { get; set; } = true;

    public uint CalculatePipelineStateHash()
    {
        var hash = new HashCode();
        hash.Add(RenderBufferManager.CalculatePipelineStateHash());
        hash.Add(DrawDebugGeometry);
        return unchecked((uint)hash.ToHashCode());
    }

    public void Validate()
    {
        RenderBufferManager.Validate();
    }

    public bool Equals(RenderPipelineSettings? other)
    {
        if (other is null)
            return false;

        return RenderBufferManager.Equals(other.RenderBufferManager)
            && DrawDebugGeometry == other.DrawDebugGeometry;
    }

    public override bool Equals(object? obj) => Equals(obj as RenderPipelineSettings);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(RenderBufferManager);
        hash.Add(DrawDebugGeometry);
        return hash.ToHashCode();
    }
}

public sealed class CommonFrameInfo
{
    public uint FrameNumber { get; set; }

    public float TimeStep { get; set; }

    public IntVector2 ViewportSize { get; set; }

    public IntRect ViewportRect { get; set; }

    public Viewport? Viewport { get; set; }

    public object? RenderTarget { get; set; }

    public Camera?[] Cameras { get; } = new Camera?[2];
}

public interface IRenderPipelineInterface
{
    bool IsLinearColorSpace { get; }

    event Action<CommonFrameInfo>? UpdateBegin;

    event Action<CommonFrameInfo>? UpdateEnd;

    event Action<CommonFrameInfo>? RenderBegin;

    event Action<CommonFrameInfo>? RenderEnd;

    event Action? PipelineStatesInvalidated;

    event Action<RenderPipelineStats>? CollectStatistics;
}

public abstract class RenderPipelineView : IRenderPipelineInterface
{
    public abstract bool IsLinearColorSpace { get; }

    public abstract bool Define(Viewport viewport, Graphics graphics);

    public abstract void Update(Graphics graphics);

    public abstract void Render(Graphics graphics, MaterialQuality materialQuality, string? screenshotPath = null);

    public abstract CommonFrameInfo GetFrameInfo();

    public abstract RenderPipelineStats GetStats();

    public event Action<CommonFrameInfo>? UpdateBegin;

    public event Action<CommonFrameInfo>? UpdateEnd;

    public event Action<CommonFrameInfo>? RenderBegin;

    public event Action<CommonFrameInfo>? RenderEnd;

    public event Action? PipelineStatesInvalidated;

    public event Action<RenderPipelineStats>? CollectStatistics;

    protected void RaiseUpdateBegin(CommonFrameInfo frameInfo) => UpdateBegin?.Invoke(frameInfo);

    protected void RaiseUpdateEnd(CommonFrameInfo frameInfo) => UpdateEnd?.Invoke(frameInfo);

    protected void RaiseRenderBegin(CommonFrameInfo frameInfo) => RenderBegin?.Invoke(frameInfo);

    protected void RaiseRenderEnd(CommonFrameInfo frameInfo) => RenderEnd?.Invoke(frameInfo);

    protected void RaisePipelineStatesInvalidated() => PipelineStatesInvalidated?.Invoke();

    protected void RaiseCollectStatistics(RenderPipelineStats stats) => CollectStatistics?.Invoke(stats);
}
