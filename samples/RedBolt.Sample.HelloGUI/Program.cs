using System;
using System.IO;
using RedBolt;
using RedBolt.Bindings.DearImGui;

try
{
    var exitCode = new HelloGuiApplication().Run();
    if (exitCode == 0)
        Console.WriteLine("RedBolt HelloGUI sample finished.");
    return exitCode;
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex);
    return 1;
}

sealed class HelloGuiApplication : Application
{
    private readonly CString _windowTitle = new("Hello GUI!");
    private readonly Scene _scene = new();
    private float _fishPositionX;
    private float _fishPositionY;
    private ContextEventSubscription? _dearImGuiSubscription;
    private bool _checkBox;
    private int _lineEdit;
    private bool _windowOpen = true;

    protected override void Setup(EngineParameters parameters)
    {
        parameters.ApplicationName = "RedBolt.Sample.HelloGUI";
        parameters.Borderless = false;
        parameters.FullScreen = false;
        parameters.WindowMaximize = false;
        parameters.ResourcePaths = "CoreData;Data";
        parameters.ResourcePrefixPaths = FindResourcePrefixPath();
        parameters.WindowMaximize = false;
        parameters.WindowHeight = 720;
        parameters.WindowTitle = "RedBolt Hello GUI";
        parameters.WindowWidth = 1280;
    }

    protected override void Start()
    {
        // Execute base class startup
        // Enable OS cursor
        Input.SetMouseVisible(true);

        Context.RegisterSubsystem(new DearImGui(Context, Engine.Parameters.DearImGuiFlags));
        _dearImGuiSubscription = Context.SubscribeToEvent<DearImGuiEvents.EndRenderingDearImGuiEvent>((ref DearImGuiEvents.EndRenderingDearImGuiEvent _) => DrawUI());

        // Initialize Window
        InitWindow();

        // Create and add some controls to the Window
        InitControls();

        // Create a draggable Fish
        CreateDraggableFish();

        var cameraNode = _scene.CreateChild("Camera");
        var camera = cameraNode.CreateComponent<Camera>();
        Renderer.SetViewport(0, new Viewport(_scene, camera));

        // Set the mouse mode to use in the sample
        Input.SetMouseMode(MouseMode.Free);
        Input.SetMouseVisible(true);
    }

    private void InitControls()
    {
        // Create a CheckBox
        // Create a Button
        // Create a LineEdit
        // Add controls to Window
        // Apply previously set default style
    }

    private void InitWindow()
    {
        // Create the Window and add it to the UI's root node
        // Set Window size and layout settings
        // Create Window 'titlebar' container
        // Create the Window title Text
        // Create the Window's close button
        // Add the controls to the title bar
        // Add the title bar to the Window
        // Apply styles
        // Subscribe to buttonClose release (following a 'press') events
        // Subscribe also to all UI mouse clicks just to see where we have clicked
    }

    private void CreateDraggableFish()
    {
        // Create a draggable Fish button
        _fishPositionX = 576.0f;
        _fishPositionY = 200.0f;

        // Add a tooltip to Fish button
        // Subscribe draggableFish to Drag Events (in order to make it draggable)
        // See "Event list" in documentation's Main Page for reference on available Events and their eventData
    }

    private void HandleDragBegin()
    {
        // Get UIElement relative position where input (touch or click) occurred (top-left = IntVector2(0,0))
    }

    private void HandleDragMove()
    {
        // ImGui's draggable position widget replaces the built-in drag event path.
    }

    private void HandleDragEnd()
    {
        // For reference (not used here)
    }

    private void HandleControlClicked(string name)
    {
        // Get the Text control acting as the Window's title
        // Get control that was clicked
        // Update the Window's title text
        _windowTitle.FormatInPlace("Hello {0}!", name);
    }

    private void HandleClosePressed()
    {
        Exit();
    }

    private void DrawUI()
    {
        if (!_windowOpen)
            return;

        if (ImGui.Begin(_windowTitle.Bytes, out var open, ImGuiWindowFlags.ImGuiWindowFlagsNoSavedSettings))
        {
            _windowOpen = open;

            if (ImGui.Checkbox("CheckBox\0"u8, out _checkBox))
                HandleControlClicked("CheckBox");

            if (ImGui.Button("Button\0"u8))
                HandleControlClicked("Button");

            if (ImGui.InputInt("LineEdit\0"u8, out _lineEdit))
                HandleControlClicked("LineEdit");

            if (ImGui.DragFloat("Fish X\0"u8, out _fishPositionX, 1.0f, 0.0f, 1280.0f) ||
                ImGui.DragFloat("Fish Y\0"u8, out _fishPositionY, 1.0f, 0.0f, 720.0f))
            {
                HandleDragBegin();
                HandleDragMove();
            }

            ImGui.TextUnformatted("Please drag me!\0"u8);
            if (ImGui.Button("Close\0"u8))
                HandleClosePressed();
        }

        ImGui.End();
    }

    protected override void Update(float timeStep)
    {
    }

    protected override void Stop()
    {
        _dearImGuiSubscription?.Dispose();
        _dearImGuiSubscription = null;
        _windowTitle.Dispose();
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
            if (TryNormalizeResourcePrefixPath(Path.Combine(current.FullName, "rbfx", "bin"), out var repoRbfxBinRoot))
                return repoRbfxBinRoot;
            if (TryNormalizeResourcePrefixPath(Path.Combine(current.FullName, "..", "rbfx", "bin"), out var siblingRbfxBinRoot))
                return siblingRbfxBinRoot;

            if (Directory.Exists(Path.Combine(current.FullName, ".git")))
                break;
        }

        throw new InvalidOperationException("HelloGUI sample could not locate rbfx CoreData/Data resource directories.");
    }

    private static bool TryNormalizeResourcePrefixPath(string? root, out string normalizedRoot)
    {
        normalizedRoot = string.Empty;
        if (string.IsNullOrWhiteSpace(root))
            return false;

        var candidateRoot = Path.GetFullPath(root);
        if (!Directory.Exists(Path.Combine(candidateRoot, "CoreData")) ||
            !Directory.Exists(Path.Combine(candidateRoot, "Data")))
        {
            return false;
        }

        normalizedRoot = candidateRoot;
        return true;
    }
}
