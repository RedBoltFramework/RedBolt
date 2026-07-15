using Microsoft.Extensions.Logging;

namespace RedBolt;

public sealed class Engine : Object
{
    private readonly record struct ResourceRootEntry(string FullName, string ShortName);

    private readonly Dictionary<string, object?> _customParameters = new(StringComparer.Ordinal);
    private readonly HiresTimer _frameTimer = new();
    private float _currentTimeStep;
    private readonly List<float> _lastTimeSteps = [];
    private long _timeOut;
    private bool _audioPaused;
    private bool _exitRequired;
    private Context _previousContext = Context.Instance;
    private readonly Context _context = new();
    private readonly ConfigFile _configFile = new();

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

    public override void Dispose()
    {
        Shutdown();
        base.Dispose();
    }

    public bool Initialize(EngineParameters applicationParameters)
    {
        ArgumentNullException.ThrowIfNull(applicationParameters);

        if (IsInitialized)
            return true;

        Parameters = applicationParameters.Clone();
        InitializeContext();
        PopulateDefaultParameters();
        LoadConfigFiles();
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
                GetSubsystem<RenderDevice>()!.PostInitialize();
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
        _exitRequired = false;
        _currentTimeStep = 0.0f;
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
        GetSubsystem<WorkQueue>()!.Update();

        if (!IsHeadless && !graphics.IsInitialized)
        {
            IsExiting = true;
            return;
        }

        var timeStep = _currentTimeStep;
        if (timeStep < 0.0f)
            timeStep = 0.0f;

        time.BeginFrame(timeStep);

        var audio = GetSubsystem<Audio>()!;
        var isPaused = PauseMinimized && input.IsMinimized;
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
                HandleExitRequested();
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
        HandleEndFrame();
    }

    /// <summary>
    /// Update the web canvas size and preserve the current input/display state.
    /// </summary>
    public void OnCanvasResize(int width, int height, bool isFullScreen, float dpiScale)
    {
        Log.Logger.LogInformation(
            "Web canvas resized to {Width}x{Height}{Fullscreen} with DPI scale={DpiScale}.",
            width, height, isFullScreen ? " FullScreen" : " ", dpiScale);

        var input = GetSubsystem<Input>();
        var graphics = GetSubsystem<Graphics>();
        var mouseVisible = input?.MouseVisible ?? false;
        var mouseMode = input?.GetMouseMode() ?? MouseMode.Absolute;

        // TODO: port UI and RmlUI scale state updates when the managed UI APIs exist.
        graphics?.SetMode(width, height);

        if (input is not null)
        {
            input.SetMouseVisible(mouseVisible);
            input.SetMouseMode(mouseMode);
        }
    }

    // C++: #if defined(IOS) || defined(TVOS) – on iOS/tvOS DoExit() is NOT called
    //       because iOS/tvOS apps cannot legally exit on their own.
    public void Exit()
    {
        DoExit();
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

    // C++: #if defined(__ANDROID__) || defined(IOS) || defined(TVOS) – on mobile platforms
    //       autoExit_ is forced to true regardless of the argument.
    public void SetAutoExit(bool enable)
    {
        AutoExit = enable;
    }

    public bool GetAutoExit() => AutoExit;

    public void SetNextTimeStep(float seconds)
    {
        _currentTimeStep = Math.Max(seconds, 0.0f);
    }

    public float GetNextTimeStep() => _currentTimeStep;

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
        _currentTimeStep = 0.0f;
        _lastTimeSteps.Clear();
        _timeOut = 0;
        _audioPaused = false;
        _exitRequired = false;
        _frameTimer.Reset();
    }

