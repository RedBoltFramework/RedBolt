using System.Runtime.InteropServices;
using RedBolt.Bindings.Diligent;
using RedBolt.Bindings.Sdl;

namespace RedBolt;

public sealed class Graphics
{
    private const uint GlFront = 0x0404;
    private const uint GlBack = 0x0405;
    private const uint GlRgba = 0x1908;
    private const uint GlUnsignedByte = 0x1401;
    private const int CameraBufferSize = 368;
    private const int ObjectBufferSize = 80;
    private const int MaterialBufferSize = 128;
    private const int LightBufferSize = 48;
    private const int ZoneBufferSize = 16;
    private const string ShaderExtraCode = """
#define URHO3D_CUSTOM_FRAME_UNIFORMS
#define URHO3D_CUSTOM_MATERIAL_UNIFORMS
#define URHO3D_CUSTOM_LIGHT_UNIFORMS

layout(std140) uniform Light
{
    vec4 uLightPos;
    vec4 uLightDir;
    vec4 uLightColor;
};

#define cLightPos uLightPos
#define cLightDir uLightDir.xyz
#define cLightColor uLightColor

layout(std140) uniform Material
{
    vec4 uUOffset;
    vec4 uVOffset;
    vec4 uMatDiffColor;
    vec4 uMatSpecColor;
    vec4 uMatEmissiveRoughness;
    vec4 uMatEnvMapMetallic;
    vec4 uMaterialMisc;
    vec4 uFadeOffsetScale;
};

#define cUOffset uUOffset
#define cVOffset uVOffset
#define cMatDiffColor uMatDiffColor
#define cMatSpecColor uMatSpecColor
#define cMatEmissiveColor uMatEmissiveRoughness.xyz
#define cRoughness uMatEmissiveRoughness.w
#define cMatEnvMapColor uMatEnvMapMetallic.xyz
#define cMetallic uMatEnvMapMetallic.w
#define cNormalScale uMaterialMisc.x
#define cDielectricReflectance uMaterialMisc.y
#define cAlphaCutoff uMaterialMisc.z
#define cLMOffset vec4(1.0, 1.0, 0.0, 0.0)
#define cFadeOffsetScale uFadeOffsetScale.xy
""";

    private static readonly Vector4 DefaultAmbient = new(0.0f, 0.0f, 0.0f, 1.0f);
    private static readonly Vector4 DefaultFogColor = new(0.03f, 0.05f, 0.09f, 1.0f);
    private static readonly byte[] DefaultAlbedoTextureData = [255, 255, 255, 255];
    private static readonly byte[] DefaultNormalTextureData = [128, 128, 255, 255];
    private static readonly byte[] DefaultPropertiesTextureData = [255, 255, 255, 255];
    private static readonly byte[] DefaultEmissionTextureData = [0, 0, 0, 255];
    private static readonly bool RenderDiagnosticsEnabled = IsRenderDiagnosticsEnabled();
    private static readonly bool ShaderBindingDiagnosticsEnabled = IsShaderBindingDiagnosticsEnabled();
    private static readonly bool ShaderReflectionDiagnosticsEnabled = IsShaderReflectionDiagnosticsEnabled();

    private readonly float[] clearColor = [0.0f, 0.0f, 0.0f, 1.0f];
    private readonly ITextureView?[] renderTargets = new ITextureView?[1];
    private readonly Dictionary<Geometry, UploadedGeometry> uploadedGeometries = new();
    private readonly Dictionary<VertexBuffer, IBuffer> uploadedVertexBuffers = new();
    private readonly Dictionary<IndexBuffer, IBuffer> uploadedIndexBuffers = new();
    private readonly Dictionary<Texture2D, UploadedTexture> uploadedTextures = new();
    private readonly Dictionary<PipelineKey, PipelineResources> pipelines = new();

    private GCHandle clearColorHandle;
    private Texture2D? defaultAlbedoTexture;
    private Texture2D? defaultNormalTexture;
    private Texture2D? defaultPropertiesTexture;
    private Texture2D? defaultEmissionTexture;
    private GlFinishDelegate? glFinish;
    private GlReadBufferDelegate? glReadBuffer;
    private GlReadPixelsDelegate? glReadPixels;
    private IDeviceContext? immediateContext;
    private IRenderDevice? device;
    private ISwapChain? swapChain;
    private ITextureView? renderTargetView;
    private ITextureView? depthStencilView;
    private IntPtr glContext;
    private bool shaderReflectionDiagnosticsDumped;
    private SdlWindow? window;

    public bool IsInitialized { get; private set; }

    public int Width { get; private set; }

    public int Height { get; private set; }

    public void Initialize(EngineParameters parameters, Input input)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(input);

        if (IsInitialized)
            throw new InvalidOperationException("Graphics system is already initialized.");

        if (parameters.GraphicsBackend is not GraphicsBackend.Auto and not GraphicsBackend.OpenGL)
            throw new NotSupportedException($"The first RedBolt graphics backend supports OpenGL only, not {parameters.GraphicsBackend}.");

        window = input.Window;
        glContext = SdlNativeBindings.SdlGlCreateContext(window);
        if (glContext == IntPtr.Zero)
            throw new InvalidOperationException("Generated SDL_GL_CreateContext binding returned a null context.");

