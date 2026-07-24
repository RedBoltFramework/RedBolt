using System.Runtime.InteropServices.JavaScript;
using System;

namespace RedBolt;

public abstract partial class Application
{
    private Action _runFrameAction = null!;
    private Action<int, int, float> _canvasResizeAction = null!;
    private Action<int, int, float> _fullscreenResizeAction = null!;

    private partial void InitializeMainLoop()
    {
        _runFrameAction = RunFrame;
        _canvasResizeAction = (width, height, dpiScale) =>
            Engine.OnCanvasResize(width, height, false, dpiScale);
        _fullscreenResizeAction = (width, height, dpiScale) =>
            Engine.OnCanvasResize(width, height, true, dpiScale);
        SubscribeToEvent<GraphicsEvents.ScreenModeEvent>(HandleScreenMode);
    }

    private partial bool RunMainLoop()
    {
        SetCanvasResizeHandlers(_canvasResizeAction, _fullscreenResizeAction);
        SetMainLoop(_runFrameAction);
        return true;
    }

    private void RunFrame()
    {
        Engine.RunFrame(_updateAction);
    }

    private static void HandleScreenMode(ref GraphicsEvents.ScreenModeEvent eventData)
    {
        SetRendererSize(eventData.Width, eventData.Height);
    }

    [JSImport("canvas.setResizeHandlers", "main.js")]
    private static partial void SetCanvasResizeHandlers(
        [JSMarshalAs<JSType.Function<JSType.Number, JSType.Number, JSType.Number>>]
        Action<int, int, float> resizeCallback,
        [JSMarshalAs<JSType.Function<JSType.Number, JSType.Number, JSType.Number>>]
        Action<int, int, float> fullscreenResizeCallback);

    [JSImport("canvas.setRendererSize", "main.js")]
    private static partial void SetRendererSize(int width, int height);

    [JSImport("mainLoop.set", "main.js")]
    private static partial void SetMainLoop(
        [JSMarshalAs<JSType.Function>] Action callback);
}
