using RedBolt.Bindings.Sdl;

namespace RedBolt;

public sealed class InputSystem
{
    private const uint SdlInitVideo = 0x00000020u;

    private SdlEvent? nativeEvent;
    private SdlWindow? window;

    public bool ExitRequested { get; private set; }

    public bool IsInitialized { get; private set; }

    internal SdlWindow Window => window is not null && !window.IsNull
        ? window
        : throw new InvalidOperationException("Input window is not initialized.");

    public void Initialize(EngineParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        if (IsInitialized)
            throw new InvalidOperationException("Input system is already initialized.");

        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DISPLAY")))
            throw new InvalidOperationException("RedBolt OpenGL first-cube path requires an X11 DISPLAY.");

        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SDL_VIDEODRIVER")))
            Environment.SetEnvironmentVariable("SDL_VIDEODRIVER", "x11");

        var initResult = SdlNativeBindings.SdlInit(SdlInitVideo);
        if (initResult != 0)
            throw new InvalidOperationException($"Generated SDL_Init binding failed with {initResult}.");

        try
        {
            CheckSdlGl(SdlNativeBindings.SdlGlSetAttribute(SdlGLattr.SdlGlContextMajorVersion, 3), "SDL_GL_SetAttribute major version");
            CheckSdlGl(SdlNativeBindings.SdlGlSetAttribute(SdlGLattr.SdlGlContextMinorVersion, 3), "SDL_GL_SetAttribute minor version");
            CheckSdlGl(SdlNativeBindings.SdlGlSetAttribute(SdlGLattr.SdlGlContextProfileMask, (int)SdlGLprofile.SdlGlContextProfileCore), "SDL_GL_SetAttribute profile mask");
            CheckSdlGl(SdlNativeBindings.SdlGlSetAttribute(SdlGLattr.SdlGlDoublebuffer, 1), "SDL_GL_SetAttribute doublebuffer");

            using var title = new PinnedUtf8(parameters.WindowTitle);
            window = SdlNativeBindings.SdlCreateWindow(
                in title.FirstByte,
                x: 64,
                y: 64,
                w: parameters.WindowWidth,
                h: parameters.WindowHeight,
                flags: (uint)(SdlWindowFlags.SdlWindowOpengl | SdlWindowFlags.SdlWindowShown));

            if (window.IsNull)
                throw new InvalidOperationException("Generated SDL_CreateWindow binding returned a null OpenGL window.");

            nativeEvent = new SdlEvent();
            IsInitialized = true;
        }
        catch
        {
            Shutdown();
            throw;
        }
    }

    public void BeginFrame()
    {
        ExitRequested = false;
    }

    public void PollEvents()
    {
        if (!IsInitialized || nativeEvent is null)
            return;

        while (SdlNativeBindings.SdlPollEvent(nativeEvent) != 0)
        {
            var eventType = (SdlEventType)nativeEvent.Type;
            if (eventType == SdlEventType.SdlQuit)
            {
                ExitRequested = true;
            }
            else if (eventType == SdlEventType.SdlKeydown && nativeEvent.KeyKeysymScancode == SdlScancode.SdlScancodeEscape)
            {
                ExitRequested = true;
            }
            else if (eventType == SdlEventType.SdlWindowevent && nativeEvent.WindowEvent == (byte)SdlWindowEventID.SdlWindoweventClose)
            {
                ExitRequested = true;
            }
        }
    }

    internal NativeWindowInfo GetNativeWindowInfo()
    {
        using var wmInfo = CreateInitializedWmInfo();
        if (SdlNativeBindings.SdlGetWindowWMInfo(Window, wmInfo) != SdlBool.SdlTrue)
            throw new InvalidOperationException("Generated SDL_GetWindowWMInfo binding failed for the OpenGL window.");

        if (wmInfo.Subsystem != SdlSyswmType.SdlSyswmX11)
            throw new InvalidOperationException($"RedBolt OpenGL first-cube path requires SDL X11 WM info, got {wmInfo.Subsystem}.");

        if (wmInfo.InfoX11Display == IntPtr.Zero || wmInfo.InfoX11Window == 0)
            throw new InvalidOperationException("SDL X11 WM info did not contain display and window handles.");

        return new NativeWindowInfo(wmInfo.InfoX11Display, checked((uint)wmInfo.InfoX11Window));
    }

    public void Shutdown()
    {
        nativeEvent?.Dispose();
        nativeEvent = null;

        if (window is not null && !window.IsNull)
            SdlNativeBindings.SdlDestroyWindow(window);
        window = null;

        if (IsInitialized)
            SdlNativeBindings.SdlQuit();

        IsInitialized = false;
        ExitRequested = false;
    }

    private static SdlSysWMinfo CreateInitializedWmInfo()
    {
        using var version = new SdlVersion();
        SdlNativeBindings.SdlGetVersion(version);

        return new SdlSysWMinfo
        {
            VersionMajor = version.Major,
            VersionMinor = version.Minor,
            VersionPatch = version.Patch
        };
    }

    private static void CheckSdlGl(int result, string operation)
    {
        if (result != 0)
            throw new InvalidOperationException($"{operation} failed with {result}.");
    }

    internal readonly record struct NativeWindowInfo(IntPtr Display, uint WindowId);
}
