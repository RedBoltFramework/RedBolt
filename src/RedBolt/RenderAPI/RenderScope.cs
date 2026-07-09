using System.Globalization;

namespace RedBolt;

/// <summary>
/// Utility class to add debug scope markers.
/// </summary>
public sealed class RenderScope : IDisposable
{
    private readonly RenderContext? renderContext;

    public RenderScope(RenderContext? renderContext, string name)
    {
        this.renderContext = renderContext is not null && renderContext.IsDebugScopeEnabled() ? renderContext : null;
        if (this.renderContext is not null)
            this.renderContext.BeginGroup(name);
    }

    public RenderScope(RenderContext? renderContext, string format, params object[] args)
        : this(renderContext, string.Format(CultureInfo.InvariantCulture, format, args))
    {
    }

    public void Dispose()
    {
        renderContext?.EndGroup();
    }
}