        try
        {
            CheckSdlGl(SdlNativeBindings.SdlGlMakeCurrent(window, glContext), "SDL_GL_MakeCurrent");
            _ = SdlNativeBindings.SdlGlSetSwapInterval(parameters.VSync ? 1 : 0);
            SdlNativeBindings.SdlGetWindowSize(window, out var actualWidth, out var actualHeight);
            Width = actualWidth;
            Height = actualHeight;

            var nativeWindowInfo = input.GetNativeWindowInfo();
            using var engineCreateInfo = new EngineGLCreateInfo
            {
                FeaturesSeparablePrograms = DeviceFeatureState.DeviceFeatureStateOptional,
                FeaturesShaderResourceQueries = DeviceFeatureState.DeviceFeatureStateOptional,
                WindowPDisplay = nativeWindowInfo.Display,
                WindowWindowId = nativeWindowInfo.WindowId
            };
            using var swapChainDesc = new SwapChainDesc();

            var factory = DiligentNativeBindings.GetEngineFactoryOpenGL();
            if (factory.IsNull)
                throw new InvalidOperationException("Generated GetEngineFactoryOpenGL binding returned a null factory.");

            factory.CreateDeviceAndSwapChainGL(engineCreateInfo, out device, out immediateContext, swapChainDesc, out swapChain);
            if (device.IsNull || immediateContext.IsNull || swapChain.IsNull)
                throw new InvalidOperationException("Generated CreateDeviceAndSwapChainGL binding returned a null device, context, or swap chain.");

            renderTargetView = swapChain.GetCurrentBackBufferRTV();
            depthStencilView = swapChain.GetDepthBufferDSV();
            if (renderTargetView.IsNull || depthStencilView.IsNull)
                throw new InvalidOperationException("Swap chain render target or depth view was null.");
            renderTargets[0] = renderTargetView;

            clearColorHandle = GCHandle.Alloc(clearColor, GCHandleType.Pinned);
            IsInitialized = true;
        }
        catch
        {
            Shutdown();
            throw;
        }
    }

    internal void RenderViewport(Viewport? viewport, string? screenshotPath = null)
    {
        RenderViewport(viewport, MaterialQuality.High, screenshotPath);
    }

    internal void RenderViewport(Viewport? viewport, MaterialQuality materialQuality, string? screenshotPath = null)
    {
        if (!IsInitialized || viewport is null || immediateContext is null || swapChain is null || renderTargetView is null || depthStencilView is null)
            return;

        if (viewport.Camera.GetAutoAspectRatio())
            viewport.Camera.SetAspectRatioInternal(Math.Max((float)Width / Math.Max(Height, 1), MathDefs.Epsilon));

        var viewportRect = GetEffectiveViewportRect(viewport);
        using var diligentViewport = new global::RedBolt.Bindings.Diligent.Viewport(
            viewportRect.Left,
            viewportRect.Top,
            viewportRect.Width,
            viewportRect.Height);
        immediateContext.SetViewports(1, diligentViewport, 0, 0);

        immediateContext.SetRenderTargets((uint)renderTargets.Length, renderTargets, depthStencilView,
            ResourceStateTransitionMode.ResourceStateTransitionModeTransition);
        immediateContext.ClearRenderTarget(renderTargetView, clearColorHandle.AddrOfPinnedObject(),
            ResourceStateTransitionMode.ResourceStateTransitionModeTransition);
        immediateContext.ClearDepthStencil(depthStencilView, ClearDepthStencilFlags.ClearDepthFlag, 1.0f, 0,
            ResourceStateTransitionMode.ResourceStateTransitionModeTransition);

        var directionalLight = viewport.Scene.GetAllComponents().Values
            .OfType<Light>()
            .FirstOrDefault(static light => light.IsAttached && light.IsEnabledEffective && light.GetLightType() == LightType.Directional);

        uint submittedDrawCalls = 0;
        uint submittedPrimitives = 0;
        foreach (var staticModel in viewport.Scene.StaticModels)
        {
            var (drawCalls, primitives) = RenderStaticModel(viewport, staticModel, materialQuality, directionalLight);
            submittedDrawCalls += drawCalls;
            submittedPrimitives += primitives;
        }

        if (RenderDiagnosticsEnabled)
            Console.Error.WriteLine($"RedBolt render diagnostics: drawCalls={submittedDrawCalls}, primitives={submittedPrimitives}, staticModels={viewport.Scene.StaticModels.Count}");

        if (!string.IsNullOrWhiteSpace(screenshotPath))
            SaveScreenshot(screenshotPath);
        swapChain.Present(0);
        immediateContext.SetRenderTargets(0, global::System.ReadOnlySpan<ITextureView?>.Empty, null!,
            ResourceStateTransitionMode.ResourceStateTransitionModeNone);
    }

    public void SetVSync(bool enable)
    {
        if (!IsInitialized)
            return;

        _ = SdlNativeBindings.SdlGlSetSwapInterval(enable ? 1 : 0);
    }

    public void SaveScreenshot(string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        if (!IsInitialized || device is null || immediateContext is null || renderTargetView is null)
            throw new InvalidOperationException("Graphics system is not initialized.");

        if (TrySaveCurrentFramebufferScreenshot(fileName))
            return;

        var sourceTexture = renderTargetView.GetTexture();
        if (!sourceTexture.IsNull)
        {
            SaveTexture(sourceTexture, fileName);
            return;
        }

        if (!TrySaveCurrentFramebufferScreenshot(fileName))
            throw new InvalidOperationException("Backbuffer texture was not available for screenshot capture.");
    }

    private void SaveTexture(ITexture sourceTexture, string fileName)
    {
        ArgumentNullException.ThrowIfNull(sourceTexture);

        if (device is null || immediateContext is null)
            throw new InvalidOperationException("Graphics system is not initialized.");

        var sourceDescription = sourceTexture.GetDesc();
        var width = checked((int)sourceDescription.Width);
        var height = checked((int)sourceDescription.Height);
        var stagingDescription = sourceTexture.GetDesc();
        stagingDescription.BindFlags = BindFlags.BindNone;
        stagingDescription.CPUAccessFlags = CpuAccessFlags.CpuAccessRead;
        stagingDescription.MiscFlags = MiscTextureFlags.MiscTextureFlagNone;
        stagingDescription.SampleCount = 1;
        stagingDescription.Usage = Usage.UsageStaging;

        ITexture? stagingTexture = null;
        try
        {
            device.CreateTexture(stagingDescription, null!, out stagingTexture);
            if (stagingTexture is null || stagingTexture.IsNull)
                throw new InvalidOperationException("Failed to create the staging texture for screenshot capture.");

            using var copyAttributes = new CopyTextureAttribs(
                sourceTexture,
                ResourceStateTransitionMode.ResourceStateTransitionModeTransition,
                stagingTexture,
                ResourceStateTransitionMode.ResourceStateTransitionModeTransition);
            immediateContext.CopyTexture(copyAttributes);
            immediateContext.Flush();
            immediateContext.WaitForIdle();

            using var mappedSubresource = new MappedTextureSubresource();
            using var mapRegion = new Box(0, sourceDescription.Width, 0, sourceDescription.Height, 0, 1);
            immediateContext.MapTextureSubresource(stagingTexture, 0, 0, MapType.MapRead, MapFlags.MapFlagNone, mapRegion, mappedSubresource);
            try
            {
                var rgbaPixels = new byte[checked(width * height * 4)];
                CopyMappedTexture(sourceDescription.Format, width, height, mappedSubresource, rgbaPixels);
                ImageFileWriter.Save(fileName, width, height, rgbaPixels);
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

    private void SaveCurrentFramebufferScreenshot(string fileName)
    {
        if (!TrySaveCurrentFramebufferScreenshot(fileName))
            throw new InvalidOperationException("OpenGL framebuffer readback is not available.");
    }

    private void SavePresentedFrameScreenshot(string fileName)
    {
        if (TrySaveCurrentFramebufferScreenshot(fileName, GlFront))
            return;

        if (renderTargetView is not null)
        {
            var sourceTexture = renderTargetView.GetTexture();
            if (!sourceTexture.IsNull)
            {
                SaveTexture(sourceTexture, fileName);
                return;
            }
        }

        SaveCurrentFramebufferScreenshot(fileName);
    }

    private bool TrySaveCurrentFramebufferScreenshot(string fileName)
    {
        return TrySaveCurrentFramebufferScreenshot(fileName, GlBack);
    }

    private bool TrySaveCurrentFramebufferScreenshot(string fileName, uint readBuffer)
    {
        if (glContext == IntPtr.Zero || Width <= 0 || Height <= 0)
            return false;

        EnsureOpenGlReadbackFunctions();

        var rgbaPixels = new byte[checked(Width * Height * 4)];
        using var pinnedPixels = new PinnedBuffer(rgbaPixels);

        glFinish!.Invoke();
        glReadBuffer!.Invoke(readBuffer);
        glReadPixels!.Invoke(0, 0, Width, Height, GlRgba, GlUnsignedByte, pinnedPixels.Pointer);
        FlipImageRows(rgbaPixels, Width, Height);
        ImageFileWriter.Save(fileName, Width, Height, rgbaPixels);
        return true;
    }

    private void EnsureOpenGlReadbackFunctions()
    {
        glFinish ??= LoadOpenGlProcedure<GlFinishDelegate>("glFinish");
        glReadBuffer ??= LoadOpenGlProcedure<GlReadBufferDelegate>("glReadBuffer");
        glReadPixels ??= LoadOpenGlProcedure<GlReadPixelsDelegate>("glReadPixels");
    }

    private static T LoadOpenGlProcedure<T>(string name)
        where T : Delegate
    {
        using var pinnedName = new PinnedUtf8(name);
        var procedure = SdlNativeBindings.SdlGlGetProcAddress(pinnedName.Bytes);
        if (procedure == IntPtr.Zero)
            throw new InvalidOperationException($"OpenGL procedure '{name}' is not available.");

        return Marshal.GetDelegateForFunctionPointer<T>(procedure);
    }

    public void Shutdown()
    {
        Width = 0;
        Height = 0;
        renderTargets[0] = null;
        renderTargetView = null;
        depthStencilView = null;

        foreach (var uploaded in uploadedGeometries.Values)
            uploaded.Dispose();
        uploadedGeometries.Clear();

        foreach (var uploaded in uploadedVertexBuffers.Values)
            ReleaseIfNotNull(uploaded);
        uploadedVertexBuffers.Clear();

        foreach (var uploaded in uploadedIndexBuffers.Values)
            ReleaseIfNotNull(uploaded);
        uploadedIndexBuffers.Clear();

        foreach (var uploaded in uploadedTextures.Values)
            uploaded.Dispose();
        uploadedTextures.Clear();

        defaultAlbedoTexture = null;
        defaultNormalTexture = null;

        foreach (var pipeline in pipelines.Values)
            pipeline.Dispose();
        pipelines.Clear();

        ReleaseIfNotNull(swapChain);
        swapChain = null;
        ReleaseIfNotNull(immediateContext);
        immediateContext = null;
        ReleaseIfNotNull(device);
        device = null;

        if (clearColorHandle.IsAllocated)
            clearColorHandle.Free();

        if (glContext != IntPtr.Zero)
        {
            if (window is not null && !window.IsNull)
                _ = SdlNativeBindings.SdlGlMakeCurrent(window, IntPtr.Zero);
            SdlNativeBindings.SdlGlDeleteContext(glContext);
            glContext = IntPtr.Zero;
        }

        window = null;
        IsInitialized = false;
    }

    private (uint DrawCalls, uint Primitives) RenderStaticModel(Viewport viewport, StaticModel staticModel, MaterialQuality materialQuality, Light? directionalLight)
    {
        if (!staticModel.IsAttached || !staticModel.IsEnabledEffective || staticModel.GetModel() is not { } model)
            return default;

        uint drawCalls = 0;
        uint primitives = 0;
        var cameraZone = FindZone(viewport.Scene, viewport.Camera.IsAttached ? viewport.Camera.Node.WorldPosition : Vector3.Zero);
        var drawableZone = FindZone(viewport.Scene, staticModel.Node.WorldPosition);
        var drawableAmbient = drawableZone is not null ? new Vector4(drawableZone.GetAmbientLighting(), 1.0f) : DefaultAmbient;
        for (var geometryIndex = 0u; geometryIndex < model.GetNumGeometries(); ++geometryIndex)
        {
            var geometry = model.GetGeometry(geometryIndex, 0);
            var vertexBuffer = geometry?.GetVertexBuffer(0);
            var indexBuffer = geometry?.GetIndexBuffer();
            var material = staticModel.GetMaterial(geometryIndex);
            if (geometry is null || vertexBuffer is null || indexBuffer is null || material is null)
                continue;

            var resourceCache = material.OwnerCache;
            if (resourceCache is null)
                continue;

            var technique = material.FindTechnique(materialQuality);
            if (technique is null)
                continue;

            var pass = SelectPass(technique, directionalLight);
            if (pass is null)
                continue;

            var effectiveLight = PassUsesLighting(pass) ? directionalLight : null;
            var pipeline = GetOrCreatePipeline(resourceCache, viewport.Camera, material, pass, vertexBuffer, effectiveLight);
            var uploadedGeometry = GetOrCreateUploadedGeometry(geometry);
            if (pipeline is null || uploadedGeometry is null || immediateContext is null)
                continue;

            UpdateCameraBuffer(pipeline.CameraBuffer, viewport.Camera, cameraZone);
            UpdateObjectBuffer(pipeline.ObjectBuffer, staticModel.Node.GetWorldTransform().Matrix4, drawableAmbient);
            UpdateMaterialBuffer(pipeline.MaterialBuffer, material);
            UpdateLightBuffer(pipeline.LightBuffer, effectiveLight);
            UpdateZoneBuffer(pipeline.ZoneBuffer, drawableZone);

            BindDynamicBuffer(pipeline, global::RedBolt.Bindings.Diligent.ShaderType.ShaderTypeVertex,
                "Camera", pipeline.CameraBuffer, CameraBufferSize);
            BindDynamicBuffer(pipeline, global::RedBolt.Bindings.Diligent.ShaderType.ShaderTypePixel,
                "Camera", pipeline.CameraBuffer, CameraBufferSize);
            BindDynamicBuffer(pipeline, global::RedBolt.Bindings.Diligent.ShaderType.ShaderTypeVertex,
                "Zone", pipeline.ZoneBuffer, ZoneBufferSize);
            BindDynamicBuffer(pipeline, global::RedBolt.Bindings.Diligent.ShaderType.ShaderTypePixel,
                "Zone", pipeline.ZoneBuffer, ZoneBufferSize);
            BindDynamicBuffer(pipeline, global::RedBolt.Bindings.Diligent.ShaderType.ShaderTypeVertex,
                "Object", pipeline.ObjectBuffer, ObjectBufferSize);
            BindDynamicBuffer(pipeline, global::RedBolt.Bindings.Diligent.ShaderType.ShaderTypeVertex,
                "Material", pipeline.MaterialBuffer, MaterialBufferSize);
            BindDynamicBuffer(pipeline, global::RedBolt.Bindings.Diligent.ShaderType.ShaderTypePixel,
                "Material", pipeline.MaterialBuffer, MaterialBufferSize);
            BindDynamicBuffer(pipeline, global::RedBolt.Bindings.Diligent.ShaderType.ShaderTypeVertex,
                "Light", pipeline.LightBuffer, LightBufferSize);
            BindDynamicBuffer(pipeline, global::RedBolt.Bindings.Diligent.ShaderType.ShaderTypePixel,
                "Light", pipeline.LightBuffer, LightBufferSize);
            BindMaterialTextures(pipeline, material);

            immediateContext.SetPipelineState(pipeline.PipelineState);
            immediateContext.CommitShaderResources(pipeline.ShaderResources, ResourceStateTransitionMode.ResourceStateTransitionModeTransition);

            nuint vertexOffset = 0;
            IBuffer?[] vertexBuffers = [uploadedGeometry.VertexBuffer];
            immediateContext.SetVertexBuffers(0, 1, vertexBuffers, in vertexOffset,
                ResourceStateTransitionMode.ResourceStateTransitionModeTransition,
                SetVertexBuffersFlags.SetVertexBuffersFlagReset);
            immediateContext.SetIndexBuffer(uploadedGeometry.IndexBuffer, 0,
                ResourceStateTransitionMode.ResourceStateTransitionModeTransition);
            immediateContext.DrawIndexed(uploadedGeometry.DrawIndexedAttribs);
            ++drawCalls;
            primitives += geometry.GetPrimitiveCount();
        }

        return (drawCalls, primitives);
    }

    private PipelineResources? GetOrCreatePipeline(ResourceCache resourceCache, Camera camera, Material material, Pass pass,
        VertexBuffer vertexBuffer, Light? light)
    {
        var vertexDefines = ComposeShaderDefines(pass, vertexBuffer, material, light, ShaderStage.Vertex);
        var pixelDefines = ComposeShaderDefines(pass, vertexBuffer, material, light, ShaderStage.Pixel);
        var effectiveCullMode = pass.CullMode != CullMode.Max ? pass.CullMode : material.CullMode;
        var effectiveFillMode = GetEffectiveFillMode(camera, material);
        var reverseCulling = camera.GetReverseCulling();
        var alphaToCoverage = pass.AlphaToCoverage || material.GetAlphaToCoverage();
        var key = new PipelineKey(pass.VertexShader, pass.PixelShader, vertexDefines, pixelDefines,
            BuildLayoutSignature(vertexBuffer), effectiveCullMode, effectiveFillMode, pass.DepthTestMode,
            pass.DepthWrite, alphaToCoverage, reverseCulling);
        if (pipelines.TryGetValue(key, out var cached))
            return cached;

        if (device is null || swapChain is null)
            return null;

        var vertexShaderResource = resourceCache.GetResourceOrNull<Shader>(pass.VertexShader);
        var pixelShaderResource = resourceCache.GetResourceOrNull<Shader>(pass.PixelShader);
        if (vertexShaderResource is null || pixelShaderResource is null)
            return null;

        var vertexShader = CreateShader(device, global::RedBolt.Bindings.Diligent.ShaderType.ShaderTypeVertex,
            pass.VertexShader, vertexShaderResource.PrepareSource(ShaderStage.Vertex, vertexDefines, GraphicsBackend.OpenGL, ShaderExtraCode));
        var pixelShader = CreateShader(device, global::RedBolt.Bindings.Diligent.ShaderType.ShaderTypePixel,
            pass.PixelShader, pixelShaderResource.PrepareSource(ShaderStage.Pixel, pixelDefines, GraphicsBackend.OpenGL, ShaderExtraCode));

        TemporaryLayoutArray? layoutArray = null;
        try
        {
            layoutArray = new TemporaryLayoutArray(BuildLayoutElements(vertexBuffer));
            using var pipelineName = new PinnedUtf8($"{pass.VertexShader}:{pass.PixelShader}:{key.LayoutSignature}");
            using var psoDesc = new PipelineStateDesc
            {
                Name = pipelineName.Pointer,
                PipelineType = PipelineType.PipelineTypeGraphics,
                ResourceLayoutDefaultVariableType = ShaderResourceVariableType.ShaderResourceVariableTypeDynamic
            };
            using var psoCreateInfo = new GraphicsPipelineStateCreateInfo
            {
                PSODesc = psoDesc,
                PVS = vertexShader,
                PPS = pixelShader,
                GraphicsPipelineBlendDescAlphaToCoverageEnable = alphaToCoverage,
                GraphicsPipelineNumRenderTargets = 1,
                GraphicsPipelineNumViewports = 1,
                GraphicsPipelinePrimitiveTopology = PrimitiveTopology.PrimitiveTopologyTriangleList,
                GraphicsPipelineRasterizerDescCullMode = MapCullMode(effectiveCullMode, reverseCulling),
                GraphicsPipelineRasterizerDescFillMode = MapFillMode(effectiveFillMode),
                GraphicsPipelineRasterizerDescFrontCounterClockwise = false,
                GraphicsPipelineDepthStencilDescDepthEnable = pass.DepthTestMode != CompareMode.Always || pass.DepthWrite,
                GraphicsPipelineDepthStencilDescDepthWriteEnable = pass.DepthWrite,
                GraphicsPipelineDepthStencilDescDepthFunc = MapCompareMode(pass.DepthTestMode),
                GraphicsPipelineInputLayoutLayoutElements = layoutArray.First,
                GraphicsPipelineInputLayoutNumElements = (uint)layoutArray.Count
            };

            using var desc = swapChain.GetDesc();
            psoCreateInfo.GraphicsPipelineRTVFormats[0] = desc.ColorBufferFormat;
            psoCreateInfo.GraphicsPipelineDSVFormat = desc.DepthBufferFormat;

            device.CreateGraphicsPipelineState(psoCreateInfo, out var pipelineState);
            if (pipelineState.IsNull)
                throw new InvalidOperationException($"Failed to create pipeline state for {pass.VertexShader}/{pass.PixelShader}.");

            var cameraBuffer = CreateUniformBuffer(device, "Camera", CameraBufferSize);
            var objectBuffer = CreateUniformBuffer(device, "Object", ObjectBufferSize);
            var materialBuffer = CreateUniformBuffer(device, "Material", MaterialBufferSize);
            var lightBuffer = CreateUniformBuffer(device, "Light", LightBufferSize);
            var zoneBuffer = CreateUniformBuffer(device, "Zone", ZoneBufferSize);
            pipelineState.CreateShaderResourceBinding(out var shaderResources, true);
            if (shaderResources.IsNull)
                throw new InvalidOperationException($"Failed to create shader resources for {pass.VertexShader}/{pass.PixelShader}.");

            DumpFirstPipelineDiagnostics(pass, key, vertexShaderResource, pixelShaderResource, vertexDefines, pixelDefines,
                vertexShader, pixelShader, pipelineState, shaderResources);

            cached = new PipelineResources(pipelineState, vertexShader, pixelShader, cameraBuffer, objectBuffer, materialBuffer,
                lightBuffer, zoneBuffer, shaderResources, BuildShaderVariableLookup(shaderResources), layoutArray);
            layoutArray = null;
            pipelines[key] = cached;
            return cached;
        }
        finally
        {
            layoutArray?.Dispose();
        }
    }

    private UploadedGeometry? GetOrCreateUploadedGeometry(Geometry geometry)
    {
        if (uploadedGeometries.TryGetValue(geometry, out var uploaded))
            return uploaded;

        if (device is null || immediateContext is null)
            return null;

        var vertexBuffer = geometry.GetVertexBuffer(0);
        var indexBuffer = geometry.GetIndexBuffer();
        if (vertexBuffer is null || indexBuffer is null)
            return null;

        var gpuVertexBuffer = GetOrCreateUploadedVertexBuffer(vertexBuffer);
        var gpuIndexBuffer = GetOrCreateUploadedIndexBuffer(indexBuffer);
        var drawIndexedAttribs = new DrawIndexedAttribs(geometry.GetIndexCount(),
            indexBuffer.LargeIndices ? global::RedBolt.Bindings.Diligent.ValueType.VtUint32 : global::RedBolt.Bindings.Diligent.ValueType.VtUint16,
            DrawFlags.DrawFlagVerifyAll,
            _FirstIndexLocation: geometry.GetIndexStart());

        uploaded = new UploadedGeometry(gpuVertexBuffer, gpuIndexBuffer, drawIndexedAttribs);
        uploadedGeometries[geometry] = uploaded;
        return uploaded;
    }

    private void BindMaterialTextures(PipelineResources pipeline, Material material)
    {
        BindTextureToAvailableStages(pipeline,
            "sAlbedo", GetTextureView(material.GetTexture("Albedo"), GetDefaultAlbedoTexture()));
        BindTextureToAvailableStages(pipeline,
            "sNormal", GetTextureView(material.GetTexture("Normal"), GetDefaultNormalTexture()));
        BindTextureToAvailableStages(pipeline,
            "sProperties", GetTextureView(material.GetTexture("Properties"), GetDefaultPropertiesTexture()));
        BindTextureToAvailableStages(pipeline,
            "sEmission", GetTextureView(material.GetTexture("Emission"), GetDefaultEmissionTexture()));
    }

    private ITextureView GetTextureView(Texture? texture, Texture2D fallbackTexture)
    {
        if (texture is Texture2D texture2D)
            return GetOrCreateUploadedTexture(texture2D).ShaderView;

        return GetOrCreateUploadedTexture(fallbackTexture).ShaderView;
    }

    private IBuffer GetOrCreateUploadedVertexBuffer(VertexBuffer vertexBuffer)
    {
        if (uploadedVertexBuffers.TryGetValue(vertexBuffer, out var uploaded))
            return uploaded;

        if (device is null || immediateContext is null)
            throw new InvalidOperationException("Graphics device is not initialized.");

        var vertexData = vertexBuffer.GetData().ToArray();
        uploaded = CreateBuffer(device, immediateContext, "RedBoltModelVB", vertexData,
            (ulong)vertexData.Length, BindFlags.BindVertexBuffer, vertexBuffer.GetVertexSize());
        uploadedVertexBuffers[vertexBuffer] = uploaded;
        return uploaded;
    }

    private IBuffer GetOrCreateUploadedIndexBuffer(IndexBuffer indexBuffer)
    {
        if (uploadedIndexBuffers.TryGetValue(indexBuffer, out var uploaded))
            return uploaded;

        if (device is null || immediateContext is null)
            throw new InvalidOperationException("Graphics device is not initialized.");

        uploaded = CreateIndexBuffer(device, immediateContext, indexBuffer, indexBuffer.GetUnpackedData().ToArray());
        uploadedIndexBuffers[indexBuffer] = uploaded;
        return uploaded;
    }

    private Texture2D GetDefaultAlbedoTexture()
    {
        return defaultAlbedoTexture ??= CreateFallbackTexture("#fallback-albedo", DefaultAlbedoTextureData);
    }

    private Texture2D GetDefaultNormalTexture()
    {
        return defaultNormalTexture ??= CreateFallbackTexture("#fallback-normal", DefaultNormalTextureData);
    }

    private Texture2D GetDefaultPropertiesTexture()
    {
        return defaultPropertiesTexture ??= CreateFallbackTexture("#fallback-properties", DefaultPropertiesTextureData);
    }

    private Texture2D GetDefaultEmissionTexture()
    {
        return defaultEmissionTexture ??= CreateFallbackTexture("#fallback-emission", DefaultEmissionTextureData);
    }

    private static Texture2D CreateFallbackTexture(string name, ReadOnlySpan<byte> rgbaPixels)
    {
        var texture = new Texture2D(name);
        texture.SetSize(1, 1, TextureFormat.RGBA8);
        texture.SetData(0, 0, 0, 1, 1, rgbaPixels);
        return texture;
    }

    private UploadedTexture GetOrCreateUploadedTexture(Texture2D texture)
    {
        if (uploadedTextures.TryGetValue(texture, out var uploaded))
            return uploaded;

        if (device is null)
            throw new InvalidOperationException("Graphics device is not initialized.");

        var data = texture.GetData(0) ?? throw new InvalidOperationException($"Texture {texture.Name} has no base level data.");
        using var nativeName = new PinnedUtf8(texture.Name);
        using var textureDescOwner = CreateTextureDesc();
        var textureDesc = textureDescOwner.Desc;
        textureDesc.Name = nativeName.Pointer;
        textureDesc.Type = ResourceDimension.ResourceDimTex2d;
        textureDesc.Width = (uint)texture.Width;
        textureDesc.Height = (uint)texture.Height;
        textureDesc.ArraySizeOrDepth = 1;
        textureDesc.MipLevels = 1;
        textureDesc.BindFlags = BindFlags.BindShaderResource;
        textureDesc.Usage = Usage.UsageImmutable;
        textureDesc.Format = RedBolt.Bindings.Diligent.TextureFormat.TexFormatRgba8Unorm;

        using var dataHandle = new PinnedBuffer(data);
        using var subresource = new TextureSubResData(dataHandle.Pointer, (nuint)(texture.Width * 4), 0);
        using var textureData = new TextureData(subresource, 1, immediateContext);
        device.CreateTexture(textureDesc, textureData, out var gpuTexture);
        if (gpuTexture.IsNull)
            throw new InvalidOperationException($"Failed to create GPU texture for {texture.Name}.");

        var shaderView = gpuTexture.GetDefaultView(TextureViewType.TextureViewShaderResource);
        uploaded = new UploadedTexture(gpuTexture, shaderView);
        uploadedTextures[texture] = uploaded;
        return uploaded;
    }

    private void UpdateCameraBuffer(IBuffer buffer, Camera camera, Zone? zone)
    {
        var data = new byte[CameraBufferSize];
        WriteMatrix4(data, 0, camera.GetView().Matrix4);
        WriteMatrix4(data, 64, camera.GetInverseView().Matrix4);
        WriteMatrix4(data, 128, camera.GetGPUViewProj());
        WriteVector4(data, 192, new Vector4(0.0f, 0.0f, 0.0f, 1.0f));
        var cameraPosition = camera.IsAttached ? camera.Node.WorldPosition : Vector3.Zero;
        WriteVector4(data, 208, new Vector4(cameraPosition, 0.0f));
        WriteFloat(data, 224, camera.GetNearClip());
        WriteFloat(data, 228, camera.GetFarClip());
        WriteVector4(data, 240, GetDepthMode(camera));
        var frustumSize = camera.GetViewSizeAt(camera.GetFarClip());
        WriteVector4(data, 256, new Vector4(frustumSize.X, frustumSize.Y, camera.GetFarClip(), 0.0f));
        WriteVector4(data, 272, new Vector4(0.0f, 0.0f, 1.0f, 1.0f));
        WriteVector4(data, 288, GetDepthReconstruct(camera));
        WriteVector2(data, 304, new Vector2(1.0f / Math.Max(Width, 1), 1.0f / Math.Max(Height, 1)));
        WriteVector4(data, 320, zone is not null ? new Vector4(zone.GetAmbientLighting(), 1.0f) : DefaultAmbient);
        WriteVector4(data, 336, GetFogParameters(camera, zone));
        WriteVector3(data, 352, zone is not null ? zone.GetFogColor().ToVector3() : new Vector3(DefaultFogColor.X, DefaultFogColor.Y, DefaultFogColor.Z));
        WriteFloat(data, 364, 0.0f);
        UpdateBuffer(buffer, data);
    }

    private void UpdateObjectBuffer(IBuffer buffer, Matrix4 modelMatrix, Vector4 ambient)
    {
        var data = new byte[ObjectBufferSize];
        WriteMatrix4(data, 0, modelMatrix);
        WriteVector4(data, 64, ambient);
        UpdateBuffer(buffer, data);
    }

    private void UpdateMaterialBuffer(IBuffer buffer, Material material)
    {
        var data = new byte[MaterialBufferSize];
        WriteVector4(data, 0, material.GetShaderParameter("UOffset") is Vector4 uOffset ? uOffset : Vector4.Zero);
        WriteVector4(data, 16, material.GetShaderParameter("VOffset") is Vector4 vOffset ? vOffset : Vector4.Zero);
        WriteVector4(data, 32, material.GetShaderParameter("MatDiffColor") is Vector4 diff ? diff : Vector4.One);
        WriteVector4(data, 48, material.GetShaderParameter("MatSpecColor") is Vector4 spec ? spec : new Vector4(0.0f, 0.0f, 0.0f, 1.0f));

        var emissive = material.GetShaderParameter("MatEmissiveColor") as Vector3? ?? Vector3.Zero;
        var roughness = material.GetShaderParameter("Roughness") is float roughnessValue ? roughnessValue : 1.0f;
        WriteVector4(data, 64, new Vector4(emissive, roughness));

        var env = material.GetShaderParameter("MatEnvMapColor") as Vector3? ?? Vector3.One;
        var metallic = material.GetShaderParameter("Metallic") is float metallicValue ? metallicValue : 1.0f;
        WriteVector4(data, 80, new Vector4(env, metallic));

        var normalScale = material.GetShaderParameter("NormalScale") is float normalScaleValue ? normalScaleValue : 1.0f;
        var dielectricReflectance = material.GetShaderParameter("DielectricReflectance") is float dielectric ? dielectric : 0.5f;
        var alphaCutoff = material.GetShaderParameter("AlphaCutoff") is float alpha ? alpha : 0.5f;
        WriteVector4(data, 96, new Vector4(normalScale, dielectricReflectance, alphaCutoff, 0.0f));
        WriteVector4(data, 112, new Vector4(0.0f, 1.0f, 0.0f, 0.0f));
        UpdateBuffer(buffer, data);
    }

    private void UpdateLightBuffer(IBuffer buffer, Light? light)
    {
        var data = new byte[LightBufferSize];
        if (light is not null && light.IsAttached)
        {
            WriteVector4(data, 0, new Vector4(light.Node.WorldPosition, 0.0f));
            WriteVector4(data, 16, new Vector4((-light.Node.WorldDirection).Normalized, 0.0f));
            WriteVector4(data, 32, new Vector4(light.GetEffectiveColor().ToVector3(), light.GetEffectiveSpecularIntensity()));
        }
        else
        {
            WriteVector4(data, 0, Vector4.Zero);
            WriteVector4(data, 16, new Vector4(0.0f, -1.0f, 0.0f, 0.0f));
            WriteVector4(data, 32, Vector4.Zero);
        }

        UpdateBuffer(buffer, data);
    }

    private void UpdateZoneBuffer(IBuffer buffer, Zone? zone)
    {
        var data = new byte[ZoneBufferSize];
        WriteFloat(data, 0, GetZoneRoughnessToLodFactor(zone));
        WriteFloat(data, 4, 0.0f);
        WriteFloat(data, 8, 0.0f);
        WriteFloat(data, 12, 0.0f);
        UpdateBuffer(buffer, data);
    }

    private static string ComposeShaderDefines(Pass pass, VertexBuffer vertexBuffer, Material material,
        Light? light, ShaderStage stage)
    {
        var defines = new List<string> { "URHO3D_GEOMETRY_STATIC" };

        if (PassUsesAmbient(pass))
        {
            defines.Add("URHO3D_AMBIENT_PASS");
            defines.Add("URHO3D_AMBIENT_FLAT");
        }

        if (PassUsesLighting(pass) && light is not null)
            AddLightDefines(defines, light);

        if ((vertexBuffer.GetElementMask() & (uint)VertexMask.Normal) != 0)
        {
            defines.Add("URHO3D_VERTEX_HAS_NORMAL");
            defines.Add("URHO3D_VERTEX_NORMAL_AVAILABLE");
        }
        if ((vertexBuffer.GetElementMask() & (uint)VertexMask.Tangent) != 0)
        {
            defines.Add("URHO3D_VERTEX_HAS_TANGENT");
            defines.Add("URHO3D_VERTEX_TANGENT_AVAILABLE");
        }
        if ((vertexBuffer.GetElementMask() & (uint)VertexMask.TexCoord1) != 0)
            defines.Add("URHO3D_VERTEX_HAS_TEXCOORD0");
        if ((vertexBuffer.GetElementMask() & (uint)VertexMask.TexCoord2) != 0)
            defines.Add("URHO3D_VERTEX_HAS_TEXCOORD1");
        if ((vertexBuffer.GetElementMask() & (uint)VertexMask.Color) != 0)
            defines.Add("URHO3D_VERTEX_HAS_COLOR");

        AddTextureDefine(defines, material.GetTexture("Albedo"), "ALBEDO");
        AddTextureDefine(defines, material.GetTexture("Normal"), "NORMAL");
        AddTextureDefine(defines, material.GetTexture("Properties"), "PROPERTIES");
        AddTextureDefine(defines, material.GetTexture("Emission"), "EMISSION");

        var passDefines = stage == ShaderStage.Vertex ? pass.GetEffectiveVertexShaderDefines() : pass.GetEffectivePixelShaderDefines();
        if (!string.IsNullOrWhiteSpace(passDefines))
            defines.AddRange(passDefines.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        return string.Join(' ', defines.Distinct(StringComparer.OrdinalIgnoreCase));
    }

    private static void AddTextureDefine(ICollection<string> defines, Texture? texture, string name)
    {
        if (texture is not null)
            defines.Add($"URHO3D_TEXTURE_{name}=1");
    }

    private static void AddLightDefines(ICollection<string> defines, Light light)
    {
        defines.Add(light.GetLightType() switch
        {
            LightType.Directional => "URHO3D_LIGHT_DIRECTIONAL",
            LightType.Spot => "URHO3D_LIGHT_SPOT",
            LightType.Point => "URHO3D_LIGHT_POINT",
            _ => "URHO3D_LIGHT_DIRECTIONAL"
        });
    }

    private static bool PassUsesAmbient(Pass pass)
    {
        return pass.Name is "base" or "alpha" or "litbase" or "litalpha";
    }

    private static bool PassUsesLighting(Pass pass)
    {
        return pass.Name is "litbase" or "litalpha" or "light";
    }

    private static Pass? SelectPass(Technique technique, Light? directionalLight)
    {
        if (directionalLight is not null)
            return technique.GetPass("litbase") ?? technique.GetPass("base") ?? technique.GetPass("light");

        return technique.GetPass("base") ?? technique.GetPass("litbase") ?? technique.GetPass("light");
    }

    private static string BuildLayoutSignature(VertexBuffer vertexBuffer)
    {
        return string.Join('|', vertexBuffer.GetElements().Select(static element =>
            $"{element.Semantic}:{element.Index}:{element.Type}:{element.Offset}:{element.StepRate}"));
    }

    private static LayoutElementSpec[] BuildLayoutElements(VertexBuffer vertexBuffer)
    {
        var elements = new List<LayoutElementSpec>();
        AddLayoutElement(elements, vertexBuffer, VertexElementSemantic.Position, 0, 0);
        AddLayoutElement(elements, vertexBuffer, VertexElementSemantic.BlendWeights, 0, 1);
        AddLayoutElement(elements, vertexBuffer, VertexElementSemantic.BlendIndices, 0, 2);
        AddLayoutElement(elements, vertexBuffer, VertexElementSemantic.TexCoord, 0, 3);
        AddLayoutElement(elements, vertexBuffer, VertexElementSemantic.TexCoord, 1, 4);
        AddLayoutElement(elements, vertexBuffer, VertexElementSemantic.Color, 0, 5);
        AddLayoutElement(elements, vertexBuffer, VertexElementSemantic.Normal, 0, 6);
        AddLayoutElement(elements, vertexBuffer, VertexElementSemantic.Tangent, 0, 7);
        return elements.ToArray();
    }

    private static void AddLayoutElement(List<LayoutElementSpec> target, VertexBuffer vertexBuffer,
        VertexElementSemantic semantic, byte index, uint inputIndex)
    {
        var element = vertexBuffer.GetElements().FirstOrDefault(existing => existing.Semantic == semantic && existing.Index == index);
        if (element == default && semantic != VertexElementSemantic.Position)
            return;

        if (semantic == VertexElementSemantic.Position && vertexBuffer.GetElementOffset(semantic, index) == uint.MaxValue)
            return;

        target.Add(new LayoutElementSpec
        {
            InputIndex = inputIndex,
            BufferSlot = 0,
            NumComponents = GetNumComponents(element.Type),
            ValueType = GetValueType(element.Type),
            IsNormalized = IsNormalized(element.Type),
            RelativeOffset = element.Offset,
            Stride = vertexBuffer.GetVertexSize(),
            Frequency = InputElementFrequency.InputElementFrequencyPerVertex,
            InstanceDataStepRate = 1
        });
    }

    private static uint GetNumComponents(VertexElementType type) => type switch
    {
        VertexElementType.Float or VertexElementType.Int => 1,
        VertexElementType.Vector2 => 2,
        VertexElementType.Vector3 => 3,
        VertexElementType.Vector4 or VertexElementType.UByte4 or VertexElementType.UByte4Norm => 4,
        _ => 4
    };

    private static global::RedBolt.Bindings.Diligent.ValueType GetValueType(VertexElementType type) => type switch
    {
        VertexElementType.Int => global::RedBolt.Bindings.Diligent.ValueType.VtInt32,
        VertexElementType.UByte4 or VertexElementType.UByte4Norm => global::RedBolt.Bindings.Diligent.ValueType.VtUint8,
        _ => global::RedBolt.Bindings.Diligent.ValueType.VtFloat32
    };

    private static bool IsNormalized(VertexElementType type) => type == VertexElementType.UByte4Norm;

    private static global::RedBolt.Bindings.Diligent.CullMode MapCullMode(CullMode cullMode, bool reverseCulling)
    {
        var result = cullMode switch
        {
            CullMode.None => RedBolt.Bindings.Diligent.CullMode.CullModeNone,
            CullMode.Clockwise => RedBolt.Bindings.Diligent.CullMode.CullModeFront,
            _ => RedBolt.Bindings.Diligent.CullMode.CullModeBack
        };

        if (!reverseCulling)
            return result;

        return result switch
        {
            RedBolt.Bindings.Diligent.CullMode.CullModeFront => RedBolt.Bindings.Diligent.CullMode.CullModeBack,
            RedBolt.Bindings.Diligent.CullMode.CullModeBack => RedBolt.Bindings.Diligent.CullMode.CullModeFront,
            _ => result
        };
    }

    private static global::RedBolt.Bindings.Diligent.FillMode MapFillMode(FillMode fillMode) => fillMode switch
    {
        FillMode.Wireframe or FillMode.Point => RedBolt.Bindings.Diligent.FillMode.FillModeWireframe,
        _ => RedBolt.Bindings.Diligent.FillMode.FillModeSolid
    };

    private static ComparisonFunction MapCompareMode(CompareMode compareMode) => compareMode switch
    {
        CompareMode.Always => ComparisonFunction.ComparisonFuncAlways,
        CompareMode.Equal => ComparisonFunction.ComparisonFuncEqual,
        CompareMode.NotEqual => ComparisonFunction.ComparisonFuncNotEqual,
        CompareMode.Less => ComparisonFunction.ComparisonFuncLess,
        CompareMode.LessEqual => ComparisonFunction.ComparisonFuncLessEqual,
        CompareMode.Greater => ComparisonFunction.ComparisonFuncGreater,
        CompareMode.GreaterEqual => ComparisonFunction.ComparisonFuncGreaterEqual,
        _ => ComparisonFunction.ComparisonFuncLessEqual
    };

    private static FillMode GetEffectiveFillMode(Camera camera, Material material)
    {
        return (FillMode)Math.Max((int)camera.GetFillMode(), (int)material.GetFillMode());
    }

    private IntRect GetEffectiveViewportRect(Viewport viewport)
    {
        var rect = viewport.GetRect();
        if (rect.Width <= 0 || rect.Height <= 0)
            return new IntRect(0, 0, Width, Height);

        return new IntRect(
            Math.Clamp(rect.Left, 0, Width),
            Math.Clamp(rect.Top, 0, Height),
            Math.Clamp(rect.Right, 0, Width),
            Math.Clamp(rect.Bottom, 0, Height));
    }

    private static Vector4 GetDepthMode(Camera camera)
    {
        if (camera.IsOrthographic())
            return new Vector4(1.0f, 0.0f, 0.5f, 0.5f);

        return new Vector4(0.0f, 0.0f, 0.0f, 1.0f / Math.Max(camera.GetFarClip(), MathDefs.Epsilon));
    }

    private static Vector4 GetDepthReconstruct(Camera camera)
    {
        var nearClip = camera.GetNearClip();
        var farClip = camera.GetFarClip();
        var range = Math.Max(farClip - nearClip, MathDefs.Epsilon);
        return new Vector4(
            farClip / range,
            -nearClip / range,
            camera.IsOrthographic() ? 1.0f : 0.0f,
            camera.IsOrthographic() ? 0.0f : 1.0f);
    }

    private static Vector4 GetFogParameters(Camera camera, Zone? zone)
    {
        var farClip = camera.GetFarClip();
        var fogStart = zone is not null ? Math.Min(zone.GetFogStart(), farClip) : farClip;
        var fogEnd = zone is not null ? Math.Min(zone.GetFogEnd(), farClip) : farClip;
        if (fogStart >= fogEnd * (1.0f - 0.0001f))
            fogStart = fogEnd * (1.0f - 0.0001f);
        var fogRange = Math.Max(fogEnd - fogStart, MathDefs.Epsilon);
        return new Vector4(fogEnd / farClip, farClip / fogRange, 0.0f, 0.0f);
    }

    private static float GetZoneRoughnessToLodFactor(Zone? zone)
    {
        return zone?.GetZoneTexture() is TextureCube zoneTexture && zoneTexture.Width > 0
            ? MathF.Log2(zoneTexture.Width)
            : 1.0f;
    }

    private static Zone? FindZone(Scene scene, Vector3 worldPosition)
    {
        return scene.GetAllComponents().Values
            .OfType<Zone>()
            .Where(static zone => zone.IsAttached && zone.IsEnabledEffective)
            .OrderByDescending(static zone => zone.GetPriority())
            .FirstOrDefault(zone => zone.IsInside(worldPosition));
    }

    private static Vector4 GetAmbientForDrawable(Scene scene, Drawable drawable)
    {
        var zone = FindZone(scene, drawable.Node.WorldPosition);
        return zone is not null ? new Vector4(zone.GetAmbientLighting(), 1.0f) : DefaultAmbient;
    }

    private static void BindTexture(PipelineResources pipeline, global::RedBolt.Bindings.Diligent.ShaderType shaderType,
        string name, ITextureView? textureView, bool logIfMissing = true)
    {
        if (textureView is null)
            return;

        if (pipeline.TryGetShaderVariable(shaderType, name, out var variable))
        {
            variable.Set(textureView);
        }
        else if (logIfMissing && ShaderBindingDiagnosticsEnabled)
        {
            Console.Error.WriteLine($"RedBolt shader binding diagnostics: texture '{name}' was not found for stage '{shaderType}'.");
        }
    }

    private static void BindTextureToAvailableStages(PipelineResources pipeline, string name, ITextureView textureView)
    {
        BindTexture(pipeline, global::RedBolt.Bindings.Diligent.ShaderType.ShaderTypeVertex, name, textureView, logIfMissing: false);
        BindTexture(pipeline, global::RedBolt.Bindings.Diligent.ShaderType.ShaderTypePixel, name, textureView, logIfMissing: false);
    }

    private static void BindDynamicBuffer(PipelineResources pipeline,
        global::RedBolt.Bindings.Diligent.ShaderType shaderType, string name, IBuffer buffer, nuint size)
    {
        if (pipeline.TryGetShaderVariable(shaderType, name, out var variable))
        {
            variable.SetBufferRange(buffer, 0, size);
        }
        else if (ShaderBindingDiagnosticsEnabled)
        {
            Console.Error.WriteLine($"RedBolt shader binding diagnostics: dynamic buffer '{name}' was not found for stage '{shaderType}'.");
        }
    }

    private static IShader CreateShader(IRenderDevice device, global::RedBolt.Bindings.Diligent.ShaderType shaderType,
        string name, string source)
    {
        using var shaderName = new PinnedUtf8(name);
        using var entryPoint = new PinnedUtf8("main");
        using var shaderSource = new PinnedUtf8(source);
        using var shaderDesc = new ShaderDesc
        {
            Name = shaderName.Pointer,
            ShaderType = shaderType,
            UseCombinedTextureSamplers = true
        };
        using var shaderCreateInfo = new ShaderCreateInfo
        {
            Desc = shaderDesc,
            EntryPoint = entryPoint.Pointer,
            Source = shaderSource.Pointer,
            SourceLanguage = global::RedBolt.Bindings.Diligent.ShaderSourceLanguage.ShaderSourceLanguageGlslVerbatim,
            LoadConstantBufferReflection = true
        };

        device.CreateShader(shaderCreateInfo, out var shader, out var compilerOutput);
        ReleaseIfNotNull(compilerOutput);

        if (shader.IsNull)
            throw new InvalidOperationException($"Generated CreateShader returned a null {shaderType} shader for {name}.");

        return shader;
    }

    private static IBuffer CreateUniformBuffer(IRenderDevice device, string name, uint size)
    {
        using var nativeName = new PinnedUtf8(name);
        using var bufferDesc = new BufferDesc();
        bufferDesc.Name = nativeName.Pointer;
        bufferDesc.Size = (nuint)size;
        bufferDesc.BindFlags = BindFlags.BindUniformBuffer;
        bufferDesc.Usage = Usage.UsageDynamic;
        bufferDesc.CPUAccessFlags = CpuAccessFlags.CpuAccessWrite;
        device.CreateBuffer(bufferDesc, null!, out var buffer);
        if (buffer.IsNull)
            throw new InvalidOperationException($"CreateBuffer returned null for uniform buffer {name}.");
        return buffer;
    }

    private static IBuffer CreateBuffer(IRenderDevice device, IDeviceContext immediateContext, string name, byte[] data,
        ulong size, BindFlags bindFlags, uint elementStride)
    {
        using var nativeName = new PinnedUtf8(name);
        using var bufferDesc = new BufferDesc();
        bufferDesc.Name = nativeName.Pointer;
        bufferDesc.Size = (nuint)size;
        bufferDesc.BindFlags = bindFlags;
        bufferDesc.Usage = Usage.UsageImmutable;
        bufferDesc.ElementByteStride = elementStride;
        using var dataHandle = new PinnedBuffer(data);
        using var bufferData = new BufferData(dataHandle.Pointer, (nuint)size, immediateContext);
        device.CreateBuffer(bufferDesc, bufferData, out var buffer);
        if (buffer.IsNull)
            throw new InvalidOperationException($"CreateBuffer returned null for {name}.");
        return buffer;
    }

    private static IBuffer CreateIndexBuffer(IRenderDevice device, IDeviceContext immediateContext, IndexBuffer indexBuffer, uint[] indices)
    {
        byte[] data;
        if (indexBuffer.LargeIndices)
        {
            data = new byte[indices.Length * sizeof(uint)];
            Buffer.BlockCopy(indices, 0, data, 0, data.Length);
        }
        else
        {
            var narrowed = indices.Select(static value => checked((ushort)value)).ToArray();
            data = new byte[narrowed.Length * sizeof(ushort)];
            Buffer.BlockCopy(narrowed, 0, data, 0, data.Length);
        }

        return CreateBuffer(device, immediateContext, "RedBoltModelIB", data,
            (ulong)data.Length, BindFlags.BindIndexBuffer, indexBuffer.GetIndexSize());
    }

    private void UpdateBuffer(IBuffer buffer, byte[] data)
    {
        if (immediateContext is null)
            return;

        using var dataHandle = new PinnedBuffer(data);
        immediateContext.UpdateBuffer(buffer, 0, (nuint)data.Length, dataHandle.Pointer,
            ResourceStateTransitionMode.ResourceStateTransitionModeTransition);
    }

    private static TextureDescOwner CreateTextureDesc()
    {
        var factory = DiligentNativeBindings.GetEngineFactoryOpenGL();
        var apiInfo = factory.GetAPIInfo();
        return new TextureDescOwner((int)apiInfo.TextureDescSize);
    }

    private static void WriteFloat(byte[] buffer, int offset, float value)
    {
        Buffer.BlockCopy(BitConverter.GetBytes(value), 0, buffer, offset, sizeof(float));
    }

    private static void WriteVector2(byte[] buffer, int offset, Vector2 value)
    {
        WriteFloat(buffer, offset + 0, value.X);
        WriteFloat(buffer, offset + 4, value.Y);
    }

    private static void WriteVector3(byte[] buffer, int offset, Vector3 value)
    {
        WriteFloat(buffer, offset + 0, value.X);
        WriteFloat(buffer, offset + 4, value.Y);
        WriteFloat(buffer, offset + 8, value.Z);
    }

    private static void WriteVector4(byte[] buffer, int offset, Vector4 value)
    {
        WriteFloat(buffer, offset + 0, value.X);
        WriteFloat(buffer, offset + 4, value.Y);
        WriteFloat(buffer, offset + 8, value.Z);
        WriteFloat(buffer, offset + 12, value.W);
    }

    private static void WriteMatrix4(byte[] buffer, int offset, Matrix4 value)
    {
        // rbfx stores Matrix4 in row-major CPU order and memcpy's it into uniform buffers.
        // GLSL consumes mat4 from std140 as column-major storage and shaders use vec4 * mat4,
        // so uploading raw rbfx memory yields the expected transposed shader-side math.
        Buffer.BlockCopy(value.Data, 0, buffer, offset, 16 * sizeof(float));
    }

    private static void CheckSdlGl(int result, string operation)
    {
        if (result != 0)
            throw new InvalidOperationException($"{operation} failed with {result}.");
    }

    private static void ReleaseIfNotNull(IObject? value)
    {
        if (value is not null && !value.IsNull)
            _ = value.Release();
    }

    private static void CopyMappedTexture(RedBolt.Bindings.Diligent.TextureFormat format, int width, int height, MappedTextureSubresource mappedSubresource, byte[] rgbaPixels)
    {
        var rowLength = checked(width * 4);
        var row = new byte[rowLength];
        for (var sourceY = 0; sourceY < height; ++sourceY)
        {
            var sourceRow = IntPtr.Add(mappedSubresource.PData, checked((int)(mappedSubresource.Stride * (ulong)sourceY)));
            Marshal.Copy(sourceRow, row, 0, rowLength);

            var destinationOffset = (height - 1 - sourceY) * rowLength;
            switch (format)
            {
                case RedBolt.Bindings.Diligent.TextureFormat.TexFormatRgba8Unorm:
                case RedBolt.Bindings.Diligent.TextureFormat.TexFormatRgba8UnormSrgb:
                    Buffer.BlockCopy(row, 0, rgbaPixels, destinationOffset, rowLength);
                    break;

                case RedBolt.Bindings.Diligent.TextureFormat.TexFormatBgra8Unorm:
                case RedBolt.Bindings.Diligent.TextureFormat.TexFormatBgra8UnormSrgb:
                    for (var x = 0; x < width; ++x)
                    {
                        var sourceOffset = x * 4;
                        var destinationPixelOffset = destinationOffset + sourceOffset;
                        rgbaPixels[destinationPixelOffset + 0] = row[sourceOffset + 2];
                        rgbaPixels[destinationPixelOffset + 1] = row[sourceOffset + 1];
                        rgbaPixels[destinationPixelOffset + 2] = row[sourceOffset + 0];
                        rgbaPixels[destinationPixelOffset + 3] = row[sourceOffset + 3];
                    }
                    break;

                default:
                    throw new NotSupportedException($"Screenshot capture does not support backbuffer format '{format}'.");
            }
        }
    }

    private static void FlipImageRows(byte[] rgbaPixels, int width, int height)
    {
        var rowLength = checked(width * 4);
        var temporaryRow = new byte[rowLength];
        for (var y = 0; y < height / 2; ++y)
        {
            var topOffset = y * rowLength;
            var bottomOffset = (height - 1 - y) * rowLength;

            Buffer.BlockCopy(rgbaPixels, topOffset, temporaryRow, 0, rowLength);
            Buffer.BlockCopy(rgbaPixels, bottomOffset, rgbaPixels, topOffset, rowLength);
            Buffer.BlockCopy(temporaryRow, 0, rgbaPixels, bottomOffset, rowLength);
        }
    }

    private static bool IsRenderDiagnosticsEnabled()
    {
        var value = Environment.GetEnvironmentVariable("REDBOLT_RENDER_DIAGNOSTICS");
        return value is not null && value != "0" && !value.Equals("false", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsShaderBindingDiagnosticsEnabled()
    {
        var value = Environment.GetEnvironmentVariable("REDBOLT_SHADER_BINDING_DIAGNOSTICS");
        return value is not null && value != "0" && !value.Equals("false", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsShaderReflectionDiagnosticsEnabled()
    {
        var value = Environment.GetEnvironmentVariable("REDBOLT_SHADER_REFLECTION_DIAGNOSTICS");
        return value is not null && value != "0" && !value.Equals("false", StringComparison.OrdinalIgnoreCase);
    }

    private void DumpFirstPipelineDiagnostics(Pass pass, PipelineKey key, Shader vertexShaderResource, Shader pixelShaderResource,
        string vertexDefines, string pixelDefines, IShader vertexShader, IShader pixelShader, IPipelineState pipelineState,
        IShaderResourceBinding shaderResources)
    {
        if (!ShaderReflectionDiagnosticsEnabled || shaderReflectionDiagnosticsDumped)
            return;

        shaderReflectionDiagnosticsDumped = true;

        var diagnosticBaseName = $"{SanitizeDiagnosticName(pass.VertexShader)}_{SanitizeDiagnosticName(pass.PixelShader)}";
        var vertexPath = Path.Combine(Path.GetTempPath(), $"{diagnosticBaseName}.vs.glsl");
        var pixelPath = Path.Combine(Path.GetTempPath(), $"{diagnosticBaseName}.ps.glsl");

        try
        {
            File.WriteAllText(vertexPath, vertexShaderResource.PrepareSource(ShaderStage.Vertex, vertexDefines, GraphicsBackend.OpenGL, ShaderExtraCode));
            File.WriteAllText(pixelPath, pixelShaderResource.PrepareSource(ShaderStage.Pixel, pixelDefines, GraphicsBackend.OpenGL, ShaderExtraCode));
        }
        catch (Exception error)
        {
            Console.Error.WriteLine($"RedBolt shader reflection diagnostics: failed to write shader source dump: {error.Message}");
        }

        Console.Error.WriteLine(
            $"RedBolt shader reflection diagnostics: first pipeline '{pass.VertexShader}/{pass.PixelShader}', layout='{key.LayoutSignature}', vertexSource='{vertexPath}', pixelSource='{pixelPath}'.");
        DumpShaderResources("vertex shader", vertexShader);
        DumpShaderResources("pixel shader", pixelShader);
        DumpPipelineVariables("pipeline", pipelineState, shaderResources);
    }

    private static void DumpShaderResources(string label, IShader shader)
    {
        var resourceCount = shader.GetResourceCount();
        Console.Error.WriteLine($"RedBolt shader reflection diagnostics: {label} resourceCount={resourceCount}.");
        for (uint resourceIndex = 0; resourceIndex < resourceCount; ++resourceIndex)
        {
            using var resourceDesc = new ShaderResourceDesc();
            shader.GetResourceDesc(resourceIndex, resourceDesc);
            var resourceName = PtrToUtf8String(resourceDesc.Name);
            var line =
                $"RedBolt shader reflection diagnostics: {label} resource[{resourceIndex}] name='{resourceName}', type={resourceDesc.Type}, arraySize={resourceDesc.ArraySize}";
            if (resourceDesc.Type == ShaderResourceType.ShaderResourceTypeConstantBuffer)
            {
                var bufferDesc = shader.GetConstantBufferDesc(resourceIndex);
                line += $", size={bufferDesc.Size}, variableCount={bufferDesc.NumVariables}";
            }

            Console.Error.WriteLine(line + ".");
        }
    }

    private static void DumpPipelineVariables(string label, IPipelineState pipelineState, IShaderResourceBinding shaderResources)
    {
        using var pipelineDesc = pipelineState.GetDesc();
        Console.Error.WriteLine(
            $"RedBolt shader reflection diagnostics: {label} defaultVariableType={pipelineDesc.ResourceLayoutDefaultVariableType}, staticSignatures={pipelineState.GetResourceSignatureCount()}.");

        DumpVariableList(label, "static", global::RedBolt.Bindings.Diligent.ShaderType.ShaderTypeVertex,
            pipelineState.GetStaticVariableCount(global::RedBolt.Bindings.Diligent.ShaderType.ShaderTypeVertex),
            index => pipelineState.GetStaticVariableByIndex(global::RedBolt.Bindings.Diligent.ShaderType.ShaderTypeVertex, index));
        DumpVariableList(label, "static", global::RedBolt.Bindings.Diligent.ShaderType.ShaderTypePixel,
            pipelineState.GetStaticVariableCount(global::RedBolt.Bindings.Diligent.ShaderType.ShaderTypePixel),
            index => pipelineState.GetStaticVariableByIndex(global::RedBolt.Bindings.Diligent.ShaderType.ShaderTypePixel, index));
        DumpVariableList(label, "srb", global::RedBolt.Bindings.Diligent.ShaderType.ShaderTypeVertex,
            shaderResources.GetVariableCount(global::RedBolt.Bindings.Diligent.ShaderType.ShaderTypeVertex),
            index => shaderResources.GetVariableByIndex(global::RedBolt.Bindings.Diligent.ShaderType.ShaderTypeVertex, index));
        DumpVariableList(label, "srb", global::RedBolt.Bindings.Diligent.ShaderType.ShaderTypePixel,
            shaderResources.GetVariableCount(global::RedBolt.Bindings.Diligent.ShaderType.ShaderTypePixel),
            index => shaderResources.GetVariableByIndex(global::RedBolt.Bindings.Diligent.ShaderType.ShaderTypePixel, index));
    }

    private static void DumpVariableList(string label, string scope, global::RedBolt.Bindings.Diligent.ShaderType shaderType,
        uint count, Func<uint, IShaderResourceVariable> getVariable)
    {
        Console.Error.WriteLine(
            $"RedBolt shader reflection diagnostics: {label} {scope} {shaderType} variableCount={count}.");
        for (uint variableIndex = 0; variableIndex < count; ++variableIndex)
        {
            var variable = getVariable(variableIndex);
            using var resourceDesc = new ShaderResourceDesc();
            variable.GetResourceDesc(resourceDesc);
            Console.Error.WriteLine(
                $"RedBolt shader reflection diagnostics: {label} {scope} {shaderType} variable[{variableIndex}] name='{PtrToUtf8String(resourceDesc.Name)}', type={resourceDesc.Type}, arraySize={resourceDesc.ArraySize}, variableType={variable.GetType()}.");
        }
    }

    private static string PtrToUtf8String(IntPtr pointer)
    {
        return pointer == IntPtr.Zero ? string.Empty : Marshal.PtrToStringUTF8(pointer) ?? string.Empty;
    }

    private static string SanitizeDiagnosticName(string name)
    {
        return string.Concat(name.Select(static ch => char.IsLetterOrDigit(ch) ? ch : '_'));
    }

    private static Dictionary<PipelineVariableKey, IShaderResourceVariable> BuildShaderVariableLookup(IShaderResourceBinding shaderResources)
    {
        var result = new Dictionary<PipelineVariableKey, IShaderResourceVariable>();
        AddShaderVariableLookupEntries(result, shaderResources, global::RedBolt.Bindings.Diligent.ShaderType.ShaderTypeVertex);
        AddShaderVariableLookupEntries(result, shaderResources, global::RedBolt.Bindings.Diligent.ShaderType.ShaderTypePixel);
        return result;
    }

    private static void AddShaderVariableLookupEntries(Dictionary<PipelineVariableKey, IShaderResourceVariable> result,
        IShaderResourceBinding shaderResources, global::RedBolt.Bindings.Diligent.ShaderType shaderType)
    {
        var count = shaderResources.GetVariableCount(shaderType);
        for (uint index = 0; index < count; ++index)
        {
            var variable = shaderResources.GetVariableByIndex(shaderType, index);
            using var resourceDesc = new ShaderResourceDesc();
            variable.GetResourceDesc(resourceDesc);
            var name = PtrToUtf8String(resourceDesc.Name);
            if (!string.IsNullOrWhiteSpace(name))
                result[new PipelineVariableKey(shaderType, name)] = variable;
        }
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void GlFinishDelegate();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void GlReadBufferDelegate(uint mode);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void GlReadPixelsDelegate(int x, int y, int width, int height, uint format, uint type, IntPtr pixels);

    private readonly record struct LayoutElementSpec(
        uint InputIndex,
        uint BufferSlot,
        uint NumComponents,
        global::RedBolt.Bindings.Diligent.ValueType ValueType,
        bool IsNormalized,
        uint RelativeOffset,
        uint Stride,
        InputElementFrequency Frequency,
        uint InstanceDataStepRate)
    {
        public LayoutElementSpec()
            : this(0, 0, 0, global::RedBolt.Bindings.Diligent.ValueType.VtFloat32, false, 0, 0,
                InputElementFrequency.InputElementFrequencyPerVertex, 1)
        {
        }
    }

    private readonly record struct PipelineKey(string VertexShader, string PixelShader, string VertexDefines,
        string PixelDefines, string LayoutSignature, CullMode CullMode, FillMode FillMode,
        CompareMode DepthTestMode, bool DepthWrite, bool AlphaToCoverage, bool ReverseCulling);

    private readonly record struct PipelineVariableKey(global::RedBolt.Bindings.Diligent.ShaderType ShaderType, string Name);

    private sealed class PipelineResources : IDisposable
    {
        public PipelineResources(IPipelineState pipelineState, IShader vertexShader, IShader pixelShader,
            IBuffer cameraBuffer, IBuffer objectBuffer, IBuffer materialBuffer, IBuffer lightBuffer, IBuffer zoneBuffer,
            IShaderResourceBinding shaderResources, Dictionary<PipelineVariableKey, IShaderResourceVariable> shaderVariables,
            TemporaryLayoutArray layoutArray)
        {
            PipelineState = pipelineState;
            VertexShader = vertexShader;
            PixelShader = pixelShader;
            CameraBuffer = cameraBuffer;
            ObjectBuffer = objectBuffer;
            MaterialBuffer = materialBuffer;
            LightBuffer = lightBuffer;
            ZoneBuffer = zoneBuffer;
            ShaderResources = shaderResources;
            ShaderVariables = shaderVariables;
            LayoutArray = layoutArray;
        }

        public IPipelineState PipelineState { get; }

        public IShader VertexShader { get; }

        public IShader PixelShader { get; }

        public IBuffer CameraBuffer { get; }

        public IBuffer ObjectBuffer { get; }

        public IBuffer MaterialBuffer { get; }

        public IBuffer LightBuffer { get; }

        public IBuffer ZoneBuffer { get; }

        public IShaderResourceBinding ShaderResources { get; }

        public Dictionary<PipelineVariableKey, IShaderResourceVariable> ShaderVariables { get; }

        public TemporaryLayoutArray LayoutArray { get; }

        public bool TryGetShaderVariable(global::RedBolt.Bindings.Diligent.ShaderType shaderType, string name,
            out IShaderResourceVariable variable)
        {
            return ShaderVariables.TryGetValue(new PipelineVariableKey(shaderType, name), out variable!);
        }

        public void Dispose()
        {
            LayoutArray.Dispose();
            ReleaseIfNotNull(ShaderResources);
            ReleaseIfNotNull(ZoneBuffer);
            ReleaseIfNotNull(LightBuffer);
            ReleaseIfNotNull(MaterialBuffer);
            ReleaseIfNotNull(ObjectBuffer);
            ReleaseIfNotNull(CameraBuffer);
            ReleaseIfNotNull(PipelineState);
            ReleaseIfNotNull(PixelShader);
            ReleaseIfNotNull(VertexShader);
        }
    }

    private sealed class TemporaryLayoutArray : IDisposable
    {
        private readonly IntPtr memory;
        private readonly PinnedUtf8 semantic;

        public TemporaryLayoutArray(IReadOnlyList<LayoutElementSpec> elements)
        {
            semantic = new PinnedUtf8("ATTRIB");
            Count = elements.Count;
            if (Count == 0)
                throw new InvalidOperationException("Graphics pipeline layout may not be empty.");

            var layoutElementSize = (int)DiligentNativeBindings.GetEngineFactoryOpenGL().GetAPIInfo().LayoutElementSize;
            memory = Marshal.AllocHGlobal(Count * layoutElementSize);
            for (var i = 0; i < Count; ++i)
            {
                var ptr = memory + i * layoutElementSize;
                var element = LayoutElement.CreateManagedWrapper(ptr, ownsHandle: false);
                var spec = elements[i];
                element.HLSLSemantic = semantic.Pointer;
                element.InputIndex = spec.InputIndex;
                element.BufferSlot = spec.BufferSlot;
                element.NumComponents = spec.NumComponents;
                element.ValueType = spec.ValueType;
                element.IsNormalized = spec.IsNormalized;
                element.RelativeOffset = spec.RelativeOffset;
                element.Stride = spec.Stride;
                element.Frequency = spec.Frequency;
                element.InstanceDataStepRate = spec.InstanceDataStepRate;
            }

            First = LayoutElement.CreateManagedWrapper(memory, ownsHandle: false);
        }

        public LayoutElement First { get; }

        public int Count { get; }

        public void Dispose()
        {
            semantic.Dispose();
            if (memory != IntPtr.Zero)
                Marshal.FreeHGlobal(memory);
        }
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

        public static implicit operator TextureDesc(TextureDescOwner owner) => owner.Desc;

        public void Dispose()
        {
            if (memory != IntPtr.Zero)
                Marshal.FreeHGlobal(memory);
        }
    }

    private sealed class UploadedGeometry : IDisposable
    {
        public UploadedGeometry(IBuffer vertexBuffer, IBuffer indexBuffer, DrawIndexedAttribs drawIndexedAttribs)
        {
            VertexBuffer = vertexBuffer;
            IndexBuffer = indexBuffer;
            DrawIndexedAttribs = drawIndexedAttribs;
        }

        public IBuffer VertexBuffer { get; }

        public IBuffer IndexBuffer { get; }

        public DrawIndexedAttribs DrawIndexedAttribs { get; }

        public void Dispose()
        {
            DrawIndexedAttribs.Dispose();
        }
    }

    private sealed class UploadedTexture : IDisposable
    {
        public UploadedTexture(ITexture texture, ITextureView shaderView)
        {
            Texture = texture;
            ShaderView = shaderView;
        }

        public ITexture Texture { get; }

        public ITextureView ShaderView { get; }

        public void Dispose()
        {
            ReleaseIfNotNull(Texture);
        }
    }

    private sealed class PinnedBuffer : IDisposable
    {
        private readonly GCHandle handle;

        public PinnedBuffer(byte[] data)
        {
            handle = GCHandle.Alloc(data, GCHandleType.Pinned);
        }

        public nint Pointer => handle.AddrOfPinnedObject();

        public void Dispose()
        {
            if (handle.IsAllocated)
                handle.Free();
        }
    }
}
