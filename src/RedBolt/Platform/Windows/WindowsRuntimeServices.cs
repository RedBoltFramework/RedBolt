namespace RedBolt.Platform;

using System;

using RedBolt.Bindings.Sdl;
using RedBolt.Core;
using RedBolt.Engine;

public static class WindowsPlatform
{
    public static void Initialize()
    {
        var runtimeServices = new RuntimeServices(new WindowsPlatformRuntime(),
        [
            new D3D11GraphicsBackend(),
            new D3D12GraphicsBackend(),
            new OpenGLGraphicsBackend(),
            new VulkanGraphicsBackend()
        ]);
        RuntimeServices.SetDefault(runtimeServices);
        Context.SetInstance(new Context(runtimeServices));
    }
}

internal sealed class WindowsPlatformRuntime : IPlatformRuntime
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
        if (SdlNativeBindings.SdlGetWindowWMInfo(window, wmInfo) != SdlBool.SdlTrue)
            throw new InvalidOperationException("Generated SDL_GetWindowWMInfo binding failed for the window.");
        if (wmInfo.Subsystem != SdlSyswmType.SdlSyswmWindows || wmInfo.InfoWinWindow == IntPtr.Zero)
            throw new InvalidOperationException($"SDL Win32 WM info is invalid ({wmInfo.Subsystem}).");

        return new NativeWindowInfo(IntPtr.Zero, wmInfo.InfoWinWindow);
    }
}
