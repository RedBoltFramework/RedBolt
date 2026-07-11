using System.Globalization;
using System.Runtime.CompilerServices;

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
            BeginGroup(name);
    }

    public RenderScope(RenderContext? renderContext, string format, params object[] args)
        : this(renderContext, string.Format(CultureInfo.InvariantCulture, format, args))
    {
    }

    public void Dispose()
    {
        if (_renderContext is not null)
            EndGroup();
    }

    private unsafe void BeginGroup(string name)
    {
        var handle = _renderContext!.GetHandle();
        if (handle is null || handle.IsNull)
            return;
        using var nativeName = new CString(name);
        Span<float> color = stackalloc float[4];
        ref var firstCharacter = ref Unsafe.AsRef<sbyte>((void*)nativeName.Pointer);
        handle.BeginDebugGroup(in firstCharacter, in color[0]);
    }

    private void EndGroup()
    {
        var handle = _renderContext!.GetHandle();
        if (handle is { IsNull: false })
            handle.EndDebugGroup();
    }
}
