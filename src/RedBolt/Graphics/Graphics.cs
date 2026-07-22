using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using RedBolt.Bindings.Diligent;
using RedBolt.Bindings.Sdl;

using Microsoft.Extensions.Logging;

namespace RedBolt;

    /// <summary>
    /// Graphics subsystem. Manages the application window, rendering state and GPU resources.
    /// </summary>
public sealed class Graphics : Object
    {
    /// <summary>Register Graphics library objects.</summary>
    public static void RegisterGraphicsLibrary(Context context)
    {
        Animation.RegisterObject(context);
        Material.RegisterObject(context);
        Model.RegisterObject(context);
        Shader.RegisterObject(context);
        Technique.RegisterObject(context);
        Texture2D.RegisterObject(context);
        Texture2DArray.RegisterObject(context);
        Texture3D.RegisterObject(context);
        TextureCube.RegisterObject(context);
        Camera.RegisterObject(context);
        CameraOperator.RegisterObject(context);
        Drawable.RegisterObject(context);
        Light.RegisterObject(context);
        LightBaker.RegisterObject(context);
        LightProbeGroup.RegisterObject(context);
        GlobalIllumination.RegisterObject(context);
        StaticModel.RegisterObject(context);
        StaticModelGroup.RegisterObject(context);
        Skybox.RegisterObject(context);
        AnimatedModel.RegisterObject(context);
        AnimationController.RegisterObject(context);
        BillboardSet.RegisterObject(context);
        ParticleEffect.RegisterObject(context);
        ParticleEmitter.RegisterObject(context);
        RibbonTrail.RegisterObject(context);
        CustomGeometry.RegisterObject(context);
        DecalSet.RegisterObject(context);
        Terrain.RegisterObject(context);
        TerrainPatch.RegisterObject(context);
        DebugRenderer.RegisterObject(context);
        Octree.RegisterObject(context);
        OutlineGroup.RegisterObject(context);
        Zone.RegisterObject(context);
        Geometry.RegisterObject(context);
        Viewport.RegisterObject(context);
        OcclusionBuffer.RegisterObject(context);
        ReflectionProbe.RegisterObject(context);
        ReflectionProbeManager.RegisterObject(context);
    }

    private const uint SdlInitVideo = 0x00000020u;
    private const int SdlWindowposUndefined = 0x1FFF0000;
    private readonly Context _ownerContext;
    private static uint _maxBonesHWSkinned;

    private const int CameraBufferSize = 368;
    private const int FrameBufferSize = 16;
    private const int ObjectBufferSize = 80;
    private const int MaterialBufferSize = 128;
    private const int LightBufferSize = 48;
    private const int ZoneBufferSize = 16;
    private const string ShaderExtraCode = """
#define URHO3D_CUSTOM_FRAME_UNIFORMS
#define URHO3D_CUSTOM_MATERIAL_UNIFORMS
#define URHO3D_CUSTOM_LIGHT_UNIFORMS

layout(std140) uniform Light
{
    vec4 uLightPos;
    vec4 uLightDir;
    vec4 uLightColor;
};

#define cLightPos uLightPos
#define cLightDir uLightDir.xyz
#define cLightColor uLightColor

layout(std140) uniform Material
{
    vec4 uUOffset;
    vec4 uVOffset;
    vec4 uMatDiffColor;
    vec4 uMatSpecColor;
    vec4 uMatEmissiveRoughness;
    vec4 uMatEnvMapMetallic;
    vec4 uMaterialMisc;
    vec4 uFadeOffsetScale;
};

#define cUOffset uUOffset
#define cVOffset uVOffset
#define cMatDiffColor uMatDiffColor
#define cMatSpecColor uMatSpecColor
#define cMatEmissiveColor uMatEmissiveRoughness.xyz
#define cRoughness uMatEmissiveRoughness.w
#define cMatEnvMapColor uMatEnvMapMetallic.xyz
#define cMetallic uMatEnvMapMetallic.w
#define cNormalScale uMaterialMisc.x
#define cDielectricReflectance uMaterialMisc.y
#define cAlphaCutoff uMaterialMisc.z
#define cLMOffset vec4(1.0, 1.0, 0.0, 0.0)
#define cFadeOffsetScale uFadeOffsetScale.xy
""";

    private static readonly Vector4f DefaultAmbient = new(0.0f, 0.0f, 0.0f, 1.0f);
    private static readonly Vector4f DefaultFogColor = new(0.03f, 0.05f, 0.09f, 1.0f);
    private static readonly byte[] DefaultAlbedoTextureData = [255, 255, 255, 255];
    private static readonly byte[] DefaultNormalTextureData = [128, 128, 255, 255];
    private static readonly byte[] DefaultPropertiesTextureData = [255, 255, 255, 255];
    private static readonly byte[] DefaultEmissionTextureData = [0, 0, 0, 255];
    private static readonly StringHash AlbedoTextureSlot = new("Albedo");
    private static readonly StringHash NormalTextureSlot = new("Normal");
    private static readonly StringHash PropertiesTextureSlot = new("Properties");
    private static readonly StringHash EmissionTextureSlot = new("Emission");
    private static readonly string[] OutlinePassNames = ["deferred", "deferred_decal", "base", "alpha"];
    private static readonly string[] NormalBufferPassNames = ["base", "alpha", "litbase", "litalpha"];
    private const string NormalBufferShaderName = "M_NormalBuffer";
    private const string NormalBufferShaderDefines = "URHO3D_SURFACE_NEED_NORMAL URHO3D_PIXEL_NEED_TEXCOORD";
    private static readonly IReadOnlyDictionary<string, object?> EmptyFullscreenParameters = new Dictionary<string, object?>(0, StringComparer.Ordinal);
    private static readonly bool RenderDiagnosticsEnabled = IsRenderDiagnosticsEnabled();
    private static readonly bool ShaderBindingDiagnosticsEnabled = IsShaderBindingDiagnosticsEnabled();
    private static readonly bool ShaderReflectionDiagnosticsEnabled = IsShaderReflectionDiagnosticsEnabled();

    private readonly float[] _clearColor = [0.0f, 0.0f, 0.0f, 1.0f];
    private readonly ITextureView?[] _renderTargets = new ITextureView?[RenderApiLimits.MaxRenderTargets];
    private readonly RenderTargetView[] _singleRenderTargetView = new RenderTargetView[1];
    private readonly IBuffer?[] _singleVertexBuffer = new IBuffer?[1];
    private readonly ulong[] _singleVertexBufferOffset = new ulong[1];
    private readonly Dictionary<Geometry, UploadedGeometry> _uploadedGeometries = new();
    private readonly Dictionary<Texture, UploadedTexture> _uploadedTextures = new();
    private readonly Dictionary<PipelineKey, PipelineResources> _pipelines = new();
    private readonly Dictionary<FullscreenPipelineKey, FullscreenPipelineResources> _fullscreenPipelines = new();
    private readonly Dictionary<ClearFramebufferPipelineKey, FullscreenPipelineResources> _clearFramebufferPipelines = new();
    private readonly Dictionary<string, string> _fullscreenShaderDefinesCache = new(StringComparer.Ordinal);
    private readonly Dictionary<ShaderDefinesKey, string> _shaderDefinesCache = new();
    private readonly Dictionary<Pass, Pass> _normalBufferPasses = new();
    private readonly List<Light> _lightsScratch = [];
    private readonly List<Zone> _zonesScratch = [];

    private GCHandle _clearColorHandle;
    private Texture2D? _defaultAlbedoTexture;
    private Texture2D? _defaultNormalTexture;
    private Texture2D? _defaultPropertiesTexture;
    private Texture2D? _defaultEmissionTexture;
    private IDeviceContext? _immediateContext;
    private IEngineFactory? _factory;
    private IRenderDevice? _device;
    private ISwapChain? _swapChain;
#if REDBOLT_VULKAN_SUPPORTED
    private ISwapChainVk? _swapChainVulkan;
#endif
    private global::RedBolt.Bindings.Diligent.Viewport? _diligentViewport;
    private RenderOutputTargetInfo? _swapChainOutputTarget;
    private ITextureView? _renderTargetView;
    private ITextureView? _depthStencilView;
    private ITextureView? _currentDepthStencilTarget;
    private int _currentRenderTargetCount;
    private int _configuredMultiSample = 1;
    private global::RedBolt.Bindings.Diligent.TextureFormat _currentColorTargetFormat;
    private global::RedBolt.Bindings.Diligent.TextureFormat _currentDepthTargetFormat;
    private global::RedBolt.Bindings.Diligent.TextureFormat _swapChainColorFormat;
    private global::RedBolt.Bindings.Diligent.TextureFormat _swapChainDepthFormat;
    private PipelineStateOutputDesc _currentOutputDesc = new();
    private IntRect _currentViewport;
    private Vector2i _currentRenderTargetSize;
    private bool _isSwapChainRenderTarget;
    private IntPtr _glContext;
    private bool _shaderReflectionDiagnosticsDumped;
    private SdlWindow? _window;
    private string _windowTitle = string.Empty;
    private Vector2i _windowPosition;
    private Image? _windowIcon;
    private WindowSettings _primaryWindowSettings = new();
    private WindowSettings _secondaryWindowSettings = new();
    private Shader? _lastShader;
    private string _lastShaderName = string.Empty;

    public Graphics()
    {
        _ownerContext = Context.Instance;
        _windowPosition = new Vector2i(SdlWindowposUndefined, SdlWindowposUndefined);
        _ownerContext.RequireSdl(SdlInitVideo);
    }

    public override void Dispose()
    {
        Close();
        _ownerContext.ReleaseSdl();
        base.Dispose();
    }

    public bool IsInitialized { get; private set; }

    public int Width { get; private set; }

    public int Height { get; private set; }

    public WindowSettings GetWindowSettings()
    {
        var renderDevice = GetSubsystem<RenderDevice>();
        return renderDevice?.GetWindowSettings() ?? _primaryWindowSettings;
    }

    public string GetWindowTitle()
    {
        if (_window is null || _window.IsNull)
            return _windowTitle;

        var title = SdlNativeBindings.SdlGetWindowTitle(_window);
        var length = title.IndexOf((byte)0);
        return Encoding.UTF8.GetString(length < 0 ? title : title[..length]);
    }

    public void SetWindowTitle(string windowTitle)
    {
        _windowTitle = windowTitle;
        if (_window is not null && !_window.IsNull)
        {
            using var title = new CString(windowTitle);
            SdlNativeBindings.SdlSetWindowTitle(_window, title.Bytes);
        }
    }

    public Vector2i GetWindowPosition()
    {
        if (_window is not null && !_window.IsNull)
        {
            SdlNativeBindings.SdlGetWindowPosition(_window, out var x, out var y);
            return new Vector2i(x, y);
        }

        return _windowPosition;
    }

    public void SetWindowPosition(Vector2i position)
    {
        if (_window is not null && !_window.IsNull)
            SdlNativeBindings.SdlSetWindowPosition(_window, position.X, position.Y);
        else
            _windowPosition = position;
    }

    public void SetWindowPosition(int x, int y) => SetWindowPosition(new Vector2i(x, y));

    /// <summary>
    /// Set window icon.
    /// </summary>
    public void SetWindowIcon(Image? windowIcon)
    {
        _windowIcon = windowIcon;
        if (_window is not null && !_window.IsNull)
            CreateWindowIcon();
    }

    private void CreateWindowIcon()
    {
        if (_windowIcon is null)
            return;

        var rgba = _windowIcon.Components == 4 ? _windowIcon : _windowIcon.ConvertToRGBA();
        var data = rgba.GetData();
        var handle = GCHandle.Alloc(data, GCHandleType.Pinned);
        try
        {
            var surface = SdlNativeBindings.SdlCreateRGBSurfaceWithFormatFrom(
                handle.AddrOfPinnedObject(), rgba.Width, rgba.Height, 32, rgba.Width * 4,
                (uint)SdlPixelFormatEnum.SdlPixelformatRgba32);
            if (surface.IsNull)
                return;

            try
            {
                SdlNativeBindings.SdlSetWindowIcon(_window!, surface);
            }
            finally
            {
                SdlNativeBindings.SdlFreeSurface(surface);
            }
        }
        finally
        {
            handle.Free();
        }
    }

    /// <summary>
    /// Set screen mode.
    /// </summary>
    public bool SetScreenMode(WindowSettings windowSettings)
    {
        var renderDevice = GetSubsystem<RenderDevice>();
        if (renderDevice is null)
            return false;

        renderDevice.UpdateWindowSettings(windowSettings);
        SetWindowTitle(_windowTitle);
        SetWindowIcon(_windowIcon);

        var renderContext = renderDevice.GetRenderContext();
        renderContext.SetSwapChainRenderTargets();
        renderContext.ClearRenderTarget(0, Color.Black);
        renderDevice.Present();

        OnScreenModeChanged();
        return true;
    }

    private void OnScreenModeChanged()
    {
        var renderDevice = GetSubsystem<RenderDevice>()!;
        var currentSettings = renderDevice.GetWindowSettings();
        Log.GetLogger().LogInformation(
            "Set screen mode: {Width}x{Height} pixels at {RefreshRate} Hz at monitor {Monitor} [{Mode}]{Resizable}{MultiSample}",
            Width,
            Height,
            currentSettings.RefreshRate,
            currentSettings.Monitor,
            currentSettings.Mode,
            currentSettings.Resizable ? " [Resizable]" : "",
            GetMultiSample() > 1 ? $" [{GetMultiSample()}x MSAA]" : "");
        var screenModeEvent = new GraphicsEvents.ScreenModeEvent
        {
            Width = currentSettings.Size.X,
            Height = currentSettings.Size.Y,
            Fullscreen = currentSettings.Mode == WindowMode.Fullscreen,
            Borderless = currentSettings.Mode == WindowMode.Borderless,
            Resizable = currentSettings.Resizable,
            Monitor = currentSettings.Monitor,
            RefreshRate = currentSettings.RefreshRate
        };
        SendEvent(ref screenModeEvent);
    }

    /// <summary>
    /// Set window modes to be rotated by <see cref="ToggleFullscreen"/>.
    /// </summary>
    public bool SetWindowModes(WindowSettings primarySettings, WindowSettings secondarySettings)
    {
        _primaryWindowSettings = primarySettings;
        _secondaryWindowSettings = secondarySettings;
        return SetScreenMode(_primaryWindowSettings);
    }

    /// <summary>
    /// Set default window modes.
    /// </summary>
    public bool SetDefaultWindowModes(WindowSettings commonSettings)
    {
        var primaryWindowSettings = commonSettings;
        var secondaryWindowSettings = commonSettings;
        secondaryWindowSettings.Size = Vector2i.Zero;
        secondaryWindowSettings.Mode = primaryWindowSettings.Mode == WindowMode.Windowed
            ? WindowMode.Borderless
            : WindowMode.Windowed;

        return SetWindowModes(primaryWindowSettings, secondaryWindowSettings);
    }

    /// <summary>
    /// Set screen resolution and window mode.
    /// </summary>
    public bool SetMode(int width, int height, bool fullscreen, bool borderless, bool resizable,
        bool highDPI, bool vsync, bool tripleBuffer, int multiSample, int monitor, int refreshRate)
    {
        var parameters = new WindowSettings
        {
            Size = new Vector2i(width, height),
            Mode = fullscreen ? WindowMode.Fullscreen : borderless ? WindowMode.Borderless : WindowMode.Windowed,
            Resizable = resizable,
            VSync = vsync,
            MultiSample = multiSample,
            Monitor = monitor,
            RefreshRate = refreshRate
        };

        return SetDefaultWindowModes(parameters);
    }

    /// <summary>
    /// Set screen resolution only.
    /// </summary>
    public bool SetMode(int width, int height)
    {
        var parameters = GetWindowSettings();
        parameters.Size = new Vector2i(width, height);
        return SetDefaultWindowModes(parameters);
    }

    /// <summary>
    /// Save pipeline state cache.
    /// </summary>
    public void SavePipelineStateCache(FileIdentifier fileName)
    {
        if (fileName.IsEmpty())
            return;

        var pipelineStateCache = GetSubsystem<PipelineStateCache>();
        if (pipelineStateCache is null)
            return;

        var cachedData = pipelineStateCache.GetCachedData();
        var virtualFileSystem = GetSubsystem<VirtualFileSystem>()!;
        var file = virtualFileSystem.OpenFile(fileName, FileMode.Write);
        if (file is null)
            return;

        try
        {
            file.Write(cachedData);
        }
        finally
        {
            file.Close();
        }
    }

    /// <summary>
    /// Initialize pipeline state cache.
    /// </summary>
    public void InitializePipelineStateCache(FileIdentifier fileName)
    {
        var pipelineStateCache = GetSubsystem<PipelineStateCache>();
        if (pipelineStateCache is null)
            return;

        var cachedData = Array.Empty<byte>();
        var virtualFileSystem = GetSubsystem<VirtualFileSystem>();
        if (!fileName.IsEmpty() && virtualFileSystem?.Exists(fileName) == true)
        {
            var file = virtualFileSystem.OpenFile(fileName, FileMode.Read);
            if (file is not null)
            {
                try
                {
                    cachedData = new byte[checked((int)file.Size)];
                    _ = file.Read(cachedData);
                }
                finally
                {
                    file.Close();
                }
            }
        }

        pipelineStateCache.Initialize(cachedData);
    }

    /// <summary>
    /// Toggle between full screen and windowed mode.
    /// </summary>
    public bool ToggleFullscreen()
    {
        (_primaryWindowSettings, _secondaryWindowSettings) = (_secondaryWindowSettings, _primaryWindowSettings);
        return SetScreenMode(_primaryWindowSettings);
    }

    /// <summary>
    /// Close the window.
    /// </summary>
    public void Close()
    {
        if (ReferenceEquals(_ownerContext.GetSubsystem<Graphics>(), this))
        {
            var renderDevice = _ownerContext.GetSubsystem<RenderDevice>();
            _ownerContext.RemoveSubsystem<RenderDevice>();
            renderDevice?.Dispose();
        }
        Shutdown();
    }

    /// <summary>
    /// Begin frame rendering.
    /// </summary>
    public bool BeginFrame()
    {
        if (!IsInitialized)
            return false;

        if (GetFullscreen() && _window is not null && !_window.IsNull)
        {
            var flags = SdlNativeBindings.SdlGetWindowFlags(_window);
            if ((flags & (uint)SdlWindowFlags.SdlWindowMinimized) != 0)
                return false;
        }

        SendEvent<GraphicsEvents.BeginRenderingEvent>();
        return true;
    }

    /// <summary>
    /// End frame rendering and swap buffers.
    /// </summary>
    public void EndFrame()
    {
        if (!IsInitialized)
            return;

        SendEvent<GraphicsEvents.EndRenderingEvent>();
        GetSubsystem<RenderDevice>()!.Present();
    }

    /// <summary>
    /// Clear any or all of the render target, depth buffer, and stencil buffer.
    /// </summary>
    public void Clear(ClearTarget flags, Color color = default, float depth = 1.0f, uint stencil = 0)
    {
        var renderDevice = GetSubsystem<RenderDevice>();
        if (renderDevice is null)
            return;

        var renderContext = renderDevice.GetRenderContext();
        if ((flags & ClearTarget.Color) != 0)
            renderContext.ClearRenderTarget(0, color);
        if ((flags & (ClearTarget.Depth | ClearTarget.Stencil)) != 0)
            renderContext.ClearDepthStencil(flags, depth, stencil);
    }

    /// <summary>
    /// Reset all render targets, the depth-stencil surface, and the viewport.
    /// </summary>
    public void ResetRenderTargets()
    {
        var renderDevice = GetSubsystem<RenderDevice>();
        if (renderDevice is null)
            return;

        var renderContext = renderDevice.GetRenderContext();
        renderContext.SetSwapChainRenderTargets();
        renderContext.SetFullViewport();
    }

    /// <summary>
    /// Restore GPU objects and reinitialize state.
    /// </summary>
    public void Restore()
    {
        var renderDevice = GetSubsystem<RenderDevice>();
        if (renderDevice is not null && !renderDevice.Restore())
            Close();
    }

    /// <summary>
    /// Return current render target width and height.
    /// </summary>
    public Vector2i GetRenderTargetDimensions() => Vector2i.Zero;

    /// <summary>
    /// Update the swap chain after the window was resized through user interaction.
    /// </summary>
    public void OnWindowResized()
    {
        var renderDevice = GetSubsystem<RenderDevice>();
        if (renderDevice is null)
            return;

        renderDevice.UpdateSwapChainSize();

        var screenModeEvent = new GraphicsEvents.ScreenModeEvent
        {
            Width = Width,
            Height = Width,
            Fullscreen = GetFullscreen(),
            Borderless = GetBorderless(),
            Resizable = GetResizable()
        };
        SendEvent(ref screenModeEvent);
    }

    /// <summary>
    /// Update the cached position after the window was moved through user interaction.
    /// </summary>
    public void OnWindowMoved()
    {
        if (GetFullscreen())
            return;

        var newPosition = GetWindowPosition();
        if (newPosition == _windowPosition)
            return;

        _windowPosition = newPosition;

        var windowPosEvent = new GraphicsEvents.WindowPosEvent
        {
            X = _windowPosition.X,
            Y = _windowPosition.Y
        };
        SendEvent(ref windowPosEvent);
    }

    public int GetMultiSample() => GetWindowSettings().MultiSample;

    public bool GetFullscreen() => GetWindowSettings().Mode == WindowMode.Fullscreen;

    public bool GetBorderless() => GetWindowSettings().Mode == WindowMode.Borderless;

    public bool GetResizable() => GetWindowSettings().Resizable;

    public bool GetVSync() => GetWindowSettings().VSync;

    public int GetRefreshRate() => GetWindowSettings().RefreshRate;

    public int GetMonitor() => GetWindowSettings().Monitor;

    public bool GetSRGB() => GetWindowSettings().SRgb;

    public Vector2i GetSize() => new(Width, Height);

    public IReadOnlyList<Vector3i> GetResolutions(int monitor)
    {
        var result = new List<Vector3i>();
        var numModes = SdlNativeBindings.SdlGetNumDisplayModes(monitor);
        for (var i = 0; i < numModes; ++i)
        {
            using var mode = new SdlDisplayMode();
            SdlNativeBindings.SdlGetDisplayMode(monitor, i, mode);
            var resolution = new Vector3i(mode.W, mode.H, mode.RefreshRate);
            var unique = true;
            for (var j = 0; j < result.Count; ++j)
            {
                if (result[j] == resolution)
                {
                    unique = false;
                    break;
                }
            }

            if (unique)
                result.Add(resolution);
        }

        return result;
    }

    public uint FindBestResolutionIndex(int monitor, int width, int height, int refreshRate)
    {
        var resolutions = GetResolutions(monitor);
        if (resolutions.Count == 0)
            return uint.MaxValue;

        var best = 0u;
        var bestError = uint.MaxValue;
        for (var i = 0; i < resolutions.Count; ++i)
        {
            var error = (uint)(Math.Abs(resolutions[i].X - width) + Math.Abs(resolutions[i].Y - height));
            if (refreshRate != 0)
                error += (uint)Math.Abs(resolutions[i].Z - refreshRate);
            if (error < bestError)
            {
                best = (uint)i;
                bestError = error;
            }
        }

        return best;
    }

    public Vector2i GetDesktopResolution(int monitor)
    {
        using var mode = new SdlDisplayMode();
        SdlNativeBindings.SdlGetDesktopDisplayMode(monitor, mode);
        return new Vector2i(mode.W, mode.H);
    }

    public int GetMonitorCount() => SdlNativeBindings.SdlGetNumVideoDisplays();

    public int GetCurrentMonitor()
    {
        return _window is not null && !_window.IsNull
            ? SdlNativeBindings.SdlGetWindowDisplayIndex(_window)
            : 0;
    }

    public bool GetMaximized()
    {
        return _window is not null && !_window.IsNull
            && (SdlNativeBindings.SdlGetWindowFlags(_window) & (uint)SdlWindowFlags.SdlWindowMaximized) != 0;
    }

    public void Maximize()
    {
        if (_window is not null && !_window.IsNull)
            SdlNativeBindings.SdlMaximizeWindow(_window);
    }

    public void Minimize()
    {
        if (_window is not null && !_window.IsNull)
            SdlNativeBindings.SdlMinimizeWindow(_window);
    }

    public void Raise()
    {
        if (_window is not null && !_window.IsNull)
            SdlNativeBindings.SdlRaiseWindow(_window);
    }

    public Vector3f GetDisplayDPI(int monitor = 0)
    {
        SdlNativeBindings.SdlGetDisplayDPI(monitor, out var ddpi, out var hdpi, out var vdpi);
        return new Vector3f(hdpi, vdpi, ddpi);
    }

    public RenderDeviceCaps GetRenderDeviceCaps()
    {
        var renderDevice = GetSubsystem<RenderDevice>();
        if (IsInitialized && renderDevice is not null)
            return renderDevice.GetCaps();

        return new RenderDeviceCaps
        {
            ComputeShaders = false,
            DrawBaseVertex = true,
            DrawBaseInstance = true,
            ClipDistance = true,
            ReadOnlyDepth = true,
            SrgbOutput = true,
            HdrOutput = true,
            ConstantBufferOffsetAlignment = 256,
            MaxTextureSize = 16384,
            MaxRenderTargetSize = 16384
        };
    }

    public int GetSupportedMultiSample(TextureFormat format, int requestedLevel)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(requestedLevel);
        _ = format;

        var normalizedLevel = (int)Math.Clamp(MathDefs.ClosestPowerOfTwo((uint)requestedLevel), 1u, 16u);
        return Math.Min(normalizedLevel, Math.Max(_configuredMultiSample, 1));
    }

    public bool IsRenderTargetFormatSupported(TextureFormat format)
    {
        return format is TextureFormat.TexFormatD32Float or TextureFormat.TexFormatD24UnormS8Uint
            or TextureFormat.TexFormatR8Unorm or TextureFormat.TexFormatRg16Float or TextureFormat.TexFormatRgba8Unorm or TextureFormat.TexFormatRgba8Unorm
            or TextureFormat.TexFormatRgba16Float or TextureFormat.TexFormatRgba32Float;
    }

    public ShaderVariation? GetShader(ShaderType type, string name, string defines = "")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (_lastShaderName == name && _lastShader is not null)
            return _lastShader.GetVariation(type, defines);

        var cache = GetSubsystem<ResourceCache>()!;
        _lastShader = null;
        var shaderResourceName = Shader.GetResourceName(name);
        if (name.StartsWith("v2/", StringComparison.Ordinal) && cache.Exists(shaderResourceName))
        {
            _lastShader = cache.GetResourceOrNull<Shader>(shaderResourceName);
            _lastShaderName = name;
        }

        if (_lastShader is null && (_lastShaderName != name || cache.Exists(shaderResourceName)))
        {
            _lastShader = cache.GetResourceOrNull<Shader>(shaderResourceName);
            _lastShaderName = name;
        }

        return _lastShader?.GetVariation(type, defines);
    }

    public IRenderDevice? GetNativeRenderDevice() => _device;

    public IEngineFactory? GetNativeFactory() => _factory;

    public ISwapChain? GetNativeSwapChain() => _swapChain;

#if REDBOLT_VULKAN_SUPPORTED
    internal SwapChainDesc ReleaseVulkanSwapChain()
    {
        System.Diagnostics.Debug.Assert(_swapChain is not null);
        using var swapChainDesc = _swapChain!.GetDesc();
        var oldNativeSwapChainDesc = swapChainDesc.Clone();

        _renderTargets[0] = null;
        _renderTargetView = null;
        _depthStencilView = null;
        _swapChainOutputTarget = null;
        _swapChainColorFormat = 0;
        _swapChainDepthFormat = 0;
        ReleaseIfNotNull(_swapChain);
        _swapChain = null;
        return oldNativeSwapChainDesc;
    }

    internal bool RestoreVulkanSwapChain(SwapChainDesc swapChainDesc,
        global::RedBolt.Bindings.Diligent.TextureFormat depthFormat, int multiSample)
    {
        System.Diagnostics.Debug.Assert(_device is not null && _immediateContext is not null && _factory is not null);
        using var factoryVulkan = IEngineFactoryVk.QueryManagedWrapper(_factory!)
            ?? throw new InvalidOperationException("Vulkan engine factory interface is unavailable.");
#if REDBOLT_PLATFORM_LINUX
        var nativeWindowInfo = GetSubsystem<Input>()!.GetNativeWindowInfo();
        using var nativeWindow = new LinuxNativeWindow
        {
            PDisplay = nativeWindowInfo.Display,
            WindowId = nativeWindowInfo.WindowId
        };
#else
#error Vulkan native window restoration is not ported for this platform
#endif
        factoryVulkan.CreateSwapChainVk(_device!, _immediateContext!, swapChainDesc, nativeWindow,
            out var nativeSwapChain);
        if (nativeSwapChain.IsNull)
            return false;

        _swapChain = DiligentNativeBindings.CreateProxySwapChainMS(
            _device!, _immediateContext!, nativeSwapChain, (uint)depthFormat, (uint)multiSample);
        ReleaseIfNotNull(nativeSwapChain);
        if (_swapChain is null || _swapChain.IsNull)
            return false;

        _configuredMultiSample = multiSample;
        _swapChain.Resize(swapChainDesc.Width, swapChainDesc.Height, swapChainDesc.PreTransform);
        _renderTargetView = _swapChain.GetCurrentBackBufferRTV();
        _depthStencilView = _swapChain.GetDepthBufferDSV();
        _swapChainColorFormat = GetTextureViewFormat(_renderTargetView);
        _swapChainDepthFormat = GetTextureViewFormat(_depthStencilView);
        _renderTargets[0] = _renderTargetView;
        return true;
    }
