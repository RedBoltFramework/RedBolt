namespace RedBolt.Platform;

using System;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using System.Threading.Tasks;

using RedBolt.Engine;
using RedBolt.Graphics;

public abstract partial class Application
{
    private static Application _currentApplication = null!;

    private partial void InitializeMainLoop()
    {
        _currentApplication = this;
        SubscribeToEvent<GraphicsEvents.ScreenModeEvent>(HandleScreenMode);
    }

    private partial bool RunMainLoop()
    {
        SetCanvasResizeHandlers();
        SetMainLoop();
        return true;
    }

    private void RunFrame()
    {
        try
        {
            Engine.RunFrame(_updateAction);
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception.ToString());
            throw;
        }
    }

    [SupportedOSPlatform("browser")]
    [JSExport]
    internal static Task RunFrameAsync()
    {
        _currentApplication.RunFrame();
        return Task.CompletedTask;
    }

    [SupportedOSPlatform("browser")]
    [JSExport]
    internal static Task ResizeCanvasAsync(int width, int height, bool fullscreen, float dpiScale)
    {
        _currentApplication.Engine.OnCanvasResize(width, height, fullscreen, dpiScale);
        return Task.CompletedTask;
    }

    private static void HandleScreenMode(ref GraphicsEvents.ScreenModeEvent eventData)
    {
        SetRendererSize(eventData.Width, eventData.Height);
    }

    [JSImport("canvas.setResizeHandlers", "main.js")]
    private static partial void SetCanvasResizeHandlers();

    [JSImport("canvas.setRendererSize", "main.js")]
    private static partial void SetRendererSize(int width, int height);

    [JSImport("mainLoop.set", "main.js")]
    private static partial void SetMainLoop();
}
