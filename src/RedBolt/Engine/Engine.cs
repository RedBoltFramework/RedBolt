using Microsoft.Extensions.Logging;

namespace RedBolt;

public sealed class Engine
{
    private readonly Dictionary<string, object?> customParameters = new(StringComparer.Ordinal);
    private readonly System.Diagnostics.Stopwatch frameClock = new();
    private float? nextTimeStep;
    private readonly Context context = new();

    public EngineParameters Parameters { get; private set; } = new();

    public Context Context => context;

    public bool IsInitialized { get; private set; }

    public bool IsExiting { get; private set; }

    public bool IsHeadless { get; private set; }

    public bool AutoExit { get; private set; } = true;

    public int MaxFps { get; private set; } = 200;

    public int MaxInactiveFps { get; private set; } = 60;

    public int MinFps { get; private set; } = 10;

    public bool PauseMinimized { get; private set; } = true;

    public int TimeStepSmoothing { get; private set; } = 2;

    public bool Initialize(EngineParameters applicationParameters)
    {
        ArgumentNullException.ThrowIfNull(applicationParameters);

        if (IsInitialized)
            throw new InvalidOperationException("Engine is already initialized.");

        Parameters = applicationParameters.Clone();
        InitializeContext();
        var resources = GetRequiredSubsystem<ResourceCache>();
        var input = GetRequiredSubsystem<Input>();
        var graphics = GetRequiredSubsystem<Graphics>();
        var renderer = GetRequiredSubsystem<Renderer>();

        resources.RegisterDefaultResources();
        Log.Logger.LogInformation(
            "Initializing engine with window {WindowWidth}x{WindowHeight}, headless={Headless}, backend={Backend}.",
            applicationParameters.WindowWidth, applicationParameters.WindowHeight, applicationParameters.Headless, applicationParameters.GraphicsBackend);
        ApplyInitializationParameters();

        if (!IsHeadless)
        {
            input.Initialize(Parameters);
            try
            {
                graphics.Initialize(Parameters, input);
            }
            catch (Exception exception)
            {
                Log.Logger.LogError(exception, "Graphics initialization failed.");
                input.Shutdown();
                throw;
            }
        }

        renderer.SetTextureQuality(Parameters.TextureQuality);
        renderer.SetTextureFilterMode(Parameters.TextureFilterMode);
        renderer.SetTextureAnisotropy(Parameters.TextureAnisotropy);
        input.SetTouchEmulation(Parameters.TouchEmulation);

        IsInitialized = true;
        IsExiting = false;
        frameClock.Restart();
        Log.Logger.LogDebug("Engine initialized.");
        return true;
    }

    public void RunFrame(Action<float> update)
    {
        ArgumentNullException.ThrowIfNull(update);

        if (!IsInitialized)
            throw new InvalidOperationException("Engine must be initialized before running frames.");

        var time = GetRequiredSubsystem<EngineTime>();
        var input = GetRequiredSubsystem<Input>();
        var graphics = GetRequiredSubsystem<Graphics>();
        var renderer = GetRequiredSubsystem<Renderer>();

        var timeStep = nextTimeStep ?? (float)frameClock.Elapsed.TotalSeconds;
        nextTimeStep = null;
        frameClock.Restart();
        if (timeStep < 0.0f)
            timeStep = 0.0f;
        else if (timeStep > 0.25f)
            timeStep = 0.25f;

        time.Advance(timeStep);
        if (Parameters.TimeOut > 0 && time.ElapsedTime >= Parameters.TimeOut)
            Exit();

        if (!IsHeadless)
        {
            input.BeginFrame();
            input.PollEvents();
            if (input.ExitRequested && AutoExit)
                Exit();
        }

        if (!IsExiting)
            update(timeStep);

        if (!IsHeadless && !IsExiting)
            renderer.RenderFrame(graphics);
    }

    public void Exit()
    {
        IsExiting = true;
    }

    public void SetMinFps(int fps)
    {
        MinFps = Math.Max(fps, 1);
    }

    public int GetMinFps() => MinFps;

    public void SetMaxFps(int fps)
    {
        MaxFps = Math.Max(fps, 0);
    }

    public int GetMaxFps() => MaxFps;

    public void SetMaxInactiveFps(int fps)
    {
        MaxInactiveFps = Math.Max(fps, 0);
    }

    public int GetMaxInactiveFps() => MaxInactiveFps;

    public void SetTimeStepSmoothing(int frames)
    {
        TimeStepSmoothing = Math.Max(frames, 1);
    }

    public int GetTimeStepSmoothing() => TimeStepSmoothing;

