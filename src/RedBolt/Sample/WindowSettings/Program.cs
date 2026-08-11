namespace RedBolt.Sample.WindowSettings;

using System;

using RedBolt;
using RedBolt.Bindings.DearImGui;
using RedBolt.DearImGui;
using RedBolt.Container;
using RedBolt.Core;
using RedBolt.Engine;
using RedBolt.Graphics;
using RedBolt.Input;
using RedBolt.Math;
using RedBolt.RenderAPI;
using RedBolt.Scene;

internal static class WindowSettingsProgram
{
    private static int Main()
    {
        try
        {
            using var application = new WindowSettingsDemo();
            var exitCode = application.Run();
            if (exitCode == 0)
                Console.WriteLine("RedBolt WindowSettings sample finished.");
            return exitCode;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }
}

/// <summary>
/// Demo application for dynamic window settings change.
/// </summary>
public class WindowSettingsDemo : Application
{
    /// <summary>
    /// Construct.
    /// </summary>
    public WindowSettingsDemo()
    {
    }

    /// <summary>
    /// Setup after engine initialization and before running the main loop.
    /// </summary>
    protected override void Start()
    {
        // Execute base class startup
        base.Start();

        // Enable OS cursor
        Input.SetMouseVisible(true);

        // Load XML file containing default UI style sheet
        // Set the loaded style as default style
        // The rbfx UI style is omitted with the allowed Dear ImGui replacement.

        // Create window with settings.
        InitSettings();
        SynchronizeSettings();
        SubscribeToEvent<GraphicsEvents.ScreenModeEvent>(
            (ref GraphicsEvents.ScreenModeEvent _) => SynchronizeSettings());

        // Set the mouse mode to use in the sample
        Input.SetMouseMode(MouseMode.Free);
        Input.SetMouseVisible(true);

        // Create scene
        CreateScene();

        // Setup viewport
        var viewport = new Viewport(_scene!, _cameraNode!.GetComponent<Camera>()!);
        Renderer.SetViewport(0, viewport);
    }

    /// <summary>
    /// Construct the scene content.
    /// </summary>
    private void CreateScene()
    {
        var cache = Resources;

        _scene = new Scene();
        _scene.CreateComponent<Octree>();

        var zone = _scene.CreateComponent<Zone>();
        zone.SetAmbientColor(Color.White);
        zone.SetBoundingBox(new BoundingBox(-1000.0f, 1000.0f));

        // Create 3D object
        var objectNode = _scene.CreateChild("Object");
        objectNode.SetRotation(new Quaternion(45.0f, 45.0f, 45.0f));
        var objectModel = objectNode.CreateComponent<StaticModel>();
        objectModel.SetModel(cache.Get<Model>("Models/Box.mdl"));
        objectModel.SetMaterial(cache.Get<Material>("Materials/Stone.xml"));

        // Create camera
        _cameraNode = _scene.CreateChild("Camera");
        _cameraNode.CreateComponent<Camera>();
        _cameraNode.SetPosition(new Vector3f(0.0f, 0.0f, -4.0f));

        // Rotate object
        SubscribeToEvent<SceneEvents.SceneUpdateEvent>(_scene,
            (ref SceneEvents.SceneUpdateEvent eventData) =>
        {
            var timeStep = eventData.TimeStep;
            objectNode.Rotate(new Quaternion(0.0f, 20.0f * timeStep, 0.0f), TransformSpace.World);
        });
    }

    /// <summary>
    /// Create window with settings.
    /// </summary>
    private void InitSettings()
    {
        Context.RegisterSubsystem(new DearImGui((ImGuiConfigFlags)Engine.Parameters.DearImGuiFlags));
        SubscribeToEvent<DearImGuiEvents.EndRenderingDearImGuiEvent>(
            (ref DearImGuiEvents.EndRenderingDearImGuiEvent _) => DrawUI());

        // Create the Window and add it to the UI's root node
        // Set Window size and layout settings
        // Create the Window title Text
        // Create monitor selector
        // Create resolution selector
        // Create fullscreen controller
        // Create borderless controller
        // Create resizable controller
        // Create resizable controller
        // Create multi-sample controller from 1 (= 2^0) to 16 (= 2^4)
        // Create "Apply" button
        // Apply settings when "Apply" button is clicked
        // The rbfx controls are replaced by the allowed Dear ImGui sample UI surface.
    }

