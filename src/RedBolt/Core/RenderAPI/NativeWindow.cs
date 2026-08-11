namespace RedBolt.RenderAPI;

using RedBolt.Bindings.Sdl;

public readonly record struct NativeWindowInfo(nint Display, nint Window)
{
    public uint WindowId => checked((uint)Window);
}

internal static class NativeWindow
{
    public static NativeWindowInfo GetInfo(SdlWindow window)
    {
        using var version = new SdlVersion();
        SdlNativeBindings.SdlGetVersion(version);

        using var wmInfo = new SdlSysWMinfo
        {
            VersionMajor = version.Major,
            VersionMinor = version.Minor,
            VersionPatch = version.Patch
        };
        var result = SdlNativeBindings.SdlGetWindowWMInfo(window, wmInfo);
#if REDBOLT_PLATFORM_LINUX
        return ValidateLinuxInfo(result, wmInfo);
#elif REDBOLT_PLATFORM_WINDOWS
        return ValidateWindowsInfo(result, wmInfo);
#else
        throw new PlatformNotSupportedException("The current graphics backend does not use native window handles.");
#endif
    }

#if REDBOLT_PLATFORM_LINUX
    internal static NativeWindowInfo ValidateLinuxInfo(SdlBool result, SdlSysWMinfo wmInfo)
    {
        if (result != SdlBool.SdlTrue)
            throw new InvalidOperationException("Generated SDL_GetWindowWMInfo binding failed for the window.");
        if (wmInfo.Subsystem != SdlSyswmType.SdlSyswmX11)
            throw new InvalidOperationException($"RedBolt requires SDL X11 WM info, got {wmInfo.Subsystem}.");
        if (wmInfo.InfoX11Display == IntPtr.Zero || wmInfo.InfoX11Window == 0)
            throw new InvalidOperationException("SDL X11 WM info did not contain display and window handles.");

        return new NativeWindowInfo(wmInfo.InfoX11Display, checked((nint)wmInfo.InfoX11Window));
    }
#elif REDBOLT_PLATFORM_WINDOWS
    internal static NativeWindowInfo ValidateWindowsInfo(SdlBool result, SdlSysWMinfo wmInfo)
    {
        if (result != SdlBool.SdlTrue)
            throw new InvalidOperationException("Generated SDL_GetWindowWMInfo binding failed for the window.");
        if (wmInfo.Subsystem != SdlSyswmType.SdlSyswmWindows || wmInfo.InfoWinWindow == IntPtr.Zero)
            throw new InvalidOperationException($"SDL Win32 WM info is invalid ({wmInfo.Subsystem}).");

        return new NativeWindowInfo(IntPtr.Zero, wmInfo.InfoWinWindow);
    }
#endif
}
