// Copyright (c) 2026 RedBolt and contributors.
// This work is licensed under the terms of the MIT license.
// For a copy, see <https://opensource.org/licenses/MIT> or the accompanying LICENSE file.

namespace RedBolt.RenderAPI;

using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

using RedBolt.Bindings.Sdl;
using RedBolt.Container;

/// <summary>
/// Utility class to add debug scope markers.
/// </summary>
public class RenderScope : IDisposable
{
    private RenderScope()
    {
    }

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
            BeginGroup(format, arg, null);
    }

    public RenderScope(RenderContext? renderContext, string format, int arg0, string arg1)
    {
        _renderContext = renderContext is not null && renderContext.IsDebugScopeEnabled() ? renderContext : null;
        if (_renderContext is not null)
            BeginGroup(format, arg0, arg1);
    }

    public static RenderScope? Acquire(RenderContext? renderContext, string name)
    {
        if (renderContext is null || !renderContext.IsDebugScopeEnabled())
            return null;

        var renderScope = _freeRenderScope ?? new RenderScope();
        _freeRenderScope = renderScope._nextFreeRenderScope;
        renderScope._nextFreeRenderScope = null;
        renderScope._renderContext = renderContext;
        renderScope._isPooled = true;
        renderScope.BeginGroup(name);
        return renderScope;
    }

    public static RenderScope? Acquire(RenderContext? renderContext, string format, int arg)
    {
        if (renderContext is null || !renderContext.IsDebugScopeEnabled())
            return null;

        var renderScope = _freeRenderScope ?? new RenderScope();
        _freeRenderScope = renderScope._nextFreeRenderScope;
        renderScope._nextFreeRenderScope = null;
        renderScope._renderContext = renderContext;
        renderScope._isPooled = true;
        renderScope.BeginGroup(format, arg, null);
        return renderScope;
    }

    public static RenderScope? Acquire(RenderContext? renderContext, string format, int arg0, string arg1)
    {
        if (renderContext is null || !renderContext.IsDebugScopeEnabled())
            return null;

        var renderScope = _freeRenderScope ?? new RenderScope();
        _freeRenderScope = renderScope._nextFreeRenderScope;
        renderScope._nextFreeRenderScope = null;
        renderScope._renderContext = renderContext;
        renderScope._isPooled = true;
        renderScope.BeginGroup(format, arg0, arg1);
        return renderScope;
    }

    public void Dispose()
    {
        if (_renderContext is not null)
            EndGroup();

        _renderContext = null;
        if (_isPooled)
        {
            _isPooled = false;
            _nextFreeRenderScope = _freeRenderScope;
            _freeRenderScope = this;
        }
    }

    private void BeginGroup(string format, int arg0, string? arg1)
    {
        Span<char> name = stackalloc char[format.Length + 32 + (arg1?.Length ?? 0)];
        var nameLength = 0;
        for (var i = 0; i < format.Length; ++i)
        {
            if (format[i] == '{' && i + 2 < format.Length && format[i + 2] == '}')
            {
                if (format[i + 1] == '0')
                {
                    arg0.TryFormat(name[nameLength..], out var written, default, CultureInfo.InvariantCulture);
                    nameLength += written;
                    i += 2;
                    continue;
                }
                if (format[i + 1] == '1' && arg1 is not null)
                {
                    arg1.AsSpan().CopyTo(name[nameLength..]);
                    nameLength += arg1.Length;
                    i += 2;
                    continue;
                }
            }
            name[nameLength++] = format[i];
        }
        BeginGroup(name[..nameLength]);
    }

    private unsafe void BeginGroup(string name) => BeginGroup(name.AsSpan());

    private unsafe void BeginGroup(scoped ReadOnlySpan<char> name)
    {
        var handle = _renderContext!.GetHandle();
        Span<byte> nativeName = stackalloc byte[Encoding.UTF8.GetMaxByteCount(name.Length) + 1];
        var nativeNameLength = Encoding.UTF8.GetBytes(name, nativeName);
        nativeName[nativeNameLength] = 0;
        Span<float> color = stackalloc float[4];
        ref var firstCharacter = ref Unsafe.As<byte, sbyte>(ref MemoryMarshal.GetReference(nativeName));
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
        if (renderDevice.GetBackend() == RenderBackend.OpenGL)
        {
            if (_glGetError is null)
            {
                using var functionName = new CString("glGetError");
                var function = Sdl.SdlGlGetProcAddress(functionName.Bytes);
                _glGetError = Marshal.GetDelegateForFunctionPointer<OpenGlGetErrorFunction>(function);
            }

            _ = _glGetError();
        }
    }

    private RenderContext? _renderContext;
    private bool _isPooled;
    private RenderScope? _nextFreeRenderScope;

    [ThreadStatic]
    private static RenderScope? _freeRenderScope;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate uint OpenGlGetErrorFunction();

    private static OpenGlGetErrorFunction? _glGetError;
}
