using Microsoft.Extensions.Logging;
using System.Collections.Immutable;

namespace RedBolt;

public class Engine : RedObject
{
    private static readonly string ExecutableDirectory = AppContext.BaseDirectory;

    private readonly record struct ResourceRootEntry(string FullName, string ShortName);

    private readonly ConfigFile _configFile = new();
    private string _appPreferencesDir = string.Empty;
    private readonly HiresTimer _frameTimer = new();
    private readonly List<float> _lastTimeSteps = [];
    private float _timeStep;
    private int _timeStepSmoothing = 2;
    private int _minFps = 10;
    private int _maxFps = 200;
    private int _maxInactiveFps = 60;
    private bool _pauseMinimized;
    private long _timeOut;
    private bool _autoExit = true;
    private bool _initialized;
    private bool _exitRequired;
    private bool _exiting;
    private bool _headless;
    private bool _audioPaused;

    private readonly Dictionary<string, object?> _customParameters = new(StringComparer.Ordinal);
    private bool _screenshotCapturedForExit;
    private int _screenshotIndex;
    private int _screenshotFrameDelay;
    private Context _previousContext = Context.Instance;
    private readonly Context _context = new(Context.Instance.RuntimeServices);

    public Engine()
    {
        Directory.SetCurrentDirectory(ExecutableDirectory);
        InitializeContext();
        PopulateDefaultParameters();
    }

    public EngineParameters Parameters { get; private set; } = new();

    public bool IsInitialized
    {
        get => _initialized;
        private set => _initialized = value;
    }

    public bool IsExiting
    {
        get => _exiting;
        private set => _exiting = value;
    }

    public bool IsHeadless
    {
        get => _headless;
        private set => _headless = value;
    }

    public bool AutoExit
    {
        get => _autoExit;
        private set => _autoExit = value;
    }

    public int MaxFps
    {
        get => _maxFps;
        private set => _maxFps = value;
    }

    public int MaxInactiveFps
    {
        get => _maxInactiveFps;
        private set => _maxInactiveFps = value;
    }

    public int MinFps
    {
        get => _minFps;
        private set => _minFps = value;
    }

    public bool PauseMinimized
    {
        get => _pauseMinimized;
        private set => _pauseMinimized = value;
    }

    public int TimeStepSmoothing
    {
        get => _timeStepSmoothing;
        private set => _timeStepSmoothing = value;
    }

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
        var resources = GetSubsystem<ResourceCache>()!;
        var input = GetSubsystem<Input>()!;
        var graphics = GetSubsystem<Graphics>()!;
        var renderer = GetSubsystem<Renderer>()!;

        resources.RegisterDefaultResources();
        ApplyInitializationParameters();