    public void InitializeVirtualFileSystem(bool enableResourceRootFile)
    {
        var virtualFileSystem = GetSubsystem<VirtualFileSystem>()!;
        virtualFileSystem.UnmountAll();
        virtualFileSystem.MountAliasRoot();
        virtualFileSystem.MountRoot();

        var resourceRootFileName = Parameters.ResourceRootFile;
        var resourceRootFile = OpenResourceRootFile(virtualFileSystem, resourceRootFileName);
        var resourceRootEntries = ReadResourceRootFile(resourceRootFile);

        if (string.IsNullOrEmpty(resourceRootFileName))
            Log.Logger.LogInformation("Resource root file lookup is disabled by the application.");
        else if (resourceRootEntries.Count == 0)
            Log.Logger.LogInformation("Resource root file is not found or invalid.");
        else if (!enableResourceRootFile)
            Log.Logger.LogInformation("Resource root file is found but ignored due to explicitly specified prefix paths.");
        else
            Log.Logger.LogInformation("Resource root file is found and used: {ResourceRootFile}.", resourceRootFile!.Value.FullName);

        var prefixPaths = SplitPathList(Parameters.ResourcePrefixPaths);
        var resourcePaths = new StringVector(SplitPathList(Parameters.ResourcePaths));
        var resourcePackages = new StringVector(SplitPathList(Parameters.ResourcePackages));
        var autoLoadPaths = SplitPathList(Parameters.AutoloadPaths);
        var absolutePrefixPaths = new StringVector();

        if (resourceRootEntries.Count > 0 && enableResourceRootFile)
        {
            foreach (var entry in resourceRootEntries)
            {
                if (!virtualFileSystem.Exists(new FileIdentifier("file", entry.FullName)))
                {
                    Log.Logger.LogError("Resource directory is not found: {ResourceDirectory}.", entry.FullName);
                    continue;
                }

                var mountPoint = virtualFileSystem.MountDir(entry.FullName);
                virtualFileSystem.MountAlias($"res:{entry.ShortName}", mountPoint);
            }
        }
        else
        {
            foreach (var prefixPath in prefixPaths)
                absolutePrefixPaths.Add(NormalizeConfiguredResourcePath(prefixPath) + Path.DirectorySeparatorChar);

            var programDirectory = NormalizeConfiguredResourcePath(AppContext.BaseDirectory) + Path.DirectorySeparatorChar;
            if (!absolutePrefixPaths.Contains(programDirectory, StringComparer.Ordinal))
                absolutePrefixPaths.Add(programDirectory);

            virtualFileSystem.MountExistingDirectoriesOrPackages(absolutePrefixPaths, resourcePaths);
            virtualFileSystem.MountExistingPackages(absolutePrefixPaths, resourcePackages);

            foreach (var autoLoadPath in autoLoadPaths)
            {
                if (Path.IsPathRooted(autoLoadPath))
                {
                    virtualFileSystem.AutomountDir(autoLoadPath);
                }
                else
                {
                    foreach (var prefixPath in absolutePrefixPaths)
                        virtualFileSystem.AutomountDir(prefixPath + autoLoadPath);
                }
            }

            ApplyConfiguredResourcePaths();
        }

        if (!string.IsNullOrEmpty(GetAppPreferencesDir()))
            virtualFileSystem.MountDir("conf", GetAppPreferencesDir());
    }

    public string GetAppPreferencesDir() => Parameters.ApplicationPreferencesDir;

    public void Update()
    {
        if (!IsInitialized)
            throw new InvalidOperationException("Engine must be initialized before sending update events.");
        SendCoreEvents(_currentTimeStep);
    }

    // C++: void Engine::Render()
    // {
    //     if (headless_) return;
    //     auto* graphics = GetSubsystem<Graphics>();
    //     if (!graphics->BeginFrame()) return;
    //     GetSubsystem<Renderer>()->Render();
    //     graphics->ResetRenderTargets();
    //     if (UI* ui = GetSubsystem<UI>())
    //     {
    //         if (!ui->IsRendered() && ui->GetRenderTarget() == nullptr)
    //             ui->Render();
    //     }
    //     graphics->EndFrame();
    // }
    public void Render()
    {
        if (!IsInitialized || IsHeadless)
            return;
        var graphics = GetSubsystem<Graphics>()!;
        var renderer = GetSubsystem<Renderer>()!;
        renderer.RenderFrame(graphics, beforePresent: null);
    }

    /// <summary>Load and merge config files.</summary>
    public void LoadConfigFiles()
    {
        var configName = Parameters.ConfigName;
        if (string.IsNullOrEmpty(configName))
            return;

        _configFile.LoadDefaults(configName, ApplicationFlavor.Platform);
        _configFile.LoadOverrides("conf://" + configName);
        ApplyConfigVariables();
    }

    /// <summary>Save config file.</summary>
    public void SaveConfigFile()
    {
        var configName = Parameters.ConfigName;
        if (string.IsNullOrEmpty(configName))
            return;

        foreach (var property in typeof(EngineParameters).GetProperties(
                     System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public))
        {
            if (property.CanRead && property.CanWrite)
                _configFile.SetVariable(property.Name, Variant.FromObject(property.GetValue(Parameters)));
        }

        _configFile.SaveOverrides("conf://" + configName, ApplicationFlavor.Platform);
    }

