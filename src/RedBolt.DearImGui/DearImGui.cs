using RedBolt.Bindings.DearImGui;
using RedBolt.Bindings.Sdl;

namespace RedBolt;

public sealed class DearImGui : IDisposable, IDearImGuiInputSink
{
    private readonly Context _context;
    private nint _imContext;
    private ImGuiIO? _io;
    private ImDrawData? _pendingDrawData;
    private ImGuiDiligentRendererEx? _renderer;
    private ContextEventSubscription? _inputEndSubscription;
    private ContextEventSubscription? _endRenderingSubscription;
    private bool _isFrameActive;
    private bool _isInitialized;

    public DearImGui(Context context, uint flags = 0)
    {
        this._context = context ?? throw new ArgumentNullException(nameof(context));
        Initialize(flags);

        if (_isInitialized)
            SubscribeToEvents();
    }

    public bool IsInitialized => _isInitialized;

    public void Dispose()
    {
        Shutdown();
    }

    private void Initialize(uint flags)
    {
        _imContext = ImGui.ImGuiBackendCreateContext(0);
        if (_imContext == 0)
        {
            Console.Error.WriteLine("DearImGui: failed to create ImGui context.");
            return;
        }

        SetCurrentContext();

        _io = ImGui.GetIO();
        _io.ConfigFlags = (ImGuiConfigFlags)flags;

        var renderDevice = _context.GetSubsystem<RenderDevice>();
        if (renderDevice is not null)
        {
            var size = renderDevice.GetSwapChainSize();
            _io.DisplaySizeX = size.X;
            _io.DisplaySizeY = size.Y;
        }

        PlatformInitialize();

        _isInitialized = true;
    }

    public void Shutdown()
    {
        if (!_isInitialized)
        {
            UnsubscribeFromEvents();
            return;
        }

        SetCurrentContext();
        if (_isFrameActive)
        {
            ImGui.EndFrame();
            _pendingDrawData = null;
            _isFrameActive = false;
        }

        PlatformShutdown();

        if (_imContext != 0)
        {
            ImGui.ImGuiBackendDestroyContext(_imContext);
            _imContext = 0;
        }

        _io = null;
        _isInitialized = false;
    }

    public void EndFrame()
    {
        SetCurrentContext();
        if (!_isInitialized || !_isFrameActive)
            return;

        ImGui.Render();

        var drawData = ImGui.GetDrawData();
        _pendingDrawData = drawData.IsNull ? null : drawData;
        _isFrameActive = false;
    }

    public void Render()
    {
        SetCurrentContext();
        if (!_isInitialized || _pendingDrawData is null || _pendingDrawData.IsNull)
            return;

        var renderDevice = _context.GetSubsystem<RenderDevice>();
        var renderContext = renderDevice?.GetRenderContext();
        renderContext?.SetSwapChainRenderTargets();
        renderContext?.SetFullViewport();
        _renderer?.RenderDrawData(_pendingDrawData);
        _renderer?.RenderSecondaryWindows();
        _pendingDrawData = null;
    }

    public bool ProcessEvent(nint sdlEvent)
    {
        SetCurrentContext();
        if (!_isInitialized)
            return false;

        return ImGui.ImGuiBackendProcessEvent(sdlEvent);
    }

    public bool WantCaptureKeyboard
    {
        get
        {
            if (!_isInitialized)
                return false;
            return _io?.WantCaptureKeyboard == true;
        }
    }

    public bool WantCaptureMouse
    {
        get
        {
            if (!_isInitialized)
                return false;
            return _io?.WantCaptureMouse == true;
        }
    }

    public void BeginFrame()
    {
        SetCurrentContext();
        if (!_isInitialized)
            return;

        if (_isFrameActive)
        {
            ImGui.EndFrame();
            ImGui.UpdatePlatformWindows();
            _pendingDrawData = null;
            _isFrameActive = false;
        }

        var io = this._io;
        if (io is null)
            return;
        io.DeltaTime = _context.GetSubsystem<Time>()?.TimeStep ?? 0.0f;

        var renderDevice = _context.GetSubsystem<RenderDevice>();
        if (renderDevice == null)
            return;

        var size = renderDevice.GetSwapChainSize();
        if (size.X <= 0 || size.Y <= 0)
            return;

        io.DisplaySizeX = size.X;
        io.DisplaySizeY = size.Y;
        _renderer?.NewFrame((uint)size.X, (uint)size.Y);

        ImGui.ImGuiImplSDL2NewFrame();
        ImGui.NewFrame();
        _isFrameActive = true;
    }

    private void OnInputEnd()
    {
        SetCurrentContext();
        if (!_isInitialized)
            return;

        if (_isFrameActive)
        {
            ImGui.EndFrame();
            ImGui.UpdatePlatformWindows();
            _pendingDrawData = null;
            _isFrameActive = false;
        }

        var renderDevice = _context.GetSubsystem<RenderDevice>();
        if (renderDevice is null)
            return;

        BeginFrame();
    }

    private void OnEndRendering()
    {
        SetCurrentContext();
        if (!_isInitialized)
            return;

        if (!_isFrameActive)
            BeginFrame();
        if (!_isFrameActive)
            return;

        _context.SendEvent<DearImGuiEvents.EndRenderingDearImGuiEvent>();
        EndFrame();
        if (!_isInitialized || _pendingDrawData is null)
            return;

        Render();
    }

    private void PlatformInitialize()
    {
        SetCurrentContext();
        var renderDevice = _context.GetSubsystem<RenderDevice>();
        if (renderDevice == null)
            return;

        var backend = renderDevice.GetBackend();
        var input = _context.GetSubsystem<Input>();
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

        _renderer = new ImGuiDiligentRendererEx(_context);
        var size = renderDevice.GetSwapChainSize();
        if (size.X > 0 && size.Y > 0)
            _renderer.NewFrame((uint)size.X, (uint)size.Y);
    }

    private void PlatformShutdown()
    {
        SetCurrentContext();
        _renderer?.Dispose();
        _renderer = null;
        UnsubscribeFromEvents();

        if (_imContext != 0)
            ImGui.ImGuiImplSDL2Shutdown();
    }

    private void SubscribeToEvents()
    {
        _inputEndSubscription = _context.SubscribeToEvent<InputEvents.InputEndEvent>((ref InputEvents.InputEndEvent _) => OnInputEnd());
        _endRenderingSubscription = _context.SubscribeToEvent<GraphicsEvents.EndRenderingEvent>((ref GraphicsEvents.EndRenderingEvent _) => OnEndRendering());
    }

    private void UnsubscribeFromEvents()
    {
        _inputEndSubscription?.Dispose();
        _endRenderingSubscription?.Dispose();
        _inputEndSubscription = null;
        _endRenderingSubscription = null;
    }

    private void SetCurrentContext()
    {
        if (_imContext != 0)
            ImGui.ImGuiBackendSetCurrentContext(_imContext);
    }
}
