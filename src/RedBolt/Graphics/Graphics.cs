using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using RedBolt.Bindings.Diligent;
using RedBolt.Bindings.Sdl;

using Microsoft.Extensions.Logging;

namespace RedBolt;

public sealed class Graphics : Object
{
    private const uint GlFront = 0x0404;
    private const uint GlBack = 0x0405;
    private const uint GlRgba = 0x1908;
    private const uint GlUnsignedByte = 0x1401;
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
    private static readonly RenderTargetView[] SwapChainColorViews = [RenderTargetView.SwapChainColor()];
    private readonly ITextureView?[] _renderTargets = new ITextureView?[RenderApiLimits.MaxRenderTargets];
    private readonly RenderTargetView[] _singleRenderTargetView = new RenderTargetView[1];
    private readonly IBuffer?[] _singleVertexBuffer = new IBuffer?[1];
    private readonly Dictionary<Geometry, UploadedGeometry> _uploadedGeometries = new();
    private readonly Dictionary<VertexBuffer, IBuffer> _uploadedVertexBuffers = new();
    private readonly Dictionary<IndexBuffer, IBuffer> _uploadedIndexBuffers = new();
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
    private GlFinishDelegate? _glFinish;
    private GlReadBufferDelegate? _glReadBuffer;
    private GlReadPixelsDelegate? _glReadPixels;
    private IDeviceContext? _immediateContext;
    private IEngineFactory? _factory;
    private IRenderDevice? _device;
    private ISwapChain? _swapChain;
    private global::RedBolt.Bindings.Diligent.Viewport? _diligentViewport;
    private RenderOutputTargetInfo? _swapChainOutputTarget;
    private ITextureView? _renderTargetView;
    private ITextureView? _depthStencilView;
    private ITextureView? _currentDepthStencilTarget;
    private int _currentRenderTargetCount;
    private int _configuredMultiSample = 1;
    private Geometry? _fullscreenQuadGeometry;
    private global::RedBolt.Bindings.Diligent.TextureFormat _currentColorTargetFormat;
    private global::RedBolt.Bindings.Diligent.TextureFormat _currentDepthTargetFormat;
    private global::RedBolt.Bindings.Diligent.TextureFormat _swapChainColorFormat;
    private global::RedBolt.Bindings.Diligent.TextureFormat _swapChainDepthFormat;
    private readonly PipelineStateOutputDesc _currentOutputDesc = new();
    private IntRect _currentViewport;
    private Vector2i _currentRenderTargetSize;
    private bool _isSwapChainRenderTarget;
    private IntPtr _glContext;
    private bool _shaderReflectionDiagnosticsDumped;
    private SdlWindow? _window;
    private string _windowTitle = string.Empty;
    private Vector2i _windowPosition;

    public bool IsInitialized { get; private set; }

    public int Width { get; private set; }

    public int Height { get; private set; }

    public WindowSettings GetWindowSettings()
    {
        var renderDevice = GetSubsystem<RenderDevice>();
        return renderDevice?.GetWindowSettings() ?? new WindowSettings();
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

    public Vector3f GetDisplayDPI(int monitor = 0)
    {
        SdlNativeBindings.SdlGetDisplayDPI(monitor, out var ddpi, out var hdpi, out var vdpi);
        return new Vector3f(hdpi, vdpi, ddpi);
    }

    public RenderDeviceCaps GetRenderDeviceCaps()
    {
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
        return format is TextureFormat.Depth32 or TextureFormat.Depth24Stencil8
            or TextureFormat.R8 or TextureFormat.RG16F or TextureFormat.RGB8 or TextureFormat.RGBA8 or TextureFormat.RGBA16F;
    }

    public ShaderVariation? GetShader(ShaderType type, string name, string defines = "")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var cache = GetSubsystem<ResourceCache>();
        var shader = cache?.GetResourceOrNull<Shader>(Shader.GetResourceName(name));
        return shader is null ? null : new ShaderVariation(shader, type, defines);
    }

    public IRenderDevice? GetNativeRenderDevice() => _device;

    public IEngineFactory? GetNativeFactory() => _factory;

    public ISwapChain? GetNativeSwapChain() => _swapChain;

    public SdlWindow? GetNativeWindow() => _window;

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
    public TextureFormat GetDummyColorFormat() => TextureFormat.Unknown;

    /// <summary>
    /// Return shadow map depth texture format.
    /// </summary>
    public TextureFormat GetShadowMapFormat() => TextureFormat.Unknown;

    /// <summary>
    /// Return 24-bit shadow map depth texture format.
    /// </summary>
    public TextureFormat GetHiresShadowMapFormat() => TextureFormat.Unknown;

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

    internal ITextureView? CreateTextureUnorderedAccessView(RawTexture rawTexture, RawTextureUAVKey key)
    {
        ArgumentNullException.ThrowIfNull(rawTexture);
        if (!IsInitialized)
            return null;

        var texture = rawTexture.GetTexture();
        if (texture is null)
            return null;

        var uploaded = GetOrCreateUploadedTexture(texture, bindUnorderedAccess: true);
        if (uploaded.TryGetUav(key, out var cachedView))
            return cachedView;

        using var textureViewDescOwner = CreateTextureViewDesc();
        var textureViewDesc = textureViewDescOwner.Desc;
        textureViewDesc.ViewType = TextureViewType.TextureViewUnorderedAccess;
        textureViewDesc.TextureDim = GetViewResourceDimension(texture);
        textureViewDesc.Format = MapTextureFormat(texture.Format);
        textureViewDesc.MostDetailedMip = key.FirstLevel;
        textureViewDesc.NumMipLevels = key.NumLevels;
        textureViewDesc.FirstArraySlice = key.FirstSlice;
        textureViewDesc.NumArraySlices = key.NumSlices;
        textureViewDesc.AccessFlags = key.CanRead && key.CanWrite
            ? UavAccessFlag.UavAccessFlagReadWrite
            : key.CanRead
                ? UavAccessFlag.UavAccessFlagRead
                : UavAccessFlag.UavAccessFlagWrite;
        uploaded.Texture.CreateView(textureViewDesc, out var createdView);
        if (createdView.IsNull)
            return null;

        uploaded.CacheUav(key, createdView);
        return createdView;
    }

    internal RenderOutputTargetInfo GetSwapChainOutputTarget()
    {
        var colorFormat = GetSwapChainColorTextureFormat(out var colorIsSrgb);
        var depthFormat = GetSwapChainDepthTextureFormat();
        var fullRect = new IntRect(0, 0, Width, Height);
        var multiSample = GetSwapChainMultiSample();
        var linearColorSpace = TextureFormatUtility.IsFloatColorFormat(colorFormat) || colorIsSrgb;
        var hdr = TextureFormatUtility.IsFloatColorFormat(colorFormat);
        var hasStencil = depthFormat == TextureFormat.Depth24Stencil8;

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

        _swapChainOutputTarget = new RenderOutputTargetInfo(
            RenderTargetView.SwapChainColor(),
            fullRect,
            colorFormat,
            RenderTargetView.SwapChainDepthStencil(),
            depthFormat,
            multiSample,
            bilinearFiltered: false,
            linearColorSpace: linearColorSpace,
            hdr: hdr,
            hasReadableDepth: false,
            hasStencil: hasStencil);
        return _swapChainOutputTarget;
    }

    internal TextureFormat GetSwapChainColorTextureFormat(out bool isSrgb)
        => UnmapTextureFormat(GetSwapChainColorFormat(), out isSrgb);

    internal TextureFormat GetSwapChainDepthTextureFormat()
        => UnmapTextureFormat(GetSwapChainDepthFormat(), out _);

    internal int GetSwapChainMultiSample()
        => Math.Max(_configuredMultiSample, 1);

    public void Initialize(EngineParameters parameters, Input input)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(input);

        if (IsInitialized)
            throw new InvalidOperationException("Graphics system is already initialized.");

        GetSubsystem<RenderDevice>()?.ResetWindowSettings();

        if (parameters.GraphicsBackend is not GraphicsBackend.Auto and not GraphicsBackend.OpenGL)
            throw new NotSupportedException($"The first RedBolt graphics backend supports OpenGL only, not {parameters.GraphicsBackend}.");

        _window = input.Window;
        _glContext = SdlNativeBindings.SdlGlCreateContext(_window);
        if (_glContext == IntPtr.Zero)
            throw new InvalidOperationException("Generated SDL_GL_CreateContext binding returned a null context.");