    /// <summary>Populate default parameter values.</summary>
    public void PopulateDefaultParameters()
    {
        foreach (var property in typeof(EngineParameters).GetProperties(
                     System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public))
        {
            if (!property.CanRead || !property.CanWrite || property.Name == nameof(EngineParameters.GraphicsBackend))
                continue;

            var definition = _configFile.DefineVariable(
                property.Name, Variant.FromObject(property.GetValue(Parameters)));
            if (property.Name is nameof(EngineParameters.AutoloadPaths)
                or nameof(EngineParameters.ConfigName)
                or nameof(EngineParameters.LogLevel)
                or nameof(EngineParameters.LogName)
                or nameof(EngineParameters.LogQuiet)
                or nameof(EngineParameters.ResourcePackages)
                or nameof(EngineParameters.ResourcePaths)
                or nameof(EngineParameters.ResourcePrefixPaths)
                or nameof(EngineParameters.XR)
                or nameof(EngineParameters.Profile))
            {
                definition.CommandLinePriority();
            }

            if (property.Name is nameof(EngineParameters.Borderless)
                or nameof(EngineParameters.FrameLimiter)
                or nameof(EngineParameters.FullScreen)
                or nameof(EngineParameters.Monitor)
                or nameof(EngineParameters.RefreshRate)
                or nameof(EngineParameters.TextureAnisotropy)
                or nameof(EngineParameters.TextureFilterMode)
                or nameof(EngineParameters.TextureQuality)
                or nameof(EngineParameters.VSync)
                or nameof(EngineParameters.WindowMaximize))
            {
                definition.Overridable();
            }
        }
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

        if (string.IsNullOrEmpty(Parameters.ApplicationPreferencesDir))
        {
            var localApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrEmpty(localApplicationData))
                localApplicationData = AppContext.BaseDirectory;
            Parameters.ApplicationPreferencesDir = Path.Combine(
                localApplicationData, Parameters.OrganizationName, Parameters.ApplicationName);
        }

        InitializeVirtualFileSystem(enableResourceRootFile: true);
        _timeOut = (long)Parameters.TimeOut * 1_000_000L;
    }

    private void ApplyConfigVariables()
    {
        foreach (var (name, value) in _configFile.Variables)
        {
            if (EngineParameterBinding.IsKnown(name))
                EngineParameterBinding.TrySetValue(Parameters, name, value.GetObject());
        }
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

    private static (string FullName, string Text)? OpenResourceRootFile(VirtualFileSystem virtualFileSystem, string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
            return null;

        var programDirectory = NormalizeConfiguredResourcePath(AppContext.BaseDirectory);
        while (!string.IsNullOrEmpty(programDirectory))
        {
            var fullFileName = Path.Combine(programDirectory, fileName);
            var fileIdentifier = new FileIdentifier("file", fullFileName);
            if (virtualFileSystem.Exists(fileIdentifier))
                return (fullFileName, virtualFileSystem.ReadAllText(fileIdentifier));

            var parentDirectory = Directory.GetParent(programDirectory);
            if (parentDirectory is null || string.Equals(parentDirectory.FullName, programDirectory, StringComparison.Ordinal))
                break;
            programDirectory = parentDirectory.FullName;
        }

        return null;
    }

    private static List<ResourceRootEntry> ReadResourceRootFile((string FullName, string Text)? file)
    {
        if (file is null)
            return [];

        var path = Path.GetDirectoryName(file.Value.FullName) ?? string.Empty;
        var lines = file.Value.Text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var result = new List<ResourceRootEntry>();
        var lineIndex = 0;
        foreach (var sourceLine in lines)
        {
            ++lineIndex;
            var line = sourceLine.Trim();
            if (line.Length == 0 || line.StartsWith('#') || line.StartsWith(';'))
                continue;

            var parts = line.Split('=');
            if (parts.Length != 2)
            {
                Log.Logger.LogError("Invalid line #{LineIndex} in {ResourceRootFile}.", lineIndex, file.Value.FullName);
                continue;
            }

            var shortName = parts[0].Trim();
            var fullDirectoryName = Path.Combine(path, parts[1].Trim());
            if (!fullDirectoryName.EndsWith(Path.DirectorySeparatorChar))
                fullDirectoryName += Path.DirectorySeparatorChar;
            result.Add(new ResourceRootEntry(fullDirectoryName, shortName));
        }

        return result;
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

    private void HandleExitRequested()
    {
        if (AutoExit)
            _exitRequired = true;
    }

    private void HandleEndFrame()
    {
        if (_exitRequired)
            DoExit();
    }

    private void DoExit()
    {
        var graphics = GetSubsystem<Graphics>();
        if (graphics is not null)
        {
            graphics.SavePipelineStateCache(FileIdentifier.FromUri(Parameters.PsoCache));
            // C++: graphics->Close() is called unconditionally.
            if (string.IsNullOrWhiteSpace(Parameters.ScreenshotOnExit))
                graphics.Close();
        }

        SaveConfigFile();

        IsExiting = true;
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

    }

    public static void DefineParameters(CommandLineParser commandLine, EngineParameters engineParameters)
    {
        ArgumentNullException.ThrowIfNull(commandLine);
        ArgumentNullException.ThrowIfNull(engineParameters);
        EngineCommandLine.Register(commandLine, engineParameters);
    }

    /// <summary>
    /// Return log file name in the operating system file system.
    /// </summary>
    public string GetLogFileName(string uri)
    {
        var fileIdentifier = FileIdentifier.FromUri(uri);
        return fileIdentifier.Scheme switch
        {
            "file" => fileIdentifier.FileName,
            "conf" => Path.Combine(GetAppPreferencesDir(), fileIdentifier.FileName),
            "" => Path.Combine(AppContext.BaseDirectory, fileIdentifier.FileName),
            _ => string.Empty
        };
    }
}
