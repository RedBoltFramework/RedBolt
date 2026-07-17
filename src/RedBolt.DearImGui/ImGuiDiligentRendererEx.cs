using System.Runtime.CompilerServices;
using RedBolt.Bindings.Diligent;
using RedBolt.Bindings.DearImGui;

namespace RedBolt;

public sealed class ImGuiDiligentRendererEx : IDisposable
{
    // Default sizes are enough for default debug HUD.
    private const uint InitialVertexBufferSize = 2500;
    private const uint InitialIndexBufferSize = 5000;
    private const uint ImDrawVertSize = 20; // sizeof(ImDrawVert): vec2 + vec2 + u32

    private readonly Context _context;
    private readonly nint _nativeRenderer;
    private readonly PipelineState? _primaryPipelineState;
    private readonly PipelineState? _secondaryPipelineState;
    private bool _disposed;

    public ImGuiDiligentRendererEx(Context context)
    {
        this._context = context ?? throw new ArgumentNullException(nameof(context));

        var graphics = context.GetSubsystem<Graphics>()
            ?? throw new InvalidOperationException("Graphics subsystem not available.");

        var nativeDevice = graphics.GetNativeRenderDevice()
            ?? throw new InvalidOperationException("Native render device not available.");

        var backBufferFmt = graphics.GetSwapChainColorFormat();
        var depthBufferFmt = graphics.GetSwapChainDepthFormat();

        // Match C++ ImGuiDiligentRendererEx ctor sizes (initialVertexBufferSize / initialIndexBufferSize).
        _nativeRenderer = ImGui.ImGuiBackendCreateRenderer(
            nativeDevice.Handle, (uint)backBufferFmt, (uint)depthBufferFmt,
            InitialVertexBufferSize, InitialIndexBufferSize);

        if (_nativeRenderer == 0)
            throw new InvalidOperationException("Failed to create ImGui Diligent renderer.");

        var renderDevice = context.GetSubsystem<RenderDevice>()
            ?? throw new InvalidOperationException("RenderDevice subsystem not available.");

        var colorBufferFormat = MapPipelineTextureFormat(backBufferFmt);
        var depthBufferFormat = MapPipelineTextureFormat(depthBufferFmt);
        var multiSample = 1u;
        var swapChain = graphics.GetNativeSwapChain();
        if (swapChain is not null && !swapChain.IsNull)
        {
            using var rtv = swapChain.GetCurrentBackBufferRTV();
            if (rtv is not null && !rtv.IsNull)
            {
                using var backBuffer = rtv.GetTexture();
                if (backBuffer is not null && !backBuffer.IsNull)
                {
                    using var desc = backBuffer.GetDesc();
                    multiSample = Math.Max(desc.SampleCount, 1u);
                }
            }
        }

        // C++: primaryPipelineState_ with swap-chain color+depth+MSAA; secondary without depth.
        _primaryPipelineState = CreateRenderPipeline(
            renderDevice, graphics, colorBufferFormat, depthBufferFormat, multiSample);
        _secondaryPipelineState = CreateRenderPipeline(
            renderDevice, graphics, colorBufferFormat, TextureFormat.Unknown, 1);
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        if (_nativeRenderer != 0)
            ImGui.ImGuiBackendDestroyRenderer(_nativeRenderer);
    }

    public void NewFrame(uint width, uint height)
    {
        if (_disposed || _nativeRenderer == 0)
            return;

        ImGui.ImGuiBackendRendererNewFrame(_nativeRenderer, width, height, 1 /* SURFACE_TRANSFORM_IDENTITY */);
    }

    public void RenderDrawData(ImDrawData drawData)
    {
        if (_disposed || _nativeRenderer == 0 || drawData.IsNull)
            return;

        var renderDevice = _context.GetSubsystem<RenderDevice>();
        var renderContext = renderDevice?.GetRenderContext();
        using var renderScope = new RenderScope(renderContext, "ImGUI: Render main viewport");

        RenderDrawDataWith(drawData, _primaryPipelineState);
    }

    public void RenderSecondaryWindows()
    {
        // Multi-viewport secondary windows are not ported yet (C++ RenderSecondaryWindows).
    }

    private void RenderDrawDataWith(ImDrawData drawData, PipelineState? pipelineState)
    {
        var graphics = _context.GetSubsystem<Graphics>();
        var deviceContext = graphics?.GetImmediateContext();
        if (deviceContext is null || deviceContext.IsNull)
            return;

        if (pipelineState is null)
        {
            ImGui.ImGuiBackendRendererRenderDrawData(
                _nativeRenderer, deviceContext.Handle, drawData.Handle,
                0, 0, 0, 0);
            return;
        }

        // C++ ImGuiDiligentRendererEx::RenderDrawDataWith
        pipelineState.Restore();
        if (!pipelineState.IsValid)
            return;

        var pso = pipelineState.GetHandle();
        var srb = pipelineState.GetShaderResourceBinding();
        if (pso is null || pso.IsNull || srb is null || srb.IsNull)
            return;

        using var textureName = new CString("sTexture");
        using var cameraName = new CString("Camera");
        IShaderResourceVariable? textureVar;
        IShaderResourceVariable? constantsVar;
        unsafe
        {
            ref var textureNameRef = ref Unsafe.AsRef<sbyte>((void*)textureName.Pointer);
            ref var cameraNameRef = ref Unsafe.AsRef<sbyte>((void*)cameraName.Pointer);
            textureVar = srb.GetVariableByName(
                global::RedBolt.Bindings.Diligent.ShaderType.ShaderTypePixel, in textureNameRef);
            constantsVar = srb.GetVariableByName(
                global::RedBolt.Bindings.Diligent.ShaderType.ShaderTypeVertex, in cameraNameRef);
        }

        if (textureVar is null || textureVar.IsNull || constantsVar is null || constantsVar.IsNull)
            return;

        ImGui.ImGuiBackendRendererRenderDrawData(
            _nativeRenderer, deviceContext.Handle, drawData.Handle,
            pso.Handle, srb.Handle, textureVar.Handle, constantsVar.Handle);
    }

