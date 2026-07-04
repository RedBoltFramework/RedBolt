using System.Runtime.InteropServices;
using RedBolt.Bindings.Diligent;
using RedBolt.Bindings.Sdl;

namespace RedBolt;

public sealed class Graphics
{
    private const string PixelShaderSource = """
in vec3 v_Color;
out vec4 out_Color;

void main()
{
    out_Color = vec4(v_Color, 1.0);
}
""";

    private const string VertexShaderSource = """
layout(location = 0) in vec3 a_Position;
out vec3 v_Color;

const vec3 faceColors[6] = vec3[6](
    vec3(0.18, 0.28, 0.42),
    vec3(0.22, 0.42, 0.70),
    vec3(0.12, 0.22, 0.38),
    vec3(0.92, 0.28, 0.24),
    vec3(0.98, 0.72, 0.26),
    vec3(0.28, 0.74, 0.96)
);

void main()
{
    vec3 p = a_Position;
    float angle = 0.72;
    float c = cos(angle);
    float s = sin(angle);
    vec3 r = vec3(c * p.x + s * p.z, p.y, -s * p.x + c * p.z);
    vec2 projected = vec2(r.x * 0.82, r.y * 0.92 + r.z * 0.34);
    gl_Position = vec4(projected, 0.0, 1.0);
    v_Color = faceColors[(gl_VertexID / 6) % 6];
}
""";

    private readonly float[] clearColor = [0.03f, 0.05f, 0.09f, 1.0f];
    private readonly ITextureView?[] renderTargets = new ITextureView?[1];
    private readonly Dictionary<Model, UploadedGeometry> uploadedModels = new();

    private GCHandle clearColorHandle;
    private IDeviceContext? immediateContext;
    private IRenderDevice? device;
    private IPipelineState? pipelineState;
    private ISwapChain? swapChain;
    private IShader? pixelShader;
    private IShader? vertexShader;
    private ITextureView? renderTargetView;
    private IntPtr glContext;
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

        Width = parameters.WindowWidth;
        Height = parameters.WindowHeight;
        window = input.Window;
        glContext = SdlNativeBindings.SdlGlCreateContext(window);
        if (glContext == IntPtr.Zero)
            throw new InvalidOperationException("Generated SDL_GL_CreateContext binding returned a null context.");