    public void SetPauseMinimized(bool enable)
    {
        PauseMinimized = enable;
    }

    public bool GetPauseMinimized() => PauseMinimized;

    public void SetAutoExit(bool enable)
    {
        AutoExit = enable;
    }

    public bool GetAutoExit() => AutoExit;

    public void SetNextTimeStep(float seconds)
    {
        nextTimeStep = Math.Max(seconds, 0.0f);
    }

    public float GetNextTimeStep() => nextTimeStep ?? GetRequiredSubsystem<EngineTime>().TimeStep;

    public void SetParameter(string name, object? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (EngineParameterBinding.TrySetValue(Parameters, name, value))
        {
            ApplyLiveParameter(name);
            return;
        }

        customParameters[name] = value;
    }

    public bool HasParameter(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return customParameters.ContainsKey(name) || EngineParameterBinding.IsKnown(name);
    }

    public object? GetParameter(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (customParameters.TryGetValue(name, out var value))
            return value;

        return EngineParameterBinding.TryGetValue(Parameters, name, out value) ? value : null;
    }

    public void Shutdown()
    {
        if (!IsInitialized)
            return;

        GetRequiredSubsystem<Graphics>().Shutdown();
        GetRequiredSubsystem<Renderer>().ClearViewports();
        GetRequiredSubsystem<Input>().Shutdown();
        Context.Reset();
        if (ReferenceEquals(RedBolt.Context.TryGetInstance(), Context))
            RedBolt.Context.SetInstance(null);
        IsInitialized = false;
        IsExiting = false;
        IsHeadless = false;
        nextTimeStep = null;
        frameClock.Reset();
    }

    private void InitializeContext()
    {
        Context.Reset();
        RedBolt.Context.SetInstance(Context);
        Context.RegisterSubsystem(Context);
        WorkQueue.SetMainThread();
        Context.InitializeEngineSubsystems(this);
    }

    private T GetRequiredSubsystem<T>()
        where T : class
    {
        return Context.GetSubsystem<T>()
            ?? throw new InvalidOperationException($"Required subsystem {typeof(T).Name} is not registered in Context.");
    }

    private void ApplyInitializationParameters()
    {
        IsHeadless = Parameters.Headless;
        if (!Parameters.FrameLimiter)
            MaxFps = 0;

        ApplyConfiguredResourcePaths();
    }

    private void ApplyLiveParameter(string name)
    {
        var renderer = Context.GetSubsystem<Renderer>();
        var input = Context.GetSubsystem<Input>();

        switch (name)
        {
            case nameof(EngineParameters.Headless):
                if (!IsInitialized)
                    IsHeadless = Parameters.Headless;
                break;

            case nameof(EngineParameters.TextureAnisotropy):
                renderer?.SetTextureAnisotropy(Parameters.TextureAnisotropy);
                break;

            case nameof(EngineParameters.TextureFilterMode):
                renderer?.SetTextureFilterMode(Parameters.TextureFilterMode);
                break;

            case nameof(EngineParameters.TextureQuality):
                renderer?.SetTextureQuality(Parameters.TextureQuality);
                break;

            case nameof(EngineParameters.TouchEmulation):
                input?.SetTouchEmulation(Parameters.TouchEmulation);
                break;
        }
    }

    private void ApplyConfiguredResourcePaths()
    {
        var resourcePaths = SplitPathList(Parameters.ResourcePaths);
        if (resourcePaths.Count == 0)
            return;

        var prefixPaths = SplitPathList(Parameters.ResourcePrefixPaths);
        var resources = GetRequiredSubsystem<ResourceCache>();
        if (prefixPaths.Count == 0)
        {
            foreach (var resourcePath in resourcePaths)
                resources.AddResourceDir(NormalizeConfiguredResourcePath(resourcePath));
            return;
        }

        foreach (var prefixPath in prefixPaths)
        {
            var normalizedPrefix = NormalizeConfiguredResourcePath(prefixPath);
            foreach (var resourcePath in resourcePaths)
            {
                var combinedPath = Path.IsPathRooted(resourcePath)
                    ? NormalizeConfiguredResourcePath(resourcePath)
                    : Path.GetFullPath(Path.Combine(normalizedPrefix, resourcePath));
                resources.AddResourceDir(combinedPath);
            }
        }
    }

    private static IReadOnlyList<string> SplitPathList(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static string NormalizeConfiguredResourcePath(string path)
    {
        return Path.IsPathRooted(path)
            ? Path.GetFullPath(path)
            : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, path));
    }
}
