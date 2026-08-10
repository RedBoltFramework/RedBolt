namespace RedBolt;

using System;

using RedBolt.Bindings.Diligent;
using RedBolt.Bindings.Sdl;
using RedBolt.Core;
using RedBolt.Engine;
using RedBolt.Math;
using RedBolt.Platform;
using RedBolt.RenderAPI;

public interface IOpenGLESGraphicsApi
{
    nint Context { get; }
}

/// <summary>OpenGL ES graphics backend.</summary>
public sealed class OpenGLESGraphicsBackend : IGraphicsBackend
{
    public RenderBackend Backend => RenderBackend.OpenGL;

    public bool UsesOpenGLWindow => true;

    public void ConfigureWindow()
    {
        CheckSdl(SdlNativeBindings.SdlGlSetAttribute(SdlGLattr.SdlGlDoublebuffer, 1), "SDL_GL_SetAttribute doublebuffer");
        CheckSdl(SdlNativeBindings.SdlGlSetAttribute(SdlGLattr.SdlGlContextMajorVersion, 3), "SDL_GL_SetAttribute major version");
        CheckSdl(SdlNativeBindings.SdlGlSetAttribute(SdlGLattr.SdlGlContextMinorVersion, 0), "SDL_GL_SetAttribute minor version");
        CheckSdl(SdlNativeBindings.SdlGlSetAttribute(SdlGLattr.SdlGlContextProfileMask,
            (int)SdlGLprofile.SdlGlContextProfileEs), "SDL_GL_SetAttribute profile mask");
    }

    public GraphicsBackendResources Initialize(EngineParameters parameters, RenderDeviceSettings deviceSettings,
        WindowSettings windowSettings, SdlWindow window, IPlatformRuntime platform)
    {
        var glContext = SdlNativeBindings.SdlGlCreateContext(window);
        if (glContext == IntPtr.Zero)
            throw new InvalidOperationException("Generated SDL_GL_CreateContext binding returned a null context.");

        try
        {
            CheckSdl(SdlNativeBindings.SdlGlMakeCurrent(window, glContext), "SDL_GL_MakeCurrent");
            SdlNativeBindings.SdlGlGetDrawableSize(window, out var width, out var height);

            var multiSample = (int)System.Math.Clamp(
                MathDefs.ClosestPowerOfTwo((uint)System.Math.Max(parameters.MultiSample, 1)), 1u, 16u);
            if (SdlNativeBindings.SdlGlGetAttribute(SdlGLattr.SdlGlMultisamplesamples,
                    out var effectiveMultiSample) == 0)
                multiSample = System.Math.Max(1, effectiveMultiSample);
            _ = SdlNativeBindings.SdlGlGetAttribute(SdlGLattr.SdlGlFramebufferSrgbCapable,
                out var effectiveSrgb);

            using var engineCreateInfo = new EngineGLCreateInfo
            {
                FeaturesSeparablePrograms = DeviceFeatureState.DeviceFeatureStateOptional,
                FeaturesShaderResourceQueries = DeviceFeatureState.DeviceFeatureStateOptional
            };
            using var swapChainDesc = new SwapChainDesc
            {
                ColorBufferFormat = effectiveSrgb != 0
                    ? TextureFormat.TexFormatRgba8UnormSrgb
                    : TextureFormat.TexFormatRgba8Unorm,
                DepthBufferFormat = TextureFormat.TexFormatUnknown
            };

            var factory = DiligentNativeBindings.LoadAndGetEngineFactoryOpenGL();
            if (factory.IsNull)
                throw new InvalidOperationException("OpenGL engine factory could not be loaded.");

            factory.AttachToActiveGLContext(engineCreateInfo, out var device, out var immediateContext);
            if (device.IsNull || immediateContext.IsNull)
                throw new InvalidOperationException("Generated AttachToActiveGLContext binding returned a null device or context.");

            var swapChain = DiligentNativeBindings.CreateProxySwapChainGL(
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
                Lifetime = new OpenGLESLifetime(window, glContext),
                Width = width,
                Height = height,
                MultiSample = multiSample
            };
        }
        catch
        {
            SdlNativeBindings.SdlGlDeleteContext(glContext);
            throw;
        }
    }

    private static void CheckSdl(int result, string operation)
    {
        if (result != 0)
            throw new InvalidOperationException($"{operation} failed with {result}.");
    }

    private sealed class OpenGLESLifetime(SdlWindow window, nint glContext)
        : IGraphicsBackendLifetime, IOpenGLESGraphicsApi
    {
        public nint NativeContext => glContext;

        public nint Context => glContext;

        public void SetVSync(bool enable)
        {
        }

        public void Resize(IRenderDevice renderDevice, ISwapChain swapChain)
        {
            SdlNativeBindings.SdlGlGetDrawableSize(window, out var width, out var height);
            swapChain.Resize((uint)System.Math.Max(width, 0), (uint)System.Math.Max(height, 0));
        }

        public void ReleaseBeforeCommonResources()
        {
        }

        public void ReleaseAfterCommonResources(SdlWindow? currentWindow)
        {
            if (currentWindow is not null && !currentWindow.IsNull)
                _ = SdlNativeBindings.SdlGlMakeCurrent(currentWindow, IntPtr.Zero);
            SdlNativeBindings.SdlGlDeleteContext(glContext);
        }
    }
}
