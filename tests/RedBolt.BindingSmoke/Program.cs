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
        var tempResourceDir = Path.Combine(Path.GetTempPath(), "RedBolt.BindingSmoke", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(tempResourceDir, "Data"));
        try
        {
            var fileName = Path.Combine(tempResourceDir, "Data", "hello.txt");
            File.WriteAllText(fileName, "hello from resource cache");
            if (!Resources.AddResourceDir(tempResourceDir) || !Resources.Exists<BinaryFile>("Data/hello.txt"))
                throw new InvalidOperationException("ResourceCache did not register a file-backed resource directory.");
            var binaryFile = Resources.GetResource<BinaryFile>("Data/hello.txt");
            if (binaryFile.GetText() != "hello from resource cache" || binaryFile.AbsoluteFileName != fileName || !ReferenceEquals(binaryFile, Resources.GetResource<BinaryFile>("Data/hello.txt")))
                throw new InvalidOperationException("ResourceCache did not load and cache a file-backed BinaryFile.");
            if (ReferenceEquals(binaryFile, Resources.GetTempResource<BinaryFile>("Data/hello.txt")))
                throw new InvalidOperationException("ResourceCache temp resource loading reused a cached instance.");
        }
        finally
        {
            Directory.Delete(tempResourceDir, recursive: true);
        }

        var scene = new Scene();
        scene.CreateComponent<Octree>();
        if (scene.Root.Id == 0 || scene.Root.GetNumAttributes() == 0)
            throw new InvalidOperationException("Scene root did not initialize serializable node state.");
        if (typeof(Dictionary<string, List<int>>).GetFormattedTypeName() != "Dictionary<String, List<Int32>>")
            throw new InvalidOperationException("ObjectExtensionMethods did not preserve rbfx-style formatted type names.");
        if (ObjectReflection<StaticModel>.ClassName != nameof(StaticModel) || ObjectReflection<StaticModel>.TypeId.Value != StringHash.Calculate(nameof(StaticModel)))
            throw new InvalidOperationException("ObjectReflection did not preserve rbfx-style fallback type metadata.");
        if (new StringHash(typeof(StaticModel)).Value != StringHash.Calculate(nameof(StaticModel)) || ((StringHash)123u).Value != 123u)
            throw new InvalidOperationException("StringHash did not preserve rbfx-style type and uint conversions.");

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
        if (RedBolt.Input.GetKeyFromName("A") != Key.A || RedBolt.Input.GetKeyName(Key.Num1) != "1" || RedBolt.Input.GetScancodeFromKey(Key.A) != Scancode.A)
            throw new InvalidOperationException("Input did not preserve rbfx-style key/scancode conversion helpers.");
        if (RedBolt.Input.GetKeyFromScancode(Scancode.LeftCtrl) != Key.LeftCtrl || RedBolt.Input.GetMouseButtonName(MouseButton.Left) != "Left")
            throw new InvalidOperationException("Input did not preserve rbfx-style scancode/mouse button naming helpers.");
        Input.SetEnabled(false);
        if (Input.GetEnabled() || Input.GetMouseMoveX() != 0 || Input.GetMouseMoveY() != 0 || Input.GetRelativeMousePosition() != Vector2.Zero)
            throw new InvalidOperationException("Input did not preserve rbfx-style managed state helpers.");
        Input.SetEnabled(true);
        Input.SetMouseMode(MouseMode.Relative);
        if (!Input.IsMouseLocked())
            throw new InvalidOperationException("Input did not preserve rbfx-style mouse lock state helpers.");

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
