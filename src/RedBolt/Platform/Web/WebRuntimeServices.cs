namespace RedBolt;

using System;
using System.Runtime.InteropServices.JavaScript;

using RedBolt.Bindings.Sdl;
using RedBolt.Core;
using RedBolt.Engine;
using RedBolt.Platform;

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

internal sealed partial class WebPlatformRuntime : IPlatformRuntime
{
    public bool SupportsFileLogging => false;

    public bool UseHighDensityWindow => false;

    public void InitializeApplicationParameters(EngineParameters parameters)
    {
        parameters.WindowWidth = GetCanvasWidth();
        parameters.WindowHeight = GetCanvasHeight();
    }

    public NativeWindowInfo GetNativeWindowInfo(SdlWindow window) =>
        throw new PlatformNotSupportedException("Web does not expose native window handles.");

    [JSImport("canvas.getWidth", "main.js")]
    private static partial int GetCanvasWidth();

    [JSImport("canvas.getHeight", "main.js")]
    private static partial int GetCanvasHeight();
}
