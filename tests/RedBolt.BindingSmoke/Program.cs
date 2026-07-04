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

        Engine.SetMinFps(0);
        Engine.SetMaxFps(144);
        Engine.SetMaxInactiveFps(30);
        Engine.SetTimeStepSmoothing(0);
        Engine.SetPauseMinimized(false);
        Engine.SetAutoExit(false);
        Engine.SetParameter("CustomParameter", 42);
        Engine.SetNextTimeStep(0.125f);
        var observedTimeStep = -1.0f;
        Engine.RunFrame(timeStep => observedTimeStep = timeStep);
        if (Engine.GetMinFps() != 1 || Engine.GetMaxFps() != 144 || Engine.GetMaxInactiveFps() != 30 || Engine.GetTimeStepSmoothing() != 1)
            throw new InvalidOperationException("Engine did not preserve rbfx-style FPS and timestep smoothing helpers.");
        if (Engine.GetPauseMinimized() || Engine.GetAutoExit() || !Engine.HasParameter("CustomParameter") || !Equals(Engine.GetParameter("CustomParameter"), 42))
            throw new InvalidOperationException("Engine did not preserve rbfx-style parameter and pause/auto-exit helpers.");
        Engine.Time.SetTimerPeriod(1u);
        if (!MathDefs.ApproximatelyEquivalent(observedTimeStep, 0.125f) || !MathDefs.ApproximatelyEquivalent(Engine.GetNextTimeStep(), 0.125f) || Engine.Time.GetFrameNumber() != 1 || Engine.Time.GetTimerPeriod() != 1u)
            throw new InvalidOperationException("Engine did not preserve rbfx-style next timestep helper.");
        if (!MathDefs.ApproximatelyEquivalent(Engine.Time.GetTimeStep(), 0.125f) || !MathDefs.ApproximatelyEquivalent(Engine.Time.GetElapsedTime(), 0.125f) || !MathDefs.ApproximatelyEquivalent(Engine.Time.GetFramesPerSecond(), 8.0f))
            throw new InvalidOperationException("EngineTime did not preserve rbfx-style frame timing helpers.");
        var timer = new RedBolt.Timer();
        RedBolt.Time.Sleep(2);
        var elapsedMs = timer.GetMSec(reset: true);
        if (elapsedMs == 0)
            throw new InvalidOperationException("Timer did not preserve rbfx-style elapsed/reset helpers.");
        var hiresTimer = new HiresTimer();
        RedBolt.Time.Sleep(2);
        if (hiresTimer.GetUSec(reset: true) <= 0 || HiresTimer.GetFrequency() <= 0)
            throw new InvalidOperationException("HiresTimer did not preserve rbfx-style elapsed/frequency helpers.");
        var systemTime = RedBolt.Time.GetSystemTime();
        RedBolt.Time.Sleep(1);
        if (RedBolt.Time.GetSystemTime() - systemTime > 1000u || RedBolt.Time.GetTimeSinceEpoch() < 1_700_000_000u || string.IsNullOrWhiteSpace(RedBolt.Time.GetTimeStamp()))
            throw new InvalidOperationException("Time did not preserve rbfx-style static time helpers.");
        var timedCounter = new TimedCounter(intervalCount: 2, intervalLengthMs: 50u);
        timedCounter.AddSample(2.0f);
        timedCounter.AddSample(4.0f);
        RedBolt.Time.Sleep(60);
        if (!MathDefs.ApproximatelyEquivalent(timedCounter.GetLast(), 6.0f))
            throw new InvalidOperationException("TimedCounter did not preserve rbfx-style interval accumulation helpers.");
        timedCounter.AddSample(10.0f);
        RedBolt.Time.Sleep(60);
        if (!MathDefs.ApproximatelyEquivalent(timedCounter.GetLast(), 10.0f) || !MathDefs.ApproximatelyEquivalent(timedCounter.GetAverage(), 8.0f) || timedCounter.GetData().Count != 2)
            throw new InvalidOperationException("TimedCounter did not preserve rbfx-style interval history helpers.");

        var model = Resources.Get<Model>("Models/ProceduralBox");
        var material = Resources.Get<Material>("Materials/FirstCube");
        if (!ReferenceEquals(model, Resources.Get<Model>("Models/ProceduralBox")))
            throw new InvalidOperationException("ResourceCache did not return the cached procedural model.");
        material.SetCullMode(RedBolt.CullMode.Clockwise);
        material.SetShadowCullMode(RedBolt.CullMode.None);
        material.SetFillMode(RedBolt.FillMode.Wireframe);
        material.SetDepthBias(new BiasParameters(1.0f, 2.0f));
        material.SetRenderOrder(42u);
        material.SetUVTransform(new Vector2(0.25f, 0.5f), 90.0f, new Vector2(2.0f, 4.0f));
        material.SetShaderParameter("CustomValue", Material.ParseShaderParameterValue("1 2 3"), isCustom: true);
        if (material.GetCullMode() != RedBolt.CullMode.Clockwise || material.GetShadowCullMode() != RedBolt.CullMode.None || material.GetFillMode() != RedBolt.FillMode.Wireframe)
            throw new InvalidOperationException("Material did not preserve rbfx-style render state helpers.");
        if (material.GetRenderOrder() != 42 || material.GetDepthBias() != new BiasParameters(1.0f, 2.0f) || material.GetShaderParameterHash() == 0)
            throw new InvalidOperationException("Material did not preserve rbfx-style depth bias, render order, or shader parameter hash helpers.");
        if (material.GetShaderParameter("CustomValue") is not Vector3 customValue || customValue != new Vector3(1.0f, 2.0f, 3.0f) || !material.ShaderParameters["CustomValue"].IsCustom)
            throw new InvalidOperationException("Material did not preserve rbfx-style shader parameter helpers.");
        if (!material.UVOffset.ApproximatelyEquivalent(new Vector2(0.25f, 0.5f)) || !material.UVRepeat.ApproximatelyEquivalent(new Vector2(2.0f, 4.0f)))
            throw new InvalidOperationException("Material did not preserve rbfx-style UV transform metadata.");
        material.AddMetadata("Category", "Smoke");
        var metadataClone = material.Clone("Materials/FirstCubeMetadataClone");
        if (!material.HasMetadata() || !Equals(material.GetMetadata("Category"), "Smoke") || !material.GetMetadataKeys().Contains("Category") || !Equals(metadataClone.GetMetadata("Category"), "Smoke"))
            throw new InvalidOperationException("ResourceWithMetadata did not preserve rbfx-style metadata helpers.");
        metadataClone.RemoveMetadata("Category");
        if (metadataClone.HasMetadata("Category"))
            throw new InvalidOperationException("ResourceWithMetadata did not preserve rbfx-style metadata removal helpers.");
        var tempResourceDir = Path.Combine(Path.GetTempPath(), "RedBolt.BindingSmoke", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(tempResourceDir, "Data"));
        try
        {
            var fileName = Path.Combine(tempResourceDir, "Data", "hello.txt");
            File.WriteAllText(fileName, "hello from resource cache");
            var backgroundFileName = Path.Combine(tempResourceDir, "Data", "background.txt");
            File.WriteAllText(backgroundFileName, "background resource");
            var jsonName = Path.Combine(tempResourceDir, "Data", "config.json");
            File.WriteAllText(jsonName, "{\"name\":\"redbolt\",\"value\":7}");
            var xmlName = Path.Combine(tempResourceDir, "Data", "config.xml");
            File.WriteAllText(xmlName, "<root><child value=\"7\" /></root>");
            var localizationName = Path.Combine(tempResourceDir, "Data", "localization.json");
            File.WriteAllText(localizationName, "{\"Hello\":{\"en\":\"Hello\",\"fi\":\"Hei\"}}");
            if (!Resources.AddResourceDir(tempResourceDir) || !Resources.Exists<BinaryFile>("Data/hello.txt"))
                throw new InvalidOperationException("ResourceCache did not register a file-backed resource directory.");
            Resources.SetMemoryBudget<BinaryFile>(4096ul);
            Resources.SetReturnFailedResources(true);
            Resources.SetSearchPackagesFirst(true);
            Resources.SetFinishBackgroundResourcesMs(0);
            if (Resources.GetMemoryBudget<BinaryFile>() != 4096ul || !Resources.GetReturnFailedResources() || !Resources.GetSearchPackagesFirst() || Resources.GetFinishBackgroundResourcesMs() != 1)
                throw new InvalidOperationException("ResourceCache did not preserve rbfx-style setting helpers.");
            if (Resources.SanitateResourceName(Path.Combine(tempResourceDir, "Data", "hello.txt")) != "Data/hello.txt")
                throw new InvalidOperationException("ResourceCache did not preserve rbfx-style resource name sanitizing.");
            using (var openedFile = Resources.GetFile("Data/hello.txt"))
            {
                if (openedFile is null || openedFile.Length == 0)
                    throw new InvalidOperationException("ResourceCache did not preserve rbfx-style file lookup helpers.");
            }
            var binaryFile = Resources.GetResource<BinaryFile>("Data/hello.txt");
            if (binaryFile.GetText() != "hello from resource cache" || binaryFile.AbsoluteFileName != fileName || !ReferenceEquals(binaryFile, Resources.GetResource<BinaryFile>("Data/hello.txt")))
                throw new InvalidOperationException("ResourceCache did not load and cache a file-backed BinaryFile.");
            if (ReferenceEquals(binaryFile, Resources.GetTempResource<BinaryFile>("Data/hello.txt")))
                throw new InvalidOperationException("ResourceCache temp resource loading reused a cached instance.");
            if (!ReferenceEquals(Resources.GetExistingResource<BinaryFile>("Data/hello.txt"), binaryFile) || !Resources.GetAllResources().ContainsKey((typeof(BinaryFile), "Data/hello.txt")) || Resources.GetMemoryUse<BinaryFile>() < binaryFile.GetMemoryUse() || Resources.GetTotalMemoryUse() < binaryFile.GetMemoryUse())
                throw new InvalidOperationException("ResourceCache did not preserve rbfx-style cached resource inspection helpers.");
            if (!Resources.BackgroundLoadResource<BinaryFile>("Data/background.txt") || Resources.BackgroundLoadResource<BinaryFile>("Data/background.txt") || Resources.GetNumBackgroundLoadResources() != 1u)
                throw new InvalidOperationException("ResourceCache did not preserve rbfx-style background queue helpers.");
            Resources.FinishBackgroundResources(1000);
            var backgroundBinary = Resources.GetExistingResource<BinaryFile>("Data/background.txt");
            if (backgroundBinary is null || backgroundBinary.GetText() != "background resource" || backgroundBinary.GetAsyncLoadState() != AsyncLoadState.Success || Resources.GetNumBackgroundLoadResources() != 0u)
                throw new InvalidOperationException("ResourceCache did not preserve rbfx-style background finish helpers.");
            var savedBinaryName = Path.Combine(tempResourceDir, "Data", "saved.bin");
            var savedBinaryFile = new BinaryFile("Data/saved.bin");
            savedBinaryFile.SetText("saved through resource helper");
            var reloadedBinaryFile = new BinaryFile("Data/reloaded.bin");
            if (!savedBinaryFile.SaveFile(savedBinaryName) || !reloadedBinaryFile.LoadFile(savedBinaryName) || reloadedBinaryFile.GetText() != "saved through resource helper")
                throw new InvalidOperationException("Resource did not preserve rbfx-style LoadFile/SaveFile helpers.");
            reloadedBinaryFile.SetName("Data/renamed.bin");
            reloadedBinaryFile.SetMemoryUse(123u);
            reloadedBinaryFile.SetAsyncLoadState(AsyncLoadState.Queued);
            reloadedBinaryFile.SetAbsoluteFileName(savedBinaryName);
            if (reloadedBinaryFile.GetName() != "Data/renamed.bin" || reloadedBinaryFile.GetNameHash().Value != StringHash.Calculate("Data/renamed.bin") || reloadedBinaryFile.GetMemoryUse() != 123u || reloadedBinaryFile.GetAsyncLoadState() != AsyncLoadState.Queued || reloadedBinaryFile.GetAbsoluteFileName() != savedBinaryName)
                throw new InvalidOperationException("Resource did not preserve rbfx-style property helpers.");
            var jsonFile = Resources.GetResource<JSONFile>("Data/config.json");
            if (jsonFile.GetRoot().Get("value").GetInt() != 7 || jsonFile.AbsoluteFileName != jsonName || !jsonFile.ToString("").Contains("redbolt", StringComparison.Ordinal))
                throw new InvalidOperationException("ResourceCache did not load a file-backed JSONFile.");
            var xmlFile = Resources.GetResource<XMLFile>("Data/config.xml");
            if (xmlFile.GetRoot("root").GetChild("child").GetInt("value") != 7 || xmlFile.AbsoluteFileName != xmlName || !xmlFile.ToString("").Contains("child", StringComparison.Ordinal))
                throw new InvalidOperationException("ResourceCache did not load a file-backed XMLFile.");
            if (!JSONFile.ParseJSON("{\"ok\":true}", out var parsedJson) || !parsedJson.Get("ok").GetBool() || !xmlFile.FromString("<newRoot />") || xmlFile.GetOrCreateRoot("newRoot").Name != "newRoot")
                throw new InvalidOperationException("JSONFile/XMLFile did not preserve rbfx-style string/root helpers.");
            jsonFile.GetRoot().Set("tags", new JSONValue([new JSONValue("engine"), new JSONValue("rbfx")]));
            if (jsonFile.GetRoot().Get("tags").Size() != 2 || !jsonFile.GetRoot().Contains("tags") || !jsonFile.ToString("").Contains("engine", StringComparison.Ordinal))
                throw new InvalidOperationException("JSONValue did not preserve rbfx-style object and array helpers.");
            jsonFile.GetRoot().Set("enabled", new JSONValue(true));
            jsonFile.GetRoot().Erase("enabled");
            if (jsonFile.GetRoot().Contains("enabled"))
                throw new InvalidOperationException("JSONValue did not preserve rbfx-style object erase helpers.");
            var xmlRoot = xmlFile.GetOrCreateRoot("newRoot");
            var childA = xmlRoot.CreateChild("child");
            var xmlMaterialRef = ResourceRef.Parse("Material;Materials/FirstCube");
            childA.SetVector3("position", new Vector3(1.0f, 2.0f, 3.0f));
            childA.SetResourceRef("material", xmlMaterialRef);
            var childB = xmlRoot.CreateChild("sibling");
            childB.SetBoundingBox("bounds", new BoundingBox(new Vector3(-1.0f, -2.0f, -3.0f), new Vector3(4.0f, 5.0f, 6.0f)));
            if (!xmlRoot.HasChild("child") || childA.GetNext("sibling").Name != "sibling" || childA.GetParent().Name != "newRoot")
                throw new InvalidOperationException("XMLElement did not preserve rbfx-style hierarchy helpers.");
            if (!childA.GetVector3("position").ApproximatelyEquivalent(new Vector3(1.0f, 2.0f, 3.0f)) || childA.GetResourceRef("material") != xmlMaterialRef || !childB.GetBoundingBox("bounds").Min.ApproximatelyEquivalent(new Vector3(-1.0f, -2.0f, -3.0f)) || childA.GetNumAttributes() != 2)
                throw new InvalidOperationException("XMLElement did not preserve rbfx-style typed attribute helpers.");
            if (!xmlRoot.RemoveChild("sibling") || xmlRoot.HasChild("sibling"))
                throw new InvalidOperationException("XMLElement did not preserve rbfx-style child removal helpers.");
            var manualBinaryFile = new BinaryFile("Data/manual.bin");
            manualBinaryFile.SetText("manual");
            if (!Resources.AddManualResource(manualBinaryFile) || Resources.AddManualResource(manualBinaryFile))
                throw new InvalidOperationException("ResourceCache did not preserve rbfx-style manual resource helpers.");
            Resources.ReleaseResource("Data/manual.bin");
            if (Resources.GetExistingResource<BinaryFile>("Data/manual.bin") is not null)
                throw new InvalidOperationException("ResourceCache did not preserve rbfx-style release-by-name helpers.");
            var localization = new Localization(Resources);
            localization.LoadJSONFile("Data/localization.json");
            if (localization.GetNumLanguages() != 2 || localization.GetLanguage() != "en" || localization.Get("Hello") != "Hello")
                throw new InvalidOperationException("Localization did not preserve rbfx-style multi-language loading helpers.");
            localization.SetLanguage("fi");
            if (localization.GetLanguageIndex("fi") != 1 || localization.Get("Hello") != "Hei")
                throw new InvalidOperationException("Localization did not preserve rbfx-style language selection helpers.");
            if (!JSONFile.ParseJSON("{\"Bye\":\"Moi\"}", out var singleLanguageJson))
                throw new InvalidOperationException("Localization smoke JSON parsing failed.");
            localization.LoadSingleLanguageJSON(singleLanguageJson, "fi");
            if (localization.Get("Bye") != "Moi")
                throw new InvalidOperationException("Localization did not preserve rbfx-style single-language loading helpers.");
            if (!Resources.RemoveResourceDir(tempResourceDir) || Resources.Exists("Data/hello.txt"))
                throw new InvalidOperationException("ResourceCache did not preserve rbfx-style resource directory removal helpers.");

            var jsonResource = new SmokeJsonResource("Data/smoke.json");
            using var jsonResourceSource = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("{\"Name\":\"redbolt\",\"Count\":3}"));
            if (!jsonResource.Load(jsonResourceSource) || jsonResource.Value?.Name != "redbolt" || jsonResource.Value.Count != 3)
                throw new InvalidOperationException("JSONResourceBase did not preserve rbfx-style json deserialization helpers.");
            using var jsonResourceDestination = new MemoryStream();
            jsonResource.Value = new SmokeJsonPayload { Name = "serialized", Count = 7 };
            if (!jsonResource.Save(jsonResourceDestination) || !System.Text.Encoding.UTF8.GetString(jsonResourceDestination.ToArray()).Contains("serialized", StringComparison.Ordinal))
                throw new InvalidOperationException("JSONResourceBase did not preserve rbfx-style json serialization helpers.");
        }
        finally
        {
            Directory.Delete(tempResourceDir, recursive: true);
        }

        var vectorBuffer = new VectorBuffer();
        if (!vectorBuffer.WriteInt(42) || !vectorBuffer.WriteString("redbolt") || !vectorBuffer.WriteVector3(new Vector3(1.0f, 2.0f, 3.0f)) || !vectorBuffer.WriteVLE(300u))
            throw new InvalidOperationException("VectorBuffer did not preserve rbfx-style serializer helpers.");
        vectorBuffer.Seek(0);
        if (vectorBuffer.ReadInt() != 42 || vectorBuffer.ReadString() != "redbolt" || vectorBuffer.ReadVector3() != new Vector3(1.0f, 2.0f, 3.0f) || vectorBuffer.ReadVLE() != 300u)
            throw new InvalidOperationException("VectorBuffer did not preserve rbfx-style deserializer helpers.");
        var memoryBuffer = new MemoryBuffer(vectorBuffer.GetBuffer());
        if (memoryBuffer.ReadInt() != 42 || memoryBuffer.ReadString() != "redbolt" || memoryBuffer.ReadVector3() != new Vector3(1.0f, 2.0f, 3.0f) || memoryBuffer.ReadVLE() != 300u || !memoryBuffer.Eof)
            throw new InvalidOperationException("MemoryBuffer did not preserve rbfx-style read-only buffer helpers.");
        var lineBuffer = new VectorBuffer();
        lineBuffer.WriteLine("first");
        lineBuffer.WriteLine("second");
        lineBuffer.Seek(0);
        if (lineBuffer.ReadLine() != "first" || lineBuffer.ReadLine() != "second")
            throw new InvalidOperationException("Deserializer did not preserve rbfx-style line helpers.");

        var scene = new Scene();
        var octree = scene.CreateComponent<Octree>();
        if (scene.Root.Id == 0 || scene.Root.GetNumAttributes() == 0)
            throw new InvalidOperationException("Scene root did not initialize serializable node state.");
        if (scene.IsEmpty() || !scene.IsEmpty(ignoreComponents: true))
            throw new InvalidOperationException("Scene did not preserve rbfx-style empty-state helpers.");
        scene.SetTimeScale(2.0f);
        scene.SetElapsedTime(1.0f);
        scene.Update(0.5f);
        scene.SetUpdateEnabled(false);
        scene.Update(1.0f);
        if (!MathDefs.ApproximatelyEquivalent(scene.GetElapsedTime(), 2.0f) || !MathDefs.ApproximatelyEquivalent(scene.GetTimeScale(), 2.0f) || !MathDefs.ApproximatelyEquivalent(scene.GetEffectiveTimeScale(), 0.0f))
            throw new InvalidOperationException("Scene did not preserve rbfx-style update time helpers.");
        scene.SetUpdateEnabled(true);
        scene.SetAsyncLoadingMs(0);
        scene.SetFileName("Scenes/Test.scene");
        if (!scene.IsUpdateEnabled() || scene.GetAsyncLoadingMs() != 1 || scene.IsAsyncLoading() || !MathDefs.ApproximatelyEquivalent(scene.GetAsyncProgress(), 1.0f) || scene.GetFileName() != "Scenes/Test.scene" || scene.GetChecksum() == 0u)
            throw new InvalidOperationException("Scene did not preserve rbfx-style async/file metadata helpers.");
        var lookupNode = scene.CreateChild("Lookup", 99);
        lookupNode.AddTag("lookup");
        var lookupCamera = lookupNode.CreateComponent<Camera>(124);
        if (scene.GetNode(99) != lookupNode || scene.GetComponent(124) != lookupCamera || scene.GetNodesWithTag("lookup").SingleOrDefault() != lookupNode)
            throw new InvalidOperationException("Scene did not preserve rbfx-style node/component lookup helpers.");
        if (!scene.GetAllNodes().ContainsKey(lookupNode.Id) || !scene.GetAllComponents().ContainsKey(lookupCamera.Id))
            throw new InvalidOperationException("Scene did not preserve rbfx-style full node/component maps.");
        if (typeof(Dictionary<string, List<int>>).GetFormattedTypeName() != "Dictionary<String, List<Int32>>")
            throw new InvalidOperationException("ObjectExtensionMethods did not preserve rbfx-style formatted type names.");
        if (ObjectReflection<StaticModel>.ClassName != nameof(StaticModel) || ObjectReflection<StaticModel>.TypeId.Value != StringHash.Calculate(nameof(StaticModel)))
            throw new InvalidOperationException("ObjectReflection did not preserve rbfx-style fallback type metadata.");
        if (new StringHash(typeof(StaticModel)).Value != StringHash.Calculate(nameof(StaticModel)) || ((StringHash)123u).Value != 123u)
            throw new InvalidOperationException("StringHash did not preserve rbfx-style type and uint conversions.");
        var materialRef = ResourceRef.Parse("Material;Materials/FirstCube");
        if (materialRef.Type != StringHash.Calculate("Material") || materialRef.Name != "Materials/FirstCube" || materialRef.ToHash() == 0u || ResourceRef.IsNullOrEmpty(materialRef))
            throw new InvalidOperationException("ResourceRef did not preserve rbfx-style parse and hash helpers.");
        var materialRefList = ResourceRefList.Parse("Material;Materials/One,Materials/Two");
        var expectedRefList = new ResourceRefList(StringHash.Calculate("Material"), ["Materials/One", "Materials/Two"]);
        if (!materialRefList.Equals(expectedRefList) || materialRefList.ToHash() == 0u || ResourceRefList.IsNullOrEmpty(materialRefList) || !ResourceRefList.IsNullOrEmpty(ResourceRefList.Empty))
            throw new InvalidOperationException("ResourceRefList did not preserve rbfx-style parse, equality, and hash helpers.");

        var explicitNode = scene.CreateChild("Explicit", 42);
        if (explicitNode.Id != 42 || explicitNode.NameHash.Value == 0)
            throw new InvalidOperationException("Node did not preserve explicit ID and name hash.");
        if (!explicitNode.SetAttribute("Name", "Renamed") || explicitNode.Name != "Renamed")
            throw new InvalidOperationException("Serializable node name attribute did not update the live property.");
        if (!explicitNode.SetAttribute("Position", new Vector3(1.0f, 2.0f, 3.0f)) || explicitNode.Position != new Vector3(1.0f, 2.0f, 3.0f))
            throw new InvalidOperationException("Serializable node position attribute did not update the live property.");
        explicitNode.SetTags(["editor", "dynamic"]);
        if (!explicitNode.HasTag("editor") || explicitNode.RemoveTag("dynamic") != true || explicitNode.HasTag("dynamic"))
            throw new InvalidOperationException("Node tags did not preserve rbfx-style tag operations.");
        explicitNode.SetVar("Health", 100);
        if (!Equals(explicitNode.GetVar("Health"), 100) || !explicitNode.RemoveVar("Health") || explicitNode.GetVar("Health") is not null)
            throw new InvalidOperationException("Node variables did not preserve rbfx-style variable operations.");
        explicitNode.SetTransform(new Vector3(1.0f, 2.0f, 3.0f), Quaternion.Identity, new Vector3(2.0f, 3.0f, 4.0f));
        var transformChild = explicitNode.CreateChild("TransformChild");
        transformChild.SetPosition(new Vector3(0.5f, 1.0f, 1.5f));
        var expectedWorld = new Vector3(2.0f, 5.0f, 9.0f);
        if (!transformChild.WorldPosition.ApproximatelyEquivalent(expectedWorld) || !explicitNode.WorldToLocal(expectedWorld).ApproximatelyEquivalent(transformChild.Position))
            throw new InvalidOperationException("Node transform conversion did not preserve rbfx-style local/world math.");
        var matrixParent = scene.CreateChild("MatrixParent");
        matrixParent.SetTransform(new Vector3(10.0f, 0.0f, 0.0f), Quaternion.Identity, new Vector3(2.0f, 2.0f, 2.0f));
        var matrixChild = matrixParent.CreateChild("MatrixChild");
        matrixChild.SetWorldTransform(new Matrix3x4(new Vector3(12.0f, 2.0f, 0.0f), Quaternion.Identity, new Vector3(4.0f, 4.0f, 4.0f)));
        if (!matrixChild.GetWorldPosition().ApproximatelyEquivalent(new Vector3(12.0f, 2.0f, 0.0f)) || !matrixChild.GetWorldScale().ApproximatelyEquivalent(new Vector3(4.0f, 4.0f, 4.0f)) || !matrixChild.GetPosition().ApproximatelyEquivalent(new Vector3(1.0f, 1.0f, 0.0f)))
            throw new InvalidOperationException("Node did not preserve rbfx-style matrix world transform helpers.");
        matrixChild.SetTransformMatrix(new Matrix3x4(new Vector3(1.0f, 2.0f, 3.0f), Quaternion.Identity, new Vector3(2.0f, 3.0f, 4.0f)));
        if (!matrixChild.GetPosition().ApproximatelyEquivalent(new Vector3(1.0f, 2.0f, 3.0f)) || !matrixChild.GetScale().ApproximatelyEquivalent(new Vector3(2.0f, 3.0f, 4.0f)))
            throw new InvalidOperationException("Node did not preserve rbfx-style matrix local transform helpers.");
        var rotateAroundNode = scene.CreateChild("RotateAround");
        rotateAroundNode.SetPosition(new Vector3(1.0f, 0.0f, 0.0f));
        rotateAroundNode.RotateAround(Vector3.Zero, Quaternion.FromAngleAxis(90.0f, Vector3.Forward), TransformSpace.Parent);
        if (!rotateAroundNode.GetPosition().ApproximatelyEquivalent(new Vector3(0.0f, 1.0f, 0.0f), MathDefs.LargeEpsilon))
            throw new InvalidOperationException("Node did not preserve rbfx-style rotate-around helpers.");
        var scaleAroundNode = scene.CreateChild("ScaleAround");
        scaleAroundNode.SetPosition(new Vector3(2.0f, 0.0f, 0.0f));
        scaleAroundNode.ScaleBy(new Vector3(2.0f, 3.0f, 4.0f));
        scaleAroundNode.ScaleAround(Vector3.Zero, new Vector3(2.0f, 1.0f, 1.0f), TransformSpace.Parent);
        if (!scaleAroundNode.GetPosition().ApproximatelyEquivalent(new Vector3(4.0f, 0.0f, 0.0f)) || !scaleAroundNode.GetScale().ApproximatelyEquivalent(new Vector3(4.0f, 3.0f, 4.0f)))
            throw new InvalidOperationException("Node did not preserve rbfx-style scale modifier helpers.");
        var newParent = scene.CreateChild("ReparentTarget");
        newParent.SetPosition(new Vector3(-1.0f, -2.0f, -3.0f));
        transformChild.SetParent(newParent);
        if (!ReferenceEquals(transformChild.Parent, newParent) || !transformChild.WorldPosition.ApproximatelyEquivalent(expectedWorld) || explicitNode.GetChild("TransformChild") is not null)
            throw new InvalidOperationException("Node reparenting did not preserve rbfx-style parent and world transform semantics.");
        newParent.RemoveChild(transformChild);
        if (transformChild.Parent is not null || newParent.GetNumChildren() != 0)
            throw new InvalidOperationException("Node child removal did not preserve rbfx-style detach semantics.");
        var removableParent = scene.CreateChild("RemovableParent");
        removableParent.CreateChild("Nested");
        scene.Root.RemoveChildren(recursive: false);
        if (scene.Root.GetNumChildren() != 0 || removableParent.Parent is not null)
            throw new InvalidOperationException("Node RemoveChildren did not preserve rbfx-style child removal semantics.");
        var hierarchyParent = scene.CreateChild("HierarchyParent");
        var hierarchyFirst = hierarchyParent.CreateChild("First");
        var hierarchySecond = hierarchyParent.CreateChild("Second");
        var hierarchyGrandchild = hierarchySecond.CreateChild("Grandchild");
        hierarchyFirst.AddTag("selectable");
        hierarchyGrandchild.CreateComponent<StaticModel>();
        if (!ReferenceEquals(hierarchyParent.GetDirectChildFor(hierarchyGrandchild), hierarchySecond) || !hierarchyGrandchild.IsChildOf(hierarchyParent))
            throw new InvalidOperationException("Node did not preserve rbfx-style direct-child hierarchy helpers.");
        if (hierarchyParent.GetChildrenWithTag("selectable").SingleOrDefault() != hierarchyFirst || hierarchyParent.GetChildrenWithComponent<StaticModel>(recursive: true).SingleOrDefault() != hierarchyGrandchild)
            throw new InvalidOperationException("Node did not preserve rbfx-style child filtering helpers.");
        hierarchyParent.ReorderChild(hierarchySecond, 0);
        if (hierarchyParent.GetChild(0) != hierarchySecond || hierarchySecond.GetIndexInParent() != 0)
            throw new InvalidOperationException("Node did not preserve rbfx-style child reordering helpers.");
        var reorderCamera = hierarchyParent.CreateComponent<Camera>();
        var reorderModel = hierarchyParent.CreateComponent<StaticModel>();
        hierarchyParent.ReorderComponent(reorderModel, 0);
        if (reorderModel.GetIndexInParent() != 0 || reorderCamera.GetIndexInParent() != 1)
            throw new InvalidOperationException("Node did not preserve rbfx-style component reordering helpers.");
        if (hierarchyParent.GetNumComponents() != 2 || hierarchyParent.GetComponent(0) != reorderModel || hierarchyParent.GetNthComponent<Camera>(0) != reorderCamera || !hierarchyParent.HasComponent<StaticModel>())
            throw new InvalidOperationException("Node did not preserve rbfx-style component query helpers.");
        var cloneComponentTarget = scene.CreateChild("CloneComponentTarget");
        reorderCamera.SetFov(75.0f);
        var clonedCameraComponent = cloneComponentTarget.CloneComponent(reorderCamera) as Camera;
        if (clonedCameraComponent is null || clonedCameraComponent.Id == reorderCamera.Id || !MathDefs.ApproximatelyEquivalent(clonedCameraComponent.GetFov(), 75.0f) || !ReferenceEquals(clonedCameraComponent.Node, cloneComponentTarget))
            throw new InvalidOperationException("Node did not preserve rbfx-style component cloning helpers.");
        var cloneSource = scene.CreateChild("CloneSource");
        cloneSource.SetTags(["cloneable"]);
        cloneSource.SetVar("Score", 5);
        cloneSource.SetTransform(new Vector3(3.0f, 4.0f, 5.0f), Quaternion.Identity, new Vector3(2.0f, 2.0f, 2.0f));
        var cloneSourceCamera = cloneSource.CreateComponent<Camera>();
        cloneSourceCamera.SetFov(65.0f);
        cloneSource.CreateComponent<Octree>().SetTemporary(true);
        var cloneChild = cloneSource.CreateChild("CloneChild");
        cloneChild.SetPosition(new Vector3(1.0f, 0.0f, 0.0f));
        cloneSource.CreateTemporaryChild("TemporaryCloneChild");
        var cloneTarget = scene.CreateChild("CloneTarget");
        var clonedNode = cloneSource.Clone(cloneTarget);
        if (clonedNode is null || clonedNode.Parent != cloneTarget || clonedNode.Id == cloneSource.Id || clonedNode.Name != "CloneSource" || !clonedNode.HasTag("cloneable") || !Equals(clonedNode.GetVar("Score"), 5))
            throw new InvalidOperationException("Node did not preserve rbfx-style node cloning identity helpers.");
        if (!clonedNode.Position.ApproximatelyEquivalent(cloneSource.Position) || clonedNode.GetChild("CloneChild") is null || clonedNode.GetChild("TemporaryCloneChild") is not null)
            throw new InvalidOperationException("Node did not preserve rbfx-style recursive clone helpers.");
        if (clonedNode.GetComponent<Camera>()?.GetFov() != 65.0f || clonedNode.HasComponent<Octree>())
            throw new InvalidOperationException("Node did not preserve rbfx-style cloned component filtering helpers.");
        if (Node.GetNodes([cloneSourceCamera, clonedCameraComponent]).Count != 2 || Node.GetParentNodes([cloneSource, cloneChild]).SingleOrDefault() != cloneSource)
            throw new InvalidOperationException("Node did not preserve rbfx-style static node collection helpers.");
        var temporaryParent = scene.CreateChild("TemporaryParent", temporary: true);
        if (!temporaryParent.CreateChild("TemporaryChild").IsTemporaryEffective() || !scene.Root.IsTransformHierarchyRoot())
            throw new InvalidOperationException("Node did not preserve rbfx-style effective temporary or hierarchy-root helpers.");
        var rect = new global::RedBolt.Rect();
        rect.Merge(new Vector2(0.0f, 0.0f));
        rect.Merge(new Vector2(2.0f, 2.0f));
        if (rect.IsInside(new Vector2(1.0f, 1.0f)) != Intersection.Inside || rect.Size != new Vector2(2.0f, 2.0f))
            throw new InvalidOperationException("Rect did not preserve rbfx-style merge and containment math.");
        var intRect = new global::RedBolt.IntRect(0, 0, 10, 10);
        if (intRect.IsInside(new IntVector2(9, 9)) != Intersection.Inside || intRect.IsInside(new IntVector2(10, 10)) != Intersection.Outside)
            throw new InvalidOperationException("IntRect did not preserve rbfx-style containment math.");
        var color = new global::RedBolt.Color(0.25f, 0.5f, 1.0f, 1.0f);
        if (color.ToVector4() != new Vector4(0.25f, 0.5f, 1.0f, 1.0f) || color.ToUInt() == 0)
            throw new InvalidOperationException("Color did not preserve rbfx-style vector and packed conversion math.");
        var box = new global::RedBolt.BoundingBox(new Vector3(-1.0f, -1.0f, -1.0f), new Vector3(1.0f, 1.0f, 1.0f));
        var sphere = new global::RedBolt.Sphere(Vector3.Zero, 1.0f);
        if (box.IsInside(Vector3.Zero) != Intersection.Inside || box.IsInside(sphere) != Intersection.Inside)
            throw new InvalidOperationException("BoundingBox/Sphere did not preserve rbfx-style containment math.");
        var ray = new global::RedBolt.Ray(new Vector3(0.0f, 0.0f, -5.0f), Vector3.Forward);
        if (!MathDefs.ApproximatelyEquivalent(ray.HitDistance(box), 4.0f) || !MathDefs.ApproximatelyEquivalent(ray.HitDistance(sphere), 4.0f))
            throw new InvalidOperationException("Ray did not preserve rbfx-style hit distance math.");
        var plane = new global::RedBolt.Plane(Vector3.Up, Vector3.Zero);
        var downRay = new global::RedBolt.Ray(new Vector3(0.0f, 1.0f, 0.0f), Vector3.Down);
        if (!MathDefs.ApproximatelyEquivalent(downRay.HitDistance(plane), 1.0f))
            throw new InvalidOperationException("Plane did not preserve rbfx-style ray intersection math.");
        var transformMatrix = new Matrix3x4(new Vector3(1.0f, 2.0f, 3.0f), Quaternion.Identity, new Vector3(2.0f, 3.0f, 4.0f)).Matrix4;
        var transformedPoint = transformMatrix * Vector3.One;
        if (!transformedPoint.ApproximatelyEquivalent(new Vector3(3.0f, 5.0f, 7.0f)) || !(transformMatrix.Inverted * transformedPoint).ApproximatelyEquivalent(Vector3.One))
            throw new InvalidOperationException("Matrix4 did not preserve rbfx-style transform and inverse math.");
        var movedPlane = plane.Transformed(new Matrix3x4(new Vector3(0.0f, 2.0f, 0.0f), Quaternion.Identity, 1.0f));
        if (!MathDefs.ApproximatelyEquivalent(movedPlane.Distance(new Vector3(0.0f, 2.0f, 0.0f)), 0.0f))
            throw new InvalidOperationException("Plane did not preserve rbfx-style Matrix3x4 transform math.");
        var matrix2 = new Matrix2(2.0f, 0.0f, 0.0f, 4.0f);
        if (!((matrix2 * matrix2.Inverse) * Vector2.One).ApproximatelyEquivalent(Vector2.One))
            throw new InvalidOperationException("Matrix2 did not preserve rbfx-style inverse math.");
        if (!MathDefs.ApproximatelyEquivalent(EaseMath.BounceInOut(0.0f), 0.0f) || !MathDefs.ApproximatelyEquivalent(EaseMath.BounceInOut(1.0f), 1.0f))
            throw new InvalidOperationException("EaseMath did not preserve rbfx-style easing endpoints.");
        if (!MathDefs.ApproximatelyEquivalent(MathDefs.InverseLerp(10.0f, 20.0f, 15.0f), 0.5f) || !MathDefs.ApproximatelyEquivalent(MathDefs.SmoothStep(0.0f, 1.0f, 0.5f), 0.5f) || !MathDefs.ApproximatelyEquivalent(MathDefs.Smooth(0.0f, 10.0f, 1.0f), 5.0f))
            throw new InvalidOperationException("MathDefs did not preserve rbfx-style interpolation helpers.");
        if (!MathDefs.ApproximatelyEquivalent(MathDefs.Sin(90.0f), 1.0f) || !MathDefs.ApproximatelyEquivalent(MathDefs.Cos(180.0f), -1.0f) || !MathDefs.ApproximatelyEquivalent(MathDefs.Atan2(1.0f, 0.0f), 90.0f))
            throw new InvalidOperationException("MathDefs did not preserve rbfx-style degree trigonometry helpers.");
        if (MathDefs.AbsMod(-1, 10) != 9 || !MathDefs.ApproximatelyEquivalent(MathDefs.SnapRound(2.25f, 0.5f), 2.5f) || !MathDefs.ApproximatelyEquivalent(MathDefs.RoundToNearestMultiple(-7.0f, 4.0f), -8.0f))
            throw new InvalidOperationException("MathDefs did not preserve rbfx-style modulo and snapping helpers.");
        if (!MathDefs.IsPowerOfTwo(8u) || MathDefs.IsPowerOfTwo(0u) || MathDefs.NextPowerOfTwo(5u) != 8u || MathDefs.ClosestPowerOfTwo(6u) != 4u || MathDefs.LogBaseTwo(16u) != 4u || MathDefs.CountSetBits(0b1011u) != 3u)
            throw new InvalidOperationException("MathDefs did not preserve rbfx-style bit helpers.");
        if (!MathDefs.ApproximatelyEquivalent(MathDefs.ExponentialDecay(1.0f), 0.5f) || !MathDefs.ApproximatelyEquivalent(MathDefs.Wrap(12.0f, 0.0f, 10.0f), 2.0f) || MathDefs.SDBMHash(0u, (byte)'A') != 65u)
            throw new InvalidOperationException("MathDefs did not preserve rbfx-style scalar utility helpers.");
        if (MathDefs.FloatToHalf(1.0f) != 0x3c00 || !MathDefs.ApproximatelyEquivalent(MathDefs.HalfToFloat(0x3c00), 1.0f) || MathDefs.FloatToHalf(0.0f) != 0 || !MathDefs.ApproximatelyEquivalent(MathDefs.HalfToFloat(0), 0.0f))
            throw new InvalidOperationException("MathDefs did not preserve rbfx-style half-float conversion helpers.");
        var seededRandom = new RandomEngine(1u);
        if (RandomEngine.MaxRange() != 2147483646u || seededRandom.Save() != "1" || seededRandom.GetUInt() != 48270u)
            throw new InvalidOperationException("RandomEngine did not preserve rbfx-style deterministic seed helpers.");
        var randomState = new RandomEngine(7u).Save();
        var randomA = new RandomEngine(randomState);
        var randomB = new RandomEngine(randomState);
        if (randomA.GetUInt() != randomB.GetUInt() || randomA.GetUInt(10u) >= 10u || randomA.GetUInt(5u, 7u) is < 5u or >= 7u || randomA.GetInt(-3, 3) is < -3 or >= 3)
            throw new InvalidOperationException("RandomEngine did not preserve rbfx-style bounded integer helpers.");
        if (randomA.GetBool(0.0f) || !randomA.GetBool(1.0f) || randomA.GetFloat(-1.0f, 1.0f) is < -1.0f or > 1.0f)
            throw new InvalidOperationException("RandomEngine did not preserve rbfx-style scalar random helpers.");
        var randomDirection = new RandomEngine(2u).GetDirectionVector3();
        var randomQuaternion = new RandomEngine(3u).GetQuaternion();
        var randomVector = new RandomEngine(4u).GetVector3(new BoundingBox(Vector3.Zero, Vector3.One));
        if (!MathDefs.ApproximatelyEquivalent(randomDirection.Length, 1.0f, MathDefs.LargeEpsilon) || !MathDefs.ApproximatelyEquivalent(randomQuaternion.Length, 1.0f, MathDefs.LargeEpsilon) || randomVector.X is < 0.0f or > 1.0f || randomVector.Y is < 0.0f or > 1.0f || randomVector.Z is < 0.0f or > 1.0f)
            throw new InvalidOperationException("RandomEngine did not preserve rbfx-style vector random helpers.");
        MathDefs.SetRandomSeed(1u);
        if (MathDefs.GetRandomSeed() != 1u || MathDefs.Rand() != 41 || MathDefs.Random(3) is < 0 or >= 3 || !float.IsFinite(MathDefs.RandomNormal(0.0f, 1.0f)))
            throw new InvalidOperationException("MathDefs did not preserve rbfx-style global random helpers.");
        if (!scene.CreateChild("Temporary", temporary: true).IsTemporary)
            throw new InvalidOperationException("Temporary child node did not preserve the temporary flag.");

        var childCamera = explicitNode.CreateChild("Child").CreateComponent<Camera>();
        if (!ReferenceEquals(explicitNode.FindComponent<Camera>(), childCamera))
            throw new InvalidOperationException("Default component search did not include child nodes recursively.");

        var cameraNode = scene.CreateChild("Camera");
        var camera = cameraNode.CreateComponent<Camera>(123);
        if (camera.Id != 123 || camera.GetIndexInParent() != 0)
            throw new InvalidOperationException("Component did not preserve explicit ID or parent index.");
        if (!camera.SetAttribute("Is Enabled", false) || camera.Enabled || camera.IsEnabledEffective)
            throw new InvalidOperationException("Serializable component enabled attribute did not update the live property.");
        camera.SetEnabled(true);
        camera.SetNearClip(-1.0f);
        camera.SetFarClip(200.0f);
        camera.SetFov(200.0f);
        camera.SetAspectRatio(2.0f);
        camera.SetOrthoSize(new Vector2(40.0f, 20.0f));
        camera.SetZoom(0.0f);
        camera.SetLodBias(0.0f);
        camera.SetViewMask(0x0fu);
        camera.SetZoneMask(0xf0u);
        camera.SetViewOverrideFlags(ViewOverride.DisableShadows | ViewOverride.DisableOcclusion);
        camera.SetProjectionOffset(new Vector2(0.1f, 0.2f));
        camera.SetMousePosition(new Vector2(0.5f, 0.25f));
        camera.SetOrthographic(true);
        camera.SetFillMode(RedBolt.FillMode.Point);
        if (!MathDefs.ApproximatelyEquivalent(camera.GetNearClip(), MathDefs.MinNearClip) || !MathDefs.ApproximatelyEquivalent(camera.GetFarClip(), 200.0f) || !MathDefs.ApproximatelyEquivalent(camera.GetFov(), 160.0f))
            throw new InvalidOperationException("Camera did not preserve rbfx-style clip and FOV helpers.");
        if (camera.GetAutoAspectRatio() || !MathDefs.ApproximatelyEquivalent(camera.GetAspectRatio(), 2.0f) || !MathDefs.ApproximatelyEquivalent(camera.GetOrthoSize(), 20.0f))
            throw new InvalidOperationException("Camera did not preserve rbfx-style aspect and ortho-size helpers.");
        if (!MathDefs.ApproximatelyEquivalent(camera.GetZoom(), MathDefs.Epsilon) || !MathDefs.ApproximatelyEquivalent(camera.GetLodBias(), MathDefs.Epsilon))
            throw new InvalidOperationException("Camera did not preserve rbfx-style zoom and LOD bias clamping.");
        if (camera.GetViewMask() != 0x0fu || camera.GetZoneMask() != 0xf0u || camera.GetViewOverrideFlags() != (ViewOverride.DisableShadows | ViewOverride.DisableOcclusion))
            throw new InvalidOperationException("Camera did not preserve rbfx-style view mask helpers.");
        if (!camera.GetProjectionOffset().ApproximatelyEquivalent(new Vector2(0.1f, 0.2f)) || !camera.HasMousePosition() || !camera.IsOrthographic())
            throw new InvalidOperationException("Camera did not preserve rbfx-style projection, mouse, or orthographic helpers.");
        if (camera.GetFillMode() != RedBolt.FillMode.Point || !MathDefs.ApproximatelyEquivalent(camera.GetDistance(new Vector3(0.0f, 0.0f, 5.0f)), 5.0f) || camera.GetLodDistance(100.0f, 1.0f, 1.0f) <= 0.0f)
            throw new InvalidOperationException("Camera did not preserve rbfx-style distance helpers.");
        var viewport = new global::RedBolt.Viewport(scene, camera, new IntRect(0, 0, 320, 200));
        viewport.SetDrawDebug(false);
        viewport.SetCullCamera(childCamera);
        viewport.SetEye(childCamera, 1);
        if (viewport.GetRect() != new IntRect(0, 0, 320, 200) || viewport.GetDrawDebug() || viewport.GetCullCamera() != childCamera || !viewport.IsStereo() || viewport.GetEye(1) != childCamera)
            throw new InvalidOperationException("Viewport did not preserve rbfx-style managed state helpers.");
        Renderer.SetNumViewports(2);
        Renderer.SetViewport(1, viewport);
        Renderer.SetViewport(0, viewport);
        Renderer.SetTextureAnisotropy(0);
        Renderer.SetTextureFilterMode(global::RedBolt.TextureFilterMode.Trilinear);
        Renderer.SetTextureQuality(MaterialQuality.Medium);
        Renderer.SetSkinningMode(global::RedBolt.SkinningMode.Hardware);
        Renderer.SetNumSoftwareSkinningBones(24);
        if (Renderer.GetNumViewports() != 2 || Renderer.GetNumViews() != 2 || Renderer.GetViewportForScene(scene, 1) != viewport)
            throw new InvalidOperationException("Renderer did not preserve rbfx-style viewport helpers.");
        if (Renderer.GetTextureAnisotropy() != 1 || Renderer.GetTextureFilterMode() != global::RedBolt.TextureFilterMode.Trilinear || Renderer.GetTextureQuality() != MaterialQuality.Medium)
            throw new InvalidOperationException("Renderer did not preserve rbfx-style texture quality helpers.");
        if (!Renderer.GetUseHardwareSkinning() || Renderer.GetSkinningMode() != global::RedBolt.SkinningMode.Hardware || Renderer.GetNumSoftwareSkinningBones() != 24)
            throw new InvalidOperationException("Renderer did not preserve rbfx-style skinning helpers.");
        var vertexBuffer = new VertexBuffer();
        vertexBuffer.SetSize(4, 12, dynamic: true);
        vertexBuffer.SetShadowed(true);
        vertexBuffer.SetData(Enumerable.Range(0, 48).Select(static value => (byte)value));
        var indexBuffer = new IndexBuffer();
        indexBuffer.SetSize(6, largeIndices: false, dynamic: true);
        indexBuffer.SetShadowed(true);
        indexBuffer.SetUnpackedData([0u, 1u, 2u, 2u, 3u, 0u]);
        if (!indexBuffer.GetUsedVertexRange(0, 6, out var minVertex, out var usedVertexCount) || minVertex != 0 || usedVertexCount != 4 || IndexBuffer.GetIndexBufferType(indexBuffer) != IndexBufferType.UInt16)
            throw new InvalidOperationException("IndexBuffer did not preserve rbfx-style CPU-side metadata helpers.");
        var geometry = new Geometry();
        geometry.SetVertexBuffers([vertexBuffer]);
        geometry.SetIndexBuffer(indexBuffer);
        geometry.SetDrawRange(PrimitiveType.TriangleList, 0, 6, 0, 4);
        geometry.SetLodDistance(5.0f);
        if (vertexBuffer.GetVertexCount() != 4 || vertexBuffer.GetVertexSize() != 12 || !vertexBuffer.Shadowed || vertexBuffer.GetData().Count != 48)
            throw new InvalidOperationException("VertexBuffer did not preserve rbfx-style CPU-side metadata helpers.");
        if (geometry.GetPrimitiveCount() != 2 || geometry.GetNumVertexBuffers() != 1 || !geometry.IsInstanced(GeometryType.Static) || !MathDefs.ApproximatelyEquivalent(geometry.GetLodDistance(), 5.0f))
            throw new InvalidOperationException("Geometry did not preserve rbfx-style CPU-side metadata helpers.");
        var cubeNode = scene.CreateChild("Cube");
        var cube = cubeNode.CreateComponent<StaticModel>();
        cube.SetModel(model);
        cube.SetMaterial(material);
        cube.SetDrawDistance(-1.0f);
        cube.SetShadowDistance(50.0f);
        cube.SetLodBias(0.0f);
        cube.SetViewMask(0x11u);
        cube.SetLightMask(0x22u);
        cube.SetShadowMask(0x33u);
        cube.SetZoneMask(0x44u);
        cube.SetCastShadows(true);
        cube.SetOccluder(false);
        cube.SetOccludee(false);
        cube.SetGlobalIlluminationType(GlobalIlluminationType.BlendLightProbes);
        cube.SetReflectionMode(ReflectionMode.BlendProbesAndZone);
        if (cube.GetDrawableFlags() != DrawableFlags.Geometry || !MathDefs.ApproximatelyEquivalent(cube.GetDrawDistance(), 0.0f) || !MathDefs.ApproximatelyEquivalent(cube.GetShadowDistance(), 50.0f) || !MathDefs.ApproximatelyEquivalent(cube.GetLodBias(), MathDefs.Epsilon))
            throw new InvalidOperationException("Drawable did not preserve rbfx-style distance and flag helpers.");
        if (cube.GetViewMask() != 0x11u || cube.GetLightMask() != 0x22u || cube.GetShadowMask() != 0x33u || cube.GetZoneMask() != 0x44u || !cube.GetCastShadows() || cube.IsOccluder() || cube.IsOccludee())
            throw new InvalidOperationException("Drawable did not preserve rbfx-style mask and visibility helpers.");
        if (cube.GetGlobalIlluminationType() != GlobalIlluminationType.BlendLightProbes || cube.GetReflectionMode() != ReflectionMode.BlendProbesAndZone || Drawable.PortableLightMask != 0x0fu || Drawable.MaxVertexLights != 4)
            throw new InvalidOperationException("Drawable did not preserve rbfx-style lighting metadata helpers.");
        cube.SetOcclusionLodLevel(2u);
        cube.SetBakeLightmap(true);
        cube.SetScaleInLightmap(0.0f);
        if (cube.GetBakeLightmapEffective())
            throw new InvalidOperationException("StaticModel did not preserve rbfx-style effective lightmap scale helpers.");
        cube.SetScaleInLightmap(2.0f);
        cube.SetLightmapIndex(3u);
        cube.SetLightmapScaleOffset(new Vector4(1.0f, 2.0f, 3.0f, 4.0f));
        if (cube.GetModel() != model || cube.GetNumGeometries() != 1u || cube.GetOcclusionLodLevel() != 2u || !cube.GetBakeLightmap() || !cube.GetBakeLightmapEffective())
            throw new InvalidOperationException("StaticModel did not preserve rbfx-style model and lightmap helpers.");
        if (!MathDefs.ApproximatelyEquivalent(cube.GetScaleInLightmap(), 2.0f) || cube.GetLightmapIndex() != 3u || cube.GetLightmapScaleOffset() != new Vector4(1.0f, 2.0f, 3.0f, 4.0f))
            throw new InvalidOperationException("StaticModel did not preserve rbfx-style lightmap metadata helpers.");
        if (model.GetNumGeometries() != 1u || model.GetGeometries().Count != 1 || model.GetGeometryCenters().Count != 1)
            throw new InvalidOperationException("Model did not preserve rbfx-style geometry collection helpers.");
        if (cube.NumGeometries != 1 || model.GetGeometry(0)?.VertexCount != 36u)
            throw new InvalidOperationException("StaticModel did not expose the procedural model geometry.");
        if (!ReferenceEquals(cube.GetMaterial(), material))
            throw new InvalidOperationException("StaticModel did not expose the assigned material.");
        if (model.GetBoundingBox().IsInside(Vector3.Zero) != Intersection.Inside || !cube.IsInsideLocal(Vector3.Zero))
            throw new InvalidOperationException("Model/StaticModel did not preserve rbfx-style local bounding boxes.");
        cubeNode.SetTransform(new Vector3(10.0f, 0.0f, 0.0f), Quaternion.Identity, new Vector3(2.0f, 2.0f, 2.0f));
        if (cube.GetWorldBoundingBox().IsInside(new Vector3(10.5f, 0.0f, 0.0f)) != Intersection.Inside || !cube.IsInside(new Vector3(10.5f, 0.0f, 0.0f)))
            throw new InvalidOperationException("Drawable/StaticModel did not preserve rbfx-style world bounding boxes.");
        octree.SetSize(new BoundingBox(-100.0f, 100.0f), 4u);
        octree.QueueUpdate(cube);
        octree.Update();
        if (octree.GetNumLevels() != 4u || octree.GetRootOctant().GetWorldBoundingBox().IsInside(cube.GetWorldBoundingBox()) == Intersection.Outside || !octree.GetAllDrawables().Contains(cube))
            throw new InvalidOperationException("Octree did not preserve rbfx-style size and drawable collection helpers.");
        if (!octree.GetDrawables(cube.GetWorldBoundingBox()).Contains(cube) || !octree.GetDrawables(cube.GetWorldBoundingBox().Center).Contains(cube) || !octree.GetDrawables(new Sphere(cube.GetWorldBoundingBox())).Contains(cube))
            throw new InvalidOperationException("Octree did not preserve rbfx-style bounding volume query helpers.");
        var cubeRay = new Ray(cube.GetWorldBoundingBox().Center - Vector3.Forward * 10.0f, Vector3.Forward);
        var cubeHit = octree.RaycastSingle(cubeRay, maxDistance: 100.0f);
        if (cubeHit?.Drawable != cube || cubeHit.Node != cubeNode || !octree.Raycast(cubeRay, maxDistance: 100.0f).Any(result => result.Drawable == cube))
            throw new InvalidOperationException("Octree did not preserve rbfx-style raycast helpers.");
        var manualDrawable = new StaticModel();
        manualDrawable.SetBoundingBox(new BoundingBox(new Vector3(20.0f, 20.0f, 20.0f), new Vector3(21.0f, 21.0f, 21.0f)));
        octree.AddManualDrawable(manualDrawable);
        if (!octree.GetAllDrawables().Contains(manualDrawable))
            throw new InvalidOperationException("Octree did not preserve rbfx-style manual drawable addition helpers.");
        octree.RemoveManualDrawable(manualDrawable);
        if (octree.GetAllDrawables().Contains(manualDrawable))
            throw new InvalidOperationException("Octree did not preserve rbfx-style manual drawable removal helpers.");
        octree.SetLocked(true);
        octree.AddManualDrawable(manualDrawable);
        if (!octree.GetLocked() || octree.GetAllDrawables().Contains(manualDrawable))
            throw new InvalidOperationException("Octree did not preserve rbfx-style lock helpers.");
        octree.SetLocked(false);
        var lightNode = scene.CreateChild("Light");
        var light = lightNode.CreateComponent<Light>();
        light.SetColor(new Color(0.5f, 0.25f, 0.125f));
        light.SetBrightness(2.0f);
        light.SetSpecularIntensity(-1.0f);
        light.SetRange(-1.0f);
        light.SetFov(200.0f);
        light.SetAspectRatio(0.0f);
        light.SetShadowBias(new BiasParameters(2.0f, 20.0f, -1.0f));
        light.SetShadowCascade(new CascadeParameters(-1.0f, 5.0f, 10.0f, 0.0f, -1.0f, 2.0f));
        light.SetShadowFocus(new FocusParameters(true, false, false, 0.0f, 0.0f));
        light.SetShadowIntensity(2.0f);
        light.SetShadowResolution(0.0f);
        light.SetShadowNearFarRatio(1.0f);
        light.SetShadowMaxExtrusion(-1.0f);
        light.SetLightImportance(LightImportance.NotImportant);
        light.SetLightMode(LightMode.Baked);
        if (light.GetDrawableFlags() != DrawableFlags.Light || light.GetLightType() != LightType.Point || light.GetLightImportance() != LightImportance.NotImportant || light.GetLightMode() != LightMode.Baked)
            throw new InvalidOperationException("Light did not preserve rbfx-style type, mode, or importance helpers.");
        if (light.GetLightMaskEffective() != 0u || !light.GetPerVertex() || !MathDefs.ApproximatelyEquivalent(light.GetRange(), 0.0f) || !MathDefs.ApproximatelyEquivalent(light.GetFov(), MathDefs.MaxFov))
            throw new InvalidOperationException("Light did not preserve rbfx-style effective mask or range/FOV clamping helpers.");
        if (!light.GetEffectiveColor().ApproximatelyEquivalent(new Color(1.0f, 0.5f, 0.25f, 1.0f)) || !MathDefs.ApproximatelyEquivalent(light.GetEffectiveSpecularIntensity(), 0.0f))
            throw new InvalidOperationException("Light did not preserve rbfx-style effective color or specular helpers.");
        if (!MathDefs.ApproximatelyEquivalent(light.GetAspectRatio(), MathDefs.Epsilon) || light.GetShadowBias() != new BiasParameters(1.0f, 16.0f, 0.0f))
            throw new InvalidOperationException("Light did not preserve rbfx-style aspect ratio or shadow bias validation helpers.");
        if (light.GetNumShadowSplits() != 2 || !MathDefs.ApproximatelyEquivalent(light.GetShadowCascade().FadeStart, MathDefs.Epsilon) || !MathDefs.ApproximatelyEquivalent(light.GetShadowFocus().Quantize, Light.ShadowMinQuantize))
            throw new InvalidOperationException("Light did not preserve rbfx-style shadow cascade or focus validation helpers.");
        if (!MathDefs.ApproximatelyEquivalent(light.GetShadowIntensity(), 1.0f) || !MathDefs.ApproximatelyEquivalent(light.GetShadowResolution(), 0.125f) || !MathDefs.ApproximatelyEquivalent(light.GetShadowNearFarRatio(), 0.5f) || !MathDefs.ApproximatelyEquivalent(light.GetShadowMaxExtrusion(), 0.0f))
            throw new InvalidOperationException("Light did not preserve rbfx-style shadow scalar validation helpers.");
        light.SetLightMode(LightMode.Realtime);
        light.SetRange(100.0f);
        if (light.GetLightMaskEffective() != light.GetLightMask() || light.GetDistanceTo(cube) <= 0.0f)
            throw new InvalidOperationException("Light did not preserve rbfx-style realtime mask or drawable distance helpers.");
        light.SetLightType(LightType.Directional);
        if (light.GetDistanceTo(cube) != 0.0f || light.GetWorldBoundingBox().IsInside(Vector3.Zero) == Intersection.Outside)
            throw new InvalidOperationException("Light did not preserve rbfx-style directional bounds or distance helpers.");
        var zoneNode = scene.CreateChild("Zone");
        var zone = zoneNode.CreateComponent<Zone>();
        if (zone.GetDrawableFlags() != DrawableFlags.Zone || zone.GetAmbientColor() != Zone.DefaultAmbientColor || zone.GetFogColor() != Zone.DefaultFogColor || !MathDefs.ApproximatelyEquivalent(zone.GetFogStart(), Zone.DefaultFogStart))
            throw new InvalidOperationException("Zone did not preserve rbfx-style default render state helpers.");
        zone.SetBoundingBox(new BoundingBox(new Vector3(-1.0f, -2.0f, -3.0f), new Vector3(1.0f, 2.0f, 3.0f)));
        zone.SetAmbientColor(new Color(0.2f, 0.3f, 0.4f));
        zone.SetAmbientBrightness(2.0f);
        zone.SetBackgroundBrightness(3.0f);
        zone.SetBackgroundStatic(true);
        zone.SetFogColor(new Color(0.5f, 0.6f, 0.7f));
        zone.SetFogStart(-1.0f);
        zone.SetFogEnd(-1.0f);
        zone.SetFogHeight(4.0f);
        zone.SetFogHeightScale(5.0f);
        zone.SetPriority(7);
        zone.SetHeightFog(true);
        zone.SetOverride(true);
        zone.SetAmbientGradient(true);
        zone.SetZoneTexture(material);
        var zoneTextureRef = ResourceRef.Parse("TextureCube;Textures/Sky.xml");
        zone.SetZoneTextureAttr(zoneTextureRef);
        if (!zone.GetBoundingBox().Equals(new BoundingBox(new Vector3(-1.0f, -2.0f, -3.0f), new Vector3(1.0f, 2.0f, 3.0f))) || zone.GetAmbientColor() != new Color(0.2f, 0.3f, 0.4f))
            throw new InvalidOperationException("Zone did not preserve rbfx-style bounding box or ambient color helpers.");
        if (!MathDefs.ApproximatelyEquivalent(zone.GetAmbientBrightness(), 2.0f) || !MathDefs.ApproximatelyEquivalent(zone.GetBackgroundBrightness(), 3.0f) || !zone.IsBackgroundStatic())
            throw new InvalidOperationException("Zone did not preserve rbfx-style ambient/background brightness helpers.");
        if (zone.GetFogColor() != new Color(0.5f, 0.6f, 0.7f) || !MathDefs.ApproximatelyEquivalent(zone.GetFogStart(), 0.0f) || !MathDefs.ApproximatelyEquivalent(zone.GetFogEnd(), 0.0f))
            throw new InvalidOperationException("Zone did not preserve rbfx-style fog color or distance clamping helpers.");
        if (!MathDefs.ApproximatelyEquivalent(zone.GetFogHeight(), 4.0f) || !MathDefs.ApproximatelyEquivalent(zone.GetFogHeightScale(), 5.0f) || zone.GetPriority() != 7)
            throw new InvalidOperationException("Zone did not preserve rbfx-style fog height or priority helpers.");
        if (!zone.GetHeightFog() || !zone.GetOverride() || !zone.GetAmbientGradient() || !ReferenceEquals(zone.GetZoneTexture(), material) || zone.GetZoneTextureAttr() != zoneTextureRef)
            throw new InvalidOperationException("Zone did not preserve rbfx-style mode flags or zone texture metadata helpers.");
        if (!zone.GetAmbientStartColor().ApproximatelyEquivalent(zone.GetAmbientColor()) || !zone.GetAmbientEndColor().ApproximatelyEquivalent(zone.GetAmbientColor()) || !zone.GetAmbientLighting().ApproximatelyEquivalent(new Vector3(0.4f, 0.6f, 0.8f)))
            throw new InvalidOperationException("Zone did not preserve rbfx-style ambient gradient fallback or lighting helpers.");
        zoneNode.SetTransform(new Vector3(5.0f, 0.0f, 0.0f), Quaternion.Identity, new Vector3(2.0f, 1.0f, 1.0f));
        if (!zone.IsInside(new Vector3(6.0f, 0.0f, 0.0f)) || zone.IsInside(new Vector3(8.0f, 0.0f, 0.0f)))
            throw new InvalidOperationException("Zone did not preserve rbfx-style oriented containment helpers.");
        if (!(zone.GetInverseWorldTransform() * new Vector3(6.0f, 0.0f, 0.0f)).ApproximatelyEquivalent(new Vector3(0.5f, 0.0f, 0.0f)))
            throw new InvalidOperationException("Zone did not preserve rbfx-style inverse world transform helpers.");
        var skybox = scene.CreateChild("Skybox").CreateComponent<Skybox>();
        skybox.SetModel(model);
        skybox.SetMaterial(material);
        if (skybox.GetReflectionMode() != ReflectionMode.Zone || skybox.GetDrawableFlags() != DrawableFlags.Geometry || skybox.GetImage() is not null)
            throw new InvalidOperationException("Skybox did not preserve rbfx-style default reflection mode or image helper.");
        if (skybox.GetWorldBoundingBox().IsInside(new Vector3(50_000.0f, 0.0f, 0.0f)) == Intersection.Outside)
            throw new InvalidOperationException("Skybox did not preserve rbfx-style huge world bounds.");
        if (octree.Raycast(new Ray(Vector3.Zero, Vector3.Up), maxDistance: 1.0f, drawableFlags: DrawableFlags.Geometry).Any(result => ReferenceEquals(result.Drawable, skybox)))
            throw new InvalidOperationException("Skybox did not preserve rbfx-style raycast exclusion.");
        var groupNode = scene.CreateChild("StaticModelGroup");
        var group = groupNode.CreateComponent<StaticModelGroup>();
        group.SetModel(model);
        group.SetMaterial(material);
        var groupInstanceA = scene.CreateChild("GroupInstanceA");
        groupInstanceA.SetPosition(new Vector3(30.0f, 0.0f, 0.0f));
        var groupInstanceB = scene.CreateChild("GroupInstanceB");
        groupInstanceB.SetPosition(new Vector3(40.0f, 0.0f, 0.0f));
        group.AddInstanceNode(groupInstanceA);
        group.AddInstanceNode(groupInstanceB);
        if (group.GetNumInstanceNodes() != 2u || group.GetInstanceNode(1u) != groupInstanceB || !group.GetNodeIDsAttr().SequenceEqual([2u, groupInstanceA.Id, groupInstanceB.Id]))
            throw new InvalidOperationException("StaticModelGroup did not preserve rbfx-style instance node helpers.");
        if (group.GetNumWorldTransforms() != 2u || group.GetWorldTransforms().Count != 2 || group.GetWorldBoundingBox().IsInside(groupInstanceA.GetWorldPosition()) == Intersection.Outside || group.GetWorldBoundingBox().IsInside(groupInstanceB.GetWorldPosition()) == Intersection.Outside)
            throw new InvalidOperationException("StaticModelGroup did not preserve rbfx-style world transform and bounds helpers.");
        var groupHit = octree.RaycastSingle(new Ray(new Vector3(30.0f, 0.0f, -10.0f), Vector3.Forward), maxDistance: 100.0f, drawableFlags: DrawableFlags.Geometry);
        if (groupHit?.Drawable != group || groupHit.SubObject != 0u)
            throw new InvalidOperationException("StaticModelGroup did not preserve rbfx-style per-instance raycast helpers.");
        group.RemoveInstanceNode(groupInstanceB);
        if (group.GetNumInstanceNodes() != 1u || !group.GetNodeIDsAttr().SequenceEqual([1u, groupInstanceA.Id]))
            throw new InvalidOperationException("StaticModelGroup did not preserve rbfx-style instance node removal helpers.");
        group.RemoveAllInstanceNodes();
        group.SetNodeIDsAttr([2u, groupInstanceA.Id, groupInstanceB.Id]);
        group.ApplyAttributes();
        if (group.GetNumInstanceNodes() != 2u || group.GetInstanceNode(0u) != groupInstanceA || group.GetInstanceNode(1u) != groupInstanceB)
            throw new InvalidOperationException("StaticModelGroup did not preserve rbfx-style node ID attribute resolution helpers.");
        var customNode = scene.CreateChild("CustomGeometry");
        customNode.SetPosition(new Vector3(70.0f, 0.0f, 0.0f));
        var custom = customNode.CreateComponent<CustomGeometry>();
        if (custom.GetDrawableFlags() != DrawableFlags.Geometry || custom.GetNumGeometries() != 1u || custom.IsDynamic())
            throw new InvalidOperationException("CustomGeometry did not preserve rbfx-style defaults.");
        custom.SetDynamic(true);
        custom.SetNumGeometries(2u);
        if (!custom.BeginGeometry(0u, PrimitiveType.TriangleList))
            throw new InvalidOperationException("CustomGeometry did not begin a valid geometry.");
        custom.DefineVertex(new Vector3(-1.0f, -1.0f, 0.0f));
        custom.DefineNormal(Vector3.Forward);
        custom.DefineColor(new Color(1.0f, 0.0f, 0.0f));
        custom.DefineTexCoord(Vector2.Zero);
        custom.DefineTangent(new Vector4(1.0f, 0.0f, 0.0f, 1.0f));
        custom.DefineVertex(new Vector3(1.0f, -1.0f, 0.0f));
        custom.DefineNormal(Vector3.Forward);
        custom.DefineColor(new Color(0.0f, 1.0f, 0.0f));
        custom.DefineTexCoord(Vector2.Right);
        custom.DefineTangent(new Vector4(1.0f, 0.0f, 0.0f, 1.0f));
        custom.DefineVertex(new Vector3(0.0f, 1.0f, 0.0f));
        custom.DefineNormal(Vector3.Forward);
        custom.DefineColor(new Color(0.0f, 0.0f, 1.0f));
        custom.DefineTexCoord(Vector2.Up);
        custom.DefineTangent(new Vector4(1.0f, 0.0f, 0.0f, 1.0f));
        if (!custom.DefineGeometry(1u, PrimitiveType.LineList, 2u, hasNormals: true, hasColors: true, hasTexCoords: true, hasTangents: true))
            throw new InvalidOperationException("CustomGeometry did not define editable geometry storage.");
        var editableVertex = custom.GetVertex(1u, 1u) ?? throw new InvalidOperationException("CustomGeometry did not return an editable vertex.");
        editableVertex.Position = new Vector3(2.0f, 0.0f, 0.0f);
        editableVertex.Normal = Vector3.Up;
        editableVertex.Color = new Color(1.0f, 1.0f, 0.0f).ToUInt();
        editableVertex.TexCoord = Vector2.One;
        editableVertex.Tangent = new Vector4(0.0f, 1.0f, 0.0f, 1.0f);
        custom.SetMaterial(material);
        custom.Commit();
        if (!custom.IsDynamic() || custom.GetNumVertices(0u) != 3u || custom.GetNumVertices(1u) != 2u || custom.GetLodGeometry(0u, 0u)?.GetPrimitiveCount() != 1u)
            throw new InvalidOperationException("CustomGeometry did not preserve rbfx-style geometry counts after Commit.");
        if (custom.GetVertexBuffer().GetVertexCount() != 5u || custom.GetVertexBuffer().GetVertexSize() != 52u || custom.GetVertexBuffer().GetData().Count != 260)
            throw new InvalidOperationException("CustomGeometry did not preserve rbfx-style CPU vertex buffer metadata.");
        if (custom.GetBoundingBox().IsInside(new Vector3(2.0f, 0.0f, 0.0f)) == Intersection.Outside || custom.GetMaterial() != material)
            throw new InvalidOperationException("CustomGeometry did not preserve rbfx-style bounds or material helpers.");
        if (custom.GetMaterialsAttr().Type != StringHash.Calculate("Material") || custom.GetMaterialsAttr().Names.FirstOrDefault() != material.GetName())
            throw new InvalidOperationException("CustomGeometry did not preserve rbfx-style material resource references.");
        var customData = custom.GetGeometryDataAttr();
        var customClone = new CustomGeometry();
        customClone.SetGeometryDataAttr(customData);
        if (customClone.GetNumGeometries() != 2u || customClone.GetNumVertices(0u) != 3u || customClone.GetVertex(1u, 1u)?.Position != new Vector3(2.0f, 0.0f, 0.0f))
            throw new InvalidOperationException("CustomGeometry did not preserve rbfx-style geometry data attribute roundtrip.");
        var customRefs = ResourceRefList.Parse("Material;Materials/A,Materials/B");
        custom.SetMaterialsAttr(customRefs);
        if (!custom.GetMaterialsAttr().Equals(customRefs) || custom.BeginGeometry(99u, PrimitiveType.PointList))
            throw new InvalidOperationException("CustomGeometry did not preserve rbfx-style material metadata or index validation.");
        var customHit = octree.RaycastSingle(new Ray(new Vector3(70.0f, 0.0f, -5.0f), Vector3.Forward), RayQueryLevel.RayObb, maxDistance: 100.0f, drawableFlags: DrawableFlags.Geometry);
        if (customHit?.Drawable != custom || customHit.SubObject != uint.MaxValue)
            throw new InvalidOperationException("CustomGeometry did not preserve rbfx-style ray query helpers.");
        var emptyCustom = scene.CreateChild("EmptyCustomGeometry").CreateComponent<CustomGeometry>();
        if (octree.Raycast(new Ray(Vector3.Zero, Vector3.Forward), maxDistance: 1.0f, drawableFlags: DrawableFlags.Geometry).Any(result => ReferenceEquals(result.Drawable, emptyCustom)))
            throw new InvalidOperationException("CustomGeometry empty bounds should not produce ray hits.");
        if (Input.GetKeyDown(Key.Escape) || Input.GetScancodePress(Scancode.Escape))
            throw new InvalidOperationException("Headless input state should start empty.");
        if (RedBolt.Input.GetKeyFromName("A") != Key.A || RedBolt.Input.GetKeyName(Key.Num1) != "1" || RedBolt.Input.GetScancodeFromKey(Key.A) != Scancode.A)
            throw new InvalidOperationException("Input did not preserve rbfx-style key/scancode conversion helpers.");
        if (RedBolt.Input.GetKeyFromScancode(Scancode.LeftCtrl) != Key.LeftCtrl || RedBolt.Input.GetMouseButtonName(MouseButton.Left) != "Left")
            throw new InvalidOperationException("Input did not preserve rbfx-style scancode/mouse button naming helpers.");
        Input.SetKey(Key.A, Scancode.A, true);
        if (!Input.GetKeyDown(Key.A) || !Input.GetKeyPress(Key.A) || !Input.GetScancodeDown(Scancode.A) || !Input.GetScancodePress(Scancode.A))
            throw new InvalidOperationException("Input did not preserve rbfx-style managed key state setters.");
        Input.BeginFrame();
        if (!Input.GetKeyDown(Key.A) || Input.GetKeyPress(Key.A) || Input.GetScancodePress(Scancode.A))
            throw new InvalidOperationException("Input did not preserve rbfx-style per-frame key press reset semantics.");
        Input.SetKey(Key.A, Scancode.A, false);
        if (Input.GetKeyDown(Key.A) || Input.GetScancodeDown(Scancode.A))
            throw new InvalidOperationException("Input did not preserve rbfx-style key release state setters.");
        var inputMap = new InputMap("Input/Smoke", Input);
        inputMap.SetDeadZone(-1.0f);
        inputMap.MapKeyboardKey("Jump", Scancode.A);
        Input.SetKey(Key.A, Scancode.A, true);
        if (!MathDefs.ApproximatelyEquivalent(inputMap.GetDeadZone(), 0.0f) || !MathDefs.ApproximatelyEquivalent(inputMap.Evaluate("Jump"), 1.0f))
            throw new InvalidOperationException("InputMap did not preserve rbfx-style keyboard action evaluation.");
        inputMap.MapKeyboardKey("Move", Scancode.A);
        if (!MathDefs.ApproximatelyEquivalent(inputMap.Evaluate("Jump"), 0.0f) || !MathDefs.ApproximatelyEquivalent(inputMap.EvaluateByHash(new StringHash("Move")), 1.0f))
            throw new InvalidOperationException("InputMap did not preserve rbfx-style unique keyboard remapping.");
        Input.SetKey(Key.A, Scancode.A, false);
        Input.SetMouseButton(MouseButton.Left, true);
        Input.SetMouseWheel(2);
        if (!Input.GetMouseButtonDown(MouseButton.Left) || !Input.GetMouseButtonPress(MouseButton.Left) || Input.GetMouseMoveWheel() != 2)
            throw new InvalidOperationException("Input did not preserve rbfx-style managed mouse state setters.");
        inputMap.MapMouseButton("Fire", MouseButton.Left);
        inputMap.MapControllerButton("Fire", ControllerButton.A);
        inputMap.MapControllerAxis("Throttle", ControllerAxis.TriggerRight, 0.0f, 1.0f);
        inputMap.MapHat("Menu", HatPosition.Up);
        inputMap.MapScreenButton("TouchFire", "FireButton");
        inputMap.AddMetadata("Sensitivity", 2.0f);
        var fireMapping = inputMap.GetMapping("Fire");
        var throttleMapping = inputMap.GetMappingByHash(new StringHash("Throttle"));
        if (!MathDefs.ApproximatelyEquivalent(inputMap.Evaluate("Fire"), 1.0f) || fireMapping.MouseButtons.SingleOrDefault() != MouseButton.Left || fireMapping.ControllerButtons.SingleOrDefault().GetControllerButton() != ControllerButton.A)
            throw new InvalidOperationException("InputMap did not preserve rbfx-style mouse/controller mapping helpers.");
        if (!MathDefs.ApproximatelyEquivalent(throttleMapping.ControllerAxes.Single().Translate(0.5f, inputMap.GetDeadZone()), 0.5f) || inputMap.GetMapping("Menu").ControllerHats.SingleOrDefault() != HatPosition.Up || inputMap.GetMapping("TouchFire").ScreenButtons.SingleOrDefault() != "FireButton")
            throw new InvalidOperationException("InputMap did not preserve rbfx-style axis, hat, or screen mapping helpers.");
        if (!inputMap.HasMetadata() || !Equals(inputMap.GetMetadata("Sensitivity"), 2.0f) || !InputMap.GetScanCodeNames().Contains(nameof(Scancode.A)) || !InputMap.GetMouseButtonNames().Contains(nameof(MouseButton.Left)))
            throw new InvalidOperationException("InputMap did not preserve rbfx-style metadata or name-list helpers.");
        var axisAdapter = new AxisAdapter();
        axisAdapter.SetDeadZone(0.2f);
        axisAdapter.SetSensitivity(1.0f);
        if (!MathDefs.ApproximatelyEquivalent(axisAdapter.Transform(0.1f), 0.0f) || !MathDefs.ApproximatelyEquivalent(axisAdapter.Transform(0.6f), 0.25f, 0.001f))
            throw new InvalidOperationException("AxisAdapter did not preserve rbfx-style dead-zone or positive sensitivity helpers.");
        axisAdapter.SetInverted(true);
        axisAdapter.SetNegativeSensitivity(0.0f);
        if (!axisAdapter.IsInverted() || !MathDefs.ApproximatelyEquivalent(axisAdapter.Transform(0.6f), -0.5f, 0.001f))
            throw new InvalidOperationException("AxisAdapter did not preserve rbfx-style inversion helpers.");
        axisAdapter.SetInverted(false);
        axisAdapter.SetNeutralValue(-1.0f);
        axisAdapter.SetNegativeSensitivity(-0.5f);
        if (!MathDefs.ApproximatelyEquivalent(axisAdapter.GetDeadZone(), 0.2f) || !MathDefs.ApproximatelyEquivalent(axisAdapter.GetNeutralValue(), -1.0f) || !MathDefs.ApproximatelyEquivalent(axisAdapter.GetPositiveSensitivity(), 1.0f) || !MathDefs.ApproximatelyEquivalent(axisAdapter.GetNegativeSensitivity(), -0.5f))
            throw new InvalidOperationException("AxisAdapter did not preserve rbfx-style getter helpers.");
        Input.BeginFrame();
        Input.SetMouseButton(MouseButton.Left, false, clicks: 1);
        if (Input.GetMouseButtonDown(MouseButton.Left) || !Input.GetMouseButtonClick(MouseButton.Left))
            throw new InvalidOperationException("Input did not preserve rbfx-style mouse click state setters.");
        Input.SetEnabled(false);
        if (Input.GetEnabled() || Input.GetMouseMoveX() != 0 || Input.GetMouseMoveY() != 0 || Input.GetRelativeMousePosition() != Vector2.Zero)
            throw new InvalidOperationException("Input did not preserve rbfx-style managed state helpers.");
        Input.SetEnabled(true);
        Input.SetMouseVisible(false);
        Input.SetMouseVisible(true, suppressEvent: true);
        Input.ResetMouseVisible();
        Input.SetMouseGrabbed(true);
        Input.SetMouseGrabbed(false, suppressEvent: true);
        Input.ResetMouseGrabbed();
        if (Input.IsMouseVisible() || !Input.IsMouseGrabbed())
            throw new InvalidOperationException("Input did not preserve rbfx-style mouse visibility/grab reset helpers.");
        Input.SetMouseMode(MouseMode.Relative);
        Input.SetMouseMode(MouseMode.Absolute, suppressEvent: true);
        Input.ResetMouseMode();
        Input.SetToggleFullscreen(false);
        Input.SetTouchEmulation(true);
        if (!Input.IsMouseLocked() || !Input.IsMouseGrabbed() || Input.GetToggleFullscreen() || !Input.GetTouchEmulation())
            throw new InvalidOperationException("Input did not preserve rbfx-style mouse lock state helpers.");

        var logicScene = new Scene();
        var logicNode = logicScene.CreateChild("LogicNode");
        var logicComponent = logicNode.CreateComponent<CountingLogicComponent>();
        logicScene.Update(0.25f);
        logicScene.Update(0.5f);
        if (logicComponent.StartCount != 1 || logicComponent.DelayedStartCount != 1 || logicComponent.UpdateCount != 2 || logicComponent.PostUpdateCount != 2 || !MathDefs.ApproximatelyEquivalent(logicComponent.LastUpdateTimeStep, 0.5f) || !MathDefs.ApproximatelyEquivalent(logicComponent.LastPostUpdateTimeStep, 0.5f))
            throw new InvalidOperationException("LogicComponent did not preserve rbfx-style scene update lifecycle helpers.");
        logicNode.RemoveComponent(logicComponent);
        if (logicComponent.StopCount != 1)
            throw new InvalidOperationException("LogicComponent did not preserve rbfx-style stop lifecycle helpers.");

        var moveAndOrbitScene = new Scene();
        var moveAndOrbitNode = moveAndOrbitScene.CreateChild("Mover");
        var moveAndOrbit = moveAndOrbitNode.CreateComponent<MoveAndOrbitComponent>();
        moveAndOrbit.SetVelocity(new Vector3(1.0f, 0.0f, -1.0f));
        moveAndOrbit.SetYaw(45.0f);
        moveAndOrbit.SetPitch(180.0f);
        if (moveAndOrbit.GetVelocity() != new Vector3(1.0f, 0.0f, -1.0f) || !MathDefs.ApproximatelyEquivalent(moveAndOrbit.GetYaw(), 45.0f) || !MathDefs.ApproximatelyEquivalent(moveAndOrbit.GetPitch(), 90.0f) || !moveAndOrbit.GetYawPitchRotation().ApproximatelyEquivalent(new Quaternion(90.0f, 45.0f, 0.0f), 0.001f))
            throw new InvalidOperationException("MoveAndOrbitComponent did not preserve rbfx-style state helpers.");

        var freeFlyScene = new Scene();
        var freeFlyNode = freeFlyScene.CreateChild("FreeFly");
        var freeFly = freeFlyNode.CreateComponent<FreeFlyController>();
        freeFly.SetInput(Input);
        Input.BeginFrame();
        Input.SetMouseMode(MouseMode.Absolute);
        Input.SetKey(Key.W, Scancode.W, true);
        freeFlyScene.Update(0.5f);
        Input.SetKey(Key.W, Scancode.W, false);
        if (!freeFlyNode.Position.ApproximatelyEquivalent(new Vector3(0.0f, 0.0f, 10.0f)))
            throw new InvalidOperationException("FreeFlyController did not preserve rbfx-style keyboard translation helpers.");
        Input.BeginFrame();
        Input.SetMouseVisible(true);
        Input.SetMouseMode(MouseMode.Free);
        Input.SetMouseButton(MouseButton.Right, true);
        freeFlyScene.Update(0.1f);
        if (Input.IsMouseVisible() || Input.MouseMode != MouseMode.Relative)
            throw new InvalidOperationException("FreeFlyController did not preserve rbfx-style activation mouse capture helpers.");
        Input.BeginFrame();
        Input.SetMouseButton(MouseButton.Right, false);
        freeFlyScene.Update(0.1f);
        if (!Input.IsMouseVisible() || Input.MouseMode != MouseMode.Free)
            throw new InvalidOperationException("FreeFlyController did not preserve rbfx-style mouse capture restore helpers.");

        var moveControllerScene = new Scene();
        var moveControllerNode = moveControllerScene.CreateChild("MoveController");
        var controlledMovement = moveControllerNode.CreateComponent<MoveAndOrbitComponent>();
        var moveController = moveControllerNode.CreateComponent<MoveAndOrbitController>();
        moveController.SetInput(Input);
        var moveControllerMap = new InputMap("Input/MoveController", Input);
        moveControllerMap.MapKeyboardKey(MoveAndOrbitController.ActionForward, Scancode.W);
        moveControllerMap.MapKeyboardKey(MoveAndOrbitController.ActionRight, Scancode.D);
        moveControllerMap.MapKeyboardKey(MoveAndOrbitController.ActionTurnRight, Scancode.E);
        moveControllerMap.MapKeyboardKey(MoveAndOrbitController.ActionLookDown, Scancode.S);
        moveControllerMap.AddMetadata(MoveAndOrbitController.AxisRotationSensitivityMetadata, 50.0f);
        moveController.SetInputMap(moveControllerMap);
        Input.BeginFrame();
        Input.SetKey(Key.W, Scancode.W, true);
        Input.SetKey(Key.D, Scancode.D, true);
        Input.SetKey(Key.E, Scancode.E, true);
        Input.SetKey(Key.S, Scancode.S, true);
        moveControllerScene.Update(0.5f);
        Input.SetKey(Key.W, Scancode.W, false);
        Input.SetKey(Key.D, Scancode.D, false);
        Input.SetKey(Key.E, Scancode.E, false);
        Input.SetKey(Key.S, Scancode.S, false);
        if (!controlledMovement.GetVelocity().ApproximatelyEquivalent(new Vector3(1.0f, 0.0f, 1.0f)) || !MathDefs.ApproximatelyEquivalent(controlledMovement.GetYaw(), 25.0f) || !MathDefs.ApproximatelyEquivalent(controlledMovement.GetPitch(), 25.0f) || moveController.GetInputMapAttr().Name != "Input/MoveController")
            throw new InvalidOperationException("MoveAndOrbitController did not preserve rbfx-style action mapping helpers.");

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

sealed class CountingLogicComponent : LogicComponent
{
    public int StartCount { get; private set; }

    public int DelayedStartCount { get; private set; }

    public int StopCount { get; private set; }

    public int UpdateCount { get; private set; }

    public int PostUpdateCount { get; private set; }

    public float LastUpdateTimeStep { get; private set; }

    public float LastPostUpdateTimeStep { get; private set; }

    public override void Start()
    {
        ++StartCount;
    }

    public override void DelayedStart()
    {
        ++DelayedStartCount;
    }

    public override void Stop()
    {
        ++StopCount;
    }

    public override void Update(float timeStep)
    {
        ++UpdateCount;
        LastUpdateTimeStep = timeStep;
    }

    public override void PostUpdate(float timeStep)
    {
        ++PostUpdateCount;
        LastPostUpdateTimeStep = timeStep;
    }
}

sealed class SmokeJsonResource : JSONResource<SmokeJsonPayload>
{
    public SmokeJsonResource(string name)
        : base(name)
    {
    }
}

sealed class SmokeJsonPayload
{
    public string Name { get; set; } = string.Empty;

    public int Count { get; set; }
}
