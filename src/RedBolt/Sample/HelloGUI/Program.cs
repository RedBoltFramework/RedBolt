using System;

using RedBolt;
using RedBolt.Bindings.DearImGui;
using RedBolt.Container;
using RedBolt.Core;
using RedBolt.Engine;
using RedBolt.Input;

#if !REDBOLT_WEB_SAMPLE
try
{
    using var application = new HelloGUI();
    var exitCode = application.Run();
    if (exitCode == 0)
        Console.WriteLine("RedBolt HelloGUI sample finished.");
    return exitCode;
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex);
    return 1;
}
#endif

/// <summary>
/// A simple 'HelloWorld' GUI created purely from code.
/// This sample demonstrates:
///     - Creation of controls and building a UI hierarchy
///     - Loading UI style from XML and applying it to controls
///     - Handling of global and per-control events
/// For more advanced users (beginners can skip this section):
///     - Dragging UIElements
///     - Displaying tooltips
///     - Accessing available Events data (eventData)
/// </summary>
public class HelloGUI : Application
{
    /// <summary>
    /// Construct.
    /// </summary>
    public HelloGUI()
    {
        _dragBeginPosition = Vector2i.Zero;
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

        Context.RegisterSubsystem(new DearImGui((ImGuiConfigFlags)Engine.Parameters.DearImGuiFlags));
        SubscribeToEvent<DearImGuiEvents.EndRenderingDearImGuiEvent>((ref DearImGuiEvents.EndRenderingDearImGuiEvent _) => DrawUI());

        // Load XML file containing default UI style sheet
        // Set the loaded style as default style

        // Initialize Window
        InitWindow();

        // Create and add some controls to the Window
        InitControls();

        // Create a draggable Fish
        CreateDraggableFish();

        // Set the mouse mode to use in the sample
        Input.SetMouseMode(MouseMode.Free);
        Input.SetMouseVisible(true);
    }

    /// <summary>
    /// Create and initialize a Window control.
    /// </summary>
    private void InitWindow()
    {
        // Create the Window and add it to the UI's root node
        _windowOpen = true;

        // Set Window size and layout settings
        // Create Window 'titlebar' container
        // Create the Window title Text
        _windowTitle.FormatInPlace($"Hello GUI!");

        // Create the Window's close button
        // Add the controls to the title bar
        // Add the title bar to the Window
        // Apply styles
        // Subscribe to buttonClose release (following a 'press') events
        // Subscribe also to all UI mouse clicks just to see where we have clicked
    }

    /// <summary>
    /// Create and add various common controls for demonstration purposes.
    /// </summary>
    private void InitControls()
    {
        // Create a CheckBox
        _checkBox = false;

        // Create a Button
        // Create a LineEdit
        _lineEdit = 0;

        // Add controls to Window
        // Apply previously set default style
    }

    /// <summary>
    /// Create a draggable fish button.
    /// </summary>
    private void CreateDraggableFish()
    {
        const int FishSize = 128;

        // Create a draggable Fish button
        _fishPositionX = (Graphics.Width - FishSize) / 2;
        _fishPositionY = 200;

        // Add a tooltip to Fish button
        // Subscribe draggableFish to Drag Events (in order to make it draggable)
        // See "Event list" in documentation's Main Page for reference on available Events and their eventData
    }

    /// <summary>
    /// Handle drag begin for the fish button.
    /// </summary>
    private void HandleDragBegin()
    {
        // Get UIElement relative position where input (touch or click) occurred (top-left = IntVector2(0,0))
        _dragBeginPosition = Vector2i.Zero;
    }

    /// <summary>
    /// Handle drag move for the fish button.
    /// </summary>
    private void HandleDragMove()
    {
        _fishPositionX -= _dragBeginPosition.X;
        _fishPositionY -= _dragBeginPosition.Y;
    }

    /// <summary>
    /// Handle drag end for the fish button.
    /// </summary>
    private void HandleDragEnd() // For reference (not used here)
    {
    }

    /// <summary>
    /// Handle any UI control being clicked.
    /// </summary>
    private void HandleControlClicked(string name)
    {
        // Get the Text control acting as the Window's title
        // Get control that was clicked
        // Update the Window's title text
        _windowTitle.FormatInPlace($"Hello {name}!");
    }

    /// <summary>
    /// Handle close button pressed and released.
    /// </summary>
    private void HandleClosePressed()
    {
        Exit();
    }

    private void DrawUI()
    {
        if (!_windowOpen)
            return;

        var visible = ImGui.Begin(_windowTitle.Bytes, ref _windowOpen, ImGuiWindowFlags.ImGuiWindowFlagsNoSavedSettings);
        if (!_windowOpen)
            HandleClosePressed();

        if (visible)
        {
            if (ImGui.Checkbox("CheckBox\0"u8, ref _checkBox))
                HandleControlClicked("CheckBox");

            if (ImGui.Button("Button\0"u8))
                HandleControlClicked("Button");

            if (ImGui.InputInt("LineEdit\0"u8, out _lineEdit))
                HandleControlClicked("LineEdit");

            if (ImGui.DragInt("Fish X\0"u8, out _fishPositionX, 1.0f, 0, Graphics.Width) ||
                ImGui.DragInt("Fish Y\0"u8, out _fishPositionY, 1.0f, 0, Graphics.Height))
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

    protected override void Setup(EngineParameters parameters)
    {
        parameters.ApplicationName = "RedBolt.Sample.HelloGUI";
        parameters.Borderless = false;
        parameters.FullScreen = false;
        parameters.WindowMaximize = false;
        parameters.ResourcePaths = "CoreData;Data";
        parameters.ResourcePrefixPaths = ".;..";
        if (ProcessUtils.GetPlatform() != PlatformId.Web)
        {
            parameters.WindowHeight = 720;
            parameters.WindowWidth = 1280;
        }
        parameters.WindowTitle = "RedBolt Hello GUI";
    }

    protected override void Stop()
    {
        _windowTitle.Dispose();
        base.Stop();
    }

    public override void Dispose()
    {
        _windowTitle.Dispose();
        base.Dispose();
    }

    private bool _windowOpen;
    private readonly CString _windowTitle = new();
    /// <summary>
    /// Remembered drag begin position.
    /// </summary>
    private Vector2i _dragBeginPosition;
    private int _fishPositionX;
    private int _fishPositionY;
    private bool _checkBox;
    private int _lineEdit;
}
