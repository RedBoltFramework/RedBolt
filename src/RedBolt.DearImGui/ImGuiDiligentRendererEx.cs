using RedBolt.Bindings.Diligent;
using RedBolt.Bindings.DearImGui;
using RedBolt;

namespace RedBolt;

public sealed class ImGuiDiligentRendererEx : IDisposable
{
    private const uint DefaultVBInitialSize = 16384;
    private const uint DefaultIBInitialSize = 32768;

    private readonly Context _context;
    private readonly nint _nativeRenderer;
    private bool _disposed;

    public ImGuiDiligentRendererEx(Context context)
    {
        this._context = context ?? throw new ArgumentNullException(nameof(context));

        var graphics = context.GetSubsystem<Graphics>()
            ?? throw new InvalidOperationException("Graphics subsystem not available.");

        var nativeDevice = graphics.GetNativeRenderDevice()
            ?? throw new InvalidOperationException("Native render device not available.");

        uint backBufferFmt = (uint)graphics.GetSwapChainColorFormat();
        uint depthBufferFmt = (uint)graphics.GetSwapChainDepthFormat();

        _nativeRenderer = ImGui.ImGuiBackendCreateRenderer(
            nativeDevice.Handle, backBufferFmt, depthBufferFmt,
            DefaultVBInitialSize, DefaultIBInitialSize);

        if (_nativeRenderer == 0)
            throw new InvalidOperationException("Failed to create ImGui Diligent renderer.");
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

        var graphics = _context.GetSubsystem<Graphics>();
        var deviceContext = graphics?.GetImmediateContext();
        if (deviceContext == null || deviceContext.IsNull)
            return;

        ImGui.ImGuiBackendRendererRenderDrawData(
            _nativeRenderer, deviceContext.Handle, drawData.Handle,
            0, 0, 0, 0);
    }

    public void RenderSecondaryWindows()
    {
    }
}
