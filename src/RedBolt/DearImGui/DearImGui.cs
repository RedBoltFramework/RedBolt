namespace RedBolt;

using RedBolt.Bindings.DearImGui;
using RedBolt.Bindings.Diligent;
using RedBolt.Bindings.Sdl;
using RedBolt.Core;
using RedBolt.Graphics;
using RedBolt.Input;
using RedBolt.RenderAPI;

public class DearImGui : RedObject
{
    public const float DefaultFontSize = 14.0f;

    /// <summary>Construct.</summary>
    public DearImGui(ImGuiConfigFlags flags = 0)
    {
        _imContext = ImGui.ImGuiBackendCreateContext(0);
        SetCurrentContext();

        var io = ImGui.GetIO();
        // TODO: port io.UserData = this when generated bindings can retain a managed Object pointer as native user data.
        // UI subsystem is responsible for managing cursors and that interferes with ImGui.
        io.ConfigFlags |= flags;

        PlatformInitialize();

        // Subscribe to events
        SubscribeToEvent<InputEvents.SDLRawInputEvent>(OnRawEvent);
        SubscribeToEvent<GraphicsEvents.ScreenModeEvent>(OnScreenMode);
        SubscribeToEvent<InputEvents.InputBeginEvent>((ref InputEvents.InputBeginEvent _) => OnInputBegin());
        SubscribeToEvent<InputEvents.InputEndEvent>((ref InputEvents.InputEndEvent _) => OnInputEnd());
        SubscribeToEvent<GraphicsEvents.EndRenderingEvent>((ref GraphicsEvents.EndRenderingEvent _) => OnRenderEnd());
        SubscribeToEvent<CoreEvents.EndFrameEvent>((ref CoreEvents.EndFrameEvent _) => ClearReferencedTextures());
        SubscribeToEvent<GraphicsEvents.DeviceLostEvent>((ref GraphicsEvents.DeviceLostEvent _) => PlatformShutdown());
        SubscribeToEvent<GraphicsEvents.DeviceResetEvent>((ref GraphicsEvents.DeviceResetEvent _) => PlatformInitialize());
        SubscribeToEvent<InputEvents.MouseVisibleChangedEvent>(OnMouseVisibilityChanged);
    }

    /// <summary>Destruct.</summary>
    public override void Dispose()
    {
        if (_imContext == 0)
            return;

        SetCurrentContext();
        PlatformShutdown();
        ImGui.ImGuiBackendDestroyContext(_imContext);
        _imContext = 0;
        base.Dispose();
    }

    /// <summary>Enable or disable relative mouse movement.</summary>
    /// <remarks>
    /// Should be called withing ImGUI window.
    /// Relative mouse movement is automatically disabled when all mouse buttons are released.
    /// </remarks>
    public void SetRelativeMouseMove(bool enabled, bool revertMousePositionOnDisable)
    {
        SetCurrentContext();
        if (!enabled || !ImGui.ImGuiBackendHasCurrentWindow())
        {
            _enableRelativeMouseMove = false;
            SdlNativeBindings.SdlSetRelativeMouseMode(SdlBool.SdlFalse);
            return;
        }

        _enableRelativeMouseMove = true;
        SdlNativeBindings.SdlSetRelativeMouseMode(SdlBool.SdlTrue);

        var io = ImGui.GetIO();
        _revertMousePositionOnDisable = revertMousePositionOnDisable;
        _revertMousePosition = new Vector2f(io.MousePosX, io.MousePosY);
    }

    /// <summary>Return relative mouse movement value.</summary>
    public Vector2f RelativeMouseMove => _relativeMouseMove;

