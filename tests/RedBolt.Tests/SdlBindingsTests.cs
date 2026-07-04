using RedBolt.Bindings.Sdl;

namespace RedBolt.Tests;

public sealed class SdlBindingsTests
{
    private const uint SdlInitVideo = 0x00000020u;

    private static bool SdlAvailable
    {
        get
        {
            try { _ = SdlNativeBindings.SdlInit(0); SdlNativeBindings.SdlQuit(); return true; }
            catch (DllNotFoundException) { return false; }
        }
    }

    [Fact]
    public void Init_and_was_init_work()
    {
        if (!SdlAvailable) return;
        EnsureDummyVideoDriver();
        var result = SdlNativeBindings.SdlInit(SdlInitVideo);
        try
        {
            Assert.Equal(0, result);
            var initialized = SdlNativeBindings.SdlWasInit(SdlInitVideo);
            Assert.NotEqual(0u, initialized & SdlInitVideo);
        }
        finally
        {
            SdlNativeBindings.SdlQuit();
        }
    }

    [Fact]
    public void CreateWindow_returns_valid_window()
    {
        if (!SdlAvailable) return;
        EnsureDummyVideoDriver();
        var result = SdlNativeBindings.SdlInit(SdlInitVideo);
        Assert.Equal(0, result);
        try
        {
            sbyte emptyTitle = 0;
            using var window = SdlNativeBindings.SdlCreateWindow(
                in emptyTitle, x: 0, y: 0, w: 16, h: 16,
                flags: (uint)SdlWindowFlags.SdlWindowHidden);
            Assert.False(window.IsNull);
        }
        finally
        {
            SdlNativeBindings.SdlQuit();
        }
    }

    [Fact]
    public void PollEvent_works()
    {
        if (!SdlAvailable) return;
        EnsureDummyVideoDriver();
        var result = SdlNativeBindings.SdlInit(SdlInitVideo);
        Assert.Equal(0, result);
        try
        {
            sbyte emptyTitle = 0;
            using var window = SdlNativeBindings.SdlCreateWindow(
                in emptyTitle, x: 0, y: 0, w: 16, h: 16,
                flags: (uint)SdlWindowFlags.SdlWindowHidden);
            Assert.False(window.IsNull);
            using var @event = new SdlEvent();
            _ = SdlNativeBindings.SdlPollEvent(@event);
        }
        finally
        {
            SdlNativeBindings.SdlQuit();
        }
    }

    private static void EnsureDummyVideoDriver()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SDL_VIDEODRIVER")))
            Environment.SetEnvironmentVariable("SDL_VIDEODRIVER", "dummy");
    }
}
