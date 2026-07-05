using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace RedBolt.Bindings.Sdl;

public static partial class SdlNativeBindings
{
    /// <summary>Create a window with a UTF-8 encoded title pointer.</summary>
    public static SdlWindow SdlCreateWindowUtf8(in sbyte title, int x, int y, int w, int h, uint flags)
    {
        var handle = SdlCreateWindowUtf8Core(in title, x, y, w, h, flags);
        return SdlWindow.CreateManagedWrapper(handle, ownsHandle: false);
    }

    /// <summary>Look up an OpenGL procedure by UTF-8 encoded name.</summary>
    public static IntPtr SdlGlGetProcAddressUtf8(in sbyte proc)
    {
        return SdlGlGetProcAddressUtf8Core(in proc);
    }

    /// <summary>Set the window title from a UTF-8 encoded string pointer.</summary>
    public static void SdlSetWindowTitleUtf8(SdlWindow window, in sbyte title)
    {
        SdlSetWindowTitleUtf8Core(window.Handle, in title);
        GC.KeepAlive(window);
    }

    [LibraryImport("RedBolt.Native", EntryPoint = "redbolt_sdl_SDL_CreateWindow_389", StringMarshalling = StringMarshalling.Utf8)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static partial IntPtr SdlCreateWindowUtf8Core(in sbyte title, int x, int y, int w, int h, uint flags);

    [LibraryImport("RedBolt.Native", EntryPoint = "redbolt_sdl_SDL_GL_GetProcAddress_445", StringMarshalling = StringMarshalling.Utf8)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static partial IntPtr SdlGlGetProcAddressUtf8Core(in sbyte proc);

    [LibraryImport("RedBolt.Native", EntryPoint = "redbolt_sdl_SDL_SetWindowTitle_394", StringMarshalling = StringMarshalling.Utf8)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static partial void SdlSetWindowTitleUtf8Core(IntPtr window, in sbyte title);
}
