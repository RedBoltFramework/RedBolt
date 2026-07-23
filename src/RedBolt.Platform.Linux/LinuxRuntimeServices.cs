using RedBolt.Bindings.Sdl;
using System;

namespace RedBolt;

public static class LinuxPlatform
{
    public static void Initialize()
    {
        var runtimeServices = new RuntimeServices(new LinuxPlatformRuntime(),
        [
            new OpenGLGraphicsBackend(),
            new VulkanGraphicsBackend()
        ]);
        RuntimeServices.SetDefault(runtimeServices);
        Context.SetInstance(new Context(runtimeServices));
    }
}

internal sealed class LinuxPlatformRuntime : IPlatformRuntime
{
    public bool SupportsFileLogging => true;

    public NativeWindowInfo GetNativeWindowInfo(SdlWindow window)
    {
        using var version = new SdlVersion();
        SdlNativeBindings.SdlGetVersion(version);

        using var wmInfo = new SdlSysWMinfo
        {
            VersionMajor = version.Major,
            VersionMinor = version.Minor,
            VersionPatch = version.Patch
        };
        if (SdlNativeBindings.SdlGetWindowWMInfo(window, wmInfo) != SdlBool.SdlTrue)
            throw new InvalidOperationException("Generated SDL_GetWindowWMInfo binding failed for the window.");

        if (wmInfo.Subsystem != SdlSyswmType.SdlSyswmX11)
            throw new InvalidOperationException($"RedBolt requires SDL X11 WM info, got {wmInfo.Subsystem}.");

        if (wmInfo.InfoX11Display == IntPtr.Zero || wmInfo.InfoX11Window == 0)
            throw new InvalidOperationException("SDL X11 WM info did not contain display and window handles.");

        return new NativeWindowInfo(wmInfo.InfoX11Display, checked((uint)wmInfo.InfoX11Window));
    }
}
