using RedBolt.Bindings.Sdl;
using System;

namespace RedBolt;

public static class WebPlatform
{
    public static void Initialize()
    {
        var runtimeServices = new RuntimeServices(new WebPlatformRuntime(),
        [
            new OpenGLESGraphicsBackend()
        ]);
        RuntimeServices.SetDefault(runtimeServices);
        Context.SetInstance(new Context(runtimeServices));
    }
}

internal sealed class WebPlatformRuntime : IPlatformRuntime
{
    public bool SupportsFileLogging => false;

    public NativeWindowInfo GetNativeWindowInfo(SdlWindow window) =>
        throw new PlatformNotSupportedException("Web does not expose native window handles.");
}
