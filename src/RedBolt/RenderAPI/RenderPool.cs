namespace RedBolt;

public class RenderPool : Object
{
    private readonly RenderDevice renderDevice;

    public RenderPool(RenderDevice renderDevice)
    {
        this.renderDevice = renderDevice ?? throw new ArgumentNullException(nameof(renderDevice));
    }

    public void Invalidate()
    {
    }

    public void Restore()
    {
    }

    public void OnFrameEnd()
    {
    }

    public RenderDevice GetRenderDevice() => renderDevice;
}