        if (!IsHeadless)
        {
            var vulkanSettings = new RenderDeviceSettingsVulkan();
            try
            {
                using var jsonFile = new JSONFile();
                if (!jsonFile.FromString(Parameters.TweakVulkan))
                    throw new ArchiveException("Failed to parse JSON string");
                using var archive = new JSONInputArchive(jsonFile);
                RenderAPIUtils.SerializeValue(archive, "object", ref vulkanSettings);
                archive.Flush();
            }
            catch (ArchiveException)
            {
                vulkanSettings = new RenderDeviceSettingsVulkan();
            }

            var d3D12Settings = new RenderDeviceSettingsD3D12();
            try
            {
                using var jsonFile = new JSONFile();
                if (!jsonFile.FromString(Parameters.TweakD3D12))
                    throw new ArchiveException("Failed to parse JSON string");
                using var archive = new JSONInputArchive(jsonFile);
                RenderAPIUtils.SerializeValue(archive, "object", ref d3D12Settings);
                archive.Flush();
            }
            catch (ArchiveException)
            {
                d3D12Settings = new RenderDeviceSettingsD3D12();
            }

            var deviceSettings = new RenderDeviceSettings
            {
                Backend = RenderAPIUtils.SelectRenderBackend(Parameters.RenderBackend),
                ExternalWindowHandle = Parameters.ExternalWindow,
                GpuDebug = Parameters.GPUDebug,
                AdapterId = Parameters.RenderAdapterId.HasValue
                    ? checked((int)Parameters.RenderAdapterId.Value)
                    : null,
                Vulkan = vulkanSettings,
                D3D12 = d3D12Settings
            };
            var requestedShaderPolicy = Parameters.ShaderPolicy switch
            {
                nameof(ShaderTranslationPolicy.Translate) => ShaderTranslationPolicy.Translate,
                nameof(ShaderTranslationPolicy.Optimize) => ShaderTranslationPolicy.Optimize,
                nameof(ShaderTranslationPolicy.Verbatim) => ShaderTranslationPolicy.Verbatim,
                _ => (ShaderTranslationPolicy?)null
            };
            var graphicsSettings = new GraphicsSettings
            {
                RenderDevice = deviceSettings,
                ShaderTranslationPolicy = RenderAPIUtils.SelectShaderTranslationPolicy(
                    deviceSettings.Backend, requestedShaderPolicy),
                ShaderCacheDir = FileIdentifier.FromUri(Parameters.ShaderCacheDir),
                LogShaderSources = Parameters.ShaderLogSources,
                ValidateShaders = Parameters.ValidateShaders,
                DiscardShaderCache = Parameters.DiscardShaderCache,
                CacheShaders = Parameters.SaveShaderCache
            };
            graphics.Configure(graphicsSettings);

            var windowSettings = new WindowSettings
            {
                Mode = ProcessUtils.GetPlatform() == PlatformId.Web
                    ? WindowMode.Windowed
                    : Parameters.FullScreen
                        ? WindowMode.Fullscreen
                        : Parameters.Borderless
                            ? WindowMode.Borderless
                            : WindowMode.Windowed,
                Size = new Vector2i(Parameters.WindowWidth, Parameters.WindowHeight),
                Title = Parameters.WindowTitle,
                Resizable = Parameters.WindowResizable,
                Monitor = Parameters.Monitor,
                VSync = Parameters.VSync,
                RefreshRate = Parameters.RefreshRate,
                MultiSample = Parameters.MultiSample,
                SRgb = true,
                Orientations = Parameters.Orientations.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .ToImmutableArray()
            };

            input.Initialize(Parameters);
            try
            {
                graphics.Initialize(Parameters, graphicsSettings.RenderDevice, windowSettings, input);
                graphics.InitializePipelineStateCache(FileIdentifier.FromUri(Parameters.PsoCache));
            }
            catch (Exception exception)
            {
                Log.GetLogger().LogError(exception, "Graphics initialization failed.");
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
        _screenshotCapturedForExit = false;
        _screenshotIndex = 0;
        _screenshotFrameDelay = 0;
        _timeStep = 0.0f;
        _lastTimeSteps.Clear();
        _frameTimer.Reset();
        Log.GetLogger().LogInformation("Initialized engine");
        return true;
    }

    public void RunFrame() => RunFrame(static _ => { });

    public void RunFrame(Action<float> update)
    {
        ArgumentNullException.ThrowIfNull(update);

        if (!IsInitialized)
            throw new InvalidOperationException("Engine must be initialized before running frames.");
        if (IsExiting)
            return;

        var time = GetSubsystem<Time>()!;
        var input = GetSubsystem<Input>()!;
        var graphics = GetSubsystem<Graphics>()!;
        GetSubsystem<WorkQueue>()!.Update();

        if (!IsHeadless && !graphics.IsInitialized)
        {
            IsExiting = true;
            return;
        }

        var timeStep = _timeStep;
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
                SendCoreEvents(timeStep, update);
            }

            if (!IsHeadless)
            {
                Render();
            }
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
        Log.GetLogger().LogInformation(
            "Web canvas resized to {Width}x{Height}{Fullscreen} with DPI scale={DpiScale}.",
            width, height, isFullScreen ? " FullScreen" : " ", dpiScale);

        var input = GetSubsystem<Input>();
        var graphics = GetSubsystem<Graphics>();
        var mouseVisible = input?.MouseVisible ?? false;
        var mouseMode = input?.GetMouseMode() ?? MouseMode.Absolute;

        graphics?.SetMode(width, height);

        if (input is not null)
        {
            input.SetMouseVisible(mouseVisible);
            input.SetMouseMode(mouseMode);
        }
    }

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

    public void SetAutoExit(bool enable)
    {
        if (OperatingSystem.IsAndroid() || OperatingSystem.IsIOS() || OperatingSystem.IsTvOS())
            enable = true;
        AutoExit = enable;
    }

    public bool GetAutoExit() => AutoExit;

    public void SetNextTimeStep(float seconds)
    {
        _timeStep = Math.Max(seconds, 0.0f);
    }

