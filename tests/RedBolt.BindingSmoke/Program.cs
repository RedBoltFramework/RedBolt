using System.Reflection;
using System.Runtime.InteropServices;
using RedBolt;
using RedBolt.Bindings.Diligent;
using RedBolt.Bindings.Sdl;

try
{
    var diligentAssembly = Assembly.Load("RedBolt.Bindings.Diligent");

    var valueTypeEnum = diligentAssembly.GetTypes()
        .FirstOrDefault(type => type.IsEnum && type.GetField("VtFloat32") is not null)
        ?? throw new InvalidOperationException("Generated Diligent VALUE_TYPE enum was not found.");

    if (Enum.Parse(valueTypeEnum, "VtFloat32") is not object vtFloat32 || Convert.ToInt32(vtFloat32) <= 0)
        throw new InvalidOperationException("Generated Diligent VT_FLOAT32 enum value was invalid.");

    var runDiligentGlSmoke = IsEnvironmentFlagEnabled("REDBOLT_RUN_DILIGENT_GL_SMOKE");

    RunGeneratedSdlWindowSmoke(useDummyVideoDriver: !runDiligentGlSmoke);

    if (runDiligentGlSmoke)
        RunDiligentOpenGlSmoke();

    RunHeadlessApplicationSmoke();

    Console.WriteLine("RedBolt generated dependency binding smoke OK");
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex);
    return 1;
}

static bool IsEnvironmentFlagEnabled(string name)
{
    var value = Environment.GetEnvironmentVariable(name);
    return string.Equals(value, "1", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);
}

