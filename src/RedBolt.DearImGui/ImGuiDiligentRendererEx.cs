using System.Runtime.CompilerServices;
using RedBolt.Bindings.DearImGui;
using RedBolt.Bindings.Diligent;

namespace RedBolt;

public class ImGuiDiligentRendererEx : IDisposable
{
    // Default sizes are enough for default debug HUD.
    private const int InitialVertexBufferSize = 2500;
    private const int InitialIndexBufferSize = 5000;
    private const int ImDrawVertSize = 20;

    public ImGuiDiligentRendererEx(RenderDevice renderDevice)
    {
        var swapChain = renderDevice.GetSwapChain()!;
        using var swapChainDesc = swapChain.GetDesc();
        _nativeRenderer = ImGui.ImGuiBackendCreateRenderer(
            renderDevice.GetRenderDevice()!.Handle,
            (uint)swapChainDesc.ColorBufferFormat,
            (uint)swapChainDesc.DepthBufferFormat,
            InitialVertexBufferSize,
            InitialIndexBufferSize);
        _renderDevice = renderDevice;

        try
        {
            // TODO: port ImGui platform renderer callback installation when SharpCpp supports function pointer fields.
            // ImGuiIO& IO = ImGui::GetIO();
            // ImGuiPlatformIO& platformIO = ImGui::GetPlatformIO();
            // createPlatformWindow_ = platformIO.Platform_CreateWindow;
            // IO.BackendFlags |= ImGuiBackendFlags_RendererHasViewports;
            // IO.BackendRendererUserData = this;
            // platformIO.Platform_CreateWindow = [](ImGuiViewport* viewport) { GetBackendData()->CreatePlatformWindow(viewport); };
            // platformIO.Renderer_CreateWindow = [](ImGuiViewport* viewport) { GetBackendData()->CreateRendererWindow(viewport); };
            // platformIO.Renderer_DestroyWindow = [](ImGuiViewport* viewport) { GetBackendData()->DestroyRendererWindow(viewport); };
            // platformIO.Renderer_SetWindowSize = [](ImGuiViewport* viewport, ImVec2 size) { GetBackendData()->SetWindowSize(viewport, size); };
            // platformIO.Renderer_RenderWindow = [](ImGuiViewport* viewport, void* renderArg) { GetBackendData()->RenderWindow(viewport, renderArg); };
            // platformIO.Renderer_SwapBuffers = [](ImGuiViewport* viewport, void* renderArg) { GetBackendData()->SwapBuffers(viewport, renderArg); };

            using var renderTarget = swapChain.GetCurrentBackBufferRTV()!;
            using var backBuffer = renderTarget.GetTexture();
            using var backBufferDesc = backBuffer.GetDesc();
            var multiSample = checked((int)backBufferDesc.SampleCount);
            var colorBufferFormat = MapPipelineTextureFormat(swapChainDesc.ColorBufferFormat);
            var depthBufferFormat = MapPipelineTextureFormat(swapChainDesc.DepthBufferFormat);
            _primaryPipelineState = CreateRenderPipeline(
                renderDevice, colorBufferFormat, depthBufferFormat, multiSample);
            _secondaryPipelineState =
                CreateRenderPipeline(renderDevice, colorBufferFormat, TextureFormat.Unknown, 1);
        }
        catch
        {
            ImGui.ImGuiBackendDestroyRenderer(_nativeRenderer);
            _nativeRenderer = 0;
            throw;
        }
    }

    public void Dispose()
    {
        // TODO: port viewport destruction and ImGui platform renderer callback restoration when SharpCpp supports
        // ImGuiPlatformIO function pointer fields.
        // const auto viewports = ea::move(viewports_);
        // for (ImGuiViewport* viewport : viewports)
        //     DestroyRendererWindow(viewport);
        // ImGuiIO& IO = ImGui::GetIO();
        // ImGuiPlatformIO& platformIO = ImGui::GetPlatformIO();
        // IO.BackendRendererUserData = nullptr;
        // platformIO.Renderer_CreateWindow = nullptr;
        // platformIO.Renderer_DestroyWindow = nullptr;
        // platformIO.Renderer_SetWindowSize = nullptr;
        // platformIO.Renderer_RenderWindow = nullptr;
        // platformIO.Renderer_SwapBuffers = nullptr;
        // platformIO.Platform_CreateWindow = createPlatformWindow_;
        ImGui.ImGuiBackendDestroyRenderer(_nativeRenderer);
    }

    public void NewFrame()
    {
        using var swapChainDesc = _renderDevice.GetSwapChain()!.GetDesc();
        var width = checked((int)swapChainDesc.Width);
        var height = checked((int)swapChainDesc.Height);
        ImGui.ImGuiBackendRendererNewFrame(
            _nativeRenderer,
            checked((uint)width),
            checked((uint)height),
            (uint)swapChainDesc.PreTransform);
    }

    public void RenderDrawData(ImDrawData drawData)
    {
        using var renderScope = new RenderScope(_renderDevice.GetRenderContext(), "ImGUI: Render main viewport");

        RenderDrawDataWith(drawData, _primaryPipelineState);
    }