    /// <summary>
    /// Synchronize settings with current state of the engine.
    /// </summary>
    private void SynchronizeSettings()
    {
        var renderDevice = GetSubsystem<RenderDevice>()!;
        var windowSettings = renderDevice.GetWindowSettings();

        // Synchronize monitor
        _monitor = windowSettings.Monitor;

        // Synchronize resolution list
        var fullscreenModes = RenderDevice.GetFullscreenModes(windowSettings.Monitor);

        // Synchronize selected resolution
        var currentSwapChainSize = renderDevice.GetSwapChainSize();
        var currentMode = new FullscreenMode(currentSwapChainSize, windowSettings.RefreshRate);
        _mode = RenderDevice.GetClosestFullscreenModeIndex(fullscreenModes, currentMode);
        _resolutionLabel.FormatInPlace(
            $"Current: {currentMode.Size.X}x{currentMode.Size.Y}, {currentMode.RefreshRate} Hz");

        // Synchronize fullscreen and borderless flags
        _fullscreen = windowSettings.Mode == WindowMode.Fullscreen;
        _borderless = windowSettings.Mode == WindowMode.Borderless;
        _resizable = windowSettings.Resizable;
        _vSync = windowSettings.VSync;

        // Synchronize MSAA
        for (var i = 0; i <= 4; ++i)
        {
            if (windowSettings.MultiSample == 1 << i)
                _multiSample = i;
        }
    }

    private void ApplySettings()
    {
        if (_monitor < 0)
            return;

        var fullscreenModes = RenderDevice.GetFullscreenModes(_monitor);
        if (_mode < 0 || _mode >= fullscreenModes.Count)
            return;

        var windowSettings = new WindowSettings();

        if (_fullscreen)
            windowSettings.Mode = WindowMode.Fullscreen;
        else if (_borderless)
            windowSettings.Mode = WindowMode.Borderless;
        windowSettings.Resizable = _resizable;
        windowSettings.VSync = _vSync;

        windowSettings.MultiSample = _multiSample is < 0 or > 4 ? 1 : 1 << _multiSample;

        var renderDevice = GetSubsystem<RenderDevice>()!;
        var dpiScale = windowSettings.Mode == WindowMode.Fullscreen ? 1.0f : renderDevice.GetDpiScale();
        var width = MathDefs.RoundToInt(fullscreenModes[_mode].Size.X / dpiScale);
        var height = MathDefs.RoundToInt(fullscreenModes[_mode].Size.Y / dpiScale);
        windowSettings.Size = new Vector2i(width, height);

        windowSettings.RefreshRate = fullscreenModes[_mode].RefreshRate;

        Graphics.SetDefaultWindowModes(windowSettings);
    }

    private void DrawUI()
    {
        var windowOpen = true;
        if (!ImGui.Begin("Window Settings"u8, ref windowOpen, ImGuiWindowFlags.ImGuiWindowFlagsNoSavedSettings))
        {
            ImGui.End();
            return;
        }

        ImGui.TextUnformatted(_resolutionLabel.ContentBytes);
        ImGui.InputInt("Monitor"u8, ref _monitor);
        ImGui.InputInt("Mode index"u8, ref _mode);
        ImGui.Checkbox("Fullscreen"u8, ref _fullscreen);
        ImGui.Checkbox("Borderless"u8, ref _borderless);
        ImGui.Checkbox("Resizable"u8, ref _resizable);
        ImGui.Checkbox("V-Sync"u8, ref _vSync);
        ImGui.InputInt("MSAA index"u8, ref _multiSample);
        if (ImGui.Button("Apply"u8))
            ApplySettings();
        ImGui.End();
    }

    protected override void Update(float timeStep)
    {
        if (Input.ExitRequested || Input.GetKeyPress(Key.Escape))
            Exit();
    }

    protected override void Setup(EngineParameters parameters)
    {
        parameters.ApplicationName = "RedBolt.Sample.WindowSettings";
        parameters.Borderless = false;
        parameters.FullScreen = false;
        parameters.WindowMaximize = false;
        parameters.ResourcePaths = "CoreData;Data";
        parameters.ResourcePrefixPaths = ".;..";
#if !REDBOLT_PLATFORM_BROWSER
            parameters.WindowHeight = 720;
            parameters.WindowWidth = 1280;
#endif
        parameters.WindowTitle = "RedBolt Window Settings";
    }

    protected override void Stop()
    {
        _resolutionLabel.Dispose();
        Renderer.SetNumViewports(0);
        _cameraNode = null;
        _scene?.Dispose();
        _scene = null;
        base.Stop();
    }

    public override void Dispose()
    {
        _resolutionLabel.Dispose();
        if (_scene is not null)
            Renderer.SetNumViewports(0);
        _cameraNode = null;
        _scene?.Dispose();
        _scene = null;
        base.Dispose();
    }

    private Scene? _scene;
    private Node? _cameraNode;
    private int _monitor;
    private readonly CString _resolutionLabel = new();
    private int _mode;
    private bool _fullscreen;
    private bool _borderless;
    private bool _resizable;
    private bool _vSync;
    private int _multiSample;
}
