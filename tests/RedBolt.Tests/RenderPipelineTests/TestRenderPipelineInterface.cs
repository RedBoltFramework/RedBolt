namespace RedBolt.Tests;

internal sealed class TestRenderPipelineInterface : IRenderPipelineInterface
{
    public static RenderBufferManager CreateRenderBufferManager(TestRenderPipelineInterface? pipeline = null)
    {
        return new RenderBufferManager(pipeline ?? new TestRenderPipelineInterface());
    }

    public Context? GetContext() => Context.Instance;

    public RenderPipelineDebugger GetDebugger() => null!;

    public bool IsLinearColorSpace { get; set; }

    public event Action<CommonFrameInfo>? UpdateBegin;
    public event Action<CommonFrameInfo>? UpdateEnd;
    public event Action<CommonFrameInfo>? RenderBegin;
    public event Action<CommonFrameInfo>? RenderEnd;
    public event Action? PipelineStatesInvalidated;
    public event RenderPipelineStatsHandler? CollectStatistics;

    public CommonFrameInfo BeginFrame(RenderBufferManager manager, RenderSurface? renderTarget, IntRect viewportRect)
    {
        manager.OnViewportDefined(renderTarget, viewportRect);
        var frameInfo = new CommonFrameInfo
        {
            RenderTarget = renderTarget,
            ViewportRect = viewportRect,
            ViewportSize = viewportRect.Size
        };
        RaiseRenderBegin(frameInfo);
        return frameInfo;
    }

    public void RaiseRenderBegin(CommonFrameInfo frameInfo) => RenderBegin?.Invoke(frameInfo);

    public void EndFrame(CommonFrameInfo frameInfo) => RenderEnd?.Invoke(frameInfo);
}
