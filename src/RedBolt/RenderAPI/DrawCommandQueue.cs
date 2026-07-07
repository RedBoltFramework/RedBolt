namespace RedBolt;

public class DrawCommandQueue : Object
{
    private readonly RenderDevice renderDevice;
    private uint clipPlaneMask;

    public DrawCommandQueue(RenderDevice renderDevice)
    {
        this.renderDevice = renderDevice ?? throw new ArgumentNullException(nameof(renderDevice));
    }

    public void Reset()
    {
    }

    public void SetClipPlaneMask(uint mask)
    {
        clipPlaneMask = mask;
    }

    public uint GetClipPlaneMask() => clipPlaneMask;

    public RenderDevice GetRenderDevice() => renderDevice;

    public virtual void ExecuteInContext(RenderContext renderContext)
    {
        ArgumentNullException.ThrowIfNull(renderContext);
        renderContext.SetClipPlaneMask(clipPlaneMask);
    }
}
