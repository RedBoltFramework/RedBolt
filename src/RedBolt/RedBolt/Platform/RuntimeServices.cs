using RedBolt.Bindings.Diligent;
using RedBolt.Bindings.Sdl;

using RedBolt.Engine;
using RedBolt.Graphics;
using RedBolt.RenderAPI;
namespace RedBolt.Platform;


/// <summary>Platform services available to the shared engine core.</summary>
public interface IPlatformRuntime
{
    /// <summary>Whether files may be opened by the logging subsystem.</summary>
    bool SupportsFileLogging { get; }

    bool UseHighDensityWindow { get; }

    void InitializeApplicationParameters(EngineParameters parameters);

    /// <summary>Return native window information required by graphics backends.</summary>
    NativeWindowInfo GetNativeWindowInfo(SdlWindow window);
}

/// <summary>Native window information shared between platform and graphics modules.</summary>
public readonly record struct NativeWindowInfo(nint Display, uint WindowId);

/// <summary>Graphics backend implemented by a graphics module.</summary>
public interface IGraphicsBackend
{
    /// <summary>Backend exposed by this module.</summary>
    RenderBackend Backend { get; }

    /// <summary>Whether the SDL window must be created with OpenGL support.</summary>
    bool UsesOpenGLWindow { get; }

    /// <summary>Configure SDL attributes before creating the window.</summary>
    void ConfigureWindow();

    /// <summary>Create the native render device and swap chain.</summary>
    GraphicsBackendResources Initialize(EngineParameters parameters, RenderDeviceSettings deviceSettings,
        WindowSettings windowSettings, SdlWindow window, IPlatformRuntime platform);
}

/// <summary>Native graphics resources returned to the shared graphics subsystem.</summary>
public sealed class GraphicsBackendResources
{
    public required IEngineFactory Factory { get; init; }

    public required IRenderDevice Device { get; init; }

    public required IDeviceContext ImmediateContext { get; init; }

    public required ISwapChain SwapChain { get; init; }

    public required IGraphicsBackendLifetime Lifetime { get; init; }

    public int Width { get; init; }

    public int Height { get; init; }

    public int MultiSample { get; init; } = 1;
}

/// <summary>Backend-specific state retained for the lifetime of a render device.</summary>
public interface IGraphicsBackendLifetime
{
    nint NativeContext { get; }

    void SetVSync(bool enable);

    void Resize(IRenderDevice renderDevice, ISwapChain swapChain);

    void ReleaseBeforeCommonResources();

    void ReleaseAfterCommonResources(SdlWindow? window);
}

/// <summary>Runtime composition selected by the platform module.</summary>
public sealed class RuntimeServices
{
    private static readonly object _defaultLock = new();
    private readonly Dictionary<RenderBackend, IGraphicsBackend> _graphicsBackends;

    public RuntimeServices(IPlatformRuntime platform, IEnumerable<IGraphicsBackend> graphicsBackends)
    {
        ArgumentNullException.ThrowIfNull(platform);
        ArgumentNullException.ThrowIfNull(graphicsBackends);

        Platform = platform;
        _graphicsBackends = graphicsBackends.ToDictionary(static backend => backend.Backend);
    }

    public IPlatformRuntime Platform { get; }

    public IReadOnlyCollection<IGraphicsBackend> GraphicsBackends => _graphicsBackends.Values;

    public IGraphicsBackend GetGraphicsBackend(RenderBackend backend)
    {
        return _graphicsBackends.TryGetValue(backend, out var graphicsBackend)
            ? graphicsBackend
            : throw new InvalidOperationException($"Render backend '{backend}' is not available on this platform.");
    }

    public T? GetPlatformApi<T>() where T : class => Platform as T;

    public T? GetGraphicsApi<T>(RenderBackend backend) where T : class =>
        _graphicsBackends.TryGetValue(backend, out var graphicsBackend) ? graphicsBackend as T : null;

    internal static RuntimeServices Empty { get; } =
        new(new EmptyPlatformRuntime(), Array.Empty<IGraphicsBackend>());

    internal static RuntimeServices Default { get; private set; } = Empty;

    internal static void SetDefault(RuntimeServices runtimeServices)
    {
        ArgumentNullException.ThrowIfNull(runtimeServices);
        lock (_defaultLock)
        {
            if (!ReferenceEquals(Default, Empty)
                && Default.Platform.GetType() != runtimeServices.Platform.GetType())
            {
                throw new InvalidOperationException(
                    $"Platform module '{Default.Platform.GetType().Name}' is already active; "
                    + $"cannot activate '{runtimeServices.Platform.GetType().Name}'.");
            }

            Default = runtimeServices;
        }
    }

    private sealed class EmptyPlatformRuntime : IPlatformRuntime
    {
        public bool SupportsFileLogging => true;

        public bool UseHighDensityWindow => false;

        public void InitializeApplicationParameters(EngineParameters parameters)
        {
        }

        public NativeWindowInfo GetNativeWindowInfo(SdlWindow window) =>
            throw new PlatformNotSupportedException("Native window information requires a platform module.");
    }
}
