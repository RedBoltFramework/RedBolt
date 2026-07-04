namespace RedBolt;

public sealed class Renderer
{
    private readonly Dictionary<uint, Viewport> viewports = [];

    public Viewport? GetViewport(uint index = 0)
    {
        return viewports.GetValueOrDefault(index);
    }

    public void SetViewport(uint index, Viewport viewport)
    {
        viewports[index] = viewport ?? throw new ArgumentNullException(nameof(viewport));
    }

    public void SetViewport(Viewport viewport)
    {
        SetViewport(0, viewport);
    }

    public void ClearViewports()
    {
        viewports.Clear();
    }

    internal void RenderFrame(Graphics graphics)
    {
        ArgumentNullException.ThrowIfNull(graphics);

        if (viewports.TryGetValue(0, out var viewport))
            graphics.RenderViewport(viewport);
    }
}
