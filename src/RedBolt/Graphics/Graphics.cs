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
out vec3 v_Color;

const vec3 corners[8] = vec3[8](
    vec3(-0.65, -0.65, -0.65),
    vec3(-0.65,  0.65, -0.65),
    vec3( 0.65,  0.65, -0.65),
    vec3( 0.65, -0.65, -0.65),
    vec3(-0.65, -0.65,  0.65),
    vec3(-0.65,  0.65,  0.65),
    vec3( 0.65,  0.65,  0.65),
    vec3( 0.65, -0.65,  0.65)
);

const int indices[36] = int[36](
    0, 1, 2, 0, 2, 3,
    0, 4, 5, 0, 5, 1,
    0, 3, 7, 0, 7, 4,
    3, 2, 6, 3, 6, 7,
    4, 7, 6, 4, 6, 5,
    1, 5, 6, 1, 6, 2
);

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
    vec3 p = corners[indices[gl_VertexID]];
    float angle = 0.72;
    float c = cos(angle);
    float s = sin(angle);
    vec3 r = vec3(c * p.x + s * p.z, p.y, -s * p.x + c * p.z);
    vec2 projected = vec2(r.x * 0.82, r.y * 0.92 + r.z * 0.34);
    gl_Position = vec4(projected, 0.0, 1.0);
    v_Color = faceColors[gl_VertexID / 6];
}
""";

    private readonly float[] clearColor = [0.03f, 0.05f, 0.09f, 1.0f];
    private readonly ITextureView?[] renderTargets = new ITextureView?[1];

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
    private DrawAttribs? cubeDrawAttribs;
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

            cubeDrawAttribs = new DrawAttribs(36u, DrawFlags.DrawFlagVerifyAll);
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
        if (!IsInitialized || viewport is null || immediateContext is null || swapChain is null || pipelineState is null || cubeDrawAttribs is null || renderTargetView is null)
            return;

        immediateContext.SetRenderTargets((uint)renderTargets.Length, renderTargets, null!, ResourceStateTransitionMode.ResourceStateTransitionModeTransition);
        immediateContext.ClearRenderTarget(renderTargetView, clearColorHandle.AddrOfPinnedObject(), ResourceStateTransitionMode.ResourceStateTransitionModeTransition);
        immediateContext.SetPipelineState(pipelineState);

        foreach (var staticModel in viewport.Scene.StaticModels)
        {
            if (staticModel.Model is { } model && staticModel.Material is not null && model.VertexCount == 36u)
                immediateContext.Draw(cubeDrawAttribs);
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

        cubeDrawAttribs?.Dispose();
        cubeDrawAttribs = null;

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
}
