using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using RedBolt.Bindings.Sdl;

namespace RedBolt;

/// <summary>
/// Utility class to add debug scope markers.
/// </summary>
public class RenderScope : IDisposable
{
    public RenderScope(RenderContext? renderContext, string name)
    {
        _renderContext = renderContext is not null && renderContext.IsDebugScopeEnabled() ? renderContext : null;
        if (_renderContext is not null)
            BeginGroup(name);
    }

    public RenderScope(RenderContext? renderContext, string format, int arg)
    {
        _renderContext = renderContext is not null && renderContext.IsDebugScopeEnabled() ? renderContext : null;
        if (_renderContext is not null)
            BeginGroup(string.Format(CultureInfo.InvariantCulture, format, arg));
    }

    public RenderScope(RenderContext? renderContext, string format, int arg0, string arg1)
    {
        _renderContext = renderContext is not null && renderContext.IsDebugScopeEnabled() ? renderContext : null;
        if (_renderContext is not null)
            BeginGroup(string.Format(CultureInfo.InvariantCulture, format, arg0, arg1));
    }

    public void Dispose()
    {
        if (_renderContext is not null)
            EndGroup();
    }

    private unsafe void BeginGroup(string name)
    {
        var handle = _renderContext!.GetHandle();
        using var nativeName = new CString(name);
        Span<float> color = stackalloc float[4];
        ref var firstCharacter = ref Unsafe.AsRef<sbyte>((void*)nativeName.Pointer);
        // TODO: port: renderContext_->GetHandle()->BeginDebugGroup(name.data());
        // Generated binding cannot express the optional null color pointer.
        handle!.BeginDebugGroup(in firstCharacter, in color[0]);
        ConsumeOpenGlError(_renderContext.GetRenderDevice());
    }

    private void EndGroup()
    {
        var handle = _renderContext!.GetHandle();
        handle!.EndDebugGroup();
        ConsumeOpenGlError(_renderContext.GetRenderDevice());
    }

    private static void ConsumeOpenGlError(RenderDevice renderDevice)
    {
#if REDBOLT_GL_SUPPORTED || REDBOLT_GLES_SUPPORTED
        if (renderDevice.GetBackend() == RenderBackend.OpenGL)
        {
            if (_glGetError is null)
            {
                using var functionName = new CString("glGetError");
                var function = SdlNativeBindings.SdlGlGetProcAddress(functionName.Bytes);
                _glGetError = Marshal.GetDelegateForFunctionPointer<OpenGlGetErrorFunction>(function);
            }

            _ = _glGetError();
        }
#endif
    }

    private readonly RenderContext? _renderContext;

#if REDBOLT_GL_SUPPORTED || REDBOLT_GLES_SUPPORTED
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate uint OpenGlGetErrorFunction();

    private static OpenGlGetErrorFunction? _glGetError;
#endif
}
