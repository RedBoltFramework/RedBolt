using RedBolt.Bindings.Sdl;
using System.Runtime.InteropServices;
using System.Text;

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

    [Fact]
    public void GlGetProcAddress_utf8_wrapper_resolves_real_symbol()
    {
        if (!SdlAvailable) return;

        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DISPLAY")))
            return;

        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SDL_VIDEODRIVER")))
            Environment.SetEnvironmentVariable("SDL_VIDEODRIVER", "x11");

        var result = SdlNativeBindings.SdlInit(SdlInitVideo);
        Assert.Equal(0, result);

        SdlWindow? window = null;
        nint glContext = 0;
        try
        {
            CheckSdlGl(SdlNativeBindings.SdlGlSetAttribute(SdlGLattr.SdlGlContextMajorVersion, 3));
            CheckSdlGl(SdlNativeBindings.SdlGlSetAttribute(SdlGLattr.SdlGlContextMinorVersion, 3));
            CheckSdlGl(SdlNativeBindings.SdlGlSetAttribute(SdlGLattr.SdlGlContextProfileMask, (int)SdlGLprofile.SdlGlContextProfileCore));
            CheckSdlGl(SdlNativeBindings.SdlGlSetAttribute(SdlGLattr.SdlGlDoublebuffer, 1));

            using var title = PinUtf8("RedBolt Tests");
            window = SdlNativeBindings.SdlCreateWindowUtf8(
                in title.FirstByte, x: 0, y: 0, w: 16, h: 16,
                flags: (uint)(SdlWindowFlags.SdlWindowHidden | SdlWindowFlags.SdlWindowOpengl));
            Assert.False(window.IsNull);

            glContext = SdlNativeBindings.SdlGlCreateContext(window);
            Assert.NotEqual(IntPtr.Zero, glContext);
            CheckSdlGl(SdlNativeBindings.SdlGlMakeCurrent(window, glContext));

            using var proc = PinUtf8("glFinish");
            var address = SdlNativeBindings.SdlGlGetProcAddressUtf8(in proc.FirstByte);
            Assert.NotEqual(IntPtr.Zero, address);
        }
        finally
        {
            if (glContext != 0)
            {
                if (window is not null && !window.IsNull)
                    _ = SdlNativeBindings.SdlGlMakeCurrent(window, IntPtr.Zero);
                SdlNativeBindings.SdlGlDeleteContext(glContext);
            }

            if (window is not null && !window.IsNull)
                SdlNativeBindings.SdlDestroyWindow(window);

            SdlNativeBindings.SdlQuit();
        }
    }

    private static void EnsureDummyVideoDriver()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SDL_VIDEODRIVER")))
            Environment.SetEnvironmentVariable("SDL_VIDEODRIVER", "dummy");
    }

    private static void CheckSdlGl(int result)
    {
        if (result != 0)
            throw new InvalidOperationException($"SDL GL operation failed with {result}.");
    }

    private static Utf8Pin PinUtf8(string value) => new(value);

    private sealed class Utf8Pin : IDisposable
    {
        private readonly byte[] bytes;
        private readonly GCHandle handle;

        public Utf8Pin(string value)
        {
            bytes = Encoding.UTF8.GetBytes(value + '\0');
            handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
        }

        public ref sbyte FirstByte => ref MemoryMarshal.GetReference(MemoryMarshal.Cast<byte, sbyte>(bytes.AsSpan()));

        public void Dispose()
        {
            if (handle.IsAllocated)
                handle.Free();
        }
    }
}