    /// <summary>
    /// C++ IsSRGBTextureFormat — RGBA8/BGRA8 sRGB Diligent formats.
    /// </summary>
    private static bool IsSRGBTextureFormat(TextureFormat format)
        => format is TextureFormat.RGBA8Srgb or TextureFormat.BGRA8Srgb;

    private static PipelineState? CreateRenderPipeline(
        RenderDevice renderDevice,
        Graphics graphics,
        TextureFormat colorBufferFormat,
        TextureFormat depthBufferFormat,
        uint multiSample)
    {
        var pipelineStateCache = renderDevice.GetSubsystem<PipelineStateCache>();
        if (pipelineStateCache is null)
            return null;

        // C++ CreateRenderPipeline in ImGuiDiligentRendererEx.cpp
        var desc = new GraphicsPipelineStateDesc
        {
            DebugName = $"ImGUI Render Pipeline (Color: {colorBufferFormat}, Depth: {depthBufferFormat})",
            ColorWriteEnabled = true,
            PrimitiveType = PrimitiveType.TriangleList,
            DepthCompareFunction = CompareMode.Always,
            DepthWriteEnabled = false,
            BlendMode = BlendMode.Alpha,
            ScissorTestEnabled = true,
            CullMode = CullMode.None,
        };

        desc.Output.SetRenderTargetFormat(colorBufferFormat);
        desc.Output.DepthStencilFormat = depthBufferFormat;
        desc.Output.MultiSample = multiSample;

        // Match C++ input layout for ImDrawVert: pos(vec2), uv(vec2), col(ubyte4norm).
        desc.InputLayout.SetElements(
            new InputLayoutElementDesc(0, ImDrawVertSize, 0, 0, VertexElementType.Vector2, VertexElementSemantic.Position, 0),
            new InputLayoutElementDesc(0, ImDrawVertSize, 8, 0, VertexElementType.Vector2, VertexElementSemantic.TexCoord, 0),
            new InputLayoutElementDesc(0, ImDrawVertSize, 16, 0, VertexElementType.UByte4Norm, VertexElementSemantic.Color, 0));

        var shaderDefines = string.Empty;
        if (IsSRGBTextureFormat(colorBufferFormat))
            shaderDefines = "URHO3D_LINEAR_OUTPUT ";

        desc.VertexShader = graphics.GetShader(ShaderType.VS, "v2/X_ImGui", shaderDefines);
        desc.PixelShader = graphics.GetShader(ShaderType.PS, "v2/X_ImGui", shaderDefines);

        desc.Samplers.Add("Texture", SamplerStateDesc.Bilinear(TextureAddressMode.Wrap));

        if (!desc.IsInitialized())
            return null;

        return pipelineStateCache.GetGraphicsPipelineState(desc);
    }

    /// <summary>
    /// Map Diligent swap-chain formats to RedBolt pipeline TextureFormat, preserving sRGB.
    /// Mirrors Graphics.UnmapPipelineTextureFormat for formats needed by ImGui.
    /// </summary>
    private static TextureFormat MapPipelineTextureFormat(global::RedBolt.Bindings.Diligent.TextureFormat format) => format switch
    {
        global::RedBolt.Bindings.Diligent.TextureFormat.TexFormatBgra8Unorm => TextureFormat.BGRA8,
        global::RedBolt.Bindings.Diligent.TextureFormat.TexFormatBgra8UnormSrgb => TextureFormat.BGRA8Srgb,
        global::RedBolt.Bindings.Diligent.TextureFormat.TexFormatRgba8Unorm => TextureFormat.RGBA8,
        global::RedBolt.Bindings.Diligent.TextureFormat.TexFormatRgba8UnormSrgb => TextureFormat.RGBA8Srgb,
        global::RedBolt.Bindings.Diligent.TextureFormat.TexFormatD16Unorm => TextureFormat.Depth16,
        global::RedBolt.Bindings.Diligent.TextureFormat.TexFormatD32Float => TextureFormat.Depth32,
        global::RedBolt.Bindings.Diligent.TextureFormat.TexFormatD24UnormS8Uint => TextureFormat.Depth24Stencil8,
        global::RedBolt.Bindings.Diligent.TextureFormat.TexFormatD32FloatS8x24Uint => TextureFormat.Depth32Stencil8,
        global::RedBolt.Bindings.Diligent.TextureFormat.TexFormatUnknown => TextureFormat.Unknown,
        _ => TextureFormat.Unknown
    };
}
