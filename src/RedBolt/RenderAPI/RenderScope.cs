using System.Globalization;

namespace RedBolt;

/// <summary>
/// Utility class to add debug scope markers.
/// </summary>
public sealed class RenderScope : IDisposable
{
    private readonly RenderContext? _renderContext;

    public RenderScope(RenderContext? renderContext, string name)
    {
        this._renderContext = renderContext is not null && renderContext.IsDebugScopeEnabled() ? renderContext : null;
        if (this._renderContext is not null)
            this._renderContext.BeginGroup(name);
    }

    public RenderScope(RenderContext? renderContext, string format, params object[] args)
        : this(renderContext, string.Format(CultureInfo.InvariantCulture, format, args))
    {
    }

    public void Dispose()
    {
        _renderContext?.EndGroup();
    }
}