#endif

    internal IntPtr GetGLContext() => _glContext;

    internal void ReleaseGLContext()
    {
        if (_glContext == IntPtr.Zero)
            return;

        SdlNativeBindings.SdlGlDeleteContext(_glContext);
        _glContext = IntPtr.Zero;
    }

    internal bool RestoreGLContext()
    {
        _glContext = SdlNativeBindings.SdlGlCreateContext(_window!);
        return _glContext != IntPtr.Zero;
    }

    public SdlWindow? GetNativeWindow() => _window;

    public static bool GetGL3Support() => true;

    public SdlWindow? GetSDLWindow() => GetNativeWindow();

    public static void SetMaxBones(uint maxBones)
    {
        _maxBonesHWSkinned = maxBones;
    }

    public static uint GetMaxBones() => _maxBonesHWSkinned != 0u ? _maxBonesHWSkinned : 128u;

    /// <summary>
    /// Return OS-specific external window handle.
    /// </summary>
    public nint GetExternalWindow()
    {
        var renderDevice = GetSubsystem<RenderDevice>();
        return renderDevice?.GetDeviceSettings().ExternalWindowHandle ?? nint.Zero;
    }

    /// <summary>
    /// Return graphics API name.
    /// </summary>
    public string GetApiName() => GetRenderBackend().ToString();

    /// <summary>
    /// Return render backend.
    /// </summary>
    public RenderBackend GetRenderBackend()
    {
        var renderDevice = GetSubsystem<RenderDevice>();
        return renderDevice?.GetBackend() ?? RenderBackend.OpenGL;
    }

    /// <summary>
    /// Return dummy color texture format for shadow maps.
    /// </summary>
    public TextureFormat GetDummyColorFormat() => TextureFormat.TexFormatUnknown;

    /// <summary>
    /// Return shadow map depth texture format.
    /// </summary>
    public TextureFormat GetShadowMapFormat() => TextureFormat.TexFormatUnknown;

    /// <summary>
    /// Return 24-bit shadow map depth texture format.
    /// </summary>
    public TextureFormat GetHiresShadowMapFormat() => TextureFormat.TexFormatUnknown;

    /// <summary>
    /// Return whether hardware instancing is supported.
    /// </summary>
    public bool GetInstancingSupport() => true;

    /// <summary>
    /// Return whether shadow map depth compare is done in hardware.
    /// </summary>
    public bool GetHardwareShadowSupport() => true;

    internal ITexture? GetTextureHandle(Texture texture)
    {
        ArgumentNullException.ThrowIfNull(texture);
        return !IsInitialized ? null : GetOrCreateUploadedTexture(texture).Texture;
    }

    internal RenderOutputTargetInfo GetSwapChainOutputTarget()
    {
        var colorFormat = GetSwapChainColorTextureFormat(out var colorIsSrgb);
        var depthFormat = GetSwapChainDepthTextureFormat();
        var fullRect = new IntRect(0, 0, Width, Height);
        var multiSample = GetSwapChainMultiSample();
        var linearColorSpace = TextureFormatUtility.IsFloatColorFormat(colorFormat) || colorIsSrgb;
        var hdr = TextureFormatUtility.IsFloatColorFormat(colorFormat);
        var hasStencil = depthFormat == TextureFormat.TexFormatD24UnormS8Uint;

        if (_swapChainOutputTarget is not null
            && _swapChainOutputTarget.FullViewportRect == fullRect
            && _swapChainOutputTarget.ColorFormat == colorFormat
            && _swapChainOutputTarget.DepthStencilFormat == depthFormat
            && _swapChainOutputTarget.MultiSample == multiSample
            && _swapChainOutputTarget.LinearColorSpace == linearColorSpace
            && _swapChainOutputTarget.HDR == hdr
            && _swapChainOutputTarget.HasStencil == hasStencil)
        {
            return _swapChainOutputTarget;
        }

        var renderDevice = GetSubsystem<RenderDevice>()!;
        _swapChainOutputTarget = new RenderOutputTargetInfo(
            RenderTargetView.SwapChainColor(renderDevice),
            fullRect,
            colorFormat,
            RenderTargetView.SwapChainDepthStencil(renderDevice),
            depthFormat,
            multiSample,
            bilinearFiltered: false,
            linearColorSpace: linearColorSpace,
            hdr: hdr,
            hasReadableDepth: false,
            hasStencil: hasStencil);
        return _swapChainOutputTarget;
    }

    /// <summary>
    /// Return the managed texture format that matches the swap chain color buffer.
    /// C++ <c>RenderTargetView::GetFormat</c> returns <c>SwapChainDesc.ColorBufferFormat</c> as-is
    /// (Diligent <c>TextureFormat</c>). Preserve BGRA/sRGB so pipeline RT formats match the actual
    /// back buffer — required for Vulkan PSO compatibility.
    /// </summary>
    internal TextureFormat GetSwapChainColorTextureFormat(out bool isSrgb)
    {
        var format = GetSwapChainColorFormat();
        isSrgb = format is TextureFormat.TexFormatRgba8UnormSrgb or TextureFormat.TexFormatBgra8UnormSrgb;
        return format;
    }

    internal TextureFormat GetSwapChainDepthTextureFormat()
        => GetSwapChainDepthFormat();

    internal int GetSwapChainMultiSample()
        => Math.Max(_configuredMultiSample, 1);

    private static void CopyBackendDeviceSettings(EngineVkCreateInfo createInfo, RenderDeviceSettingsVulkan settings)
    {
        if (settings.MainDescriptorPoolSize is { } mainDescriptorPoolSize)
        {
            createInfo.MainDescriptorPoolSizeMaxDescriptorSets = checked((uint)mainDescriptorPoolSize.MaxDescriptorSets);
            createInfo.MainDescriptorPoolSizeNumSeparateSamplerDescriptors = checked((uint)mainDescriptorPoolSize.NumSeparateSamplerDescriptors);
            createInfo.MainDescriptorPoolSizeNumCombinedSamplerDescriptors = checked((uint)mainDescriptorPoolSize.NumCombinedSamplerDescriptors);
            createInfo.MainDescriptorPoolSizeNumSampledImageDescriptors = checked((uint)mainDescriptorPoolSize.NumSampledImageDescriptors);
            createInfo.MainDescriptorPoolSizeNumStorageImageDescriptors = checked((uint)mainDescriptorPoolSize.NumStorageImageDescriptors);
            createInfo.MainDescriptorPoolSizeNumUniformBufferDescriptors = checked((uint)mainDescriptorPoolSize.NumUniformBufferDescriptors);
            createInfo.MainDescriptorPoolSizeNumStorageBufferDescriptors = checked((uint)mainDescriptorPoolSize.NumStorageBufferDescriptors);
            createInfo.MainDescriptorPoolSizeNumUniformTexelBufferDescriptors = checked((uint)mainDescriptorPoolSize.NumUniformTexelBufferDescriptors);
            createInfo.MainDescriptorPoolSizeNumStorageTexelBufferDescriptors = checked((uint)mainDescriptorPoolSize.NumStorageTexelBufferDescriptors);
            createInfo.MainDescriptorPoolSizeNumInputAttachmentDescriptors = checked((uint)mainDescriptorPoolSize.NumInputAttachmentDescriptors);
            createInfo.MainDescriptorPoolSizeNumAccelStructDescriptors = checked((uint)mainDescriptorPoolSize.NumAccelStructDescriptors);
        }
        if (settings.DynamicDescriptorPoolSize is { } dynamicDescriptorPoolSize)
        {
            createInfo.DynamicDescriptorPoolSizeMaxDescriptorSets = checked((uint)dynamicDescriptorPoolSize.MaxDescriptorSets);
            createInfo.DynamicDescriptorPoolSizeNumSeparateSamplerDescriptors = checked((uint)dynamicDescriptorPoolSize.NumSeparateSamplerDescriptors);
            createInfo.DynamicDescriptorPoolSizeNumCombinedSamplerDescriptors = checked((uint)dynamicDescriptorPoolSize.NumCombinedSamplerDescriptors);
            createInfo.DynamicDescriptorPoolSizeNumSampledImageDescriptors = checked((uint)dynamicDescriptorPoolSize.NumSampledImageDescriptors);
            createInfo.DynamicDescriptorPoolSizeNumStorageImageDescriptors = checked((uint)dynamicDescriptorPoolSize.NumStorageImageDescriptors);
            createInfo.DynamicDescriptorPoolSizeNumUniformBufferDescriptors = checked((uint)dynamicDescriptorPoolSize.NumUniformBufferDescriptors);
            createInfo.DynamicDescriptorPoolSizeNumStorageBufferDescriptors = checked((uint)dynamicDescriptorPoolSize.NumStorageBufferDescriptors);
            createInfo.DynamicDescriptorPoolSizeNumUniformTexelBufferDescriptors = checked((uint)dynamicDescriptorPoolSize.NumUniformTexelBufferDescriptors);
            createInfo.DynamicDescriptorPoolSizeNumStorageTexelBufferDescriptors = checked((uint)dynamicDescriptorPoolSize.NumStorageTexelBufferDescriptors);
            createInfo.DynamicDescriptorPoolSizeNumInputAttachmentDescriptors = checked((uint)dynamicDescriptorPoolSize.NumInputAttachmentDescriptors);
            createInfo.DynamicDescriptorPoolSizeNumAccelStructDescriptors = checked((uint)dynamicDescriptorPoolSize.NumAccelStructDescriptors);
        }
        if (settings.DeviceLocalMemoryPageSize.HasValue)
            createInfo.DeviceLocalMemoryPageSize = checked((uint)settings.DeviceLocalMemoryPageSize.Value);
        if (settings.HostVisibleMemoryPageSize.HasValue)
            createInfo.HostVisibleMemoryPageSize = checked((uint)settings.HostVisibleMemoryPageSize.Value);
        if (settings.DeviceLocalMemoryReserveSize.HasValue)
            createInfo.DeviceLocalMemoryReserveSize = checked((uint)settings.DeviceLocalMemoryReserveSize.Value);
        if (settings.HostVisibleMemoryReserveSize.HasValue)
            createInfo.HostVisibleMemoryReserveSize = checked((uint)settings.HostVisibleMemoryReserveSize.Value);
        if (settings.UploadHeapPageSize.HasValue)
            createInfo.UploadHeapPageSize = checked((uint)settings.UploadHeapPageSize.Value);
        if (settings.DynamicHeapSize.HasValue)
            createInfo.DynamicHeapSize = checked((uint)settings.DynamicHeapSize.Value);
        if (settings.DynamicHeapPageSize.HasValue)
            createInfo.DynamicHeapPageSize = checked((uint)settings.DynamicHeapPageSize.Value);

        var queryPoolSizes = createInfo.QueryPoolSizes;
        for (var i = 1; i < (int)QueryType.QueryTypeNumTypes; ++i)
        {
            var size = settings.QueryPoolSizes[i];
            if (size.HasValue)
                queryPoolSizes[i] = checked((uint)size.Value);
        }
    }

    public void Initialize(EngineParameters parameters, Input input)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(input);

        if (IsInitialized)
            throw new InvalidOperationException("Graphics system is already initialized.");

        // C++ RenderDevice ctor: Diligent::SetDebugMessageCallback(&DebugMessageCallback)
        // before factory/device creation (filters noisy shader compiler INFO logs).
        DiligentNativeBindings.InstallDebugMessageCallback();

        GetSubsystem<RenderDevice>()!.InitializeWindowSettings(parameters);

        _window = input.Window;
        var backend = GetSubsystem<RenderDevice>()!.GetBackend();
        if (backend == RenderBackend.OpenGL)
        {
            _glContext = SdlNativeBindings.SdlGlCreateContext(_window);
            if (_glContext == IntPtr.Zero)
                throw new InvalidOperationException("Generated SDL_GL_CreateContext binding returned a null context.");
        }

        try
        {
            if (backend == RenderBackend.OpenGL)
            {
                CheckSdlGl(SdlNativeBindings.SdlGlMakeCurrent(_window, _glContext), "SDL_GL_MakeCurrent");
#if !REDBOLT_PLATFORM_WEB
                _ = SdlNativeBindings.SdlGlSetSwapInterval(parameters.VSync ? 1 : 0);
#endif
                SdlNativeBindings.SdlGetWindowSize(_window, out var actualWidth, out var actualHeight);
            Width = actualWidth;
            Height = actualHeight;
            _configuredMultiSample = (int)Math.Clamp(MathDefs.ClosestPowerOfTwo((uint)Math.Max(parameters.MultiSample, 1)), 1u, 16u);
            if (SdlNativeBindings.SdlGlGetAttribute(SdlGLattr.SdlGlMultisamplesamples, out var effectiveMultiSample) == 0)
                _configuredMultiSample = Math.Max(1, effectiveMultiSample);
            var effectiveSrgb = 0;
            _ = SdlNativeBindings.SdlGlGetAttribute(SdlGLattr.SdlGlFramebufferSrgbCapable, out effectiveSrgb);

            using var engineCreateInfo = new EngineGLCreateInfo
            {
                FeaturesSeparablePrograms = DeviceFeatureState.DeviceFeatureStateOptional,
                FeaturesShaderResourceQueries = DeviceFeatureState.DeviceFeatureStateOptional
#if REDBOLT_PLATFORM_LINUX
                ,
                WindowPDisplay = input.GetNativeWindowInfo().Display,
                WindowWindowId = input.GetNativeWindowInfo().WindowId
#endif
            };
            using var swapChainDesc = new SwapChainDesc
            {
                ColorBufferFormat = effectiveSrgb != 0
                    ? global::RedBolt.Bindings.Diligent.TextureFormat.TexFormatRgba8UnormSrgb
                    : global::RedBolt.Bindings.Diligent.TextureFormat.TexFormatRgba8Unorm,
                DepthBufferFormat = global::RedBolt.Bindings.Diligent.TextureFormat.TexFormatUnknown
            };

            var factory = DiligentNativeBindings.GetEngineFactoryOpenGL();
            if (factory.IsNull)
                throw new InvalidOperationException("Generated GetEngineFactoryOpenGL binding returned a null factory.");

            _factory = factory;
            factory.AttachToActiveGLContext(engineCreateInfo, out _device, out _immediateContext);
            if (_device.IsNull || _immediateContext.IsNull)
                throw new InvalidOperationException("Generated AttachToActiveGLContext binding returned a null device or context.");

            var swapChainGL = DiligentNativeBindings.CreateProxySwapChainGL(
                _device, _immediateContext, swapChainDesc, _window.Handle);
            if (swapChainGL.IsNull)
                throw new InvalidOperationException("Generated CreateProxySwapChainGL binding returned a null swap chain.");

                using var deviceContextGL = IDeviceContextGL.QueryManagedWrapper(_immediateContext)
                    ?? throw new InvalidOperationException("OpenGL device context interface is unavailable.");
                deviceContextGL.SetSwapChain(swapChainGL);
                _swapChain = swapChainGL;
            }
#if REDBOLT_VULKAN_SUPPORTED
            else if (backend == RenderBackend.Vulkan)
            {
                SdlNativeBindings.SdlGetWindowSize(_window, out var actualWidth, out var actualHeight);
                Width = actualWidth;
                Height = actualHeight;

                using var createInfo = new EngineVkCreateInfo();
                var deviceSettings = GetSubsystem<RenderDevice>()!.GetDeviceSettings();
                CopyBackendDeviceSettings(createInfo, deviceSettings.Vulkan);

                // TODO: port C++ instanceExtensionsCStr, deviceExtensionsCStr and ppIgnoreDebugMessages when
                // SharpCpp supports const char* const* fields.
                createInfo.FeaturesAsyncShaderCompilation = DeviceFeatureState.DeviceFeatureStateOptional;
                createInfo.FeaturesBinaryOcclusionQueries = DeviceFeatureState.DeviceFeatureStateOptional;
                createInfo.FeaturesBindlessResources = DeviceFeatureState.DeviceFeatureStateOptional;
                createInfo.FeaturesComputeShaders = DeviceFeatureState.DeviceFeatureStateOptional;
                createInfo.FeaturesDepthBiasClamp = DeviceFeatureState.DeviceFeatureStateOptional;
                createInfo.FeaturesDepthClamp = DeviceFeatureState.DeviceFeatureStateOptional;
                createInfo.FeaturesDualSourceBlend = DeviceFeatureState.DeviceFeatureStateOptional;
                createInfo.FeaturesDurationQueries = DeviceFeatureState.DeviceFeatureStateOptional;
                createInfo.FeaturesFormattedBuffers = DeviceFeatureState.DeviceFeatureStateOptional;
                createInfo.FeaturesGeometryShaders = DeviceFeatureState.DeviceFeatureStateOptional;
                createInfo.FeaturesIndependentBlend = DeviceFeatureState.DeviceFeatureStateOptional;
                createInfo.FeaturesInstanceDataStepRate = DeviceFeatureState.DeviceFeatureStateOptional;
                createInfo.FeaturesMeshShaders = DeviceFeatureState.DeviceFeatureStateOptional;
                createInfo.FeaturesMultiViewport = DeviceFeatureState.DeviceFeatureStateOptional;
                createInfo.FeaturesMultithreadedResourceCreation = DeviceFeatureState.DeviceFeatureStateOptional;
                createInfo.FeaturesNativeFence = DeviceFeatureState.DeviceFeatureStateOptional;
                createInfo.FeaturesNativeMultiDraw = DeviceFeatureState.DeviceFeatureStateOptional;
                createInfo.FeaturesOcclusionQueries = DeviceFeatureState.DeviceFeatureStateOptional;
                createInfo.FeaturesPipelineStatisticsQueries = DeviceFeatureState.DeviceFeatureStateOptional;
                createInfo.FeaturesPixelUAVWritesAndAtomics = DeviceFeatureState.DeviceFeatureStateOptional;
                createInfo.FeaturesRayTracing = DeviceFeatureState.DeviceFeatureStateOptional;
                createInfo.FeaturesResourceBuffer16BitAccess = DeviceFeatureState.DeviceFeatureStateOptional;
                createInfo.FeaturesResourceBuffer8BitAccess = DeviceFeatureState.DeviceFeatureStateOptional;
                createInfo.FeaturesSeparablePrograms = DeviceFeatureState.DeviceFeatureStateOptional;
                createInfo.FeaturesShaderFloat16 = DeviceFeatureState.DeviceFeatureStateOptional;
                createInfo.FeaturesShaderInputOutput16 = DeviceFeatureState.DeviceFeatureStateOptional;
                createInfo.FeaturesShaderInt8 = DeviceFeatureState.DeviceFeatureStateOptional;
                createInfo.FeaturesShaderResourceQueries = DeviceFeatureState.DeviceFeatureStateOptional;
                createInfo.FeaturesShaderResourceRuntimeArrays = DeviceFeatureState.DeviceFeatureStateOptional;
                createInfo.FeaturesShaderResourceStaticArrays = DeviceFeatureState.DeviceFeatureStateOptional;
                createInfo.FeaturesSparseResources = DeviceFeatureState.DeviceFeatureStateOptional;
                createInfo.FeaturesSubpassFramebufferFetch = DeviceFeatureState.DeviceFeatureStateOptional;
                createInfo.FeaturesTessellation = DeviceFeatureState.DeviceFeatureStateOptional;
                createInfo.FeaturesTextureComponentSwizzle = DeviceFeatureState.DeviceFeatureStateOptional;
                createInfo.FeaturesTextureCompressionBC = DeviceFeatureState.DeviceFeatureStateOptional;
                createInfo.FeaturesTextureCompressionETC2 = DeviceFeatureState.DeviceFeatureStateOptional;
                createInfo.FeaturesTextureSubresourceViews = DeviceFeatureState.DeviceFeatureStateOptional;
                createInfo.FeaturesTextureUAVExtendedFormats = DeviceFeatureState.DeviceFeatureStateOptional;
                createInfo.FeaturesTileShaders = DeviceFeatureState.DeviceFeatureStateOptional;
                createInfo.FeaturesTimestampQueries = DeviceFeatureState.DeviceFeatureStateOptional;
                createInfo.FeaturesTransferQueueTimestampQueries = DeviceFeatureState.DeviceFeatureStateDisabled;
                createInfo.FeaturesUniformBuffer16BitAccess = DeviceFeatureState.DeviceFeatureStateOptional;
                createInfo.FeaturesUniformBuffer8BitAccess = DeviceFeatureState.DeviceFeatureStateOptional;
                createInfo.FeaturesVariableRateShading = DeviceFeatureState.DeviceFeatureStateOptional;
                createInfo.FeaturesVertexPipelineUAVWritesAndAtomics = DeviceFeatureState.DeviceFeatureStateOptional;
                createInfo.FeaturesWaveOp = DeviceFeatureState.DeviceFeatureStateOptional;
                createInfo.FeaturesWireframeFill = DeviceFeatureState.DeviceFeatureStateOptional;
                if (deviceSettings.AdapterId.HasValue)
                    createInfo.AdapterId = checked((uint)deviceSettings.AdapterId.Value);
                var factoryVulkan = DiligentNativeBindings.GetEngineFactoryVk();
                if (factoryVulkan.IsNull)
                    throw new InvalidOperationException("Generated GetEngineFactoryVk binding returned a null factory.");

                _factory = factoryVulkan;
                factoryVulkan.CreateDeviceAndContextsVk(createInfo, out _device, out _immediateContext);
                if (_device.IsNull || _immediateContext.IsNull)
                    throw new InvalidOperationException("Generated CreateDeviceAndContextsVk binding returned a null device or context.");

                using var swapChainDesc = new SwapChainDesc
                {
                    ColorBufferFormat = GetSubsystem<RenderDevice>()!.GetWindowSettings().SRgb
                        ? global::RedBolt.Bindings.Diligent.TextureFormat.TexFormatRgba8UnormSrgb
                        : global::RedBolt.Bindings.Diligent.TextureFormat.TexFormatRgba8Unorm,
                    DepthBufferFormat = global::RedBolt.Bindings.Diligent.TextureFormat.TexFormatUnknown
                };
                var nativeWindowInfo = input.GetNativeWindowInfo();
                using var nativeWindow = new LinuxNativeWindow
                {
                    PDisplay = nativeWindowInfo.Display,
                    WindowId = nativeWindowInfo.WindowId
                };
                factoryVulkan.CreateSwapChainVk(_device, _immediateContext, swapChainDesc, nativeWindow,
                    out var nativeSwapChain);
                if (nativeSwapChain.IsNull)
                    throw new InvalidOperationException("Generated CreateSwapChainVk binding returned a null swap chain.");
                _swapChainVulkan = ISwapChainVk.QueryManagedWrapper(nativeSwapChain)
                    ?? throw new InvalidOperationException("Vulkan swap chain interface is unavailable.");

                var renderDevice = GetSubsystem<RenderDevice>()!;
                var depthFormat = RenderDevice.SelectDefaultDepthFormat(_device, true);
                _configuredMultiSample = renderDevice.GetSupportedMultiSample(
                    (TextureFormat)swapChainDesc.ColorBufferFormat, parameters.MultiSample);
                _swapChain = DiligentNativeBindings.CreateProxySwapChainMS(
                    _device, _immediateContext, nativeSwapChain, (uint)depthFormat, (uint)_configuredMultiSample);
                ReleaseIfNotNull(nativeSwapChain);
                if (_swapChain.IsNull)
                    throw new InvalidOperationException("Generated CreateProxySwapChainMS binding returned a null swap chain.");
            }
#endif
            else
                throw new InvalidOperationException("Unsupported render backend.");

            _renderTargetView = _swapChain.GetCurrentBackBufferRTV();
            _depthStencilView = _swapChain.GetDepthBufferDSV();
            if (_renderTargetView.IsNull || _depthStencilView.IsNull)
                throw new InvalidOperationException("Swap chain render target or depth view was null.");
            _swapChainColorFormat = GetTextureViewFormat(_renderTargetView);
            _swapChainDepthFormat = GetTextureViewFormat(_depthStencilView);
            _renderTargets[0] = _renderTargetView;

            GetSubsystem<RenderDevice>()!.UpdateEffectiveWindowSettings(
                new Vector2i(Width, Height),
                _configuredMultiSample,
                RenderAPIUtils.IsTextureFormatSRGB((TextureFormat)_swapChainColorFormat));

            _clearColorHandle = GCHandle.Alloc(_clearColor, GCHandleType.Pinned);
            IsInitialized = true;
            GetSubsystem<RenderDevice>()!.PostInitialize();
            OnScreenModeChanged();
        }
        catch
        {
            Shutdown();
            throw;
        }
    }

    internal void RenderViewport(Viewport? viewport)
    {
        RenderViewport(viewport, MaterialQuality.High);
    }

    /// <summary>
    /// Render viewport through the legacy direct path that binds the backbuffer and draws the scene.
    /// </summary>
    internal void RenderViewport(Viewport? viewport, MaterialQuality materialQuality)
    {
        if (!IsInitialized || viewport is null || _immediateContext is null || _swapChain is null || _renderTargetView is null || _depthStencilView is null)
            return;

        SendEvent<GraphicsEvents.BeginRenderingEvent>();

        var viewportRect = GetEffectiveViewportRect(viewport);
        SetSwapChainRenderTargets(viewportRect);
        var camera = viewport.GetCamera()!;
        var scene = viewport.GetScene()!;
        var cameraPosition = camera.Node is not null ? camera.Node.WorldPosition : Vector3f.Zero;
        var fogColorGamma = FindZone(scene, cameraPosition)?.GetFogColor() ?? Color.Black;
        var effectiveFogColor = GetSwapChainOutputTarget().LinearColorSpace
            ? fogColorGamma.GammaToLinear()
            : fogColorGamma;
        ClearCurrentRenderTargets(effectiveFogColor);
        RenderScene(viewport, materialQuality, viewportRect);
        SendEvent<GraphicsEvents.EndRenderingEvent>();
        UnbindRenderTargets();
    }

    /// <summary>
    /// Render viewport into a render surface without presenting the swap chain.
    /// </summary>
    internal void RenderViewport(RenderSurface renderSurface, Viewport viewport, MaterialQuality materialQuality)
    {
        ArgumentNullException.ThrowIfNull(renderSurface);
        ArgumentNullException.ThrowIfNull(viewport);

        if (!IsInitialized || _immediateContext is null)
            return;

        SendEvent<GraphicsEvents.BeginRenderingEvent>();

        var viewportRect = viewport.GetEffectiveRect(renderSurface);
        var colorTarget = renderSurface.GetView();
        var depthStencil = renderSurface.GetLinkedDepthStencil()?.GetView();
        SetRenderTargets(viewportRect, colorTarget, depthStencil);
        var camera = viewport.GetCamera()!;
        var scene = viewport.GetScene()!;
        var cameraPosition = camera.Node is not null ? camera.Node.WorldPosition : Vector3f.Zero;
        var fogColorGamma = FindZone(scene, cameraPosition)?.GetFogColor() ?? Color.Black;
        var outputTexture = renderSurface.GetParentTexture()!;
        var linearColorSpace = outputTexture.GetLinear() || outputTexture.GetSRGB()
            || TextureFormatUtility.IsFloatColorFormat(outputTexture.Format);
        var effectiveFogColor = linearColorSpace ? fogColorGamma.GammaToLinear() : fogColorGamma;
        ClearCurrentRenderTargets(effectiveFogColor);
        RenderScene(viewport, materialQuality, viewportRect);
        SendEvent<GraphicsEvents.EndRenderingEvent>();

        if (renderSurface.GetMultiSample() > 1)
            renderSurface.SetResolveDirty(true);
        UnbindRenderTargets();
    }

    /// <summary>
    /// Bind an optional color target and optional depth-stencil target for subsequent scene or post-process rendering.
    /// </summary>
    internal void SetRenderTargets(IntRect viewportRect, ReadOnlySpan<RenderTargetView> colorTargets, RenderTargetView? depthStencilTarget)
    {
        if (!IsInitialized || _immediateContext is null)
            return;

        if (viewportRect.Width <= 0 || viewportRect.Height <= 0)
            throw new ArgumentOutOfRangeException(nameof(viewportRect), "Viewport rectangle must have positive dimensions.");
        if (colorTargets.Length > RenderApiLimits.MaxRenderTargets)
            throw new ArgumentOutOfRangeException(nameof(colorTargets));

        for (var i = 0; i < colorTargets.Length; ++i)
            _renderTargets[i] = ResolveRenderTargetView(colorTargets[i]);
        Array.Clear(_renderTargets, colorTargets.Length, _renderTargets.Length - colorTargets.Length);
        _currentRenderTargetCount = colorTargets.Length;
        _currentColorTargetFormat = colorTargets.Length != 0 ? ResolveColorTargetFormat(colorTargets[0]) : 0;
        _currentDepthStencilTarget = depthStencilTarget is { } depthTarget
            ? ResolveRenderTargetView(depthTarget)
            : null;
        _currentDepthTargetFormat = depthStencilTarget is { } depthView ? ResolveDepthTargetFormat(depthView) : 0;
        _currentViewport = viewportRect;
        _currentRenderTargetSize = ResolveRenderTargetSize(colorTargets, depthStencilTarget, viewportRect);
        _isSwapChainRenderTarget = depthStencilTarget?.IsSwapChain == true;
        UpdateCurrentOutputDesc(colorTargets, depthStencilTarget);

        foreach (var colorTarget in colorTargets)
        {
            _isSwapChainRenderTarget |= colorTarget.IsSwapChain;
            MarkRenderTargetDirty(colorTarget);
        }
        MarkRenderTargetDirty(depthStencilTarget);

        _immediateContext.SetRenderTargets((uint)_currentRenderTargetCount,
            _renderTargets, _currentDepthStencilTarget!,
            ResourceStateTransitionMode.ResourceStateTransitionModeTransition);
        ApplyDiligentViewport(_immediateContext, viewportRect);
    }

    internal void SetRenderTargets(IntRect viewportRect, RenderTargetView colorTarget, RenderTargetView? depthStencilTarget)
    {
        _singleRenderTargetView[0] = colorTarget;
        SetRenderTargets(viewportRect, _singleRenderTargetView, depthStencilTarget);
    }

    internal void SetRenderTargets(IntRect viewportRect, RenderTargetView? colorTarget, RenderTargetView? depthStencilTarget)
    {
        if (colorTarget is { } target)
            SetRenderTargets(viewportRect, target, depthStencilTarget);
        else
            SetRenderTargets(viewportRect, ReadOnlySpan<RenderTargetView>.Empty, depthStencilTarget);
    }

    /// <summary>
    /// Bind the swap chain backbuffer and depth buffer for rendering.
    /// </summary>
    internal void SetSwapChainRenderTargets(IntRect viewportRect)
    {
        var renderDevice = GetSubsystem<RenderDevice>()!;
        _singleRenderTargetView[0] = RenderTargetView.SwapChainColor(renderDevice);
        SetRenderTargets(viewportRect, _singleRenderTargetView, RenderTargetView.SwapChainDepthStencil(renderDevice));
    }

    internal void SetViewport(IntRect viewportRect)
    {
        if (!IsInitialized || _immediateContext is null)
            return;

        if (viewportRect.Width <= 0 || viewportRect.Height <= 0)
            throw new ArgumentOutOfRangeException(nameof(viewportRect), "Viewport rectangle must have positive dimensions.");

        _currentViewport = viewportRect;
        ApplyDiligentViewport(_immediateContext, viewportRect);
    }

    /// <summary>
    /// Clear currently bound render targets.
    /// </summary>
    internal void ClearCurrentRenderTargets(Color? color = null, bool clearColorTarget = true, bool clearDepth = true, bool clearStencil = false,
        float depth = 1.0f, byte stencil = 0)
    {
        if (!IsInitialized || _immediateContext is null)
            return;

        if (color is { } clearValue)
        {
            _clearColor[0] = clearValue.R;
            _clearColor[1] = clearValue.G;
            _clearColor[2] = clearValue.B;
            _clearColor[3] = clearValue.A;
        }

        if (clearColorTarget)
        {
            for (var i = 0; i < _currentRenderTargetCount; ++i)
            {
                if (_renderTargets[i] is { } target)
                {
                    _immediateContext.ClearRenderTarget(target, _clearColorHandle.AddrOfPinnedObject(),
                        ResourceStateTransitionMode.ResourceStateTransitionModeTransition);
                }
            }
        }

        if ((clearDepth || clearStencil) && _currentDepthStencilTarget is not null)
        {
            var flags = ClearDepthStencilFlags.ClearDepthFlagNone;
            if (clearDepth)
                flags |= ClearDepthStencilFlags.ClearDepthFlag;
            if (clearStencil)
                flags |= ClearDepthStencilFlags.ClearStencilFlag;
            _immediateContext.ClearDepthStencil(_currentDepthStencilTarget, flags, depth, stencil,
                ResourceStateTransitionMode.ResourceStateTransitionModeTransition);
        }
    }

    internal void ClearRenderTarget(RenderTargetView renderTarget, Color color)
    {
        if (!IsInitialized || _immediateContext is null)
            return;

        _clearColor[0] = color.R;
        _clearColor[1] = color.G;
        _clearColor[2] = color.B;
        _clearColor[3] = color.A;
        _immediateContext.ClearRenderTarget(ResolveRenderTargetView(renderTarget), _clearColorHandle.AddrOfPinnedObject(),
            ResourceStateTransitionMode.ResourceStateTransitionModeTransition);
    }

    /// <summary>
    /// Draw the scene into whichever render targets are currently bound.
    /// </summary>
    internal void RenderScene(Viewport viewport, MaterialQuality materialQuality, IntRect? viewportRect = null)
    {
        ArgumentNullException.ThrowIfNull(viewport);

        if (!IsInitialized)
            return;

        var effectiveViewportRect = viewportRect ?? GetEffectiveViewportRect(viewport);
        var camera = viewport.GetCamera()!;
        var scene = viewport.GetScene()!;
        if (camera.GetAutoAspectRatio())
        {
            var aspectRatio = Math.Max((float)effectiveViewportRect.Width / Math.Max(effectiveViewportRect.Height, 1), MathDefs.Epsilon);
            camera.SetAspectRatioInternal(aspectRatio);
        }

        scene.GetComponents(_lightsScratch);
        Light? directionalLight = null;
        for (var i = 0; i < _lightsScratch.Count; ++i)
        {
            var light = _lightsScratch[i];
            if (light.Node is null || !light.IsEnabledEffective || light.GetLightType() != LightType.Directional)
                continue;

            directionalLight = light;
            break;
        }

        uint submittedDrawCalls = 0;
        uint submittedPrimitives = 0;
        var staticModels = scene.StaticModels;
        for (var staticModelIndex = 0; staticModelIndex < staticModels.Count; ++staticModelIndex)
        {
            var staticModel = staticModels[staticModelIndex];
            var (drawCalls, primitives) = RenderStaticModel(viewport, staticModel, materialQuality, directionalLight);
            submittedDrawCalls += drawCalls;
            submittedPrimitives += primitives;
        }

        if (RenderDiagnosticsEnabled)
            Log.GetLogger().LogDebug(
                "Render diagnostics: drawCalls={DrawCalls}, primitives={Primitives}, staticModels={StaticModels}.",
                submittedDrawCalls, submittedPrimitives, scene.StaticModels.Count);
    }

    internal void RenderOutlineGroups(Viewport viewport, MaterialQuality materialQuality, IReadOnlyList<OutlineGroup> outlineGroups, IntRect? viewportRect = null)
    {
        ArgumentNullException.ThrowIfNull(viewport);
        ArgumentNullException.ThrowIfNull(outlineGroups);

        if (!IsInitialized)
            return;

        var effectiveViewportRect = viewportRect ?? GetEffectiveViewportRect(viewport);
        var camera = viewport.GetCamera()!;
        var scene = viewport.GetScene()!;
        if (camera.GetAutoAspectRatio())
        {
            var aspectRatio = Math.Max((float)effectiveViewportRect.Width / Math.Max(effectiveViewportRect.Height, 1), MathDefs.Epsilon);
            camera.SetAspectRatioInternal(aspectRatio);
        }

        for (var groupIndex = 0; groupIndex < outlineGroups.Count; ++groupIndex)
        {
            var outlineGroup = outlineGroups[groupIndex];
            var drawableEnumerator = outlineGroup.GetDrawableEnumerator();
            while (drawableEnumerator.MoveNext())
            {
                var drawable = drawableEnumerator.Current;
                if (drawable is not StaticModel staticModel)
                    continue;

                RenderOutlinedStaticModel(viewport, staticModel, materialQuality, outlineGroup);
            }
        }
    }

    internal void RenderNormalBuffer(Viewport viewport, MaterialQuality materialQuality, IntRect? viewportRect = null)
    {
        ArgumentNullException.ThrowIfNull(viewport);

        if (_immediateContext is null)
            return;

        var targetRect = viewportRect ?? new IntRect(0, 0, Width, Height);
        ApplyDiligentViewport(_immediateContext, targetRect);
        var scene = viewport.GetScene()!;
        var staticModels = scene.StaticModels;
        for (var staticModelIndex = 0; staticModelIndex < staticModels.Count; ++staticModelIndex)
        {
            var staticModel = staticModels[staticModelIndex];
            RenderNormalStaticModel(viewport, staticModel, materialQuality);
        }
    }

    /// <summary>
    /// Present the current frame.
    /// </summary>
    internal void PresentFrame(Action? beforePresent = null)
    {
        if (!IsInitialized || _swapChain is null)
            return;

        beforePresent?.Invoke();

        GetSubsystem<RenderDevice>()?.Present();
    }

    internal void UnbindRenderTargets()
    {
        if (!IsInitialized || _immediateContext is null)
            return;

        _immediateContext.SetRenderTargets(0, global::System.ReadOnlySpan<ITextureView?>.Empty, null!,
            ResourceStateTransitionMode.ResourceStateTransitionModeNone);
        Array.Clear(_renderTargets);
        _currentRenderTargetCount = 0;
        _currentDepthStencilTarget = null;
        _currentColorTargetFormat = 0;
        _currentDepthTargetFormat = 0;
        _currentViewport = default;
        _currentRenderTargetSize = default;
        _isSwapChainRenderTarget = false;
        _currentOutputDesc.NumRenderTargets = 0;
        _currentOutputDesc.DepthStencilFormat = TextureFormat.TexFormatUnknown;
        _currentOutputDesc.MultiSample = 1;
    }

    public IDeviceContext? GetImmediateContext() => _immediateContext;

    internal IntRect GetCurrentViewportRect() => _currentViewport;

    internal Vector2i GetCurrentRenderTargetSize() => _currentRenderTargetSize;

    internal PipelineStateOutputDesc GetCurrentRenderTargetsDesc() => _currentOutputDesc;

    internal bool IsSwapChainRenderTarget() => _isSwapChainRenderTarget;

    internal void DrawCopyTexture(Texture sourceTexture, bool bilinearFiltering = false, bool flipVertical = false)
    {
        ArgumentNullException.ThrowIfNull(sourceTexture);

        var invInputSize = new Vector2f(
            sourceTexture.Width > 0 ? 1.0f / sourceTexture.Width : 0.0f,
            sourceTexture.Height > 0 ? 1.0f / sourceTexture.Height : 0.0f);
        DrawFullscreenQuad(
            "Copy texture to output",
            "X_CopyTexture",
            string.Empty,
            BlendMode.Replace,
            bilinearFiltering,
            sourceTexture.GetRawTexture(),
            secondaryTexture: null,
            depthTexture: null,
            EmptyFullscreenParameters,
            invInputSize,
            new Vector4f(0.5f, 0.5f, 0.5f, 0.5f),
            flipVertical);
    }

    internal bool TryCopyTextureToSwapChain(Texture sourceTexture)
    {
        ArgumentNullException.ThrowIfNull(sourceTexture);

        if (!IsInitialized || _immediateContext is null || _renderTargetView is null)
            return false;
        if (sourceTexture is not Texture2D sourceTexture2D)
            return false;

        var destinationTexture = _renderTargetView.GetTexture();
        if (destinationTexture.IsNull)
            return false;

        var uploadedSource = GetCopySourceTexture(sourceTexture2D);
        using var copyAttributes = new CopyTextureAttribs(
            uploadedSource,
            ResourceStateTransitionMode.ResourceStateTransitionModeTransition,
            destinationTexture,
            ResourceStateTransitionMode.ResourceStateTransitionModeTransition);
        _immediateContext.CopyTexture(copyAttributes);
        return true;
    }

    internal void DrawFullscreenQuad(string debugComment, string shaderName, string shaderDefines, BlendMode blendMode,
        bool bilinearColorSampler, RawTexture? albedoTexture, RawTexture? secondaryTexture, RawTexture? depthTexture,
        IReadOnlyDictionary<string, object?> parameters, Vector2f invInputSize, Vector4f clipToUvOffsetAndScale,
        bool flipVertical = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(shaderName);
        ArgumentNullException.ThrowIfNull(parameters);

        if (!IsInitialized || _immediateContext is null || _device is null || _currentRenderTargetCount == 0)
            return;

        var pipeline = GetOrCreateFullscreenPipeline(shaderName, shaderDefines, blendMode, bilinearColorSampler);
        if (pipeline is null)
            return;

        DrawFullscreenQuad(
            pipeline,
            albedoTexture,
            secondaryTexture,
            depthTexture,
            parameters,
            invInputSize,
            clipToUvOffsetAndScale,
            flipVertical);
    }

    internal void DrawFullscreenQuad<TParameters>(string debugComment, string shaderName, string shaderDefines, BlendMode blendMode,
        bool bilinearColorSampler, RawTexture? albedoTexture, RawTexture? secondaryTexture, RawTexture? depthTexture,
        in TParameters parameters, Vector2f invInputSize, Vector4f clipToUvOffsetAndScale, bool flipVertical = false)
        where TParameters : struct
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(shaderName);

        if (!IsInitialized || _immediateContext is null || _device is null || _currentRenderTargetCount == 0)
            return;

        var pipeline = GetOrCreateFullscreenPipeline(shaderName, shaderDefines, blendMode, bilinearColorSampler);
        if (pipeline is null)
            return;

        DrawFullscreenQuad(
            pipeline,
            albedoTexture,
            secondaryTexture,
            depthTexture,
            parameters,
            invInputSize,
            clipToUvOffsetAndScale,
            flipVertical);
    }

    internal void DrawClearFramebufferQuad(string debugComment, ClearTarget flags, IReadOnlyDictionary<string, object?> parameters,
        Vector2f invInputSize, Vector4f clipToUvOffsetAndScale, byte stencil = 0)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        if (!IsInitialized || _immediateContext is null || _device is null || _currentRenderTargetCount == 0)
            return;

        var pipeline = GetOrCreateClearFramebufferPipeline(flags);
        if (pipeline is null)
            return;

        if ((flags & ClearTarget.Stencil) == ClearTarget.Stencil)
            _immediateContext.SetStencilRef(stencil);

        DrawFullscreenQuad(
            pipeline,
            albedoTexture: null,
            secondaryTexture: null,
            depthTexture: null,
            parameters,
            invInputSize,
            clipToUvOffsetAndScale,
            flipVertical: false);
    }

    internal void DrawClearFramebufferQuad<TParameters>(string debugComment, ClearTarget flags, in TParameters parameters,
        Vector2f invInputSize, Vector4f clipToUvOffsetAndScale, byte stencil = 0)
        where TParameters : struct
    {
        if (!IsInitialized || _immediateContext is null || _device is null || _currentRenderTargetCount == 0)
            return;

        var pipeline = GetOrCreateClearFramebufferPipeline(flags);
        if (pipeline is null)
            return;

        if ((flags & ClearTarget.Stencil) == ClearTarget.Stencil)
            _immediateContext.SetStencilRef(stencil);

        DrawFullscreenQuad(
            pipeline,
            albedoTexture: null,
            secondaryTexture: null,
            depthTexture: null,
            parameters,
            invInputSize,
            clipToUvOffsetAndScale,
            flipVertical: false);
    }

    public void SetVSync(bool enable)
    {
        if (!IsInitialized)
            return;

#if !REDBOLT_PLATFORM_WEB
        _ = SdlNativeBindings.SdlGlSetSwapInterval(enable ? 1 : 0);
#endif
    }

    /// <summary>
    /// Take a screenshot.
    /// </summary>
    public bool TakeScreenShot(Image destImage)
    {
        ArgumentNullException.ThrowIfNull(destImage);
        if (!IsInitialized)
            return false;

        var renderDevice = GetSubsystem<RenderDevice>();
        if (renderDevice is null || !renderDevice.TakeScreenShot(out var size, out var data))
            return false;

        destImage.SetSize(size.X, size.Y, 4);
        destImage.SetData(data);
        return true;
    }

    internal void UpdateSwapChainSize()
    {
        if (!IsInitialized || _window is null || _window.IsNull || _swapChain is null || _swapChain.IsNull)
            return;

        switch (GetRenderBackend())
        {
            case RenderBackend.OpenGL:
                SdlNativeBindings.SdlGlGetDrawableSize(_window, out var drawableWidth, out var drawableHeight);
                _swapChain.Resize((uint)Math.Max(drawableWidth, 0), (uint)Math.Max(drawableHeight, 0));
                break;

#if REDBOLT_VULKAN_SUPPORTED
            case RenderBackend.Vulkan:
            {
                using var renderDeviceVulkan = IRenderDeviceVk.QueryManagedWrapper(_device!)
                    ?? throw new InvalidOperationException("Vulkan render device interface is unavailable.");
                uint surfaceWidth = 0;
                uint surfaceHeight = 0;
                if (_swapChainVulkan is not null && DiligentNativeBindings.GetVulkanSurfaceExtent(
                        renderDeviceVulkan, _swapChainVulkan, ref surfaceWidth, ref surfaceHeight))
                    _swapChain.Resize(surfaceWidth, surfaceHeight);
                else
                    Log.GetLogger().LogError("Cannot resize Vulkan swap chain");
                break;
            }
#endif
            default:
                throw new InvalidOperationException("Unsupported render backend.");
        }
        using var swapChainDesc = _swapChain.GetDesc();
        Width = (int)swapChainDesc.Width;
        Height = (int)swapChainDesc.Height;
        _renderTargetView = _swapChain.GetCurrentBackBufferRTV();
        _depthStencilView = _swapChain.GetDepthBufferDSV();
        if (_renderTargetView.IsNull || _depthStencilView.IsNull)
            throw new InvalidOperationException("Swap chain render target or depth view was null.");

        _renderTargets[0] = _renderTargetView;
        _swapChainColorFormat = GetTextureViewFormat(_renderTargetView);
        _swapChainDepthFormat = GetTextureViewFormat(_depthStencilView);
    }

    internal void UpdateWindowSettings(WindowSettings oldSettings, WindowSettings newSettings)
    {
        if (!IsInitialized || _window is null || _window.IsNull)
            return;

        var sizeChanged = oldSettings.Size != newSettings.Size;
        if (sizeChanged && newSettings.Mode == WindowMode.Windowed)
            SdlNativeBindings.SdlSetWindowSize(_window, newSettings.Size.X, newSettings.Size.Y);

        if (sizeChanged || oldSettings.Mode != newSettings.Mode || oldSettings.RefreshRate != newSettings.RefreshRate)
        {
            using var displayMode = new SdlDisplayMode();
            if (newSettings.Mode == WindowMode.Fullscreen)
            {
                displayMode.W = newSettings.Size.X;
                displayMode.H = newSettings.Size.Y;
                displayMode.RefreshRate = newSettings.RefreshRate;
                _ = SdlNativeBindings.SdlSetWindowDisplayMode(_window, displayMode);
            }

            _ = SdlNativeBindings.SdlSetWindowFullscreen(_window, 0u);
            var fullscreenFlags = newSettings.Mode switch
            {
                WindowMode.Fullscreen => (uint)SdlWindowFlags.SdlWindowFullscreen,
                WindowMode.Borderless => (uint)SdlWindowFlags.SdlWindowFullscreenDesktop,
                _ => 0u
            };
            _ = SdlNativeBindings.SdlSetWindowFullscreen(_window, fullscreenFlags);
        }

        if (oldSettings.Monitor != newSettings.Monitor)
        {
            var position = unchecked((int)(0x1FFF0000u | (uint)newSettings.Monitor));
            SdlNativeBindings.SdlSetWindowPosition(_window, position, position);
        }

        if (oldSettings.Title != newSettings.Title)
        {
            using var title = new CString(newSettings.Title);
            SdlNativeBindings.SdlSetWindowTitle(_window, title.Bytes);
        }

        if (oldSettings.Resizable != newSettings.Resizable)
            SdlNativeBindings.SdlSetWindowResizable(_window, newSettings.Resizable ? SdlBool.SdlTrue : SdlBool.SdlFalse);

#if !REDBOLT_PLATFORM_WEB
        if (oldSettings.VSync != newSettings.VSync)
            _ = SdlNativeBindings.SdlGlSetSwapInterval(newSettings.VSync ? 1 : 0);
#endif
    }

    public void Shutdown()
    {
        Width = 0;
        Height = 0;
        _renderTargets[0] = null;
        _renderTargetView = null;
        _depthStencilView = null;
        _swapChainOutputTarget = null;
        _swapChainColorFormat = 0;
        _swapChainDepthFormat = 0;
        _currentRenderTargetCount = 0;
        _currentDepthStencilTarget = null;
        _currentColorTargetFormat = 0;
        _currentDepthTargetFormat = 0;

        foreach (var uploaded in _uploadedGeometries.Values)
            uploaded.Dispose();
        _uploadedGeometries.Clear();

        foreach (var uploaded in _uploadedTextures.Values)
            uploaded.Dispose();
        _uploadedTextures.Clear();

        _defaultAlbedoTexture = null;
        _defaultNormalTexture = null;

        foreach (var pipeline in _pipelines.Values)
            pipeline.Dispose();
        _pipelines.Clear();

        foreach (var pipeline in _fullscreenPipelines.Values)
            pipeline.Dispose();
        _fullscreenPipelines.Clear();

        foreach (var pipeline in _clearFramebufferPipelines.Values)
            pipeline.Dispose();
        _clearFramebufferPipelines.Clear();

        _diligentViewport?.Dispose();
        _diligentViewport = null;

#if REDBOLT_VULKAN_SUPPORTED
        ReleaseIfNotNull(_swapChainVulkan);
        _swapChainVulkan = null;
#endif
        ReleaseIfNotNull(_swapChain);
        _swapChain = null;
        _factory = null;
        ReleaseIfNotNull(_immediateContext);
        _immediateContext = null;
        ReleaseIfNotNull(_device);
        _device = null;

        if (_clearColorHandle.IsAllocated)
            _clearColorHandle.Free();

        if (_glContext != IntPtr.Zero)
        {
            if (_window is not null && !_window.IsNull)
                _ = SdlNativeBindings.SdlGlMakeCurrent(_window, IntPtr.Zero);
            SdlNativeBindings.SdlGlDeleteContext(_glContext);
            _glContext = IntPtr.Zero;
        }

        _window = null;
        IsInitialized = false;
    }

    private (uint DrawCalls, uint Primitives) RenderStaticModel(Viewport viewport, StaticModel staticModel, MaterialQuality materialQuality, Light? directionalLight)
    {
        if (staticModel.Node is null || !staticModel.IsEnabledEffective || staticModel.GetModel() is not { } model)
            return default;

        uint drawCalls = 0;
        uint primitives = 0;
        var camera = viewport.GetCamera()!;
        var scene = viewport.GetScene()!;
        var cameraZone = FindZone(scene, camera.Node is not null ? camera.Node.WorldPosition : Vector3f.Zero);
        var drawableZone = FindZone(scene, staticModel.Node.WorldPosition);
        var drawableAmbient = drawableZone is not null ? new Vector4f(drawableZone.GetAmbientLighting(), 1.0f) : DefaultAmbient;
        for (var geometryIndex = 0u; geometryIndex < model.GetNumGeometries(); ++geometryIndex)
        {
            var geometry = model.GetGeometry(geometryIndex, 0);
            var vertexBuffer = geometry?.GetVertexBuffer(0);
            var indexBuffer = geometry?.GetIndexBuffer();
            var material = staticModel.GetMaterial(geometryIndex)
                ?? GetSubsystem<Renderer>()?.GetDefaultMaterial();
            if (geometry is null || vertexBuffer is null || indexBuffer is null || material is null)
                continue;

            var resourceCache = GetSubsystem<ResourceCache>();
            if (resourceCache is null)
                continue;

            var technique = material.FindTechnique(materialQuality);
            if (technique is null)
                continue;

            var unlitBasePass = technique.GetPass("base") ?? technique.GetPass("alpha");
            var lightPass = technique.GetPass("light");
            var litBasePass = lightPass is not null ? technique.GetPass("litbase") : null;

            var primaryPass = unlitBasePass;
            var primaryLight = (Light?)null;
            var additiveLightPass = lightPass;
            if (directionalLight is not null && lightPass is not null && litBasePass is not null && !directionalLight.IsNegative())
            {
                primaryPass = litBasePass;
                primaryLight = directionalLight;
                additiveLightPass = null;
            }

            if (primaryPass is not null)
            {
                var (passDrawCalls, passPrimitives) = RenderGeometryPass(
                    resourceCache,
                    viewport,
                    material,
                    primaryPass,
                    geometry,
                    vertexBuffer,
                    staticModel,
                    cameraZone,
                    drawableZone,
                    drawableAmbient,
                    primaryLight);
                drawCalls += passDrawCalls;
                primitives += passPrimitives;
            }

            if (directionalLight is not null && additiveLightPass is not null)
            {
                var (passDrawCalls, passPrimitives) = RenderGeometryPass(
                    resourceCache,
                    viewport,
                    material,
                    additiveLightPass,
                    geometry,
                    vertexBuffer,
                    staticModel,
                    cameraZone,
                    drawableZone,
                    drawableAmbient,
                    directionalLight);
                drawCalls += passDrawCalls;
                primitives += passPrimitives;
            }
        }

        return (drawCalls, primitives);
    }

    private (uint DrawCalls, uint Primitives) RenderOutlinedStaticModel(Viewport viewport, StaticModel staticModel,
        MaterialQuality materialQuality, OutlineGroup outlineGroup)
    {
        if (staticModel.Node is null || !staticModel.IsEnabledEffective || staticModel.GetModel() is not { } model)
            return default;

        uint drawCalls = 0;
        uint primitives = 0;
        var camera = viewport.GetCamera()!;
        var scene = viewport.GetScene()!;
        var cameraZone = FindZone(scene, camera.Node is not null ? camera.Node.WorldPosition : Vector3f.Zero);
        var drawableZone = FindZone(scene, staticModel.Node.WorldPosition);
        var drawableAmbient = drawableZone is not null ? new Vector4f(drawableZone.GetAmbientLighting(), 1.0f) : DefaultAmbient;
        for (var geometryIndex = 0u; geometryIndex < model.GetNumGeometries(); ++geometryIndex)
        {
            var geometry = model.GetGeometry(geometryIndex, 0);
            var vertexBuffer = geometry?.GetVertexBuffer(0);
            var material = staticModel.GetMaterial(geometryIndex)
                ?? GetSubsystem<Renderer>()?.GetDefaultMaterial();
            if (geometry is null || vertexBuffer is null || material is null)
                continue;

            var resourceCache = GetSubsystem<ResourceCache>();
            if (resourceCache is null)
                continue;

            var outlineMaterial = outlineGroup.GetOutlineMaterial(material);
            var technique = outlineMaterial.FindTechnique(materialQuality);
            var pass = technique is not null ? GetFirstOutlinePass(technique) : null;
            if (pass is null)
                continue;

            var (passDrawCalls, passPrimitives) = RenderGeometryPass(
                resourceCache,
                viewport,
                outlineMaterial,
                pass,
                geometry,
                vertexBuffer,
                staticModel,
                cameraZone,
                drawableZone,
                drawableAmbient,
                light: null);
            drawCalls += passDrawCalls;
            primitives += passPrimitives;
        }

        return (drawCalls, primitives);
    }

    private (uint DrawCalls, uint Primitives) RenderNormalStaticModel(Viewport viewport, StaticModel staticModel,
        MaterialQuality materialQuality)
    {
        if (staticModel.Node is null || !staticModel.IsEnabledEffective || staticModel.GetModel() is not { } model)
            return default;

        uint drawCalls = 0;
        uint primitives = 0;
        var camera = viewport.GetCamera()!;
        var scene = viewport.GetScene()!;
        var cameraZone = FindZone(scene, camera.Node is not null ? camera.Node.WorldPosition : Vector3f.Zero);
        var drawableZone = FindZone(scene, staticModel.Node.WorldPosition);
        var drawableAmbient = drawableZone is not null ? new Vector4f(drawableZone.GetAmbientLighting(), 1.0f) : DefaultAmbient;

        for (var geometryIndex = 0u; geometryIndex < model.GetNumGeometries(); ++geometryIndex)
        {
            var geometry = model.GetGeometry(geometryIndex, 0);
            var vertexBuffer = geometry?.GetVertexBuffer(0);
            var material = staticModel.GetMaterial(geometryIndex)
                ?? GetSubsystem<Renderer>()?.GetDefaultMaterial();
            if (geometry is null || vertexBuffer is null || material is null)
                continue;

            if ((vertexBuffer.GetElementMask() & VertexMask.Normal) == 0)
                continue;

            var resourceCache = GetSubsystem<ResourceCache>();
            if (resourceCache is null)
                continue;

            var technique = material.FindTechnique(materialQuality);
            var referencePass = technique is not null ? GetFirstNormalBufferPass(technique) : null;
            if (referencePass is null)
                continue;

            var normalPass = GetOrCreateNormalBufferPass(referencePass);
            var (passDrawCalls, passPrimitives) = RenderGeometryPass(
                resourceCache,
                viewport,
                material,
                normalPass,
                geometry,
                vertexBuffer,
                staticModel,
                cameraZone,
                drawableZone,
                drawableAmbient,
                light: null);
            drawCalls += passDrawCalls;
            primitives += passPrimitives;
        }

        return (drawCalls, primitives);
    }

    private (uint DrawCalls, uint Primitives) RenderGeometryPass(ResourceCache resourceCache, Viewport viewport, Material material,
        Pass pass, Geometry geometry, VertexBuffer vertexBuffer, StaticModel staticModel, Zone? cameraZone, Zone? drawableZone,
        Vector4f drawableAmbient, Light? light)
    {
        var linearColorSpace = viewport.GetRenderPipelineView()?.IsLinearColorSpace ?? false;
        var camera = viewport.GetCamera()!;
        var pipeline = GetOrCreatePipeline(resourceCache, camera, material, pass, vertexBuffer, light, linearColorSpace);
        var uploadedGeometry = GetOrCreateUploadedGeometry(geometry);
        if (pipeline is null || uploadedGeometry is null || _immediateContext is null)
            return default;

        UpdateCameraBuffer(pipeline.CameraBuffer, camera, cameraZone, linearColorSpace);
        var effectiveAmbient = linearColorSpace
            ? drawableAmbient
            : new Color(drawableAmbient.X, drawableAmbient.Y, drawableAmbient.Z, drawableAmbient.W).LinearToGamma().ToVector4();
        UpdateObjectBuffer(pipeline.ObjectBuffer, staticModel.Node.GetWorldTransform().Matrix4, effectiveAmbient);
        UpdateMaterialBuffer(pipeline.MaterialBuffer, material);
        if (pipeline.CustomBuffer is not null && pipeline.CustomLayout.Size > 0)
            UpdateMaterialCustomBuffer(pipeline.CustomBuffer, pipeline.CustomLayout, pipeline.CustomParameterScratch, material);
        UpdateLightBuffer(pipeline.LightBuffer, light);
        UpdateZoneBuffer(pipeline.ZoneBuffer, drawableZone);

        BindDynamicBuffer(pipeline, global::RedBolt.Bindings.Diligent.ShaderType.ShaderTypeVertex,
            "Camera", pipeline.CameraBuffer, CameraBufferSize);
        BindDynamicBuffer(pipeline, global::RedBolt.Bindings.Diligent.ShaderType.ShaderTypePixel,
            "Camera", pipeline.CameraBuffer, CameraBufferSize);
        BindDynamicBuffer(pipeline, global::RedBolt.Bindings.Diligent.ShaderType.ShaderTypeVertex,
            "Zone", pipeline.ZoneBuffer, ZoneBufferSize);
        BindDynamicBuffer(pipeline, global::RedBolt.Bindings.Diligent.ShaderType.ShaderTypePixel,
            "Zone", pipeline.ZoneBuffer, ZoneBufferSize);
        BindDynamicBuffer(pipeline, global::RedBolt.Bindings.Diligent.ShaderType.ShaderTypeVertex,
            "Object", pipeline.ObjectBuffer, ObjectBufferSize);
        BindDynamicBuffer(pipeline, global::RedBolt.Bindings.Diligent.ShaderType.ShaderTypeVertex,
            "Material", pipeline.MaterialBuffer, MaterialBufferSize);
        BindDynamicBuffer(pipeline, global::RedBolt.Bindings.Diligent.ShaderType.ShaderTypePixel,
            "Material", pipeline.MaterialBuffer, MaterialBufferSize);
        if (pipeline.CustomBuffer is not null)
        {
            BindDynamicBuffer(pipeline, global::RedBolt.Bindings.Diligent.ShaderType.ShaderTypeVertex,
                "Custom", pipeline.CustomBuffer, pipeline.CustomLayout.Size);
            BindDynamicBuffer(pipeline, global::RedBolt.Bindings.Diligent.ShaderType.ShaderTypePixel,
                "Custom", pipeline.CustomBuffer, pipeline.CustomLayout.Size);
        }
        BindDynamicBuffer(pipeline, global::RedBolt.Bindings.Diligent.ShaderType.ShaderTypeVertex,
            "Light", pipeline.LightBuffer, LightBufferSize);
        BindDynamicBuffer(pipeline, global::RedBolt.Bindings.Diligent.ShaderType.ShaderTypePixel,
            "Light", pipeline.LightBuffer, LightBufferSize);
        BindMaterialTextures(pipeline, material);

        _immediateContext.SetPipelineState(pipeline.PipelineState);
        _immediateContext.CommitShaderResources(pipeline.ShaderResources, ResourceStateTransitionMode.ResourceStateTransitionModeTransition);

        _singleVertexBuffer[0] = uploadedGeometry.VertexBuffer;
        _immediateContext.SetVertexBuffers(0, 1, _singleVertexBuffer, _singleVertexBufferOffset,
            ResourceStateTransitionMode.ResourceStateTransitionModeTransition,
            SetVertexBuffersFlags.SetVertexBuffersFlagReset);
        _immediateContext.SetIndexBuffer(uploadedGeometry.IndexBuffer, 0,
            ResourceStateTransitionMode.ResourceStateTransitionModeTransition);
        _immediateContext.DrawIndexed(uploadedGeometry.DrawIndexedAttribs);
        return (1u, geometry.GetPrimitiveCount());
    }

    private PipelineResources? GetOrCreatePipeline(ResourceCache resourceCache, Camera camera, Material material, Pass pass,
        VertexBuffer vertexBuffer, Light? light, bool linearColorSpace)
    {
        var vertexDefines = ComposeShaderDefines(pass, vertexBuffer, material, light, linearColorSpace, ShaderStage.Vertex);
        var pixelDefines = ComposeShaderDefines(pass, vertexBuffer, material, light, linearColorSpace, ShaderStage.Pixel);
        var effectiveCullMode = pass.CullMode != CullMode.Max ? pass.CullMode : material.CullMode;
        var effectiveFillMode = GetEffectiveFillMode(camera, material);
        var reverseCulling = camera.GetReverseCulling();
        var alphaToCoverage = pass.AlphaToCoverage || material.GetAlphaToCoverage();
        var colorFormat = _currentColorTargetFormat != 0 ? _currentColorTargetFormat : GetSwapChainColorFormat();
        var depthFormat = _currentDepthTargetFormat != 0 ? _currentDepthTargetFormat : GetSwapChainDepthFormat();
        var key = new PipelineKey(pass.VertexShader, pass.PixelShader, vertexDefines, pixelDefines,
            material, BuildLayoutSignature(vertexBuffer), effectiveCullMode, effectiveFillMode, pass.DepthTestMode,
            pass.DepthWrite, alphaToCoverage, reverseCulling, colorFormat, depthFormat);
        if (_pipelines.TryGetValue(key, out var cached))
            return cached;

        if (_device is null)
            return null;

        var vertexShaderResource = resourceCache.GetResourceOrNull<Shader>(pass.VertexShader);
        var pixelShaderResource = resourceCache.GetResourceOrNull<Shader>(pass.PixelShader);
        if (vertexShaderResource is null || pixelShaderResource is null)
            return null;

        // C++ RawShader uses SPIR-V bytecode on Vulkan/D3D and GLSL verbatim on OpenGL.
        // Use ShaderVariation (same path as fullscreen) instead of CompileShader from GLSL source.
        var vertexVariation = vertexShaderResource.GetVariation(ShaderType.VS, vertexDefines);
        var pixelVariation = pixelShaderResource.GetVariation(ShaderType.PS, pixelDefines);
        var vertexShader = vertexVariation.GetHandle();
        var pixelShader = pixelVariation.GetHandle();
        if (vertexShader is null || vertexShader.IsNull || pixelShader is null || pixelShader.IsNull)
            return null;

        TemporaryLayoutArray? layoutArray = null;
        nint temporaryGlProgram = 0;
        try
        {
            var isOpenGl = GetSubsystem<RenderDevice>()?.GetBackend() == RenderBackend.OpenGL;
            var hasSeparableShaderPrograms = _device.GetDeviceInfo().FeaturesSeparablePrograms
                != DeviceFeatureState.DeviceFeatureStateDisabled;
            var vertexAttributes = vertexVariation.GetBytecode().VertexAttributes;
            if (isOpenGl)
            {
                temporaryGlProgram = DiligentNativeBindings.CreateTemporaryGLProgram(
                    vertexShader, pixelShader, null!, null!, null!, hasSeparableShaderPrograms);
                vertexAttributes = [];
                var numAttributes = DiligentNativeBindings.GetTemporaryGLProgramVertexAttributeCount(temporaryGlProgram);
                for (var i = 0u; i < numAttributes; ++i)
                {
                    var attributeName = System.Text.Encoding.UTF8.GetString(
                        DiligentNativeBindings.GetTemporaryGLProgramVertexAttributeName(temporaryGlProgram, i));
                    var attribute = RenderAPIUtils.ParseVertexAttribute(attributeName);
                    if (attribute is not null)
                    {
                        vertexAttributes.Add(attribute.Value with
                        {
                            InputIndex = checked((int)DiligentNativeBindings.GetTemporaryGLProgramVertexAttributeLocation(
                                temporaryGlProgram, i))
                        });
                    }
                }

            }

            var reflection = isOpenGl && !hasSeparableShaderPrograms
                ? new ShaderProgramReflection(temporaryGlProgram)
                : new ShaderProgramReflection([vertexVariation, pixelVariation]);
            var customLayout = CreateCustomUniformLayout(reflection);
            var samplerOverrides = new ImmutableSamplersDesc();
            var textureEnumerator = material.EnumerateTextures();
            while (textureEnumerator.MoveNext())
            {
                var pair = textureEnumerator.Current;
                samplerOverrides.Add(pair.Key, pair.Value.Texture.GetSamplerStateDesc());
            }

            var layoutElements = BuildLayoutElements(vertexBuffer);
            layoutElements = ApplyVertexAttributeLocations(layoutElements, vertexAttributes);
            layoutArray = new TemporaryLayoutArray(layoutElements);
            using var samplers = new TemporaryImmutableSamplers(samplerOverrides, reflection,
                GetSubsystem<RenderDevice>(), global::RedBolt.Bindings.Diligent.ShaderType.ShaderTypeAllGraphics);
            using var pipelineName = new CString($"{pass.VertexShader}:{pass.PixelShader}:{key.LayoutSignature}");
            using var psoDesc = new global::RedBolt.Bindings.Diligent.PipelineStateDesc
            {
                Name = pipelineName.Pointer,
                PipelineType = PipelineType.PipelineTypeGraphics,
                ResourceLayoutDefaultVariableType = ShaderResourceVariableType.ShaderResourceVariableTypeDynamic,
                ResourceLayoutImmutableSamplers = samplers.First!,
                ResourceLayoutNumImmutableSamplers = samplers.Count
            };
            using var psoCreateInfo = new GraphicsPipelineStateCreateInfo
            {
                PSODesc = psoDesc,
                PVS = vertexShader,
                PPS = pixelShader,
                GraphicsPipelineBlendDescAlphaToCoverageEnable = alphaToCoverage,
                GraphicsPipelineNumRenderTargets = 1,
                GraphicsPipelineNumViewports = 1,
                GraphicsPipelinePrimitiveTopology = PrimitiveTopology.PrimitiveTopologyTriangleList,
                GraphicsPipelineRasterizerDescCullMode = MapCullMode(effectiveCullMode, reverseCulling),
                GraphicsPipelineRasterizerDescFillMode = MapFillMode(effectiveFillMode),
                GraphicsPipelineRasterizerDescFrontCounterClockwise = false,
                GraphicsPipelineDepthStencilDescDepthEnable = pass.DepthTestMode != CompareMode.Always || pass.DepthWrite,
                GraphicsPipelineDepthStencilDescDepthWriteEnable = pass.DepthWrite,
                GraphicsPipelineDepthStencilDescDepthFunc = MapCompareMode(pass.DepthTestMode),
                GraphicsPipelineInputLayoutLayoutElements = layoutArray.First,
                GraphicsPipelineInputLayoutNumElements = (uint)layoutArray.Count
            };

            psoCreateInfo.GraphicsPipelineRTVFormats[0] = colorFormat;
            psoCreateInfo.GraphicsPipelineDSVFormat = depthFormat;

            _device.CreateGraphicsPipelineState(psoCreateInfo, out var pipelineState);
            if (pipelineState.IsNull)
                throw new InvalidOperationException($"Failed to create pipeline state for {pass.VertexShader}/{pass.PixelShader}.");
            if (isOpenGl &&
                !DiligentNativeBindings.BindTemporaryGLProgramVertexAttributes(temporaryGlProgram, pipelineState))
                throw new InvalidOperationException($"Failed to bind OpenGL vertex attributes for {pass.VertexShader}/{pass.PixelShader}.");

            var cameraBuffer = CreateUniformBuffer(_device, "Camera", CameraBufferSize);
            var objectBuffer = CreateUniformBuffer(_device, "Object", ObjectBufferSize);
            var materialBuffer = CreateUniformBuffer(_device, "Material", MaterialBufferSize);
            var customBuffer = customLayout.Size > 0u ? CreateUniformBuffer(_device, "Custom", customLayout.Size) : null;
            var lightBuffer = CreateUniformBuffer(_device, "Light", LightBufferSize);
            var zoneBuffer = CreateUniformBuffer(_device, "Zone", ZoneBufferSize);
            pipelineState.CreateShaderResourceBinding(out var shaderResources, true);
            if (shaderResources.IsNull)
                throw new InvalidOperationException($"Failed to create shader resources for {pass.VertexShader}/{pass.PixelShader}.");

            DumpFirstPipelineDiagnostics(pass, key, vertexShaderResource, pixelShaderResource, vertexDefines, pixelDefines,
                vertexShader, pixelShader, pipelineState, shaderResources);

            // Keep variation GPU handles alive while the pipeline owns them.
            _ = vertexShader.AddRef();
            _ = pixelShader.AddRef();
            cached = new PipelineResources(pipelineState, vertexShader, pixelShader, cameraBuffer, objectBuffer, materialBuffer,
                customBuffer, customLayout, lightBuffer, zoneBuffer, shaderResources,
                BuildShaderVariableLookup(shaderResources, reflection), layoutArray);
            layoutArray = null;
            _pipelines[key] = cached;
            return cached;
        }
        finally
        {
            layoutArray?.Dispose();
            if (temporaryGlProgram != 0)
                DiligentNativeBindings.DeleteTemporaryGLProgram(temporaryGlProgram);
        }
    }

    private FullscreenPipelineResources? GetOrCreateFullscreenPipeline(string shaderName, string shaderDefines,
        BlendMode blendMode, bool bilinearColorSampler)
    {
        if (_device is null)
            return null;

        var effectiveShaderDefines = GetOrCreateFullscreenShaderDefines(shaderDefines);
        var key = new FullscreenPipelineKey(
            shaderName,
            effectiveShaderDefines,
            blendMode,
            bilinearColorSampler,
            _currentColorTargetFormat,
            _currentDepthTargetFormat);
        if (_fullscreenPipelines.TryGetValue(key, out var cached))
            return cached;

        var resourceCache = GetSubsystem<ResourceCache>();
        var shaderResource = resourceCache?.GetResourceOrNull<Shader>(shaderName);
        if (shaderResource is null)
            return null;

        var vertexVariation = shaderResource.GetVariation(ShaderType.VS, effectiveShaderDefines);
        var pixelVariation = shaderResource.GetVariation(ShaderType.PS, effectiveShaderDefines);
        var vertexShader = vertexVariation.GetHandle();
        var pixelShader = pixelVariation.GetHandle();
        if (vertexShader is null || vertexShader.IsNull || pixelShader is null || pixelShader.IsNull)
            return null;
        var reflection = new ShaderProgramReflection([vertexVariation, pixelVariation]);
        var customLayout = CreateCustomUniformLayout(reflection);
        var samplerOverrides = new ImmutableSamplersDesc();
        if (bilinearColorSampler)
            samplerOverrides.Add(AlbedoTextureSlot, SamplerStateDesc.Bilinear());

        TemporaryLayoutArray? layoutArray = null;
        try
        {
            var quadGeometry = GetOrCreateFullscreenQuadGeometry();
            var vertexBuffer = quadGeometry.GetVertexBuffer(0) ?? throw new InvalidOperationException("Fullscreen quad geometry has no vertex buffer.");
            var layoutElements = BuildLayoutElements(vertexBuffer);
            var vertexAttributes = vertexVariation.GetBytecode().VertexAttributes;
            for (var i = 0; i < vertexAttributes.Count; ++i)
            {
                if (vertexAttributes[i].Semantic == VertexElementSemantic.Position &&
                    vertexAttributes[i].SemanticIndex == 0)
                {
                    layoutElements[0] = layoutElements[0] with { InputIndex = vertexAttributes[i].InputIndex };
                    break;
                }
            }
            layoutArray = new TemporaryLayoutArray(layoutElements);
            using var samplers = new TemporaryImmutableSamplers(samplerOverrides, reflection,
                GetSubsystem<RenderDevice>(), global::RedBolt.Bindings.Diligent.ShaderType.ShaderTypeAllGraphics);
            using var pipelineName = new CString($"{shaderName}:{shaderDefines}:fullscreen");
            using var psoDesc = new global::RedBolt.Bindings.Diligent.PipelineStateDesc
            {
                Name = pipelineName.Pointer,
                PipelineType = PipelineType.PipelineTypeGraphics,
                ResourceLayoutDefaultVariableType = ShaderResourceVariableType.ShaderResourceVariableTypeDynamic,
                ResourceLayoutImmutableSamplers = samplers.First!,
                ResourceLayoutNumImmutableSamplers = samplers.Count
            };
            using var psoCreateInfo = new GraphicsPipelineStateCreateInfo
            {
                PSODesc = psoDesc,
                PVS = vertexShader,
                PPS = pixelShader,
                GraphicsPipelineNumRenderTargets = 1,
                GraphicsPipelineNumViewports = 1,
                GraphicsPipelinePrimitiveTopology = PrimitiveTopology.PrimitiveTopologyTriangleList,
                GraphicsPipelineRasterizerDescCullMode = RedBolt.Bindings.Diligent.CullMode.CullModeNone,
                GraphicsPipelineRasterizerDescFillMode = RedBolt.Bindings.Diligent.FillMode.FillModeSolid,
                GraphicsPipelineRasterizerDescFrontCounterClockwise = false,
                GraphicsPipelineDepthStencilDescDepthEnable = false,
                GraphicsPipelineDepthStencilDescDepthWriteEnable = false,
                GraphicsPipelineDepthStencilDescDepthFunc = ComparisonFunction.ComparisonFuncAlways,
                GraphicsPipelineInputLayoutLayoutElements = layoutArray.First,
                GraphicsPipelineInputLayoutNumElements = (uint)layoutArray.Count
            };

            psoCreateInfo.GraphicsPipelineRTVFormats[0] = _currentColorTargetFormat;
            psoCreateInfo.GraphicsPipelineDSVFormat = _currentDepthTargetFormat;
            ConfigureBlendState(psoCreateInfo, blendMode, colorWriteEnabled: true);

            _device.CreateGraphicsPipelineState(psoCreateInfo, out var pipelineState);
            if (pipelineState.IsNull)
                throw new InvalidOperationException($"Failed to create fullscreen pipeline state for {shaderName}.");

            var frameBuffer = CreateUniformBuffer(_device, "Frame", FrameBufferSize);
            var cameraBuffer = CreateUniformBuffer(_device, "Camera", CameraBufferSize);
            var objectBuffer = CreateUniformBuffer(_device, "Object", ObjectBufferSize);
            var customBuffer = customLayout.Size > 0u ? CreateUniformBuffer(_device, "Custom", customLayout.Size) : null;

            pipelineState.CreateShaderResourceBinding(out var shaderResources, true);
            if (shaderResources.IsNull)
                throw new InvalidOperationException($"Failed to create fullscreen shader resources for {shaderName}.");

            cached = new FullscreenPipelineResources(
                pipelineState,
                vertexShader,
                pixelShader,
                shaderResources,
                BuildShaderVariableLookup(shaderResources, reflection),
                frameBuffer,
                cameraBuffer,
                objectBuffer,
                customBuffer,
                customLayout,
                layoutArray);
            _ = vertexShader.AddRef();
            _ = pixelShader.AddRef();
            layoutArray = null;
            _fullscreenPipelines[key] = cached;
            return cached;
        }
        finally
        {
            layoutArray?.Dispose();
        }
    }

    private FullscreenPipelineResources? GetOrCreateClearFramebufferPipeline(ClearTarget flags)
    {
        if (_device is null)
            return null;

        flags &= ClearTarget.Color | ClearTarget.Depth | ClearTarget.Stencil;
        var key = new ClearFramebufferPipelineKey(flags, _currentColorTargetFormat, _currentDepthTargetFormat);
        if (_clearFramebufferPipelines.TryGetValue(key, out var cached))
            return cached;

        const string shaderName = "X_ClearFramebuffer";
        const string shaderDefines = "URHO3D_GEOMETRY_STATIC";
        var resourceCache = GetSubsystem<ResourceCache>();
        var shaderResource = resourceCache?.GetResourceOrNull<Shader>(shaderName);
        if (shaderResource is null)
            return null;

        var vertexVariation = shaderResource.GetVariation(ShaderType.VS, shaderDefines);
        var pixelVariation = shaderResource.GetVariation(ShaderType.PS, shaderDefines);
        var vertexShader = vertexVariation.GetHandle();
        var pixelShader = pixelVariation.GetHandle();
        if (vertexShader is null || vertexShader.IsNull || pixelShader is null || pixelShader.IsNull)
            return null;
        var reflection = new ShaderProgramReflection([vertexVariation, pixelVariation]);
        var customLayout = CreateCustomUniformLayout(reflection);
        var settings = GetClearFramebufferPipelineSettings(flags);

        TemporaryLayoutArray? layoutArray = null;
        try
        {
            var quadGeometry = GetOrCreateFullscreenQuadGeometry();
            var vertexBuffer = quadGeometry.GetVertexBuffer(0) ?? throw new InvalidOperationException("Fullscreen quad geometry has no vertex buffer.");
            var layoutElements = BuildLayoutElements(vertexBuffer);
            ApplyVertexAttributeLocations(layoutElements, vertexVariation.GetBytecode().VertexAttributes);
            layoutArray = new TemporaryLayoutArray(layoutElements);
            using var pipelineName = new CString($"{shaderName}:{flags}:clear");
            using var psoDesc = new global::RedBolt.Bindings.Diligent.PipelineStateDesc
            {
                Name = pipelineName.Pointer,
                PipelineType = PipelineType.PipelineTypeGraphics,
                ResourceLayoutDefaultVariableType = ShaderResourceVariableType.ShaderResourceVariableTypeDynamic
            };
            using var psoCreateInfo = new GraphicsPipelineStateCreateInfo
            {
                PSODesc = psoDesc,
                PVS = vertexShader,
                PPS = pixelShader,
                GraphicsPipelineNumRenderTargets = 1,
                GraphicsPipelineNumViewports = 1,
                GraphicsPipelinePrimitiveTopology = PrimitiveTopology.PrimitiveTopologyTriangleList,
                GraphicsPipelineRasterizerDescCullMode = RedBolt.Bindings.Diligent.CullMode.CullModeNone,
                GraphicsPipelineRasterizerDescFillMode = RedBolt.Bindings.Diligent.FillMode.FillModeSolid,
                GraphicsPipelineRasterizerDescFrontCounterClockwise = false,
                GraphicsPipelineDepthStencilDescDepthEnable = true,
                GraphicsPipelineDepthStencilDescDepthWriteEnable = settings.DepthWriteEnabled,
                GraphicsPipelineDepthStencilDescDepthFunc = ComparisonFunction.ComparisonFuncAlways,
                GraphicsPipelineDepthStencilDescStencilEnable = settings.StencilEnabled,
                GraphicsPipelineDepthStencilDescStencilReadMask = settings.StencilReadMask,
                GraphicsPipelineDepthStencilDescStencilWriteMask = settings.StencilWriteMask,
                GraphicsPipelineDepthStencilDescFrontFaceStencilFailOp = global::RedBolt.Bindings.Diligent.StencilOp.StencilOpKeep,
                GraphicsPipelineDepthStencilDescFrontFaceStencilDepthFailOp = global::RedBolt.Bindings.Diligent.StencilOp.StencilOpKeep,
                GraphicsPipelineDepthStencilDescFrontFaceStencilPassOp = settings.StencilPassOp,
                GraphicsPipelineDepthStencilDescFrontFaceStencilFunc = ComparisonFunction.ComparisonFuncAlways,
                GraphicsPipelineDepthStencilDescBackFaceStencilFailOp = global::RedBolt.Bindings.Diligent.StencilOp.StencilOpKeep,
                GraphicsPipelineDepthStencilDescBackFaceStencilDepthFailOp = global::RedBolt.Bindings.Diligent.StencilOp.StencilOpKeep,
                GraphicsPipelineDepthStencilDescBackFaceStencilPassOp = settings.StencilPassOp,
                GraphicsPipelineDepthStencilDescBackFaceStencilFunc = ComparisonFunction.ComparisonFuncAlways,
                GraphicsPipelineInputLayoutLayoutElements = layoutArray.First,
                GraphicsPipelineInputLayoutNumElements = (uint)layoutArray.Count
            };

            psoCreateInfo.GraphicsPipelineRTVFormats[0] = _currentColorTargetFormat;
            psoCreateInfo.GraphicsPipelineDSVFormat = _currentDepthTargetFormat;
            ConfigureBlendState(psoCreateInfo, BlendMode.Replace, settings.ColorWriteEnabled);

            _device.CreateGraphicsPipelineState(psoCreateInfo, out var pipelineState);
            if (pipelineState.IsNull)
                throw new InvalidOperationException($"Failed to create clear framebuffer pipeline state for {shaderName}.");

            var frameBuffer = CreateUniformBuffer(_device, "Frame", FrameBufferSize);
            var cameraBuffer = CreateUniformBuffer(_device, "Camera", CameraBufferSize);
            var objectBuffer = CreateUniformBuffer(_device, "Object", ObjectBufferSize);
            var customBuffer = customLayout.Size > 0u ? CreateUniformBuffer(_device, "Custom", customLayout.Size) : null;

            pipelineState.CreateShaderResourceBinding(out var shaderResources, true);
            if (shaderResources.IsNull)
                throw new InvalidOperationException($"Failed to create clear framebuffer shader resources for {shaderName}.");

            _ = vertexShader.AddRef();
            _ = pixelShader.AddRef();
            cached = new FullscreenPipelineResources(
                pipelineState,
                vertexShader,
                pixelShader,
                shaderResources,
                BuildShaderVariableLookup(shaderResources, reflection),
                frameBuffer,
                cameraBuffer,
                objectBuffer,
                customBuffer,
                customLayout,
                layoutArray);
            layoutArray = null;
            _clearFramebufferPipelines[key] = cached;
            return cached;
        }
        finally
        {
            layoutArray?.Dispose();
        }
    }

    private Geometry GetOrCreateFullscreenQuadGeometry()
    {
        return GetSubsystem<Renderer>()?.GetQuadGeometry()
            ?? throw new InvalidOperationException("Renderer quad geometry is not initialized.");
    }

    private void UpdateFullscreenFrameBuffer(IBuffer buffer)
    {
        var time = GetSubsystem<Time>();
        Span<byte> data = stackalloc byte[FrameBufferSize];
        data.Clear();
        WriteFloat(data, 0, time?.TimeStep ?? 0.0f);
        WriteFloat(data, 4, (float)(time?.ElapsedTime ?? 0.0));
        UpdateBuffer(buffer, data);
    }

    private void UpdateFullscreenCameraBuffer(IBuffer buffer, Vector4f clipToUvOffsetAndScale, Vector2f invInputSize, bool flipVertical)
    {
        Span<byte> data = stackalloc byte[CameraBufferSize];
        WriteFullscreenCameraBufferData(data, clipToUvOffsetAndScale, invInputSize, flipVertical);
        UpdateBuffer(buffer, data);
    }

    private void UpdateFullscreenObjectBuffer(IBuffer buffer)
    {
        Span<byte> data = stackalloc byte[ObjectBufferSize];
        var modelMatrix = Matrix4.Identity;
        modelMatrix.M23 = GetSubsystem<RenderDevice>()?.GetBackend() == RenderBackend.OpenGL ? 0.0f : 0.5f;
        WriteMatrix4Raw(data, 0, modelMatrix);
        WriteVector4(data, 64, DefaultAmbient);
        UpdateBuffer(buffer, data);
    }

    private void DrawFullscreenQuad(FullscreenPipelineResources pipeline, RawTexture? albedoTexture, RawTexture? secondaryTexture, RawTexture? depthTexture,
        IReadOnlyDictionary<string, object?> parameters, Vector2f invInputSize, Vector4f clipToUvOffsetAndScale, bool flipVertical)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        if (_immediateContext is null)
            return;

        var uploadedGeometry = GetOrCreateUploadedGeometry(GetOrCreateFullscreenQuadGeometry());
        if (uploadedGeometry is null)
            return;

        UpdateFullscreenFrameBuffer(pipeline.FrameBuffer);
        UpdateFullscreenCameraBuffer(pipeline.CameraBuffer, clipToUvOffsetAndScale, invInputSize, flipVertical);
        UpdateFullscreenObjectBuffer(pipeline.ObjectBuffer);
        if (pipeline.CustomBuffer is not null && pipeline.CustomLayout.Size > 0)
            UpdateFullscreenCustomBuffer(pipeline.CustomBuffer, pipeline.CustomLayout, pipeline.CustomParameterScratch, parameters);
        DrawFullscreenQuadCore(pipeline, uploadedGeometry, albedoTexture, secondaryTexture, depthTexture);
    }

    private void DrawFullscreenQuad<TParameters>(FullscreenPipelineResources pipeline, RawTexture? albedoTexture, RawTexture? secondaryTexture,
        RawTexture? depthTexture, in TParameters parameters, Vector2f invInputSize, Vector4f clipToUvOffsetAndScale, bool flipVertical)
        where TParameters : struct
    {
        if (_immediateContext is null)
            return;

        var uploadedGeometry = GetOrCreateUploadedGeometry(GetOrCreateFullscreenQuadGeometry());
        if (uploadedGeometry is null)
            return;

        UpdateFullscreenFrameBuffer(pipeline.FrameBuffer);
        UpdateFullscreenCameraBuffer(pipeline.CameraBuffer, clipToUvOffsetAndScale, invInputSize, flipVertical);
        UpdateFullscreenObjectBuffer(pipeline.ObjectBuffer);
        if (pipeline.CustomBuffer is not null && pipeline.CustomLayout.Size > 0)
            UpdateFullscreenCustomBuffer(pipeline.CustomBuffer, pipeline.CustomLayout, pipeline.CustomParameterScratch, parameters);
        DrawFullscreenQuadCore(pipeline, uploadedGeometry, albedoTexture, secondaryTexture, depthTexture);
    }

    private void DrawFullscreenQuadCore(FullscreenPipelineResources pipeline, UploadedGeometry uploadedGeometry,
        RawTexture? albedoTexture, RawTexture? secondaryTexture, RawTexture? depthTexture)
    {
        if (_immediateContext is null)
            return;

        BindDynamicBuffer(pipeline, global::RedBolt.Bindings.Diligent.ShaderType.ShaderTypeVertex, "Frame", pipeline.FrameBuffer, FrameBufferSize);
        BindDynamicBuffer(pipeline, global::RedBolt.Bindings.Diligent.ShaderType.ShaderTypePixel, "Frame", pipeline.FrameBuffer, FrameBufferSize);
        BindDynamicBuffer(pipeline, global::RedBolt.Bindings.Diligent.ShaderType.ShaderTypeVertex, "Camera", pipeline.CameraBuffer, CameraBufferSize);
        BindDynamicBuffer(pipeline, global::RedBolt.Bindings.Diligent.ShaderType.ShaderTypePixel, "Camera", pipeline.CameraBuffer, CameraBufferSize);
        BindDynamicBuffer(pipeline, global::RedBolt.Bindings.Diligent.ShaderType.ShaderTypeVertex, "Object", pipeline.ObjectBuffer, ObjectBufferSize);
        BindDynamicBuffer(pipeline, global::RedBolt.Bindings.Diligent.ShaderType.ShaderTypePixel, "Object", pipeline.ObjectBuffer, ObjectBufferSize);
        if (pipeline.CustomBuffer is not null)
        {
            BindDynamicBuffer(pipeline, global::RedBolt.Bindings.Diligent.ShaderType.ShaderTypeVertex, "Custom", pipeline.CustomBuffer, pipeline.CustomLayout.Size);
            BindDynamicBuffer(pipeline, global::RedBolt.Bindings.Diligent.ShaderType.ShaderTypePixel, "Custom", pipeline.CustomBuffer, pipeline.CustomLayout.Size);
        }

        if (albedoTexture?.GetHandles().ShaderResourceView is { } albedoView)
            BindTextureToAvailableStages(pipeline, "sAlbedo", albedoView);
        if (secondaryTexture?.GetHandles().ShaderResourceView is { } secondaryView)
            BindTextureToAvailableStages(pipeline, "sNormal", secondaryView);
        if (depthTexture?.GetHandles().ShaderResourceView is { } depthView)
            BindTextureToAvailableStages(pipeline, "sDepthBuffer", depthView);

        _immediateContext.SetPipelineState(pipeline.PipelineState);
        _immediateContext.CommitShaderResources(pipeline.ShaderResources, ResourceStateTransitionMode.ResourceStateTransitionModeTransition);

        _singleVertexBuffer[0] = uploadedGeometry.VertexBuffer;
        _immediateContext.SetVertexBuffers(0, 1, _singleVertexBuffer, _singleVertexBufferOffset,
            ResourceStateTransitionMode.ResourceStateTransitionModeTransition,
            SetVertexBuffersFlags.SetVertexBuffersFlagReset);
        _immediateContext.SetIndexBuffer(uploadedGeometry.IndexBuffer, 0,
            ResourceStateTransitionMode.ResourceStateTransitionModeTransition);
        _immediateContext.DrawIndexed(uploadedGeometry.DrawIndexedAttribs);
    }

    private void UpdateFullscreenCustomBuffer(IBuffer buffer, CustomUniformLayout layout, byte[]? scratch,
        IReadOnlyDictionary<string, object?> parameters)
    {
        var data = GetCustomParameterScratchBuffer(layout, scratch);
        foreach (var field in layout.Fields)
        {
            if (!TryGetCustomParameterValue(field, parameters, out var value))
                continue;

            WriteCustomParameterValue(data, field, value);
        }
        UpdateBuffer(buffer, data);
    }

    private void UpdateFullscreenCustomBuffer<TParameters>(IBuffer buffer, CustomUniformLayout layout, byte[]? scratch, in TParameters parameters)
        where TParameters : struct
    {
        var data = GetCustomParameterScratchBuffer(layout, scratch);
        StructCustomUniformWriter<TParameters>.Write(data, layout, parameters);
        UpdateBuffer(buffer, data);
    }

    private void UpdateMaterialCustomBuffer(IBuffer buffer, CustomUniformLayout layout, byte[]? scratch, Material material)
    {
        var data = GetCustomParameterScratchBuffer(layout, scratch);
        var parameters = material.GetShaderParameters();
        var fields = layout.Fields;
        for (var fieldIndex = 0; fieldIndex < fields.Count; ++fieldIndex)
        {
            var field = fields[fieldIndex];
            if (!TryGetCustomParameterValue(field, parameters, out var value))
                continue;

            WriteCustomParameterValue(data, field, value);
        }
        UpdateBuffer(buffer, data);
    }

    private static byte[] GetCustomParameterScratchBuffer(CustomUniformLayout layout, byte[]? scratch)
    {
        var size = checked((int)layout.Size);
        if (size == 0)
            return Array.Empty<byte>();

        if (scratch is null || scratch.Length != size)
            return new byte[size];

        scratch.AsSpan().Clear();
        return scratch;
    }

    private static class StructCustomUniformWriter<T>
        where T : struct
    {
        private delegate void StructFieldWriter(byte[] buffer, CustomUniformField field, T value);

        private static readonly Dictionary<string, StructFieldWriter> Writers = CreateWriters();

        public static void Write(byte[] buffer, CustomUniformLayout layout, in T value)
        {
            var fields = layout.Fields;
            for (var fieldIndex = 0; fieldIndex < fields.Count; ++fieldIndex)
            {
                var field = fields[fieldIndex];
                if (Writers.TryGetValue(field.ParameterName, out var writer) || Writers.TryGetValue(field.UniformName, out writer))
                    writer(buffer, field, value);
            }
        }

        private static Dictionary<string, StructFieldWriter> CreateWriters()
        {
            var writers = new Dictionary<string, StructFieldWriter>(StringComparer.Ordinal);
            var parameter = Expression.Parameter(typeof(T), "value");

            foreach (var field in typeof(T).GetFields(BindingFlags.Instance | BindingFlags.Public))
            {
                var name = field.GetCustomAttribute<ShaderParameterNameAttribute>()?.Name ?? field.Name;
                writers[name] = CreateWriter(Expression.Field(parameter, field), field.FieldType, parameter);
            }

            foreach (var property in typeof(T).GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                if (!property.CanRead || property.GetMethod is null || property.GetIndexParameters().Length > 0)
                    continue;

                var name = property.GetCustomAttribute<ShaderParameterNameAttribute>()?.Name ?? property.Name;
                writers[name] = CreateWriter(Expression.Property(parameter, property), property.PropertyType, parameter);
            }

            return writers;
        }

        private static StructFieldWriter CreateWriter(Expression access, Type type, ParameterExpression parameter)
        {
            if (type == typeof(float))
            {
                var getter = Expression.Lambda<Func<T, float>>(access, parameter).Compile();
                return (buffer, field, value) =>
                {
                    if (field.Kind == CustomUniformKind.Float)
                        WriteFloat(buffer, field.Offset, getter(value));
                };
            }

            if (type == typeof(int))
            {
                var getter = Expression.Lambda<Func<T, int>>(access, parameter).Compile();
                return (buffer, field, value) =>
                {
                    var fieldValue = getter(value);
                    switch (field.Kind)
                    {
                        case CustomUniformKind.Float:
                            WriteFloat(buffer, field.Offset, fieldValue);
                            break;
                        case CustomUniformKind.Int:
                            Buffer.BlockCopy(BitConverter.GetBytes(fieldValue), 0, buffer, field.Offset, sizeof(int));
                            break;
                        case CustomUniformKind.UInt when fieldValue >= 0:
                            Buffer.BlockCopy(BitConverter.GetBytes((uint)fieldValue), 0, buffer, field.Offset, sizeof(uint));
                            break;
                    }
                };
            }

            if (type == typeof(uint))
            {
                var getter = Expression.Lambda<Func<T, uint>>(access, parameter).Compile();
                return (buffer, field, value) =>
                {
                    var fieldValue = getter(value);
                    switch (field.Kind)
                    {
                        case CustomUniformKind.Float:
                            WriteFloat(buffer, field.Offset, fieldValue);
                            break;
                        case CustomUniformKind.UInt:
                            Buffer.BlockCopy(BitConverter.GetBytes(fieldValue), 0, buffer, field.Offset, sizeof(uint));
                            break;
                    }
                };
            }

            if (type == typeof(bool))
            {
                var getter = Expression.Lambda<Func<T, bool>>(access, parameter).Compile();
                return (buffer, field, value) =>
                {
                    if (field.Kind is not (CustomUniformKind.Bool or CustomUniformKind.Int))
                        return;

                    Buffer.BlockCopy(BitConverter.GetBytes(getter(value) ? 1 : 0), 0, buffer, field.Offset, sizeof(int));
                };
            }

            if (type == typeof(Vector2f))
            {
                var getter = Expression.Lambda<Func<T, Vector2f>>(access, parameter).Compile();
                return (buffer, field, value) =>
                {
                    if (field.Kind == CustomUniformKind.Vector2)
                        WriteVector2(buffer, field.Offset, getter(value));
                };
            }

            if (type == typeof(Vector3f))
            {
                var getter = Expression.Lambda<Func<T, Vector3f>>(access, parameter).Compile();
                return (buffer, field, value) =>
                {
                    if (field.Kind == CustomUniformKind.Vector3)
                        WriteVector4(buffer, field.Offset, new Vector4f(getter(value), 0.0f));
                };
            }

            if (type == typeof(Vector4f))
            {
                var getter = Expression.Lambda<Func<T, Vector4f>>(access, parameter).Compile();
                return (buffer, field, value) =>
                {
                    if (field.Kind == CustomUniformKind.Vector4)
                        WriteVector4(buffer, field.Offset, getter(value));
                };
            }

            if (type == typeof(Matrix4))
            {
                var getter = Expression.Lambda<Func<T, Matrix4>>(access, parameter).Compile();
                return (buffer, field, value) =>
                {
                    if (field.Kind == CustomUniformKind.Matrix4)
                        WriteMatrix4Raw(buffer, field.Offset, getter(value));
                };
            }

            return static (_, _, _) => { };
        }
    }

    private bool TryGetCustomParameterValue(CustomUniformField field, IReadOnlyDictionary<string, object?> parameters, out object? value)
    {
        if (parameters.TryGetValue(field.ParameterName, out value))
            return true;
        if (parameters.TryGetValue(field.UniformName, out value))
            return true;
        value = null;
        return false;
    }

    private static bool TryGetCustomParameterValue(CustomUniformField field, IReadOnlyDictionary<string, MaterialShaderParameter> parameters,
        out object? value)
    {
        if (TryGetCustomParameterValue(field.ParameterName, parameters, out value))
            return true;
        if (TryGetCustomParameterValue(field.UniformName, parameters, out value))
            return true;
        value = null;
        return false;
    }

    private static bool TryGetCustomParameterValue(string parameterName, IReadOnlyDictionary<string, MaterialShaderParameter> parameters,
        out object? value)
    {
        if (parameters.TryGetValue(parameterName, out var parameter))
        {
            value = parameter.Value;
            return parameter.IsCustom;
        }

        value = null;
        return false;
    }

    private static void WriteCustomParameterValue(byte[] buffer, CustomUniformField field, object? value)
    {
        switch (field.Kind)
        {
            case CustomUniformKind.Float:
                if (value is float floatValue)
                    WriteFloat(buffer, field.Offset, floatValue);
                else if (value is int intValue)
                    WriteFloat(buffer, field.Offset, intValue);
                else if (value is uint uintValue)
                    WriteFloat(buffer, field.Offset, uintValue);
                break;

            case CustomUniformKind.Int:
                if (value is int intParameter)
                    Buffer.BlockCopy(BitConverter.GetBytes(intParameter), 0, buffer, field.Offset, sizeof(int));
                else if (value is bool boolParameter)
                    Buffer.BlockCopy(BitConverter.GetBytes(boolParameter ? 1 : 0), 0, buffer, field.Offset, sizeof(int));
                break;

            case CustomUniformKind.UInt:
                if (value is uint uintParameter)
                    Buffer.BlockCopy(BitConverter.GetBytes(uintParameter), 0, buffer, field.Offset, sizeof(uint));
                else if (value is int signedParameter && signedParameter >= 0)
                    Buffer.BlockCopy(BitConverter.GetBytes((uint)signedParameter), 0, buffer, field.Offset, sizeof(uint));
                break;

            case CustomUniformKind.Bool:
                if (value is bool boolValue)
                    Buffer.BlockCopy(BitConverter.GetBytes(boolValue ? 1 : 0), 0, buffer, field.Offset, sizeof(int));
                break;

            case CustomUniformKind.Vector2:
                if (value is Vector2f vector2)
                    WriteVector2(buffer, field.Offset, vector2);
                break;

            case CustomUniformKind.Vector3:
                if (value is Vector3f vector3)
                    WriteVector4(buffer, field.Offset, new Vector4f(vector3, 0.0f));
                break;

            case CustomUniformKind.Vector4:
                if (value is Vector4f vector4)
                    WriteVector4(buffer, field.Offset, vector4);
                break;

            case CustomUniformKind.Matrix4:
                if (value is Matrix4 matrix4)
                    WriteMatrix4Raw(buffer, field.Offset, matrix4);
                break;
        }
    }

    private static CustomUniformLayout CreateCustomUniformLayout(ShaderProgramReflection reflection)
    {
        ArgumentNullException.ThrowIfNull(reflection);

        var customBuffer = reflection.GetUniformBuffer(ShaderParameterGroup.Custom);
        if (customBuffer is null || customBuffer.Size == 0)
            return new CustomUniformLayout(0u, []);

        var customUniforms = new List<UniformReflection>();
        foreach (var uniform in reflection.Uniforms.Values)
        {
            if (uniform.Group == ShaderParameterGroup.Custom)
                customUniforms.Add(uniform);
        }

        customUniforms.Sort(static (left, right) =>
        {
            var offsetComparison = left.Offset.CompareTo(right.Offset);
            return offsetComparison != 0
                ? offsetComparison
                : left.NameHash.Value.CompareTo(right.NameHash.Value);
        });

        var fields = new CustomUniformField[customUniforms.Count];
        for (var i = 0; i < fields.Length; ++i)
        {
            var uniform = customUniforms[i];
            var uniformName = reflection.GetUniformName(uniform.NameHash);
            fields[i] = new CustomUniformField(
                uniformName,
                GetCustomParameterName(uniformName),
                MapCustomUniformKind(uniform, uniformName),
                checked((int)uniform.Offset));
        }

        return new CustomUniformLayout(checked((uint)customBuffer.Size), fields);
    }

    private static string GetCustomParameterName(string uniformName)
    {
        return uniformName.Length > 1 && uniformName[0] == 'c' && char.IsUpper(uniformName[1])
            ? uniformName[1..]
            : uniformName;
    }

    private static CustomUniformKind MapCustomUniformKind(UniformReflection uniform, string uniformName)
    {
        if (uniform.ArraySize > 1)
            throw new NotSupportedException($"Custom uniform arrays are not implemented yet: {uniformName}[{uniform.ArraySize}].");

        return uniform.VariableClass switch
        {
            ShaderCodeVariableClass.ShaderCodeVariableClassScalar => uniform.BasicType switch
            {
                ShaderCodeBasicType.ShaderCodeBasicTypeBool => CustomUniformKind.Bool,
                ShaderCodeBasicType.ShaderCodeBasicTypeUint or ShaderCodeBasicType.ShaderCodeBasicTypeUint16 or ShaderCodeBasicType.ShaderCodeBasicTypeUint8 => CustomUniformKind.UInt,
                ShaderCodeBasicType.ShaderCodeBasicTypeInt or ShaderCodeBasicType.ShaderCodeBasicTypeInt16 or ShaderCodeBasicType.ShaderCodeBasicTypeInt8 => CustomUniformKind.Int,
                ShaderCodeBasicType.ShaderCodeBasicTypeFloat or ShaderCodeBasicType.ShaderCodeBasicTypeFloat16 => CustomUniformKind.Float,
                _ => throw new NotSupportedException($"Unsupported custom scalar uniform type {uniform.BasicType} for {uniformName}.")
            },
            ShaderCodeVariableClass.ShaderCodeVariableClassVector => (Math.Max(uniform.NumRows, uniform.NumColumns), uniform.BasicType) switch
            {
                (2, ShaderCodeBasicType.ShaderCodeBasicTypeFloat or ShaderCodeBasicType.ShaderCodeBasicTypeFloat16) => CustomUniformKind.Vector2,
                (3, ShaderCodeBasicType.ShaderCodeBasicTypeFloat or ShaderCodeBasicType.ShaderCodeBasicTypeFloat16) => CustomUniformKind.Vector3,
                (4, ShaderCodeBasicType.ShaderCodeBasicTypeFloat or ShaderCodeBasicType.ShaderCodeBasicTypeFloat16) => CustomUniformKind.Vector4,
                _ => throw new NotSupportedException($"Unsupported custom vector uniform type {uniform.BasicType}x{Math.Max(uniform.NumRows, uniform.NumColumns)} for {uniformName}.")
            },
            ShaderCodeVariableClass.ShaderCodeVariableClassMatrixColumns
                when (uniform.BasicType is ShaderCodeBasicType.ShaderCodeBasicTypeFloat or ShaderCodeBasicType.ShaderCodeBasicTypeFloat16)
                     && uniform.NumRows == 4
                     && uniform.NumColumns == 4
                => CustomUniformKind.Matrix4,
            _ => throw new NotSupportedException($"Unsupported custom uniform layout for {uniformName}: class={uniform.VariableClass}, type={uniform.BasicType}, rows={uniform.NumRows}, columns={uniform.NumColumns}.")
        };
    }

    private UploadedGeometry? GetOrCreateUploadedGeometry(Geometry geometry)
    {
        if (_uploadedGeometries.TryGetValue(geometry, out var uploaded))
            return uploaded;

        if (_device is null || _immediateContext is null)
            return null;

        var vertexBuffer = geometry.GetVertexBuffer(0);
        var indexBuffer = geometry.GetIndexBuffer();
        if (vertexBuffer is null || indexBuffer is null)
            return null;

        var gpuVertexBuffer = GetVertexBufferHandle(vertexBuffer);
        var gpuIndexBuffer = GetIndexBufferHandle(indexBuffer);
        var drawIndexedAttribs = new DrawIndexedAttribs(geometry.GetIndexCount(),
            indexBuffer.GetIndexSize() == 4 ? global::RedBolt.Bindings.Diligent.ValueType.VtUint32 : global::RedBolt.Bindings.Diligent.ValueType.VtUint16,
            DrawFlags.DrawFlagVerifyAll,
            _FirstIndexLocation: geometry.GetIndexStart());

        uploaded = new UploadedGeometry(gpuVertexBuffer, gpuIndexBuffer, drawIndexedAttribs);
        _uploadedGeometries[geometry] = uploaded;
        return uploaded;
    }

    private void BindMaterialTextures(PipelineResources pipeline, Material material)
    {
        BindTextureToAvailableStages(pipeline,
            "sAlbedo", GetTextureView(material.GetTexture(AlbedoTextureSlot), GetDefaultAlbedoTexture()));
        BindTextureToAvailableStages(pipeline,
            "sNormal", GetTextureView(material.GetTexture(NormalTextureSlot), GetDefaultNormalTexture()));
        BindTextureToAvailableStages(pipeline,
            "sProperties", GetTextureView(material.GetTexture(PropertiesTextureSlot), GetDefaultPropertiesTexture()));
        BindTextureToAvailableStages(pipeline,
            "sEmission", GetTextureView(material.GetTexture(EmissionTextureSlot), GetDefaultEmissionTexture()));
    }

    private ITextureView GetTextureView(Texture? texture, Texture2D fallbackTexture)
    {
        if (texture is Texture2D texture2D)
            return GetShaderResourceView(texture2D);

        return GetShaderResourceView(fallbackTexture);
    }

    private ITextureView GetShaderResourceView(Texture2D texture)
    {
        if (texture.IsRenderTarget() && texture.GetRawTexture() is { } rawTexture)
        {
            if (rawTexture.GetResolveDirty())
                rawTexture.Resolve();
            if (rawTexture.GetLevelsDirty())
                rawTexture.GenerateLevels();
            return rawTexture.GetHandles().ShaderResourceView
                ?? throw new InvalidOperationException($"Texture '{texture.Name}' was not created with a shader-resource view.");
        }

        var uploaded = GetOrCreateUploadedTexture(texture);
        EnsureUploadedTextureResolved(texture, uploaded);
        return uploaded.ShaderView;
    }

    private ITexture GetCopySourceTexture(Texture2D texture)
    {
        var uploaded = GetOrCreateUploadedTexture(texture);
        EnsureUploadedTextureResolved(texture, uploaded);
        return uploaded.ResolvedTexture ?? uploaded.Texture;
    }

    private static IBuffer GetVertexBufferHandle(VertexBuffer vertexBuffer)
    {
        var rawBuffer = vertexBuffer.GetRawBuffer()
            ?? throw new InvalidOperationException("Vertex buffer is not initialized.");
        rawBuffer.Resolve();
        return rawBuffer.GetHandle()
            ?? throw new InvalidOperationException("Vertex buffer has no GPU handle.");
    }

    private static IBuffer GetIndexBufferHandle(IndexBuffer indexBuffer)
    {
        var rawBuffer = indexBuffer.GetRawBuffer()
            ?? throw new InvalidOperationException("Index buffer is not initialized.");
        rawBuffer.Resolve();
        return rawBuffer.GetHandle()
            ?? throw new InvalidOperationException("Index buffer has no GPU handle.");
    }

    private Texture2D GetDefaultAlbedoTexture()
    {
        return _defaultAlbedoTexture ??= CreateFallbackTexture("#fallback-albedo", DefaultAlbedoTextureData);
    }

    private Texture2D GetDefaultNormalTexture()
    {
        return _defaultNormalTexture ??= CreateFallbackTexture("#fallback-normal", DefaultNormalTextureData);
    }

    private Texture2D GetDefaultPropertiesTexture()
    {
        return _defaultPropertiesTexture ??= CreateFallbackTexture("#fallback-properties", DefaultPropertiesTextureData);
    }

    private Texture2D GetDefaultEmissionTexture()
    {
        return _defaultEmissionTexture ??= CreateFallbackTexture("#fallback-emission", DefaultEmissionTextureData);
    }

    private static Texture2D CreateFallbackTexture(string name, ReadOnlySpan<byte> rgbaPixels)
    {
        var texture = new Texture2D();
        texture.SetSize(1, 1, TextureFormat.TexFormatRgba8Unorm);
        texture.SetData(0, 0, 0, 1, 1, rgbaPixels);
        return texture;
    }

    private UploadedTexture GetOrCreateUploadedTexture(Texture texture)
    {
        return GetOrCreateUploadedTexture(texture, bindUnorderedAccess: false);
    }

    private UploadedTexture GetOrCreateUploadedTexture(Texture2D texture)
    {
        return GetOrCreateUploadedTexture(texture, bindUnorderedAccess: false);
    }

    private UploadedTexture GetOrCreateUploadedTexture(TextureCube texture)
    {
        return GetOrCreateUploadedTexture(texture, bindUnorderedAccess: false);
    }

    private UploadedTexture GetOrCreateUploadedTexture(Texture2DArray texture)
    {
        return GetOrCreateUploadedTexture(texture, bindUnorderedAccess: false);
    }

    private UploadedTexture GetOrCreateUploadedTexture(Texture texture, bool bindUnorderedAccess)
    {
        return texture switch
        {
            Texture2D texture2D => GetOrCreateUploadedTexture(texture2D, bindUnorderedAccess),
            TextureCube textureCube => GetOrCreateUploadedTexture(textureCube, bindUnorderedAccess),
            Texture2DArray texture2DArray => GetOrCreateUploadedTexture(texture2DArray, bindUnorderedAccess),
            _ => throw new NotSupportedException($"Texture type '{texture.GetType().Name}' is not supported by the current managed GPU upload path.")
        };
    }

    private UploadedTexture GetOrCreateUploadedTexture(Texture2D texture, bool bindUnorderedAccess)
    {
        if (_uploadedTextures.TryGetValue(texture, out var uploaded))
        {
            if (UploadedTextureMatches(texture, uploaded, bindUnorderedAccess))
                return uploaded;

            uploaded.Dispose();
            _uploadedTextures.Remove(texture);
        }

        if (TryGetRawTexture(texture, bindUnorderedAccess, out uploaded))
        {
            _uploadedTextures[texture] = uploaded;
            return uploaded;
        }

        if (_device is null)
            throw new InvalidOperationException("Graphics device is not initialized.");

        using var nativeName = new CString(texture.Name);
        using var textureDescOwner = CreateTextureDesc();
        var textureDesc = textureDescOwner.Desc;
        textureDesc.Name = nativeName.Pointer;
        textureDesc.Type = ResourceDimension.ResourceDimTex2d;
        textureDesc.Width = (uint)texture.Width;
        textureDesc.Height = (uint)texture.Height;
        textureDesc.ArraySize = 1;
        textureDesc.SampleCount = (uint)texture.MultiSample;
        textureDesc.Format = texture.Format;
        if (textureDesc.Format == 0)
            throw new NotSupportedException($"Texture format '{texture.Format}' is not supported by managed GPU uploads.");

        textureDesc.MipLevels = texture.MultiSample > 1 ? 1u : checked((uint)Math.Max(texture.Levels, 1));
        textureDesc.BindFlags = IsDepthTextureFormat(texture.Format)
            ? BindFlags.BindShaderResource | BindFlags.BindDepthStencil
            : BindFlags.BindShaderResource | BindFlags.BindRenderTarget;
        if (bindUnorderedAccess)
            textureDesc.BindFlags |= BindFlags.BindUnorderedAccess;
        textureDesc.Usage = Usage.UsageDefault;
        _device.CreateTexture(textureDesc, null!, out var gpuTexture);
        if (gpuTexture.IsNull)
            throw new InvalidOperationException($"Failed to create GPU texture for {texture.Name}.");

        var (resolvedTexture, shaderView) = CreateResolvedTextureAndShaderView(texture, textureDesc);
        shaderView ??= GetDefaultTextureView(gpuTexture, TextureViewType.TextureViewShaderResource, texture.Format);
        var targetView = IsDepthTextureFormat(texture.Format) ? null : GetDefaultTextureView(gpuTexture, TextureViewType.TextureViewRenderTarget, texture.Format);
        var depthView = IsDepthTextureFormat(texture.Format) ? GetDefaultTextureView(gpuTexture, TextureViewType.TextureViewDepthStencil, texture.Format) : null;
        var readOnlyDepthView = IsDepthTextureFormat(texture.Format) && depthView is not null
            ? GetReadOnlyDepthTextureView(gpuTexture, depthView)
            : null;
        uploaded = new UploadedTexture(texture, gpuTexture, shaderView, targetView, depthView, readOnlyDepthView,
            resolvedTexture, supportsUnorderedAccess: bindUnorderedAccess);
        _uploadedTextures[texture] = uploaded;
        return uploaded;
    }

    private UploadedTexture GetOrCreateUploadedTexture(TextureCube texture, bool bindUnorderedAccess)
    {
        if (_uploadedTextures.TryGetValue(texture, out var uploaded))
        {
            if (UploadedTextureMatches(texture, uploaded, bindUnorderedAccess))
                return uploaded;

            uploaded.Dispose();
            _uploadedTextures.Remove(texture);
        }

        if (TryGetRawTexture(texture, bindUnorderedAccess, out uploaded))
        {
            _uploadedTextures[texture] = uploaded;
            return uploaded;
        }

        if (_device is null)
            throw new InvalidOperationException("Graphics device is not initialized.");

        using var nativeName = new CString(texture.Name);
        using var textureDescOwner = CreateTextureDesc();
        var textureDesc = textureDescOwner.Desc;
        textureDesc.Name = nativeName.Pointer;
        textureDesc.Type = ResourceDimension.ResourceDimTexCube;
        textureDesc.Width = (uint)texture.Width;
        textureDesc.Height = (uint)texture.Height;
        textureDesc.ArraySize = 6;
        textureDesc.SampleCount = (uint)texture.MultiSample;
        textureDesc.Format = texture.Format;
        if (textureDesc.Format == 0)
            throw new NotSupportedException($"Texture format '{texture.Format}' is not supported by managed GPU uploads.");

        textureDesc.MipLevels = texture.MultiSample > 1 ? 1u : checked((uint)Math.Max(texture.Levels, 1));
        textureDesc.BindFlags = IsDepthTextureFormat(texture.Format)
            ? BindFlags.BindShaderResource | BindFlags.BindDepthStencil
            : BindFlags.BindShaderResource | BindFlags.BindRenderTarget;
        if (bindUnorderedAccess)
            textureDesc.BindFlags |= BindFlags.BindUnorderedAccess;
        textureDesc.Usage = Usage.UsageDefault;
        _device.CreateTexture(textureDesc, null!, out var gpuTexture);
        if (gpuTexture.IsNull)
            throw new InvalidOperationException($"Failed to create GPU texture for {texture.Name}.");

        var (resolvedTexture, shaderView) = CreateResolvedTextureAndShaderView(texture, textureDesc);
        shaderView ??= GetDefaultTextureView(gpuTexture, TextureViewType.TextureViewShaderResource, texture.Format);
        uploaded = new UploadedTexture(texture, gpuTexture, shaderView, null, null, null, resolvedTexture,
            supportsUnorderedAccess: bindUnorderedAccess);
        _uploadedTextures[texture] = uploaded;
        return uploaded;
    }

    private UploadedTexture GetOrCreateUploadedTexture(Texture2DArray texture, bool bindUnorderedAccess)
    {
        if (_uploadedTextures.TryGetValue(texture, out var uploaded))
        {
            if (UploadedTextureMatches(texture, uploaded, bindUnorderedAccess))
                return uploaded;

            uploaded.Dispose();
            _uploadedTextures.Remove(texture);
        }

        if (TryGetRawTexture(texture, bindUnorderedAccess, out uploaded))
        {
            _uploadedTextures[texture] = uploaded;
            return uploaded;
        }

        if (_device is null)
            throw new InvalidOperationException("Graphics device is not initialized.");

        using var nativeName = new CString(texture.Name);
        using var textureDescOwner = CreateTextureDesc();
        var textureDesc = textureDescOwner.Desc;
        textureDesc.Name = nativeName.Pointer;
        textureDesc.Type = ResourceDimension.ResourceDimTex2dArray;
        textureDesc.Width = (uint)texture.Width;
        textureDesc.Height = (uint)texture.Height;
        textureDesc.ArraySize = checked((uint)texture.Layers);
        textureDesc.SampleCount = (uint)texture.MultiSample;
        textureDesc.Format = texture.Format;
        if (textureDesc.Format == 0)
            throw new NotSupportedException($"Texture format '{texture.Format}' is not supported by managed GPU uploads.");

        textureDesc.MipLevels = checked((uint)Math.Max(texture.Levels, 1));
        textureDesc.BindFlags = IsDepthTextureFormat(texture.Format)
            ? BindFlags.BindShaderResource | BindFlags.BindDepthStencil
            : BindFlags.BindShaderResource | BindFlags.BindRenderTarget;
        if (bindUnorderedAccess)
            textureDesc.BindFlags |= BindFlags.BindUnorderedAccess;
        textureDesc.Usage = Usage.UsageDefault;
        _device.CreateTexture(textureDesc, null!, out var gpuTexture);
        if (gpuTexture.IsNull)
            throw new InvalidOperationException($"Failed to create GPU texture for {texture.Name}.");

        var (resolvedTexture, shaderView) = CreateResolvedTextureAndShaderView(texture, textureDesc);
        shaderView ??= GetDefaultTextureView(gpuTexture, TextureViewType.TextureViewShaderResource, texture.Format);
        uploaded = new UploadedTexture(texture, gpuTexture, shaderView, null, null, null, resolvedTexture,
            supportsUnorderedAccess: bindUnorderedAccess);
        _uploadedTextures[texture] = uploaded;
        return uploaded;
    }

    private static bool TryGetRawTexture(Texture texture, bool bindUnorderedAccess, out UploadedTexture uploaded)
    {
        uploaded = null!;
        var rawTexture = texture.GetRawTexture();
        if (rawTexture is null || !rawTexture.GetHandles().IsValid())
            return false;
        var parameters = rawTexture.GetParams();
        if (bindUnorderedAccess && (parameters.Flags & TextureFlag.BindUnorderedAccess) == 0)
            return false;
        ref readonly var handles = ref rawTexture.GetHandles();
        var textureHandle = handles.Texture;
        var shaderResourceView = handles.ShaderResourceView;
        if (textureHandle is null || shaderResourceView is null)
            return false;

        uploaded = new UploadedTexture(texture, textureHandle, shaderResourceView,
            handles.RenderTargetView, handles.DepthStencilView, handles.ReadOnlyDepthStencilView,
            handles.ResolvedTexture, bindUnorderedAccess, ownsResources: false);
        return true;
    }

    private (ITexture? Texture, ITextureView? ShaderView) CreateResolvedTextureAndShaderView(Texture texture, TextureDesc sourceDesc)
    {
        if (!NeedsResolvedTexture(texture))
            return (null, null);
        if (_device is null)
            throw new InvalidOperationException("Graphics device is not initialized.");

        using var resolvedDescOwner = CreateTextureDesc();
        var resolvedDesc = resolvedDescOwner.Desc;
        resolvedDesc.Name = sourceDesc.Name;
        resolvedDesc.Type = sourceDesc.Type;
        resolvedDesc.Width = sourceDesc.Width;
        resolvedDesc.Height = sourceDesc.Height;
        CopyTextureDescArraySizeOrDepth(resolvedDesc, sourceDesc);
        resolvedDesc.Format = sourceDesc.Format;
        resolvedDesc.MipLevels = GetUploadedMipLevels(texture);
        resolvedDesc.BindFlags = sourceDesc.BindFlags;
        resolvedDesc.Usage = Usage.UsageDefault;
        resolvedDesc.SampleCount = 1;
        _device.CreateTexture(resolvedDesc, null!, out var resolvedTexture);
        if (resolvedTexture.IsNull)
            throw new InvalidOperationException($"Failed to create resolved GPU texture for {texture.Name}.");

        var shaderView = GetDefaultTextureView(resolvedTexture, TextureViewType.TextureViewShaderResource, texture.Format);
        return (resolvedTexture, shaderView);
    }

    private ITextureView GetDefaultTextureView(ITexture texture, TextureViewType viewType, TextureFormat format)
    {
        using var textureViewDescOwner = CreateTextureViewDesc();
        var textureViewDesc = textureViewDescOwner.Desc;
        textureViewDesc.ViewType = viewType;
        textureViewDesc.Format = format;
        if (viewType == TextureViewType.TextureViewShaderResource)
        {
            using var textureDesc = texture.GetDesc();
            if ((textureDesc.MiscFlags & MiscTextureFlags.MiscTextureFlagGenerateMips) != 0)
                textureViewDesc.Flags |= TextureViewFlags.TextureViewFlagAllowMipMapGeneration;
        }
        else if (viewType == TextureViewType.TextureViewUnorderedAccess)
        {
            textureViewDesc.AccessFlags |= UavAccessFlag.UavAccessFlagReadWrite;
        }

        texture.CreateView(textureViewDesc, out var createdView);
        if (createdView.IsNull)
            throw new InvalidOperationException($"Failed to create {viewType} for texture.");

        return createdView;
    }

    private ITextureView GetReadOnlyDepthTextureView(ITexture texture, ITextureView depthView)
    {
        var view = texture.GetDefaultView(TextureViewType.TextureViewReadOnlyDepthStencil);
        if (view is not null && !view.IsNull)
            return RetainDefaultTextureView(view);

        using var depthViewDesc = depthView.GetDesc();
        using var textureViewDesc = depthViewDesc.Clone();
        textureViewDesc.ViewType = TextureViewType.TextureViewReadOnlyDepthStencil;
        texture.CreateView(textureViewDesc, out var createdView);
        if (createdView.IsNull)
            throw new InvalidOperationException("Failed to create read-only depth-stencil view for texture.");

        return createdView;
    }

    private static ITextureView RetainDefaultTextureView(ITextureView view)
    {
        _ = view.AddRef();
        return view;
    }

    private void EnsureUploadedTextureResolved(Texture texture, UploadedTexture uploaded)
    {
        var rawTexture = texture.GetRawTexture();
        if (rawTexture is not null && ReferenceEquals(rawTexture.GetHandles().Texture, uploaded.Texture))
        {
            rawTexture.Resolve();
            rawTexture.GenerateLevels();
            return;
        }

        if (!uploaded.ResolveDirty || uploaded.ResolvedTexture is null || _immediateContext is null)
            return;

        using var resolveAttribsOwner = CreateResolveTextureSubresourceAttribs();
        var resolveAttribs = resolveAttribsOwner.Attribs;
        resolveAttribs.Format = texture.Format;
        resolveAttribs.SrcTextureTransitionMode = ResourceStateTransitionMode.ResourceStateTransitionModeTransition;
        resolveAttribs.DstTextureTransitionMode = ResourceStateTransitionMode.ResourceStateTransitionModeTransition;
        for (var slice = 0u; slice < GetArraySizeOrDepth(texture); ++slice)
        {
            resolveAttribs.SrcSlice = slice;
            resolveAttribs.DstSlice = slice;
            _immediateContext.ResolveTextureSubresource(uploaded.Texture, uploaded.ResolvedTexture, resolveAttribs);
        }

        uploaded.ClearResolveDirty();
    }

    internal void MarkRenderTargetDirty(RenderTargetView? view)
    {
        if (view is not { RenderTexture: { } renderTexture } actualView)
            return;
        if (renderTexture.GetRawTexture() is { } rawTexture)
        {
            rawTexture.MarkDirty();
            return;
        }
        if (!_uploadedTextures.TryGetValue(renderTexture, out var uploaded))
            return;

        uploaded.MarkDirty();
    }

    internal ITextureView ResolveRenderTargetView(RenderTargetView view)
    {
        return view.Type switch
        {
            RenderTargetViewType.SwapChainColor => _swapChain?.GetCurrentBackBufferRTV()
                ?? throw new InvalidOperationException("Swap chain color target is not available."),
            RenderTargetViewType.SwapChainDepthStencil => _swapChain?.GetDepthBufferDSV()
                ?? throw new InvalidOperationException("Swap chain depth target is not available."),
            RenderTargetViewType.Resource => ResolveTextureRenderTargetView(view),
            RenderTargetViewType.ResourceSlice => ResolveTextureRenderTargetView(view),
            RenderTargetViewType.ReadOnlyResource => ResolveTextureReadOnlyDepthView(view),
            RenderTargetViewType.ReadOnlyResourceSlice => ResolveTextureReadOnlyDepthView(view),
            _ => throw new NotSupportedException($"Unsupported render target view type {view.Type}.")
        };
    }

    private ITextureView ResolveTextureRenderTargetView(RenderTargetView view)
    {
        if (view.GetRawTexture() is { } rawTexture)
        {
            var handles = rawTexture.GetHandles();
            if (view.Type == RenderTargetViewType.Resource)
                return handles.RenderTargetView ?? handles.DepthStencilView
                    ?? throw new InvalidOperationException("Raw texture was not created with a render-target or depth-stencil view.");

            var surfaces = handles.RenderSurfaces;
            if (view.Slice >= surfaces.Length)
                throw new ArgumentOutOfRangeException(nameof(view), view.Slice, "Invalid raw texture slice index.");
            return surfaces[view.Slice];
        }

        if (view.RenderTexture is not { } texture)
            throw new InvalidOperationException("Render target view has no texture.");

        var uploaded = GetOrCreateUploadedTexture(texture);
        if (view.Type == RenderTargetViewType.ResourceSlice && texture is not Texture2D)
            return GetOrCreateTextureSliceView(uploaded, texture, view.Slice, readOnlyDepth: false);

        if (texture is not Texture2D)
            throw new NotSupportedException("Non-2D render targets must be bound through explicit slice views in the current managed pipeline slice.");

        if (IsDepthTextureFormat(texture.Format))
            return uploaded.DepthStencilView
                ?? throw new InvalidOperationException($"Texture '{texture.Name}' was not created with a depth-stencil view.");
        return uploaded.RenderTargetView
            ?? throw new InvalidOperationException($"Texture '{texture.Name}' was not created with a render-target view.");
    }

    private ITextureView ResolveTextureReadOnlyDepthView(RenderTargetView view)
    {
        if (view.GetRawTexture() is { } rawTexture)
        {
            var handles = rawTexture.GetHandles();
            if (view.Type == RenderTargetViewType.ReadOnlyResource)
                return handles.ReadOnlyDepthStencilView
                    ?? throw new InvalidOperationException("Raw texture was not created with a read-only depth-stencil view.");

            var surfaces = handles.ReadOnlyRenderSurfaces;
            if (view.Slice >= surfaces.Length)
                throw new ArgumentOutOfRangeException(nameof(view), view.Slice, "Invalid raw texture slice index.");
            return surfaces[view.Slice];
        }

        if (view.RenderTexture is not { } texture)
            throw new InvalidOperationException("Read-only depth view has no texture.");

        var uploaded = GetOrCreateUploadedTexture(texture);
        if (view.Type == RenderTargetViewType.ReadOnlyResourceSlice && texture is not Texture2D)
            return GetOrCreateTextureSliceView(uploaded, texture, view.Slice, readOnlyDepth: true);

        if (texture is not Texture2D)
            throw new NotSupportedException("Non-2D depth targets must be bound through explicit slice views in the current managed pipeline slice.");

        return uploaded.ReadOnlyDepthView
            ?? throw new InvalidOperationException($"Texture '{texture.Name}' was not created with a read-only depth view.");
    }

    private ITextureView GetOrCreateTextureSliceView(UploadedTexture uploaded, Texture texture, int slice, bool readOnlyDepth)
    {
        var viewType = readOnlyDepth ? TextureViewType.TextureViewReadOnlyDepthStencil : IsDepthTextureFormat(texture.Format)
            ? TextureViewType.TextureViewDepthStencil
            : TextureViewType.TextureViewRenderTarget;
        if (uploaded.TryGetSliceView(viewType, slice, out var cachedView))
            return cachedView;

        using var textureViewDescOwner = CreateTextureViewDesc();
        var textureViewDesc = textureViewDescOwner.Desc;
        textureViewDesc.ViewType = viewType;
        textureViewDesc.TextureDim = ResourceDimension.ResourceDimTex2d;
        textureViewDesc.Format = texture.Format;
        textureViewDesc.MostDetailedMip = 0;
        textureViewDesc.NumMipLevels = 1;
        textureViewDesc.FirstArraySlice = checked((uint)slice);
        textureViewDesc.NumArraySlices = 1;
        uploaded.Texture.CreateView(textureViewDesc, out var createdView);
        if (createdView.IsNull)
            throw new InvalidOperationException($"Failed to create slice view {viewType} for texture '{texture.Name}'.");

        uploaded.CacheSliceView(viewType, slice, createdView);
        return createdView;
    }

    private global::RedBolt.Bindings.Diligent.TextureFormat ResolveColorTargetFormat(RenderTargetView view)
    {
        return view.Type switch
        {
            RenderTargetViewType.SwapChainColor => GetSwapChainColorFormat(),
            RenderTargetViewType.Resource or RenderTargetViewType.ResourceSlice => GetResourceTargetFormat(view),
            _ => 0
        };
    }

    private global::RedBolt.Bindings.Diligent.TextureFormat ResolveDepthTargetFormat(RenderTargetView view)
    {
        return view.Type switch
        {
            RenderTargetViewType.SwapChainDepthStencil => GetSwapChainDepthFormat(),
            RenderTargetViewType.ReadOnlyResource
                or RenderTargetViewType.ReadOnlyResourceSlice
                or RenderTargetViewType.Resource
                or RenderTargetViewType.ResourceSlice
                => GetResourceTargetFormat(view),
            _ => 0
        };
    }

    private global::RedBolt.Bindings.Diligent.TextureFormat GetResourceTargetFormat(RenderTargetView view)
    {
        if (view.GetRawTexture() is { } rawTexture)
        {
            var parameters = rawTexture.GetParams();
            return parameters.Format;
        }
        if (view.RenderTexture is not { } texture)
            return 0;

        return GetOrCreateUploadedTexture(texture).Format;
    }

    private static global::RedBolt.Bindings.Diligent.TextureFormat GetTextureViewFormat(ITextureView textureView)
    {
        var texture = textureView.GetTexture();
        if (!texture.IsNull)
        {
            using var textureDesc = texture.GetDesc();
            return textureDesc.Format;
        }

        using var viewDesc = textureView.GetDesc();
        return viewDesc.Format;
    }

    public global::RedBolt.Bindings.Diligent.TextureFormat GetSwapChainColorFormat()
    {
        if (_swapChainColorFormat != 0)
            return _swapChainColorFormat;
        if (_swapChain is null)
            return 0;

        using var desc = _swapChain.GetDesc();
        return desc.ColorBufferFormat;
    }

    public global::RedBolt.Bindings.Diligent.TextureFormat GetSwapChainDepthFormat()
    {
        if (_swapChainDepthFormat != 0)
            return _swapChainDepthFormat;
        if (_swapChain is null)
            return 0;

        using var desc = _swapChain.GetDesc();
        return desc.DepthBufferFormat;
    }

    private Vector2i ResolveRenderTargetSize(ReadOnlySpan<RenderTargetView> colorTargets, RenderTargetView? depthStencilTarget, IntRect viewportRect)
    {
        if ((colorTargets.Length != 0 && colorTargets[0].IsSwapChain) || depthStencilTarget?.IsSwapChain == true)
            return new Vector2i(Width, Height);
        if (colorTargets.Length != 0 && colorTargets[0].RenderTexture is { } colorTexture)
            return new Vector2i(colorTexture.Width, colorTexture.Height);
        if (colorTargets.Length != 0 && colorTargets[0].GetRawTexture() is { } colorRawTexture)
            return new Vector2i(colorRawTexture.GetParams().Size.X, colorRawTexture.GetParams().Size.Y);
        if (depthStencilTarget?.RenderTexture is { } depthTexture)
            return new Vector2i(depthTexture.Width, depthTexture.Height);
        if (depthStencilTarget?.GetRawTexture() is { } depthRawTexture)
            return new Vector2i(depthRawTexture.GetParams().Size.X, depthRawTexture.GetParams().Size.Y);
        return new Vector2i(viewportRect.Width, viewportRect.Height);
    }

    private void UpdateCurrentOutputDesc(ReadOnlySpan<RenderTargetView> colorTargets, RenderTargetView? depthStencilTarget)
    {
        _currentOutputDesc.NumRenderTargets = 0;
        Span<TextureFormat> formats = stackalloc TextureFormat[colorTargets.Length];
        for (var i = 0; i < colorTargets.Length; ++i)
            formats[i] = ResolveColorTargetFormat(colorTargets[i]);
        _currentOutputDesc.NumRenderTargets = formats.Length;
        formats.CopyTo(_currentOutputDesc.RenderTargetFormats);

        global::RedBolt.Bindings.Diligent.TextureFormat depthFormat = 0;
        if (depthStencilTarget is { } depthView)
        {
            depthFormat = ResolveDepthTargetFormat(depthView);
            _currentOutputDesc.DepthStencilFormat = depthFormat;
        }
        else
            _currentOutputDesc.DepthStencilFormat = TextureFormat.TexFormatUnknown;
        _currentOutputDesc.MultiSample = Math.Max(
            (colorTargets.Length != 0 ? colorTargets[0].GetMultiSample() : (int?)null)
            ?? depthStencilTarget?.GetMultiSample()
            ?? Math.Max(_configuredMultiSample, 1), 1);
    }

    private void ApplyDiligentViewport(IDeviceContext immediateContext, IntRect viewportRect)
    {
        var viewport = _diligentViewport ??= new global::RedBolt.Bindings.Diligent.Viewport();
        viewport.TopLeftX = viewportRect.Left;
        viewport.TopLeftY = viewportRect.Top;
        viewport.Width = viewportRect.Width;
        viewport.Height = viewportRect.Height;
        viewport.MinDepth = 0.0f;
        viewport.MaxDepth = 1.0f;
        immediateContext.SetViewports(1, viewport, 0, 0);
    }

    private static bool IsDepthTextureFormat(TextureFormat format)
        => RenderAPIUtils.IsDepthTextureFormat(format);

    private static bool UploadedTextureMatches(Texture texture, UploadedTexture uploaded, bool requireUnorderedAccess = false)
    {
        var expectedFormat = texture.Format;
        if (expectedFormat == 0)
            return false;
        var expectedFlags = texture.GetRawTexture()?.GetParams().Flags ?? TextureFlag.None;

        return uploaded.Dimension == GetResourceDimension(texture)
            && uploaded.Width == (uint)texture.Width
            && uploaded.Height == (uint)texture.Height
            && uploaded.ArraySizeOrDepth == GetArraySizeOrDepth(texture)
            && uploaded.SampleCount == (uint)texture.MultiSample
            && uploaded.HasResolvedTexture == NeedsResolvedTexture(texture)
            && ((expectedFlags & TextureFlag.BindRenderTarget) == 0 || uploaded.RenderTargetView is not null)
            && ((expectedFlags & TextureFlag.BindDepthStencil) == 0 || uploaded.DepthStencilView is not null)
            && (!requireUnorderedAccess || uploaded.SupportsUnorderedAccess)
            && uploaded.Format == expectedFormat
            && uploaded.MipLevels == GetUploadedMipLevels(texture);
    }

    private static bool NeedsResolvedTexture(Texture texture)
    {
        return texture.MultiSample > 1
            && texture.GetAutoResolve()
            && !IsDepthTextureFormat(texture.Format);
    }

    private static ResourceDimension GetResourceDimension(Texture texture)
    {
        return texture switch
        {
            TextureCube => ResourceDimension.ResourceDimTexCube,
            Texture2DArray => ResourceDimension.ResourceDimTex2dArray,
            _ => ResourceDimension.ResourceDimTex2d
        };
    }

    private static ResourceDimension GetViewResourceDimension(Texture texture)
    {
        return texture switch
        {
            TextureCube => ResourceDimension.ResourceDimTex2dArray,
            Texture2DArray => ResourceDimension.ResourceDimTex2dArray,
            _ => ResourceDimension.ResourceDimTex2d
        };
    }

    private static void CopyTextureDescArraySizeOrDepth(TextureDesc destination, TextureDesc source)
    {
        if (source.Type == ResourceDimension.ResourceDimTex3d)
            destination.Depth = source.Depth;
        else
            destination.ArraySize = source.GetArraySize();
    }

    private static uint GetArraySizeOrDepth(Texture texture)
    {
        return texture switch
        {
            TextureCube => 6u,
            Texture2DArray textureArray => checked((uint)textureArray.Layers),
            Texture3D texture3D => (uint)texture3D.Depth,
            _ => 1u
        };
    }

    private static uint GetUploadedMipLevels(Texture texture)
    {
        return texture switch
        {
            { MultiSample: > 1 } => 1u,
            _ => checked((uint)Math.Max(texture.Levels, 1))
        };
    }

    private static byte[] BuildFullscreenCameraBufferData(Vector4f clipToUvOffsetAndScale, Vector2f invInputSize, bool flipVertical)
    {
        var data = new byte[CameraBufferSize];
        WriteFullscreenCameraBufferData(data, clipToUvOffsetAndScale, invInputSize, flipVertical);
        return data;
    }

    private static void WriteFullscreenCameraBufferData(Span<byte> data, Vector4f clipToUvOffsetAndScale, Vector2f invInputSize, bool flipVertical)
    {
        var projection = Matrix4.Identity;
        if (flipVertical)
            projection.M11 = -1.0f;

        WriteMatrix4Raw(data, 0, Matrix4.Identity);
        WriteMatrix4Raw(data, 64, Matrix4.Identity);
        WriteMatrix4Raw(data, 128, projection);
        WriteVector4(data, 192, Vector4f.Zero);
        WriteVector4(data, 208, Vector4f.Zero);
        WriteFloat(data, 224, 0.0f);
        WriteFloat(data, 228, 1.0f);
        WriteVector4(data, 240, new Vector4f(0.0f, 0.0f, 0.5f, 0.5f));
        WriteVector4(data, 256, Vector4f.Zero);
        WriteVector4(data, 272, clipToUvOffsetAndScale);
        WriteVector4(data, 288, Vector4f.Zero);
        WriteVector2(data, 304, invInputSize);
        WriteVector4(data, 320, DefaultAmbient);
        WriteVector4(data, 336, Vector4f.Zero);
        WriteVector3(data, 352, new Vector3f(DefaultFogColor.X, DefaultFogColor.Y, DefaultFogColor.Z));
        WriteFloat(data, 364, 0.0f);
    }

    private static void WriteFullscreenCameraBufferData(byte[] data, Vector4f clipToUvOffsetAndScale, Vector2f invInputSize, bool flipVertical)
    {
        ArgumentNullException.ThrowIfNull(data);
        WriteFullscreenCameraBufferData(data.AsSpan(), clipToUvOffsetAndScale, invInputSize, flipVertical);
    }

    private void UpdateCameraBuffer(IBuffer buffer, Camera camera, Zone? zone, bool linearColorSpace)
    {
        Span<byte> data = stackalloc byte[CameraBufferSize];
        WriteMatrix4Raw(data, 0, camera.GetView().Matrix4);
        WriteMatrix4Raw(data, 64, camera.GetInverseView().Matrix4);
        WriteMatrix4Raw(data, 128, camera.GetGPUViewProj());
        WriteVector4(data, 192, new Vector4f(0.0f, 0.0f, 0.0f, 1.0f));
        var cameraPosition = camera.Node is not null ? camera.Node.WorldPosition : Vector3f.Zero;
        WriteVector4(data, 208, new Vector4f(cameraPosition, 0.0f));
        WriteFloat(data, 224, camera.GetNearClip());
        WriteFloat(data, 228, camera.GetFarClip());
        WriteVector4(data, 240, GetDepthMode(camera));
        var frustumSize = camera.GetViewSizeAt(camera.GetFarClip());
        WriteVector4(data, 256, new Vector4f(frustumSize.X, frustumSize.Y, camera.GetFarClip(), 0.0f));
        WriteVector4(data, 272, new Vector4f(0.0f, 0.0f, 1.0f, 1.0f));
        WriteVector4(data, 288, GetDepthReconstruct(camera));
        WriteVector2(data, 304, new Vector2f(1.0f / Math.Max(Width, 1), 1.0f / Math.Max(Height, 1)));
        var ambientColorGamma = camera.GetEffectiveAmbientColor() * camera.GetEffectiveAmbientBrightness();
        var effectiveAmbientColor = linearColorSpace ? ambientColorGamma.GammaToLinear() : ambientColorGamma;
        WriteVector4(data, 320, effectiveAmbientColor.ToVector4());
        WriteVector4(data, 336, GetFogParameters(camera, zone));
        var fogColorGamma = camera.GetEffectiveFogColor();
        var effectiveFogColor = linearColorSpace ? fogColorGamma.GammaToLinear() : fogColorGamma;
        WriteVector3(data, 352, effectiveFogColor.ToVector3());
        WriteFloat(data, 364, 0.0f);
        UpdateBuffer(buffer, data);
    }

    private void UpdateObjectBuffer(IBuffer buffer, Matrix4 modelMatrix, Vector4f ambient)
    {
        Span<byte> data = stackalloc byte[ObjectBufferSize];
        WriteMatrix4Raw(data, 0, modelMatrix);
        WriteVector4(data, 64, ambient);
        UpdateBuffer(buffer, data);
    }

    private void UpdateMaterialBuffer(IBuffer buffer, Material material)
    {
        Span<byte> data = stackalloc byte[MaterialBufferSize];
        WriteVector4(data, 0, material.GetShaderParameter("UOffset") is Vector4f uOffset ? uOffset : Vector4f.Zero);
        WriteVector4(data, 16, material.GetShaderParameter("VOffset") is Vector4f vOffset ? vOffset : Vector4f.Zero);
        WriteVector4(data, 32, material.GetShaderParameter("MatDiffColor") is Vector4f diff ? diff : Vector4f.One);
        WriteVector4(data, 48, material.GetShaderParameter("MatSpecColor") is Vector4f spec ? spec : new Vector4f(0.0f, 0.0f, 0.0f, 1.0f));

        var emissive = material.GetShaderParameter("MatEmissiveColor") as Vector3f? ?? Vector3f.Zero;
        var roughness = material.GetShaderParameter("Roughness") is float roughnessValue ? roughnessValue : 1.0f;
        WriteVector4(data, 64, new Vector4f(emissive, roughness));

        var env = material.GetShaderParameter("MatEnvMapColor") as Vector3f? ?? Vector3f.One;
        var metallic = material.GetShaderParameter("Metallic") is float metallicValue ? metallicValue : 1.0f;
        WriteVector4(data, 80, new Vector4f(env, metallic));

        var normalScale = material.GetShaderParameter("NormalScale") is float normalScaleValue ? normalScaleValue : 1.0f;
        var dielectricReflectance = material.GetShaderParameter("DielectricReflectance") is float dielectric ? dielectric : 0.5f;
        var alphaCutoff = material.GetShaderParameter("AlphaCutoff") is float alpha ? alpha : 0.5f;
        WriteVector4(data, 96, new Vector4f(normalScale, dielectricReflectance, alphaCutoff, 0.0f));
        WriteVector4(data, 112, new Vector4f(0.0f, 1.0f, 0.0f, 0.0f));
        UpdateBuffer(buffer, data);
    }

    private void UpdateLightBuffer(IBuffer buffer, Light? light)
    {
        Span<byte> data = stackalloc byte[LightBufferSize];
        if (light is not null && light.Node is not null)
        {
            WriteVector4(data, 0, new Vector4f(light.Node.WorldPosition, 0.0f));
            WriteVector4(data, 16, new Vector4f((-light.Node.WorldDirection).Normalized, 0.0f));
            WriteVector4(data, 32, new Vector4f(light.GetEffectiveColor().ToVector3(), light.GetEffectiveSpecularIntensity()));
        }
        else
        {
            WriteVector4(data, 0, Vector4f.Zero);
            WriteVector4(data, 16, new Vector4f(0.0f, -1.0f, 0.0f, 0.0f));
            WriteVector4(data, 32, Vector4f.Zero);
        }

        UpdateBuffer(buffer, data);
    }

    private void UpdateZoneBuffer(IBuffer buffer, Zone? zone)
    {
        Span<byte> data = stackalloc byte[ZoneBufferSize];
        WriteFloat(data, 0, GetZoneRoughnessToLodFactor(zone));
        WriteFloat(data, 4, 0.0f);
        WriteFloat(data, 8, 0.0f);
        WriteFloat(data, 12, 0.0f);
        UpdateBuffer(buffer, data);
    }

    private string ComposeShaderDefines(Pass pass, VertexBuffer vertexBuffer, Material material,
        Light? light, bool linearColorSpace, ShaderStage stage)
    {
        var usesAmbient = PassUsesAmbient(pass);
        var usesLighting = PassUsesLighting(pass);
        var passDefines = stage == ShaderStage.Vertex ? pass.GetEffectiveVertexShaderDefines() : pass.GetEffectivePixelShaderDefines();
        var textureDefines = ComposeTextureDefines(material,
            stage == ShaderStage.Vertex ? pass.VertexTextureDefines : pass.PixelTextureDefines);
        var key = new ShaderDefinesKey(
            (uint)vertexBuffer.GetElementMask(),
            textureDefines,
            passDefines,
            usesAmbient,
            usesLighting,
            linearColorSpace,
            light is not null,
            light?.GetLightType() ?? LightType.Directional);
        if (_shaderDefinesCache.TryGetValue(key, out var cachedDefines))
            return cachedDefines;

        var defines = new List<string> { "URHO3D_GEOMETRY_STATIC" };

        if (usesAmbient)
        {
            defines.Add("URHO3D_AMBIENT_PASS");
            defines.Add("URHO3D_AMBIENT_FLAT");
        }

        if (linearColorSpace)
            defines.Add("URHO3D_GAMMA_CORRECTION");

        if (usesLighting && light is not null)
            AddLightDefines(defines, light);

        if ((vertexBuffer.GetElementMask() & VertexMask.Normal) != 0)
        {
            defines.Add("URHO3D_VERTEX_HAS_NORMAL");
            defines.Add("URHO3D_VERTEX_NORMAL_AVAILABLE");
        }
        if ((vertexBuffer.GetElementMask() & VertexMask.Tangent) != 0)
        {
            defines.Add("URHO3D_VERTEX_HAS_TANGENT");
            defines.Add("URHO3D_VERTEX_TANGENT_AVAILABLE");
        }
        if ((vertexBuffer.GetElementMask() & VertexMask.TexCoord1) != 0)
            defines.Add("URHO3D_VERTEX_HAS_TEXCOORD0");
        if ((vertexBuffer.GetElementMask() & VertexMask.TexCoord2) != 0)
            defines.Add("URHO3D_VERTEX_HAS_TEXCOORD1");
        if ((vertexBuffer.GetElementMask() & VertexMask.Color) != 0)
            defines.Add("URHO3D_VERTEX_HAS_COLOR");

        if (!string.IsNullOrEmpty(textureDefines))
            defines.AddRange(textureDefines.Split(' ', StringSplitOptions.RemoveEmptyEntries));

        if (!string.IsNullOrWhiteSpace(passDefines))
            defines.AddRange(passDefines.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        var uniqueDefines = new List<string>(defines.Count);
        var seenDefines = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var define in defines)
        {
            if (seenDefines.Add(define))
                uniqueDefines.Add(define);
        }

        cachedDefines = string.Join(' ', uniqueDefines);
        _shaderDefinesCache[key] = cachedDefines;
        return cachedDefines;
    }

    private static string ComposeTextureDefines(Material material, IReadOnlyList<string> textureNames)
    {
        var defines = new List<string>(textureNames.Count);
        foreach (var textureName in textureNames)
        {
            if (material.GetTexture(textureName) is not { } texture)
                continue;

            var hint = (texture.GetLinear() ? 1 : 0) + (texture.GetSRGB() ? 1 : 0);
            if (hint > 1)
                Log.GetLogger().LogWarning("Texture {Material} cannot be both sRGB and Linear", material.GetName());
            defines.Add($"URHO3D_TEXTURE_{textureName.ToUpperInvariant()}={Math.Min(1, hint) + 1}");
        }
        return string.Join(' ', defines);
    }

    private static Pass? GetFirstOutlinePass(Technique technique)
    {
        foreach (var passName in OutlinePassNames)
        {
            if (technique.GetPass(passName) is { } pass)
                return pass;
        }

        return null;
    }

    private Pass GetOrCreateNormalBufferPass(Pass referencePass)
    {
        if (_normalBufferPasses.TryGetValue(referencePass, out var cachedPass))
            return cachedPass;

        var normalPass = new Pass(referencePass.Name);
        normalPass.SetCullMode(referencePass.CullMode);
        normalPass.SetBlendMode(referencePass.BlendMode);
        normalPass.SetDepthTestMode(referencePass.DepthTestMode);
        normalPass.SetColorWrite(referencePass.ColorWrite);
        normalPass.SetDepthWrite(referencePass.DepthWrite);
        normalPass.SetAlphaToCoverage(referencePass.AlphaToCoverage);
        normalPass.SetVertexShader(referencePass.VertexShader);
        normalPass.SetPixelShader(referencePass.PixelShader);
        normalPass.SetVertexShaderDefines(referencePass.VertexShaderDefines);
        normalPass.SetPixelShaderDefines(referencePass.PixelShaderDefines);
        normalPass.SetVertexShaderDefineExcludes(referencePass.VertexShaderDefineExcludes);
        normalPass.SetPixelShaderDefineExcludes(referencePass.PixelShaderDefineExcludes);
        normalPass.SetVertexTextureDefines(referencePass.VertexTextureDefines);
        normalPass.SetPixelTextureDefines(referencePass.PixelTextureDefines);
        normalPass.SetBlendMode(BlendMode.Replace);
        normalPass.SetColorWrite(true);
        normalPass.SetDepthWrite(false);
        normalPass.SetPixelShader(NormalBufferShaderName);
        normalPass.SetVertexShaderDefines(AppendShaderDefines(referencePass.VertexShaderDefines, NormalBufferShaderDefines));
        normalPass.SetPixelShaderDefines(AppendShaderDefines(referencePass.PixelShaderDefines, NormalBufferShaderDefines));
        _normalBufferPasses[referencePass] = normalPass;
        return normalPass;
    }

    private static Pass? GetFirstNormalBufferPass(Technique technique)
    {
        foreach (var passName in NormalBufferPassNames)
        {
            if (technique.GetPass(passName) is { } pass)
                return pass;
        }

        return null;
    }

    private static void AddLightDefines(ICollection<string> defines, Light light)
    {
        defines.Add(light.GetLightType() switch
        {
            LightType.Directional => "URHO3D_LIGHT_DIRECTIONAL",
            LightType.Spot => "URHO3D_LIGHT_SPOT",
            LightType.Point => "URHO3D_LIGHT_POINT",
            _ => "URHO3D_LIGHT_DIRECTIONAL"
        });
    }

    private static bool PassUsesAmbient(Pass pass)
    {
        return pass.Name is "base" or "alpha" or "litbase" or "litalpha";
    }

    private static bool PassUsesLighting(Pass pass)
    {
        return pass.Name is "litbase" or "litalpha" or "light";
    }

    private static string AppendShaderDefines(string existingDefines, string extraDefines)
    {
        var orderedDefines = new List<string>();
        var seenDefines = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        static void AppendUniqueDefines(List<string> destination, HashSet<string> seen, string defines)
        {
            foreach (var define in defines.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (!seen.Add(define))
                    continue;

                destination.Add(define);
            }
        }

        AppendUniqueDefines(orderedDefines, seenDefines, existingDefines);
        AppendUniqueDefines(orderedDefines, seenDefines, extraDefines);
        return string.Join(' ', orderedDefines);
    }

    private static string BuildLayoutSignature(VertexBuffer vertexBuffer)
    {
        return vertexBuffer.GetLayoutSignature();
    }

    private static LayoutElementSpec[] BuildLayoutElements(VertexBuffer vertexBuffer)
    {
        var elements = new List<LayoutElementSpec>();
        AddLayoutElement(elements, vertexBuffer, VertexElementSemantic.Position, 0, 0);
        AddLayoutElement(elements, vertexBuffer, VertexElementSemantic.BlendWeights, 0, 1);
        AddLayoutElement(elements, vertexBuffer, VertexElementSemantic.BlendIndices, 0, 2);
        AddLayoutElement(elements, vertexBuffer, VertexElementSemantic.TexCoord, 0, 3);
        AddLayoutElement(elements, vertexBuffer, VertexElementSemantic.TexCoord, 1, 4);
        AddLayoutElement(elements, vertexBuffer, VertexElementSemantic.Color, 0, 5);
        AddLayoutElement(elements, vertexBuffer, VertexElementSemantic.Normal, 0, 6);
        AddLayoutElement(elements, vertexBuffer, VertexElementSemantic.Tangent, 0, 7);
        return elements.ToArray();
    }

    /// <summary>
    /// Map layout InputIndex values to SPIR-V/HLSL locations from vertex shader reflection.
    /// Matches C++ InitializeInputLayout attribute location binding used for Vulkan/D3D.
    /// </summary>
    private static LayoutElementSpec[] ApplyVertexAttributeLocations(LayoutElementSpec[] layoutElements,
        IReadOnlyList<VertexShaderAttribute> vertexAttributes)
    {
        var inputIndices = new int[layoutElements.Length];
        Array.Fill(inputIndices, -1);
        foreach (var attribute in vertexAttributes)
        {
            // For each attribute, find the latest element in the layout that matches the attribute.
            // This is needed because the layout may contain multiple elements with the same semantic.
            var elementIndex = layoutElements.Length - 1;
            for (; elementIndex >= 0; --elementIndex)
            {
                var element = layoutElements[elementIndex];
                if (attribute.Semantic == element.Semantic && attribute.SemanticIndex == element.SemanticIndex)
                {
                    inputIndices[elementIndex] = attribute.InputIndex;
                    break;
                }
            }

            if (elementIndex < 0)
                Log.GetLogger().LogError("Attribute #{InputIndex} with semantics '{Semantic}{SemanticIndex}' is not found in the vertex layout",
                    attribute.InputIndex, RenderAPIUtils.ToShaderInputName(attribute.Semantic), attribute.SemanticIndex);
        }

        var result = new List<LayoutElementSpec>(layoutElements.Length);
        for (var i = 0; i < layoutElements.Length; ++i)
        {
            if (inputIndices[i] != -1)
                result.Add(layoutElements[i] with { InputIndex = inputIndices[i] });
        }
        return result.ToArray();
    }

    private static void AddLayoutElement(List<LayoutElementSpec> target, VertexBuffer vertexBuffer,
        VertexElementSemantic semantic, byte index, int inputIndex)
    {
        var elements = vertexBuffer.GetElements();
        var element = default(VertexElement);
        for (var i = 0; i < elements.Count; ++i)
        {
            if (elements[i].Semantic == semantic && elements[i].Index == index)
            {
                element = elements[i];
                break;
            }
        }
        if (element == default && semantic != VertexElementSemantic.Position)
            return;

        if (semantic == VertexElementSemantic.Position && vertexBuffer.GetElementOffset(semantic, index) == uint.MaxValue)
            return;

        target.Add(new LayoutElementSpec
        {
            InputIndex = inputIndex,
            BufferSlot = 0,
            NumComponents = GetNumComponents(element.Type),
            ValueType = GetValueType(element.Type),
            IsNormalized = IsNormalized(element.Type),
            RelativeOffset = element.Offset,
            Stride = vertexBuffer.GetVertexSize(),
            Frequency = InputElementFrequency.InputElementFrequencyPerVertex,
            InstanceDataStepRate = 1,
            Semantic = semantic,
            SemanticIndex = index
        });
    }

    private static uint GetNumComponents(VertexElementType type) => type switch
    {
        VertexElementType.Float or VertexElementType.Int => 1,
        VertexElementType.Vector2 => 2,
        VertexElementType.Vector3 => 3,
        VertexElementType.Vector4 or VertexElementType.UByte4 or VertexElementType.UByte4Norm => 4,
        _ => 4
    };

    private static global::RedBolt.Bindings.Diligent.ValueType GetValueType(VertexElementType type) => type switch
    {
        VertexElementType.Int => global::RedBolt.Bindings.Diligent.ValueType.VtInt32,
        VertexElementType.UByte4 or VertexElementType.UByte4Norm => global::RedBolt.Bindings.Diligent.ValueType.VtUint8,
        _ => global::RedBolt.Bindings.Diligent.ValueType.VtFloat32
    };

    private static bool IsNormalized(VertexElementType type) => type == VertexElementType.UByte4Norm;

    private static global::RedBolt.Bindings.Diligent.CullMode MapCullMode(CullMode cullMode, bool reverseCulling)
    {
        var result = cullMode switch
        {
            CullMode.None => RedBolt.Bindings.Diligent.CullMode.CullModeNone,
            CullMode.Clockwise => RedBolt.Bindings.Diligent.CullMode.CullModeFront,
            _ => RedBolt.Bindings.Diligent.CullMode.CullModeBack
        };

        if (!reverseCulling)
            return result;

        return result switch
        {
            RedBolt.Bindings.Diligent.CullMode.CullModeFront => RedBolt.Bindings.Diligent.CullMode.CullModeBack,
            RedBolt.Bindings.Diligent.CullMode.CullModeBack => RedBolt.Bindings.Diligent.CullMode.CullModeFront,
            _ => result
        };
    }

    private static global::RedBolt.Bindings.Diligent.FillMode MapFillMode(FillMode fillMode) => fillMode switch
    {
        FillMode.Wireframe or FillMode.Point => RedBolt.Bindings.Diligent.FillMode.FillModeWireframe,
        _ => RedBolt.Bindings.Diligent.FillMode.FillModeSolid
    };

    private static ComparisonFunction MapCompareMode(CompareMode compareMode) => compareMode switch
    {
        CompareMode.Always => ComparisonFunction.ComparisonFuncAlways,
        CompareMode.Equal => ComparisonFunction.ComparisonFuncEqual,
        CompareMode.NotEqual => ComparisonFunction.ComparisonFuncNotEqual,
        CompareMode.Less => ComparisonFunction.ComparisonFuncLess,
        CompareMode.LessEqual => ComparisonFunction.ComparisonFuncLessEqual,
        CompareMode.Greater => ComparisonFunction.ComparisonFuncGreater,
        CompareMode.GreaterEqual => ComparisonFunction.ComparisonFuncGreaterEqual,
        _ => ComparisonFunction.ComparisonFuncLessEqual
    };

    private static void ConfigureBlendState(GraphicsPipelineStateCreateInfo pipelineStateCreateInfo, BlendMode blendMode, bool colorWriteEnabled)
    {
        using var primaryRenderTargetBlend = CreateRenderTargetBlendDesc(blendMode, colorWriteEnabled);
        using var defaultRenderTargetBlend = new RenderTargetBlendDesc();
        using var renderTargets = new global::SharpCpp.NativeArray<RenderTargetBlendDesc>(
        [
            primaryRenderTargetBlend,
            defaultRenderTargetBlend,
            defaultRenderTargetBlend,
            defaultRenderTargetBlend,
            defaultRenderTargetBlend,
            defaultRenderTargetBlend,
            defaultRenderTargetBlend,
            defaultRenderTargetBlend
        ]);

        pipelineStateCreateInfo.GraphicsPipelineBlendDescIndependentBlendEnable = false;
        pipelineStateCreateInfo.GraphicsPipelineBlendDescRenderTargets = renderTargets;
    }

    private static RenderTargetBlendDesc CreateRenderTargetBlendDesc(BlendMode blendMode, bool colorWriteEnabled)
    {
        return new RenderTargetBlendDesc
        {
            BlendEnable = IsBlendEnabled(blendMode),
            SrcBlend = GetSourceBlend(blendMode),
            DestBlend = GetDestinationBlend(blendMode),
            SrcBlendAlpha = GetSourceAlphaBlend(blendMode),
            DestBlendAlpha = GetDestinationAlphaBlend(blendMode),
            BlendOp = GetBlendOperation(blendMode),
            BlendOpAlpha = GetBlendOperation(blendMode),
            RenderTargetWriteMask = colorWriteEnabled ? ColorMask.ColorMaskAll : ColorMask.ColorMaskNone
        };
    }

    private static ClearFramebufferPipelineSettings GetClearFramebufferPipelineSettings(ClearTarget flags)
    {
        flags &= ClearTarget.Color | ClearTarget.Depth | ClearTarget.Stencil;
        var clearStencil = (flags & ClearTarget.Stencil) == ClearTarget.Stencil;
        return new ClearFramebufferPipelineSettings(
            (flags & ClearTarget.Color) == ClearTarget.Color,
            (flags & ClearTarget.Depth) == ClearTarget.Depth,
            clearStencil,
            0xff,
            clearStencil ? (byte)0xff : (byte)0x00,
            clearStencil
                ? global::RedBolt.Bindings.Diligent.StencilOp.StencilOpReplace
                : global::RedBolt.Bindings.Diligent.StencilOp.StencilOpKeep);
    }

    private static bool IsBlendEnabled(BlendMode blendMode) => blendMode != BlendMode.Replace;

    private static BlendFactor GetSourceBlend(BlendMode blendMode) => blendMode switch
    {
        BlendMode.Replace => BlendFactor.BlendFactorOne,
        BlendMode.Add => BlendFactor.BlendFactorOne,
        BlendMode.Multiply => BlendFactor.BlendFactorDestColor,
        BlendMode.Alpha => BlendFactor.BlendFactorSrcAlpha,
        BlendMode.AddAlpha => BlendFactor.BlendFactorSrcAlpha,
        BlendMode.PremulAlpha => BlendFactor.BlendFactorOne,
        BlendMode.InvDestAlpha => BlendFactor.BlendFactorInvDestAlpha,
        BlendMode.Subtract => BlendFactor.BlendFactorOne,
        BlendMode.SubtractAlpha => BlendFactor.BlendFactorSrcAlpha,
        BlendMode.Decal => BlendFactor.BlendFactorSrcAlpha,
        _ => BlendFactor.BlendFactorOne
    };

    private static BlendFactor GetDestinationBlend(BlendMode blendMode) => blendMode switch
    {
        BlendMode.Replace => BlendFactor.BlendFactorZero,
        BlendMode.Add => BlendFactor.BlendFactorOne,
        BlendMode.Multiply => BlendFactor.BlendFactorZero,
        BlendMode.Alpha => BlendFactor.BlendFactorInvSrcAlpha,
        BlendMode.AddAlpha => BlendFactor.BlendFactorOne,
        BlendMode.PremulAlpha => BlendFactor.BlendFactorInvSrcAlpha,
        BlendMode.InvDestAlpha => BlendFactor.BlendFactorDestAlpha,
        BlendMode.Subtract => BlendFactor.BlendFactorOne,
        BlendMode.SubtractAlpha => BlendFactor.BlendFactorOne,
        BlendMode.Decal => BlendFactor.BlendFactorInvSrcAlpha,
        _ => BlendFactor.BlendFactorZero
    };

    private static BlendFactor GetSourceAlphaBlend(BlendMode blendMode) => blendMode switch
    {
        BlendMode.Replace => BlendFactor.BlendFactorOne,
        BlendMode.Add => BlendFactor.BlendFactorOne,
        BlendMode.Multiply => BlendFactor.BlendFactorDestColor,
        BlendMode.Alpha => BlendFactor.BlendFactorSrcAlpha,
        BlendMode.AddAlpha => BlendFactor.BlendFactorSrcAlpha,
        BlendMode.PremulAlpha => BlendFactor.BlendFactorOne,
        BlendMode.InvDestAlpha => BlendFactor.BlendFactorInvDestAlpha,
        BlendMode.Subtract => BlendFactor.BlendFactorOne,
        BlendMode.SubtractAlpha => BlendFactor.BlendFactorSrcAlpha,
        BlendMode.Decal => BlendFactor.BlendFactorZero,
        _ => BlendFactor.BlendFactorOne
    };

    private static BlendFactor GetDestinationAlphaBlend(BlendMode blendMode) => blendMode switch
    {
        BlendMode.Replace => BlendFactor.BlendFactorZero,
        BlendMode.Add => BlendFactor.BlendFactorOne,
        BlendMode.Multiply => BlendFactor.BlendFactorZero,
        BlendMode.Alpha => BlendFactor.BlendFactorInvSrcAlpha,
        BlendMode.AddAlpha => BlendFactor.BlendFactorOne,
        BlendMode.PremulAlpha => BlendFactor.BlendFactorInvSrcAlpha,
        BlendMode.InvDestAlpha => BlendFactor.BlendFactorDestAlpha,
        BlendMode.Subtract => BlendFactor.BlendFactorOne,
        BlendMode.SubtractAlpha => BlendFactor.BlendFactorOne,
        BlendMode.Decal => BlendFactor.BlendFactorOne,
        _ => BlendFactor.BlendFactorZero
    };

    private static BlendOperation GetBlendOperation(BlendMode blendMode) => blendMode switch
    {
        BlendMode.Subtract or BlendMode.SubtractAlpha => BlendOperation.BlendOperationRevSubtract,
        _ => BlendOperation.BlendOperationAdd
    };

    private static FillMode GetEffectiveFillMode(Camera camera, Material material)
    {
        return (FillMode)Math.Max((int)camera.GetFillMode(), (int)material.GetFillMode());
    }

    private IntRect GetEffectiveViewportRect(Viewport viewport)
    {
        var rect = viewport.GetRect();
        if (rect.Width <= 0 || rect.Height <= 0)
            return new IntRect(0, 0, Width, Height);

        return new IntRect(
            Math.Clamp(rect.Left, 0, Width),
            Math.Clamp(rect.Top, 0, Height),
            Math.Clamp(rect.Right, 0, Width),
            Math.Clamp(rect.Bottom, 0, Height));
    }

    private static Vector4f GetDepthMode(Camera camera)
    {
        if (camera.IsOrthographic())
            return new Vector4f(1.0f, 0.0f, 0.5f, 0.5f);

        return new Vector4f(0.0f, 0.0f, 0.0f, 1.0f / Math.Max(camera.GetFarClip(), MathDefs.Epsilon));
    }

    private static Vector4f GetDepthReconstruct(Camera camera)
    {
        var nearClip = camera.GetNearClip();
        var farClip = camera.GetFarClip();
        var range = Math.Max(farClip - nearClip, MathDefs.Epsilon);
        return new Vector4f(
            farClip / range,
            -nearClip / range,
            camera.IsOrthographic() ? 1.0f : 0.0f,
            camera.IsOrthographic() ? 0.0f : 1.0f);
    }

    private static Vector4f GetFogParameters(Camera camera, Zone? zone)
    {
        var farClip = camera.GetFarClip();
        var fogStart = zone is not null ? Math.Min(zone.GetFogStart(), farClip) : farClip;
        var fogEnd = zone is not null ? Math.Min(zone.GetFogEnd(), farClip) : farClip;
        if (fogStart >= fogEnd * (1.0f - 0.0001f))
            fogStart = fogEnd * (1.0f - 0.0001f);
        var fogRange = Math.Max(fogEnd - fogStart, MathDefs.Epsilon);
        return new Vector4f(fogEnd / farClip, farClip / fogRange, 0.0f, 0.0f);
    }

    private static float GetZoneRoughnessToLodFactor(Zone? zone)
    {
        return zone?.GetZoneTexture() is TextureCube zoneTexture && zoneTexture.Width > 0
            ? MathF.Log2(zoneTexture.Width)
            : 1.0f;
    }

    internal Zone? FindZone(Scene scene, Vector3f worldPosition)
    {
        scene.GetComponents(_zonesScratch);

        Zone? bestZone = null;
        for (var i = 0; i < _zonesScratch.Count; ++i)
        {
            var zone = _zonesScratch[i];
            if (zone.Node is null || !zone.IsEnabledEffective || !zone.IsInside(worldPosition))
                continue;

            if (bestZone is null || zone.GetPriority() > bestZone.GetPriority())
                bestZone = zone;
        }

        return bestZone;
    }

    private Vector4f GetAmbientForDrawable(Scene scene, Drawable drawable)
    {
        var zone = FindZone(scene, drawable.Node.WorldPosition);
        return zone is not null ? new Vector4f(zone.GetAmbientLighting(), 1.0f) : DefaultAmbient;
    }

    private static void BindTexture(PipelineResources pipeline, global::RedBolt.Bindings.Diligent.ShaderType shaderType,
        string name, ITextureView? textureView, bool logIfMissing = true)
    {
        if (textureView is null)
            return;

        if (pipeline.TryGetShaderVariable(shaderType, name, out var variable))
        {
            variable.Set(textureView);
        }
        else if (logIfMissing && ShaderBindingDiagnosticsEnabled)
            Log.GetLogger().LogWarning("Shader binding diagnostics: texture '{Texture}' was not found for stage '{Stage}'.", name, shaderType);
    }

    private static void BindTextureToAvailableStages(PipelineResources pipeline, string name, ITextureView textureView)
    {
        BindTexture(pipeline, global::RedBolt.Bindings.Diligent.ShaderType.ShaderTypeVertex, name, textureView, logIfMissing: false);
        BindTexture(pipeline, global::RedBolt.Bindings.Diligent.ShaderType.ShaderTypePixel, name, textureView, logIfMissing: false);
    }

    private static void BindTexture(FullscreenPipelineResources pipeline, global::RedBolt.Bindings.Diligent.ShaderType shaderType,
        string name, ITextureView? textureView, bool logIfMissing = true)
    {
        if (textureView is null)
            return;

        if (pipeline.TryGetShaderVariable(shaderType, name, out var variable))
        {
            variable.Set(textureView);
        }
        else if (logIfMissing && ShaderBindingDiagnosticsEnabled)
            Log.GetLogger().LogWarning("Shader binding diagnostics: fullscreen texture '{Texture}' was not found for stage '{Stage}'.", name, shaderType);
    }

    private static void BindTextureToAvailableStages(FullscreenPipelineResources pipeline, string name, ITextureView textureView)
    {
        BindTexture(pipeline, global::RedBolt.Bindings.Diligent.ShaderType.ShaderTypeVertex, name, textureView, logIfMissing: false);
        BindTexture(pipeline, global::RedBolt.Bindings.Diligent.ShaderType.ShaderTypePixel, name, textureView, logIfMissing: false);
    }

    private static void BindDynamicBuffer(PipelineResources pipeline,
        global::RedBolt.Bindings.Diligent.ShaderType shaderType, string name, IBuffer buffer, nuint size)
    {
        if (pipeline.TryGetShaderVariable(shaderType, name, out var variable))
        {
            variable.SetBufferRange(buffer, 0, size);
        }
        else if (ShaderBindingDiagnosticsEnabled)
            Log.GetLogger().LogWarning("Shader binding diagnostics: dynamic buffer '{Buffer}' was not found for stage '{Stage}'.", name, shaderType);
    }

    private static void BindDynamicBuffer(FullscreenPipelineResources pipeline,
        global::RedBolt.Bindings.Diligent.ShaderType shaderType, string name, IBuffer buffer, nuint size)
    {
        if (pipeline.TryGetShaderVariable(shaderType, name, out var variable))
        {
            variable.SetBufferRange(buffer, 0, size);
        }
        else if (ShaderBindingDiagnosticsEnabled)
            Log.GetLogger().LogWarning("Shader binding diagnostics: fullscreen dynamic buffer '{Buffer}' was not found for stage '{Stage}'.", name, shaderType);
    }

    internal static IShader CreateShader(IRenderDevice device, global::RedBolt.Bindings.Diligent.ShaderType shaderType,
        string name, string source)
    {
        using var shaderName = new CString(name);
        using var entryPoint = new CString("main");
        using var shaderSource = new CString(source);
        using var shaderDesc = new ShaderDesc
        {
            Name = shaderName.Pointer,
            ShaderType = shaderType,
            UseCombinedTextureSamplers = true
        };
        using var shaderCreateInfo = new ShaderCreateInfo
        {
            Desc = shaderDesc,
            EntryPoint = entryPoint.Pointer,
            Source = shaderSource.Pointer,
            SourceLanguage = global::RedBolt.Bindings.Diligent.ShaderSourceLanguage.ShaderSourceLanguageGlslVerbatim,
            LoadConstantBufferReflection = true
        };

        device.CreateShader(shaderCreateInfo, out var shader, out var compilerOutput);
        ReleaseIfNotNull(compilerOutput);

        if (shader.IsNull)
            throw new InvalidOperationException($"Generated CreateShader returned a null {shaderType} shader for {name}.");

        return shader;
    }

    private static IBuffer CreateUniformBuffer(IRenderDevice device, string name, uint size)
    {
        using var nativeName = new CString(name);
        using var bufferDesc = new BufferDesc();
        bufferDesc.Name = nativeName.Pointer;
        bufferDesc.Size = (nuint)size;
        bufferDesc.BindFlags = BindFlags.BindUniformBuffer;
        bufferDesc.Usage = Usage.UsageDefault;
        bufferDesc.CPUAccessFlags = CpuAccessFlags.CpuAccessNone;
        device.CreateBuffer(bufferDesc, null!, out var buffer);
        if (buffer.IsNull)
            throw new InvalidOperationException($"CreateBuffer returned null for uniform buffer {name}.");
        return buffer;
    }

    private static IBuffer CreateBuffer(IRenderDevice device, IDeviceContext immediateContext, string name, byte[] data,
        ulong size, BindFlags bindFlags, uint elementStride)
    {
        using var nativeName = new CString(name);
        using var bufferDesc = new BufferDesc();
        bufferDesc.Name = nativeName.Pointer;
        bufferDesc.Size = (nuint)size;
        bufferDesc.BindFlags = bindFlags;
        bufferDesc.Usage = Usage.UsageImmutable;
        bufferDesc.ElementByteStride = elementStride;
        using var dataHandle = new PinnedBuffer(data);
        using var bufferData = new BufferData(dataHandle.Pointer, (nuint)size, immediateContext);
        device.CreateBuffer(bufferDesc, bufferData, out var buffer);
        if (buffer.IsNull)
            throw new InvalidOperationException($"CreateBuffer returned null for {name}.");
        return buffer;
    }

    private static IBuffer CreateIndexBuffer(IRenderDevice device, IDeviceContext immediateContext, IndexBuffer indexBuffer, uint[] indices)
    {
        byte[] data;
        if (indexBuffer.GetIndexSize() == 4)
        {
            data = new byte[indices.Length * sizeof(uint)];
            Buffer.BlockCopy(indices, 0, data, 0, data.Length);
        }
        else
        {
            var narrowed = new ushort[indices.Length];
            for (var i = 0; i < indices.Length; ++i)
                narrowed[i] = checked((ushort)indices[i]);
            data = new byte[narrowed.Length * sizeof(ushort)];
            Buffer.BlockCopy(narrowed, 0, data, 0, data.Length);
        }

        return CreateBuffer(device, immediateContext, "RedBoltModelIB", data,
            (ulong)data.Length, BindFlags.BindIndexBuffer, indexBuffer.GetIndexSize());
    }

    private unsafe void UpdateBuffer(IBuffer buffer, ReadOnlySpan<byte> data)
    {
        if (_immediateContext is null)
            return;

        fixed (byte* dataPointer = data)
        {
            _immediateContext.UpdateBuffer(buffer, 0, (nuint)data.Length, (nint)dataPointer,
                ResourceStateTransitionMode.ResourceStateTransitionModeTransition);
        }
    }

    private static TextureDescOwner CreateTextureDesc()
    {
        var factory = DiligentNativeBindings.GetEngineFactoryOpenGL();
        var apiInfo = factory.GetAPIInfo();
        return new TextureDescOwner((int)apiInfo.TextureDescSize);
    }

    private static TextureViewDescOwner CreateTextureViewDesc()
    {
        var factory = DiligentNativeBindings.GetEngineFactoryOpenGL();
        var apiInfo = factory.GetAPIInfo();
        return new TextureViewDescOwner((int)apiInfo.TextureViewDescSize);
    }

    private static ResolveTextureSubresourceAttribsOwner CreateResolveTextureSubresourceAttribs()
    {
        return new ResolveTextureSubresourceAttribsOwner();
    }

    private static void WriteFloat(Span<byte> buffer, int offset, float value)
    {
        MemoryMarshal.Write(buffer.Slice(offset, sizeof(float)), in value);
    }

    private static void WriteVector2(Span<byte> buffer, int offset, Vector2f value)
    {
        WriteFloat(buffer, offset + 0, value.X);
        WriteFloat(buffer, offset + 4, value.Y);
    }

    private static void WriteVector3(Span<byte> buffer, int offset, Vector3f value)
    {
        WriteFloat(buffer, offset + 0, value.X);
        WriteFloat(buffer, offset + 4, value.Y);
        WriteFloat(buffer, offset + 8, value.Z);
    }

    private static void WriteVector4(Span<byte> buffer, int offset, Vector4f value)
    {
        WriteFloat(buffer, offset + 0, value.X);
        WriteFloat(buffer, offset + 4, value.Y);
        WriteFloat(buffer, offset + 8, value.Z);
        WriteFloat(buffer, offset + 12, value.W);
    }

    private static void WriteMatrix4Raw(Span<byte> buffer, int offset, Matrix4 value)
    {
        // rbfx stores Matrix4 in row-major CPU order and memcpy's it into uniform buffers.
        // GLSL consumes mat4 from std140 as column-major storage and shaders use vec4 * mat4,
        // so uploading raw rbfx memory yields the expected transposed shader-side math.
        System.Runtime.InteropServices.MemoryMarshal.Write(buffer.Slice(offset, 16 * sizeof(float)), in value);
    }

    private static void WriteMatrix4(byte[] buffer, int offset, Matrix4 value)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        WriteMatrix4Raw(buffer.AsSpan(), offset, value);
    }

    private static void CheckSdlGl(int result, string operation)
    {
        if (result != 0)
            throw new InvalidOperationException($"{operation} failed with {result}.");
    }

    private static void ReleaseIfNotNull(IObject? value)
    {
        if (value is not null && !value.IsNull)
            _ = value.Release();
    }

    private static bool IsRenderDiagnosticsEnabled()
    {
        var value = Environment.GetEnvironmentVariable("REDBOLT_RENDER_DIAGNOSTICS");
        return value is not null && value != "0" && !value.Equals("false", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsShaderBindingDiagnosticsEnabled()
    {
        var value = Environment.GetEnvironmentVariable("REDBOLT_SHADER_BINDING_DIAGNOSTICS");
        return value is not null && value != "0" && !value.Equals("false", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsShaderReflectionDiagnosticsEnabled()
    {
        var value = Environment.GetEnvironmentVariable("REDBOLT_SHADER_REFLECTION_DIAGNOSTICS");
        return value is not null && value != "0" && !value.Equals("false", StringComparison.OrdinalIgnoreCase);
    }

    private void DumpFirstPipelineDiagnostics(Pass pass, PipelineKey key, Shader vertexShaderResource, Shader pixelShaderResource,
        string vertexDefines, string pixelDefines, IShader vertexShader, IShader pixelShader, IPipelineState pipelineState,
        IShaderResourceBinding shaderResources)
    {
        if (!ShaderReflectionDiagnosticsEnabled || _shaderReflectionDiagnosticsDumped)
            return;

        _shaderReflectionDiagnosticsDumped = true;

        var diagnosticBaseName = $"{SanitizeDiagnosticName(pass.VertexShader)}_{SanitizeDiagnosticName(pass.PixelShader)}";
        var vertexPath = Path.Combine(Path.GetTempPath(), $"{diagnosticBaseName}.vs.glsl");
        var pixelPath = Path.Combine(Path.GetTempPath(), $"{diagnosticBaseName}.ps.glsl");

        try
        {
            var virtualFileSystem = GetSubsystem<VirtualFileSystem>()!;
            using var rootMount = new MountPointGuard(virtualFileSystem.MountRoot());
            virtualFileSystem.WriteAllText(
                new FileIdentifier("file", vertexPath),
                vertexShaderResource.PrepareSource(ShaderStage.Vertex, vertexDefines, RenderBackend.OpenGL, ShaderExtraCode));
            virtualFileSystem.WriteAllText(
                new FileIdentifier("file", pixelPath),
                pixelShaderResource.PrepareSource(ShaderStage.Pixel, pixelDefines, RenderBackend.OpenGL, ShaderExtraCode));
        }
        catch (Exception error)
        {
            Log.GetLogger().LogWarning(error, "Shader reflection diagnostics: failed to write shader source dump.");
        }

        Log.GetLogger().LogDebug(
            "Shader reflection diagnostics: first pipeline '{PassVertexShader}/{PassPixelShader}', layout='{Layout}', vertexSource='{VertexSource}', pixelSource='{PixelSource}'.",
            pass.VertexShader, pass.PixelShader, key.LayoutSignature, vertexPath, pixelPath);
        DumpShaderResources("vertex shader", vertexShader);
        DumpShaderResources("pixel shader", pixelShader);
        DumpPipelineVariables("pipeline", pipelineState, shaderResources);
    }

    private static void DumpShaderResources(string label, IShader shader)
    {
        var resourceCount = shader.GetResourceCount();
        Log.GetLogger().LogDebug("Shader reflection diagnostics: {Label} resourceCount={ResourceCount}.", label, resourceCount);
        for (uint resourceIndex = 0; resourceIndex < resourceCount; ++resourceIndex)
        {
            using var resourceDesc = new global::RedBolt.Bindings.Diligent.ShaderResourceDesc();
            shader.GetResourceDesc(resourceIndex, resourceDesc);
            var resourceName = PtrToUtf8String(resourceDesc.Name);
            var line =
                $"Shader reflection diagnostics: {label} resource[{resourceIndex}] name='{resourceName}', type={resourceDesc.Type}, arraySize={resourceDesc.ArraySize}";
            if (resourceDesc.Type == ShaderResourceType.ShaderResourceTypeConstantBuffer)
            {
                var bufferDesc = shader.GetConstantBufferDesc(resourceIndex);
                line += $", size={bufferDesc.Size}, variableCount={bufferDesc.NumVariables}";
            }

            Log.GetLogger().LogDebug(line + ".");
        }
    }

    private static void DumpPipelineVariables(string label, IPipelineState pipelineState, IShaderResourceBinding shaderResources)
    {
        using var pipelineDesc = pipelineState.GetDesc();
        Log.GetLogger().LogDebug(
            "Shader reflection diagnostics: {Label} defaultVariableType={DefaultVariableType}, staticSignatures={StaticSignatures}.",
            label,
            pipelineDesc.ResourceLayoutDefaultVariableType,
            pipelineState.GetResourceSignatureCount());

        DumpVariableList(label, "static", global::RedBolt.Bindings.Diligent.ShaderType.ShaderTypeVertex,
            pipelineState.GetStaticVariableCount(global::RedBolt.Bindings.Diligent.ShaderType.ShaderTypeVertex),
            index => pipelineState.GetStaticVariableByIndex(global::RedBolt.Bindings.Diligent.ShaderType.ShaderTypeVertex, index));
        DumpVariableList(label, "static", global::RedBolt.Bindings.Diligent.ShaderType.ShaderTypePixel,
            pipelineState.GetStaticVariableCount(global::RedBolt.Bindings.Diligent.ShaderType.ShaderTypePixel),
            index => pipelineState.GetStaticVariableByIndex(global::RedBolt.Bindings.Diligent.ShaderType.ShaderTypePixel, index));
        DumpVariableList(label, "srb", global::RedBolt.Bindings.Diligent.ShaderType.ShaderTypeVertex,
            shaderResources.GetVariableCount(global::RedBolt.Bindings.Diligent.ShaderType.ShaderTypeVertex),
            index => shaderResources.GetVariableByIndex(global::RedBolt.Bindings.Diligent.ShaderType.ShaderTypeVertex, index));
        DumpVariableList(label, "srb", global::RedBolt.Bindings.Diligent.ShaderType.ShaderTypePixel,
            shaderResources.GetVariableCount(global::RedBolt.Bindings.Diligent.ShaderType.ShaderTypePixel),
            index => shaderResources.GetVariableByIndex(global::RedBolt.Bindings.Diligent.ShaderType.ShaderTypePixel, index));
    }

    private static void DumpVariableList(string label, string scope, global::RedBolt.Bindings.Diligent.ShaderType shaderType,
        uint count, Func<uint, IShaderResourceVariable> getVariable)
    {
        Log.GetLogger().LogDebug(
            "Shader reflection diagnostics: {Label} {Scope} {ShaderType} variableCount={VariableCount}.",
            label, scope, shaderType, count);
        for (uint variableIndex = 0; variableIndex < count; ++variableIndex)
        {
            var variable = getVariable(variableIndex);
            using var resourceDesc = new global::RedBolt.Bindings.Diligent.ShaderResourceDesc();
            variable.GetResourceDesc(resourceDesc);
            Log.GetLogger().LogDebug(
                "Shader reflection diagnostics: {Label} {Scope} {ShaderType} variable[{VariableIndex}] name='{Name}', type={Type}, arraySize={ArraySize}, variableType={VariableType}.",
                label,
                scope,
                shaderType,
                variableIndex,
                PtrToUtf8String(resourceDesc.Name),
                resourceDesc.Type,
                resourceDesc.ArraySize,
                variable.GetType());
        }
    }

    private static string PtrToUtf8String(IntPtr pointer)
    {
        return pointer == IntPtr.Zero ? string.Empty : Marshal.PtrToStringUTF8(pointer) ?? string.Empty;
    }

    private static string SanitizeDiagnosticName(string name)
    {
        return string.Create(name.Length, name, static (span, value) =>
        {
            for (var i = 0; i < value.Length; ++i)
                span[i] = char.IsLetterOrDigit(value[i]) ? value[i] : '_';
        });
    }

    private static Dictionary<PipelineVariableKey, IShaderResourceVariable> BuildShaderVariableLookup(
        IShaderResourceBinding shaderResources, ShaderProgramReflection reflection)
    {
        var result = new Dictionary<PipelineVariableKey, IShaderResourceVariable>();

        for (var groupIndex = 0; groupIndex < RenderApiLimits.MaxShaderParameterGroups; ++groupIndex)
        {
            var uniformBuffer = reflection.GetUniformBuffer((ShaderParameterGroup)groupIndex);
            if (uniformBuffer is not null && uniformBuffer.Size != 0)
                AddShaderVariableLookupEntries(result, shaderResources, uniformBuffer.InternalName);
        }

        foreach (var resource in reflection.ShaderResources.Values)
            AddShaderVariableLookupEntries(result, shaderResources, resource.InternalName);

        foreach (var resource in reflection.UnorderedAccessViews.Values)
            AddShaderVariableLookupEntries(result, shaderResources, resource.InternalName);

        return result;
    }

    private static void AddShaderVariableLookupEntries(Dictionary<PipelineVariableKey, IShaderResourceVariable> result,
        IShaderResourceBinding shaderResources, string name)
    {
        AddShaderVariableLookupEntry(result, shaderResources,
            global::RedBolt.Bindings.Diligent.ShaderType.ShaderTypeVertex, name);
        AddShaderVariableLookupEntry(result, shaderResources,
            global::RedBolt.Bindings.Diligent.ShaderType.ShaderTypePixel, name);
    }

    private static void AddShaderVariableLookupEntry(Dictionary<PipelineVariableKey, IShaderResourceVariable> result,
        IShaderResourceBinding shaderResources, global::RedBolt.Bindings.Diligent.ShaderType shaderType, string name)
    {
        using var internalName = new CString(name);
        unsafe
        {
            ref var internalNameRef = ref System.Runtime.CompilerServices.Unsafe.AsRef<sbyte>((void*)internalName.Pointer);
            var variable = shaderResources.GetVariableByName(shaderType, in internalNameRef);
            if (variable is not null && !variable.IsNull)
                result[new PipelineVariableKey(shaderType, name)] = variable;
        }
    }

    private static string AppendShaderDefine(string defines, string define)
    {
        if (string.IsNullOrWhiteSpace(define))
            return defines;
        if (string.IsNullOrWhiteSpace(defines))
            return define;

        return ContainsShaderDefine(defines, define)
            ? defines
            : string.Concat(defines, " ", define);
    }

    private string GetOrCreateFullscreenShaderDefines(string shaderDefines)
    {
        const string geometryStaticDefine = "URHO3D_GEOMETRY_STATIC";

        if (string.IsNullOrWhiteSpace(shaderDefines))
            return geometryStaticDefine;
        if (ContainsShaderDefine(shaderDefines, geometryStaticDefine))
            return shaderDefines;
        if (_fullscreenShaderDefinesCache.TryGetValue(shaderDefines, out var cachedDefines))
            return cachedDefines;

        cachedDefines = AppendShaderDefine(shaderDefines, geometryStaticDefine);
        _fullscreenShaderDefinesCache[shaderDefines] = cachedDefines;
        return cachedDefines;
    }

    private static bool ContainsShaderDefine(string defines, string define)
    {
        var remaining = defines.AsSpan().Trim();
        while (!remaining.IsEmpty)
        {
            var separatorIndex = remaining.IndexOf(' ');
            var token = separatorIndex >= 0 ? remaining[..separatorIndex] : remaining;
            if (token.Equals(define, StringComparison.Ordinal))
                return true;

            if (separatorIndex < 0)
                break;

            remaining = remaining[(separatorIndex + 1)..].TrimStart();
        }

        return false;
    }

    private readonly record struct LayoutElementSpec(
        int InputIndex,
        uint BufferSlot,
        uint NumComponents,
        global::RedBolt.Bindings.Diligent.ValueType ValueType,
        bool IsNormalized,
        uint RelativeOffset,
        uint Stride,
        InputElementFrequency Frequency,
        uint InstanceDataStepRate,
        VertexElementSemantic Semantic = VertexElementSemantic.Position,
        int SemanticIndex = 0)
    {
        public LayoutElementSpec()
            : this(0, 0, 0, global::RedBolt.Bindings.Diligent.ValueType.VtFloat32, false, 0, 0,
                InputElementFrequency.InputElementFrequencyPerVertex, 1)
        {
        }
    }

    private readonly record struct PipelineKey(string VertexShader, string PixelShader, string VertexDefines,
        string PixelDefines, Material Material, string LayoutSignature, CullMode CullMode, FillMode FillMode,
        CompareMode DepthTestMode, bool DepthWrite, bool AlphaToCoverage, bool ReverseCulling,
        global::RedBolt.Bindings.Diligent.TextureFormat ColorFormat,
        global::RedBolt.Bindings.Diligent.TextureFormat DepthFormat);

    private readonly record struct FullscreenPipelineKey(string ShaderName, string ShaderDefines,
        BlendMode BlendMode,
        bool BilinearColorSampler, global::RedBolt.Bindings.Diligent.TextureFormat ColorFormat,
        global::RedBolt.Bindings.Diligent.TextureFormat DepthFormat);
    private readonly record struct ShaderDefinesKey(
        uint ElementMask,
        string TextureDefines,
        string PassDefines,
        bool UsesAmbient,
        bool UsesLighting,
        bool LinearColorSpace,
        bool HasLight,
        LightType LightType);

    private readonly record struct ClearFramebufferPipelineKey(ClearTarget Flags,
        global::RedBolt.Bindings.Diligent.TextureFormat ColorFormat,
        global::RedBolt.Bindings.Diligent.TextureFormat DepthFormat);

    private readonly record struct ClearFramebufferPipelineSettings(
        bool ColorWriteEnabled,
        bool DepthWriteEnabled,
        bool StencilEnabled,
        byte StencilReadMask,
        byte StencilWriteMask,
        global::RedBolt.Bindings.Diligent.StencilOp StencilPassOp);

    private readonly record struct PipelineVariableKey(global::RedBolt.Bindings.Diligent.ShaderType ShaderType, string Name);

    private enum CustomUniformKind
    {
        Float,
        Int,
        UInt,
        Bool,
        Vector2,
        Vector3,
        Vector4,
        Matrix4
    }

    private readonly record struct CustomUniformField(string UniformName, string ParameterName, CustomUniformKind Kind, int Offset);

    private readonly record struct CustomUniformLayout(uint Size, IReadOnlyList<CustomUniformField> Fields);

    private sealed class PipelineResources : IDisposable
    {
        public PipelineResources(IPipelineState pipelineState, IShader vertexShader, IShader pixelShader,
            IBuffer cameraBuffer, IBuffer objectBuffer, IBuffer materialBuffer, IBuffer? customBuffer, CustomUniformLayout customLayout,
            IBuffer lightBuffer, IBuffer zoneBuffer,
            IShaderResourceBinding shaderResources, Dictionary<PipelineVariableKey, IShaderResourceVariable> shaderVariables,
            TemporaryLayoutArray layoutArray)
        {
            PipelineState = pipelineState;
            VertexShader = vertexShader;
            PixelShader = pixelShader;
            CameraBuffer = cameraBuffer;
            ObjectBuffer = objectBuffer;
            MaterialBuffer = materialBuffer;
            CustomBuffer = customBuffer;
            CustomLayout = customLayout;
            CustomParameterScratch = customLayout.Size > 0u ? new byte[checked((int)customLayout.Size)] : null;
            LightBuffer = lightBuffer;
            ZoneBuffer = zoneBuffer;
            ShaderResources = shaderResources;
            ShaderVariables = shaderVariables;
            LayoutArray = layoutArray;
        }

        public IPipelineState PipelineState { get; }

        public IShader VertexShader { get; }

        public IShader PixelShader { get; }

        public IBuffer CameraBuffer { get; }

        public IBuffer ObjectBuffer { get; }

        public IBuffer MaterialBuffer { get; }

        public IBuffer? CustomBuffer { get; }

        public CustomUniformLayout CustomLayout { get; }

        public byte[]? CustomParameterScratch { get; }

        public IBuffer LightBuffer { get; }

        public IBuffer ZoneBuffer { get; }

        public IShaderResourceBinding ShaderResources { get; }

        public Dictionary<PipelineVariableKey, IShaderResourceVariable> ShaderVariables { get; }

        public TemporaryLayoutArray LayoutArray { get; }

        public bool TryGetShaderVariable(global::RedBolt.Bindings.Diligent.ShaderType shaderType, string name,
            out IShaderResourceVariable variable)
        {
            return ShaderVariables.TryGetValue(new PipelineVariableKey(shaderType, name), out variable!);
        }

        public void Dispose()
        {
            LayoutArray.Dispose();
            ReleaseIfNotNull(ShaderResources);
            ReleaseIfNotNull(ZoneBuffer);
            ReleaseIfNotNull(LightBuffer);
            ReleaseIfNotNull(CustomBuffer);
            ReleaseIfNotNull(MaterialBuffer);
            ReleaseIfNotNull(ObjectBuffer);
            ReleaseIfNotNull(CameraBuffer);
            ReleaseIfNotNull(PipelineState);
            ReleaseIfNotNull(PixelShader);
            ReleaseIfNotNull(VertexShader);
        }
    }

    private sealed class FullscreenPipelineResources : IDisposable
    {
        public FullscreenPipelineResources(IPipelineState pipelineState, IShader vertexShader, IShader pixelShader,
            IShaderResourceBinding shaderResources, Dictionary<PipelineVariableKey, IShaderResourceVariable> shaderVariables,
            IBuffer frameBuffer, IBuffer cameraBuffer, IBuffer objectBuffer, IBuffer? customBuffer,
            CustomUniformLayout customLayout, TemporaryLayoutArray layoutArray)
        {
            PipelineState = pipelineState;
            VertexShader = vertexShader;
            PixelShader = pixelShader;
            ShaderResources = shaderResources;
            ShaderVariables = shaderVariables;
            FrameBuffer = frameBuffer;
            CameraBuffer = cameraBuffer;
            ObjectBuffer = objectBuffer;
            CustomBuffer = customBuffer;
            CustomLayout = customLayout;
            CustomParameterScratch = customLayout.Size > 0u ? new byte[checked((int)customLayout.Size)] : null;
            LayoutArray = layoutArray;
        }

        public IPipelineState PipelineState { get; }

        public IShader VertexShader { get; }

        public IShader PixelShader { get; }

        public IShaderResourceBinding ShaderResources { get; }

        public Dictionary<PipelineVariableKey, IShaderResourceVariable> ShaderVariables { get; }

        public IBuffer FrameBuffer { get; }

        public IBuffer CameraBuffer { get; }

        public IBuffer ObjectBuffer { get; }

        public IBuffer? CustomBuffer { get; }

        public CustomUniformLayout CustomLayout { get; }

        public byte[]? CustomParameterScratch { get; }

        public TemporaryLayoutArray LayoutArray { get; }

        public bool TryGetShaderVariable(global::RedBolt.Bindings.Diligent.ShaderType shaderType, string name,
            out IShaderResourceVariable variable)
        {
            return ShaderVariables.TryGetValue(new PipelineVariableKey(shaderType, name), out variable!);
        }

        public void Dispose()
        {
            LayoutArray.Dispose();
            ReleaseIfNotNull(CustomBuffer);
            ReleaseIfNotNull(ObjectBuffer);
            ReleaseIfNotNull(CameraBuffer);
            ReleaseIfNotNull(FrameBuffer);
            ReleaseIfNotNull(ShaderResources);
            ReleaseIfNotNull(PipelineState);
            ReleaseIfNotNull(PixelShader);
            ReleaseIfNotNull(VertexShader);
        }
    }

    private sealed class TemporaryLayoutArray : IDisposable
    {
        private readonly IntPtr _memory;
        private readonly CString _semantic;

        public TemporaryLayoutArray(IReadOnlyList<LayoutElementSpec> elements)
        {
            _semantic = new CString("ATTRIB");
            Count = elements.Count;
            if (Count == 0)
                throw new InvalidOperationException("Graphics pipeline layout may not be empty.");

            var layoutElementSize = (int)DiligentNativeBindings.GetEngineFactoryOpenGL().GetAPIInfo().LayoutElementSize;
            _memory = Marshal.AllocHGlobal(Count * layoutElementSize);
            for (var i = 0; i < Count; ++i)
            {
                var ptr = _memory + i * layoutElementSize;
                using var element = LayoutElement.CreateManagedWrapper(ptr, ownsHandle: false);
                var spec = elements[i];
                element.HLSLSemantic = _semantic.Pointer;
                element.InputIndex = checked((uint)spec.InputIndex);
                element.BufferSlot = spec.BufferSlot;
                element.NumComponents = spec.NumComponents;
                element.ValueType = spec.ValueType;
                element.IsNormalized = spec.IsNormalized;
                element.RelativeOffset = spec.RelativeOffset;
                element.Stride = spec.Stride;
                element.Frequency = spec.Frequency;
                element.InstanceDataStepRate = spec.InstanceDataStepRate;
            }

            First = LayoutElement.CreateManagedWrapper(_memory, ownsHandle: false);
        }

        public LayoutElement First { get; }

        public int Count { get; }

        public void Dispose()
        {
            First.Dispose();
            _semantic.Dispose();
            if (_memory != IntPtr.Zero)
                Marshal.FreeHGlobal(_memory);
        }
    }

    private sealed class TextureDescOwner : IDisposable
    {
        private readonly IntPtr _memory;

        public TextureDescOwner(int size)
        {
            _memory = Marshal.AllocHGlobal(size);
            Marshal.Copy(new byte[size], 0, _memory, size);
            Desc = TextureDesc.CreateManagedWrapper(_memory, ownsHandle: false);
            Desc.ArraySize = 1;
            Desc.MipLevels = 1;
            Desc.SampleCount = 1;
            Desc.ImmediateContextMask = 1;
        }

        public TextureDesc Desc { get; }

        public static implicit operator TextureDesc(TextureDescOwner owner) => owner.Desc;

        public void Dispose()
        {
            Desc.Dispose();
            if (_memory != IntPtr.Zero)
                Marshal.FreeHGlobal(_memory);
        }
    }

    private sealed class TextureViewDescOwner : IDisposable
    {
        private readonly IntPtr _memory;

        public TextureViewDescOwner(int size)
        {
            _memory = Marshal.AllocHGlobal(size);
            Marshal.Copy(new byte[size], 0, _memory, size);
            Desc = TextureViewDesc.CreateManagedWrapper(_memory, ownsHandle: false);
        }

        public TextureViewDesc Desc { get; }

        public static implicit operator TextureViewDesc(TextureViewDescOwner owner) => owner.Desc;

        public void Dispose()
        {
            Desc.Dispose();
            if (_memory != IntPtr.Zero)
                Marshal.FreeHGlobal(_memory);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ResolveTextureSubresourceAttribsData
    {
        public uint SrcMipLevel;
        public uint SrcSlice;
        public ResourceStateTransitionMode SrcTextureTransitionMode;
        public uint DstMipLevel;
        public uint DstSlice;
        public ResourceStateTransitionMode DstTextureTransitionMode;
        public global::RedBolt.Bindings.Diligent.TextureFormat Format;
    }

    private sealed class ResolveTextureSubresourceAttribsOwner : IDisposable
    {
        private readonly IntPtr _memory;

        public ResolveTextureSubresourceAttribsOwner()
        {
            _memory = Marshal.AllocHGlobal(Marshal.SizeOf<ResolveTextureSubresourceAttribsData>());
            Marshal.Copy(new byte[Marshal.SizeOf<ResolveTextureSubresourceAttribsData>()], 0, _memory,
                Marshal.SizeOf<ResolveTextureSubresourceAttribsData>());
            Attribs = ResolveTextureSubresourceAttribs.CreateManagedWrapper(_memory, ownsHandle: false);
        }

        public ResolveTextureSubresourceAttribs Attribs { get; }

        public static implicit operator ResolveTextureSubresourceAttribs(ResolveTextureSubresourceAttribsOwner owner) => owner.Attribs;

        public void Dispose()
        {
            Attribs.Dispose();
            if (_memory != IntPtr.Zero)
                Marshal.FreeHGlobal(_memory);
        }
    }

    private sealed class UploadedGeometry : IDisposable
    {
        public UploadedGeometry(IBuffer vertexBuffer, IBuffer indexBuffer, DrawIndexedAttribs drawIndexedAttribs)
        {
            VertexBuffer = vertexBuffer;
            IndexBuffer = indexBuffer;
            DrawIndexedAttribs = drawIndexedAttribs;
        }

        public IBuffer VertexBuffer { get; }

        public IBuffer IndexBuffer { get; }

        public DrawIndexedAttribs DrawIndexedAttribs { get; }

        public void Dispose()
        {
            DrawIndexedAttribs.Dispose();
        }
    }

    private sealed class TemporaryImmutableSamplers : IDisposable
    {
        private readonly CString[] _names;
        private readonly SamplerDesc[] _descriptions;
        private readonly ImmutableSamplerDesc[] _samplers;
        private readonly IntPtr _memory;

        public TemporaryImmutableSamplers(ImmutableSamplersDesc overrides, ShaderProgramReflection reflection,
            RenderDevice? renderDevice, global::RedBolt.Bindings.Diligent.ShaderType stages)
        {
            var resources = reflection.ShaderResources.ToArray();
            Count = (uint)resources.Length;
            _names = new CString[resources.Length];
            _descriptions = new SamplerDesc[resources.Length];
            _samplers = new ImmutableSamplerDesc[resources.Length];
            if (resources.Length == 0)
                return;

            var immutableSamplerDescSize = (int)DiligentNativeBindings.GetEngineFactoryOpenGL().GetAPIInfo().ImmutableSamplerDescSize;
            _memory = Marshal.AllocHGlobal(checked(immutableSamplerDescSize * resources.Length));
            for (var i = 0; i < resources.Length; ++i)
            {
                var pair = resources[i];
                var source = FindSampler(overrides, pair.Key) ?? SamplerStateDesc.Bilinear();
                _names[i] = new CString(pair.Value.InternalName);
                _descriptions[i] = CreateSamplerDesc(source, renderDevice);
                _samplers[i] = ImmutableSamplerDesc.CreateManagedWrapper(
                    IntPtr.Add(_memory, immutableSamplerDescSize * i), false);
                _samplers[i].ShaderStages = stages;
                _samplers[i].SamplerOrTextureName = _names[i].Pointer;
                _samplers[i].Desc = _descriptions[i];
            }
            First = _samplers[0];
        }

        public ImmutableSamplerDesc? First { get; }
        public uint Count { get; }

        public void Dispose()
        {
            foreach (var sampler in _samplers)
                sampler?.Dispose();
            foreach (var description in _descriptions)
                description?.Dispose();
            foreach (var name in _names)
                name?.Dispose();
            if (_memory != IntPtr.Zero)
                Marshal.FreeHGlobal(_memory);
        }

        private static SamplerStateDesc? FindSampler(ImmutableSamplersDesc overrides, StringHash name)
        {
            for (var i = 0; i < overrides.Size; ++i)
            {
                if (overrides.Names[i] == name)
                    return overrides.Descriptions[i];
            }
            return null;
        }

        private static SamplerDesc CreateSamplerDesc(SamplerStateDesc source, RenderDevice? renderDevice)
        {
            var filter = source.FilterMode == TextureFilterMode.Default
                ? renderDevice?.GetDefaultTextureFilterMode() ?? TextureFilterMode.Trilinear
                : source.FilterMode;
            var compareIndex = source.ShadowCompare ? 1 : 0;
            var minMag = filter switch
            {
                TextureFilterMode.Nearest or TextureFilterMode.NearestAnisotropic => FilterType.FilterTypePoint,
                TextureFilterMode.Anisotropic => FilterType.FilterTypeAnisotropic,
                _ => FilterType.FilterTypeLinear
            };
            var mip = filter switch
            {
                TextureFilterMode.Nearest or TextureFilterMode.Bilinear => FilterType.FilterTypePoint,
                TextureFilterMode.Anisotropic => FilterType.FilterTypeAnisotropic,
                _ => FilterType.FilterTypeLinear
            };
            if (compareIndex != 0)
            {
                minMag = minMag switch
                {
                    FilterType.FilterTypePoint => FilterType.FilterTypeComparisonPoint,
                    FilterType.FilterTypeAnisotropic => FilterType.FilterTypeComparisonAnisotropic,
                    _ => FilterType.FilterTypeComparisonLinear
                };
                mip = mip switch
                {
                    FilterType.FilterTypePoint => FilterType.FilterTypeComparisonPoint,
                    FilterType.FilterTypeAnisotropic => FilterType.FilterTypeComparisonAnisotropic,
                    _ => FilterType.FilterTypeComparisonLinear
                };
            }
            return new SamplerDesc
            {
                MinFilter = minMag,
                MagFilter = minMag,
                MipFilter = mip,
                AddressU = ToNative(source[TextureCoordinate.U]),
                AddressV = ToNative(source[TextureCoordinate.V]),
                AddressW = ToNative(source[TextureCoordinate.W]),
                MaxAnisotropy = source.Anisotropy != 0 ? source.Anisotropy : (uint)(renderDevice?.GetDefaultTextureAnisotropy() ?? 1),
                ComparisonFunc = ComparisonFunction.ComparisonFuncLessEqual,
                MinLOD = source.MinLod,
                MaxLOD = source.MaxLod
            };
        }

        private static global::RedBolt.Bindings.Diligent.TextureAddressMode ToNative(TextureAddressMode value) => value switch
        {
            TextureAddressMode.Wrap => global::RedBolt.Bindings.Diligent.TextureAddressMode.TextureAddressWrap,
            TextureAddressMode.Mirror => global::RedBolt.Bindings.Diligent.TextureAddressMode.TextureAddressMirror,
            TextureAddressMode.Clamp => global::RedBolt.Bindings.Diligent.TextureAddressMode.TextureAddressClamp,
            _ => global::RedBolt.Bindings.Diligent.TextureAddressMode.TextureAddressUnknown
        };
    }

    private sealed class UploadedTexture : IDisposable
    {
        private readonly Dictionary<(TextureViewType ViewType, int Slice), ITextureView> _sliceViews = [];
        private readonly Dictionary<RawTextureUAVKey, ITextureView> _uavs = [];

        public UploadedTexture(Texture sourceTexture, ITexture texture, ITextureView shaderView, ITextureView? renderTargetView,
            ITextureView? depthStencilView, ITextureView? readOnlyDepthView, ITexture? resolvedTexture = null,
            bool supportsUnorderedAccess = false, bool ownsResources = true)
        {
            Texture = texture;
            ShaderView = shaderView;
            RenderTargetView = renderTargetView;
            DepthStencilView = depthStencilView;
            ReadOnlyDepthView = readOnlyDepthView;
            ResolvedTexture = resolvedTexture;
            Dimension = GetResourceDimension(sourceTexture);
            Width = (uint)sourceTexture.Width;
            Height = (uint)sourceTexture.Height;
            ArraySizeOrDepth = GetArraySizeOrDepth(sourceTexture);
            SampleCount = (uint)sourceTexture.MultiSample;
            Format = sourceTexture.Format;
            MipLevels = GetUploadedMipLevels(sourceTexture);
            SupportsUnorderedAccess = supportsUnorderedAccess;
            _ownsResources = ownsResources;
        }

        public ITexture Texture { get; }

        public ITextureView ShaderView { get; }

        public ITextureView? RenderTargetView { get; }

        public ITextureView? DepthStencilView { get; }

        public ITextureView? ReadOnlyDepthView { get; }

        public ITexture? ResolvedTexture { get; }

        public ResourceDimension Dimension { get; }

        public uint Width { get; }

        public uint Height { get; }

        public uint ArraySizeOrDepth { get; }

        public uint SampleCount { get; }

        public global::RedBolt.Bindings.Diligent.TextureFormat Format { get; }

        public uint MipLevels { get; }

        public bool HasResolvedTexture => ResolvedTexture is not null && !ResolvedTexture.IsNull;

        public bool SupportsUnorderedAccess { get; }

        public bool ResolveDirty { get; private set; }

        public bool TryGetSliceView(TextureViewType viewType, int slice, out ITextureView view)
        {
            return _sliceViews.TryGetValue((viewType, slice), out view!);
        }

        public void CacheSliceView(TextureViewType viewType, int slice, ITextureView view)
        {
            _sliceViews[(viewType, slice)] = view;
        }

        public bool TryGetUav(RawTextureUAVKey key, out ITextureView view)
        {
            return _uavs.TryGetValue(key, out view!);
        }

        public void CacheUav(RawTextureUAVKey key, ITextureView view)
        {
            _uavs[key] = view;
        }

        public void MarkDirty()
        {
            if (HasResolvedTexture)
                ResolveDirty = true;
        }

        public void ClearResolveDirty()
        {
            ResolveDirty = false;
        }

        public void Dispose()
        {
            foreach (var view in _uavs.Values)
                ReleaseIfNotNull(view);
            _uavs.Clear();
            foreach (var view in _sliceViews.Values)
                ReleaseIfNotNull(view);
            _sliceViews.Clear();

            if (_ownsResources)
            {
                ReleaseIfNotNull(ReadOnlyDepthView);
                ReleaseIfNotNull(DepthStencilView);
                ReleaseIfNotNull(RenderTargetView);
                ReleaseIfNotNull(ShaderView);
                ReleaseIfNotNull(ResolvedTexture);
                ReleaseIfNotNull(Texture);
            }
        }

        private readonly bool _ownsResources;
    }

    private sealed class PinnedBuffer : IDisposable
    {
        private readonly GCHandle _handle;

        public PinnedBuffer(byte[] data)
        {
            _handle = GCHandle.Alloc(data, GCHandleType.Pinned);
        }

        public nint Pointer => _handle.AddrOfPinnedObject();

        public void Dispose()
        {
            if (_handle.IsAllocated)
                _handle.Free();
        }
    }
}