static void RunGeneratedSdlWindowSmoke(bool useDummyVideoDriver)
{
    const uint sdlInitVideo = 0x00000020u;

    if (useDummyVideoDriver && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SDL_VIDEODRIVER")))
        Environment.SetEnvironmentVariable("SDL_VIDEODRIVER", "dummy");

    var initResult = SdlNativeBindings.SdlInit(sdlInitVideo);
    if (initResult != 0)
        throw new InvalidOperationException($"Generated SDL_Init binding failed with {initResult}.");

    try
    {
        var initialized = SdlNativeBindings.SdlWasInit(sdlInitVideo);
        if ((initialized & sdlInitVideo) == 0)
            throw new InvalidOperationException("Generated SDL_WasInit binding did not report the video subsystem as initialized.");

        sbyte emptyTitle = 0;
        using var window = SdlNativeBindings.SdlCreateWindow(
            in emptyTitle,
            x: 0,
            y: 0,
            w: 16,
            h: 16,
            flags: (uint)SdlWindowFlags.SdlWindowHidden);

        if (window.IsNull)
            throw new InvalidOperationException("Generated SDL_CreateWindow binding returned a null window.");

        try
        {
            using var @event = new SdlEvent();
            _ = SdlNativeBindings.SdlPollEvent(@event);
        }
        finally
        {
            SdlNativeBindings.SdlDestroyWindow(window);
        }
    }
    finally
    {
        SdlNativeBindings.SdlQuit();
    }
}

static void RunDiligentOpenGlSmoke()
{
    const uint sdlInitVideo = 0x00000020u;

    if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DISPLAY")))
        throw new InvalidOperationException("REDBOLT_RUN_DILIGENT_GL_SMOKE requires an X11 DISPLAY.");

    if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SDL_VIDEODRIVER")))
        Environment.SetEnvironmentVariable("SDL_VIDEODRIVER", "x11");

    var initResult = SdlNativeBindings.SdlInit(sdlInitVideo);
    if (initResult != 0)
        throw new InvalidOperationException($"Generated SDL_Init binding failed with {initResult} for Diligent OpenGL smoke.");

    SdlWindow? window = null;
    var glContext = IntPtr.Zero;
    IRenderDevice? device = null;
    IDeviceContext? immediateContext = null;
    ISwapChain? swapChain = null;

    try
    {
        CheckSdlGl(SdlNativeBindings.SdlGlSetAttribute(SdlGLattr.SdlGlContextMajorVersion, 3), "SDL_GL_SetAttribute major version");
        CheckSdlGl(SdlNativeBindings.SdlGlSetAttribute(SdlGLattr.SdlGlContextMinorVersion, 3), "SDL_GL_SetAttribute minor version");
        CheckSdlGl(SdlNativeBindings.SdlGlSetAttribute(SdlGLattr.SdlGlContextProfileMask, (int)SdlGLprofile.SdlGlContextProfileCore), "SDL_GL_SetAttribute profile mask");
        CheckSdlGl(SdlNativeBindings.SdlGlSetAttribute(SdlGLattr.SdlGlDoublebuffer, 1), "SDL_GL_SetAttribute doublebuffer");

        sbyte emptyTitle = 0;
        window = SdlNativeBindings.SdlCreateWindow(
            in emptyTitle,
            x: 0,
            y: 0,
            w: 64,
            h: 64,
            flags: (uint)(SdlWindowFlags.SdlWindowOpengl | SdlWindowFlags.SdlWindowHidden));

        if (window.IsNull)
            throw new InvalidOperationException("Generated SDL_CreateWindow binding returned a null OpenGL window.");

        glContext = SdlNativeBindings.SdlGlCreateContext(window);
        if (glContext == IntPtr.Zero)
            throw new InvalidOperationException("Generated SDL_GL_CreateContext binding returned a null context.");

        CheckSdlGl(SdlNativeBindings.SdlGlMakeCurrent(window, glContext), "SDL_GL_MakeCurrent");
        _ = SdlNativeBindings.SdlGlSetSwapInterval(0);

        using var wmInfo = CreateInitializedWmInfo();
        if (SdlNativeBindings.SdlGetWindowWMInfo(window, wmInfo) != SdlBool.SdlTrue)
            throw new InvalidOperationException("Generated SDL_GetWindowWMInfo binding failed for the OpenGL window.");

        if (wmInfo.Subsystem != SdlSyswmType.SdlSyswmX11)
            throw new InvalidOperationException($"Diligent OpenGL smoke requires SDL X11 WM info, got {wmInfo.Subsystem}.");

        if (wmInfo.InfoX11Display == IntPtr.Zero || wmInfo.InfoX11Window == 0)
            throw new InvalidOperationException("SDL X11 WM info did not contain display and window handles.");

        using var engineCreateInfo = new EngineGLCreateInfo
        {
            WindowPDisplay = wmInfo.InfoX11Display,
            WindowWindowId = checked((uint)wmInfo.InfoX11Window)
        };
        using var swapChainDesc = new SwapChainDesc();

        var factory = DiligentNativeBindings.GetEngineFactoryOpenGL();
        if (factory.IsNull)
            throw new InvalidOperationException("Generated GetEngineFactoryOpenGL binding returned a null factory.");

        factory.CreateDeviceAndSwapChainGL(engineCreateInfo, out device, out immediateContext, swapChainDesc, out swapChain);
        if (device.IsNull || immediateContext.IsNull || swapChain.IsNull)
            throw new InvalidOperationException("Generated CreateDeviceAndSwapChainGL binding returned a null device, context, or swap chain.");

        var rtv = swapChain.GetCurrentBackBufferRTV();
        if (rtv.IsNull)
            throw new InvalidOperationException("Generated GetCurrentBackBufferRTV binding returned a null render target view.");

        ITextureView?[] renderTargets = [rtv];
        immediateContext.SetRenderTargets((uint)renderTargets.Length, renderTargets, null!, ResourceStateTransitionMode.ResourceStateTransitionModeTransition);

        var clearColor = new[] { 0.08f, 0.16f, 0.30f, 1.0f };
        var clearColorHandle = GCHandle.Alloc(clearColor, GCHandleType.Pinned);
        try
        {
            immediateContext.ClearRenderTarget(rtv, clearColorHandle.AddrOfPinnedObject(), ResourceStateTransitionMode.ResourceStateTransitionModeTransition);
        }
        finally
        {
            clearColorHandle.Free();
        }

        swapChain.Present(0);
        immediateContext.SetRenderTargets(0, global::System.ReadOnlySpan<ITextureView?>.Empty, null!, ResourceStateTransitionMode.ResourceStateTransitionModeNone);
    }
    finally
    {
        ReleaseIfNotNull(swapChain);
        ReleaseIfNotNull(immediateContext);
        ReleaseIfNotNull(device);

        if (glContext != IntPtr.Zero)
        {
            if (window is not null && !window.IsNull)
                _ = SdlNativeBindings.SdlGlMakeCurrent(window, IntPtr.Zero);
            SdlNativeBindings.SdlGlDeleteContext(glContext);
        }

        if (window is not null && !window.IsNull)
            SdlNativeBindings.SdlDestroyWindow(window);

        SdlNativeBindings.SdlQuit();
    }
}

static SdlSysWMinfo CreateInitializedWmInfo()
{
    using var version = new SdlVersion();
    SdlNativeBindings.SdlGetVersion(version);

    return new SdlSysWMinfo
    {
        VersionMajor = version.Major,
        VersionMinor = version.Minor,
        VersionPatch = version.Patch
    };
}

static void CheckSdlGl(int result, string operation)
{
    if (result != 0)
        throw new InvalidOperationException($"{operation} failed with {result}.");
}

static void ReleaseIfNotNull(IObject? value)
{
    if (value is not null && !value.IsNull)
        _ = value.Release();
}

static void RunHeadlessApplicationSmoke()
{
    var application = new SmokeHeadlessApplication();
    var exitCode = application.Run();
    if (exitCode != 0)
        throw new InvalidOperationException($"Headless application exited with {exitCode}.");

    application.AssertLifecycle();
}

sealed class SmokeHeadlessApplication : Application
{
    private readonly List<string> hooks = [];

    private bool engineWasInitializedAtStart;
    private bool engineWasHeadlessAtStart;

    protected override void Setup(EngineParameters parameters)
    {
        hooks.Add(nameof(Setup));
        parameters.ApplicationName = "RedBolt.BindingSmoke";
        parameters.Headless = true;
    }

    protected override void Start()
    {
        hooks.Add(nameof(Start));
        engineWasInitializedAtStart = Engine.IsInitialized;
        engineWasHeadlessAtStart = Engine.IsHeadless;

        var model = Resources.Get<Model>("Models/ProceduralBox");
        var material = Resources.Get<Material>("Materials/FirstCube");
        if (!ReferenceEquals(model, Resources.Get<Model>("Models/ProceduralBox")))
            throw new InvalidOperationException("ResourceCache did not return the cached procedural model.");

        var scene = new Scene();
        scene.CreateComponent<Octree>();
        var cameraNode = scene.CreateChild("Camera");
        cameraNode.CreateComponent<Camera>();
        var cubeNode = scene.CreateChild("Cube");
        var cube = cubeNode.CreateComponent<StaticModel>();
        cube.SetModel(model);
        cube.SetMaterial(material);
        if (cube.NumGeometries != 1 || model.GetGeometry(0)?.VertexCount != 36u)
            throw new InvalidOperationException("StaticModel did not expose the procedural model geometry.");
        if (!ReferenceEquals(cube.GetMaterial(), material))
            throw new InvalidOperationException("StaticModel did not expose the assigned material.");
        if (Input.GetKeyDown(Key.Escape) || Input.GetScancodePress(Scancode.Escape))
            throw new InvalidOperationException("Headless input state should start empty.");

        Exit();
    }

    protected override void Stop()
    {
        hooks.Add(nameof(Stop));
    }

    public void AssertLifecycle()
    {
        var expectedHooks = new[] { nameof(Setup), nameof(Start), nameof(Stop) };
        if (!hooks.SequenceEqual(expectedHooks))
            throw new InvalidOperationException($"Unexpected application lifecycle: {string.Join(", ", hooks)}.");

        if (!engineWasInitializedAtStart)
            throw new InvalidOperationException("Engine was not initialized before Start.");

        if (!engineWasHeadlessAtStart)
            throw new InvalidOperationException("Headless engine parameter was not propagated to Engine.");
    }
}
