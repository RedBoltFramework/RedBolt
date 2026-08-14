#if REDBOLT_PLATFORM_LINUX || REDBOLT_PLATFORM_WINDOWS
namespace RedBolt.RenderAPI.Backends;

using System;

using RedBolt.Bindings.Diligent;
using RedBolt.Bindings.Sdl;
using RedBolt.Core;
using RedBolt.Engine;
using RedBolt.Math;
using RedBolt.RenderAPI;

public interface IOpenGLGraphicsApi
{
    nint Context { get; }
}

/// <summary>Desktop OpenGL graphics backend.</summary>
public sealed class OpenGLGraphicsBackend : IGraphicsBackend
{
    public RenderBackend Backend => RenderBackend.OpenGL;

    public bool UsesOpenGLWindow => true;

    public void ConfigureWindow()
    {
        CheckSdl(Sdl.SdlGlSetAttribute(SdlGLattr.SdlGlDoublebuffer, 1), "SDL_GL_SetAttribute doublebuffer");
        CheckSdl(Sdl.SdlGlSetAttribute(SdlGLattr.SdlGlContextMajorVersion, 4), "SDL_GL_SetAttribute major version");
        CheckSdl(Sdl.SdlGlSetAttribute(SdlGLattr.SdlGlContextMinorVersion, 1), "SDL_GL_SetAttribute minor version");
        CheckSdl(Sdl.SdlGlSetAttribute(SdlGLattr.SdlGlContextProfileMask,
            (int)SdlGLprofile.SdlGlContextProfileCore), "SDL_GL_SetAttribute profile mask");
    }

    public GraphicsBackendResources Initialize(EngineParameters parameters, RenderDeviceSettings deviceSettings,
        WindowSettings windowSettings, SdlWindow window)
    {
        var glContext = Sdl.SdlGlCreateContext(window);
        if (glContext == IntPtr.Zero)
            throw new InvalidOperationException("Generated SDL_GL_CreateContext binding returned a null context.");

        try
        {
            CheckSdl(Sdl.SdlGlMakeCurrent(window, glContext), "SDL_GL_MakeCurrent");
            _ = Sdl.SdlGlSetSwapInterval(parameters.VSync ? 1 : 0);
            Sdl.SdlGlGetDrawableSize(window, out var width, out var height);

            var multiSample = (int)System.Math.Clamp(
                MathDefs.ClosestPowerOfTwo((uint)System.Math.Max(parameters.MultiSample, 1)), 1u, 16u);
            if (Sdl.SdlGlGetAttribute(SdlGLattr.SdlGlMultisamplesamples,
                    out var effectiveMultiSample) == 0)
                multiSample = System.Math.Max(1, effectiveMultiSample);
            _ = Sdl.SdlGlGetAttribute(SdlGLattr.SdlGlFramebufferSrgbCapable,
                out var effectiveSrgb);

            var nativeWindowInfo = NativeWindow.GetInfo(window);
            using var engineCreateInfo = new EngineGLCreateInfo
            {
                FeaturesSeparablePrograms = DeviceFeatureState.DeviceFeatureStateOptional,
                FeaturesShaderResourceQueries = DeviceFeatureState.DeviceFeatureStateOptional
            };
#if REDBOLT_PLATFORM_LINUX
            engineCreateInfo.WindowPDisplay = nativeWindowInfo.Display;
            engineCreateInfo.WindowWindowId = checked((uint)nativeWindowInfo.Window);
#elif REDBOLT_PLATFORM_WINDOWS
            engineCreateInfo.WindowHWnd = nativeWindowInfo.Window;
#endif
            using var swapChainDesc = new SwapChainDesc
            {
                ColorBufferFormat = effectiveSrgb != 0
                    ? TextureFormat.TexFormatRgba8UnormSrgb
                    : TextureFormat.TexFormatRgba8Unorm,
                DepthBufferFormat = TextureFormat.TexFormatUnknown
            };

            var factory = Diligent.LoadAndGetEngineFactoryOpenGL();
            if (factory.IsNull)
                throw new InvalidOperationException("OpenGL engine factory could not be loaded.");

            factory.AttachToActiveGLContext(engineCreateInfo, out var device, out var immediateContext);
            if (device.IsNull || immediateContext.IsNull)
                throw new InvalidOperationException("Generated AttachToActiveGLContext binding returned a null device or context.");

            var swapChain = Diligent.CreateProxySwapChainGL(
                device, immediateContext, swapChainDesc, window.Handle);
            if (swapChain.IsNull)
                throw new InvalidOperationException("Generated CreateProxySwapChainGL binding returned a null swap chain.");

            using var deviceContextGL = IDeviceContextGL.QueryManagedWrapper(immediateContext)
                ?? throw new InvalidOperationException("OpenGL device context interface is unavailable.");
            deviceContextGL.SetSwapChain(swapChain);

            return new GraphicsBackendResources
            {
                Factory = factory,
                Device = device,
                ImmediateContext = immediateContext,
                SwapChain = swapChain,
                Lifetime = new OpenGLLifetime(window, glContext),
                Width = width,
                Height = height,
                MultiSample = multiSample
            };
        }
        catch
        {
            Sdl.SdlGlDeleteContext(glContext);
            throw;
        }
    }

    private static void CheckSdl(int result, string operation)
    {
        if (result != 0)
            throw new InvalidOperationException($"{operation} failed with {result}.");
    }

    private sealed class OpenGLLifetime(SdlWindow window, nint glContext)
        : IGraphicsBackendLifetime, IOpenGLGraphicsApi
    {
        public nint NativeContext => glContext;

        public nint Context => glContext;

        public void SetVSync(bool enable)
        {
            _ = Sdl.SdlGlSetSwapInterval(enable ? 1 : 0);
        }

        public void Resize(IRenderDevice renderDevice, ISwapChain swapChain)
        {
            Sdl.SdlGlGetDrawableSize(window, out var width, out var height);
            swapChain.Resize((uint)System.Math.Max(width, 0), (uint)System.Math.Max(height, 0));
        }

        public void ReleaseBeforeCommonResources()
        {
        }

        public void ReleaseAfterCommonResources(SdlWindow? currentWindow)
        {
            if (currentWindow is not null && !currentWindow.IsNull)
                _ = Sdl.SdlGlMakeCurrent(currentWindow, IntPtr.Zero);
            Sdl.SdlGlDeleteContext(glContext);
        }
    }
}
#endif