    public void RenderSecondaryWindows()
    {
        // TODO: port the complete C++ body with the secondary-window callbacks when SharpCpp supports
        // ImGuiPlatformIO function pointer fields.
        // const bool isOpenGL = renderDevice_->GetBackend() == RenderBackend::OpenGL;
        // isCachedStateInvalid_ = false;
        // SDL_Window* backupCurrentWindow = isOpenGL ? SDL_GL_GetCurrentWindow() : nullptr;
        // SDL_GLContext backupCurrentContext = isOpenGL ? SDL_GL_GetCurrentContext() : nullptr;
        // ui::UpdatePlatformWindows();
        // ui::RenderPlatformWindowsDefault();
        // if (isCachedStateInvalid_ && isOpenGL)
        // {
        //     SDL_GL_MakeCurrent(backupCurrentWindow, backupCurrentContext);
        //     auto deviceContextGL = Diligent::ClassPtrCast<Diligent::IDeviceContextGL>(renderDevice_->GetImmediateContext());
        //     deviceContextGL->InvalidateState();
        //     deviceContextGL->UpdateCurrentGLContext();
        // }
    }

    private void RenderDrawDataWith(ImDrawData drawData, PipelineState pipelineState)
    {
        pipelineState.Restore();
        if (!pipelineState.IsValid)
            return;

        var srb = pipelineState.GetShaderResourceBinding()!;
        IShaderResourceVariable? textureVar;
        IShaderResourceVariable? constantsVar;
        unsafe
        {
            fixed (byte* textureName = "sTexture\0"u8)
            fixed (byte* cameraName = "Camera\0"u8)
            {
                ref var textureNameRef = ref Unsafe.As<byte, sbyte>(ref *textureName);
                ref var cameraNameRef = ref Unsafe.As<byte, sbyte>(ref *cameraName);
                textureVar = srb.GetVariableByName(
                    global::RedBolt.Bindings.Diligent.ShaderType.ShaderTypePixel, in textureNameRef);
                constantsVar = srb.GetVariableByName(
                    global::RedBolt.Bindings.Diligent.ShaderType.ShaderTypeVertex, in cameraNameRef);
            }
        }

        ImGui.ImGuiBackendRendererRenderDrawData(
            _nativeRenderer,
            _renderDevice.GetImmediateContext()!.Handle,
            drawData.Handle,
            pipelineState.GetHandle()!.Handle,
            srb.Handle,
            textureVar!.Handle,
            constantsVar!.Handle);
    }

    // TODO: port ImGuiDiligentRendererEx* GetBackendData() with the managed renderer callback handle.
    // TODO: port ViewportRendererData and ViewportRendererData* GetViewportData(ImGuiViewport* viewport)
    // with deterministic ownership of RendererUserData.

    private static bool IsSRGBTextureFormat(TextureFormat format)
    {
        return format is TextureFormat.RGBA8Srgb or TextureFormat.BGRA8Srgb;
    }

    private static PipelineState CreateRenderPipeline(
        RenderDevice renderDevice, TextureFormat colorBufferFormat, TextureFormat depthBufferFormat, int multiSample)
    {
        var pipelineStateCache = renderDevice.GetSubsystem<PipelineStateCache>()!;
        var graphics = renderDevice.GetSubsystem<Graphics>()!;

        var desc = new GraphicsPipelineStateDesc
        {
            DebugName = $"ImGUI Render Pipeline (Color: {colorBufferFormat}, Depth: {depthBufferFormat})"
        };

        desc.Output.SetRenderTargetFormat(colorBufferFormat);
        desc.Output.DepthStencilFormat = depthBufferFormat;
        desc.Output.MultiSample = checked((uint)multiSample);

        desc.InputLayout.SetElements(
            new InputLayoutElementDesc(0, ImDrawVertSize, 0, 0, VertexElementType.Vector2, VertexElementSemantic.Position, 0),
            new InputLayoutElementDesc(0, ImDrawVertSize, 8, 0, VertexElementType.Vector2, VertexElementSemantic.TexCoord, 0),
            new InputLayoutElementDesc(0, ImDrawVertSize, 16, 0, VertexElementType.UByte4Norm, VertexElementSemantic.Color, 0));
        desc.ColorWriteEnabled = true;

        var shaderDefines = string.Empty;
        if (IsSRGBTextureFormat(colorBufferFormat))
            shaderDefines += "URHO3D_LINEAR_OUTPUT ";

        desc.VertexShader = graphics.GetShader(ShaderType.VS, "v2/X_ImGui", shaderDefines);
        desc.PixelShader = graphics.GetShader(ShaderType.PS, "v2/X_ImGui", shaderDefines);

        desc.PrimitiveType = PrimitiveType.TriangleList;
        desc.DepthCompareFunction = CompareMode.Always;
        desc.DepthWriteEnabled = false;
        desc.BlendMode = BlendMode.Alpha;
        desc.ScissorTestEnabled = true;

        desc.Samplers.Add("Texture", SamplerStateDesc.Bilinear(TextureAddressMode.Wrap));

        return pipelineStateCache.GetGraphicsPipelineState(desc)!;
    }

    // TODO: port CreatePlatformWindow, CreateRendererWindow, DestroyRendererWindow, SetWindowSize, RenderWindow,
    // SwapBuffers, and CreateSwapChainForViewport when SharpCpp supports ImGuiPlatformIO function pointer fields.
    // TODO: port the implicit C++ copy constructor as ICloneable.Clone() when native ImGuiDiligentRenderer
    // base copy construction is available through generated bindings. There are no native copy call sites.

    private static TextureFormat MapPipelineTextureFormat(global::RedBolt.Bindings.Diligent.TextureFormat format)
    {
        return format switch
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

    private nint _nativeRenderer;
    private readonly RenderDevice _renderDevice;
    // TODO: port void (*createPlatformWindow_)(ImGuiViewport* viewport) when SharpCpp supports function pointer fields.
    // TODO: port bool isCachedStateInvalid_{} with the secondary-window render path.
    private readonly PipelineState _primaryPipelineState;
    private readonly PipelineState _secondaryPipelineState;
    // TODO: port ea::vector<ImGuiViewport*> viewports_ with the secondary-window callback path.
}
