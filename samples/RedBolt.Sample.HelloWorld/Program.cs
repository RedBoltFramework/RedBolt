using System;
using RedBolt;
using RedBolt.Bindings.DearImGui;

try
{
    var exitCode = new HelloWorldApplication().Run();
    if (exitCode == 0)
        Console.WriteLine("RedBolt HelloWorld sample finished.");
    return exitCode;
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex);
    return 1;
}

sealed class HelloWorldApplication : Application
{
    private readonly CString _helloText = new();
    private ContextEventSubscription? _dearImGuiSubscription;

    protected override void Setup(EngineParameters parameters)
    {
        parameters.ApplicationName = "RedBolt.Sample.HelloWorld";
        parameters.Borderless = false;
        parameters.FullScreen = false;
        parameters.WindowMaximize = false;
        parameters.ResourcePaths = "CoreData;Data";
        parameters.ResourcePrefixPaths = ".;..";
        parameters.WindowMaximize = false;
        parameters.WindowHeight = 720;
        parameters.WindowTitle = "RedBolt Hello World";
        parameters.WindowWidth = 1280;
    }

    protected override void Start()
    {
        // Set the mouse mode to use in the sample
        Input.SetMouseMode(MouseMode.Free);
        Input.SetMouseVisible(true);

        Context.RegisterSubsystem(new DearImGui(Context, Engine.Parameters.DearImGuiFlags));

        // Execute base class startup
        var scene = new Scene();
        scene.CreateComponent<RenderPipeline>();
        scene.CreateComponent<Octree>();
        var cameraNode = scene.CreateChild("Camera");
        cameraNode.SetPosition(new Vector3f(0.0f, 0.0f, -4.0f));
        var camera = cameraNode.CreateComponent<Camera>();
        Renderer.SetViewport(0, new Viewport(scene, camera));

        // Create "Hello World" Text
        CreateText();

        // Finally subscribe to the update event. Note that by subscribing events at this point we have already missed some events
        // like the ScreenMode event sent by the Graphics subsystem when opening the application window. To catch those as well we
        // could subscribe in the constructor instead.
        SubscribeToEvents();
    }

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

    private void SubscribeToEvents()
    {
        _dearImGuiSubscription = Context.SubscribeToEvent<DearImGuiEvents.EndRenderingDearImGuiEvent>((ref DearImGuiEvents.EndRenderingDearImGuiEvent _) => DrawText());
    }

    protected override void Update(float timeStep)
    {
        // Do nothing for now, could be extended to eg. animate the display
    }

    protected override void Stop()
    {
        _dearImGuiSubscription?.Dispose();
        _dearImGuiSubscription = null;
        _helloText.Dispose();
    }

    private void DrawText()
    {
        if (ImGui.Begin("Hello World\0"u8, out _, ImGuiWindowFlags.ImGuiWindowFlagsNoResize | ImGuiWindowFlags.ImGuiWindowFlagsNoSavedSettings))
            ImGui.TextUnformatted(_helloText.Bytes);
        ImGui.End();
    }
}
