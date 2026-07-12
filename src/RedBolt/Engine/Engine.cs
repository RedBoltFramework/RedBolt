using Microsoft.Extensions.Logging;

namespace RedBolt;

public sealed class Engine : Object
{
    private readonly Dictionary<string, object?> _customParameters = new(StringComparer.Ordinal);
    private readonly HiresTimer _frameTimer = new();
    private float? _nextTimeStep;
    private readonly List<float> _lastTimeSteps = [];
    private float _currentTimeStep;
    private long _timeOut;
    private bool _audioPaused;
    private Context _previousContext = Context.Instance;
    private readonly Context _context = new();

    public EngineParameters Parameters { get; private set; } = new();

    public bool IsInitialized { get; private set; }

    public bool IsExiting { get; private set; }

    public bool IsHeadless { get; private set; }

    public bool AutoExit { get; private set; } = true;

    public int MaxFps { get; private set; } = 200;

    public int MaxInactiveFps { get; private set; } = 60;

    public int MinFps { get; private set; } = 10;

    public bool PauseMinimized { get; private set; }

    public int TimeStepSmoothing { get; private set; } = 2;

    public bool Initialize(EngineParameters applicationParameters)
    {
        ArgumentNullException.ThrowIfNull(applicationParameters);

        if (IsInitialized)
            return true;

        Parameters = applicationParameters.Clone();
        InitializeContext();
        var resources = GetSubsystem<ResourceCache>()!;
        var input = GetSubsystem<Input>()!;
        var graphics = GetSubsystem<Graphics>()!;
        var renderer = GetSubsystem<Renderer>()!;

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

            var audio = GetSubsystem<Audio>()!;
            if (Parameters.Sound)
            {
                audio.SetMode(
                    Parameters.SoundBuffer,
                    Parameters.SoundMixRate,
                    (SpeakerMode)Parameters.SoundMode,
                    Parameters.SoundInterpolation);
            }
        }

        renderer.SetTextureQuality(Parameters.TextureQuality);
        renderer.SetTextureFilterMode(Parameters.TextureFilterMode);
        renderer.SetTextureAnisotropy(Parameters.TextureAnisotropy);
        input.SetTouchEmulation(Parameters.TouchEmulation);

