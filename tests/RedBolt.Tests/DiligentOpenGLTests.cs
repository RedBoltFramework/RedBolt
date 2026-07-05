using System.Reflection;
using System.Runtime.InteropServices;
using RedBolt;
using RedBolt.Bindings.Diligent;
using RedBolt.Bindings.Sdl;

namespace RedBolt.Tests;

public sealed class DiligentOpenGLTests
{
    private const uint SdlInitVideo = 0x00000020u;

    [Fact]
    public void Value_type_enum_is_generated()
    {
        var diligentAssembly = Assembly.Load("RedBolt.Bindings.Diligent");
        var valueTypeEnum = diligentAssembly.GetTypes()
            .First(type => type.IsEnum && type.GetField("VtFloat32") is not null);

        var vtFloat32 = Enum.Parse(valueTypeEnum, "VtFloat32");
        Assert.True(Convert.ToInt32(vtFloat32) > 0);
    }

    [Fact]
    public void Core_cpp_interface_methods_are_generated()
    {
        var diligentAssembly = Assembly.Load("RedBolt.Bindings.Diligent");
        var renderDevice = diligentAssembly.GetType("RedBolt.Bindings.Diligent.IRenderDevice")
            ?? throw new InvalidOperationException("IRenderDevice type was not found.");
        var deviceContext = diligentAssembly.GetType("RedBolt.Bindings.Diligent.IDeviceContext")
            ?? throw new InvalidOperationException("IDeviceContext type was not found.");

        Assert.NotNull(renderDevice.GetMethod("CreateBuffer"));
        Assert.NotNull(deviceContext.GetMethod("SetVertexBuffers"));
        Assert.NotNull(deviceContext.GetMethod("SetIndexBuffer"));
        Assert.NotNull(deviceContext.GetMethod("DrawIndexed"));
    }

    [Fact]
    public void Opengl_smoke_creates_device_and_renders()
    {
        if (!IsEnvFlagEnabled("REDBOLT_RUN_DILIGENT_GL_SMOKE"))
            return;

        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DISPLAY")))
            throw new InvalidOperationException("REDBOLT_RUN_DILIGENT_GL_SMOKE requires an X11 DISPLAY.");

        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SDL_VIDEODRIVER")))
            Environment.SetEnvironmentVariable("SDL_VIDEODRIVER", "x11");

        var result = SdlNativeBindings.SdlInit(SdlInitVideo);
        if (result != 0)
            throw new InvalidOperationException($"SDL_Init failed with {result}.");

        SdlWindow? window = null;
        nint glContext = 0;
        IRenderDevice? device = null;
        IDeviceContext? immediateContext = null;
        ISwapChain? swapChain = null;

        try
        {
            CheckSdlGl(SdlNativeBindings.SdlGlSetAttribute(SdlGLattr.SdlGlContextMajorVersion, 3));
            CheckSdlGl(SdlNativeBindings.SdlGlSetAttribute(SdlGLattr.SdlGlContextMinorVersion, 3));
            CheckSdlGl(SdlNativeBindings.SdlGlSetAttribute(SdlGLattr.SdlGlContextProfileMask,
                (int)SdlGLprofile.SdlGlContextProfileCore));
            CheckSdlGl(SdlNativeBindings.SdlGlSetAttribute(SdlGLattr.SdlGlDoublebuffer, 1));

            ReadOnlySpan<byte> emptyTitle = [0];
            window = SdlNativeBindings.SdlCreateWindow(
                emptyTitle, x: 0, y: 0, w: 64, h: 64,
                flags: (uint)(SdlWindowFlags.SdlWindowOpengl | SdlWindowFlags.SdlWindowHidden));

            if (window.IsNull)
                throw new InvalidOperationException("SDL_CreateWindow returned a null OpenGL window.");

            glContext = SdlNativeBindings.SdlGlCreateContext(window);
            if (glContext == 0)
                throw new InvalidOperationException("SDL_GL_CreateContext returned null.");

            CheckSdlGl(SdlNativeBindings.SdlGlMakeCurrent(window, glContext));
            _ = SdlNativeBindings.SdlGlSetSwapInterval(0);

            using var wmInfo = CreateWmInfo();
            if (SdlNativeBindings.SdlGetWindowWMInfo(window, wmInfo) != SdlBool.SdlTrue)
                throw new InvalidOperationException("SDL_GetWindowWMInfo failed.");

            if (wmInfo.Subsystem != SdlSyswmType.SdlSyswmX11)
                throw new InvalidOperationException($"Expected X11 WM info, got {wmInfo.Subsystem}.");

            using var engineCI = new EngineGLCreateInfo
            {
                WindowPDisplay = wmInfo.InfoX11Display,
                WindowWindowId = checked((uint)wmInfo.InfoX11Window)
            };
            using var swapChainDesc = new SwapChainDesc();

            var factory = DiligentNativeBindings.GetEngineFactoryOpenGL();
            if (factory.IsNull)
                throw new InvalidOperationException("GetEngineFactoryOpenGL returned null.");

            factory.CreateDeviceAndSwapChainGL(engineCI, out device, out immediateContext,
                swapChainDesc, out swapChain);
            if (device.IsNull || immediateContext.IsNull || swapChain.IsNull)
                throw new InvalidOperationException("CreateDeviceAndSwapChainGL returned null objects.");

            var rtv = swapChain.GetCurrentBackBufferRTV();
            if (rtv.IsNull)
                throw new InvalidOperationException("GetCurrentBackBufferRTV returned null.");

            ITextureView?[] renderTargets = [rtv];
            immediateContext.SetRenderTargets((uint)renderTargets.Length, renderTargets, null!,
                ResourceStateTransitionMode.ResourceStateTransitionModeTransition);

            var clearColor = new[] { 0.08f, 0.16f, 0.30f, 1.0f };
            var handle = GCHandle.Alloc(clearColor, GCHandleType.Pinned);
            try
            {
                immediateContext.ClearRenderTarget(rtv, handle.AddrOfPinnedObject(),
                    ResourceStateTransitionMode.ResourceStateTransitionModeTransition);
            }
            finally
            {
                handle.Free();
            }

            swapChain.Present(0);
            immediateContext.SetRenderTargets(0, Array.Empty<ITextureView?>(), null!,
                ResourceStateTransitionMode.ResourceStateTransitionModeNone);
        }
        finally
        {
            ReleaseIfNotNull(swapChain);
            ReleaseIfNotNull(immediateContext);
            ReleaseIfNotNull(device);

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

    private static bool IsEnvFlagEnabled(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return string.Equals(value, "1", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);
    }

    private static SdlSysWMinfo CreateWmInfo()
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

    private static void CheckSdlGl(int result)
    {
        if (result != 0)
            throw new InvalidOperationException($"SDL GL operation failed with {result}.");
    }

    private static void ReleaseIfNotNull(IObject? value)
    {
        if (value is not null && !value.IsNull)
            _ = value.Release();
    }
}
