using System;
using System.IO;
using RedBolt;
using RedBolt.Bindings.DearImGui;

try
{
    var exitCode = new WindowSettingsApplication().Run();
    if (exitCode == 0)
        Console.WriteLine("RedBolt WindowSettings sample finished.");
    return exitCode;
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex);
    return 1;
}

sealed class WindowSettingsApplication : Application
{
    private ContextEventSubscription? _dearImGuiSubscription;
    private Scene? _scene;
    private Node? _objectNode;
    private Camera? _camera;
    private int _monitor;
    private int _mode;
    private int _multiSample;
    private bool _resizable;
    private bool _vSync;
    private bool _borderless;
    private bool _fullscreen;

    protected override void Setup(EngineParameters parameters)
    {
        parameters.ApplicationName = "RedBolt.Sample.WindowSettings";
        parameters.Borderless = false;
        parameters.GraphicsBackend = GraphicsBackend.OpenGL;
        parameters.ResourcePaths = "CoreData;Data";
        parameters.ResourcePrefixPaths = FindResourcePrefixPath();
        parameters.WindowHeight = 720;
        parameters.WindowTitle = "RedBolt Window Settings";
        parameters.WindowWidth = 1280;
    }

    protected override void Start()
    {
        Context.RegisterSubsystem(new DearImGui(Context, Engine.Parameters.DearImGuiFlags));
        _dearImGuiSubscription = Context.SubscribeToEvent<DearImGuiEvents.EndRenderingDearImGuiEvent>((ref DearImGuiEvents.EndRenderingDearImGuiEvent _) => DrawUI());

        CreateScene();
        Renderer.SetViewport(0, new Viewport(_scene!, _camera!));
        SynchronizeSettings();
    }

    protected override void Update(float timeStep)
    {
        if (Input.ExitRequested || Input.GetKeyPress(Key.Escape))
            Exit();
        if (_objectNode is not null)
            _objectNode.Rotate(new Quaternion(0.0f, 20.0f * timeStep, 0.0f));
    }

    protected override void Stop()
    {
        _dearImGuiSubscription?.Dispose();
        _dearImGuiSubscription = null;
    }

    private void CreateScene()
    {
        _scene = new Scene();
        _scene.CreateComponent<Octree>();
        var zone = _scene.CreateComponent<Zone>();
        zone.SetAmbientColor(Color.White);
        zone.SetBoundingBox(new BoundingBox(-1000.0f, 1000.0f));

        _objectNode = _scene.CreateChild("Object");
        _objectNode.SetRotation(Quaternion.FromEulerAngles(45.0f, 45.0f, 45.0f));
        var model = _objectNode.CreateComponent<StaticModel>();
        model.SetModel(Resources.Get<Model>("Models/Box.mdl"));
        model.SetMaterial(Resources.Get<Material>("Materials/Stone.xml"));

        var cameraNode = _scene.CreateChild("Camera");
        _camera = cameraNode.CreateComponent<Camera>();
        cameraNode.SetPosition(new Vector3f(0.0f, 0.0f, -4.0f));
    }

    private void SynchronizeSettings()
    {
        var settings = Graphics.GetWindowSettings();
        _monitor = settings.Monitor;
        _fullscreen = settings.Mode == WindowMode.Fullscreen;
        _borderless = settings.Mode == WindowMode.Borderless;
        _resizable = settings.Resizable;
        _vSync = settings.VSync;
        _multiSample = settings.MultiSample;
        var modes = RenderDevice.GetFullscreenModes(_monitor);
        var renderDevice = GetSubsystem<RenderDevice>()!;
        var current = new FullscreenMode(renderDevice.GetSwapChainSize(), settings.RefreshRate);
        _mode = (int)RenderDevice.GetClosestFullscreenModeIndex(modes, current);
        _multiSample = 0;
        for (var i = 0; i <= 4; ++i)
        {
            if (settings.MultiSample == (1 << i))
                _multiSample = i;
        }
    }

    private void ApplySettings()
    {
        var modes = RenderDevice.GetFullscreenModes(_monitor);
        if (_mode < 0 || _mode >= modes.Count)
            return;

        var mode = modes[_mode];
        var settings = new WindowSettings
        {
            Mode = _fullscreen ? WindowMode.Fullscreen : _borderless ? WindowMode.Borderless : WindowMode.Windowed,
            Monitor = _monitor,
            Resizable = _resizable,
            VSync = _vSync,
            MultiSample = 1 << _multiSample,
            RefreshRate = mode.RefreshRate,
            Size = new Vector2i(mode.Size.X, mode.Size.Y)
        };
        var dpiScale = settings.Mode == WindowMode.Fullscreen ? 1.0f : GetSubsystem<RenderDevice>()!.GetDpiScale();
        settings.Size = new Vector2i(
            MathDefs.RoundToInt(mode.Size.X / dpiScale),
            MathDefs.RoundToInt(mode.Size.Y / dpiScale));
        Graphics.SetDefaultWindowModes(settings);
    }

    private void DrawUI()
    {
        if (!ImGui.Begin("Window Settings\0"u8, out _, ImGuiWindowFlags.ImGuiWindowFlagsNoSavedSettings))
        {
            ImGui.End();
            return;
        }

        ImGui.TextUnformatted("WindowSettingsDemo uses Dear ImGui instead of rbfx UI.\0"u8);
        ImGui.InputInt("Monitor\0"u8, out _monitor);
        ImGui.InputInt("Mode index\0"u8, out _mode);
        ImGui.InputInt("MSAA index\0"u8, out _multiSample);
        if (ImGui.Checkbox("Fullscreen\0"u8, out _fullscreen))
            ApplySettings();
        if (ImGui.Checkbox("Borderless\0"u8, out _borderless))
            ApplySettings();
        if (ImGui.Checkbox("Resizable\0"u8, out _resizable))
            ApplySettings();
        if (ImGui.Checkbox("V-Sync\0"u8, out _vSync))
            ApplySettings();
        ImGui.End();
    }

    private static string FindResourcePrefixPath()
    {
        var envRoot = Environment.GetEnvironmentVariable("REDBOLT_RBFX_BIN_DIR");
        if (TryNormalizeResourcePrefixPath(envRoot, out var normalizedEnvRoot))
            return normalizedEnvRoot;
        for (var current = new DirectoryInfo(AppContext.BaseDirectory); current is not null; current = current.Parent)
        {
            if (TryNormalizeResourcePrefixPath(Path.Combine(current.FullName, "bin"), out var localBinRoot))
                return localBinRoot;
            if (TryNormalizeResourcePrefixPath(Path.Combine(current.FullName, "tests", "RedBolt.Tests", "TestData", "rbfx"), out var testDataRoot))
                return testDataRoot;
            if (Directory.Exists(Path.Combine(current.FullName, ".git")))
                break;
        }
        throw new InvalidOperationException("WindowSettings sample could not locate rbfx CoreData/Data resource directories.");
    }

    private static bool TryNormalizeResourcePrefixPath(string? root, out string normalizedRoot)
    {
        normalizedRoot = string.Empty;
        if (string.IsNullOrWhiteSpace(root))
            return false;
        var candidateRoot = Path.GetFullPath(root);
        if (!Directory.Exists(Path.Combine(candidateRoot, "CoreData")) || !Directory.Exists(Path.Combine(candidateRoot, "Data")))
            return false;
        normalizedRoot = candidateRoot;
        return true;
    }
}
