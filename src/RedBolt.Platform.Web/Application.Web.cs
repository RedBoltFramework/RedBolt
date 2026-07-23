using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.JavaScript;
using System;

namespace RedBolt;

public abstract partial class Application
{
    private Action _runFrameAction = null!;

    private partial void InitializeMainLoop()
    {
        _runFrameAction = RunFrame;
    }

    private partial bool RunMainLoop()
    {
        SetMainLoop(_runFrameAction);
        return true;
    }

    private void RunFrame()
    {
        Engine.RunFrame(_updateAction);
    }

    [JSImport("mainLoop.set", "main.js")]
    private static partial void SetMainLoop(
        [JSMarshalAs<JSType.Function>] Action callback);
}

internal static class WebPlatformModule
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        WebPlatform.Initialize();
    }
}
