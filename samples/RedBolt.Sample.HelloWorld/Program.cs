using System;
using RedBolt;
using RedBolt.Bindings.DearImGui;

try
{
    using var application = new HelloWorld();
    var exitCode = application.Run();
    if (exitCode == 0)
        Console.WriteLine("RedBolt HelloWorld sample finished.");
    return exitCode;
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex);
    return 1;
}

/// <summary>
/// This first example, maintaining tradition, prints a "Hello World" message.
/// Furthermore it shows:
///     - Using the Sample / Application classes, which initialize the Urho3D engine and run the main loop
///     - Adding a Text element to the graphical user interface
///     - Subscribing to and handling of update events
/// </summary>
public class HelloWorld : Application
{
    /// <summary>
    /// Construct.
    /// </summary>
    public HelloWorld()
    {
    }

    /// <summary>
    /// Setup after engine initialization and before running the main loop.
    /// </summary>
    protected override void Start()
    {
        // Set the mouse mode to use in the sample
        Input.SetMouseMode(MouseMode.Free);
        Input.SetMouseVisible(true);

        // Execute base class startup
        base.Start();

        Context.RegisterSubsystem(new DearImGui((ImGuiConfigFlags)Engine.Parameters.DearImGuiFlags));

        // Create "Hello World" Text
        CreateText();

        // Finally subscribe to the update event. Note that by subscribing events at this point we have already missed some events
        // like the ScreenMode event sent by the Graphics subsystem when opening the application window. To catch those as well we
        // could subscribe in the constructor instead.
        SubscribeToEvents();
    }

    /// <summary>
    /// Construct a new Text instance, containing the 'Hello World' String, and add it to the UI root element.
    /// </summary>
    private void CreateText()
    {
        // Construct new Text object
        // Set String to display
        _helloText.FormatInPlace("Hello World from Urho3D!");

        // Set font and text color
        // Align Text center-screen
        // Add Text instance to the UI root element
        // The rbfx Text control is replaced by the allowed Dear ImGui sample UI surface.
    }

    /// <summary>
    /// Subscribe to application-wide logic update events.
    /// </summary>
    private void SubscribeToEvents()
    {
        _dearImGuiSubscription = Context.SubscribeToEvent<DearImGuiEvents.EndRenderingDearImGuiEvent>((ref DearImGuiEvents.EndRenderingDearImGuiEvent _) => DrawText());
    }

    /// <summary>
    /// Handle the logic update event.
    /// </summary>
    protected override void Update(float timeStep)
    {
        // Do nothing for now, could be extended to eg. animate the display
    }

    private void DrawText()
    {
        if (ImGui.Begin("Hello World\0"u8, out _, ImGuiWindowFlags.ImGuiWindowFlagsNoResize | ImGuiWindowFlags.ImGuiWindowFlagsNoSavedSettings))
            ImGui.TextUnformatted(_helloText.Bytes);
        ImGui.End();
    }

    protected override void Setup(EngineParameters parameters)
    {
        parameters.ApplicationName = "RedBolt.Sample.HelloWorld";
        parameters.Borderless = false;
        parameters.FullScreen = false;
        parameters.WindowMaximize = false;
        parameters.ResourcePaths = "CoreData;Data";
        parameters.ResourcePrefixPaths = ".;..";
        parameters.WindowHeight = 720;
        parameters.WindowTitle = "RedBolt Hello World";
        parameters.WindowWidth = 1280;
    }

    protected override void Stop()
    {
        _dearImGuiSubscription?.Dispose();
        _dearImGuiSubscription = null;
        _helloText.Dispose();
        base.Stop();
    }

    public override void Dispose()
    {
        _dearImGuiSubscription?.Dispose();
        _dearImGuiSubscription = null;
        _helloText.Dispose();
        base.Dispose();
    }

    private readonly CString _helloText = new();
    private ContextEventSubscription? _dearImGuiSubscription;
}
