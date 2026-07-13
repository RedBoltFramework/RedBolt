namespace RedBolt;

public sealed class EngineParameters
{
    public string ApplicationName { get; set; } = "Unspecified Application";

    public string ApplicationPreferencesDir { get; set; } = string.Empty;

    public string AutoloadPaths { get; set; } = "Autoload";

    public bool Borderless { get; set; } = true;

    public string ConfigName { get; set; } = "EngineParameters.json";

    public bool DiscardShaderCache { get; set; }

    public bool EngineAutoLoadScripts { get; set; }

    public bool EngineCliParameters { get; set; } = true;

    public nint ExternalWindow { get; set; }

    public bool FrameLimiter { get; set; } = true;

    public bool FullScreen { get; set; }

    public bool GPUDebug { get; set; }

    public bool Headless { get; set; }

    public bool LoadFonts { get; set; } = true;

    public string LogLevel { get; set; } = "trace";

    public string LogName { get; set; } = "conf://Urho3D.log";

    public bool LogQuiet { get; set; }

    public string MainPlugin { get; set; } = string.Empty;

    public int Monitor { get; set; }

    public int MultiSample { get; set; } = 1;

    public string OrganizationName { get; set; } = "Urho3D Rebel Fork";

    public string Orientations { get; set; } = "LandscapeLeft LandscapeRight";

    public string PackageCacheDir { get; set; } = string.Empty;

    public string Plugins { get; set; } = string.Empty;

    public bool Profile { get; set; }

    public string PsoCache { get; set; } = "conf://psocache.bin";

    public int RefreshRate { get; set; }

    public bool ReloadPlugins { get; set; }

    public uint? RenderAdapterId { get; set; }

    public GraphicsBackend RenderBackend { get; set; } = GraphicsBackend.Auto;

    public string ResourcePackages { get; set; } = string.Empty;

    public string ResourcePaths { get; set; } = "CoreData;Cache;Data";

    public string ResourcePrefixPaths { get; set; } = string.Empty;

    public string ResourceRootFile { get; set; } = "ResourceRoot.ini";

    public bool SaveShaderCache { get; set; } = true;

    public string? ScreenshotOnExit { get; set; }

    public string ShaderCacheDir { get; set; } = "conf://ShaderCache";

    public bool ShaderLogSources { get; set; }

    public string? ShaderPolicy { get; set; }

    public bool Sound { get; set; } = true;

    public int SoundBuffer { get; set; } = 100;

    public bool SoundInterpolation { get; set; } = true;

    public int SoundMixRate { get; set; } = 44_100;

    public int SoundMode { get; set; }

    public uint DearImGuiFlags { get; set; }

    public int TextureAnisotropy { get; set; } = 4;

    public TextureFilterMode TextureFilterMode { get; set; } = TextureFilterMode.Trilinear;

    public MaterialQuality TextureQuality { get; set; } = MaterialQuality.High;

    public int TimeOut { get; set; }

    public bool TouchEmulation { get; set; }

    public string TweakD3D12 { get; set; } = "{\"gpuDescriptorHeapSize_cvb_srv_uav\":32768,\"gpuDescriptorHeapDynamicSize_cvb_srv_uav\":24576}";

    public string TweakVulkan { get; set; } = "{\"dynamicHeapSize\":33554432}";

    public bool ValidateShaders { get; set; }

    public bool VSync { get; set; }

    public int WindowHeight { get; set; }

    public string WindowIcon { get; set; } = string.Empty;

    public bool WindowMaximize { get; set; } = true;

    public int? WindowPositionX { get; set; } = 0;

    public int? WindowPositionY { get; set; } = 0;

    public bool WindowResizable { get; set; }

    public string WindowTitle { get; set; } = "Urho3D";

    public int WindowWidth { get; set; }

    public bool WorkerThreads { get; set; } = true;

    public bool XR { get; set; }

    public GraphicsBackend GraphicsBackend
    {
        get => RenderBackend;
        set => RenderBackend = value;
    }

    internal EngineParameters Clone() => (EngineParameters)MemberwiseClone();
}