    public float GetNextTimeStep() => _timeStep;

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
        Log.GetLogger().LogInformation("Profiler data is not available in the managed runtime.");
    }

    public void DumpResources(bool dumpFileName = false)
    {
        var resources = GetSubsystem<ResourceCache>()!;
        Log.GetLogger().LogInformation("{ResourceUsage}", dumpFileName ? resources.PrintResources() : resources.PrintMemoryUsage());
    }

    public void DumpMemory()
    {
        Log.GetLogger().LogInformation("Managed memory: {MemoryBytes} bytes", GC.GetTotalMemory(forceFullCollection: false));
    }

    public void Shutdown()
    {
        if (IsInitialized)
        {
            GetSubsystem<Renderer>()!.SetNumViewports(0);
            GetSubsystem<Graphics>()!.Close();
            GetSubsystem<Input>()!.Shutdown();
        }
        _configFile.Dispose();
        _context.Reset();
        if (ReferenceEquals(RedBolt.Context.Instance, _context))
            RedBolt.Context.SetInstance(_previousContext);
        IsInitialized = false;
        IsExiting = false;
        IsHeadless = false;
        _timeStep = 0.0f;
        _lastTimeSteps.Clear();
        _timeOut = 0;
        _audioPaused = false;
        _exitRequired = false;
        _screenshotCapturedForExit = false;
        _screenshotIndex = 0;
        _screenshotFrameDelay = 0;
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
            Log.GetLogger().LogInformation("Resource root file lookup is disabled by the application.");
        else if (resourceRootEntries.Count == 0)
            Log.GetLogger().LogInformation("Resource root file is not found or invalid.");
        else if (!enableResourceRootFile)
            Log.GetLogger().LogInformation("Resource root file is found but ignored due to explicitly specified prefix paths.");
        else
            Log.GetLogger().LogInformation("Resource root file is found and used: {ResourceRootFile}.", resourceRootFile!.Value.FullName);

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
                    Log.GetLogger().LogError("Resource directory is not found: {ResourceDirectory}.", entry.FullName);
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

            var programDirectory = NormalizeConfiguredResourcePath(ExecutableDirectory) + Path.DirectorySeparatorChar;
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

        if (ProcessUtils.GetPlatform() == PlatformId.Web)
            virtualFileSystem.MountDir("conf", "/IndexedDB/");
        else if (!string.IsNullOrEmpty(GetAppPreferencesDir()))
            virtualFileSystem.MountDir("conf", GetAppPreferencesDir());
    }

    public string GetAppPreferencesDir() => _appPreferencesDir;

    public void Update()
    {
        if (!IsInitialized)
            throw new InvalidOperationException("Engine must be initialized before sending update events.");
        SendCoreEvents(_timeStep);
    }

    public void Render()
    {
        if (!IsInitialized || IsHeadless)
            return;
        var graphics = GetSubsystem<Graphics>()!;
        if (!graphics.BeginFrame())
            return;

        GetSubsystem<Renderer>()!.Render();

        graphics.ResetRenderTargets();
        graphics.EndFrame();
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
            if (!property.CanRead || !property.CanWrite)
                continue;

            var definition = _configFile.DefineVariable(
                property.Name, Variant.FromObject(property.GetValue(Parameters)));
            if (property.Name is nameof(EngineParameters.AutoloadPaths)
                or nameof(EngineParameters.LogLevel)
                or nameof(EngineParameters.LogName)
                or nameof(EngineParameters.LogQuiet)
                or nameof(EngineParameters.LogRotateOnOpen)
                or nameof(EngineParameters.LogRotateBySize)
                or nameof(EngineParameters.LogMaxFiles)
                or nameof(EngineParameters.LogMaxSize)
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

        Input.RegisterInputLibrary(_context);
        Graphics.RegisterGraphicsLibrary(_context);
        Scene.RegisterSceneLibrary(_context);

        // Register render pipeline.
        // Extract this code into function if you are adding more.
        RenderPipeline.RegisterObject(_context);
        RenderPath.RegisterObject(_context);
        RenderPass.RegisterObject(_context);
        AmbientOcclusionPass.RegisterObject(_context);
        AutoExposurePass.RegisterObject(_context);
        BloomPass.RegisterObject(_context);
        FullScreenShaderPass.RegisterObject(_context);
        OutlineRenderPass.RegisterObject(_context);
        ToneMappingPass.RegisterObject(_context);

        GeneratedReflectionRegistration.RefreshExternalAssemblies(_context);
    }

    private void ApplyInitializationParameters()
    {
        _appPreferencesDir = Parameters.ApplicationPreferencesDir;
        if (string.IsNullOrEmpty(_appPreferencesDir))
        {
            var localApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrEmpty(localApplicationData))
                localApplicationData = ExecutableDirectory;
            _appPreferencesDir = Path.Combine(
                localApplicationData, Parameters.OrganizationName, Parameters.ApplicationName)
                + Path.DirectorySeparatorChar;
        }

        var log = GetSubsystem<Log>()!;
        log.SetLevel(Parameters.LogLevel);
        log.SetQuiet(Parameters.LogQuiet);
        var logFileName = GetLogFileName(Parameters.LogName);
        if (!string.IsNullOrEmpty(logFileName))
        {
            var logFileParams = new LogFileParams
            {
                RotateOnOpen = Parameters.LogRotateOnOpen,
                RotateBySize = Parameters.LogRotateBySize,
                MaxFiles = Parameters.LogMaxFiles,
                MaxSize = Parameters.LogMaxSize,
            };
            log.Open(logFileName, logFileParams);
        }

        if (!string.IsNullOrEmpty(_appPreferencesDir))
            Directory.CreateDirectory(_appPreferencesDir);

        InitializeVirtualFileSystem(enableResourceRootFile: true);

        LoadConfigFiles();

        IsHeadless = Parameters.Headless;
        if (!Parameters.FrameLimiter)
            MaxFps = 0;

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

        var programDirectory = NormalizeConfiguredResourcePath(ExecutableDirectory);
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
                Log.GetLogger().LogError("Invalid line #{LineIndex} in {ResourceRootFile}.", lineIndex, file.Value.FullName);
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

    private void SendCoreEvents(float timeStep, Action<float>? update = null)
    {
        var inputReadyEventData = new CoreEvents.InputReadyEvent
        {
            TimeStep = timeStep
        };
        SendEvent(ref inputReadyEventData);

        var updateEventData = new CoreEvents.UpdateEvent
        {
            TimeStep = timeStep
        };
        SendEvent(ref updateEventData);
        update?.Invoke(timeStep);

        var postUpdateEventData = new CoreEvents.PostUpdateEvent
        {
            TimeStep = timeStep
        };
        SendEvent(ref postUpdateEventData);

        var renderUpdateEventData = new CoreEvents.RenderUpdateEvent
        {
            TimeStep = timeStep
        };
        SendEvent(ref renderUpdateEventData);

        var postRenderUpdateEventData = new CoreEvents.PostRenderUpdateEvent
        {
            TimeStep = timeStep
        };
        SendEvent(ref postRenderUpdateEventData);
    }

    private void HandleExitRequested()
    {
        if (AutoExit)
            _exitRequired = true;
    }

    private void HandleEndFrame()
    {
        var screenshotPath = _exitRequired && !_screenshotCapturedForExit ? Parameters.ScreenshotOnExit : null;
        if (!IsHeadless && !string.IsNullOrWhiteSpace(screenshotPath) && _screenshotFrameDelay-- <= 0)
        {
            using var screenshot = new Image();
            if (GetSubsystem<Graphics>()!.TakeScreenShot(screenshot)
                && screenshot.SaveFile(new FileIdentifier(GetScreenshotPath(screenshotPath))))
            {
                ++_screenshotIndex;
                _screenshotCapturedForExit = _screenshotIndex >= Parameters.ScreenshotCount;
                _screenshotFrameDelay = Parameters.ScreenshotFrameDelay;
            }
        }

        if (_exitRequired && (IsHeadless || string.IsNullOrWhiteSpace(Parameters.ScreenshotOnExit) || _screenshotCapturedForExit))
            DoExit();
    }

    private string GetScreenshotPath(string screenshotPath)
    {
        if (Parameters.ScreenshotCount == 1)
            return screenshotPath;

        var directoryName = Path.GetDirectoryName(screenshotPath);
        var fileName = $"{Path.GetFileNameWithoutExtension(screenshotPath)}-{_screenshotIndex + 1:D4}{Path.GetExtension(screenshotPath)}";
        return string.IsNullOrEmpty(directoryName) ? fileName : Path.Combine(directoryName, fileName);
    }

    private void DoExit()
    {
        var graphics = GetSubsystem<Graphics>();
        if (graphics is not null)
        {
            graphics.SavePipelineStateCache(FileIdentifier.FromUri(Parameters.PsoCache));
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
            : Path.GetFullPath(Path.Combine(ExecutableDirectory, path));
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
            {
                if (!IsHeadless && !string.IsNullOrWhiteSpace(Parameters.ScreenshotOnExit))
                    _exitRequired = true;
                else
                    Exit();
            }
        }

        if (MinFps > 0)
        {
            var targetMin = 1_000_000L / MinFps;
            if (elapsed > targetMin)
                elapsed = targetMin;
        }

        _timeStep = 0.0f;
        _lastTimeSteps.Add(elapsed / 1_000_000.0f);
        if (_lastTimeSteps.Count > TimeStepSmoothing)
        {
            _lastTimeSteps.RemoveRange(0, TimeStepSmoothing);
            foreach (var value in _lastTimeSteps)
                _timeStep += value;
            _timeStep /= _lastTimeSteps.Count;
        }
        else
            _timeStep = _lastTimeSteps[^1];

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
            "conf" => GetAppPreferencesDir() + fileIdentifier.FileName,
            "" => Path.Combine(ExecutableDirectory, fileIdentifier.FileName),
            _ => string.Empty
        };
    }
}