    // TODO: port ImFont* AddFont(const ea::string& fontPath, const ImWchar* ranges = nullptr, float size = 0, bool merge = false)
    // when SharpCpp exposes ImFontAtlas and ImFontConfig.
    // TODO: port ImFont* AddFont(const void* data, unsigned dsize, const char* name, const ImWchar* ranges = nullptr,
    // float size = 0, bool merge = false) when SharpCpp exposes ImFontAtlas and ImFontConfig.
    // TODO: port ImFont* AddFontCompressed(const void* data, unsigned dsize, const char* name,
    // const ImWchar* ranges = nullptr, float size = 0, bool merge = false) when SharpCpp exposes ImFontAtlas and ImFontConfig.

    /// <summary>Apply built-in system ui style.</summary>
    /// <param name="darkStyle">Enables dark style, otherwise it is a light style.</param>
    /// <param name="alpha">Value between 0.0f - 1.0f.</param>
    public void ApplyStyleDefault(bool darkStyle, float alpha)
    {
        SetCurrentContext();
        var style = ImGui.GetStyle();
        style.ScrollbarSize = 10.0f;
        if (darkStyle)
            ImGui.StyleColorsDark(style);
        else
            ImGui.StyleColorsLight(style);
        style.Alpha = 1.0f;
        style.FrameRounding = 3.0f;
    }

    /// <summary>Hold a reference to this texture until end of frame.</summary>
    public void ReferenceTexture(Texture2D texture)
    {
        var textureView = texture.GetRawTexture()!.GetHandles().ShaderResourceView!;
        _ = textureView.AddRef();
        _referencedTextures.Add(textureView);
    }

    /// <summary>When set to true, DearImGui will not consume SDL events and they will be passed to Input and other subsystems.</summary>
    public bool PassThroughEvents { get; set; }

    private void PlatformInitialize()
    {
        SetCurrentContext();
        var renderDevice = GetSubsystem<RenderDevice>()!;
        var size = renderDevice.GetSwapChainSize();
        ImGui.ImGuiBackendSetDisplaySize(size.X, size.Y);

        var sdlWindow = renderDevice.GetSDLWindow()!;
        switch (renderDevice.GetBackend())
        {
            case RenderBackend.OpenGL:
                ImGui.ImGuiBackendInitForOpenGL(sdlWindow.Handle, SdlNativeBindings.SdlGlGetCurrentContext());
                break;
            case RenderBackend.Vulkan:
                // Diligent manages Vulkan on its own.
                ImGui.ImGuiBackendInitForOther(sdlWindow.Handle);
                break;
            case RenderBackend.D3D11:
            case RenderBackend.D3D12:
                ImGui.ImGuiBackendInitForD3D(sdlWindow.Handle);
                break;
            default:
                throw new InvalidOperationException("Not implemented");
        }

        _renderer = new ImGuiDiligentRendererEx(renderDevice);

        // Ensure that swap chain is initialized
        _renderer.NewFrame();

        // TODO: port ImGui_ImplSDL2_UpdateMonitors when it is exposed by generated bindings.
    }

    private void PlatformShutdown()
    {
        SetCurrentContext();
        ClearReferencedTextures();
        ClearPerScreenFonts();

        _renderer?.Dispose();
        _renderer = null;
        ImGui.ImGuiBackendShutdown();
    }

    private void ReallocateFontTexture()
    {
        // TODO: port the complete C++ body when SharpCpp exposes ImFontAtlas and its per-monitor atlas collection.
    }

    private void ClearPerScreenFonts()
    {
        for (var i = _fontTextures.Count - 1; i >= 0; --i)
            _fontTextures[i].Dispose();
        _fontTextures.Clear();
        // TODO: port deletion of io.AllFonts[1..] when SharpCpp exposes ImFontAtlas and ImGuiIO::AllFonts.
    }

    // TODO: port SharedPtr<Texture2D> AllocateFontTexture(ImFontAtlas* atlas) const when SharpCpp exposes ImFontAtlas.

