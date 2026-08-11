namespace RedBolt.Platform;

using System;
using System.IO;

using RedBolt.Bindings.Sdl;
using RedBolt.Core;
using RedBolt.Engine;

public static class AndroidPlatform
{
    public static void Initialize()
    {
        var runtimeServices = new RuntimeServices(new AndroidPlatformRuntime(),
        [
            new OpenGLESGraphicsBackend()
        ]);
        RuntimeServices.SetDefault(runtimeServices);
        Context.SetInstance(new Context(runtimeServices));
    }
}

internal sealed class AndroidPlatformRuntime : IPlatformRuntime
{
    public bool SupportsFileLogging => true;

    public bool UseHighDensityWindow => false;

    public void InitializeApplicationParameters(EngineParameters parameters)
    {
    }

    public void FinalizeApplicationParameters(EngineParameters parameters)
    {
        parameters.ApplicationPreferencesDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            parameters.ApplicationName) + Path.DirectorySeparatorChar;
        parameters.Borderless = false;
        parameters.EngineCliParameters = false;
        parameters.FullScreen = true;
        parameters.ResourcePrefixPaths = "/apk";
        parameters.WindowMaximize = false;
        parameters.WindowResizable = false;
    }

    public NativeWindowInfo GetNativeWindowInfo(SdlWindow window) =>
        throw new PlatformNotSupportedException("Android OpenGL ES uses SDL's active EGL context.");
}
