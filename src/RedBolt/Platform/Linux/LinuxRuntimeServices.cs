namespace RedBolt.Platform;

using System;

using RedBolt.Bindings.Sdl;
using RedBolt.Core;
using RedBolt.Engine;

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

    public bool UseHighDensityWindow => false;

        public void InitializeApplicationParameters(EngineParameters parameters)
        {
        }

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
        return ValidateNativeWindowInfo(SdlNativeBindings.SdlGetWindowWMInfo(window, wmInfo), wmInfo);
    }

    private static NativeWindowInfo ValidateNativeWindowInfo(SdlBool result, SdlSysWMinfo wmInfo)
    {
        if (result != SdlBool.SdlTrue)
            throw new InvalidOperationException("Generated SDL_GetWindowWMInfo binding failed for the window.");

        if (wmInfo.Subsystem != SdlSyswmType.SdlSyswmX11)
            throw new InvalidOperationException($"RedBolt requires SDL X11 WM info, got {wmInfo.Subsystem}.");

        if (wmInfo.InfoX11Display == IntPtr.Zero || wmInfo.InfoX11Window == 0)
            throw new InvalidOperationException("SDL X11 WM info did not contain display and window handles.");

        return new NativeWindowInfo(wmInfo.InfoX11Display, checked((nint)wmInfo.InfoX11Window));
    }
}