        try
        {
            CheckSdlGl(SdlNativeBindings.SdlGlMakeCurrent(_window, _glContext), "SDL_GL_MakeCurrent");
            _ = SdlNativeBindings.SdlGlSetSwapInterval(parameters.VSync ? 1 : 0);
            SdlNativeBindings.SdlGetWindowSize(_window, out var actualWidth, out var actualHeight);
            Width = actualWidth;
            Height = actualHeight;
            _configuredMultiSample = (int)Math.Clamp(MathDefs.ClosestPowerOfTwo((uint)Math.Max(parameters.MultiSample, 1)), 1u, 16u);

            var nativeWindowInfo = input.GetNativeWindowInfo();
            using var engineCreateInfo = new EngineGLCreateInfo
            {
                FeaturesSeparablePrograms = DeviceFeatureState.DeviceFeatureStateOptional,
                FeaturesShaderResourceQueries = DeviceFeatureState.DeviceFeatureStateOptional,
                WindowPDisplay = nativeWindowInfo.Display,
                WindowWindowId = nativeWindowInfo.WindowId
            };
            using var swapChainDesc = new SwapChainDesc();

            var factory = DiligentNativeBindings.GetEngineFactoryOpenGL();
            if (factory.IsNull)
                throw new InvalidOperationException("Generated GetEngineFactoryOpenGL binding returned a null factory.");

            _factory = factory;
            factory.CreateDeviceAndSwapChainGL(engineCreateInfo, out _device, out _immediateContext, swapChainDesc, out _swapChain);
            if (_device.IsNull || _immediateContext.IsNull || _swapChain.IsNull)
                throw new InvalidOperationException("Generated CreateDeviceAndSwapChainGL binding returned a null device, context, or swap chain.");

            _renderTargetView = _swapChain.GetCurrentBackBufferRTV();
            _depthStencilView = _swapChain.GetDepthBufferDSV();
            if (_renderTargetView.IsNull || _depthStencilView.IsNull)
                throw new InvalidOperationException("Swap chain render target or depth view was null.");
            _swapChainColorFormat = GetTextureViewFormat(_renderTargetView);
            _swapChainDepthFormat = GetTextureViewFormat(_depthStencilView);
            _renderTargets[0] = _renderTargetView;

            _clearColorHandle = GCHandle.Alloc(_clearColor, GCHandleType.Pinned);
            IsInitialized = true;
        }
        catch
        {
            Shutdown();
            throw;
        }
    }

    internal void RenderViewport(Viewport? viewport, Action? beforePresent = null, string? screenshotPath = null)
    {
        RenderViewport(viewport, MaterialQuality.High, beforePresent, screenshotPath);
    }

    /// <summary>
    /// Render viewport through the legacy direct path that binds the backbuffer, draws the scene and presents it.
    /// </summary>
    internal void RenderViewport(Viewport? viewport, MaterialQuality materialQuality, Action? beforePresent = null, string? screenshotPath = null)
    {
        if (!IsInitialized || viewport is null || _immediateContext is null || _swapChain is null || _renderTargetView is null || _depthStencilView is null)
            return;

        SendEvent<GraphicsEvents.BeginRenderingEvent>();

        var viewportRect = GetEffectiveViewportRect(viewport);
        SetSwapChainRenderTargets(viewportRect);
        ClearCurrentRenderTargets();
        RenderScene(viewport, materialQuality, viewportRect);
        SendEvent<GraphicsEvents.EndRenderingEvent>();
        PresentFrame(screenshotPath, beforePresent);
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

        ApplyDiligentViewport(_immediateContext, viewportRect);
        _immediateContext.SetRenderTargets((uint)_currentRenderTargetCount,
            _renderTargets, _currentDepthStencilTarget!,
            ResourceStateTransitionMode.ResourceStateTransitionModeTransition);
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
        SetRenderTargets(viewportRect, SwapChainColorViews, RenderTargetView.SwapChainDepthStencil());
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
        if (viewport.Camera.GetAutoAspectRatio())
        {
            var aspectRatio = Math.Max((float)effectiveViewportRect.Width / Math.Max(effectiveViewportRect.Height, 1), MathDefs.Epsilon);
            viewport.Camera.SetAspectRatioInternal(aspectRatio);
        }

        viewport.Scene.GetComponents(_lightsScratch);
        Light? directionalLight = null;
        for (var i = 0; i < _lightsScratch.Count; ++i)
        {
            var light = _lightsScratch[i];
            if (!light.IsAttached || !light.IsEnabledEffective || light.GetLightType() != LightType.Directional)
                continue;

            directionalLight = light;
            break;
        }

        uint submittedDrawCalls = 0;
        uint submittedPrimitives = 0;
        var staticModels = viewport.Scene.StaticModels;
        for (var staticModelIndex = 0; staticModelIndex < staticModels.Count; ++staticModelIndex)
        {
            var staticModel = staticModels[staticModelIndex];
            var (drawCalls, primitives) = RenderStaticModel(viewport, staticModel, materialQuality, directionalLight);
            submittedDrawCalls += drawCalls;
            submittedPrimitives += primitives;
        }

        if (RenderDiagnosticsEnabled)
            Log.Logger.LogDebug(
                "Render diagnostics: drawCalls={DrawCalls}, primitives={Primitives}, staticModels={StaticModels}.",
                submittedDrawCalls, submittedPrimitives, viewport.Scene.StaticModels.Count);
    }

    internal void RenderOutlineGroups(Viewport viewport, MaterialQuality materialQuality, IReadOnlyList<OutlineGroup> outlineGroups, IntRect? viewportRect = null)
    {
        ArgumentNullException.ThrowIfNull(viewport);
        ArgumentNullException.ThrowIfNull(outlineGroups);

        if (!IsInitialized)
            return;

        var effectiveViewportRect = viewportRect ?? GetEffectiveViewportRect(viewport);
        if (viewport.Camera.GetAutoAspectRatio())
        {
            var aspectRatio = Math.Max((float)effectiveViewportRect.Width / Math.Max(effectiveViewportRect.Height, 1), MathDefs.Epsilon);
            viewport.Camera.SetAspectRatioInternal(aspectRatio);
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
        var staticModels = viewport.Scene.StaticModels;
        for (var staticModelIndex = 0; staticModelIndex < staticModels.Count; ++staticModelIndex)
        {
            var staticModel = staticModels[staticModelIndex];
            RenderNormalStaticModel(viewport, staticModel, materialQuality);
        }
    }

    /// <summary>
    /// Present the current frame after optional screenshot capture.
    /// </summary>
    internal void PresentFrame(string? screenshotPath = null, Action? beforePresent = null)
    {
        if (!IsInitialized || _swapChain is null)
            return;

        beforePresent?.Invoke();

        if (!string.IsNullOrWhiteSpace(screenshotPath))
            SavePresentedFrameScreenshot(screenshotPath);

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
        _currentOutputDesc.ClearRenderTargetFormats();
        _currentOutputDesc.DepthStencilFormat = TextureFormat.Unknown;
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
            sourceTexture,
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
        bool bilinearColorSampler, Texture? albedoTexture, Texture? secondaryTexture, Texture? depthTexture,
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
        bool bilinearColorSampler, Texture? albedoTexture, Texture? secondaryTexture, Texture? depthTexture,
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

        _ = SdlNativeBindings.SdlGlSetSwapInterval(enable ? 1 : 0);
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

        SdlNativeBindings.SdlGetWindowSize(_window, out var windowWidth, out var windowHeight);
        SdlNativeBindings.SdlGlGetDrawableSize(_window, out var drawableWidth, out var drawableHeight);
        Width = windowWidth;
        Height = windowHeight;
        _swapChain.Resize((uint)Math.Max(drawableWidth, 0), (uint)Math.Max(drawableHeight, 0));
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

        if (oldSettings.VSync != newSettings.VSync)
            _ = SdlNativeBindings.SdlGlSetSwapInterval(newSettings.VSync ? 1 : 0);
    }

    public void SaveScreenshot(string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        if (!IsInitialized || _device is null || _immediateContext is null || _renderTargetView is null)
            throw new InvalidOperationException("Graphics system is not initialized.");

        if (TrySaveCurrentFramebufferScreenshot(fileName))
            return;

        var sourceTexture = _renderTargetView.GetTexture();
        if (!sourceTexture.IsNull)
        {
            SaveTexture(sourceTexture, fileName);
            return;
        }

        if (!TrySaveCurrentFramebufferScreenshot(fileName))
            throw new InvalidOperationException("Backbuffer texture was not available for screenshot capture.");
    }

    private void SaveTexture(ITexture sourceTexture, string fileName)
    {
        ArgumentNullException.ThrowIfNull(sourceTexture);

        if (_device is null || _immediateContext is null)
            throw new InvalidOperationException("Graphics system is not initialized.");

        using var sourceDescription = sourceTexture.GetDesc();
        var width = checked((int)sourceDescription.Width);
        var height = checked((int)sourceDescription.Height);

        using var stagingDescOwner = CreateTextureDesc();
        var stagingDescription = stagingDescOwner.Desc;
        stagingDescription.Type = sourceDescription.Type;
        stagingDescription.Width = sourceDescription.Width;
        stagingDescription.Height = sourceDescription.Height;
        CopyTextureDescArraySizeOrDepth(stagingDescription, sourceDescription);
        stagingDescription.MipLevels = 1;
        stagingDescription.Format = sourceDescription.Format;
        stagingDescription.BindFlags = BindFlags.BindNone;
        stagingDescription.CPUAccessFlags = CpuAccessFlags.CpuAccessRead;
        stagingDescription.MiscFlags = MiscTextureFlags.MiscTextureFlagNone;
        stagingDescription.SampleCount = 1;
        stagingDescription.Usage = Usage.UsageStaging;

        ITexture? stagingTexture = null;
        try
        {
            _device.CreateTexture(stagingDescription, null!, out stagingTexture);
            if (stagingTexture is null || stagingTexture.IsNull)
                throw new InvalidOperationException("Failed to create the staging texture for screenshot capture.");

            using var copyAttributes = new CopyTextureAttribs(
                sourceTexture,
                ResourceStateTransitionMode.ResourceStateTransitionModeTransition,
                stagingTexture,
                ResourceStateTransitionMode.ResourceStateTransitionModeTransition);
            _immediateContext.CopyTexture(copyAttributes);
            _immediateContext.Flush();
            _immediateContext.WaitForIdle();

            using var mappedSubresource = new MappedTextureSubresource();
            _immediateContext.MapTextureSubresource(stagingTexture, 0, 0, MapType.MapRead, MapFlags.MapFlagNone, null!, mappedSubresource);
            try
            {
                var rgbaPixels = new byte[checked(width * height * 4)];
                CopyMappedTexture(sourceDescription.Format, width, height, mappedSubresource, rgbaPixels);
                ImageFileWriter.Save(fileName, width, height, rgbaPixels);
            }
            finally
            {
                _immediateContext.UnmapTextureSubresource(stagingTexture, 0, 0);
            }
        }
        finally
        {
            ReleaseIfNotNull(stagingTexture);
        }
    }

    private void SaveCurrentFramebufferScreenshot(string fileName)
    {
        if (!TrySaveCurrentFramebufferScreenshot(fileName))
            throw new InvalidOperationException("OpenGL framebuffer readback is not available.");
    }

    private void SavePresentedFrameScreenshot(string fileName)
    {
        if (TrySaveCurrentFramebufferScreenshot(fileName, GlBack))
            return;

        if (_renderTargetView is not null)
        {
            var sourceTexture = _renderTargetView.GetTexture();
            if (!sourceTexture.IsNull)
            {
                SaveTexture(sourceTexture, fileName);
                return;
            }
        }

        SaveCurrentFramebufferScreenshot(fileName);
    }

    private bool TrySaveCurrentFramebufferScreenshot(string fileName)
    {
        return TrySaveCurrentFramebufferScreenshot(fileName, GlBack);
    }

    private bool TrySaveCurrentFramebufferScreenshot(string fileName, uint readBuffer)
    {
        if (!TryCaptureScreenShot(readBuffer, out var size, out var rgbaPixels))
            return false;

        ImageFileWriter.Save(fileName, size.X, size.Y, rgbaPixels);
        return true;
    }

    internal bool TryCaptureScreenShot(out Vector2i size, out byte[] data)
    {
        return TryCaptureScreenShot(GlBack, out size, out data);
    }

    private bool TryCaptureScreenShot(uint readBuffer, out Vector2i size, out byte[] data)
    {
        size = Vector2i.Zero;
        data = [];
        if (_glContext == IntPtr.Zero || Width <= 0 || Height <= 0)
            return false;

        EnsureOpenGlReadbackFunctions();

        size = new Vector2i(Width, Height);
        data = new byte[checked(Width * Height * 4)];
        using var pinnedPixels = new PinnedBuffer(data);

        _glFinish!.Invoke();
        _glReadBuffer!.Invoke(readBuffer);
        _glReadPixels!.Invoke(0, 0, Width, Height, GlRgba, GlUnsignedByte, pinnedPixels.Pointer);
        FlipImageRows(data, Width, Height);
        return true;
    }

    private void EnsureOpenGlReadbackFunctions()
    {
        _glFinish ??= LoadOpenGlProcedure<GlFinishDelegate>("glFinish");
        _glReadBuffer ??= LoadOpenGlProcedure<GlReadBufferDelegate>("glReadBuffer");
        _glReadPixels ??= LoadOpenGlProcedure<GlReadPixelsDelegate>("glReadPixels");
    }

    private static T LoadOpenGlProcedure<T>(string name)
        where T : Delegate
    {
        using var pinnedName = new CString(name);
        var procedure = SdlNativeBindings.SdlGlGetProcAddress(pinnedName.Bytes);
        if (procedure == IntPtr.Zero)
            throw new InvalidOperationException($"OpenGL procedure '{name}' is not available.");

        return Marshal.GetDelegateForFunctionPointer<T>(procedure);
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

        foreach (var uploaded in _uploadedVertexBuffers.Values)
            ReleaseIfNotNull(uploaded);
        _uploadedVertexBuffers.Clear();

        foreach (var uploaded in _uploadedIndexBuffers.Values)
            ReleaseIfNotNull(uploaded);
        _uploadedIndexBuffers.Clear();

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

        _fullscreenQuadGeometry = null;
        _diligentViewport?.Dispose();
        _diligentViewport = null;

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
        if (!staticModel.IsAttached || !staticModel.IsEnabledEffective || staticModel.GetModel() is not { } model)
            return default;

        uint drawCalls = 0;
        uint primitives = 0;
        var cameraZone = FindZone(viewport.Scene, viewport.Camera.IsAttached ? viewport.Camera.Node.WorldPosition : Vector3f.Zero);
        var drawableZone = FindZone(viewport.Scene, staticModel.Node.WorldPosition);
        var drawableAmbient = drawableZone is not null ? new Vector4f(drawableZone.GetAmbientLighting(), 1.0f) : DefaultAmbient;
        for (var geometryIndex = 0u; geometryIndex < model.GetNumGeometries(); ++geometryIndex)
        {
            var geometry = model.GetGeometry(geometryIndex, 0);
            var vertexBuffer = geometry?.GetVertexBuffer(0);
            var indexBuffer = geometry?.GetIndexBuffer();
            var material = staticModel.GetMaterial(geometryIndex);
            if (geometry is null || vertexBuffer is null || indexBuffer is null || material is null)
                continue;

            var resourceCache = material.OwnerCache;
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
        if (!staticModel.IsAttached || !staticModel.IsEnabledEffective || staticModel.GetModel() is not { } model)
            return default;

        uint drawCalls = 0;
        uint primitives = 0;
        var cameraZone = FindZone(viewport.Scene, viewport.Camera.IsAttached ? viewport.Camera.Node.WorldPosition : Vector3f.Zero);
        var drawableZone = FindZone(viewport.Scene, staticModel.Node.WorldPosition);
        var drawableAmbient = drawableZone is not null ? new Vector4f(drawableZone.GetAmbientLighting(), 1.0f) : DefaultAmbient;
        for (var geometryIndex = 0u; geometryIndex < model.GetNumGeometries(); ++geometryIndex)
        {
            var geometry = model.GetGeometry(geometryIndex, 0);
            var vertexBuffer = geometry?.GetVertexBuffer(0);
            var material = staticModel.GetMaterial(geometryIndex);
            if (geometry is null || vertexBuffer is null || material is null)
                continue;

            var resourceCache = material.OwnerCache;
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
        if (!staticModel.IsAttached || !staticModel.IsEnabledEffective || staticModel.GetModel() is not { } model)
            return default;

        uint drawCalls = 0;
        uint primitives = 0;
        var cameraZone = FindZone(viewport.Scene, viewport.Camera.IsAttached ? viewport.Camera.Node.WorldPosition : Vector3f.Zero);
        var drawableZone = FindZone(viewport.Scene, staticModel.Node.WorldPosition);
        var drawableAmbient = drawableZone is not null ? new Vector4f(drawableZone.GetAmbientLighting(), 1.0f) : DefaultAmbient;

        for (var geometryIndex = 0u; geometryIndex < model.GetNumGeometries(); ++geometryIndex)
        {
            var geometry = model.GetGeometry(geometryIndex, 0);
            var vertexBuffer = geometry?.GetVertexBuffer(0);
            var material = staticModel.GetMaterial(geometryIndex);
            if (geometry is null || vertexBuffer is null || material is null)
                continue;

            if ((vertexBuffer.GetElementMask() & (uint)VertexMask.Normal) == 0)
                continue;

            var resourceCache = material.OwnerCache;
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
        var pipeline = GetOrCreatePipeline(resourceCache, viewport.Camera, material, pass, vertexBuffer, light);
        var uploadedGeometry = GetOrCreateUploadedGeometry(geometry);
        if (pipeline is null || uploadedGeometry is null || _immediateContext is null)
            return default;

        UpdateCameraBuffer(pipeline.CameraBuffer, viewport.Camera, cameraZone);
        UpdateObjectBuffer(pipeline.ObjectBuffer, staticModel.Node.GetWorldTransform().Matrix4, drawableAmbient);
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

        nuint vertexOffset = 0;
        _singleVertexBuffer[0] = uploadedGeometry.VertexBuffer;
        _immediateContext.SetVertexBuffers(0, 1, _singleVertexBuffer, in vertexOffset,
            ResourceStateTransitionMode.ResourceStateTransitionModeTransition,
            SetVertexBuffersFlags.SetVertexBuffersFlagReset);
        _immediateContext.SetIndexBuffer(uploadedGeometry.IndexBuffer, 0,
            ResourceStateTransitionMode.ResourceStateTransitionModeTransition);
        _immediateContext.DrawIndexed(uploadedGeometry.DrawIndexedAttribs);
        return (1u, geometry.GetPrimitiveCount());
    }

    private PipelineResources? GetOrCreatePipeline(ResourceCache resourceCache, Camera camera, Material material, Pass pass,
        VertexBuffer vertexBuffer, Light? light)
    {
        var vertexDefines = ComposeShaderDefines(pass, vertexBuffer, material, light, ShaderStage.Vertex);
        var pixelDefines = ComposeShaderDefines(pass, vertexBuffer, material, light, ShaderStage.Pixel);
        var effectiveCullMode = pass.CullMode != CullMode.Max ? pass.CullMode : material.CullMode;
        var effectiveFillMode = GetEffectiveFillMode(camera, material);
        var reverseCulling = camera.GetReverseCulling();
        var alphaToCoverage = pass.AlphaToCoverage || material.GetAlphaToCoverage();
        var colorFormat = _currentColorTargetFormat != 0 ? _currentColorTargetFormat : GetSwapChainColorFormat();
        var depthFormat = _currentDepthTargetFormat != 0 ? _currentDepthTargetFormat : GetSwapChainDepthFormat();
        var key = new PipelineKey(pass.VertexShader, pass.PixelShader, vertexDefines, pixelDefines,
            BuildLayoutSignature(vertexBuffer), effectiveCullMode, effectiveFillMode, pass.DepthTestMode,
            pass.DepthWrite, alphaToCoverage, reverseCulling, colorFormat, depthFormat);
        if (_pipelines.TryGetValue(key, out var cached))
            return cached;

        if (_device is null)
            return null;

        var vertexShaderResource = resourceCache.GetResourceOrNull<Shader>(pass.VertexShader);
        var pixelShaderResource = resourceCache.GetResourceOrNull<Shader>(pass.PixelShader);
        if (vertexShaderResource is null || pixelShaderResource is null)
            return null;

        var preparedVertexSource = vertexShaderResource.PrepareSource(ShaderStage.Vertex, vertexDefines, GraphicsBackend.OpenGL, ShaderExtraCode);
        var preparedPixelSource = pixelShaderResource.PrepareSource(ShaderStage.Pixel, pixelDefines, GraphicsBackend.OpenGL, ShaderExtraCode);
        var vertexShader = CreateShader(_device, global::RedBolt.Bindings.Diligent.ShaderType.ShaderTypeVertex,
            pass.VertexShader, preparedVertexSource);
        var pixelShader = CreateShader(_device, global::RedBolt.Bindings.Diligent.ShaderType.ShaderTypePixel,
            pass.PixelShader, preparedPixelSource);
        var customLayout = CreateCustomUniformLayout(new ShaderProgramReflection([vertexShader, pixelShader]));

        TemporaryLayoutArray? layoutArray = null;
        try
        {
            layoutArray = new TemporaryLayoutArray(BuildLayoutElements(vertexBuffer));
            using var pipelineName = new CString($"{pass.VertexShader}:{pass.PixelShader}:{key.LayoutSignature}");
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

            cached = new PipelineResources(pipelineState, vertexShader, pixelShader, cameraBuffer, objectBuffer, materialBuffer,
                customBuffer, customLayout, lightBuffer, zoneBuffer, shaderResources, BuildShaderVariableLookup(shaderResources), layoutArray);
            layoutArray = null;
            _pipelines[key] = cached;
            return cached;
        }
        finally
        {
            layoutArray?.Dispose();
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

        var vertexSource = shaderResource.PrepareSource(ShaderStage.Vertex, effectiveShaderDefines, GraphicsBackend.OpenGL);
        var pixelSource = shaderResource.PrepareSource(ShaderStage.Pixel, effectiveShaderDefines, GraphicsBackend.OpenGL);
        var vertexShader = CreateShader(_device, global::RedBolt.Bindings.Diligent.ShaderType.ShaderTypeVertex, shaderName, vertexSource);
        var pixelShader = CreateShader(_device, global::RedBolt.Bindings.Diligent.ShaderType.ShaderTypePixel, shaderName, pixelSource);
        var customLayout = CreateCustomUniformLayout(new ShaderProgramReflection([vertexShader, pixelShader]));

        TemporaryLayoutArray? layoutArray = null;
        try
        {
            var quadGeometry = GetOrCreateFullscreenQuadGeometry();
            var vertexBuffer = quadGeometry.GetVertexBuffer(0) ?? throw new InvalidOperationException("Fullscreen quad geometry has no vertex buffer.");
            layoutArray = new TemporaryLayoutArray(BuildLayoutElements(vertexBuffer));
            using var pipelineName = new CString($"{shaderName}:{shaderDefines}:fullscreen");
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
                BuildShaderVariableLookup(shaderResources),
                frameBuffer,
                cameraBuffer,
                objectBuffer,
                customBuffer,
                customLayout,
                layoutArray);
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

        var vertexSource = shaderResource.PrepareSource(ShaderStage.Vertex, shaderDefines, GraphicsBackend.OpenGL);
        var pixelSource = shaderResource.PrepareSource(ShaderStage.Pixel, shaderDefines, GraphicsBackend.OpenGL);
        var vertexShader = CreateShader(_device, global::RedBolt.Bindings.Diligent.ShaderType.ShaderTypeVertex, shaderName, vertexSource);
        var pixelShader = CreateShader(_device, global::RedBolt.Bindings.Diligent.ShaderType.ShaderTypePixel, shaderName, pixelSource);
        var customLayout = CreateCustomUniformLayout(new ShaderProgramReflection([vertexShader, pixelShader]));
        var settings = GetClearFramebufferPipelineSettings(flags);

        TemporaryLayoutArray? layoutArray = null;
        try
        {
            var quadGeometry = GetOrCreateFullscreenQuadGeometry();
            var vertexBuffer = quadGeometry.GetVertexBuffer(0) ?? throw new InvalidOperationException("Fullscreen quad geometry has no vertex buffer.");
            layoutArray = new TemporaryLayoutArray(BuildLayoutElements(vertexBuffer));
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

            cached = new FullscreenPipelineResources(
                pipelineState,
                vertexShader,
                pixelShader,
                shaderResources,
                BuildShaderVariableLookup(shaderResources),
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
        if (_fullscreenQuadGeometry is not null)
            return _fullscreenQuadGeometry;

        var vertexBuffer = new VertexBuffer();
        vertexBuffer.SetSize(4, [new VertexElement(VertexElementType.Vector4, VertexElementSemantic.Position)]);
        var vertexData = new float[]
        {
            -1.0f, -1.0f, 0.0f, 1.0f,
            -1.0f, 1.0f, 0.0f, 1.0f,
            1.0f, 1.0f, 0.0f, 1.0f,
            1.0f, -1.0f, 0.0f, 1.0f
        };
        var vertexBytes = new byte[vertexData.Length * sizeof(float)];
        Buffer.BlockCopy(vertexData, 0, vertexBytes, 0, vertexBytes.Length);
        vertexBuffer.SetData(vertexBytes);

        var indexBuffer = new IndexBuffer();
        indexBuffer.SetSize(6, largeIndices: false);
        indexBuffer.SetShadowed(true);
        indexBuffer.SetUnpackedData([0u, 1u, 2u, 0u, 2u, 3u]);

        var geometry = new Geometry();
        geometry.SetVertexBuffer(0, vertexBuffer);
        geometry.SetIndexBuffer(indexBuffer);
        geometry.SetDrawRange(PrimitiveType.TriangleList, 0, 6, 0, 4);
        _fullscreenQuadGeometry = geometry;
        return geometry;
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
        WriteMatrix4Raw(data, 0, Matrix4.Identity);
        WriteVector4(data, 64, DefaultAmbient);
        UpdateBuffer(buffer, data);
    }

    private void DrawFullscreenQuad(FullscreenPipelineResources pipeline, Texture? albedoTexture, Texture? secondaryTexture, Texture? depthTexture,
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

    private void DrawFullscreenQuad<TParameters>(FullscreenPipelineResources pipeline, Texture? albedoTexture, Texture? secondaryTexture,
        Texture? depthTexture, in TParameters parameters, Vector2f invInputSize, Vector4f clipToUvOffsetAndScale, bool flipVertical)
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
        Texture? albedoTexture, Texture? secondaryTexture, Texture? depthTexture)
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

        if (albedoTexture is Texture2D texture2D)
            BindTextureToAvailableStages(pipeline, "sAlbedo", GetShaderResourceView(texture2D));
        if (secondaryTexture is Texture2D secondaryTexture2D)
            BindTextureToAvailableStages(pipeline, "sNormal", GetShaderResourceView(secondaryTexture2D));
        if (depthTexture is Texture2D depthTexture2D)
            BindTextureToAvailableStages(pipeline, "sDepthBuffer", GetShaderResourceView(depthTexture2D));

        _immediateContext.SetPipelineState(pipeline.PipelineState);
        _immediateContext.CommitShaderResources(pipeline.ShaderResources, ResourceStateTransitionMode.ResourceStateTransitionModeTransition);

        nuint vertexOffset = 0;
        _singleVertexBuffer[0] = uploadedGeometry.VertexBuffer;
        _immediateContext.SetVertexBuffers(0, 1, _singleVertexBuffer, in vertexOffset,
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
        if (customBuffer is null || customBuffer.Size == 0u)
            return new CustomUniformLayout(0u, []);

        var customUniforms = new List<UniformReflection>();
        foreach (var uniform in reflection.GetUniforms().Values)
        {
            if (uniform.Group == ShaderParameterGroup.Custom)
                customUniforms.Add(uniform);
        }

        customUniforms.Sort(static (left, right) =>
        {
            var offsetComparison = left.Offset.CompareTo(right.Offset);
            return offsetComparison != 0
                ? offsetComparison
                : string.CompareOrdinal(left.Name, right.Name);
        });

        var fields = new CustomUniformField[customUniforms.Count];
        for (var i = 0; i < fields.Length; ++i)
        {
            var uniform = customUniforms[i];
            fields[i] = new CustomUniformField(
                uniform.Name,
                GetCustomParameterName(uniform.Name),
                MapCustomUniformKind(uniform),
                checked((int)uniform.Offset));
        }

        return new CustomUniformLayout(customBuffer.Size, fields);
    }

    private static string GetCustomParameterName(string uniformName)
    {
        return uniformName.Length > 1 && uniformName[0] == 'c' && char.IsUpper(uniformName[1])
            ? uniformName[1..]
            : uniformName;
    }

    private static CustomUniformKind MapCustomUniformKind(UniformReflection uniform)
    {
        if (uniform.ArraySize > 1u)
            throw new NotSupportedException($"Custom uniform arrays are not implemented yet: {uniform.Name}[{uniform.ArraySize}].");

        return uniform.VariableClass switch
        {
            ShaderCodeVariableClass.ShaderCodeVariableClassScalar => uniform.BasicType switch
            {
                ShaderCodeBasicType.ShaderCodeBasicTypeBool => CustomUniformKind.Bool,
                ShaderCodeBasicType.ShaderCodeBasicTypeUint or ShaderCodeBasicType.ShaderCodeBasicTypeUint16 or ShaderCodeBasicType.ShaderCodeBasicTypeUint8 => CustomUniformKind.UInt,
                ShaderCodeBasicType.ShaderCodeBasicTypeInt or ShaderCodeBasicType.ShaderCodeBasicTypeInt16 or ShaderCodeBasicType.ShaderCodeBasicTypeInt8 => CustomUniformKind.Int,
                ShaderCodeBasicType.ShaderCodeBasicTypeFloat or ShaderCodeBasicType.ShaderCodeBasicTypeFloat16 => CustomUniformKind.Float,
                _ => throw new NotSupportedException($"Unsupported custom scalar uniform type {uniform.BasicType} for {uniform.Name}.")
            },
            ShaderCodeVariableClass.ShaderCodeVariableClassVector => (Math.Max(uniform.NumRows, uniform.NumColumns), uniform.BasicType) switch
            {
                (2, ShaderCodeBasicType.ShaderCodeBasicTypeFloat or ShaderCodeBasicType.ShaderCodeBasicTypeFloat16) => CustomUniformKind.Vector2,
                (3, ShaderCodeBasicType.ShaderCodeBasicTypeFloat or ShaderCodeBasicType.ShaderCodeBasicTypeFloat16) => CustomUniformKind.Vector3,
                (4, ShaderCodeBasicType.ShaderCodeBasicTypeFloat or ShaderCodeBasicType.ShaderCodeBasicTypeFloat16) => CustomUniformKind.Vector4,
                _ => throw new NotSupportedException($"Unsupported custom vector uniform type {uniform.BasicType}x{Math.Max(uniform.NumRows, uniform.NumColumns)} for {uniform.Name}.")
            },
            ShaderCodeVariableClass.ShaderCodeVariableClassMatrixColumns
                when (uniform.BasicType is ShaderCodeBasicType.ShaderCodeBasicTypeFloat or ShaderCodeBasicType.ShaderCodeBasicTypeFloat16)
                     && uniform.NumRows == 4
                     && uniform.NumColumns == 4
                => CustomUniformKind.Matrix4,
            _ => throw new NotSupportedException($"Unsupported custom uniform layout for {uniform.Name}: class={uniform.VariableClass}, type={uniform.BasicType}, rows={uniform.NumRows}, columns={uniform.NumColumns}.")
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

        var gpuVertexBuffer = GetOrCreateUploadedVertexBuffer(vertexBuffer);
        var gpuIndexBuffer = GetOrCreateUploadedIndexBuffer(indexBuffer);
        var drawIndexedAttribs = new DrawIndexedAttribs(geometry.GetIndexCount(),
            indexBuffer.LargeIndices ? global::RedBolt.Bindings.Diligent.ValueType.VtUint32 : global::RedBolt.Bindings.Diligent.ValueType.VtUint16,
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

    private IBuffer GetOrCreateUploadedVertexBuffer(VertexBuffer vertexBuffer)
    {
        if (_uploadedVertexBuffers.TryGetValue(vertexBuffer, out var uploaded))
            return uploaded;

        if (_device is null || _immediateContext is null)
            throw new InvalidOperationException("Graphics device is not initialized.");

        var vertexSource = vertexBuffer.GetData();
        var vertexData = new byte[vertexSource.Count];
        for (var i = 0; i < vertexData.Length; ++i)
            vertexData[i] = vertexSource[i];
        uploaded = CreateBuffer(_device, _immediateContext, "RedBoltModelVB", vertexData,
            (ulong)vertexData.Length, BindFlags.BindVertexBuffer, vertexBuffer.GetVertexSize());
        _uploadedVertexBuffers[vertexBuffer] = uploaded;
        return uploaded;
    }

    private IBuffer GetOrCreateUploadedIndexBuffer(IndexBuffer indexBuffer)
    {
        if (_uploadedIndexBuffers.TryGetValue(indexBuffer, out var uploaded))
            return uploaded;

        if (_device is null || _immediateContext is null)
            throw new InvalidOperationException("Graphics device is not initialized.");

        var indexSource = indexBuffer.GetUnpackedData();
        var indices = new uint[indexSource.Count];
        for (var i = 0; i < indices.Length; ++i)
            indices[i] = indexSource[i];
        uploaded = CreateIndexBuffer(_device, _immediateContext, indexBuffer, indices);
        _uploadedIndexBuffers[indexBuffer] = uploaded;
        return uploaded;
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
        var texture = new Texture2D(name);
        texture.SetSize(1, 1, TextureFormat.RGBA8);
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
        textureDesc.Format = MapTextureFormat(texture.Format);
        if (textureDesc.Format == 0)
            throw new NotSupportedException($"Texture format '{texture.Format}' is not supported by managed GPU uploads.");

        var data = texture.GetDataNoCopy(0);
        if (data is { Length: > 0 })
        {
            if (texture.MultiSample != 1)
                throw new NotSupportedException("CPU-backed multisampled Texture2D uploads are not implemented in the current managed renderer.");
            if (bindUnorderedAccess)
                throw new NotSupportedException("CPU-backed Texture2D uploads with unordered access are not implemented in the current managed renderer.");

            // CPU-backed textures still upload only their base level in the managed renderer.
            // Keep the GPU texture single-mip until full subresource upload is implemented.
            textureDesc.MipLevels = 1;
            textureDesc.BindFlags = BindFlags.BindShaderResource;
            textureDesc.Usage = Usage.UsageImmutable;

            using var dataHandle = new PinnedBuffer(data);
            using var subresource = new TextureSubResData(dataHandle.Pointer, (nuint)(texture.Width * texture.GetComponents()), 0);
            using var textureData = new TextureData(subresource, 1, _immediateContext);
            _device.CreateTexture(textureDesc, textureData, out var gpuTextureWithData);
            if (gpuTextureWithData.IsNull)
                throw new InvalidOperationException($"Failed to create GPU texture for {texture.Name}.");

            var shaderResourceView = RetainDefaultTextureView(
                gpuTextureWithData.GetDefaultView(TextureViewType.TextureViewShaderResource));
            uploaded = new UploadedTexture(texture, gpuTextureWithData, shaderResourceView, null, null, null,
                supportsUnorderedAccess: false);
            _uploadedTextures[texture] = uploaded;
            return uploaded;
        }

        textureDesc.MipLevels = texture.MultiSample > 1 ? 1u : Math.Max(texture.Levels, 1u);
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
        textureDesc.Format = MapTextureFormat(texture.Format);
        if (textureDesc.Format == 0)
            throw new NotSupportedException($"Texture format '{texture.Format}' is not supported by managed GPU uploads.");

        for (var face = CubeMapFace.PositiveX; face <= CubeMapFace.NegativeZ; ++face)
        {
            if (texture.GetData(face, 0u) is { Length: > 0 })
                throw new NotSupportedException("CPU-backed TextureCube uploads are not implemented in the current managed renderer.");
        }

        textureDesc.MipLevels = texture.MultiSample > 1 ? 1u : Math.Max(texture.Levels, 1u);
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

        if (_device is null)
            throw new InvalidOperationException("Graphics device is not initialized.");

        using var nativeName = new CString(texture.Name);
        using var textureDescOwner = CreateTextureDesc();
        var textureDesc = textureDescOwner.Desc;
        textureDesc.Name = nativeName.Pointer;
        textureDesc.Type = ResourceDimension.ResourceDimTex2dArray;
        textureDesc.Width = (uint)texture.Width;
        textureDesc.Height = (uint)texture.Height;
        textureDesc.ArraySize = texture.Layers;
        textureDesc.SampleCount = (uint)texture.MultiSample;
        textureDesc.Format = MapTextureFormat(texture.Format);
        if (textureDesc.Format == 0)
            throw new NotSupportedException($"Texture format '{texture.Format}' is not supported by managed GPU uploads.");

        for (var slice = 0u; slice < texture.Layers; ++slice)
        {
            if (texture.GetData(slice, 0u) is { Length: > 0 })
                throw new NotSupportedException("CPU-backed Texture2DArray uploads are not implemented in the current managed renderer.");
        }

        textureDesc.MipLevels = Math.Max(texture.Levels, 1u);
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
        textureViewDesc.Format = MapTextureFormat(format);
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

        using var textureViewDesc = depthView.GetDesc();
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
        if (!uploaded.ResolveDirty || uploaded.ResolvedTexture is null || _immediateContext is null)
            return;

        using var resolveAttribsOwner = CreateResolveTextureSubresourceAttribs();
        var resolveAttribs = resolveAttribsOwner.Attribs;
        resolveAttribs.Format = MapTextureFormat(texture.Format);
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
        if (view is not { } actualView || actualView.RenderTexture is null)
            return;
        if (!_uploadedTextures.TryGetValue(actualView.RenderTexture, out var uploaded))
            return;

        uploaded.MarkDirty();
    }

    internal ITextureView ResolveRenderTargetView(RenderTargetView view)
    {
        return view.Type switch
        {
            RenderTargetViewType.SwapChainColor => _renderTargetView ?? throw new InvalidOperationException("Swap chain color target is not available."),
            RenderTargetViewType.SwapChainDepthStencil => _depthStencilView ?? throw new InvalidOperationException("Swap chain depth target is not available."),
            RenderTargetViewType.Resource => ResolveTextureRenderTargetView(view),
            RenderTargetViewType.ResourceSlice => ResolveTextureRenderTargetView(view),
            RenderTargetViewType.ReadOnlyResource => ResolveTextureReadOnlyDepthView(view),
            RenderTargetViewType.ReadOnlyResourceSlice => ResolveTextureReadOnlyDepthView(view),
            _ => throw new NotSupportedException($"Unsupported render target view type {view.Type}.")
        };
    }

    private ITextureView ResolveTextureRenderTargetView(RenderTargetView view)
    {
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

    private ITextureView GetOrCreateTextureSliceView(UploadedTexture uploaded, Texture texture, uint slice, bool readOnlyDepth)
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
        textureViewDesc.Format = MapTextureFormat(texture.Format);
        textureViewDesc.MostDetailedMip = 0;
        textureViewDesc.NumMipLevels = 1;
        textureViewDesc.FirstArraySlice = slice;
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

    internal static global::RedBolt.Bindings.Diligent.TextureFormat MapTextureFormat(TextureFormat format) => format switch
    {
        TextureFormat.Depth32 => global::RedBolt.Bindings.Diligent.TextureFormat.TexFormatD32Float,
        TextureFormat.Depth24Stencil8 => global::RedBolt.Bindings.Diligent.TextureFormat.TexFormatD24UnormS8Uint,
        TextureFormat.R8 => global::RedBolt.Bindings.Diligent.TextureFormat.TexFormatR8Unorm,
        TextureFormat.RG16F => global::RedBolt.Bindings.Diligent.TextureFormat.TexFormatRg16Float,
        TextureFormat.RGB8 => global::RedBolt.Bindings.Diligent.TextureFormat.TexFormatRgba8Unorm,
        TextureFormat.RGBA8 => global::RedBolt.Bindings.Diligent.TextureFormat.TexFormatRgba8Unorm,
        TextureFormat.RGBA16F => global::RedBolt.Bindings.Diligent.TextureFormat.TexFormatRgba16Float,
        _ => 0
    };

    private Vector2i ResolveRenderTargetSize(ReadOnlySpan<RenderTargetView> colorTargets, RenderTargetView? depthStencilTarget, IntRect viewportRect)
    {
        if ((colorTargets.Length != 0 && colorTargets[0].IsSwapChain) || depthStencilTarget?.IsSwapChain == true)
            return new Vector2i(Width, Height);
        if (colorTargets.Length != 0 && colorTargets[0].RenderTexture is { } colorTexture)
            return new Vector2i(colorTexture.Width, colorTexture.Height);
        if (depthStencilTarget?.RenderTexture is { } depthTexture)
            return new Vector2i(depthTexture.Width, depthTexture.Height);
        return new Vector2i(viewportRect.Width, viewportRect.Height);
    }

    private void UpdateCurrentOutputDesc(ReadOnlySpan<RenderTargetView> colorTargets, RenderTargetView? depthStencilTarget)
    {
        _currentOutputDesc.ClearRenderTargetFormats();
        Span<TextureFormat> formats = stackalloc TextureFormat[colorTargets.Length];
        for (var i = 0; i < colorTargets.Length; ++i)
            formats[i] = UnmapTextureFormat(ResolveColorTargetFormat(colorTargets[i]), out _);
        _currentOutputDesc.SetRenderTargetFormats(formats);

        global::RedBolt.Bindings.Diligent.TextureFormat depthFormat = 0;
        if (depthStencilTarget is { } depthView)
        {
            depthFormat = ResolveDepthTargetFormat(depthView);
            _currentOutputDesc.DepthStencilFormat = UnmapTextureFormat(depthFormat, out _);
        }
        else
            _currentOutputDesc.DepthStencilFormat = TextureFormat.Unknown;
        _currentOutputDesc.MultiSample = (uint)Math.Max(
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

    internal static TextureFormat UnmapTextureFormat(global::RedBolt.Bindings.Diligent.TextureFormat format, out bool isSrgb)
    {
        isSrgb = false;
        return format switch
        {
            global::RedBolt.Bindings.Diligent.TextureFormat.TexFormatD32Float => TextureFormat.Depth32,
            global::RedBolt.Bindings.Diligent.TextureFormat.TexFormatD24UnormS8Uint => TextureFormat.Depth24Stencil8,
            global::RedBolt.Bindings.Diligent.TextureFormat.TexFormatR8Unorm => TextureFormat.R8,
            global::RedBolt.Bindings.Diligent.TextureFormat.TexFormatRg16Float => TextureFormat.RG16F,
            global::RedBolt.Bindings.Diligent.TextureFormat.TexFormatBgra8Unorm => TextureFormat.RGBA8,
            global::RedBolt.Bindings.Diligent.TextureFormat.TexFormatBgra8UnormSrgb => MarkSrgb(TextureFormat.RGBA8, out isSrgb),
            global::RedBolt.Bindings.Diligent.TextureFormat.TexFormatRgba8Unorm => TextureFormat.RGBA8,
            global::RedBolt.Bindings.Diligent.TextureFormat.TexFormatRgba8UnormSrgb => MarkSrgb(TextureFormat.RGBA8, out isSrgb),
            global::RedBolt.Bindings.Diligent.TextureFormat.TexFormatRgba16Float => TextureFormat.RGBA16F,
            _ => TextureFormat.Unknown
        };
    }

    private static TextureFormat MarkSrgb(TextureFormat format, out bool isSrgb)
    {
        isSrgb = true;
        return format;
    }

    private static bool IsDepthTextureFormat(TextureFormat format)
        => format is TextureFormat.Depth32 or TextureFormat.Depth24Stencil8;

    private static bool UploadedTextureMatches(Texture texture, UploadedTexture uploaded, bool requireUnorderedAccess = false)
    {
        var expectedFormat = MapTextureFormat(texture.Format);
        if (expectedFormat == 0)
            return false;

        return uploaded.Dimension == GetResourceDimension(texture)
            && uploaded.Width == (uint)texture.Width
            && uploaded.Height == (uint)texture.Height
            && uploaded.ArraySizeOrDepth == GetArraySizeOrDepth(texture)
            && uploaded.SampleCount == (uint)texture.MultiSample
            && uploaded.HasResolvedTexture == NeedsResolvedTexture(texture)
            && (!requireUnorderedAccess || uploaded.SupportsUnorderedAccess)
            && uploaded.Format == expectedFormat
            && uploaded.MipLevels == GetUploadedMipLevels(texture);
    }

    private static bool NeedsResolvedTexture(Texture texture)
    {
        return texture.MultiSample > 1
            && !texture.NoMultiSampledAutoResolve
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
            Texture2DArray textureArray => textureArray.Layers,
            Texture3D texture3D => (uint)texture3D.Depth,
            _ => 1u
        };
    }

    private static uint GetUploadedMipLevels(Texture texture)
    {
        return texture switch
        {
            { MultiSample: > 1 } => 1u,
            Texture2D texture2D when texture2D.GetDataNoCopy(0) is { Length: > 0 } => 1u,
            _ => Math.Max(texture.Levels, 1u)
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

    private void UpdateCameraBuffer(IBuffer buffer, Camera camera, Zone? zone)
    {
        Span<byte> data = stackalloc byte[CameraBufferSize];
        WriteMatrix4Raw(data, 0, camera.GetView().Matrix4);
        WriteMatrix4Raw(data, 64, camera.GetInverseView().Matrix4);
        WriteMatrix4Raw(data, 128, camera.GetGPUViewProj());
        WriteVector4(data, 192, new Vector4f(0.0f, 0.0f, 0.0f, 1.0f));
        var cameraPosition = camera.IsAttached ? camera.Node.WorldPosition : Vector3f.Zero;
        WriteVector4(data, 208, new Vector4f(cameraPosition, 0.0f));
        WriteFloat(data, 224, camera.GetNearClip());
        WriteFloat(data, 228, camera.GetFarClip());
        WriteVector4(data, 240, GetDepthMode(camera));
        var frustumSize = camera.GetViewSizeAt(camera.GetFarClip());
        WriteVector4(data, 256, new Vector4f(frustumSize.X, frustumSize.Y, camera.GetFarClip(), 0.0f));
        WriteVector4(data, 272, new Vector4f(0.0f, 0.0f, 1.0f, 1.0f));
        WriteVector4(data, 288, GetDepthReconstruct(camera));
        WriteVector2(data, 304, new Vector2f(1.0f / Math.Max(Width, 1), 1.0f / Math.Max(Height, 1)));
        WriteVector4(data, 320, zone is not null ? new Vector4f(zone.GetAmbientLighting(), 1.0f) : DefaultAmbient);
        WriteVector4(data, 336, GetFogParameters(camera, zone));
        WriteVector3(data, 352, zone is not null ? zone.GetFogColor().ToVector3() : new Vector3f(DefaultFogColor.X, DefaultFogColor.Y, DefaultFogColor.Z));
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
        if (light is not null && light.IsAttached)
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
        Light? light, ShaderStage stage)
    {
        var usesAmbient = PassUsesAmbient(pass);
        var usesLighting = PassUsesLighting(pass);
        var passDefines = stage == ShaderStage.Vertex ? pass.GetEffectiveVertexShaderDefines() : pass.GetEffectivePixelShaderDefines();
        var key = new ShaderDefinesKey(
            vertexBuffer.GetElementMask(),
            GetMaterialTextureMask(material),
            passDefines,
            usesAmbient,
            usesLighting,
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

        if (usesLighting && light is not null)
            AddLightDefines(defines, light);

        if ((vertexBuffer.GetElementMask() & (uint)VertexMask.Normal) != 0)
        {
            defines.Add("URHO3D_VERTEX_HAS_NORMAL");
            defines.Add("URHO3D_VERTEX_NORMAL_AVAILABLE");
        }
        if ((vertexBuffer.GetElementMask() & (uint)VertexMask.Tangent) != 0)
        {
            defines.Add("URHO3D_VERTEX_HAS_TANGENT");
            defines.Add("URHO3D_VERTEX_TANGENT_AVAILABLE");
        }
        if ((vertexBuffer.GetElementMask() & (uint)VertexMask.TexCoord1) != 0)
            defines.Add("URHO3D_VERTEX_HAS_TEXCOORD0");
        if ((vertexBuffer.GetElementMask() & (uint)VertexMask.TexCoord2) != 0)
            defines.Add("URHO3D_VERTEX_HAS_TEXCOORD1");
        if ((vertexBuffer.GetElementMask() & (uint)VertexMask.Color) != 0)
            defines.Add("URHO3D_VERTEX_HAS_COLOR");

        AddTextureDefine(defines, material.GetTexture(AlbedoTextureSlot), "ALBEDO");
        AddTextureDefine(defines, material.GetTexture(NormalTextureSlot), "NORMAL");
        AddTextureDefine(defines, material.GetTexture(PropertiesTextureSlot), "PROPERTIES");
        AddTextureDefine(defines, material.GetTexture(EmissionTextureSlot), "EMISSION");

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

    private static byte GetMaterialTextureMask(Material material)
    {
        byte mask = 0;
        if (material.GetTexture(AlbedoTextureSlot) is not null)
            mask |= 1 << 0;
        if (material.GetTexture(NormalTextureSlot) is not null)
            mask |= 1 << 1;
        if (material.GetTexture(PropertiesTextureSlot) is not null)
            mask |= 1 << 2;
        if (material.GetTexture(EmissionTextureSlot) is not null)
            mask |= 1 << 3;
        return mask;
    }

    private static void AddTextureDefine(ICollection<string> defines, Texture? texture, string name)
    {
        if (texture is not null)
            defines.Add($"URHO3D_TEXTURE_{name}=1");
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

        var normalPass = referencePass.Clone();
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

    private static void AddLayoutElement(List<LayoutElementSpec> target, VertexBuffer vertexBuffer,
        VertexElementSemantic semantic, byte index, uint inputIndex)
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
            InstanceDataStepRate = 1
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

    private Zone? FindZone(Scene scene, Vector3f worldPosition)
    {
        scene.GetComponents(_zonesScratch);

        Zone? bestZone = null;
        for (var i = 0; i < _zonesScratch.Count; ++i)
        {
            var zone = _zonesScratch[i];
            if (!zone.IsAttached || !zone.IsEnabledEffective || !zone.IsInside(worldPosition))
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
            Log.Logger.LogWarning("Shader binding diagnostics: texture '{Texture}' was not found for stage '{Stage}'.", name, shaderType);
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
            Log.Logger.LogWarning("Shader binding diagnostics: fullscreen texture '{Texture}' was not found for stage '{Stage}'.", name, shaderType);
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
            Log.Logger.LogWarning("Shader binding diagnostics: dynamic buffer '{Buffer}' was not found for stage '{Stage}'.", name, shaderType);
    }

    private static void BindDynamicBuffer(FullscreenPipelineResources pipeline,
        global::RedBolt.Bindings.Diligent.ShaderType shaderType, string name, IBuffer buffer, nuint size)
    {
        if (pipeline.TryGetShaderVariable(shaderType, name, out var variable))
        {
            variable.SetBufferRange(buffer, 0, size);
        }
        else if (ShaderBindingDiagnosticsEnabled)
            Log.Logger.LogWarning("Shader binding diagnostics: fullscreen dynamic buffer '{Buffer}' was not found for stage '{Stage}'.", name, shaderType);
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
        bufferDesc.Usage = Usage.UsageDynamic;
        bufferDesc.CPUAccessFlags = CpuAccessFlags.CpuAccessWrite;
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
        if (indexBuffer.LargeIndices)
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

    private static void CopyMappedTexture(RedBolt.Bindings.Diligent.TextureFormat format, int width, int height, MappedTextureSubresource mappedSubresource, byte[] rgbaPixels)
    {
        var rowLength = checked(width * 4);
        var row = new byte[rowLength];
        for (var sourceY = 0; sourceY < height; ++sourceY)
        {
            var sourceRow = IntPtr.Add(mappedSubresource.PData, checked((int)(mappedSubresource.Stride * (ulong)sourceY)));
            Marshal.Copy(sourceRow, row, 0, rowLength);

            var destinationOffset = (height - 1 - sourceY) * rowLength;
            switch (format)
            {
                case RedBolt.Bindings.Diligent.TextureFormat.TexFormatRgba8Unorm:
                case RedBolt.Bindings.Diligent.TextureFormat.TexFormatRgba8UnormSrgb:
                    Buffer.BlockCopy(row, 0, rgbaPixels, destinationOffset, rowLength);
                    break;

                case RedBolt.Bindings.Diligent.TextureFormat.TexFormatBgra8Unorm:
                case RedBolt.Bindings.Diligent.TextureFormat.TexFormatBgra8UnormSrgb:
                    for (var x = 0; x < width; ++x)
                    {
                        var sourceOffset = x * 4;
                        var destinationPixelOffset = destinationOffset + sourceOffset;
                        rgbaPixels[destinationPixelOffset + 0] = row[sourceOffset + 2];
                        rgbaPixels[destinationPixelOffset + 1] = row[sourceOffset + 1];
                        rgbaPixels[destinationPixelOffset + 2] = row[sourceOffset + 0];
                        rgbaPixels[destinationPixelOffset + 3] = row[sourceOffset + 3];
                    }
                    break;

                default:
                    throw new NotSupportedException($"Screenshot capture does not support backbuffer format '{format}'.");
            }
        }
    }

    private static void FlipImageRows(byte[] rgbaPixels, int width, int height)
    {
        var rowLength = checked(width * 4);
        var temporaryRow = new byte[rowLength];
        for (var y = 0; y < height / 2; ++y)
        {
            var topOffset = y * rowLength;
            var bottomOffset = (height - 1 - y) * rowLength;

            Buffer.BlockCopy(rgbaPixels, topOffset, temporaryRow, 0, rowLength);
            Buffer.BlockCopy(rgbaPixels, bottomOffset, rgbaPixels, topOffset, rowLength);
            Buffer.BlockCopy(temporaryRow, 0, rgbaPixels, bottomOffset, rowLength);
        }
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
            System.IO.File.WriteAllText(vertexPath, vertexShaderResource.PrepareSource(ShaderStage.Vertex, vertexDefines, GraphicsBackend.OpenGL, ShaderExtraCode));
            System.IO.File.WriteAllText(pixelPath, pixelShaderResource.PrepareSource(ShaderStage.Pixel, pixelDefines, GraphicsBackend.OpenGL, ShaderExtraCode));
        }
        catch (Exception error)
        {
            Log.Logger.LogWarning(error, "Shader reflection diagnostics: failed to write shader source dump.");
        }

        Log.Logger.LogDebug(
            "Shader reflection diagnostics: first pipeline '{PassVertexShader}/{PassPixelShader}', layout='{Layout}', vertexSource='{VertexSource}', pixelSource='{PixelSource}'.",
            pass.VertexShader, pass.PixelShader, key.LayoutSignature, vertexPath, pixelPath);
        DumpShaderResources("vertex shader", vertexShader);
        DumpShaderResources("pixel shader", pixelShader);
        DumpPipelineVariables("pipeline", pipelineState, shaderResources);
    }

    private static void DumpShaderResources(string label, IShader shader)
    {
        var resourceCount = shader.GetResourceCount();
        Log.Logger.LogDebug("Shader reflection diagnostics: {Label} resourceCount={ResourceCount}.", label, resourceCount);
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

            Log.Logger.LogDebug(line + ".");
        }
    }

    private static void DumpPipelineVariables(string label, IPipelineState pipelineState, IShaderResourceBinding shaderResources)
    {
        using var pipelineDesc = pipelineState.GetDesc();
        Log.Logger.LogDebug(
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
        Log.Logger.LogDebug(
            "Shader reflection diagnostics: {Label} {Scope} {ShaderType} variableCount={VariableCount}.",
            label, scope, shaderType, count);
        for (uint variableIndex = 0; variableIndex < count; ++variableIndex)
        {
            var variable = getVariable(variableIndex);
            using var resourceDesc = new global::RedBolt.Bindings.Diligent.ShaderResourceDesc();
            variable.GetResourceDesc(resourceDesc);
            Log.Logger.LogDebug(
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

    private static Dictionary<PipelineVariableKey, IShaderResourceVariable> BuildShaderVariableLookup(IShaderResourceBinding shaderResources)
    {
        var result = new Dictionary<PipelineVariableKey, IShaderResourceVariable>();
        AddShaderVariableLookupEntries(result, shaderResources, global::RedBolt.Bindings.Diligent.ShaderType.ShaderTypeVertex);
        AddShaderVariableLookupEntries(result, shaderResources, global::RedBolt.Bindings.Diligent.ShaderType.ShaderTypePixel);
        return result;
    }

    private static void AddShaderVariableLookupEntries(Dictionary<PipelineVariableKey, IShaderResourceVariable> result,
        IShaderResourceBinding shaderResources, global::RedBolt.Bindings.Diligent.ShaderType shaderType)
    {
        var count = shaderResources.GetVariableCount(shaderType);
        for (uint index = 0; index < count; ++index)
        {
            var variable = shaderResources.GetVariableByIndex(shaderType, index);
            using var resourceDesc = new global::RedBolt.Bindings.Diligent.ShaderResourceDesc();
            variable.GetResourceDesc(resourceDesc);
            var name = PtrToUtf8String(resourceDesc.Name);
            if (!string.IsNullOrWhiteSpace(name))
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

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void GlFinishDelegate();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void GlReadBufferDelegate(uint mode);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void GlReadPixelsDelegate(int x, int y, int width, int height, uint format, uint type, IntPtr pixels);

    private readonly record struct LayoutElementSpec(
        uint InputIndex,
        uint BufferSlot,
        uint NumComponents,
        global::RedBolt.Bindings.Diligent.ValueType ValueType,
        bool IsNormalized,
        uint RelativeOffset,
        uint Stride,
        InputElementFrequency Frequency,
        uint InstanceDataStepRate)
    {
        public LayoutElementSpec()
            : this(0, 0, 0, global::RedBolt.Bindings.Diligent.ValueType.VtFloat32, false, 0, 0,
                InputElementFrequency.InputElementFrequencyPerVertex, 1)
        {
        }
    }

    private readonly record struct PipelineKey(string VertexShader, string PixelShader, string VertexDefines,
        string PixelDefines, string LayoutSignature, CullMode CullMode, FillMode FillMode,
        CompareMode DepthTestMode, bool DepthWrite, bool AlphaToCoverage, bool ReverseCulling,
        global::RedBolt.Bindings.Diligent.TextureFormat ColorFormat,
        global::RedBolt.Bindings.Diligent.TextureFormat DepthFormat);

    private readonly record struct FullscreenPipelineKey(string ShaderName, string ShaderDefines,
        BlendMode BlendMode,
        bool BilinearColorSampler, global::RedBolt.Bindings.Diligent.TextureFormat ColorFormat,
        global::RedBolt.Bindings.Diligent.TextureFormat DepthFormat);
    private readonly record struct ShaderDefinesKey(
        uint ElementMask,
        byte TextureMask,
        string PassDefines,
        bool UsesAmbient,
        bool UsesLighting,
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
                var element = LayoutElement.CreateManagedWrapper(ptr, ownsHandle: false);
                var spec = elements[i];
                element.HLSLSemantic = _semantic.Pointer;
                element.InputIndex = spec.InputIndex;
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
        }

        public TextureDesc Desc { get; }

        public static implicit operator TextureDesc(TextureDescOwner owner) => owner.Desc;

        public void Dispose()
        {
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

    private sealed class UploadedTexture : IDisposable
    {
        private readonly Dictionary<(TextureViewType ViewType, uint Slice), ITextureView> _sliceViews = [];
        private readonly Dictionary<RawTextureUAVKey, ITextureView> _uavs = [];

        public UploadedTexture(Texture sourceTexture, ITexture texture, ITextureView shaderView, ITextureView? renderTargetView,
            ITextureView? depthStencilView, ITextureView? readOnlyDepthView, ITexture? resolvedTexture = null,
            bool supportsUnorderedAccess = false)
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
            Format = MapTextureFormat(sourceTexture.Format);
            MipLevels = GetUploadedMipLevels(sourceTexture);
            SupportsUnorderedAccess = supportsUnorderedAccess;
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

        public bool TryGetSliceView(TextureViewType viewType, uint slice, out ITextureView view)
        {
            return _sliceViews.TryGetValue((viewType, slice), out view!);
        }

        public void CacheSliceView(TextureViewType viewType, uint slice, ITextureView view)
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
            ReleaseIfNotNull(ResolvedTexture);
            ReleaseIfNotNull(Texture);
        }
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