    private void OnRawEvent(ref InputEvents.SDLRawInputEvent args)
    {
        SetCurrentContext();
        var sdlEvent = SdlEvent.CreateManagedWrapper(args.SDLEvent, ownsHandle: false);
        var eventType = (SdlEventType)sdlEvent.Type;
        var io = ImGui.GetIO();

        switch (eventType)
        {
            case SdlEventType.SdlMousemotion:
                _relativeMouseMove.X += sdlEvent.MotionXrel;
                _relativeMouseMove.Y += sdlEvent.MotionYrel;
                break;
            case SdlEventType.SdlFingerup:
                io.AddMouseSourceEvent((ImGuiMouseSource)1);
                io.AddMouseButtonEvent(0, false);
                io.AddMousePosEvent(-1.0f, -1.0f);
                break;
            case SdlEventType.SdlFingerdown:
            {
                io.AddMouseSourceEvent((ImGuiMouseSource)1);
                var mousePosition = new Vector2f(sdlEvent.TfingerX, sdlEvent.TfingerY);
                // TODO: is this condition actually relevant, looks not?
                if ((io.ConfigFlags & ImGuiConfigFlags.ImGuiConfigFlagsViewportsEnable) != 0)
                {
                    var window = SdlNativeBindings.SdlGetWindowFromID(sdlEvent.TfingerWindowID);
                    SdlNativeBindings.SdlGetWindowPosition(window, out var windowX, out var windowY);
                    SdlNativeBindings.SdlGetWindowSize(window, out var sizeX, out var sizeY);
                    mousePosition.X = mousePosition.X * sizeX + windowX;
                    mousePosition.Y = mousePosition.Y * sizeY + windowY;
                }
                io.AddMousePosEvent(mousePosition.X, mousePosition.Y);
                io.AddMouseButtonEvent(0, true);
                io.AddMousePosEvent(mousePosition.X, mousePosition.Y);
                break;
            }
            case SdlEventType.SdlFingermotion:
            {
                var mousePosition = new Vector2f(sdlEvent.TfingerX, sdlEvent.TfingerY);
                if ((io.ConfigFlags & ImGuiConfigFlags.ImGuiConfigFlagsViewportsEnable) != 0)
                {
                    var window = SdlNativeBindings.SdlGetWindowFromID(sdlEvent.TfingerWindowID);
                    SdlNativeBindings.SdlGetWindowPosition(window, out var windowX, out var windowY);
                    SdlNativeBindings.SdlGetWindowSize(window, out var sizeX, out var sizeY);
                    mousePosition.X = mousePosition.X * sizeX + windowX;
                    mousePosition.Y = mousePosition.Y * sizeY + windowY;
                }
                io.AddMouseSourceEvent((ImGuiMouseSource)1);
                io.AddMousePosEvent(mousePosition.X, mousePosition.Y);
                break;
            }
        }

        // Disable mouse button press and movement events when in "relative" mode
        // so that invisible cursor does not interact with UI widgets.
        // Mouse button release is allowed because relative mouse mode is usually disabled on release.
        var isRelativeMouseEventConsumed = _enableRelativeMouseMove
            && eventType is SdlEventType.SdlMousebuttondown or SdlEventType.SdlMousemotion;
        if (!isRelativeMouseEventConsumed)
            ImGui.ImGuiBackendProcessEvent(args.SDLEvent);

        // Consume events handled by imgui, unless explicitly told not to.
        if (!PassThroughEvents)
        {
            args.Consumed = eventType switch
            {
                SdlEventType.SdlKeydown or SdlEventType.SdlKeyup => io.WantCaptureKeyboard,
                SdlEventType.SdlTextinput => io.WantTextInput,
                SdlEventType.SdlMousemotion or SdlEventType.SdlMousebuttondown or SdlEventType.SdlMousebuttonup
                    or SdlEventType.SdlMousewheel or SdlEventType.SdlFingerdown or SdlEventType.SdlFingerup
                    or SdlEventType.SdlFingermotion => io.WantCaptureMouse || isRelativeMouseEventConsumed,
                _ => args.Consumed
            };
        }
    }