        try
        {
            CheckSdlGl(SdlNativeBindings.SdlGlMakeCurrent(window, glContext), "SDL_GL_MakeCurrent");
            _ = SdlNativeBindings.SdlGlSetSwapInterval(0);

            var nativeWindowInfo = input.GetNativeWindowInfo();
            using var engineCreateInfo = new EngineGLCreateInfo
            {
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

            vertexShader = CreateShader(device, global::RedBolt.Bindings.Diligent.ShaderType.ShaderTypeVertex, "RedBoltCubeVS", VertexShaderSource);
            pixelShader = CreateShader(device, global::RedBolt.Bindings.Diligent.ShaderType.ShaderTypePixel, "RedBoltCubePS", PixelShaderSource);
            pipelineState = CreatePipelineState(device, swapChain, vertexShader, pixelShader);

            renderTargetView = swapChain.GetCurrentBackBufferRTV();
            if (renderTargetView.IsNull)
                throw new InvalidOperationException("Generated GetCurrentBackBufferRTV binding returned a null render target view.");
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

    internal void RenderViewport(Viewport? viewport)
    {
        if (!IsInitialized || viewport is null || immediateContext is null || swapChain is null || pipelineState is null || renderTargetView is null)
            return;

        immediateContext.SetRenderTargets((uint)renderTargets.Length, renderTargets, null!, ResourceStateTransitionMode.ResourceStateTransitionModeTransition);
        immediateContext.ClearRenderTarget(renderTargetView, clearColorHandle.AddrOfPinnedObject(), ResourceStateTransitionMode.ResourceStateTransitionModeTransition);
        immediateContext.SetPipelineState(pipelineState);

        foreach (var staticModel in viewport.Scene.StaticModels)
        {
            if (staticModel.Model is not { } model || staticModel.Material is null)
                continue;

            var geometry = model.GetGeometry(0, 0);
            if (geometry is null || geometry.GetVertexBuffer(0) is null || geometry.GetIndexBuffer() is null)
                continue;

            var uploaded = GetOrCreateUploadedGeometry(model, geometry, device!, immediateContext);
            if (uploaded is null)
                continue;

            nuint vertexOffset = 0;
            IBuffer?[] vertexBuffers = [uploaded.VertexBuffer];
            immediateContext.SetVertexBuffers(0, 1, vertexBuffers, in vertexOffset,
                ResourceStateTransitionMode.ResourceStateTransitionModeTransition,
                SetVertexBuffersFlags.SetVertexBuffersFlagReset);
            immediateContext.SetIndexBuffer(uploaded.IndexBuffer, 0,
                ResourceStateTransitionMode.ResourceStateTransitionModeTransition);
            immediateContext.DrawIndexed(uploaded.DrawIndexedAttribs);
        }

        swapChain.Present(0);
        immediateContext.SetRenderTargets(0, global::System.ReadOnlySpan<ITextureView?>.Empty, null!, ResourceStateTransitionMode.ResourceStateTransitionModeNone);
    }

    public void Shutdown()
    {
        Width = 0;
        Height = 0;
        renderTargets[0] = null;
        renderTargetView = null;

        foreach (var uploaded in uploadedModels.Values)
            uploaded.Dispose();
        uploadedModels.Clear();

        ReleaseIfNotNull(pipelineState);
        pipelineState = null;
        ReleaseIfNotNull(pixelShader);
        pixelShader = null;
        ReleaseIfNotNull(vertexShader);
        vertexShader = null;
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

    private static IShader CreateShader(IRenderDevice device, global::RedBolt.Bindings.Diligent.ShaderType shaderType, string name, string source)
    {
        using var shaderName = new PinnedUtf8(name);
        using var entryPoint = new PinnedUtf8("main");
        using var shaderSource = new PinnedUtf8(source);
        using var shaderDesc = new ShaderDesc
        {
            Name = shaderName.Pointer,
            ShaderType = shaderType
        };
        using var shaderCreateInfo = new ShaderCreateInfo
        {
            Desc = shaderDesc,
            EntryPoint = entryPoint.Pointer,
            Source = shaderSource.Pointer,
            SourceLanguage = global::RedBolt.Bindings.Diligent.ShaderSourceLanguage.ShaderSourceLanguageGlsl
        };

        device.CreateShader(shaderCreateInfo, out var shader, out var compilerOutput);
        ReleaseIfNotNull(compilerOutput);

        if (shader.IsNull)
            throw new InvalidOperationException($"Generated CreateShader returned a null {shaderType} shader.");

        return shader;
    }

    private static IPipelineState CreatePipelineState(IRenderDevice device, ISwapChain swapChain, IShader vertexShader, IShader pixelShader)
    {
        using var pipelineName = new PinnedUtf8("RedBoltFirstCubePSO");
        using var psoDesc = new PipelineStateDesc
        {
            Name = pipelineName.Pointer,
            PipelineType = PipelineType.PipelineTypeGraphics
        };
        using var psoCreateInfo = new GraphicsPipelineStateCreateInfo
        {
            PSODesc = psoDesc,
            PVS = vertexShader,
            PPS = pixelShader,
            GraphicsPipelineNumRenderTargets = 1,
            GraphicsPipelineInputLayoutLayoutElements = new LayoutElement(0, 0, 3, global::RedBolt.Bindings.Diligent.ValueType.VtFloat32),
            GraphicsPipelineInputLayoutNumElements = 1,
            GraphicsPipelinePrimitiveTopology = PrimitiveTopology.PrimitiveTopologyTriangleList,
            GraphicsPipelineRasterizerDescCullMode = RedBolt.Bindings.Diligent.CullMode.CullModeNone,
            GraphicsPipelineDepthStencilDescDepthEnable = false,
            GraphicsPipelineDepthStencilDescDepthWriteEnable = false
        };

        using var desc = swapChain.GetDesc();
        psoCreateInfo.GraphicsPipelineRTVFormats[0] = desc.ColorBufferFormat;

        device.CreateGraphicsPipelineState(psoCreateInfo, out var pipelineState);
        if (pipelineState.IsNull)
            throw new InvalidOperationException("Generated CreateGraphicsPipelineState returned a null PSO.");

        return pipelineState;
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

    private UploadedGeometry? GetOrCreateUploadedGeometry(Model model, Geometry geometry, IRenderDevice device, IDeviceContext immediateContext)
    {
        if (uploadedModels.TryGetValue(model, out var uploaded))
            return uploaded;

        var vertexBuffer = geometry.GetVertexBuffer(0);
        var indexBuffer = geometry.GetIndexBuffer();
        if (vertexBuffer is null || indexBuffer is null)
            return null;

        var vertexData = vertexBuffer.GetData().ToArray();
        var indexData = indexBuffer.GetUnpackedData().ToArray();
        var gpuVertexBuffer = CreateBuffer(device, immediateContext, "RedBoltModelVB", vertexData,
            (ulong)vertexData.Length, BindFlags.BindVertexBuffer, vertexBuffer.GetVertexSize());
        var gpuIndexBuffer = CreateIndexBuffer(device, immediateContext, indexBuffer, indexData);
        var drawIndexedAttribs = new DrawIndexedAttribs((uint)indexData.Length,
            indexBuffer.LargeIndices ? global::RedBolt.Bindings.Diligent.ValueType.VtUint32 : global::RedBolt.Bindings.Diligent.ValueType.VtUint16,
            DrawFlags.DrawFlagVerifyAll);

        uploaded = new UploadedGeometry(gpuVertexBuffer, gpuIndexBuffer, drawIndexedAttribs);
        uploadedModels[model] = uploaded;
        return uploaded;
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
            ReleaseIfNotNull(IndexBuffer);
            ReleaseIfNotNull(VertexBuffer);
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
