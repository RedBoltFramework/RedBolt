namespace RedBolt.RenderAPI;

using RedBolt.Bindings.Diligent;
using RedBolt.Bindings.Sdl;
using RedBolt.Engine;
using RedBolt.Graphics;
using RedBolt.RenderAPI.Backends;

/// <summary>Graphics backend compiled for the current target platform.</summary>
public interface IGraphicsBackend
{
    RenderBackend Backend { get; }

    bool UsesOpenGLWindow { get; }

    void ConfigureWindow();

    GraphicsBackendResources Initialize(EngineParameters parameters, RenderDeviceSettings deviceSettings,
        WindowSettings windowSettings, SdlWindow window);
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

/// <summary>Graphics backends compiled into the current target.</summary>
public sealed class RuntimeServices
{
    private readonly Dictionary<RenderBackend, IGraphicsBackend> _graphicsBackends;

    public RuntimeServices(IEnumerable<IGraphicsBackend> graphicsBackends)
    {
        ArgumentNullException.ThrowIfNull(graphicsBackends);
        _graphicsBackends = graphicsBackends.ToDictionary(static backend => backend.Backend);
    }

    public IReadOnlyCollection<IGraphicsBackend> GraphicsBackends => _graphicsBackends.Values;

    public IGraphicsBackend GetGraphicsBackend(RenderBackend backend)
    {
        return _graphicsBackends.TryGetValue(backend, out var graphicsBackend)
            ? graphicsBackend
            : throw new InvalidOperationException($"Render backend '{backend}' is not compiled for this platform.");
    }

    public T? GetGraphicsApi<T>(RenderBackend backend) where T : class =>
        _graphicsBackends.TryGetValue(backend, out var graphicsBackend) ? graphicsBackend as T : null;

    internal static RuntimeServices Default { get; } = new(CreateGraphicsBackends());

    private static IGraphicsBackend[] CreateGraphicsBackends()
    {
#if REDBOLT_PLATFORM_WINDOWS
        return
        [
            new D3D11GraphicsBackend(),
            new D3D12GraphicsBackend(),
            new OpenGLGraphicsBackend(),
            new VulkanGraphicsBackend()
        ];
#elif REDBOLT_PLATFORM_LINUX
        return
        [
            new OpenGLGraphicsBackend(),
            new VulkanGraphicsBackend()
        ];
#elif REDBOLT_PLATFORM_BROWSER || REDBOLT_PLATFORM_ANDROID
        return [new OpenGLESGraphicsBackend()];
#else
        return [];
#endif
    }
}