    private void OnScreenMode(ref GraphicsEvents.ScreenModeEvent args)
    {
        SetCurrentContext();
        ImGui.ImGuiBackendSetDisplaySize(args.Width, args.Height);
    }

    private void OnInputBegin()
    {
        _relativeMouseMove = Vector2f.Zero;
    }

    private void OnInputEnd()
    {
        SetCurrentContext();
        if (ImGui.ImGuiBackendIsWithinFrameScope())
        {
            ImGui.EndFrame();
            ImGui.UpdatePlatformWindows();
        }

        var input = GetSubsystem<InputSystem>()!;
        var renderDevice = GetSubsystem<RenderDevice>()!;

        // TODO: port per-screen font texture allocation and TexID assignment when SharpCpp exposes ImFontAtlas.

        _renderer!.NewFrame();
        ImGui.ImGuiBackendNewFrame();

        ImGui.NewFrame();

        if (!input.IsMouseVisible())
            ImGui.SetMouseCursor(ImGuiMouseCursor.ImGuiMouseCursorNone);

        // TODO: port ImGuizmo::BeginFrame when ImGuizmo is exposed by generated bindings.
    }

    private void OnRenderEnd()
    {
        SetCurrentContext();
        // When DearImGui subsystem is recreated during runtime this method may be called without UI being rendered.
        if (!ImGui.ImGuiBackendIsWithinFrameScope())
            return;

        SendEvent<DearImGuiEvents.EndRenderingDearImGuiEvent>();

        // Disable relative mouse movement automatically if none of mouse buttons are down
        if (_enableRelativeMouseMove && !ImGui.IsAnyMouseDown())
        {
            _enableRelativeMouseMove = false;
            SdlNativeBindings.SdlSetRelativeMouseMode(SdlBool.SdlFalse);
        }

        var io = ImGui.GetIO();
        if (ImGui.ImGuiBackendIsWithinFrameScope())
            ImGui.Render();

        // Revert mouse position after relative movement
        if (!_enableRelativeMouseMove && _revertMousePositionOnDisable)
        {
            _revertMousePositionOnDisable = false;
            io.MousePosX = _revertMousePosition.X;
            io.MousePosY = _revertMousePosition.Y;
            io.MousePosPrevX = _revertMousePosition.X;
            io.MousePosPrevY = _revertMousePosition.Y;
            io.WantSetMousePos = true;
        }

        var renderContext = GetSubsystem<RenderDevice>()!.GetRenderContext();
        renderContext.SetSwapChainRenderTargets();
        renderContext.SetFullViewport();

        _renderer!.RenderDrawData(ImGui.GetDrawData());
        _renderer.RenderSecondaryWindows();
    }

    private void OnMouseVisibilityChanged(ref InputEvents.MouseVisibleChangedEvent args)
    {
        SetCurrentContext();
        ImGui.SetMouseCursor(args.Visible ? ImGuiMouseCursor.ImGuiMouseCursorArrow : ImGuiMouseCursor.ImGuiMouseCursorNone);
    }

    private void ClearReferencedTextures()
    {
        for (var i = _referencedTextures.Count - 1; i >= 0; --i)
            _ = _referencedTextures[i].Release();
        _referencedTextures.Clear();
    }

    private void SetCurrentContext()
    {
        if (_imContext != 0)
            ImGui.ImGuiBackendSetCurrentContext(_imContext);
    }

    private readonly List<Texture2D> _fontTextures = [];
    private readonly List<float> _fontSizes = [];
    private nint _imContext;
    private readonly List<ITextureView> _referencedTextures = [];
    private bool _enableRelativeMouseMove;
    private Vector2f _relativeMouseMove;
    private bool _revertMousePositionOnDisable;
    private Vector2f _revertMousePosition;
    private ImGuiDiligentRendererEx? _renderer;
}