        IsInitialized = true;
        IsExiting = false;
        _currentTimeStep = 0.0f;
        _nextTimeStep = null;
        _lastTimeSteps.Clear();
        _frameTimer.Reset();
        Log.Logger.LogDebug("Engine initialized.");
        return true;
    }

    public void RunFrame() => RunFrame(static _ => { });

    public void RunFrame(Action<float> update)
    {
        ArgumentNullException.ThrowIfNull(update);

        if (!IsInitialized)
            throw new InvalidOperationException("Engine must be initialized before running frames.");

        var time = GetSubsystem<Time>()!;
        var input = GetSubsystem<Input>()!;
        var graphics = GetSubsystem<Graphics>()!;
        var renderer = GetSubsystem<Renderer>()!;

        if (!IsHeadless && !graphics.IsInitialized)
        {
            IsExiting = true;
            return;
        }

        var timeStep = _nextTimeStep ?? _currentTimeStep;
        _nextTimeStep = null;
        if (timeStep < 0.0f)
            timeStep = 0.0f;

        time.BeginFrame(timeStep);

        var audio = GetSubsystem<Audio>()!;
        var isPaused = !IsHeadless && PauseMinimized && input.IsMinimized;
        if (isPaused)
        {
            if (audio.IsPlaying())
            {
                audio.Stop();
                _audioPaused = true;
            }
        }
        else if (_audioPaused)
        {
            audio.Play();
            _audioPaused = false;
        }

        if (!IsHeadless)
        {
            input.BeginFrame();
            input.PollEvents();
            if (input.ExitRequested && AutoExit)
                Exit();
        }

        if (!IsExiting)
        {
            var shouldUpdate = !isPaused;
            if (shouldUpdate)
            {
                SendCoreEvents(timeStep);
                update(timeStep);
            }

            if (!IsHeadless)
                renderer.RenderFrame(graphics, beforePresent: null);
        }

        ApplyFrameLimit();
        time.EndFrame();
    }

    public void Exit()
    {
        IsExiting = true;
    }

    public void SetMinFps(int fps)
    {
        MinFps = Math.Max(fps, 0);
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
        TimeStepSmoothing = Math.Clamp(frames, 1, 20);
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
        _nextTimeStep = Math.Max(seconds, 0.0f);
    }

    public float GetNextTimeStep() => _nextTimeStep ?? _currentTimeStep;

    public void SetParameter(string name, object? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (EngineParameterBinding.TrySetValue(Parameters, name, value))
        {
            ApplyLiveParameter(name);
            return;
        }

        _customParameters[name] = value;
    }

    public bool HasParameter(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return _customParameters.ContainsKey(name) || EngineParameterBinding.IsKnown(name);
    }

    public object? GetParameter(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (_customParameters.TryGetValue(name, out var value))
            return value;

        return EngineParameterBinding.TryGetValue(Parameters, name, out value) ? value : null;
    }

    public void DumpProfiler()
    {
        if (!IsInitialized)
            return;
        Log.Logger.LogInformation("Profiler data is not available in the managed runtime.");
    }

    public void DumpResources(bool dumpFileName = false)
    {
        var resources = GetSubsystem<ResourceCache>()!;
        Log.Logger.LogInformation("{ResourceUsage}", dumpFileName ? resources.PrintResources() : resources.PrintMemoryUsage());
    }

    public void DumpMemory()
    {
        Log.Logger.LogInformation("Managed memory: {MemoryBytes} bytes", GC.GetTotalMemory(forceFullCollection: false));
    }

    public void Shutdown()
    {
        if (!IsInitialized)
            return;

        GetSubsystem<RenderDevice>()!.OnEngineShutdown();
        GetSubsystem<Graphics>()!.Shutdown();
        GetSubsystem<Renderer>()!.ClearViewports();
        GetSubsystem<Input>()!.Shutdown();
        _context.Reset();
        if (ReferenceEquals(RedBolt.Context.Instance, _context))
            RedBolt.Context.SetInstance(_previousContext);
        IsInitialized = false;
        IsExiting = false;
        IsHeadless = false;
        _nextTimeStep = null;
        _currentTimeStep = 0.0f;
        _lastTimeSteps.Clear();
        _timeOut = 0;
        _audioPaused = false;
        _frameTimer.Reset();
    }

    public void InitializeVirtualFileSystem(bool enableResourceRootFile)
    {
        ApplyConfiguredResourcePaths();
    }

    public string GetAppPreferencesDir() => Parameters.ApplicationPreferencesDir;

    public void Update()
    {
        if (!IsInitialized)
            throw new InvalidOperationException("Engine must be initialized before sending update events.");
        SendCoreEvents(_currentTimeStep);
    }

    public void Render()
    {
        if (!IsInitialized || IsHeadless)
            return;
        var graphics = GetSubsystem<Graphics>()!;
        var renderer = GetSubsystem<Renderer>()!;
        renderer.RenderFrame(graphics, beforePresent: null);
    }

    private void InitializeContext()
    {
        _previousContext = RedBolt.Context.Instance;
        _context.Reset();
        RedBolt.Context.SetInstance(_context);
        WorkQueue.SetMainThread();
        _context.InitializeEngineSubsystems(this);
    }

    private void ApplyInitializationParameters()
    {
        IsHeadless = Parameters.Headless;
        if (!Parameters.FrameLimiter)
            MaxFps = 0;

        ApplyConfiguredResourcePaths();
        _timeOut = (long)Parameters.TimeOut * 1_000_000L;
    }

    private void ApplyLiveParameter(string name)
    {
        var renderer = GetSubsystem<Renderer>();
        var input = GetSubsystem<Input>();

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
        var resources = GetSubsystem<ResourceCache>()!;
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

    private void SendCoreEvents(float timeStep)
    {
        var inputReadyEventData = new CoreEvents.InputReadyEvent
        {
            TimeStep = timeStep
        };
        _context.SendEvent(ref inputReadyEventData);

        var updateEventData = new CoreEvents.UpdateEvent
        {
            TimeStep = timeStep
        };
        _context.SendEvent(ref updateEventData);

        var postUpdateEventData = new CoreEvents.PostUpdateEvent
        {
            TimeStep = timeStep
        };
        _context.SendEvent(ref postUpdateEventData);

        var renderUpdateEventData = new CoreEvents.RenderUpdateEvent
        {
            TimeStep = timeStep
        };
        _context.SendEvent(ref renderUpdateEventData);

        var postRenderUpdateEventData = new CoreEvents.PostRenderUpdateEvent
        {
            TimeStep = timeStep
        };
        _context.SendEvent(ref postRenderUpdateEventData);
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

    public void ApplyFrameLimit()
    {
        if (!IsInitialized)
            return;

        uint maxFps = (uint)Math.Max(MaxFps, 0);
        var input = GetSubsystem<Input>();
        if (input is not null && !input.HasFocus)
            maxFps = Math.Min((uint)Math.Max(MaxInactiveFps, 0), maxFps);

        long elapsed = 0;

        if (maxFps > 0)
        {
            long targetMax = 1_000_000L / maxFps;
            while (true)
            {
                elapsed = _frameTimer.GetUSec(false);
                if (elapsed >= targetMax)
                    break;

                if (targetMax - elapsed >= 1000L)
                {
                    var sleepTime = (uint)((targetMax - elapsed) / 1000L);
                    Time.Sleep(sleepTime);
                }
            }
        }

        elapsed = _frameTimer.GetUSec(true);
        if (_timeOut > 0)
        {
            _timeOut -= elapsed;
            if (_timeOut <= 0)
                Exit();
        }

        if (MinFps > 0)
        {
            var targetMin = 1_000_000L / MinFps;
            if (elapsed > targetMin)
                elapsed = targetMin;
        }

        _currentTimeStep = 0.0f;
        _lastTimeSteps.Add(elapsed / 1_000_000.0f);
        if (_lastTimeSteps.Count > TimeStepSmoothing)
        {
            _lastTimeSteps.RemoveRange(0, TimeStepSmoothing);
            foreach (var value in _lastTimeSteps)
                _currentTimeStep += value;
            _currentTimeStep /= _lastTimeSteps.Count;
        }
        else
            _currentTimeStep = _lastTimeSteps[^1];

        _nextTimeStep = _currentTimeStep;
    }
}
