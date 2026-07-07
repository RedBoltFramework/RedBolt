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

    [Fact]
    public void Opengl_smoke_clears_offscreen_texture_and_reads_it_back()
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
        ITexture? uploadedTexture = null;
        ITexture? offscreenTexture = null;
        ITextureView? renderTargetView = null;

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

            var uploadPixels = new byte[8 * 8 * 4];
            for (var i = 0; i < uploadPixels.Length; i += 4)
            {
                uploadPixels[i + 0] = 255;
                uploadPixels[i + 1] = 0;
                uploadPixels[i + 2] = 0;
                uploadPixels[i + 3] = 255;
            }

            using (var uploadTextureDescOwner = CreateTextureDesc())
            {
                var uploadTextureDesc = uploadTextureDescOwner.Desc;
                uploadTextureDesc.Type = ResourceDimension.ResourceDimTex2d;
                uploadTextureDesc.Width = 8;
                uploadTextureDesc.Height = 8;
                uploadTextureDesc.ArraySizeOrDepth = 1;
                uploadTextureDesc.MipLevels = 1;
                uploadTextureDesc.Format = global::RedBolt.Bindings.Diligent.TextureFormat.TexFormatRgba8Unorm;
                uploadTextureDesc.BindFlags = BindFlags.BindShaderResource;
                uploadTextureDesc.Usage = Usage.UsageImmutable;
                uploadTextureDesc.SampleCount = 1;

                var uploadHandle = GCHandle.Alloc(uploadPixels, GCHandleType.Pinned);
                try
                {
                    using var subresource = new TextureSubResData(uploadHandle.AddrOfPinnedObject(), 8u * 4u, 0);
                    using var textureData = new TextureData(subresource, 1, immediateContext);
                    device.CreateTexture(uploadTextureDesc, textureData, out uploadedTexture);
                }
                finally
                {
                    uploadHandle.Free();
                }
            }

            if (uploadedTexture.IsNull)
                throw new InvalidOperationException("Failed to create uploaded texture.");

            var uploadedPixel = ReadBackFirstPixel(device, immediateContext, uploadedTexture, 8, 8,
                global::RedBolt.Bindings.Diligent.TextureFormat.TexFormatRgba8Unorm);

            using var textureDescOwner = CreateTextureDesc();
            var textureDesc = textureDescOwner.Desc;
            textureDesc.Type = ResourceDimension.ResourceDimTex2d;
            textureDesc.Width = 8;
            textureDesc.Height = 8;
            textureDesc.ArraySizeOrDepth = 1;
            textureDesc.MipLevels = 1;
            textureDesc.Format = global::RedBolt.Bindings.Diligent.TextureFormat.TexFormatRgba8Unorm;
            textureDesc.BindFlags = BindFlags.BindRenderTarget | BindFlags.BindShaderResource;
            textureDesc.Usage = Usage.UsageDefault;
            textureDesc.SampleCount = 1;
            device.CreateTexture(textureDesc, null!, out offscreenTexture);
            if (offscreenTexture.IsNull)
                throw new InvalidOperationException("Failed to create offscreen texture.");

            renderTargetView = GetOrCreateTextureView(offscreenTexture, TextureViewType.TextureViewRenderTarget,
                global::RedBolt.Bindings.Diligent.TextureFormat.TexFormatRgba8Unorm);
            Assert.False(renderTargetView.IsNull);

            ITextureView?[] renderTargets = [renderTargetView];
            immediateContext.SetRenderTargets((uint)renderTargets.Length, renderTargets, null!,
                ResourceStateTransitionMode.ResourceStateTransitionModeTransition);

            var clearColor = new[] { 1.0f, 0.0f, 0.0f, 1.0f };
            var handle = GCHandle.Alloc(clearColor, GCHandleType.Pinned);
            try
            {
                immediateContext.ClearRenderTarget(renderTargetView, handle.AddrOfPinnedObject(),
                    ResourceStateTransitionMode.ResourceStateTransitionModeTransition);
            }
            finally
            {
                handle.Free();
            }

            var offscreenPixel = ReadBackFirstPixel(device, immediateContext, offscreenTexture, 8, 8,
                global::RedBolt.Bindings.Diligent.TextureFormat.TexFormatRgba8Unorm);

            Assert.True(uploadedPixel[0] >= 250,
                $"Uploaded texture readback expected red near 255, got {uploadedPixel[0]}, {uploadedPixel[1]}, {uploadedPixel[2]}, {uploadedPixel[3]}.");
            Assert.True(uploadedPixel[1] <= 5);
            Assert.True(uploadedPixel[2] <= 5);
            Assert.True(uploadedPixel[3] >= 250);
            Assert.True(offscreenPixel[0] >= 250,
                $"Offscreen clear readback expected red near 255, got {offscreenPixel[0]}, {offscreenPixel[1]}, {offscreenPixel[2]}, {offscreenPixel[3]}.");
            Assert.True(offscreenPixel[1] <= 5);
            Assert.True(offscreenPixel[2] <= 5);
            Assert.True(offscreenPixel[3] >= 250);

            immediateContext.SetRenderTargets(0, Array.Empty<ITextureView?>(), null!,
                ResourceStateTransitionMode.ResourceStateTransitionModeNone);
        }
        finally
        {
            ReleaseIfNotNull(uploadedTexture);
            ReleaseIfNotNull(renderTargetView);
            ReleaseIfNotNull(offscreenTexture);
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

    private static byte[] ReadBackFirstPixel(IRenderDevice device, IDeviceContext immediateContext, ITexture texture, uint width,
        uint height, global::RedBolt.Bindings.Diligent.TextureFormat format)
    {
        ITexture? stagingTexture = null;
        try
        {
            using var stagingDescOwner = CreateTextureDesc();
            var stagingDesc = stagingDescOwner.Desc;
            stagingDesc.Type = ResourceDimension.ResourceDimTex2d;
            stagingDesc.Width = width;
            stagingDesc.Height = height;
            stagingDesc.ArraySizeOrDepth = 1;
            stagingDesc.MipLevels = 1;
            stagingDesc.Format = format;
            stagingDesc.BindFlags = BindFlags.BindNone;
            stagingDesc.CPUAccessFlags = CpuAccessFlags.CpuAccessRead;
            stagingDesc.MiscFlags = MiscTextureFlags.MiscTextureFlagNone;
            stagingDesc.SampleCount = 1;
            stagingDesc.Usage = Usage.UsageStaging;
            device.CreateTexture(stagingDesc, null!, out stagingTexture);
            if (stagingTexture.IsNull)
                throw new InvalidOperationException("Failed to create staging texture.");

            using var copyAttribs = new CopyTextureAttribs(
                texture,
                ResourceStateTransitionMode.ResourceStateTransitionModeTransition,
                stagingTexture,
                ResourceStateTransitionMode.ResourceStateTransitionModeTransition);
            immediateContext.CopyTexture(copyAttribs);
            immediateContext.Flush();
            immediateContext.WaitForIdle();

            using var mappedSubresource = new MappedTextureSubresource();
            immediateContext.MapTextureSubresource(stagingTexture, 0, 0, MapType.MapRead, MapFlags.MapFlagNone, null!,
                mappedSubresource);
            try
            {
                var firstPixel = new byte[4];
                Marshal.Copy(mappedSubresource.PData, firstPixel, 0, 4);
                return firstPixel;
            }
            finally
            {
                immediateContext.UnmapTextureSubresource(stagingTexture, 0, 0);
            }
        }
        finally
        {
            ReleaseIfNotNull(stagingTexture);
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

    private static ITextureView GetOrCreateTextureView(ITexture texture, TextureViewType viewType,
        global::RedBolt.Bindings.Diligent.TextureFormat format)
    {
        var view = texture.GetDefaultView(viewType);
        if (view is not null && !view.IsNull)
        {
            _ = view.AddRef();
            return view;
        }

        using var textureViewDescOwner = CreateTextureViewDesc();
        var textureViewDesc = textureViewDescOwner.Desc;
        textureViewDesc.ViewType = viewType;
        textureViewDesc.Format = format;
        texture.CreateView(textureViewDesc, out var createdView);
        if (createdView.IsNull)
            throw new InvalidOperationException($"Failed to create {viewType}.");

        return createdView;
    }

    private static TextureDescOwner CreateTextureDesc()
    {
        var apiInfo = DiligentNativeBindings.GetEngineFactoryOpenGL().GetAPIInfo();
        return new TextureDescOwner((int)apiInfo.TextureDescSize);
    }

    private static TextureViewDescOwner CreateTextureViewDesc()
    {
        var apiInfo = DiligentNativeBindings.GetEngineFactoryOpenGL().GetAPIInfo();
        return new TextureViewDescOwner((int)apiInfo.TextureViewDescSize);
    }

    private sealed class TextureDescOwner : IDisposable
    {
        private readonly IntPtr memory;

        public TextureDescOwner(int size)
        {
            memory = Marshal.AllocHGlobal(size);
            Marshal.Copy(new byte[size], 0, memory, size);
            Desc = TextureDesc.CreateManagedWrapper(memory, ownsHandle: false);
        }

        public TextureDesc Desc { get; }

        public void Dispose()
        {
            if (memory != IntPtr.Zero)
                Marshal.FreeHGlobal(memory);
        }
    }

    private sealed class TextureViewDescOwner : IDisposable
    {
        private readonly IntPtr memory;

        public TextureViewDescOwner(int size)
        {
            memory = Marshal.AllocHGlobal(size);
            Marshal.Copy(new byte[size], 0, memory, size);
            Desc = TextureViewDesc.CreateManagedWrapper(memory, ownsHandle: false);
        }

        public TextureViewDesc Desc { get; }

        public void Dispose()
        {
            if (memory != IntPtr.Zero)
                Marshal.FreeHGlobal(memory);
        }
    }

    private static void ReleaseIfNotNull(IObject? value)
    {
        if (value is not null && !value.IsNull)
            _ = value.Release();
    }
}
