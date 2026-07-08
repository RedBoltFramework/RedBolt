using RedBolt.Bindings.DearImGui;
using RedBolt.Bindings.Sdl;

namespace RedBolt;

public sealed class DearImGui : IDisposable
{
    private readonly Context context;
    private nint imContext;
    private ImDrawData? pendingDrawData;
    private ImGuiDiligentRendererEx? renderer;
    private ContextEventSubscription? inputEndSubscription;
    private ContextEventSubscription? endRenderingSubscription;
    private bool isFrameActive;
    private bool isInitialized;

    public DearImGui(Context context, uint flags = 0)
    {
        this.context = context ?? throw new ArgumentNullException(nameof(context));
        Initialize(flags);

        if (isInitialized)
            SubscribeToEvents();
    }

    public bool IsInitialized => isInitialized;

    public void Dispose()
    {
        Shutdown();
    }

    private void Initialize(uint flags)
    {
        imContext = ImGui.ImGuiBackendCreateContext(0);
        if (imContext == 0)
        {
            Console.Error.WriteLine("DearImGui: failed to create ImGui context.");
            return;
        }

        SetCurrentContext();

        var io = ImGui.GetIO();
        io.ConfigFlags = (ImGuiConfigFlags)flags;

        var renderDevice = context.GetSubsystem<RenderDevice>();
        if (renderDevice is not null)
        {
            var size = renderDevice.GetSwapChainSize();
            io.DisplaySizeX = size.X;
            io.DisplaySizeY = size.Y;
        }

        PlatformInitialize();

        isInitialized = true;
    }

    public void Shutdown()
    {
        if (!isInitialized)
        {
            UnsubscribeFromEvents();
            return;
        }

        SetCurrentContext();
        if (isFrameActive)
        {
            ImGui.EndFrame();
            pendingDrawData = null;
            isFrameActive = false;
        }

        PlatformShutdown();

        if (imContext != 0)
        {
            ImGui.ImGuiBackendDestroyContext(imContext);
            imContext = 0;
        }

        isInitialized = false;
    }

    public void EndFrame()
    {
        SetCurrentContext();
        if (!isInitialized || !isFrameActive)
            return;

        ImGui.Render();

        var drawData = ImGui.GetDrawData();
        pendingDrawData = drawData.IsNull ? null : drawData;
        isFrameActive = false;
    }

    public void Render()
    {
        SetCurrentContext();
        if (!isInitialized || pendingDrawData is null || pendingDrawData.IsNull)
            return;

        var renderDevice = context.GetSubsystem<RenderDevice>();
        var renderContext = renderDevice?.GetRenderContext();
        renderContext?.SetSwapChainRenderTargets();
        renderContext?.SetFullViewport();
        renderer?.RenderDrawData(pendingDrawData);
        renderer?.RenderSecondaryWindows();
        pendingDrawData = null;
    }

    public bool ProcessEvent(nint sdlEvent)
    {
        SetCurrentContext();
        if (!isInitialized)
            return false;

        return ImGui.ImGuiBackendProcessEvent(sdlEvent);
    }

    public bool WantCaptureKeyboard
    {
        get
        {
            if (!isInitialized)
                return false;
            return ImGui.GetIO().WantCaptureKeyboard;
        }
    }

    public bool WantCaptureMouse
    {
        get
        {
            if (!isInitialized)
                return false;
            return ImGui.GetIO().WantCaptureMouse;
        }
    }

    public void BeginFrame()
    {
        SetCurrentContext();
        if (!isInitialized)
            return;

        if (isFrameActive)
        {
            ImGui.EndFrame();
            ImGui.UpdatePlatformWindows();
            pendingDrawData = null;
            isFrameActive = false;
        }

        var io = ImGui.GetIO();
        io.DeltaTime = context.GetSubsystem<EngineTime>()?.TimeStep ?? 0.0f;

        var renderDevice = context.GetSubsystem<RenderDevice>();
        if (renderDevice == null)
            return;

        var size = renderDevice.GetSwapChainSize();
        if (size.X <= 0 || size.Y <= 0)
            return;

        io.DisplaySizeX = size.X;
        io.DisplaySizeY = size.Y;
        renderer?.NewFrame((uint)size.X, (uint)size.Y);

        ImGui.ImGuiImplSDL2NewFrame();
        ImGui.NewFrame();
        isFrameActive = true;
    }

    private void OnInputEnd()
    {
        SetCurrentContext();
        if (!isInitialized)
            return;

        if (isFrameActive)
        {
            ImGui.EndFrame();
            ImGui.UpdatePlatformWindows();
            pendingDrawData = null;
            isFrameActive = false;
        }

        var renderDevice = context.GetSubsystem<RenderDevice>();
        if (renderDevice is null)
            return;

        BeginFrame();
    }

    private void OnEndRendering()
    {
        SetCurrentContext();
        if (!isInitialized || !isFrameActive)
            return;

        context.SendEvent<DearImGuiEvents.EndRenderingDearImGuiEvent>();
        EndFrame();
        if (!isInitialized || pendingDrawData is null)
            return;

        Render();
    }

    private void PlatformInitialize()
    {
        SetCurrentContext();
        var renderDevice = context.GetSubsystem<RenderDevice>();
        if (renderDevice == null)
            return;

        var backend = renderDevice.GetBackend();
        var input = context.GetSubsystem<Input>();
        if (input == null || !input.IsInitialized)
            return;

        var sdlWindow = input.GetWindowHandle();

        switch (backend)
        {
            case RenderBackend.OpenGL:
                ImGui.ImGuiBackendInitForOpenGL(sdlWindow, SdlNativeBindings.SdlGlGetCurrentContext());
                break;
            case RenderBackend.Vulkan:
                ImGui.ImGuiBackendInitForOther(sdlWindow);
                break;
            case RenderBackend.D3D11:
            case RenderBackend.D3D12:
                ImGui.ImGuiBackendInitForD3D(sdlWindow);
                break;
        }

        renderer = new ImGuiDiligentRendererEx(context);
        var size = renderDevice.GetSwapChainSize();
        if (size.X > 0 && size.Y > 0)
            renderer.NewFrame((uint)size.X, (uint)size.Y);
    }

    private void PlatformShutdown()
    {
        SetCurrentContext();
        renderer?.Dispose();
        renderer = null;
        UnsubscribeFromEvents();

        if (imContext != 0)
            ImGui.ImGuiImplSDL2Shutdown();
    }

    private void SubscribeToEvents()
    {
        inputEndSubscription = context.SubscribeToEvent<InputEvents.InputEndEvent>((ref InputEvents.InputEndEvent _) => OnInputEnd());
        endRenderingSubscription = context.SubscribeToEvent<GraphicsEvents.EndRenderingEvent>((ref GraphicsEvents.EndRenderingEvent _) => OnEndRendering());
    }

    private void UnsubscribeFromEvents()
    {
        inputEndSubscription?.Dispose();
        endRenderingSubscription?.Dispose();
        inputEndSubscription = null;
        endRenderingSubscription = null;
    }

    private void SetCurrentContext()
    {
        if (imContext != 0)
            ImGui.ImGuiBackendSetCurrentContext(imContext);
    }
}
